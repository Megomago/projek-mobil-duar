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

    [Header("Aerodynamics")]
    [Tooltip("Koefisien drag (Cd) basis kendaraan. 0.35-0.40 untuk mobil ringan, 0.40-0.50 untuk SUV lapis baja")]
    public float baseDragCd = 0.40f;
    [Tooltip("Luas frontal (m²) basis kendaraan. ~2.2 untuk mobil ukuran sedang")]
    public float baseFrontalArea = 2.2f;
}
