using System.Collections;
using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// God Class untuk senjata konvensional modular.
    /// Mencakup semua fungsionalitas: Single Shot, Shotgun Pellet, Minigun Rotary, Overheat, dan Procedural Recoil.
    /// </summary>
    public class ModularWeapon : MonoBehaviour
    {
        #region --- 1. CORE SETTINGS ---
        [Header("=== CORE SETTINGS ===")]
        [Tooltip("Kecepatan peluru saat keluar dari laras (m/s)")]
        public float muzzleVelocity = 800f;
        [Tooltip("Tembakan per menit (RPM)")]
        public float fireRateRPM = 600f;
        
        [Tooltip("Batas amunisi dalam satu magazine (0 = Infinite)")]
        public int maxAmmo = 30;
        public int currentAmmo;
        [Tooltip("Waktu yang dibutuhkan untuk reload (detik)")]
        public float reloadTime = 2f;
        private bool _isReloading;
        private float _fireCooldown;
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
        
        [SerializeField] private float _currentHeat = 0f;
        #endregion

        #region --- 5. RECOIL (VEHICLE PHYSICS) ---
        [Header("=== RECOIL (VEHICLE PHYSICS) ===")]
        [Tooltip("Gaya dorong mundur yang diaplikasikan ke kendaraan saat menembak")]
        public float recoilForce = 500f;
        private Rigidbody _vehicleRb;
        #endregion

        #region --- 6. AUDIO & TRANSFORMS ---
        [Header("=== AUDIO & TRANSFORMS ===")]
        [Tooltip("Titik keluarnya peluru")]
        public Transform muzzleTransform;
        [Tooltip("Titik lontaran selongsong peluru (Opsional)")]
        public Transform ejectionPortTransform;
        [Tooltip("Prefab selongsong peluru yang terlempar (Opsional)")]
        public GameObject casingPrefab;

        [Space]
        public AudioSource weaponAudioSource;
        public AudioClip shootSound;
        public AudioClip reloadSound;
        public AudioClip emptyClickSound;
        
        [Tooltip("Particle system efek kilatan laras")]
        public ParticleSystem muzzleFlash;

        [Header("=== MUZZLE FLASH PREFAB ===")]
        [Tooltip("Prefab muzzle flash (bisa 4-plane, FPS style, atau sprite). Drag langsung dari Project folder.")]
        public GameObject muzzleFlashPrefab;
        [Tooltip("Berapa detik flash hidup sebelum dihancurkan")]
        public float muzzleFlashDuration = 0.06f;
        [Tooltip("Ukuran acak minimum")]
        public float muzzleFlashScaleMin = 0.8f;
        [Tooltip("Ukuran acak maksimum")]
        public float muzzleFlashScaleMax = 1.3f;
        [Tooltip("Koreksi rotasi flash (tweak ini kalau flash kebalik atau miring). Ini offset dari rotasi muzzle.")]
        public Vector3 muzzleFlashRotOffset = Vector3.zero;
        [Tooltip("Rotasi acak di sumbu X (0=mati, 1=aktif)")]
        [Range(0f,1f)] public float muzzleFlashRotX = 0f;
        [Tooltip("Rotasi acak di sumbu Y (0=mati, 1=aktif)")]
        [Range(0f,1f)] public float muzzleFlashRotY = 0f;
        [Tooltip("Rotasi acak di sumbu Z (0=mati, 1=aktif)")]
        [Range(0f,1f)] public float muzzleFlashRotZ = 1f;
        #endregion

        #region --- 7. PROCEDURAL ANIMATION (VISUAL RECOIL) ---
        [Header("=== PROCEDURAL ANIMATION ===")]
        public bool enableProceduralRecoil = true;
        [Tooltip("Bagian senjata yang mundur saat ditembak (contoh: kokangan / laras atas)")]
        public Transform movableMesh;
        [Tooltip("Seberapa jauh mundur ke belakang (Z axis lokal)")]
        public float recoilDistance = 0.15f;
        [Tooltip("Kecepatan hentakan mundur")]
        public float recoilSnapSpeed = 50f;
        [Tooltip("Kecepatan kembali ke posisi semula")]
        public float recoilReturnSpeed = 10f;
        
        private Vector3 _meshOriginalLocalPos;
        private Vector3 _meshTargetLocalPos;
        #endregion

        #region --- 8. ROTARY BARREL (MINIGUN) ---
        [Header("=== ROTARY BARREL (MINIGUN) ===")]
        public bool isRotaryBarrel = false;
        [Tooltip("Laras yang berputar")]
        public Transform barrelMesh;
        [Tooltip("Waktu tahan klik sampai laras berputar maksimal dan mulai nembak")]
        public float spinUpTime = 1f;
        [Tooltip("Kecepatan putar maksimal laras (derajat per detik)")]
        public float maxSpinSpeed = 1000f;
        
        private float _currentSpinLerp = 0f;
        private bool _isHoldingTrigger = false;
        #endregion

        private void Awake()
        {
            currentAmmo = maxAmmo;
            if (movableMesh != null)
            {
                _meshOriginalLocalPos = movableMesh.localPosition;
                _meshTargetLocalPos = _meshOriginalLocalPos;
            }

            // Mencari Rigidbody kendaraan induk secara otomatis
            _vehicleRb = GetComponentInParent<Rigidbody>();
        }

        private void Update()
        {
            if (_fireCooldown > 0) _fireCooldown -= Time.deltaTime;

            HandleCooling();
            HandleProceduralRecoilAnimation();
            HandleRotaryBarrel();
            
            // Sprite Muzzle Flash dihandle sepenuhnya oleh SpriteMuzzleFlash.cs via Coroutine
            
            // DEMO INPUT: Hapus atau ganti ini jika sudah punya sistem input/turret sentral
            if (Input.GetMouseButton(0))
            {
                TryFire();
            }
            else
            {
                _isHoldingTrigger = false;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                StartReload();
            }
        }

        public void TryFire()
        {
            _isHoldingTrigger = true;

            if (_isReloading) return;
            
            if (maxAmmo > 0 && currentAmmo <= 0)
            {
                if (Input.GetMouseButtonDown(0)) PlaySound(emptyClickSound);
                return;
            }

            // Jika senjata butuh spin up (Minigun), tunggu sampai putaran penuh
            if (isRotaryBarrel && _currentSpinLerp < 0.95f) return;

            // Jika senjata kepanasan ekstrim (Jammed), bisa ditambahkan blokir nembak disini.
            // Sementara kita biarkan bisa nembak tapi akurasinya ampas total.

            if (_fireCooldown <= 0f)
            {
                Fire();
            }
        }

        private void Fire()
        {
            if (maxAmmo > 0) currentAmmo--;

            float timeBetweenShots = 60f / fireRateRPM;
            _fireCooldown = timeBetweenShots;

            // Tambah panas
            if (enableOverheat)
            {
                _currentHeat = Mathf.Min(_currentHeat + heatPerShot, maxHeat);
            }

            // Efek suara & flash
            PlaySound(shootSound);
            if (muzzleFlash != null) muzzleFlash.Play();
            
            // Spawn muzzle flash — Instantiate + Destroy, works for any prefab type
            if (muzzleFlashPrefab != null && muzzleTransform != null)
            {
                // Rotasi = rotasi muzzle + offset koreksi (buat benerin sumbu yang kebalik)
                Quaternion flashRot = muzzleTransform.rotation * Quaternion.Euler(muzzleFlashRotOffset);
                GameObject flash = Instantiate(
                    muzzleFlashPrefab,
                    muzzleTransform.position,
                    flashRot
                );
                // Parent ke muzzleTransform — flash ngikut laras ke mana pun bergerak
                flash.transform.SetParent(muzzleTransform, worldPositionStays: true);
                // Random scale
                float s = Random.Range(muzzleFlashScaleMin, muzzleFlashScaleMax);
                flash.transform.localScale = Vector3.one * s;
                // Random rotation
                float angle = Random.Range(0f, 360f);
                flash.transform.Rotate(
                    muzzleFlashRotX * angle,
                    muzzleFlashRotY * angle,
                    muzzleFlashRotZ * angle,
                    Space.Self
                );
                // Auto-destroy setelah durasi
                Destroy(flash, muzzleFlashDuration);
            }

            // Hitung multiplier dispersi dari kepanasan
            float heatFactor = enableOverheat ? (_currentHeat / maxHeat) : 0f;
            float currentDispersion = Mathf.Lerp(baseDispersion, baseDispersion * heatDispersionMultiplier, heatFactor);

            // Tembakkan peluru (Loop untuk Shotgun Pellet)
            for (int i = 0; i < pelletCount; i++)
            {
                SpawnProjectile(currentDispersion);
            }

            // Lontarkan selongsong peluru
            EjectCasing();

            // Pukul mundur kendaraan (Fisika nyata)
            ApplyVehicleRecoil();

            // Picu animasi hentakan senjata (Procedural)
            if (enableProceduralRecoil && movableMesh != null)
            {
                _meshTargetLocalPos = _meshOriginalLocalPos - new Vector3(0, 0, recoilDistance);
            }
        }

        private void SpawnProjectile(float dispersionAngle)
        {
            if (projectilePrefab == null || muzzleTransform == null) return;

            // Kalkulasi arah peluru dengan dispersi acak
            Vector3 randomDirection = muzzleTransform.forward;
            if (dispersionAngle > 0f)
            {
                randomDirection = Quaternion.Euler(
                    Random.Range(-dispersionAngle, dispersionAngle),
                    Random.Range(-dispersionAngle, dispersionAngle),
                    Random.Range(-dispersionAngle, dispersionAngle)
                ) * muzzleTransform.forward;
            }

            GameObject projObj = ObjectPool.Instance.Spawn(projectilePrefab, muzzleTransform.position, Quaternion.identity);
            if (projObj != null)
            {
                KinematicProjectile kp = projObj.GetComponent<KinematicProjectile>();
                if (kp != null)
                {
                    kp.Initialize(muzzleTransform.position, randomDirection, muzzleVelocity);
                }
            }
        }

        private void EjectCasing()
        {
            if (casingPrefab == null || ejectionPortTransform == null) return;

            GameObject casing = ObjectPool.Instance.Spawn(casingPrefab, ejectionPortTransform.position, ejectionPortTransform.rotation);
            Rigidbody casingRb = casing.GetComponent<Rigidbody>();
            if (casingRb != null)
            {
                casingRb.velocity = Vector3.zero;
                // Lempar selongsong ke kanan + sedikit ke atas dan belakang
                Vector3 ejectForce = ejectionPortTransform.right * Random.Range(3f, 5f) + 
                                     ejectionPortTransform.up * Random.Range(1f, 2f) -
                                     ejectionPortTransform.forward * Random.Range(0.5f, 1f);
                casingRb.AddForce(ejectForce, ForceMode.Impulse);
                casingRb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }
        }

        private void ApplyVehicleRecoil()
        {
            if (_vehicleRb != null && muzzleTransform != null)
            {
                // Force diberikan berlawanan dengan arah moncong tembak
                _vehicleRb.AddForceAtPosition(-muzzleTransform.forward * recoilForce, muzzleTransform.position, ForceMode.Impulse);
            }
        }

        private void HandleCooling()
        {
            if (!enableOverheat) return;

            // Jika tidak sedang menembak secara aktif, dinginkan
            if (!_isHoldingTrigger || (isRotaryBarrel && _currentSpinLerp < 0.95f))
            {
                _currentHeat = Mathf.Max(_currentHeat - (coolingRate * Time.deltaTime), 0f);
            }
        }

        private void HandleProceduralRecoilAnimation()
        {
            if (!enableProceduralRecoil || movableMesh == null) return;

            // Kembalikan target perlahan ke posisi semula
            _meshTargetLocalPos = Vector3.Lerp(_meshTargetLocalPos, _meshOriginalLocalPos, Time.deltaTime * recoilReturnSpeed);
            
            // Snap mesh ke posisi target
            movableMesh.localPosition = Vector3.Lerp(movableMesh.localPosition, _meshTargetLocalPos, Time.deltaTime * recoilSnapSpeed);
        }

        private void HandleRotaryBarrel()
        {
            if (!isRotaryBarrel || barrelMesh == null) return;

            // Hitung akselerasi putaran (0 sampai 1)
            float lerpTarget = _isHoldingTrigger ? 1f : 0f;
            float spinAccelRate = 1f / spinUpTime;
            
            _currentSpinLerp = Mathf.MoveTowards(_currentSpinLerp, lerpTarget, spinAccelRate * Time.deltaTime);

            // Putar laras (Z axis secara lokal)
            float currentSpinSpeed = _currentSpinLerp * maxSpinSpeed;
            barrelMesh.Rotate(Vector3.forward * (currentSpinSpeed * Time.deltaTime), Space.Self);
        }

        public void StartReload()
        {
            if (_isReloading || maxAmmo <= 0 || currentAmmo == maxAmmo) return;
            StartCoroutine(ReloadCoroutine());
        }

        private IEnumerator ReloadCoroutine()
        {
            _isReloading = true;
            PlaySound(reloadSound);
            
            yield return new WaitForSeconds(reloadTime);

            currentAmmo = maxAmmo;
            _isReloading = false;
        }

        private void PlaySound(AudioClip clip)
        {
            if (weaponAudioSource != null && clip != null)
            {
                weaponAudioSource.PlayOneShot(clip);
            }
        }
    }
}
