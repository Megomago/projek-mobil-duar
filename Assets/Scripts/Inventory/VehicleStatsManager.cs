using System.Collections.Generic;
using UnityEngine;
using Weapons;

[RequireComponent(typeof(Rigidbody))]
public class VehicleStatsManager : MonoBehaviour
{
    [Header("Base Vehicle Data")]
    public VehicleBaseData baseData;

    [Header("Grid System Reference (Auto)")]
    [HideInInspector]
    public VehicleGridSystem gridSystem;

    [Header("Current Calculated Stats (Read Only)")]
    public float currentTotalMass;
    public float currentPowerConsumption;
    public float currentPowerGeneration;
    public float totalLampPower;
    public float activePowerConsumption;

    private float _enginePowerGeneration;
    public float enginePowerGeneration => _enginePowerGeneration;

    public float currentBatteryCapacity;
    public float currentFuelCapacity;
    public float currentMaxOutput;
    public float currentCapacitorCapacity;
    public float currentCapacitorChargeRate;

    [Header("Ammo Pool (Read Only)")]
    [Tooltip("Total poin amunisi yang tersisa (diakumulasi dari semua modul ammo)")]
    public float totalAmmoPoints;

    [Header("Armor Stats (Read Only)")]
    [Tooltip("Total DEF sasis (Base + ArmorPlate terpasang) - melindungi critical parts")]
    public float currentChassisArmor;

    [Header("Runtime Health")]
    public float currentFuelAmount;
    public float currentBatteryAmount;

    [Header("Mode Settings")]
    [Tooltip("Centang ini kalau vehicle lagi di mode preview/garage")]
    public bool isPreviewMode = false;

    [Header("Grid Save State")]
    [Tooltip("Diset true setelah LoadGridAsync selesai. Menjaga ammo tidak ter-save saat grid masih loading (anti save parsial).")]
    [HideInInspector]
    public bool isGridFullyLoaded = false;

    private Rigidbody rb;
    private VehicleController _vc;
    public Rigidbody VehicleRigidbody => rb;

    private bool _statsDirty = false;
    private Vector3 _initialCenterOfMass;

    [Header("UI Reference")]
    public VehicleHUD hud;

    // ── Grid Forwarding (delegated ke VehicleGridSystem) ──
    private static readonly List<GridZone> _emptyGridZones = new List<GridZone>(0);
    private static readonly List<PlacedModule> _emptyInstalledModules = new List<PlacedModule>(0);

    public List<GridZone> gridZones
    {
        get
        {
            if (gridSystem == null) return _emptyGridZones;
            return gridSystem.gridZones;
        }
    }

    public List<PlacedModule> installedModules
    {
        get
        {
            if (gridSystem == null) return _emptyInstalledModules;
            return gridSystem.installedModules;
        }
    }

    private static readonly Dictionary<Collider, PlacedModule> _emptyColliderMap = new Dictionary<Collider, PlacedModule>(0);

    public Dictionary<Collider, PlacedModule> moduleColliderMap
    {
        get
        {
            if (gridSystem == null) return _emptyColliderMap;
            return gridSystem.moduleColliderMap;
        }
    }

    public void GetOccupiedCells(Vector2Int position, int width, int height, int angle, List<Vector2Int> dest)
    {
        if (gridSystem == null) { dest.Clear(); return; }
        gridSystem.GetOccupiedCells(position, width, height, angle, dest);
    }

    public void GetClearanceCells(Vector2Int position, ModuleTemplate template, int angle, List<Vector2Int> dest)
    {
        if (gridSystem == null) { dest.Clear(); return; }
        gridSystem.GetClearanceCells(position, template, angle, dest);
    }

    public bool IsAreaFree(GridZone zone, Vector2Int position, ModuleTemplate templateToPlace, int angle, PlacedModule ignoreModule = null)
    {
        if (gridSystem == null) return false;
        return gridSystem.IsAreaFree(zone, position, templateToPlace, angle, ignoreModule);
    }

    public PlacedModule InstallModule(ModuleTemplate template, string targetZoneName, Vector2Int position, int angle)
    {
        if (gridSystem == null) return null;
        return gridSystem.InstallModule(template, targetZoneName, position, angle);
    }

    public void UninstallModule(PlacedModule module)
    {
        if (gridSystem != null)
            gridSystem.UninstallModule(module);
    }

