using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlacedModule
{
    public ModuleTemplate moduleTemplate;
    
    [Tooltip("Posisi koordinat X, Y di grid")]
    public Vector2Int gridPosition;
    
    [Tooltip("Rotasi modul dalam derajat (0, 90, 180, 270)")]
    public int rotationAngle;
    
    [HideInInspector]
    public float currentHealth;

    // Referensi ke prefab 3D yang sudah di-spawn di kendaraan
    [HideInInspector]
    public GameObject spawnedPrefab;

    public PlacedModule(ModuleTemplate template, Vector2Int position, int angle)
    {
        moduleTemplate = template;
        gridPosition = position;
        rotationAngle = angle;
        if (template != null)
        {
            currentHealth = template.maxHealth;
        }
    }
}

[RequireComponent(typeof(Rigidbody))]
public class VehicleStatsManager : MonoBehaviour
{
    [Header("Base Vehicle Data")]
    public VehicleBaseData baseData;

    [Header("Modular Grid Setup")]
    [Tooltip("Kapasitas grid mobil (contoh: atap 4x8)")]
    public Vector2Int gridCapacity = new Vector2Int(4, 8);
    
    [Tooltip("Titik awal (pojok kiri-bawah) grid di dunia 3D. Taruh empty GameObject di mobil sebagai anchor.")]
    public Transform gridOrigin;
    
    [Tooltip("Ukuran 1 kotak grid dalam meter (default 0.25 = 25cm)")]
    public float cellSize = 0.25f;

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

    private Rigidbody rb;
    
    [Header("UI Reference")]
    public VehicleHUD hud;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        CalculateAndApplyStats();
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
                
                // Tambah bensin cadangan
                currentFuelCapacity += template.extraFuelCapacity;

                // Tambah kapasitor (max output + kapasitas + charge rate)
                currentMaxOutput += template.extraMaxOutput;
                currentCapacitorCapacity += template.capacitorCapacity;
                currentCapacitorChargeRate += template.chargeRate;
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

    // Fungsi untuk memvalidasi apakah area grid kosong dan berada di dalam batas
    public bool IsAreaFree(Vector2Int position, int width, int height, int angle, PlacedModule ignoreModule = null)
    {
        List<Vector2Int> targetCells = GetOccupiedCells(position, width, height, angle);

        foreach (var cell in targetCells)
        {
            // Cek batas grid (Out of bounds)
            if (cell.x < 0 || cell.x >= gridCapacity.x || cell.y < 0 || cell.y >= gridCapacity.y)
            {
                return false;
            }

            // Cek tabrakan dengan modul lain yang sudah terpasang
            foreach (var mod in installedModules)
            {
                if (mod == ignoreModule) continue;
                if (mod.moduleTemplate == null) continue;

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
    public bool InstallModule(ModuleTemplate template, Vector2Int position, int angle)
    {
        if (template == null) return false;

        // Validasi Tetris placement
        if (!IsAreaFree(position, template.width, template.height, angle))
        {
            Debug.LogWarning($"[VehicleStatsManager] Gagal memasang {template.moduleName} di posisi {position} karena area penuh atau di luar batas.");
            return false;
        }
        
        PlacedModule newModule = new PlacedModule(template, position, angle);

        // Tentukan prefab yang akan di-spawn
        GameObject prefabToSpawn = template.modulePrefab;
        if (template.moduleType == ModuleType.Weapon && template.weaponData != null && template.weaponData.weapon3DPrefab != null)
        {
            prefabToSpawn = template.weaponData.weapon3DPrefab;
        }

        // Spawn prefab 3D di posisi grid kendaraan
        if (prefabToSpawn != null && gridOrigin != null)
        {
            // Hitung posisi dunia berdasarkan koordinat grid (Pusat modul)
            int effectiveWidth = (angle == 90 || angle == 270) ? template.height : template.width;
            int effectiveHeight = (angle == 90 || angle == 270) ? template.width : template.height;
            
            float offsetX = (position.x + effectiveWidth / 2f) * cellSize;
            float offsetZ = (position.y + effectiveHeight / 2f) * cellSize;
            
            Vector3 localPos = new Vector3(offsetX, 0f, offsetZ);
            Vector3 worldPos = gridOrigin.TransformPoint(localPos);
            
            Quaternion rotation = gridOrigin.rotation * Quaternion.Euler(0f, angle, 0f);

            GameObject spawned = Instantiate(prefabToSpawn, worldPos, rotation, gridOrigin);
            newModule.spawnedPrefab = spawned;
        }

        installedModules.Add(newModule);
        CalculateAndApplyStats();
        return true;
    }

    // Fungsi untuk melepas modul dari grid dan hapus prefab 3D-nya
    public void UninstallModule(PlacedModule module)
    {
        if (installedModules.Contains(module))
        {
            // Hapus prefab 3D dari kendaraan
            if (module.spawnedPrefab != null)
            {
                Destroy(module.spawnedPrefab);
            }

            installedModules.Remove(module);
            CalculateAndApplyStats();
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
        CalculateAndApplyStats();
    }
}
