using UnityEngine;
using TMPro; // Memerlukan TextMeshPro

public class VehicleHUD : MonoBehaviour
{
    [Header("Health & Armor (Bagian Atas)")]
    public TextMeshProUGUI bodyHPText;
    public TextMeshProUGUI bodyArmorText;
    public TextMeshProUGUI wheelHPText;
    public TextMeshProUGUI wheelArmorText;
    public TextMeshProUGUI engineHPText;
    public TextMeshProUGUI engineArmorText;

    [Header("Konsumsi Listrik & Bensin (Kiri Atas)")]
    public TextMeshProUGUI fuelConsumptionText;
    public TextMeshProUGUI powerConsumptionText;
    
    [Header("Stats Kendaraan Utama (Kiri Bawah)")]
    public TextMeshProUGUI horsePowerText;
    public TextMeshProUGUI batteryCapacityText;
    public TextMeshProUGUI totalWeightText;
    public TextMeshProUGUI fuelCapacityText;
    public TextMeshProUGUI powerGenerationText;
    public TextMeshProUGUI maxPowerOutputText;

    // Fungsi ini dipanggil otomatis oleh VehicleStatsManager
    public void UpdateHUD(VehicleStatsManager statsManager)
    {
        if (statsManager == null || statsManager.baseData == null) return;
        
        VehicleBaseData data = statsManager.baseData;

        // 1. Update HP & Armor dasar
        if (bodyHPText != null) bodyHPText.text = data.bodyHealth.ToString("0");
        if (bodyArmorText != null) bodyArmorText.text = data.bodyArmor.ToString("0");
        if (wheelHPText != null) wheelHPText.text = data.wheelHealth.ToString("0");
        if (wheelArmorText != null) wheelArmorText.text = data.wheelArmor.ToString("0");
        if (engineHPText != null) engineHPText.text = data.engineHealth.ToString("0");
        if (engineArmorText != null) engineArmorText.text = data.engineArmor.ToString("0");

        // 2. Update Performa Atas (Konsumsi saat ini)
        if (fuelConsumptionText != null) 
            fuelConsumptionText.text = data.fuelConsumptionRate.ToString("0.00") + "L/KM";
        
        // Tampilkan total sedotan listrik aktif saat ini (dari modul yang terpasang)
        if (powerConsumptionText != null) 
            powerConsumptionText.text = statsManager.currentPowerConsumption.ToString("0") + " W";

        // 3. Update Stat Bawah (Menggunakan data hasil kalkulasi modul dinamis)
        if (horsePowerText != null) horsePowerText.text = data.horsePower.ToString("0") + " hp";
        
        // Kapasitas baterai (Base + Aki Cadangan)
        if (batteryCapacityText != null) 
            batteryCapacityText.text = statsManager.currentBatteryCapacity.ToString("0") + " Wh";
        
        // Konversi berat total (kg) ke Ton (t)
        float massInTons = statsManager.currentTotalMass / 1000f;
        if (totalWeightText != null) totalWeightText.text = massInTons.ToString("0.0") + " t";

        // Kapasitas tangki bensin (Base + Drum Cadangan)
        if (fuelCapacityText != null) 
            fuelCapacityText.text = statsManager.currentFuelCapacity.ToString("0") + " L";

        // Total pasokan daya listrik (Base + Generator/Solar Panel)
        if (powerGenerationText != null) 
            powerGenerationText.text = statsManager.currentPowerGeneration.ToString("0") + " W";

        // Batas output kelistrikan (Base + Kapasitor Tambahan)
        if (maxPowerOutputText != null) 
            maxPowerOutputText.text = statsManager.currentMaxOutput.ToString("0") + " W";
    }
}
