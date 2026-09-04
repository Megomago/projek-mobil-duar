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
    Engine,
    Other
}

[CreateAssetMenu(fileName = "New Module Template", menuName = "Vehicle/Module Template")]
public class ModuleTemplate : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unik STABIL untuk save/load. Digenerate otomatis — jangan diubah manual.")]
    [SerializeField] private string uid;
    public string UID => uid;

    [Header("Basic Info")]
    public string moduleName = "New Module";
    public ModuleType moduleType = ModuleType.Other;
    public Sprite moduleIcon;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(uid))
            uid = System.Guid.NewGuid().ToString("N");
    }
    
    [Header("Weapon Data (Hanya untuk Tipe Senjata)")]
    [Tooltip("Masukkan WeaponData jika modul ini adalah Senjata. Jika diisi, Nama, Icon, dan Prefab 3D akan otomatis mengambil dari WeaponData.")]
    public Weapons.WeaponData weaponData;
    
    [Header("Visual (Untuk Tipe Non-Senjata)")]
    [Tooltip("Prefab 3D model modul ini. Jika tipe Weapon, sistem akan memprioritaskan weaponData.weapon3DPrefab.")]
    public GameObject modulePrefab;

    [Header("Grid Dimensions (1x1 = 25 cm)")]
    [Tooltip("Lebar modul dalam grid (X)")]
    public int width = 1;
    [Tooltip("Panjang modul dalam grid (Y)")]
    public int height = 1;

    [Header("Clearance Settings")]
    [Tooltip("Apakah modul ini berukuran kecil/pipih sehingga bisa ditaruh di bawah laras senjata (clearance zone)?")]
    public bool isSmall = false;
    [Tooltip("Aktifkan ini jika modul memiliki laras atau atap yang menonjol dan memakan ruang (Clearance Zone)")]
    public bool enableClearance = false;
    [Tooltip("Aktifkan agar modul isSmall boleh masuk ke clearance zone modul ini")]
    public bool enableAccessClearance = true;
    [Tooltip("Ekstra grid clearance ke KANAN")]
    public int clearanceRight = 0;
    [Tooltip("Ekstra grid clearance ke KIRI")]
    public int clearanceLeft = 0;
    [Tooltip("Ekstra grid clearance ke DEPAN")]
    public int clearanceFront = 0;
    [Tooltip("Ekstra grid clearance ke BELAKANG")]
    public int clearanceBack = 0;

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

    [Header("UI Settings")]
    [Tooltip("Sembunyikan modul ini dari daftar modul UI (misal lampu, dekorasi)")]
    public bool hideFromModuleList = false;

    [Header("Ammo Settings")]
    [Tooltip("Jumlah poin amunisi yang disediakan modul ini. 0 = bukan modul ammo.")]
    public int ammoPoint = 0;

    [Header("Aerodynamics")]
    [Tooltip("Tambahan luas frontal (m²) dari modul ini. Modul kotak besar ~0.1-0.3, modul pipih kecil ~0.02")]
    public float dragModifier = 0f;

    [Header("Explosion Risk")]
    [Tooltip("Apakah modul ini mudah meledak jika hancur? (Misal aki, generator, fuel barrel)")]
    public bool volatileExplosive = false;
    [Tooltip("Radius damage ledakan berantai di grid jika modul hancur")]
    public int explosionRadius = 1;
    [Tooltip("Damage ledakan berantai")]
    public float explosionDamage = 100f;
    [Tooltip("Prefab VFX ledakan saat modul ini meledak (WarFX atau kustom).")]
    public GameObject explosionVFXPrefab;
    [Tooltip("Clip suara ledakan saat modul ini meledak.")]
    public AudioClip explosionSFX;
}
