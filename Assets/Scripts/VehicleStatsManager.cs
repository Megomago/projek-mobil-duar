using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlacedModule
{
    public ModuleTemplate moduleTemplate;
    
    [Tooltip("Posisi koordinat X, Y di grid")]
    public Vector2Int gridPosition;
    
    [Tooltip("Apakah modul diputar 90 derajat?")]
    public bool isRotated;
    
    [HideInInspector]
    public float currentHealth;

    // Referensi ke prefab 3D yang sudah di-spawn di kendaraan
    [HideInInspector]
    public GameObject spawnedPrefab;

    public PlacedModule(ModuleTemplate template, Vector2Int position, bool rotated)
    {
        moduleTemplate = template;
        gridPosition = position;
        isRotated = rotated;
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

    // Fungsi untuk menambah modul ke grid dan spawn prefab 3D-nya di kendaraan
    public bool InstallModule(ModuleTemplate template, Vector2Int position, bool rotated)
    {
        // TODO: Tambahkan validasi Tetris placement (cek apakah grid kosong)
        
        PlacedModule newModule = new PlacedModule(template, position, rotated);

        // Spawn prefab 3D di posisi grid kendaraan
        if (template.modulePrefab != null && gridOrigin != null)
        {
            // Hitung posisi dunia berdasarkan koordinat grid
            int sizeX = rotated ? template.height : template.width;
            int sizeY = rotated ? template.width : template.height;
            
            float offsetX = (position.x + sizeX / 2f) * cellSize;
            float offsetZ = (position.y + sizeY / 2f) * cellSize;
            
            Vector3 localPos = new Vector3(offsetX, 0f, offsetZ);
            Vector3 worldPos = gridOrigin.TransformPoint(localPos);
            
            Quaternion rotation = gridOrigin.rotation;
            if (rotated)
            {
                rotation *= Quaternion.Euler(0f, 90f, 0f);
            }

            GameObject spawned = Instantiate(template.modulePrefab, worldPos, rotation, gridOrigin);
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
}