    public void ClearAllModules()
    {
        if (gridSystem != null)
            gridSystem.ClearAllModules();
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _vc = GetComponent<VehicleController>();
        gridSystem = GetComponent<VehicleGridSystem>();
        if (gridSystem == null)
            Debug.LogError("[VehicleStatsManager] VehicleGridSystem tidak ditemukan! Tambahkan component VehicleGridSystem ke kendaraan ini.");
    }

    void LateUpdate()
    {
        if (_statsDirty)
        {
            _statsDirty = false;
            CalculateAndApplyStats();
        }
    }

    public void MarkStatsDirty()
    {
        _statsDirty = true;
    }

    void Start()
    {
        currentFuelAmount = 0f;

        // Auto-init HitboxProxy pada base vehicle colliders (wheels, critical parts, dll)
        if (GetComponent<VehicleHitboxInitializer>() == null)
            gameObject.AddComponent<VehicleHitboxInitializer>();

        CalculateAndApplyStats();

        if (gridSystem != null)
        {
            foreach (var mod in gridSystem.installedModules)
            {
                if (mod.moduleTemplate != null && mod.moduleTemplate.ammoPoint > 0 && mod.currentAmmoPoint <= 0f)
                    mod.currentAmmoPoint = mod.moduleTemplate.ammoPoint;
            }
        }
        VehicleCriticalPart[] allCrit = GetComponentsInChildren<VehicleCriticalPart>(true);
        foreach (var cp in allCrit)
        {
            if (cp.ammoPoint > 0 && cp.currentAmmoPoint <= 0f)
                cp.currentAmmoPoint = cp.ammoPoint;
        }

        if (!isPreviewMode)
        {
            currentFuelAmount = currentFuelCapacity;
            currentBatteryAmount = currentBatteryCapacity;
        }

        if (!isPreviewMode)
        {
            if (_vc != null)
                _vc.engineRunning = false;
        }
    }

    void Update()
    {
        if (isPreviewMode) return;

        if (_vc != null && _vc.engineRunning)
        {
            currentFuelAmount -= _vc.currentFuelConsumptionRate * Time.deltaTime;
            currentFuelAmount = Mathf.Max(0f, currentFuelAmount);

            if (currentFuelAmount <= 0f)
            {
                _vc.engineRunning = false;
            }
        }

        float totalPowerGen = currentPowerGeneration;
        if (_vc != null && _vc.engineRunning)
            totalPowerGen += _enginePowerGeneration;
        bool lightsOn = _vc != null && _vc.lightsOn;
        float aktifConsumption = currentPowerConsumption;
        if (!lightsOn) aktifConsumption -= totalLampPower;
        activePowerConsumption = aktifConsumption;
        float netPower = totalPowerGen - aktifConsumption;
        currentBatteryAmount += netPower * Time.deltaTime / 3600f;
        currentBatteryAmount = Mathf.Clamp(currentBatteryAmount, 0f, currentBatteryCapacity);

        int count = _cachedCriticalParts.Count;
        for (int i = 0; i < count; i++)
        {
            _cachedCriticalParts[i].UpdateLampState(currentBatteryAmount, lightsOn);
        }
    }

    private List<PlacedModule> _ammoModuleCache;
    private List<VehicleCriticalPart> _ammoCriticalCache;
    private List<VehicleCriticalPart> _cachedCriticalParts = new List<VehicleCriticalPart>();
    private int _ammoCacheIndex;

    public bool TryConsumeAmmo(float points)
    {
        if (points <= 0f) return true;
        if (totalAmmoPoints < points) return false;

        float remaining = points;

        // Konsumsi dari modul grid dulu (sequential)
        int modCount = _ammoModuleCache.Count;
        for (int i = 0; i < modCount; i++)
        {
            var mod = _ammoModuleCache[i];
            if (mod.currentAmmoPoint <= 0f) continue;

            if (mod.currentAmmoPoint >= remaining)
            {
                mod.currentAmmoPoint -= remaining;
                remaining = 0f;
                break;
            }
            else
            {
                remaining -= mod.currentAmmoPoint;
                mod.currentAmmoPoint = 0f;
            }
        }

        // Kalau masih kurang, ambil dari critical parts
        if (remaining > 0f)
        {
            int critCount = _ammoCriticalCache.Count;
            for (int i = 0; i < critCount; i++)
            {
                var crit = _ammoCriticalCache[i];
                if (crit.currentAmmoPoint <= 0f) continue;

                if (crit.currentAmmoPoint >= remaining)
                {
                    crit.currentAmmoPoint -= remaining;
                    remaining = 0f;
                    break;
                }
                else
                {
                    remaining -= crit.currentAmmoPoint;
                    crit.currentAmmoPoint = 0f;
                }
            }
        }

        totalAmmoPoints -= points;
        return true;
    }

