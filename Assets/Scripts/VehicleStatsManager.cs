using System.Collections.Generic;
using UnityEngine;
using Weapons;

[System.Serializable]
public class PlacedModule
{
    public ModuleTemplate moduleTemplate;
    public string zoneName; // Nama zona grid tempat modul ini terpasang

    [Tooltip("Posisi koordinat X, Y di grid")]
    public Vector2Int gridPosition;

    [Tooltip("Rotasi modul dalam derajat (0, 90, 180, 270)")]
    public int rotationAngle;

    [HideInInspector]
    public float currentHealth;

    // Referensi ke prefab 3D yang sudah di-spawn di kendaraan
    [HideInInspector]
    public GameObject spawnedPrefab;

    public PlacedModule(ModuleTemplate template, string zone, Vector2Int position, int angle)
    {
        moduleTemplate = template;
        zoneName = zone;
        gridPosition = position;
        rotationAngle = angle;
        if (template != null)
        {
            currentHealth = template.maxHealth;
        }
    }
}

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
}

[RequireComponent(typeof(Rigidbody))]
public class VehicleStatsManager : MonoBehaviour
{
    [Header("Base Vehicle Data")]
    public VehicleBaseData baseData;

    [Header("Modular Grid Setup")]
    [Tooltip("Daftar semua zona grid di kendaraan ini (Atap, Kap, dll)")]
    public List<GridZone> gridZones = new List<GridZone>();

    [Header("Installed Modules")]
    public List<PlacedModule> installedModules = new List<PlacedModule>();

    [Header("Current Calculated Stats (Read Only)")]
    public float currentTotalMass;
    public float currentPowerConsumption;
    public float currentPowerGeneration;
    public float currentBatteryCapacity;
    public float currentFuelCapacity;
    public float currentMaxOutput;
    public float currentCapacitorCapacity;
    public float currentCapacitorChargeRate;

    [Header("Armor Stats (Read Only)")]
    [Tooltip("Total DEF bodi (Base + ArmorPlate terpasang)")]
    public float currentBodyArmor;
    [Tooltip("Total DEF roda (Base + ArmorPlate terpasang)")]
    public float currentWheelArmor;
    [Tooltip("Total DEF mesin (Base + ArmorPlate terpasang)")]
    public float currentEngineArmor;
    [Tooltip("Total DEF baterai (Base + ArmorPlate terpasang)")]
    public float currentBatteryArmor;

    [Header("Runtime Health")]
    public float currentWheelHealth;
    public float currentFuelAmount;
    public float currentBatteryAmount;

    [Header("Mode Settings")]
    [Tooltip("Centang ini kalau vehicle lagi di mode preview/garage")]
    public bool isPreviewMode = false;

    private Rigidbody rb;
    public Rigidbody VehicleRigidbody => rb;

    // Cache collider → PlacedModule biar O(1) lookup di projectile
    [HideInInspector] public Dictionary<Collider, PlacedModule> moduleColliderMap = new Dictionary<Collider, PlacedModule>();

    // Batch lock buat suppress CalculateAndApplyStats pas loading
    private int _batchLock = 0;
    public bool IsBatchLocked => _batchLock > 0;
    public void BeginBatch() { _batchLock++; }
    public void EndBatch() { _batchLock--; if (_batchLock <= 0) CalculateAndApplyStats(); }

    [Header("UI Reference")]
    public VehicleHUD hud;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        InitializeRuntimeHealth();

        // ISI BENSIN DULU sebelum CalculateAndApplyStats
        // agar hasFuel check tidak langsung false saat pertama kali
        if (!isPreviewMode) currentFuelAmount = baseData != null ? baseData.fuelCapacity : 0f;

        CalculateAndApplyStats();

