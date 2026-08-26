using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// ScriptableObject berisi SEMUA data/value konfigurasi senjata.
    /// Buat asset baru: Right Click → Create → Weapons → Weapon Data.
    /// </summary>
    [CreateAssetMenu(fileName = "New WeaponData", menuName = "Weapons/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        #region --- 0. IDENTITY & HUD ---
        [Header("=== IDENTITY & HUD ===")]
        [Tooltip("Nama senjata yang akan ditampilkan di HUD (misal: 40mm Penet)")]
        public string weaponName = "Unnamed Weapon";
        [Tooltip("Ikon senjata untuk ditampilkan di Inventory Grid")]
        public Sprite weaponIcon;
        [Tooltip("Prefab 3D senjata yang memiliki script ModularWeapon. Akan di-spawn di pivot mobil.")]
        public GameObject weapon3DPrefab;
        [Tooltip("Prefab HUD khusus senjata ini (Panel berisi WeaponUIManager + Teks). BUKAN Canvas, cukup Panel/Empty.")]
        public GameObject hudPrefab;
        #endregion

        #region --- 1. CORE SETTINGS ---
        [Header("=== CORE SETTINGS ===")]
        [Tooltip("Kecepatan peluru saat keluar dari laras (m/s)")]
        public float muzzleVelocity = 800f;
        [Tooltip("Tembakan per menit (RPM)")]
        public float fireRateRPM = 600f;

        [Tooltip("Raw damage value")]
        public float attackPower = 100f;
        [Tooltip("Penetration power vs DEF")]
        public float penetration = 150f;

        [Tooltip("Poin amunisi pool yang dikonsumsi per 1 peluru saat RELOAD mengisi magazine (bukan saat menembak). Contoh: magazine 30 & cost 10 → 1x reload penuh nyedot 300 ammoPoint. 0 = reload gratis/infinite.")]
        public int ammoCostPerShot = 1;

        [Tooltip("Batas amunisi dalam satu magazine (0 = Infinite)")]
        public int maxAmmo = 30;
        [Tooltip("Otomatis melakukan reload saat peluru habis?")]
        public bool autoReload = false;
        [Tooltip("Waktu yang dibutuhkan untuk reload (detik)")]
        public float reloadTime = 2f;
        #endregion

        #region --- 2. PROJECTILE & SHOTGUN ---
        [Header("=== PROJECTILE & SHOTGUN MECHANIC ===")]
        [Tooltip("Prefab peluru yang memiliki script KinematicProjectile")]
        public GameObject projectilePrefab;
        [Tooltip("Jumlah peluru yang keluar dalam 1x tembakan (Misal 8 untuk shotgun). Hanya mengurangi 1 Ammo.")]
        [Min(1)] public int pelletCount = 1;
        #endregion

        #region --- 3. DISPERSION & ACCURACY ---
        [Header("=== DISPERSION & ACCURACY ===")]
        [Tooltip("Penyebaran dasar peluru dalam derajat (0 = Lurus presisi)")]
        [Range(0f, 15f)] public float baseDispersion = 0.5f;
        [Tooltip("Pengali keketatan sebaran (Choke). 1.0 = Normal, 0.5 = Sangat rapat ke tengah. Bisa dimodifikasi oleh modul item.")]
        [Range(0.1f, 2f)] public float chokeMultiplier = 1f;
        #endregion

        #region --- 4. OVERHEAT SYSTEM ---
        [Header("=== OVERHEAT SYSTEM ===")]
        public bool enableOverheat = false;
        [Tooltip("Panas yang bertambah tiap 1 peluru tertembak")]
        public float heatPerShot = 5f;
        [Tooltip("Panas yang berkurang per detik saat tidak menembak")]
        public float coolingRate = 15f;
        [Tooltip("Kapasitas maksimum panas sebelum Jammed/Overheat penuh")]
        public float maxHeat = 100f;
        [Tooltip("Saat overheat 100%, dispersi tembakan dikali berapa? (Bikin akurasi sangat buruk)")]
        public float heatDispersionMultiplier = 4f;
        [Tooltip("Ambang batas panas (0.0 - 1.0) sebelum akurasi mulai memburuk. Contoh: 0.8 berarti mulai goyang saat panas 80%")]
        [Range(0f, 1f)] public float overheatDispersionThreshold = 0.8f;
        #endregion

        #region --- 5. RECOIL (VEHICLE PHYSICS) ---
        [Header("=== RECOIL (VEHICLE PHYSICS) ===")]
        [Tooltip("Gaya dorong mundur yang diaplikasikan ke kendaraan saat menembak")]
        public float recoilForce = 500f;
        
        [Tooltip("Pengali seberapa kuat guncangan kamera berdasarkan recoil (contoh: 0.0005)")]
        public float cameraShakeMultiplier = 0.0005f;
        [Tooltip("Durasi guncangan kamera saat menembak (detik)")]
        public float cameraShakeDuration = 0.2f;
        #endregion

        #region --- 6. AUDIO ---
        [Header("=== AUDIO ===")]
        public AudioClip shootSound;
        public AudioClip reloadSound;
        public AudioClip emptyClickSound;

        [Header("=== AUDIO VARIATION ===")]
        [Tooltip("Pitch random range (1.0 = original). Senjata berat → pitch rendah, ringan → tinggi")]
        public float minPitch = 0.85f;
        public float maxPitch = 1.0f;
        [Tooltip("Volume random range (1.0 = original)")]
        public float minVolume = 0.9f;
        public float maxVolume = 1.0f;
        #endregion

        #region --- 7. CASING EJECTION ---
        [Header("=== CASING EJECTION ===")]
        [Tooltip("Prefab selongsong peluru yang terlempar (Opsional)")]
        public GameObject casingPrefab;
        [Tooltip("Waktu tunda sebelum selongsong terlempar (detik)")]
        public float casingEjectDelay = 0f;
        [Tooltip("Kekuatan dasar lemparan selongsong relatif terhadap Ejection Port (X=Kanan, Y=Atas, Z=Maju)")]
        public Vector3 casingEjectForce = new Vector3(4f, 1.5f, -0.75f);
        [Tooltip("Acak kekuatan lemparan (+/- dari kekuatan dasar)")]
        public Vector3 casingEjectRandomness = new Vector3(1f, 0.5f, 0.25f);
        #endregion

        #region --- 8. MUZZLE FLASH PREFAB ---
        [Header("=== MUZZLE FLASH PREFAB ===")]
        [Tooltip("Prefab Muzzle Flash (bisa berisi banyak Particle System). Biarkan kosong jika ingin pakai ParticleSystem bawaan di weapon.")]
        public GameObject muzzleFlashPrefab;
        [Tooltip("Durasi sebelum prefab muzzle flash dihancurkan/dikembalikan ke pool")]
        public float muzzleFlashDuration = 1f;
        #endregion

        #region --- 9. PROCEDURAL ANIMATION (VISUAL RECOIL) ---
        [Header("=== PROCEDURAL ANIMATION ===")]
        public bool enableProceduralRecoil = true;
        #endregion

        #region --- 10. ROTARY BARREL (MINIGUN) ---
        [Header("=== ROTARY BARREL (MINIGUN) ===")]
        public bool isRotaryBarrel = false;
        [Tooltip("Waktu tahan klik sampai laras berputar maksimal dan mulai nembak")]
        public float spinUpTime = 1f;
        [Tooltip("Kecepatan putar maksimal laras (derajat per detik)")]
        public float maxSpinSpeed = 1000f;
        #endregion

        #region --- 11. RELOAD EFFECTS ---
        [Header("=== RELOAD EFFECTS ===")]
        [Tooltip("Prefab magazine fisik (dengan Rigidbody) yang akan jatuh saat reload")]
        public GameObject magazineDropPrefab;
        [Tooltip("Delay sebelum magazine jatuh saat reload (detik). Sesuaikan dengan timing animasi.")]
        public float magazineDropDelay = 0.3f;
        [Tooltip("Waktu despawn magazine yang jatuh (detik)")]
        public float magazineDespawnTime = 3f;
        [Tooltip("Gaya lemparan magazine saat dijatuhkan (relatif terhadap arah Drop Point)")]
        public Vector3 magazineDropForce = new Vector3(0f, -1.5f, 0f);
        [Tooltip("Kekuatan acak putaran (torque) magazine saat jatuh")]
        public float magazineDropTorque = 2f;
        #endregion

        #region --- 12. EXPLOSIVE AMMO (HE/HEAT/APHE) ---
        [Header("=== EXPLOSIVE AMMO (HE/HEAT/APHE) ===")]
        [Tooltip("Aktifkan jika amunisi ini meledak saat benturan, bukan menembus target.")]
        public bool isExplosive = false;
        [Tooltip("Radius ledakan dalam meter (world units).")]
        public float explosiveRadius = 5f;
        [Tooltip("Damage maksimal di pusat ledakan (semakin jauh dikurangi distance falloff).")]
        public float explosiveDamage = 200f;
        [Tooltip("Kekuatan dorong (explosion force) ke Rigidbody di sekitar.")]
        public float explosiveForce = 500f;
        [Tooltip("Prefab VFX ledakan (WarFX Explosion atau kustom).")]
        public GameObject explosionVFXPrefab;
        [Tooltip("Clip suara ledakan.")]
        public AudioClip explosionSFX;
        #endregion
    }
}