    /// <summary>
    /// Isi ulang amunisi SEMUA ammo box (grid) + critical part ke kapasitas penuh (GRATIS).
    /// Nanti bisa ditambah biaya resource tanpa mengubah struktur ini.
    /// </summary>
    public void RefillAmmo()
    {
        if (gridSystem != null)
        {
            foreach (var mod in gridSystem.installedModules)
            {
                if (mod.moduleTemplate != null && mod.moduleTemplate.ammoPoint > 0f)
                    mod.currentAmmoPoint = mod.moduleTemplate.ammoPoint;
            }
        }

        VehicleCriticalPart[] allCrit = GetComponentsInChildren<VehicleCriticalPart>(true);
        foreach (var cp in allCrit)
        {
            if (cp.ammoPoint > 0f)
                cp.currentAmmoPoint = cp.ammoPoint;
        }

        RebuildAmmoCache();
        MarkStatsDirty();
        PersistAmmo();
    }

    /// <summary>
    /// Simpan sisa amunisi ke file grid. Di-skip kalau grid belum selesai di-load
    /// (mencegah save parsial menimpa data utuh saat kendaraan hilang di tengah loading).
    /// </summary>
    public void PersistAmmo()
    {
        if (gridSystem == null || !isGridFullyLoaded) return;
        GridSaveSystem.SaveGrid(gameObject.name, gridSystem);
    }

    private void OnDestroy()
    {
        // Amunisi tersisa disimpan saat kendaraan hilang (keluar battle / pindah scene /
        // mobil meledak). Ammo box yang hancur otomatis tidak ikut tersimpan.
        //
        // Preview garasi: SKIP ÔÇö amunisi sudah tersimpan saat install/uninstall/refill,
        // dan ammo tidak bisa berubah di garasi. Menghindari I/O file di frame transisi
        // scene (lobby Ôåö garasi).
        if (isPreviewMode) return;

        PersistAmmo();
    }

    private void RebuildAmmoCache()
    {
        if (_ammoModuleCache == null)
            _ammoModuleCache = new List<PlacedModule>();
        _ammoModuleCache.Clear();

        if (_ammoCriticalCache == null)
            _ammoCriticalCache = new List<VehicleCriticalPart>();
        _ammoCriticalCache.Clear();

        _ammoCacheIndex = 0;

        if (gridSystem != null)
        {
            foreach (var mod in gridSystem.installedModules)
            {
                if (mod.moduleTemplate != null && mod.moduleTemplate.ammoPoint > 0f && mod.currentAmmoPoint > 0f)
                    _ammoModuleCache.Add(mod);
            }
        }

        VehicleCriticalPart[] critParts = GetComponentsInChildren<VehicleCriticalPart>(true);
        foreach (var cp in critParts)
        {
            if (cp.ammoPoint > 0f && cp.currentAmmoPoint > 0f)
                _ammoCriticalCache.Add(cp);
        }
    }

