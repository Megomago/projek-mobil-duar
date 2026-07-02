using UnityEngine;

/// <summary>
/// Script ini ditempelkan ke PREFAB 3D dari modul (misal model Tangki Bensin, Armor Plate, Generator, dll).
/// Pastikan prefab memiliki Collider (misal BoxCollider) agar bisa menerima tembakan/damage.
/// </summary>
public class VehicleModuleComponent : MonoBehaviour
{
    [Header("Module Data (Auto-Linked at Runtime)")]
    [Tooltip("Data runtime modul ini saat dipasang di grid.")]
    public PlacedModule placedModuleData;
    
    [Tooltip("Referensi ke manager kendaraan tempat modul ini dipasang.")]
    public VehicleStatsManager statsManager;

    [Header("Status (Read Only)")]
    public float currentHealth;
    public bool isDestroyed = false;

    /// <summary>
    /// Dipanggil otomatis oleh VehicleStatsManager saat modul dipasang ke mobil.
    /// </summary>
    public void Initialize(PlacedModule data, VehicleStatsManager manager)
    {
        placedModuleData = data;
        statsManager = manager;
        currentHealth = data.moduleTemplate.maxHealth;
        isDestroyed = false;
    }

    /// <summary>
    /// Fungsi untuk menerima damage (dipanggil oleh KinematicProjectile).
    /// Damage udah dikalkulasi pake OptFormula dari projectile.
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        if (isDestroyed || placedModuleData == null) return;

        currentHealth -= damageAmount;
        placedModuleData.currentHealth = currentHealth;

        if (currentHealth <= 0f)
        {
            DestroyModule();
        }
    }

    private void DestroyModule()
    {
        isDestroyed = true;
        Debug.Log($"Modul {placedModuleData.moduleTemplate.moduleName} HANCUR!");

        // Jika modul ini mudah meledak (seperti tangki bensin / aki)
        if (placedModuleData.moduleTemplate.volatileExplosive)
        {
            Explode();
        }

        // Suruh StatsManager untuk melepas dan membuang modul ini dari grid
        if (statsManager != null)
        {
            statsManager.UninstallModule(placedModuleData);
        }

        // Hancurkan game object fisik (atau ganti jadi model hancur)
        Destroy(gameObject); 
    }

    private void Explode()
    {
        Debug.Log($"BOOM! {placedModuleData.moduleTemplate.moduleName} MELEDAK dengan radius {placedModuleData.moduleTemplate.explosionRadius}!");
        // TODO: Tambahkan efek partikel ledakan, suara, dan damage area ke modul lain di sekitarnya.
    }
}
