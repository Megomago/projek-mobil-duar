using UnityEngine;
using Weapons;

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
        var template = placedModuleData.moduleTemplate;
        Vector3 pos = transform.position;

        // VFX
        if (template.explosionVFXPrefab != null && ObjectPool.Instance != null)
        {
            GameObject vfx = ObjectPool.Instance.Spawn(template.explosionVFXPrefab, pos, Quaternion.identity);
            if (vfx != null)
            {
                float vfxScale = template.explosionRadius * 0.25f * 0.5f;
                vfx.transform.localScale = Vector3.one * Mathf.Max(vfxScale, 0.5f);
            }
        }

        // SFX
        if (template.explosionSFX != null)
            AudioSource.PlayClipAtPoint(template.explosionSFX, pos);

        // Camera shake
        if (VehicleCamera.Instance != null)
            VehicleCamera.Instance.Shake(Mathf.Min(template.explosionDamage * 0.0005f, 1.5f), Mathf.Min(template.explosionRadius * 0.1f, 0.5f));

        Debug.Log($"BOOM! {template.moduleName} MELEDAK dengan radius {template.explosionRadius}!");
    }
}
