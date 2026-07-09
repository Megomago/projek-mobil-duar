using UnityEngine;
using Weapons;

/// <summary>
/// Script ini ditempelkan ke PREFAB 3D dari modul (misal model Tangki Bensin, Armor Plate, Generator, dll).
/// Pastikan prefab memiliki Collider (misal BoxCollider) agar bisa menerima tembakan/damage.
/// </summary>
public class VehicleModuleComponent : MonoBehaviour
{
    [Header("Module Data")]
    [Tooltip("Data runtime modul ini saat dipasang di grid. Untuk modul standalone (tidak dipasang di kendaraan), isi langsung Template-nya.")]
    public PlacedModule placedModuleData;

    [Tooltip("Template untuk modul standalone (tidak perlu placedModuleData). Prioritas: placedModuleData.moduleTemplate > moduleTemplate.")]
    public ModuleTemplate moduleTemplate;

    [Tooltip("Referensi ke manager kendaraan tempat modul ini dipasang.")]
    public VehicleStatsManager statsManager;

    [Header("Baked Prefab - Grid Position (isi manual di prefab)")]
    public Vector2Int bakedGridPosition;
    public int bakedRotationAngle;

    [Header("Status (Read Only)")]
    public float currentHealth;
    public bool isDestroyed = false;

    /// <summary>
    /// Dipanggil otomatis oleh VehicleStatsManager saat modul dipasang ke mobil.
    /// </summary>
    private ModuleTemplate EffectiveTemplate
    {
        get
        {
            if (placedModuleData != null && placedModuleData.moduleTemplate != null)
                return placedModuleData.moduleTemplate;
            return moduleTemplate;
        }
    }

    void Awake()
    {
        var t = EffectiveTemplate;
        currentHealth = t != null ? t.maxHealth : 100f;
    }

    public void Initialize(PlacedModule data, VehicleStatsManager manager)
    {
        placedModuleData = data;
        statsManager = manager;
        currentHealth = data.moduleTemplate.maxHealth;
        isDestroyed = false;
    }

    public void TakeDamage(float damageAmount)
    {
        if (isDestroyed) return;

        currentHealth -= damageAmount;

        if (placedModuleData != null)
            placedModuleData.currentHealth = currentHealth;

        if (currentHealth <= 0f)
        {
            DestroyModule();
        }
    }

    private void DestroyModule()
    {
        isDestroyed = true;

        var template = EffectiveTemplate;
        bool shouldExplode = template != null && template.volatileExplosive;

        Debug.Log($"Modul {(template != null ? template.moduleName : gameObject.name)} HANCUR!");

        if (shouldExplode)
        {
            Explode(template);
        }

        if (statsManager != null && placedModuleData != null)
        {
            statsManager.UninstallModule(placedModuleData);
        }

        Destroy(gameObject);
    }

    private void Explode(ModuleTemplate template)
    {
        Vector3 pos = transform.position;

        if (template.explosionVFXPrefab != null && ObjectPool.Instance != null)
        {
            GameObject vfx = ObjectPool.Instance.Spawn(template.explosionVFXPrefab, pos, Quaternion.identity);
            if (vfx != null)
            {
                float vfxScale = template.explosionRadius * 0.25f * 0.5f;
                vfx.transform.localScale = Vector3.one * Mathf.Max(vfxScale, 0.5f);
            }
        }

        if (template.explosionSFX != null)
            AudioSource.PlayClipAtPoint(template.explosionSFX, pos);

        Debug.Log($"BOOM! {template.moduleName} MELEDAK dengan radius {template.explosionRadius}!");
    }
}
