using System.Collections.Generic;
using UnityEngine;
using Weapons;

[System.Serializable]
public class GridZone
{
    [Tooltip("Nama unik zona ini (misal: 'Roof', 'Hood'). JANGAN ADA YANG SAMA!")]
    public string zoneName = "NewZone";

    [Tooltip("Titik awal grid zona ini")]
    public Transform origin;

    [Tooltip("Kapasitas grid zona ini (X, Y)")]
    public Vector2Int capacity = new Vector2Int(4, 4);

    [Tooltip("Ukuran 1 cell di zona ini")]
    public float cellSize = 0.25f;

    [Tooltip("Centang jika zona ini terkena angin (luar sasis)")]
    public bool affectDrag = true;
}

[System.Serializable]
public class PlacedModule
{
    public ModuleTemplate moduleTemplate;
    public string zoneName;

    [Tooltip("Posisi koordinat X, Y di grid")]
    public Vector2Int gridPosition;

    [Tooltip("Rotasi modul dalam derajat (0, 90, 180, 270)")]
    public int rotationAngle;

    [HideInInspector]
    public float currentHealth;

    [HideInInspector]
    public GameObject spawnedPrefab;

    [HideInInspector]
    public float currentAmmoPoint;

    public PlacedModule(ModuleTemplate template, string zone, Vector2Int position, int angle)
    {
        moduleTemplate = template;
        zoneName = zone;
        gridPosition = position;
        rotationAngle = angle;
        if (template != null)
        {
            currentHealth = template.maxHealth;
            currentAmmoPoint = template.ammoPoint;
        }
    }
}

[System.Serializable]
public class ModulePresetEntry
{
    public ModuleTemplate template;
    public string zoneName;
    public Vector2Int gridPosition;
    public int rotationAngle;
}

public class VehicleGridSystem : MonoBehaviour
{
    [Header("Modular Grid Setup")]
    [Tooltip("Daftar semua zona grid di kendaraan ini (Atap, Kap, dll)")]
    public List<GridZone> gridZones = new List<GridZone>();

    [Header("Default Module Presets (auto-install jika gak ada save)")]
    public List<ModulePresetEntry> defaultModulePresets = new List<ModulePresetEntry>();

    [Header("Installed Modules")]
    public List<PlacedModule> installedModules = new List<PlacedModule>();

    [HideInInspector]
    public Dictionary<Collider, PlacedModule> moduleColliderMap = new Dictionary<Collider, PlacedModule>();

    private VehicleStatsManager _statsManager;

    // Cache collection untuk IsAreaFree — zero allocation di grid edit
    private readonly List<Vector2Int> _tempBaseCellsA = new List<Vector2Int>();
    private readonly List<Vector2Int> _tempBaseCellsB = new List<Vector2Int>();
    private readonly List<Vector2Int> _tempClearanceCellsA = new List<Vector2Int>();
    private readonly List<Vector2Int> _tempClearanceCellsB = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> _tempBaseSetA = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> _tempClearanceSetA = new HashSet<Vector2Int>();

    void Awake()
    {
        _statsManager = GetComponent<VehicleStatsManager>();
    }

    void Start()
    {
        // Priority 1: presets (kalau sudah ada, skip)
        if (defaultModulePresets.Count > 0 && installedModules.Count == 0)
        {
            InstallDefaultPresets();
        }

        // Priority 2: baked prefab — scan hierarchy untuk modul anak
        if (installedModules.Count == 0)
        {
            RebuildColliderMapFromHierarchy();
        }
    }

    [ContextMenu("Install Default Presets")]
    public void InstallDefaultPresets()
    {
        foreach (var entry in defaultModulePresets)
        {
            if (entry.template == null) continue;
            InstallModule(entry.template, entry.zoneName, entry.gridPosition, entry.rotationAngle);
        }
        if (_statsManager != null)
            _statsManager.MarkStatsDirty();
    }