        // Setelah stats dihitung, update fuel & battery ke kapasitas aktual
        if (!isPreviewMode)
        {
            currentFuelAmount = currentFuelCapacity;
            currentBatteryAmount = currentBatteryCapacity;
        }
    }

    void Update()
    {
        if (isPreviewMode) return;

        VehicleController vc = GetComponent<VehicleController>();
        if (vc != null && vc.engineRunning)
        {
            // Drain fuel sesuai konsumsi (Liter/sec)
            currentFuelAmount -= vc.currentFuelConsumptionRate * Time.deltaTime;
            currentFuelAmount = Mathf.Max(0f, currentFuelAmount);

            // Matikan mesin kalau bensin habis
            if (currentFuelAmount <= 0f)
            {
                vc.engineRunning = false;
            }
        }

        // ── BATERAI DINAMIS ──
        // Hitung selisih daya (generation - consumption)
        // Saat mesin nyala → alternator hidup → powerGeneration aktif
        // Saat mesin mati → hanya solar panel dll yg berkontribusi
        float netPower = currentPowerGeneration - currentPowerConsumption;
        currentBatteryAmount += netPower * Time.deltaTime / 3600f; // Watt → Watt-hour
        currentBatteryAmount = Mathf.Clamp(currentBatteryAmount, 0f, currentBatteryCapacity);
    }

    public void InitializeRuntimeHealth()
    {
        if (baseData == null) return;
        currentWheelHealth = baseData.wheelHealth;
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
        currentPowerConsumption = 0f;
        currentPowerGeneration = baseData.powerGeneration;
        currentBatteryCapacity = baseData.batteryCapacity;
        currentFuelCapacity = baseData.fuelCapacity;
        currentMaxOutput = baseData.maxPowerOutput;
        currentCapacitorCapacity = 0f;
        currentCapacitorChargeRate = 0f;

        // Deteksi kehadiran critical part bawaan kendaraan
        bool hasEngineModule     = false;
        bool hasFuelModule       = false;
        bool hasBatteryModule    = false;
        bool hasAlternatorModule = false;
        bool hasCapacitorModule  = false;

        VehicleCriticalPart[] criticalParts = GetComponentsInChildren<VehicleCriticalPart>();
        foreach (var part in criticalParts)
        {
            if (!part.gameObject.activeInHierarchy) continue;

            switch (part.partType)
            {
                case VehicleCriticalPart.CriticalPartType.Engine:
                    hasEngineModule = true;
                    break;

                case VehicleCriticalPart.CriticalPartType.FuelTank:
                    hasFuelModule = true;
                    break;

                case VehicleCriticalPart.CriticalPartType.Battery:
                    hasBatteryModule = true;
                    break;

                case VehicleCriticalPart.CriticalPartType.Alternator:
                    hasAlternatorModule = true;
                    break;

                case VehicleCriticalPart.CriticalPartType.Capacitor:
                    hasCapacitorModule = true;
                    break;
            }
        }

        // Hitung stats tambahan dari setiap modul di grid
        foreach (var module in installedModules)
        {
            if (module != null && module.moduleTemplate != null)
            {
                ModuleTemplate template = module.moduleTemplate;

                // Tambah berat
                currentTotalMass += template.weight;

                // Tambah konsumsi daya
                currentPowerConsumption += template.powerConsumption;

                // Tambah produksi daya (generator / panel surya)
                currentPowerGeneration += template.powerGeneration;

                // Tambah kapasitas batrai / aki cadangan
                currentBatteryCapacity += template.extraBatteryCapacity;

                // Tambah bensin cadangan dari modul FuelBarrel ekstra
                if (template.moduleType == ModuleType.FuelBarrel)
                    currentFuelCapacity += template.extraFuelCapacity;

                // Tambah kapasitor dari modul ekstra
                currentMaxOutput           += template.extraMaxOutput;
                currentCapacitorCapacity   += template.capacitorCapacity;
                currentCapacitorChargeRate += template.chargeRate;
            }
        }

        // Tangki BAWAAN hanya aktif kalau ada CriticalPart FuelTank
        if (hasFuelModule)
            currentFuelCapacity += baseData.fuelCapacity;

        // Baterai BAWAAN hanya aktif kalau ada CriticalPart Battery
        if (hasBatteryModule)
        {
            currentBatteryCapacity += baseData.batteryCapacity;
        }

        // Alternator BAWAAN hanya aktif kalau ada CriticalPart Alternator
        if (hasAlternatorModule)
            currentPowerGeneration += baseData.powerGeneration;

        // Terapkan ke VehicleController berdasarkan kehadiran CriticalPart Engine
        VehicleController vc = GetComponent<VehicleController>();
        if (vc != null)
        {
            bool hasFuel = isPreviewMode || currentFuelAmount > 0f;
            vc.engineRunning = hasEngineModule && hasFuel;

            if (!hasEngineModule)
            {
                vc.engine.maxTorqueNm = 0f;
                vc.engine.maxFuelConsumptionRate = 0f;
            }
        }

        // Terapkan berat total ke Rigidbody
        if (rb != null)
        {
            rb.mass = currentTotalMass;
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

    // Fungsi untuk mendapatkan semua cell yang ditempati sebuah modul
    public List<Vector2Int> GetOccupiedCells(Vector2Int position, int width, int height, int angle)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        
        // Sesuaikan ukuran efektif berdasarkan rotasi (0 dan 180 = normal, 90 dan 270 = ditukar)
        int effectiveWidth = (angle == 90 || angle == 270) ? height : width;
        int effectiveHeight = (angle == 90 || angle == 270) ? width : height;

        for (int x = 0; x < effectiveWidth; x++)
        {
            for (int y = 0; y < effectiveHeight; y++)
            {
                cells.Add(new Vector2Int(position.x + x, position.y + y));
            }
        }
        return cells;
    }

    // Fungsi untuk memvalidasi apakah area grid kosong dan berada di dalam batas untuk sebuah zona tertentu
    public bool IsAreaFree(GridZone zone, Vector2Int position, int width, int height, int angle, PlacedModule ignoreModule = null)
    {
        if (zone == null) return false;

        List<Vector2Int> targetCells = GetOccupiedCells(position, width, height, angle);

        foreach (var cell in targetCells)
        {
            // Cek batas grid zona
            if (cell.x < 0 || cell.x >= zone.capacity.x || cell.y < 0 || cell.y >= zone.capacity.y)
            {
                return false;
            }

            // Cek tabrakan dengan modul lain yang sudah terpasang, tetapi hanya di zona yang sama
            foreach (var mod in installedModules)
            {
                if (mod == ignoreModule) continue;
                if (mod.moduleTemplate == null) continue;
                if (mod.zoneName != zone.zoneName) continue; // Abaikan modul di zona lain

                List<Vector2Int> modCells = GetOccupiedCells(mod.gridPosition, mod.moduleTemplate.width, mod.moduleTemplate.height, mod.rotationAngle);
                if (modCells.Contains(cell))
                {
                    return false; // Tabrakan!
                }
            }
        }
        return true;
    }

    // Fungsi untuk menambah modul ke grid dan spawn prefab 3D-nya di kendaraan
    // Install module ke zona tertentu berdasarkan nama zona
    public bool InstallModule(ModuleTemplate template, string targetZoneName, Vector2Int position, int angle)
    {
        if (template == null) return false;

        // Cari zona yang dituju
        GridZone targetZone = null;
        foreach (var z in gridZones)
        {
            if (z == null) continue;
            if (z.zoneName == targetZoneName) { targetZone = z; break; }
        }

        if (targetZone == null || targetZone.origin == null)
        {
            Debug.LogError("[VehicleStatsManager] Zona grid tidak ditemukan atau origin-nya null!");
            return false;
        }

        // Validasi Tetris placement di zona yang dituju
        if (!IsAreaFree(targetZone, position, template.width, template.height, angle))
        {
            Debug.LogWarning($"[VehicleStatsManager] Gagal memasang {template.moduleName} di zona {targetZoneName} posisi {position} karena area penuh atau di luar batas.");
            return false;
        }

        PlacedModule newModule = new PlacedModule(template, targetZoneName, position, angle);

        // Tentukan prefab yang akan di-spawn
        GameObject prefabToSpawn = template.modulePrefab;
        if (template.moduleType == ModuleType.Weapon && template.weaponData != null && template.weaponData.weapon3DPrefab != null)
        {
            prefabToSpawn = template.weaponData.weapon3DPrefab;
        }

        // Spawn prefab 3D di posisi grid kendaraan untuk zona yang dipilih
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

            // --- SET LAYER AGAR TIDAK TABRAKAN DENGAN SASIS ---
            int moduleLayer = LayerMask.NameToLayer("placedmodule");
            if (moduleLayer != -1)
            {
                SetLayerRecursively(spawned, moduleLayer);
            }
            // ----------------------------------------------------

            // Daftarkan semua collider modul ke dictionary buat O(1) lookup
            Collider[] modColliders = spawned.GetComponentsInChildren<Collider>(true);
            foreach (var col in modColliders)
                moduleColliderMap[col] = newModule;

            if (isPreviewMode)
            {
                ManualTurretController[] newTurrets = spawned.GetComponentsInChildren<ManualTurretController>(true);
                foreach (var turret in newTurrets)
                {
                    turret.enabled = false;
                }

                Animator[] newAnimators = spawned.GetComponentsInChildren<Animator>(true);
                foreach (var anim in newAnimators)
                {
                    anim.enabled = false;
                }

                Rigidbody[] newRbs = spawned.GetComponentsInChildren<Rigidbody>(true);
                foreach (var rb in newRbs)
                {
                    if (rb != spawned.GetComponentInParent<Rigidbody>())
                    {
                        rb.isKinematic = true;
                        rb.constraints = RigidbodyConstraints.FreezeAll;
                    }
                }

                // Inisialisasi Script Fisik Modul jika ada
                VehicleModuleComponent moduleComp = spawned.GetComponent<VehicleModuleComponent>();
                if (moduleComp != null)
                {
                    moduleComp.Initialize(newModule, this);
                }
            }
        }

        installedModules.Add(newModule);
        if (!IsBatchLocked) CalculateAndApplyStats();
        return true;
    }

    // Fungsi untuk melepas modul dari grid dan hapus prefab 3D-nya
    public void UninstallModule(PlacedModule module)
    {
        if (installedModules.Contains(module))
        {
            // Hapus collider modul dari cache sebelum prefab di-destroy
            if (module.spawnedPrefab != null)
            {
                Collider[] modColliders = module.spawnedPrefab.GetComponentsInChildren<Collider>(true);
                foreach (var col in modColliders)
                    moduleColliderMap.Remove(col);
            }

            // Hapus prefab 3D dari kendaraan
            if (module.spawnedPrefab != null)
            {
                Destroy(module.spawnedPrefab);
            }

            installedModules.Remove(module);
            if (!IsBatchLocked) CalculateAndApplyStats();

            GridSaveSystem.SaveGrid(gameObject.name, this);
        }
    }

    // Fungsi untuk menghapus semua modul (digunakan sebelum load data save)
    public void ClearAllModules()
    {
        for (int i = installedModules.Count - 1; i >= 0; i--)
        {
            if (installedModules[i].spawnedPrefab != null)
            {
                Destroy(installedModules[i].spawnedPrefab);
            }
        }
        installedModules.Clear();
        if (!IsBatchLocked) CalculateAndApplyStats();
    }

    // Fungsi rekursif untuk mengubah layer objek dan semua anaknya
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
