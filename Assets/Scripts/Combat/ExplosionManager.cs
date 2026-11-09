using System.Collections.Generic;
using UnityEngine;

public static class ExplosionManager
{
    private static Collider[] _overlapCache = new Collider[256];

    // Armor menyerap blast sebagai persentase terhadap besarnya tekanan ledakan.
    // Makin besar damage mentah, makin kecil porsi yang bisa ditahan pelat baja.
    private const float BlastArmorScale = 0.15f;

    // Collection cache untuk Zero GC allocation
    private static readonly List<PlacedModule> _tempSnapshot = new List<PlacedModule>();
    private static readonly List<Collider> _tempColliders = new List<Collider>();
    private static readonly HashSet<PlacedModule> _damagedModulesCache = new HashSet<PlacedModule>();
    private static readonly HashSet<Rigidbody> _uniqueRigidbodies = new HashSet<Rigidbody>();

    /// <summary>
    /// Menghitung damage ledakan setelah mitigasi armor.
    /// Penetrasi tetap menentukan seberapa dalam ledakan masuk (bonus/penalti rasio PEN/DEF),
    /// lalu armor menyerap energi ledakan secara proporsional terhadap besarnya tekanan.
    /// Pelat baja menahan ledakan jarak jauh, tapi ledakan jarak dekat tetap menembus.
    /// </summary>
    public static float CalculateExplosionDamage(float rawDamage, float armor, float pen = 0f)
    {
        if (rawDamage <= 0f) return 0f;
        if (armor <= 0f) return rawDamage;

        // 1. Efek penetrasi (sama seperti sebelumnya, tidak mengubah stat senjata).
        float damageAfterPen = rawDamage;
        if (pen > 0f)
        {
            float ratio = pen / armor;
            if (ratio < 0.5f)
            {
                // Armor terlalu tebal untuk ditembus ledakan: hanya sedikit spalling/getaran (maks 10%)
                damageAfterPen = rawDamage * Mathf.Clamp01(ratio * 0.2f);
            }
            else if (ratio < 1.0f)
            {
                // Penetrasi parsial: damage tereduksi proporsional
                damageAfterPen = rawDamage * (ratio * 0.8f);
            }
            else
            {
                // Menembus armor: damage penuh (+ bonus jika pen jauh di atas armor)
                float bonus = Mathf.Clamp(ratio - 1f, 0f, 0.5f);
                damageAfterPen = rawDamage * (1f + bonus);
            }
        }

        // 2. Mitigasi armor berbasis tekanan.
        //    Dulu: rawDamage - armor (armor 10 tidak berarti apa-apa di damage 200-2000).
        //    Sekarang: armor / (armor + rawDamage * 0.15).
        //    Contoh armor 10 → 25% di damage 200, 12% di damage 500, 35% di damage 125.
        //    Contoh armor 30 (critical part) → 40% di damage 300, 29% di damage 500.
        float mitigation = armor / (armor + rawDamage * BlastArmorScale);
        return damageAfterPen * Mathf.Clamp01(1f - mitigation);
    }

    /// <summary>
    /// Kurva falloff ledakan tunggal (linear, angka tetap sama seperti sebelumnya).
    /// Dipakai grid propagation DAN world pass supaya keduanya konsisten.
    /// Jarak selalu Euclidean, bukan jumlah langkah BFS.
    /// </summary>
    private static float CalculateBlastDamage(float distance, float radius, float maxDamage)
    {
        if (radius <= 0f || distance > radius) return 0f;
        return maxDamage * Mathf.Clamp01(1f - (distance / radius));
    }

