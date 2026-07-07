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

    // Collection cache untuk PropagateGridExplosion — zero GC allocation
    private static readonly List<PlacedModule> _tempSnapshot = new List<PlacedModule>();
    private static readonly Dictionary<Vector2Int, PlacedModule> _cellToModuleCache = new Dictionary<Vector2Int, PlacedModule>();
    private static readonly HashSet<Vector2Int> _visitedCellsCache = new HashSet<Vector2Int>();
    private static readonly HashSet<PlacedModule> _damagedModulesCache = new HashSet<PlacedModule>();
    private static readonly Queue<(Vector2Int pos, float dmg)> _propagationQueue = new Queue<(Vector2Int, float)>();
    private static readonly List<Vector2Int> _tempCells = new List<Vector2Int>();

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

            // HitboxProxy lookup — flat GetComponent, zero hierarchy traversal
            var proxy = col.GetComponent<HitboxProxy>();

            // Skip kendaraan penembak (self-damage prevention)
            var colVehicle = proxy != null ? proxy.statsManager : col.GetComponentInParent<VehicleStatsManager>();
            if (colVehicle != null && ownerVehicle != null && colVehicle == ownerVehicle)
                continue;

            // Skip modul dari targetVehicle (udah di-handle oleh grid propagation)
            if (targetVehicle != null && colVehicle != null && colVehicle == targetVehicle)
            {
                if (targetVehicle.moduleColliderMap.ContainsKey(col))
                    continue;
            }

            // Bukan kendaraan (terrain/tembok) — skip damage pipeline
            if (proxy == null && colVehicle == null)
                continue;

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

            // Damage ke Wheel (via HitboxProxy, fallback GetComponentInParent)
            var wheel = proxy != null ? proxy.wheelHealth : col.GetComponentInParent<WheelHealth>();
            if (wheel != null)
                wheel.TakeDamage(finalDamage);

            // Damage ke Critical Part (Engine, FuelTank, dll)
            var critPart = proxy != null ? proxy.criticalPart : col.GetComponentInParent<VehicleCriticalPart>();
            if (critPart != null)
                critPart.TakeDamage(finalDamage);

            // Damage ke SimpleTarget (test dummy, environment)
            var simpleTarget = proxy != null ? proxy.simpleTarget : col.GetComponentInParent<SimpleTarget>();
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

        // Build cell→module lookup table (dalam zona epicenter aja — O(1) lookup, aman dari concurrent mod)
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

        // Damage epicenter dengan full damage (dikurangi armor epicenter sendiri)
        float epicenterArmorReduction = epicenter.moduleTemplate.armor / (epicenter.moduleTemplate.armor + 100f);
        float epicenterDamage = maxDamage * (1f - epicenterArmorReduction);
        ApplyModuleDamage(epicenter, epicenterDamage);
        _damagedModulesCache.Add(epicenter);

        // Enqueue semua cell yang ditempati epicenter
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
                if (!_visitedCellsCache.Contains(neighborCell))
                    _propagationQueue.Enqueue((neighborCell, maxDamage * 0.5f));
            }
        }

        // BFS propagate ke tetangga
        while (_propagationQueue.Count > 0)
        {
            var (pos, dmg) = _propagationQueue.Dequeue();
            if (!_visitedCellsCache.Add(pos)) continue;
            if (dmg <= 0f) continue;

            // Cari modul dari lookup table (O(1), aman dari concurrent modification)
            if (_cellToModuleCache.TryGetValue(pos, out var mod))
            {
                if (mod.moduleTemplate == null || _damagedModulesCache.Contains(mod))
                    continue;

                // Apply damage dengan armor reduction
                float armorReduction = mod.moduleTemplate.armor / (mod.moduleTemplate.armor + 100f);
                float actualDamage = dmg * (1f - armorReduction);
                ApplyModuleDamage(mod, actualDamage);
                _damagedModulesCache.Add(mod);

                // Propagate ke tetangga cell modul ini dengan damage berkurang
                float neighborDmg = dmg * 0.5f;
                mgr.GetOccupiedCells(mod.gridPosition, mod.moduleTemplate.width, mod.moduleTemplate.height, mod.rotationAngle, _tempCells);
                int mcCount = _tempCells.Count;
                for (int mc = 0; mc < mcCount; mc++)
                {
                    var cell = _tempCells[mc];
                    for (int n = 0; n < _neighborOffsets.Length; n++)
                    {
                        var offset = _neighborOffsets[n];
                        Vector2Int nCell = new Vector2Int(cell.x + offset.x, cell.y + offset.y);
                        if (!_visitedCellsCache.Contains(nCell))
                            _propagationQueue.Enqueue((nCell, neighborDmg));
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
