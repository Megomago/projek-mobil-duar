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
        Engine,       // Mengaktifkan torsi dari VehicleController
        FuelTank,     // Mengaktifkan fuelCapacity dari VehicleBaseData
        Battery,      // Mengaktifkan batteryCapacity & batteryHealth dari VehicleBaseData
        Alternator,   // Mengaktifkan powerGeneration dari VehicleBaseData
        Capacitor,    // Mengaktifkan capacitor stats dari VehicleBaseData (jika ada)
        Other
    }

    [Header("Identitas Part")]
    public CriticalPartType partType = CriticalPartType.Engine;
    [Tooltip("Nama part untuk debug/log")]
    public string partName = "Unnamed Part";

    [Header("Durability")]
    public float maxHealth = 300f;
    [HideInInspector] public float currentHealth;

    [Header("Armor / DEF")]
    [Tooltip("Armor part ini. Berkontribusi ke DEF kendaraan sesuai tipenya.")]
    public float armor = 30f;

    [Header("Ledakan")]
    public bool volatileExplosive = false;
    public float explosionDamage = 200f;

    private VehicleStatsManager _statsManager;

    private void Awake()
    {
        currentHealth = maxHealth;
        _statsManager = GetComponentInParent<VehicleStatsManager>();
    }

    public void TakeDamage(float damage)
    {
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
