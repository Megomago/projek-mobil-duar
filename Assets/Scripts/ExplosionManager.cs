using System.Collections.Generic;
using UnityEngine;

public static class ExplosionManager
{
    private static Collider[] _overlapCache = new Collider[64];
    private static readonly Vector2Int[] _neighborOffsets = new Vector2Int[]
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    public static void Detonate(
        Vector3 point,
        float radius,
        float maxDamage,
        float maxForce,
        VehicleStatsManager targetVehicle,
        LayerMask hitMask,
        VehicleStatsManager ownerVehicle = null
    )
    {
        // 1. Grid propagation — damage modul internal via grid adjacency
        if (targetVehicle != null)
            PropagateGridExplosion(targetVehicle, point, maxDamage);

        // 2. World OverlapSphere — damage non-modul (roda, part kritikal, target eksternal)
        int count = Physics.OverlapSphereNonAlloc(point, radius, _overlapCache, hitMask);
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapCache[i];
            if (col == null) continue;

            // Skip kendaraan penembak (self-damage prevention)
            var colVehicle = col.GetComponentInParent<VehicleStatsManager>();
            if (colVehicle != null && ownerVehicle != null && colVehicle == ownerVehicle)
                continue;

            // Skip modul dari targetVehicle (udah di-handle oleh grid propagation)
            if (targetVehicle != null && colVehicle != null && colVehicle == targetVehicle)
            {
                if (targetVehicle.moduleColliderMap.ContainsKey(col))
                    continue;
            }

            Vector3 targetPoint = col.ClosestPoint(point);
            float distance = Vector3.Distance(point, targetPoint);
            if (distance > radius) continue;

            // Linecast occlusion — cek apakah ada penghalang di antara pusat ledakan dan target
            if (Physics.Linecast(point, targetPoint, out RaycastHit occlusionHit, hitMask))
            {
                if (occlusionHit.collider != col)
                    continue;
            }

            float falloff = 1f - (distance / radius);
            falloff = Mathf.Clamp01(falloff);
            float finalDamage = maxDamage * falloff;

            // Damage ke Wheel (pake GetComponentInParent — WheelHealth ada di parent, collider di child)
            var wheel = col.GetComponentInParent<WheelHealth>();
            if (wheel != null)
                wheel.TakeDamage(finalDamage);

            // Damage ke Critical Part (Engine, FuelTank, dll)
            var critPart = col.GetComponentInParent<VehicleCriticalPart>();
            if (critPart != null)
                critPart.TakeDamage(finalDamage);

            // Damage ke SimpleTarget (test dummy, environment)
            var simpleTarget = col.GetComponentInParent<SimpleTarget>();
            if (simpleTarget != null)
                simpleTarget.TakeDamage(finalDamage, 0f, 0f);
        }

        // 3. Explosion force ke semua Rigidbody di radius
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapCache[i];
            if (col == null || col.attachedRigidbody == null) continue;
            col.attachedRigidbody.AddExplosionForce(maxForce, point, radius, 1f, ForceMode.Impulse);
        }
    }

    private static void PropagateGridExplosion(VehicleStatsManager mgr, Vector3 explosionPoint, float maxDamage)
    {
        int moduleCount = mgr.installedModules.Count;
        if (moduleCount == 0) return;

        // Snapshot: ambil semua modul + grid position-nya SEKARANG
        // Biar aman dari concurrent modification (UninstallModule pas BFS jalan)
        PlacedModule[] snapshot = mgr.installedModules.ToArray();

        // Cari modul terdekat dari titik ledakan (epicenter)
        PlacedModule epicenter = null;
        float closestDist = float.MaxValue;

        foreach (var mod in snapshot)
        {
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

        // Build cell→module lookup table (dalam zona epicenter aja — O(1) lookup, aman dari concurrent mod)
        Dictionary<Vector2Int, PlacedModule> cellToModule = new Dictionary<Vector2Int, PlacedModule>();
        foreach (var mod in snapshot)
        {
            if (mod.moduleTemplate == null || mod.zoneName != epicenter.zoneName) continue;
            List<Vector2Int> cells = mgr.GetOccupiedCells(mod.gridPosition, mod.moduleTemplate.width, mod.moduleTemplate.height, mod.rotationAngle);
            foreach (var cell in cells)
            {
                if (!cellToModule.ContainsKey(cell))
                    cellToModule[cell] = mod;
            }
        }

        HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();
        HashSet<PlacedModule> damagedModules = new HashSet<PlacedModule>();
        Queue<(Vector2Int pos, float dmg)> queue = new Queue<(Vector2Int, float)>();

        // Damage epicenter dengan full damage (dikurangi armor epicenter sendiri)
        float epicenterArmorReduction = epicenter.moduleTemplate.armor / (epicenter.moduleTemplate.armor + 100f);
        float epicenterDamage = maxDamage * (1f - epicenterArmorReduction);
        ApplyModuleDamage(epicenter, epicenterDamage);
        damagedModules.Add(epicenter);

        // Enqueue semua cell yang ditempati epicenter
        List<Vector2Int> epicenterCells = mgr.GetOccupiedCells(epicenter.gridPosition, epicenter.moduleTemplate.width, epicenter.moduleTemplate.height, epicenter.rotationAngle);
        foreach (var cell in epicenterCells)
        {
            visitedCells.Add(cell);
            foreach (var offset in _neighborOffsets)
            {
                Vector2Int neighborCell = new Vector2Int(cell.x + offset.x, cell.y + offset.y);
                if (!visitedCells.Contains(neighborCell))
                    queue.Enqueue((neighborCell, maxDamage * 0.5f));
            }
        }

        // BFS propagate ke tetangga
        while (queue.Count > 0)
        {
            var (pos, dmg) = queue.Dequeue();
            if (!visitedCells.Add(pos)) continue;
            if (dmg <= 0f) continue;

            // Cari modul dari lookup table (O(1), aman dari concurrent modification)
            if (cellToModule.TryGetValue(pos, out var mod))
            {
                if (mod.moduleTemplate == null || damagedModules.Contains(mod))
                    continue;

                // Apply damage dengan armor reduction
                float armorReduction = mod.moduleTemplate.armor / (mod.moduleTemplate.armor + 100f);
                float actualDamage = dmg * (1f - armorReduction);
                ApplyModuleDamage(mod, actualDamage);
                damagedModules.Add(mod);

                // Propagate ke tetangga cell modul ini dengan damage berkurang
                float neighborDmg = dmg * 0.5f;
                List<Vector2Int> modCells = mgr.GetOccupiedCells(mod.gridPosition, mod.moduleTemplate.width, mod.moduleTemplate.height, mod.rotationAngle);
                foreach (var cell in modCells)
                {
                    foreach (var offset in _neighborOffsets)
                    {
                        Vector2Int nCell = new Vector2Int(cell.x + offset.x, cell.y + offset.y);
                        if (!visitedCells.Contains(nCell))
                            queue.Enqueue((nCell, neighborDmg));
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
