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

    private Rigidbody rb;
    public Rigidbody VehicleRigidbody => rb;

    private bool _statsDirty = false;

    [Header("UI Reference")]
    public VehicleHUD hud;

    // ── Grid Forwarding (delegated ke VehicleGridSystem) ──
    public List<GridZone> gridZones
    {
        get
        {
            if (gridSystem == null) return null;
            return gridSystem.gridZones;
        }
    }

    public List<PlacedModule> installedModules
    {
        get
        {
            if (gridSystem == null) return null;
            return gridSystem.installedModules;
        }
    }

    public Dictionary<Collider, PlacedModule> moduleColliderMap
    {
        get
        {
            if (gridSystem == null) return null;
            return gridSystem.moduleColliderMap;
        }
    }

    public List<Vector2Int> GetOccupiedCells(Vector2Int position, int width, int height, int angle)
    {
        if (gridSystem == null) return new List<Vector2Int>();
        return gridSystem.GetOccupiedCells(position, width, height, angle);
    }

    public List<Vector2Int> GetClearanceCells(Vector2Int position, ModuleTemplate template, int angle)
    {
        if (gridSystem == null) return new List<Vector2Int>();
        return gridSystem.GetClearanceCells(position, template, angle);
    }

    public bool IsAreaFree(GridZone zone, Vector2Int position, ModuleTemplate templateToPlace, int angle, PlacedModule ignoreModule = null)
    {
        if (gridSystem == null) return false;
        return gridSystem.IsAreaFree(zone, position, templateToPlace, angle, ignoreModule);
    }

    public bool InstallModule(ModuleTemplate template, string targetZoneName, Vector2Int position, int angle)
    {
        if (gridSystem == null) return false;
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
            VehicleController vc = GetComponent<VehicleController>();
            if (vc != null)
                vc.engineRunning = false;
        }
    }

    void Update()
    {
        if (isPreviewMode) return;

        VehicleController vc = GetComponent<VehicleController>();
        if (vc != null && vc.engineRunning)
        {
            currentFuelAmount -= vc.currentFuelConsumptionRate * Time.deltaTime;
            currentFuelAmount = Mathf.Max(0f, currentFuelAmount);

            if (currentFuelAmount <= 0f)
            {
                vc.engineRunning = false;
            }
        }

        float totalPowerGen = currentPowerGeneration;
        if (vc != null && vc.engineRunning)
            totalPowerGen += _enginePowerGeneration;
        bool lightsOn = vc != null && vc.lightsOn;
        float aktifConsumption = currentPowerConsumption;
        if (!lightsOn) aktifConsumption -= totalLampPower;
        activePowerConsumption = aktifConsumption;
        float netPower = totalPowerGen - aktifConsumption;
        currentBatteryAmount += netPower * Time.deltaTime / 3600f;
        currentBatteryAmount = Mathf.Clamp(currentBatteryAmount, 0f, currentBatteryCapacity);

        VehicleCriticalPart[] criticalParts = GetComponentsInChildren<VehicleCriticalPart>();
        foreach (var part in criticalParts)
        {
            part.UpdateLampState(currentBatteryAmount, lightsOn);
        }
    }

    private List<PlacedModule> _ammoModuleCache;
    private List<VehicleCriticalPart> _ammoCriticalCache;
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
        VehicleController vc = GetComponent<VehicleController>();
        if (vc != null)
        {
            if (!hasEngineModule)
            {
                vc.engineRunning = false;
                vc.engine.maxTorqueNm = 0f;
                vc.engine.maxFuelConsumptionRate = 0f;
            }
        }

        // Terapkan berat total ke Rigidbody
        if (rb != null)
        {
            rb.mass = currentTotalMass;
        }

        // Terapkan aerodynamics ke VehicleController
        if (vc != null)
        {
            vc.airDragCd = baseData.baseDragCd;
            vc.frontalArea = totalDragArea;
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