    [ContextMenu("Rebuild Module Collider Map From Hierarchy (Baked Prefab)")]
    public void RebuildColliderMapFromHierarchy()
    {
        bool anyFound = false;

        foreach (var zone in gridZones)
        {
            if (zone == null || zone.origin == null) continue;

            // Scan semua child di zone origin
            for (int i = 0; i < zone.origin.childCount; i++)
            {
                Transform child = zone.origin.GetChild(i);
                var modComp = child.GetComponent<VehicleModuleComponent>();
                if (modComp == null) continue;

                ModuleTemplate template = modComp.moduleTemplate != null ? modComp.moduleTemplate :
                    (modComp.placedModuleData != null ? modComp.placedModuleData.moduleTemplate : null);
                if (template == null) continue;

                PlacedModule pm = new PlacedModule(template, zone.zoneName, modComp.bakedGridPosition, modComp.bakedRotationAngle);
                pm.spawnedPrefab = child.gameObject;
                pm.currentHealth = template.maxHealth;

                // Register colliders
                Collider[] cols = child.GetComponentsInChildren<Collider>(true);
                foreach (var col in cols)
                {
                    moduleColliderMap[col] = pm;

                    HitboxProxy proxy = col.gameObject.GetComponent<HitboxProxy>();
                    if (proxy == null)
                        proxy = col.gameObject.AddComponent<HitboxProxy>();
                    proxy.moduleComponent = modComp;
                    proxy.statsManager = _statsManager;
                }

                modComp.Initialize(pm, _statsManager);
                installedModules.Add(pm);
                anyFound = true;
            }
        }

        if (anyFound && _statsManager != null)
            _statsManager.MarkStatsDirty();

        #if UNITY_EDITOR
        Debug.Log($"[VehicleGridSystem] Rebuild selesai: {installedModules.Count} modul didaftarkan dari hierarchy.");
        #endif
    }

    public void GetOccupiedCells(Vector2Int position, int width, int height, int angle, List<Vector2Int> dest)
    {
        dest.Clear();
        int effectiveWidth = (angle == 90 || angle == 270) ? height : width;
        int effectiveHeight = (angle == 90 || angle == 270) ? width : height;

        for (int x = 0; x < effectiveWidth; x++)
        {
            for (int y = 0; y < effectiveHeight; y++)
            {
                dest.Add(new Vector2Int(position.x + x, position.y + y));
            }
        }
    }

