using UnityEngine;

[CreateAssetMenu(fileName = "New Vehicle Data", menuName = "Vehicle/Base Data")]
public class VehicleBaseData : ScriptableObject
{
    [Header("Base Stats")]
    public string vehicleName = "Camaro Mod Mk.II";
    
    [Tooltip("Massa dasar kendaraan dalam Kilogram (kg). Contoh: 1900 kg = 1.9 t")]
    public float baseMass = 1900f; 

    [Header("Chassis Armor")]
    [Tooltip("Armor sasis untuk melindungi critical parts (Engine, FuelTank, Battery, dll)")]
    public float chassisArmor = 230f;
}
