using UnityEngine;
using Weapons;

/// <summary>
/// Tempelkan script ini ke Empty GameObject (dengan BoxCollider) yang mewakili
/// komponen vital bawaan kendaraan (mesin, tangki, baterai, dll).
/// VehicleStatsManager akan scan ini otomatis via GetComponentsInChildren.
/// </summary>
public class VehicleCriticalPart : MonoBehaviour
{
    public enum CriticalPartType
    {
        Engine,
        FuelTank,
        Battery,
        Alternator,
        Capacitor,
        Other
    }

    [Header("Identitas Part")]
    public CriticalPartType partType = CriticalPartType.Engine;
    [Tooltip("Nama part untuk debug/log")]
    public string partName = "Unnamed Part";

    [Header("Durability")]
    public float maxHealth = 300f;
    [HideInInspector] public float currentHealth;
    [Tooltip("ONESHOT: part ini langsung hancur kena peluru apapun")]
    public bool isOneHitPart = false;

    [Header("Armor / DEF")]
    [Tooltip("Armor part ini")]
    public float armor = 30f;

    [Header("Power Settings")]
    [Tooltip("Konsumsi daya part ini (Watt)")]
    public float powerConsumption = 0f;
    [Tooltip("Produksi daya part ini (Watt)")]
    public float powerGeneration = 0f;
    [Tooltip("Kapasitas penyimpanan listrik tambahan (Wh)")]
    public float extraBatteryCapacity = 0f;

    [Header("Fuel Settings")]
    [Tooltip("Kapasitas penyimpanan bensin tambahan (L)")]
    public float extraFuelCapacity = 0f;

    [Header("Ammo Settings")]
    [Tooltip("Jumlah poin amunisi yang disediakan part ini. 0 = bukan sumber ammo.")]
    public int ammoPoint = 0;
    [HideInInspector] public float currentAmmoPoint;

    [Header("Capacitor Settings")]
    [Tooltip("Tambahan max output (W)")]
    public float extraMaxOutput = 0f;
    [Tooltip("Kapasitas energi kapasitor (Wh)")]
    public float capacitorCapacity = 0f;
    [Tooltip("Kecepatan isi daya kapasitor (W)")]
    public float chargeRate = 0f;

    [Header("UI Settings")]
    [Tooltip("Sembunyikan dari daftar modul UI")]
    public bool hideFromModuleList = false;

    [Header("Lamp Settings")]
    [Tooltip("Centang jika part ini adalah lampu. Lampu akan mati otomatis saat baterai habis.")]
    public bool isLamp = false;

    [Header("Ledakan")]
    public bool volatileExplosive = false;
    public float explosionDamage = 200f;
    public float explosionRadius = 5f;
    public GameObject explosionVFXPrefab;
    public AudioClip explosionSFX;

    [Header("Destroyed Effect")]
    [Tooltip("Prefab yang di-spawn pas part ini hancur (api/kobaran/asap)")]
    public GameObject destroyedPrefab;

    private VehicleStatsManager _statsManager;
    private Light[] _cachedLights;

    private void Awake()
    {
        currentHealth = maxHealth;
        currentAmmoPoint = ammoPoint;
        _statsManager = GetComponentInParent<VehicleStatsManager>();
        _cachedLights = GetComponentsInChildren<Light>(true);
    }

    public void UpdateLampState(float currentBattery, bool lightsOn)
    {
        if (!isLamp) return;

        // Cek toggle lampu (L) dulu
        if (!lightsOn)
        {
            SetLightsEnabled(false);
            return;
        }

        // Matikan lampu kalau sisa baterai (Wh) ga cukup buat nyalain lampu ini.
        // Konversi: powerConsumption (W) * waktu (jam) = energi (Wh).
        // Threshold: sisa baterai < daya lampu selama 1 menit (60/3600 jam).
        float minThresholdWh = powerConsumption * 60f / 3600f;
        bool enoughBattery = currentBattery > minThresholdWh;
        SetLightsEnabled(enoughBattery);
    }

    private void SetLightsEnabled(bool enabled)
    {
        foreach (var light in _cachedLights)
        {
            if (light != null)
                light.enabled = enabled;
        }
    }

    public void TakeDamage(float damage)
    {
        if (currentHealth <= 0f) return;

        if (isOneHitPart)
        {
            currentHealth = 0f;
            OnDestroyed();
            return;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        #if UNITY_EDITOR
        Debug.Log($"[CriticalPart] {partName} terkena damage {damage}! HP: {currentHealth}/{maxHealth}");
        #endif

        if (currentHealth <= 0f) OnDestroyed();
    }

    private void OnDestroyed()
    {
        #if UNITY_EDITOR
        Debug.Log($"[CriticalPart] {partName} HANCUR!");
        #endif

        Vector3 pos = transform.position;

        // Spawn destroyed prefab (api/kobaran)
        if (destroyedPrefab != null)
        {
            if (ObjectPool.Instance != null)
                ObjectPool.Instance.Spawn(destroyedPrefab, pos, Quaternion.identity);
            else
                Instantiate(destroyedPrefab, pos, Quaternion.identity);
        }

        // Efek ledakan kalo volatile
        if (volatileExplosive)
        {
            #if UNITY_EDITOR
            Debug.Log($"[CriticalPart] {partName} MELEDAK!");
            #endif

            if (explosionVFXPrefab != null)
            {
                if (ObjectPool.Instance != null)
                {
                    GameObject vfx = ObjectPool.Instance.Spawn(explosionVFXPrefab, pos, Quaternion.identity);
                    if (vfx != null)
                    {
                        float scale = explosionRadius * 0.15f;
                        vfx.transform.localScale = Vector3.one * Mathf.Max(scale, 0.5f);
                    }
                }
                else
                {
                    GameObject vfx = Instantiate(explosionVFXPrefab, pos, Quaternion.identity);
                    float scale = explosionRadius * 0.15f;
                    vfx.transform.localScale = Vector3.one * Mathf.Max(scale, 0.5f);
                }
            }

            if (explosionSFX != null)
                AudioSource.PlayClipAtPoint(explosionSFX, pos);
        }

        // Nonaktifkan collider & object → stats di-recalculate tanpa part ini
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        gameObject.SetActive(false);

        if (_statsManager != null)
            _statsManager.CalculateAndApplyStats();
    }
}
