using UnityEngine;

[CreateAssetMenu(fileName = "New Vehicle Data", menuName = "Vehicle/Base Data")]
public class VehicleBaseData : ScriptableObject
{
    [Header("Base Stats")]
    public string vehicleName = "Camaro Mod Mk.II";
    
    [Tooltip("Massa dasar kendaraan dalam Kilogram (kg). Contoh: 1900 kg = 1.9 t")]
    public float baseMass = 1900f; 

    [Header("Health & Armor (Gambar 1)")]
    public float bodyHealth = 1300f;
    public float bodyArmor = 230f;
    
    public float wheelHealth = 50f;
    public float wheelArmor = 5f;
    
    public float engineHealth = 200f;
    public float engineArmor = 100f;

    [Header("Performance (Gambar 2)")]
    public float fuelCapacity = 80f; // Liter
    
    [Header("Electrical")]
    [Tooltip("Apakah baterai/aki menyatu dengan mesin? (Jika false, aki diletakkan terpisah seperti di truk)")]
    public bool isBatteryJoinEngine = true;
    
    [Tooltip("HP Baterai (digunakan jika isBatteryJoinEngine = false)")]
    public float batteryHealth = 50f;
    [Tooltip("Armor Baterai (digunakan jika isBatteryJoinEngine = false)")]
    public float batteryArmor = 10f;
    
    public float batteryCapacity = 720f; // Wh
    public float powerGeneration = 95f; // W
    public float maxPowerOutput = 1000f; // W
}
