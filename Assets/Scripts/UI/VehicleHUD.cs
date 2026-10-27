using System.Collections.Generic;
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

    [Header("Amunisi")]
    [Tooltip("Sisa amunisi kendaraan (semua ammo box + critical part). Kosongkan referensi kalau tidak dipakai.")]
    public TextMeshProUGUI ammoPoolText;

    // Referensi aktif ke VehicleStatsManager yang sedang ditampilkan
    private VehicleStatsManager _currentStats;
    private readonly List<VehicleCriticalPart> _critPartsCache = new List<VehicleCriticalPart>(16);

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
        if (bodyHPText    != null) bodyHPText.SetText("{0:0} DEF", statsManager.currentChassisArmor);
        if (bodyArmorText != null) bodyArmorText.SetText("{0:0}", statsManager.currentChassisArmor);

        // Roda, Mesin & Baterai — baca langsung dari VehicleCriticalPart
        _critPartsCache.Clear();
        statsManager.GetComponentsInChildren(false, _critPartsCache);
        float engineHP = 0, engineArmor = 0, batteryHP = 0, batteryArmor = 0, wheelHP = 0, wheelArmor = 0;
        foreach (var cp in _critPartsCache)
        {
                     if (cp.partType == VehicleCriticalPart.CriticalPartType.Engine)  { engineHP = cp.currentHealth; engineArmor = cp.armor; }
                else if (cp.partType == VehicleCriticalPart.CriticalPartType.Battery) { batteryHP = cp.currentHealth; batteryArmor = cp.armor; }
                else if (cp.partType == VehicleCriticalPart.CriticalPartType.Other)   { wheelHP = cp.currentHealth; wheelArmor = cp.armor; }
        }
        if (wheelHPText     != null) wheelHPText.SetText("{0:0}", wheelHP);
        if (wheelArmorText  != null) wheelArmorText.SetText("{0:0}", wheelArmor);
        if (engineHPText    != null) engineHPText.SetText("{0:0}", engineHP);
        if (engineArmorText != null) engineArmorText.SetText("{0:0}", engineArmor);
        if (batteryHPText   != null) batteryHPText.SetText("{0:0}", batteryHP);
        if (batteryArmorText!= null) batteryArmorText.SetText("{0:0}", batteryArmor);

        // ── Performa – Bensin ─────────────────────────────────────────
        VehicleController vc = statsManager.GetComponent<VehicleController>();
        if (vc != null)
        {
            if (motorTorqueText     != null) motorTorqueText.SetText("{0:0} Nm", vc.engine.maxTorqueNm);
            if (fuelConsumptionText != null) fuelConsumptionText.SetText("{0:0.00} L/s", vc.engine.maxFuelConsumptionRate);
        }
        else
        {
            if (motorTorqueText     != null) motorTorqueText.text     = "0 Nm";
            if (fuelConsumptionText != null) fuelConsumptionText.text = "0.00 L/s";
        }
        
        if (fuelCapacityText    != null) fuelCapacityText.SetText("{0:0} L", statsManager.currentFuelCapacity);

        // ── Performa – Kelistrikan ────────────────────────────────────
        if (batteryCapacityText    != null) batteryCapacityText.SetText("{0:0} Wh", statsManager.currentBatteryCapacity);
        if (powerGenerationText    != null)
        {
            float totalGen = statsManager.currentPowerGeneration + statsManager.enginePowerGeneration;
            powerGenerationText.SetText("{0:0} W", totalGen);
        }
        if (powerConsumptionText   != null) powerConsumptionText.SetText("{0:0} W", statsManager.currentPowerConsumption);
        if (maxPowerOutputText     != null) maxPowerOutputText.SetText("{0:0} W", statsManager.currentMaxOutput);
        if (capacitorCapacityText  != null) capacitorCapacityText.SetText("{0:0} Wh", statsManager.currentCapacitorCapacity);
        if (capacitorChargeRateText!= null) capacitorChargeRateText.SetText("{0:0} W", statsManager.currentCapacitorChargeRate);

        // ── Amunisi ───────────────────────────────────────────────────
        if (ammoPoolText != null)
            ammoPoolText.SetText("{0:0} pts", statsManager.totalAmmoPoints);

        // ── Berat ─────────────────────────────────────────────────────
        if (totalWeightText != null)
        {
            if (statsManager.currentTotalMass >= 10000f)
            {
                float massInTons = statsManager.currentTotalMass / 1000f;
                totalWeightText.SetText("{0:0.0} t", massInTons);
            }
            else
            {
                totalWeightText.SetText("{0:0} kg", statsManager.currentTotalMass);
            }
        }
    }
}
