using UnityEngine;

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

    private VehicleStatsManager _statsManager;

    private void Awake()
    {
        currentHealth = maxHealth;
        _statsManager = GetComponentInParent<VehicleStatsManager>();
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
        Light[] lights = GetComponentsInChildren<Light>(true);
        foreach (var light in lights)
            light.enabled = enabled;
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

        Debug.Log($"[CriticalPart] {partName} terkena damage {damage}! HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f) OnDestroyed();
    }

    private void OnDestroyed()
    {
        Debug.Log($"[CriticalPart] {partName} HANCUR!");

        if (volatileExplosive)
            Debug.Log($"[CriticalPart] {partName} MELEDAK!");

        // Nonaktifkan collider & object → scan berikutnya tidak akan mendeteksinya
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        gameObject.SetActive(false);

        // Beritahu StatsManager recalculate
        if (_statsManager != null)
            _statsManager.CalculateAndApplyStats();
    }
}
