using System.Collections.Generic;
using UnityEngine;

public static class ExplosionManager
{
    private static Collider[] _overlapCache = new Collider[256];
    private static readonly Vector2Int[] _neighborOffsets = new Vector2Int[]
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    // Collection cache untuk Zero GC allocation
    private static readonly List<PlacedModule> _tempSnapshot = new List<PlacedModule>();
    private static readonly Dictionary<Vector2Int, PlacedModule> _cellToModuleCache = new Dictionary<Vector2Int, PlacedModule>();
    private static readonly HashSet<Vector2Int> _visitedCellsCache = new HashSet<Vector2Int>();
    private static readonly HashSet<PlacedModule> _damagedModulesCache = new HashSet<PlacedModule>();
    private static readonly Queue<(Vector2Int pos, float energy)> _propagationQueue = new Queue<(Vector2Int, float)>();
    private static readonly List<Vector2Int> _tempCells = new List<Vector2Int>();
    private static readonly HashSet<Rigidbody> _uniqueRigidbodies = new HashSet<Rigidbody>();

    /// <summary>
    /// Menghitung damage ledakan setelah mitigasi armor (konsisten dengan sistem armor War Thunder / DEF).
    /// Jika pen > 0, menggunakan rasio penetrasi ledakan terhadap DEF.
    /// Jika pen <= 0, menggunakan flat absorption di mana armor bertindak sebagai pelindung penyerap energi.
    /// </summary>
    public static float CalculateExplosionDamage(float rawDamage, float armor, float pen = 0f)
    {
        if (rawDamage <= 0f) return 0f;
        if (armor <= 0f) return rawDamage;

        if (pen > 0f)
        {
            float ratio = pen / armor;
            if (ratio < 0.5f)
            {
                // Armor terlalu tebal untuk ditembus ledakan: hanya sedikit spalling/getaran (maks 10%)
                return rawDamage * Mathf.Clamp01(ratio * 0.2f);
            }
            else if (ratio < 1.0f)
            {
                // Penetrasi parsial: damage tereduksi proporsional
                return rawDamage * (ratio * 0.8f);
            }
            else
            {
                // Menembus armor: damage penuh (+ bonus jika pen jauh di atas armor)
                float bonus = Mathf.Clamp(ratio - 1f, 0f, 0.5f);
                return rawDamage * (1f + bonus);
            }
        }
        else
        {
            // Flat absorption: 1 poin armor menyerap 1 poin damage ledakan
            return Mathf.Max(0f, rawDamage - armor);
        }
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

        // 1. Grid propagation — damage modul internal via grid adjacency & air shockwave
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

            float falloff = Mathf.Clamp01(1f - (distance / radius));
            float rawDamage = maxDamage * falloff;
            if (rawDamage <= 0f) continue;

            // Damage ke Modul pada kendaraan sekunder (atau jika targetVehicle null)
            if (colVehicle != null && colVehicle.moduleColliderMap.TryGetValue(col, out var placedMod))
            {
                if (placedMod != null && placedMod.moduleTemplate != null)
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
        int moduleCount = mgr.installedModules.Count;
        if (moduleCount == 0) return;

        // Reset semua cache collection
        _tempSnapshot.Clear();
        _cellToModuleCache.Clear();
        _visitedCellsCache.Clear();
        _damagedModulesCache.Clear();
        _propagationQueue.Clear();

        // Snapshot via cached List — zero allocation, aman dari concurrent mod
        _tempSnapshot.AddRange(mgr.installedModules);

        // Cari modul terdekat dari titik ledakan (epicenter)
        PlacedModule epicenter = null;
        float closestDist = float.MaxValue;

        int snapshotCount = _tempSnapshot.Count;
        for (int i = 0; i < snapshotCount; i++)
        {
            var mod = _tempSnapshot[i];
            if (mod.spawnedPrefab == null || mod.moduleTemplate == null) continue;
            Collider modCol = mod.spawnedPrefab.GetComponentInChildren<Collider>();
            if (modCol == null) continue;

            float dist = Vector3.Distance(explosionPoint, modCol.ClosestPoint(explosionPoint));
            if (dist < closestDist)
            {
                closestDist = dist;
                epicenter = mod;
            }
        }

        if (epicenter == null) return;

        // Ambil data zona epicenter dan ukuran cell-nya
        GridZone targetZone = null;
        foreach (var z in mgr.gridZones)
        {
            if (z != null && z.zoneName == epicenter.zoneName)
            {
                targetZone = z;
                break;
            }
        }

        float zoneCellSize = GetCellSizeForZone(mgr, epicenter.zoneName);
        int maxCellSteps = Mathf.Max(1, Mathf.RoundToInt(radius / zoneCellSize));
        float airAttenuationPerCell = maxDamage / (maxCellSteps + 1);

        // Build cell→module lookup table (dalam zona epicenter aja — O(1) lookup)
        for (int i = 0; i < snapshotCount; i++)
        {
            var mod = _tempSnapshot[i];
            if (mod.moduleTemplate == null || mod.zoneName != epicenter.zoneName) continue;
            mgr.GetOccupiedCells(mod.gridPosition, mod.moduleTemplate.width, mod.moduleTemplate.height, mod.rotationAngle, _tempCells);
            int cellCount = _tempCells.Count;
            for (int j = 0; j < cellCount; j++)
            {
                var cell = _tempCells[j];
                if (!_cellToModuleCache.ContainsKey(cell))
                    _cellToModuleCache[cell] = mod;
            }
        }

        // Damage epicenter dengan formula armor terpadu
        float epicenterDist = Mathf.Min(closestDist, radius);
        float epFalloff = Mathf.Clamp01(1f - (epicenterDist / radius));
        float rawEpicenterDmg = maxDamage * epFalloff;
        float actualEpicenterDmg = CalculateExplosionDamage(rawEpicenterDmg, epicenter.moduleTemplate.armor, penetration);
        ApplyModuleDamage(epicenter, actualEpicenterDmg);
        _damagedModulesCache.Add(epicenter);

        // Energi ledakan yang merambat keluar dari epicenter (dikurangi serapan armor epicenter)
        float initialPropagationEnergy = Mathf.Max(0f, (rawEpicenterDmg - (epicenter.moduleTemplate.armor * 0.5f)) * 0.75f);

        // Enqueue semua tetangga cell yang ditempati epicenter
        mgr.GetOccupiedCells(epicenter.gridPosition, epicenter.moduleTemplate.width, epicenter.moduleTemplate.height, epicenter.rotationAngle, _tempCells);
        int epCellCount = _tempCells.Count;
        for (int c = 0; c < epCellCount; c++)
        {
            var cell = _tempCells[c];
            _visitedCellsCache.Add(cell);
            for (int n = 0; n < _neighborOffsets.Length; n++)
            {
                var offset = _neighborOffsets[n];
                Vector2Int neighborCell = new Vector2Int(cell.x + offset.x, cell.y + offset.y);
                if (targetZone != null && (neighborCell.x < 0 || neighborCell.x >= targetZone.capacity.x || neighborCell.y < 0 || neighborCell.y >= targetZone.capacity.y))
                    continue;
                if (!_visitedCellsCache.Contains(neighborCell))
                    _propagationQueue.Enqueue((neighborCell, initialPropagationEnergy));
            }
        }

        // BFS propagate gelombang kejut (merambat menembus cell kosong udara maupun modul)
        while (_propagationQueue.Count > 0)
        {
            var (pos, energy) = _propagationQueue.Dequeue();
            if (!_visitedCellsCache.Add(pos)) continue;
            if (energy <= 1f) continue;

            float nextEnergy = energy;

            // Jika cell berisi modul
            if (_cellToModuleCache.TryGetValue(pos, out var mod))
            {
                if (mod.moduleTemplate != null && !_damagedModulesCache.Contains(mod))
                {
                    // Apply damage dengan mitigasi armor
                    float actualDamage = CalculateExplosionDamage(energy, mod.moduleTemplate.armor, penetration);
                    ApplyModuleDamage(mod, actualDamage);
                    _damagedModulesCache.Add(mod);

                    // Modul menyerap gelombang kejut (armor tebal meredam lebih banyak)
                    float absorption = mod.moduleTemplate.armor * 0.3f;
                    nextEnergy = Mathf.Max(0f, (energy - absorption) * 0.6f);
                }
                else
                {
                    nextEnergy = energy * 0.6f;
                }

                // Tandai seluruh cell modul ini sebagai visited
                mgr.GetOccupiedCells(mod.gridPosition, mod.moduleTemplate.width, mod.moduleTemplate.height, mod.rotationAngle, _tempCells);
                int mcCount = _tempCells.Count;
                for (int mc = 0; mc < mcCount; mc++)
                {
                    _visitedCellsCache.Add(_tempCells[mc]);
                }

                // Enqueue perimeter luar modul ini jika masih ada energi ledakan
                if (nextEnergy > 1f)
                {
                    for (int mc = 0; mc < mcCount; mc++)
                    {
                        var cell = _tempCells[mc];
                        for (int n = 0; n < _neighborOffsets.Length; n++)
                        {
                            var offset = _neighborOffsets[n];
                            Vector2Int nCell = new Vector2Int(cell.x + offset.x, cell.y + offset.y);
                            if (targetZone != null && (nCell.x < 0 || nCell.x >= targetZone.capacity.x || nCell.y < 0 || nCell.y >= targetZone.capacity.y))
                                continue;
                            if (!_visitedCellsCache.Contains(nCell))
                                _propagationQueue.Enqueue((nCell, nextEnergy));
                        }
                    }
                }
            }
            else
            {
                // CELL KOSONG (UDARA): Shockwave merambat bebas!
                // Atenuasi hanya terjadi karena perambatan jarak di udara
                nextEnergy = Mathf.Max(0f, energy - airAttenuationPerCell);

                if (nextEnergy > 1f)
                {
                    for (int n = 0; n < _neighborOffsets.Length; n++)
                    {
                        var offset = _neighborOffsets[n];
                        Vector2Int nCell = new Vector2Int(pos.x + offset.x, pos.y + offset.y);
                        if (targetZone != null && (nCell.x < 0 || nCell.x >= targetZone.capacity.x || nCell.y < 0 || nCell.y >= targetZone.capacity.y))
                            continue;
                        if (!_visitedCellsCache.Contains(nCell))
                            _propagationQueue.Enqueue((nCell, nextEnergy));
                    }
                }
            }
        }
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

    private static float GetCellSizeForZone(VehicleStatsManager mgr, string zoneName)
    {
        foreach (var zone in mgr.gridZones)
        {
            if (zone != null && zone.zoneName == zoneName && zone.cellSize > 0f)
                return zone.cellSize;
        }
        return 0.25f;
    }
}