    public void GetClearanceCells(Vector2Int position, ModuleTemplate template, int angle, List<Vector2Int> dest)
    {
        dest.Clear();
        if (template == null || !template.enableClearance) return;

        int effectiveWidth = (angle == 90 || angle == 270) ? template.height : template.width;
        int effectiveHeight = (angle == 90 || angle == 270) ? template.width : template.height;

        int up = 0, down = 0, left = 0, right = 0;
        if (angle == 0) { up = template.clearanceFront; down = template.clearanceBack; right = template.clearanceRight; left = template.clearanceLeft; }
        else if (angle == 90) { right = template.clearanceFront; left = template.clearanceBack; down = template.clearanceRight; up = template.clearanceLeft; }
        else if (angle == 180) { down = template.clearanceFront; up = template.clearanceBack; left = template.clearanceRight; right = template.clearanceLeft; }
        else if (angle == 270) { left = template.clearanceFront; right = template.clearanceBack; up = template.clearanceRight; down = template.clearanceLeft; }

        int minX = position.x - left;
        int maxX = position.x + effectiveWidth - 1 + right;
        int minY = position.y - down;
        int maxY = position.y + effectiveHeight - 1 + up;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                bool isBase = (x >= position.x && x < position.x + effectiveWidth) && (y >= position.y && y < position.y + effectiveHeight);
                if (!isBase)
                    dest.Add(new Vector2Int(x, y));
            }
        }
    }

    private bool HasIntersection(HashSet<Vector2Int> set, List<Vector2Int> list)
    {
        foreach (var a in list)
        {
            if (set.Contains(a)) return true;
        }
        return false;
    }

    public bool IsAreaFree(GridZone zone, Vector2Int position, ModuleTemplate templateToPlace, int angle, PlacedModule ignoreModule = null)
    {
        if (zone == null || templateToPlace == null) return false;

        GetOccupiedCells(position, templateToPlace.width, templateToPlace.height, angle, _tempBaseCellsA);
        GetClearanceCells(position, templateToPlace, angle, _tempClearanceCellsA);

        int baseCountA = _tempBaseCellsA.Count;
        for (int i = 0; i < baseCountA; i++)
        {
            var cell = _tempBaseCellsA[i];
            if (cell.x < 0 || cell.x >= zone.capacity.x || cell.y < 0 || cell.y >= zone.capacity.y)
                return false;
        }

        // Build HashSet for O(1) lookups
        _tempBaseSetA.Clear();
        foreach (var c in _tempBaseCellsA) _tempBaseSetA.Add(c);
        _tempClearanceSetA.Clear();
        foreach (var c in _tempClearanceCellsA) _tempClearanceSetA.Add(c);

        foreach (var mod in installedModules)
        {
            if (mod == ignoreModule) continue;
            if (mod.moduleTemplate == null) continue;
            if (mod.zoneName != zone.zoneName) continue;

            GetOccupiedCells(mod.gridPosition, mod.moduleTemplate.width, mod.moduleTemplate.height, mod.rotationAngle, _tempBaseCellsB);
            GetClearanceCells(mod.gridPosition, mod.moduleTemplate, mod.rotationAngle, _tempClearanceCellsB);

            if (HasIntersection(_tempBaseSetA, _tempBaseCellsB)) return false;

            if (HasIntersection(_tempBaseSetA, _tempClearanceCellsB))
            {
                if (!templateToPlace.isSmall || !mod.moduleTemplate.enableAccessClearance) return false;
            }

            if (HasIntersection(_tempClearanceSetA, _tempBaseCellsB))
            {
                if (!mod.moduleTemplate.isSmall || !templateToPlace.enableAccessClearance) return false;
            }
        }
        return true;
    }

    public PlacedModule InstallModule(ModuleTemplate template, string targetZoneName, Vector2Int position, int angle)
    {
        if (template == null) return null;

        GridZone targetZone = null;
        foreach (var z in gridZones)
        {
            if (z == null) continue;
            if (z.zoneName == targetZoneName) { targetZone = z; break; }
        }

        if (targetZone == null || targetZone.origin == null)
        {
            Debug.LogError("[VehicleGridSystem] Zona grid tidak ditemukan atau origin-nya null!");
            return null;
        }

        if (!IsAreaFree(targetZone, position, template, angle))
        {
            Debug.LogWarning($"[VehicleGridSystem] Gagal memasang {template.moduleName} di zona {targetZoneName} posisi {position} karena area penuh atau di luar batas.");
            return null;
        }

        PlacedModule newModule = new PlacedModule(template, targetZoneName, position, angle);

        GameObject prefabToSpawn = template.modulePrefab;
        if (template.moduleType == ModuleType.Weapon && template.weaponData != null && template.weaponData.weapon3DPrefab != null)
        {
            prefabToSpawn = template.weaponData.weapon3DPrefab;
        }

        if (prefabToSpawn != null && targetZone.origin != null)
        {
            int effectiveWidth = (angle == 90 || angle == 270) ? template.height : template.width;
            int effectiveHeight = (angle == 90 || angle == 270) ? template.width : template.height;

            float zoneCellSize = (targetZone.cellSize > 0f) ? targetZone.cellSize : 0.25f;
            float offsetX = (position.x + effectiveWidth / 2f) * zoneCellSize;
            float offsetZ = (position.y + effectiveHeight / 2f) * zoneCellSize;

            Vector3 localPos = new Vector3(offsetX, 0f, offsetZ);
            Vector3 worldPos = targetZone.origin.TransformPoint(localPos);

            Quaternion rotation = targetZone.origin.rotation * Quaternion.Euler(0f, angle, 0f);

            GameObject spawned = Instantiate(prefabToSpawn, worldPos, rotation, targetZone.origin);
            newModule.spawnedPrefab = spawned;

            int moduleLayer = LayerMask.NameToLayer("PlacedModule");
            if (moduleLayer != -1)
                SetLayerRecursively(spawned, moduleLayer);

            VehicleModuleComponent moduleComp = spawned.GetComponent<VehicleModuleComponent>();

            Collider[] modColliders = spawned.GetComponentsInChildren<Collider>(true);
            foreach (var col in modColliders)
            {
                moduleColliderMap[col] = newModule;

                HitboxProxy proxy = col.gameObject.GetComponent<HitboxProxy>();
                if (proxy == null)
                    proxy = col.gameObject.AddComponent<HitboxProxy>();
                proxy.moduleComponent = moduleComp;
                proxy.statsManager = _statsManager;
            }
            if (moduleComp != null)
                moduleComp.Initialize(newModule, _statsManager);

            if (_statsManager != null && _statsManager.isPreviewMode)
            {
                ManualTurretController[] newTurrets = spawned.GetComponentsInChildren<ManualTurretController>(true);
                foreach (var turret in newTurrets)
                    turret.enabled = false;

                Animator[] newAnimators = spawned.GetComponentsInChildren<Animator>(true);
                foreach (var anim in newAnimators)
                    anim.enabled = false;

                Rigidbody[] newRbs = spawned.GetComponentsInChildren<Rigidbody>(true);
                foreach (var rb in newRbs)
                {
                    if (rb != spawned.GetComponentInParent<Rigidbody>())
                    {
                        rb.isKinematic = true;
                        rb.constraints = RigidbodyConstraints.FreezeAll;
                    }
                }
            }
        }

        installedModules.Add(newModule);

        if (_statsManager != null)
            _statsManager.MarkStatsDirty();

        // Sinkronkan HUD senjata (kalau kendaraan sedang dikendarai)
        var weaponTrigger = GetComponent<VehicleGridWeaponTrigger>();
        if (weaponTrigger != null)
            weaponTrigger.RebuildWeaponHUDs();

        return newModule;
    }

    public void UninstallModule(PlacedModule module)
    {
        if (installedModules.Contains(module))
        {
            if (module.spawnedPrefab != null)
            {
                Collider[] modColliders = module.spawnedPrefab.GetComponentsInChildren<Collider>(true);
                foreach (var col in modColliders)
                    moduleColliderMap.Remove(col);
            }

            if (module.spawnedPrefab != null)
                Destroy(module.spawnedPrefab);

            installedModules.Remove(module);

            if (_statsManager != null)
                _statsManager.MarkStatsDirty();

            // HUD senjata yang di-uninstall ikut dibersihkan
            var weaponTrigger = GetComponent<VehicleGridWeaponTrigger>();
            if (weaponTrigger != null)
                weaponTrigger.RebuildWeaponHUDs();

            GridSaveSystem.SaveGrid(gameObject.name, this);
        }
    }

    public void ClearAllModules()
    {
        for (int i = installedModules.Count - 1; i >= 0; i--)
        {
            PlacedModule pm = installedModules[i];
            if (pm.spawnedPrefab != null)
            {
                // Lepaskan collider dari map dulu — jangan tinggalkan key mati
                Collider[] modColliders = pm.spawnedPrefab.GetComponentsInChildren<Collider>(true);
                foreach (var col in modColliders)
                    moduleColliderMap.Remove(col);

                Destroy(pm.spawnedPrefab);
            }
        }
        installedModules.Clear();

        if (_statsManager != null)
            _statsManager.MarkStatsDirty();

        var weaponTrigger = GetComponent<VehicleGridWeaponTrigger>();
        if (weaponTrigger != null)
            weaponTrigger.RebuildWeaponHUDs();
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