    public static void Detonate(
        Vector3 point,
        float radius,
        float maxDamage,
        float maxForce,
        VehicleStatsManager targetVehicle,
        LayerMask hitMask,
        VehicleStatsManager ownerVehicle = null,
        float penetration = 0f,
        float upwardsModifier = 1f
    )
    {
        int count = Physics.OverlapSphereNonAlloc(
            point, radius, _overlapCache, hitMask, QueryTriggerInteraction.Collide);

        // Auto-detect target vehicle jika null (misal roket meledak di tanah/near-miss)
        if (targetVehicle == null)
        {
            float closestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider c = _overlapCache[i];
                if (c == null) continue;
                var p = c.GetComponent<HitboxProxy>();
                var v = p != null ? p.statsManager : c.GetComponentInParent<VehicleStatsManager>();
                if (v != null && v != ownerVehicle)
                {
                    float d = Vector3.Distance(point, c.ClosestPoint(point));
                    if (d < closestDist)
                    {
                        closestDist = d;
                        targetVehicle = v;
                    }
                }
            }
        }

        // 1. Grid propagation — damage modul internal via jarak Euclidean & air shockwave
        _damagedModulesCache.Clear();
        if (targetVehicle != null)
            PropagateGridExplosion(targetVehicle, point, radius, maxDamage, penetration);

        // 2. World OverlapSphere — damage non-modul & modul sekunder/kendaraan lain
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapCache[i];
            if (col == null) continue;

            // HitboxProxy lookup — flat GetComponent, zero hierarchy traversal
            var proxy = col.GetComponent<HitboxProxy>();

            // Skip kendaraan penembak (self-damage prevention)
            var colVehicle = proxy != null ? proxy.statsManager : col.GetComponentInParent<VehicleStatsManager>();
            if (colVehicle != null && ownerVehicle != null && colVehicle == ownerVehicle)
                continue;

            // Skip modul dari targetVehicle (sudah di-handle oleh grid propagation)
            if (targetVehicle != null && colVehicle != null && colVehicle == targetVehicle)
            {
                if (targetVehicle.moduleColliderMap.ContainsKey(col))
                    continue;
            }

            // Bukan kendaraan / target yang bisa dirusak — skip
            if (proxy == null && colVehicle == null && col.GetComponentInParent<SimpleTarget>() == null)
                continue;

            Vector3 targetPoint = col.ClosestPoint(point);
            float distance = Vector3.Distance(point, targetPoint);
            if (distance > radius) continue;

            // Linecast occlusion — hanya rintangan eksternal (tembok/terrain) yang memblokir shockwave.
            // Collider milik kendaraan itu sendiri (misal bodi luar) TIDAK memblokir shockwave ke part internal!
            if (Physics.Linecast(point, targetPoint, out RaycastHit occlusionHit, hitMask))
            {
                if (occlusionHit.collider != col)
                {
                    var hitVehicle = occlusionHit.collider.GetComponentInParent<VehicleStatsManager>();
                    bool isSameVehicle = (colVehicle != null && hitVehicle == colVehicle) ||
                                         (occlusionHit.transform.root == col.transform.root);
                    if (!isSameVehicle)
                    {
                        // Terhalang objek luar/tembok lingkungan
                        continue;
                    }
                }
            }

            float rawDamage = CalculateBlastDamage(distance, radius, maxDamage);
            if (rawDamage <= 0f) continue;

            // Damage ke Modul pada kendaraan sekunder (atau jika targetVehicle null)
            if (colVehicle != null && colVehicle.moduleColliderMap.TryGetValue(col, out var placedMod))
            {
                if (placedMod != null && placedMod.moduleTemplate != null
                    && _damagedModulesCache.Add(placedMod))
                {
                    float modDamage = CalculateExplosionDamage(rawDamage, placedMod.moduleTemplate.armor, penetration);
                    ApplyModuleDamage(placedMod, modDamage);
                }
                continue;
            }

            // Damage ke Wheel (mitigasi armor roda)
            var wheel = proxy != null ? proxy.wheelHealth : col.GetComponentInParent<WheelHealth>();
            if (wheel != null)
            {
                float wheelDamage = CalculateExplosionDamage(rawDamage, wheel.armor, penetration);
                wheel.TakeDamage(wheelDamage);
                continue;
            }

