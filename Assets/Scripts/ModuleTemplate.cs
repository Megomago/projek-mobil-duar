using UnityEngine;

public enum ModuleType
{
    Weapon,
    Battery,
    Generator,
    SolarPanel,
    FuelBarrel,
    ArmorPlate,
    Capacitor,
    Cargo,
    Other
}

[CreateAssetMenu(fileName = "New Module Template", menuName = "Vehicle/Module Template")]
public class ModuleTemplate : ScriptableObject
{
    [Header("Basic Info")]
    public string moduleName = "New Module";
    public ModuleType moduleType = ModuleType.Other;
    public Sprite moduleIcon;
    
    [Tooltip("Prefab 3D model modul ini. Akan di-spawn di kendaraan saat modul dipasang.")]
    public GameObject modulePrefab;

    [Header("Grid Dimensions (1x1 = 25 cm)")]
    [Tooltip("Lebar modul dalam grid (X)")]
    public int width = 1;
    [Tooltip("Panjang modul dalam grid (Y)")]
    public int height = 1;

    [Header("Weight & Durability")]
    [Tooltip("Berat modul dalam Kilogram (kg). Akan ditambahkan ke Rigidbody.")]
    public float weight = 50f;
    [Tooltip("HP modul saat berada di grid")]
    public float maxHealth = 100f;
    [Tooltip("Armor pertahanan modul")]
    public float armor = 10f;

    [Header("Power Settings")]
    [Tooltip("Penyedotan listrik (konsumsi daya) dalam Watt saat aktif")]
    public float powerConsumption = 0f;
    [Tooltip("Produksi listrik (generator/solar panel) dalam Watt")]
    public float powerGeneration = 0f;
    [Tooltip("Kapasitas penyimpanan listrik tambahan (Wh) jika tipe Baterai")]
    public float extraBatteryCapacity = 0f;

    [Header("Fuel Settings")]
    [Tooltip("Kapasitas penyimpanan bensin tambahan (L) jika tipe FuelBarrel")]
    public float extraFuelCapacity = 0f;

    [Header("Capacitor Settings")]
    [Tooltip("Tambahan max output (W) saat kapasitor aktif")]
    public float extraMaxOutput = 0f;
    [Tooltip("Kapasitas energi yang bisa disimpan kapasitor (Wh)")]
    public float capacitorCapacity = 0f;
    [Tooltip("Kecepatan kapasitor mengisi daya dari surplus listrik (W)")]
    public float chargeRate = 0f;

    [Header("Explosion Risk")]
    [Tooltip("Apakah modul ini mudah meledak jika hancur? (Misal aki, generator, fuel barrel)")]
    public bool volatileExplosive = false;
    [Tooltip("Radius damage ledakan berantai di grid jika modul hancur")]
    public int explosionRadius = 1;
    [Tooltip("Damage ledakan berantai")]
    public float explosionDamage = 100f;
}
