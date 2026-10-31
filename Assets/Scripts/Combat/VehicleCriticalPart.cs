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
    [Tooltip("Penanda part eksplosif (info saja). VFX/SFX ledakan main OTOMATIS saat part hancur asal di-assign — gak perlu dicentang.")]
    public bool volatileExplosive = false;
    public float explosionDamage = 200f;
    public float explosionRadius = 5f;
    public GameObject explosionVFXPrefab;
    [Tooltip("Hapus otomatis explosion VFX setelah X detik. 0 = tidak dihapus otomatis (atur durasi di prefab).")]
    public float explosionVFXDespawnTime = 0f;
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

        // Spawn destroyed prefab (api/kobaran). Di-parent ke root kendaraan supaya
        // efek ikut bergeser bersama bangkai, bukan tertinggal di titik hancur.
        if (destroyedPrefab != null)
            SpawnEffectAttachedToVehicle(destroyedPrefab);

        // Efek ledakan: main kalau VFX/SFX-nya di-assign. Sebelumnya digated flag
        // volatileExplosive → VFX udah dipasang tapi gak pernah main kalau flag-nya
        // lupa dicentang. Kehadiran VFX = niat memasang ledakan.
        if (explosionVFXPrefab != null || explosionSFX != null)
        {
            #if UNITY_EDITOR
            Debug.Log($"[CriticalPart] {partName} MELEDAK!");
            #endif

            if (explosionVFXPrefab != null)
            {
                // Skala di-set sebelum di-parent supaya tetap jadi skala dunia.
                float scale = Mathf.Max(explosionRadius * 0.15f, 0.5f);
                GameObject vfx = SpawnEffectAttachedToVehicle(explosionVFXPrefab, Vector3.one * scale);
                if (vfx != null && explosionVFXDespawnTime > 0f)
                {
                    if (ObjectPool.Instance != null)
                        ObjectPool.Instance.Despawn(vfx, explosionVFXDespawnTime);
                    else
                        Destroy(vfx, explosionVFXDespawnTime);
                }
            }

            if (explosionSFX != null)
                AudioPool.PlayClipAtPoint(explosionSFX, pos, 1f, Random.Range(0.92f, 1.08f), 1f, 150f);
        }

        // Nonaktifkan collider & object → stats di-recalculate tanpa part ini
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        gameObject.SetActive(false);

        if (_statsManager != null)
            _statsManager.MarkStatsDirty();
    }

    /// <summary>
    /// Spawn VFX lalu tempelkan ke root kendaraan supaya ikut bergerak bersama bangkai.
    /// Tanpa ini api/asap tertinggal di titik hancur saat mobil bergeser.
    ///
    /// Sengaja tidak di-parent ke part ini karena part di-SetActive(false) di akhir
    /// OnDestroyed — kalau di-parent ke sini, VFX-nya langsung ikut mati.
    /// </summary>
    private GameObject SpawnEffectAttachedToVehicle(GameObject prefab, Vector3? worldScale = null)
    {
        if (prefab == null) return null;

        GameObject fx = ObjectPool.Instance != null
            ? ObjectPool.Instance.Spawn(prefab, transform.position, Quaternion.identity)
            : Instantiate(prefab, transform.position, Quaternion.identity);

        if (fx == null) return null;

        // Set skala selagi belum di-parent → localScale == worldScale.
        if (worldScale.HasValue)
            fx.transform.localScale = worldScale.Value;

        // Root kendaraan. Fallback: root hierarchy (kalau part standalone).
        Transform anchor = _statsManager != null ? _statsManager.transform : transform.root;
        if (anchor == null || anchor == transform)
            anchor = transform.parent;

        // worldPositionStays = true → posisi/rotasi/skala dunia tetap saat di-parent.
        if (anchor != null)
            fx.transform.SetParent(anchor, true);

        return fx;
    }
}