            // Damage ke Critical Part (Engine, FuelTank, dll dengan mitigasi armor part)
            var critPart = proxy != null ? proxy.criticalPart : col.GetComponentInParent<VehicleCriticalPart>();
            if (critPart != null)
            {
                float critDamage = CalculateExplosionDamage(rawDamage, critPart.armor, penetration);
                critPart.TakeDamage(critDamage);
                continue;
            }

            // Damage ke SimpleTarget (test dummy, environment)
            var simpleTarget = proxy != null ? proxy.simpleTarget : col.GetComponentInParent<SimpleTarget>();
            if (simpleTarget != null)
            {
                simpleTarget.TakeDamage(rawDamage, penetration, 0f);
            }
        }

        // 3. Explosion force ke semua Rigidbody di radius (FIX: HashSet filter anti-NASA space program!)
        _uniqueRigidbodies.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapCache[i];
            if (col == null || col.attachedRigidbody == null) continue;
            if (_uniqueRigidbodies.Add(col.attachedRigidbody))
            {
                col.attachedRigidbody.AddExplosionForce(maxForce, point, radius, upwardsModifier, ForceMode.Impulse);
            }
        }
    }

    private static void PropagateGridExplosion(VehicleStatsManager mgr, Vector3 explosionPoint, float radius, float maxDamage, float penetration)
    {
        if (mgr == null || mgr.installedModules.Count == 0) return;

        // Snapshot dulu — damage bisa memicu uninstall/ledakan berantai yang
        // memodifikasi installedModules di tengah iterasi.
        _tempSnapshot.Clear();
        _tempSnapshot.AddRange(mgr.installedModules);

        int snapshotCount = _tempSnapshot.Count;
        for (int i = 0; i < snapshotCount; i++)
        {
            PlacedModule mod = _tempSnapshot[i];
            if (mod == null || mod.moduleTemplate == null) continue;

            // Modul bisa sudah di-uninstall oleh ledakan berantai sebelumnya.
            if (!mgr.installedModules.Contains(mod)) continue;

            // Tidak ada batas zona di sini: SEMUA zona (Roof, Hull, Hood, internal, dll)
            // dihitung dengan jarak Euclidean yang sama, jadi shockwave bisa menembus
            // batas zona dan bentuknya bola — bukan belah ketupat ala BFS 4-neighbor.
            float distance = GetClosestModuleDistance(mod, explosionPoint);
            float rawDamage = CalculateBlastDamage(distance, radius, maxDamage);
            if (rawDamage <= 0f) continue;

            if (!_damagedModulesCache.Add(mod)) continue;

            float actualDamage = CalculateExplosionDamage(rawDamage, mod.moduleTemplate.armor, penetration);
            ApplyModuleDamage(mod, actualDamage);
        }
    }

    /// <summary>
    /// Jarak Euclidean terdekat dari titik ledakan ke salah satu collider modul.
    /// Menggantikan hitungan langkah BFS yang bikin radius berbentuk belah ketupat.
    /// </summary>
    private static float GetClosestModuleDistance(PlacedModule mod, Vector3 point)
    {
        if (mod.spawnedPrefab == null) return float.MaxValue;

        _tempColliders.Clear();
        mod.spawnedPrefab.GetComponentsInChildren(true, _tempColliders);

        float closest = float.MaxValue;
        for (int i = 0; i < _tempColliders.Count; i++)
        {
            Collider col = _tempColliders[i];
            if (col == null || !col.enabled) continue;

            float d = Vector3.Distance(point, col.ClosestPoint(point));
            if (d < closest) closest = d;
        }

        // Fallback modul tanpa collider aktif: pakai pivot prefab.
        if (closest == float.MaxValue)
            closest = Vector3.Distance(point, mod.spawnedPrefab.transform.position);

        return closest;
    }

    private static void ApplyModuleDamage(PlacedModule mod, float damage)
    {
        if (damage <= 0f) return;

        VehicleModuleComponent comp = mod.spawnedPrefab != null
            ? mod.spawnedPrefab.GetComponent<VehicleModuleComponent>()
            : null;

        if (comp != null)
            comp.TakeDamage(damage);
        else
            mod.currentHealth -= damage;
    }
}