    [ContextMenu("Calculate Stats")]
    public void CalculateAndApplyStats()
    {
        if (baseData == null) 
        {
            Debug.LogWarning("Base Data belum dimasukkan ke VehicleStatsManager!");
            return;
        }

        // Reset nilai ke basis kendaraan
        currentTotalMass = baseData.baseMass;
        currentChassisArmor = baseData.chassisArmor;
        currentPowerConsumption = 0f;
        currentPowerGeneration = 0f;
        _enginePowerGeneration = 0f;
        totalLampPower = 0f;
        float oldBatteryCapacity = currentBatteryCapacity;
        float oldFuelCapacity = currentFuelCapacity;
        currentBatteryCapacity = 0f;
        currentFuelCapacity = 0f;
        currentCapacitorCapacity = 0f;
        currentCapacitorChargeRate = 0f;

        // Deteksi kehadiran Engine + accumulasi stats dari semua critical part
        bool hasEngineModule = false;

        VehicleCriticalPart[] criticalParts = GetComponentsInChildren<VehicleCriticalPart>();
        foreach (var part in criticalParts)
        {
            if (!part.gameObject.activeInHierarchy) continue;

            currentPowerConsumption   += part.powerConsumption;
            if (part.isLamp) totalLampPower += part.powerConsumption;
            currentBatteryCapacity    += part.extraBatteryCapacity;
            currentFuelCapacity       += part.extraFuelCapacity;
            currentMaxOutput          += part.extraMaxOutput;
            currentCapacitorCapacity  += part.capacitorCapacity;
            currentCapacitorChargeRate+= part.chargeRate;

            if (part.partType == VehicleCriticalPart.CriticalPartType.Engine)
                hasEngineModule = true;

            if (part.partType == VehicleCriticalPart.CriticalPartType.Engine ||
                part.partType == VehicleCriticalPart.CriticalPartType.Alternator)
                _enginePowerGeneration += part.powerGeneration;
            else
                currentPowerGeneration += part.powerGeneration;
        }

        // Jika ada kapasitas batre baru, isi proportional (batre baru datang dengan isi)
        if (currentBatteryCapacity > oldBatteryCapacity)
            currentBatteryAmount += currentBatteryCapacity - oldBatteryCapacity;
        if (currentFuelCapacity > oldFuelCapacity)
            currentFuelAmount += currentFuelCapacity - oldFuelCapacity;

        // Cache critical parts untuk Update() — zero hierarchy traversal per frame
        _cachedCriticalParts.Clear();
        _cachedCriticalParts.AddRange(criticalParts);

        // Akumulasi drag dari modul
        float totalDragArea = baseData.baseFrontalArea;

        // Hitung stats tambahan dari setiap modul di grid
        if (gridSystem != null)
        {
            foreach (var module in gridSystem.installedModules)
            {
                if (module != null && module.moduleTemplate != null)
                {
                    ModuleTemplate template = module.moduleTemplate;

                    currentTotalMass += template.weight;
                    currentPowerConsumption += template.powerConsumption;
                    currentPowerGeneration += template.powerGeneration;
                    currentBatteryCapacity += template.extraBatteryCapacity;

                    if (template.moduleType == ModuleType.FuelBarrel)
                        currentFuelCapacity += template.extraFuelCapacity;

                    currentMaxOutput           += template.extraMaxOutput;
                    currentCapacitorCapacity   += template.capacitorCapacity;
                    currentCapacitorChargeRate += template.chargeRate;

                    GridZone moduleZone = gridSystem.gridZones.Find(z => z.zoneName == module.zoneName);
                    if (moduleZone != null && moduleZone.affectDrag)
                        totalDragArea += template.dragModifier;
                }
            }
        }

        // Hitung total ammo pool & rebuild cache
        totalAmmoPoints = 0f;
        if (gridSystem != null)
        {
            foreach (var mod in gridSystem.installedModules)
            {
                if (mod.moduleTemplate != null && mod.moduleTemplate.ammoPoint > 0f)
                    totalAmmoPoints += mod.currentAmmoPoint;
            }
        }
        foreach (var cp in criticalParts)
        {
            if (cp.ammoPoint > 0f)
                totalAmmoPoints += cp.currentAmmoPoint;
        }
        RebuildAmmoCache();

        // Terapkan ke VehicleController — jangan auto-start engine!
        // Hanya matikan mesin kalau part Engine hancur/tidak ada.
        if (_vc != null)
        {
            if (!hasEngineModule)
            {
                _vc.engineRunning = false;
                _vc.engine.maxTorqueNm = 0f;
                _vc.engine.maxFuelConsumptionRate = 0f;
            }
        }

        // Terapkan berat total ke Rigidbody
        if (rb != null)
        {
            rb.mass = currentTotalMass;
        }

        // Terapkan aerodynamics ke VehicleController
        if (_vc != null)
        {
            _vc.airDragCd = baseData.baseDragCd;
            _vc.frontalArea = totalDragArea;
        }

        // Update HUD
        if (hud != null)
        {
            hud.UpdateHUD(this);
        }

        // Refresh module list UI
        VehicleModuleListUI moduleList = GetComponentInChildren<VehicleModuleListUI>();
        if (moduleList != null)
            moduleList.Refresh();
    }
}

