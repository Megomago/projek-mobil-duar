using UnityEngine;
using TMPro; // Memerlukan TextMeshPro

public class VehicleHUD : MonoBehaviour
{
    [Header("Nama Kendaraan")]
    public TextMeshProUGUI vehicleNameText;

    [Header("Health & Armor – Bodi")]
    public TextMeshProUGUI bodyHPText;
    public TextMeshProUGUI bodyArmorText;   // DEF bodi (base + armor plate)

    [Header("Health & Armor – Roda")]
    public TextMeshProUGUI wheelHPText;
    public TextMeshProUGUI wheelArmorText;  // DEF roda

    [Header("Health & Armor – Mesin")]
    public TextMeshProUGUI engineHPText;
    public TextMeshProUGUI engineArmorText; // DEF mesin

    [Header("Health & Armor – Baterai")]
    public TextMeshProUGUI batteryHPText;
    public TextMeshProUGUI batteryArmorText;

    [Header("Performa – Bensin")]
    public TextMeshProUGUI motorTorqueText;
    public TextMeshProUGUI fuelCapacityText;
    public TextMeshProUGUI fuelConsumptionText;

    [Header("Performa – Kelistrikan")]
    public TextMeshProUGUI batteryCapacityText;
    public TextMeshProUGUI powerGenerationText;
    public TextMeshProUGUI powerConsumptionText;
    public TextMeshProUGUI maxPowerOutputText;
    public TextMeshProUGUI capacitorCapacityText;
    public TextMeshProUGUI capacitorChargeRateText;

    [Header("Berat")]
    public TextMeshProUGUI totalWeightText;

    // Referensi aktif ke VehicleStatsManager yang sedang ditampilkan
    private VehicleStatsManager _currentStats;

    // ─────────────────────────────────────────────────────────────────
    // API publik: panggil ini dari VehicleSelector setiap kali kendaraan
    // berganti, atau dari VehicleStatsManager saat CalculateAndApplyStats.
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Set kendaraan aktif yang ditampilkan di HUD, lalu refresh tampilan.
    /// </summary>
    public void SetVehicle(VehicleStatsManager statsManager)
    {
        _currentStats = statsManager;
        Refresh();
    }

    /// <summary>
    /// Refresh HUD menggunakan VehicleStatsManager yang sudah di-set.
    /// Aman dipanggil tanpa argumen (pakai _currentStats).
    /// </summary>
    public void Refresh()
    {
        if (_currentStats == null) return;
        UpdateHUD(_currentStats);
    }

    // ─────────────────────────────────────────────────────────────────
    // Fungsi utama update — bisa juga dipanggil langsung oleh
    // VehicleStatsManager (backward-compatible dengan kode lama).
    // ─────────────────────────────────────────────────────────────────
    public void UpdateHUD(VehicleStatsManager statsManager)
    {
        if (statsManager == null || statsManager.baseData == null) return;

        // Simpan referensi supaya Refresh() bisa pakai _currentStats
        _currentStats = statsManager;

        VehicleBaseData data = statsManager.baseData;

        // ── Nama Kendaraan ────────────────────────────────────────────
        if (vehicleNameText != null)
            vehicleNameText.text = data.vehicleName;

        // ── Health & Armor ────────────────────────────────────────────
        // Bodi — Chassis cuma armor (DEF), gada HP
        if (bodyHPText    != null) bodyHPText.text    = statsManager.currentBodyArmor.ToString("0") + " DEF";
        if (bodyArmorText != null) bodyArmorText.text  = statsManager.currentBodyArmor.ToString("0");

        // Roda
        if (wheelHPText    != null) wheelHPText.text    = statsManager.currentWheelHealth.ToString("0");
        if (wheelArmorText != null) wheelArmorText.text  = statsManager.currentWheelArmor.ToString("0");

        // Mesin & Baterai — baca langsung dari VehicleCriticalPart
        var critParts = statsManager.GetComponentsInChildren<VehicleCriticalPart>(false);
        float engineHP = 0, batteryHP = 0;
        foreach (var cp in critParts)
        {
            if (cp.partType == VehicleCriticalPart.CriticalPartType.Engine) engineHP = cp.currentHealth;
            else if (cp.partType == VehicleCriticalPart.CriticalPartType.Battery) batteryHP = cp.currentHealth;
        }
        if (engineHPText    != null) engineHPText.text    = engineHP.ToString("0");
        if (engineArmorText != null) engineArmorText.text  = statsManager.currentEngineArmor.ToString("0");
        if (batteryHPText    != null) batteryHPText.text    = batteryHP.ToString("0");
        if (batteryArmorText != null) batteryArmorText.text  = statsManager.currentBatteryArmor.ToString("0");

        // ── Performa – Bensin ─────────────────────────────────────────
        VehicleController vc = statsManager.GetComponent<VehicleController>();
        if (vc != null)
        {
            if (motorTorqueText     != null) motorTorqueText.text     = vc.engine.maxTorqueNm.ToString("0") + " Nm";
            if (fuelConsumptionText != null) fuelConsumptionText.text = vc.engine.maxFuelConsumptionRate.ToString("0.00") + " L/s";
        }
        else
        {
            if (motorTorqueText     != null) motorTorqueText.text     = "0 Nm";
            if (fuelConsumptionText != null) fuelConsumptionText.text = "0.00 L/s";
        }
        
        if (fuelCapacityText    != null) fuelCapacityText.text    = statsManager.currentFuelCapacity.ToString("0") + " L";

        // ── Performa – Kelistrikan ────────────────────────────────────
        if (batteryCapacityText    != null) batteryCapacityText.text    = statsManager.currentBatteryCapacity.ToString("0") + " Wh";
        if (powerGenerationText    != null) powerGenerationText.text    = statsManager.currentPowerGeneration.ToString("0") + " W";
        if (powerConsumptionText   != null) powerConsumptionText.text   = statsManager.currentPowerConsumption.ToString("0") + " W";
        if (maxPowerOutputText     != null) maxPowerOutputText.text     = statsManager.currentMaxOutput.ToString("0") + " W";
        if (capacitorCapacityText  != null) capacitorCapacityText.text  = statsManager.currentCapacitorCapacity.ToString("0") + " Wh";
        if (capacitorChargeRateText!= null) capacitorChargeRateText.text= statsManager.currentCapacitorChargeRate.ToString("0") + " W";

        // ── Berat ─────────────────────────────────────────────────────
        if (totalWeightText != null)
        {
            if (statsManager.currentTotalMass >= 10000f)
            {
                float massInTons = statsManager.currentTotalMass / 1000f;
                totalWeightText.text = massInTons.ToString("0.0") + " t";
            }
            else
            {
                totalWeightText.text = statsManager.currentTotalMass.ToString("0") + " kg";
            }
        }
    }
}
