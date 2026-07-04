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

    [Tooltip("Centang jika zona ini terkena angin (luar sasis). Contoh: Roof, Hood, Trunk. Uncentang jika di dalam sasis (Engine Bay, Interior)")]
    public bool affectDrag = true;
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
    public float totalLampPower;
    public float activePowerConsumption;

    // Produksi daya dari Engine & Alternator (baru ditambahkan kalau engine hidup)
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

    // Cache collider → PlacedModule biar O(1) lookup di projectile
    [HideInInspector] public Dictionary<Collider, PlacedModule> moduleColliderMap = new Dictionary<Collider, PlacedModule>();

    // Dirty flag: CalculateAndApplyStats cuma jalan sekali per frame via LateUpdate
    private bool _statsDirty = false;

    [Header("UI Reference")]
    public VehicleHUD hud;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
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

        // Inisialisasi ammo points dari semua modul & critical parts
        foreach (var mod in installedModules)
        {
            if (mod.moduleTemplate != null && mod.moduleTemplate.ammoPoint > 0 && mod.currentAmmoPoint <= 0f)
                mod.currentAmmoPoint = mod.moduleTemplate.ammoPoint;
        }
        VehicleCriticalPart[] allCrit = GetComponentsInChildren<VehicleCriticalPart>(true);
        foreach (var cp in allCrit)
        {
            if (cp.ammoPoint > 0 && cp.currentAmmoPoint <= 0f)
                cp.currentAmmoPoint = cp.ammoPoint;
        }

        // Setelah stats dihitung, update fuel & battery ke kapasitas aktual
        if (!isPreviewMode)
        {
            currentFuelAmount = currentFuelCapacity;
            currentBatteryAmount = currentBatteryCapacity;
        }

        // Pastikan engine mati di awal battlefield — pemain harus starter manual via I
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
        // Saat mesin nyala → enginePowerGeneration ditambahkan ke total
        // Saat mesin mati → hanya solar panel / generator mandiri yg berkontribusi
        float totalPowerGen = currentPowerGeneration;
        if (vc != null && vc.engineRunning)
            totalPowerGen += _enginePowerGeneration;
        bool lightsOn = vc != null && vc.lightsOn;
        float aktifConsumption = currentPowerConsumption;
        if (!lightsOn) aktifConsumption -= totalLampPower;
        activePowerConsumption = aktifConsumption;
        float netPower = totalPowerGen - aktifConsumption;
        currentBatteryAmount += netPower * Time.deltaTime / 3600f; // Watt → Watt-hour
        currentBatteryAmount = Mathf.Clamp(currentBatteryAmount, 0f, currentBatteryCapacity);

        // Update lamp state untuk semua critical part bertipe lampu
        VehicleCriticalPart[] criticalParts = GetComponentsInChildren<VehicleCriticalPart>();
        foreach (var part in criticalParts)
        {
            part.UpdateLampState(currentBatteryAmount, lightsOn);
        }
    }

    /// <summary>
    /// Konsumsi ammo point secara sequential dari modul yang dipasang paling awal.
    /// Returns false jika point tidak mencukupi.
    /// </summary>
    // Cache sumber ammo biar gak iterate semua object tiap tembakan
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

        foreach (var mod in installedModules)
        {
            if (mod.moduleTemplate != null && mod.moduleTemplate.ammoPoint > 0f && mod.currentAmmoPoint > 0f)
                _ammoModuleCache.Add(mod);
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

                // Tambah drag area hanya jika modul di zona yang terkena angin
                GridZone moduleZone = gridZones.Find(z => z.zoneName == module.zoneName);
                if (moduleZone != null && moduleZone.affectDrag)
                    totalDragArea += template.dragModifier;
            }
        }

        // Hitung total ammo pool & rebuild cache
        totalAmmoPoints = 0f;
        foreach (var mod in installedModules)
        {
            if (mod.moduleTemplate != null && mod.moduleTemplate.ammoPoint > 0f)
                totalAmmoPoints += mod.currentAmmoPoint;
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

    public List<Vector2Int> GetClearanceCells(Vector2Int position, ModuleTemplate template, int angle)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        if (template == null || !template.enableClearance) return cells;

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
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
        }
        return cells;
    }

    private bool HasIntersection(List<Vector2Int> list1, List<Vector2Int> list2)
    {
        foreach (var a in list1)
        {
            if (list2.Contains(a)) return true;
        }
        return false;
    }

    // Fungsi untuk memvalidasi apakah area grid kosong dan berada di dalam batas untuk sebuah zona tertentu
    public bool IsAreaFree(GridZone zone, Vector2Int position, ModuleTemplate templateToPlace, int angle, PlacedModule ignoreModule = null)
    {
        if (zone == null || templateToPlace == null) return false;

        List<Vector2Int> baseCellsA = GetOccupiedCells(position, templateToPlace.width, templateToPlace.height, angle);
        List<Vector2Int> clearanceCellsA = GetClearanceCells(position, templateToPlace, angle);

        // Cek batas grid zona HANYA untuk Base fisik
        foreach (var cell in baseCellsA)
        {
            if (cell.x < 0 || cell.x >= zone.capacity.x || cell.y < 0 || cell.y >= zone.capacity.y)
            {
                return false;
            }
        }

        // Cek tabrakan dengan modul lain yang sudah terpasang
        foreach (var mod in installedModules)
        {
            if (mod == ignoreModule) continue;
            if (mod.moduleTemplate == null) continue;
            if (mod.zoneName != zone.zoneName) continue; // Abaikan modul di zona lain (sistem koordinat berbeda)

            List<Vector2Int> baseCellsB = GetOccupiedCells(mod.gridPosition, mod.moduleTemplate.width, mod.moduleTemplate.height, mod.rotationAngle);
            List<Vector2Int> clearanceCellsB = GetClearanceCells(mod.gridPosition, mod.moduleTemplate, mod.rotationAngle);

            // Cek Base A vs Base B (selalu terlarang)
            if (HasIntersection(baseCellsA, baseCellsB)) return false;

            // Cek Base A vs Clearance B (Boleh jika A kecil DAN B mengizinkan akses)
            if (HasIntersection(baseCellsA, clearanceCellsB))
            {
                if (!templateToPlace.isSmall || !mod.moduleTemplate.enableAccessClearance) return false;
            }

            // Cek Clearance A vs Base B (Boleh jika B kecil DAN A mengizinkan akses)
            if (HasIntersection(clearanceCellsA, baseCellsB))
            {
                if (!mod.moduleTemplate.isSmall || !templateToPlace.enableAccessClearance) return false;
            }

            // Clearance A vs Clearance B selalu boleh
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
        if (!IsAreaFree(targetZone, position, template, angle))
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

            // Init script module SELALU jalan (biar damage/explosion konsisten di semua mode)
            VehicleModuleComponent moduleComp = spawned.GetComponent<VehicleModuleComponent>();
            if (moduleComp != null)
            {
                moduleComp.Initialize(newModule, this);
            }

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
            }
        }

        installedModules.Add(newModule);
        MarkStatsDirty();
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
            MarkStatsDirty();

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
        MarkStatsDirty();
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
