using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Weapons
{
    /// <summary>
    /// Bagian senjata yang bergerak mundur saat menembak (bolt, kokangan, cover, dll).
    /// Masing-masing punya arah dan jarak sendiri.
    /// </summary>
    [Serializable]
    public class RecoilPart
    {
        [Tooltip("Transform bagian yang bergerak")]
        public Transform mesh;
        [Tooltip("Sumbu arah mundurnya (0,0,1 = mundur di -Z local)")]
        public Vector3 recoilAxis = new Vector3(0, 0, 1);
        [Tooltip("Seberapa jauh mundur ke belakang")]
        public float recoilDistance = 0.15f;
        [Tooltip("Kecepatan hentakan mundur")]
        public float snapSpeed = 50f;
        [Tooltip("Kecepatan kembali ke posisi semula")]
        public float returnSpeed = 10f;
        [Tooltip("Centang ini jika bagian ini digerakkan juga oleh Animator (misal: Reload)")]
        public bool isAnimated = false;

        // Runtime state (otomatis, tidak perlu diisi)
        [HideInInspector] public Vector3 originalLocalPos;
        [HideInInspector] public Vector3 currentVelocity;
        [HideInInspector] public Vector3 currentOffset;
        [HideInInspector] public Vector3 savedBasePos;
        [HideInInspector] public bool hasSavedBasePos;
    }

    /// <summary>
    /// Bagian senjata yang berputar (laras minigun, dll).
    /// Masing-masing punya sumbu putar sendiri.
    /// </summary>
    [Serializable]
    public class RotaryPart
    {
        [Tooltip("Transform bagian yang berputar")]
        public Transform mesh;
        [Tooltip("Sumbu putaran local (default: Forward/Z)")]
        public Vector3 rotationAxis = Vector3.forward;
    }

    /// <summary>
    /// Bagian senjata yang MEMUTAR per tembakan (silinder revolver, belt feed, dll).
    /// Berbeda dari RotaryPart yang muter terus-menerus (minigun).
    /// </summary>
    [Serializable]
    public class RotatablePart
    {
        [Tooltip("Transform bagian yang berputar per tembakan")]
        public Transform mesh;
        [Tooltip("Sumbu putaran local (default: Right/X untuk silinder revolver)")]
        public Vector3 rotationAxis = Vector3.right;
        [Tooltip("Sudut rotasi per tembakan (derajat). Contoh: 60 untuk revolver 6 peluru")]
        public float anglePerShot = 60f;
        [Tooltip("Kecepatan snap ke sudut baru")]
        public float snapSpeed = 20f;
        [Tooltip("Delay sebelum bagian ini mulai berputar setelah tembakan (dalam detik)")]
        public float rotationDelay = 0f;
        [Tooltip("Centang ini jika bagian ini digerakkan juga oleh Animator (misal: Reload)")]
        public bool isAnimated = false;

        // Runtime state
        [HideInInspector] public Quaternion originalLocalRot;
        [HideInInspector] public float currentAngle;
        [HideInInspector] public float targetAngle;
        [HideInInspector] public Quaternion savedBaseRot;
        [HideInInspector] public bool hasSavedBaseRot;
        [HideInInspector] public Queue<float> pendingRotationTimes;
    }

    /// <summary>
    /// God Class untuk senjata konvensional modular.
    /// Semua VALUE/konfigurasi dibaca dari WeaponData (ScriptableObject).
    /// Script ini hanya berisi LOGIC + referensi Transform/AudioSource per-instance.
    /// </summary>
    public class ModularWeapon : MonoBehaviour
    {
        #region --- WEAPON DATA SLOT ---
        [Header("=== WEAPON DATA (DRAG SCRIPTABLEOBJECT DISINI) ===")]
        [Tooltip("ScriptableObject berisi semua konfigurasi senjata. Buat via: Right Click → Create → Weapons → Weapon Data")]
        public WeaponData weaponData;
        #endregion

        #region --- RUNTIME STATE (TIDAK DISIMPAN DI WEAPON DATA) ---
        [Header("=== RUNTIME STATE ===")]
        public int currentAmmo;
        private bool _isReloading;
        private float _reloadTimer;
        private float _pendingMagDrop = -1f;
        private Queue<float> _pendingCasings = new Queue<float>();
        private float _fireCooldown;
        [SerializeField] private float _currentHeat = 0f;
        private float _currentSpinLerp = 0f;
        private bool _isHoldingTrigger = false;
        #endregion

        #region --- INSTANCE REFERENCES (PER-PREFAB) ---
        [Header("=== TRANSFORMS (PER-PREFAB) ===")]
        [Tooltip("Titik keluarnya peluru")]
        public Transform muzzleTransform;
        [Tooltip("Titik untuk posisi Muzzle Flash (Opsional, jika kosong akan menggunakan muzzleTransform)")]
        public Transform muzzleFlashTransform;
        [Tooltip("Titik lontaran selongsong peluru (Opsional)")]
        public Transform ejectionPortTransform;
        [Tooltip("Titik awal jatuhnya magazine saat reload (Opsional)")]
        public Transform magazineDropPoint;

        [Header("=== RECOIL PARTS (BISA BANYAK) ===")]
        [Tooltip("Semua bagian senjata yang bergerak mundur saat menembak. Masing-masing punya arah & jarak sendiri.")]
        public RecoilPart[] recoilParts;

        [Header("=== ROTARY PARTS (BISA BANYAK) ===")]
        [Tooltip("Semua bagian senjata yang berputar TERUS-MENERUS (minigun barrel, dll). Masing-masing punya sumbu putar sendiri.")]
        public RotaryPart[] rotaryParts;

        [Header("=== ROTATABLE PARTS (PUTAR PER TEMBAKAN) ===")]
        [Tooltip("Semua bagian senjata yang MEMUTAR per tembakan (silinder revolver, belt feed, dll).")]
        public RotatablePart[] rotatableParts;

        [Header("=== RELOAD VISUALS ===")]
        [Tooltip("Object (contoh: Magazine) yang di-disable saat mulai reload, dan di-enable kembali setelah selesai.")]
        public GameObject[] hideDuringReload;

        [Header("=== AUDIO & VFX ===")]
        public AudioSource weaponAudioSource;
        [Tooltip("Particle system efek kilatan laras")]
        public ParticleSystem muzzleFlash;
        #endregion

        #region --- PRIVATE REFERENCES ---
        private Rigidbody _vehicleRb;

        // Event untuk memicu animasi dan UI
        public event Action OnReloadStart;
        public event Action OnReloadFinished;
        public event Action<int, int> OnAmmoChanged;
        #endregion

        private void Awake()
        {
            // Simpan posisi awal semua recoil part
            if (recoilParts != null)
            {
                foreach (var part in recoilParts)
                {
                    if (part.mesh != null)
                    {
                        part.originalLocalPos = part.mesh.localPosition;
                        part.currentVelocity = Vector3.zero;
                    }
                }
            }

            // Simpan rotasi awal semua rotatable part
            if (rotatableParts != null)
            {
                foreach (var part in rotatableParts)
                {
                    if (part.mesh != null)
                    {
                        part.originalLocalRot = part.mesh.localRotation;
                        part.currentAngle = 0f;
                        part.targetAngle = 0f;
                        part.pendingRotationTimes = new Queue<float>();
                    }
                }
            }

            // Mencari Rigidbody kendaraan induk secara otomatis
            _vehicleRb = GetComponentInParent<Rigidbody>();
        }

        private void Start()
        {
            if (weaponData == null)
            {
                Debug.LogError($"[ModularWeapon] WeaponData belum di-assign pada '{gameObject.name}'! Senjata tidak akan berfungsi.", this);
                enabled = false;
                return;
            }

            // Inisialisasi ammo awal setelah WeaponData pasti di-assign oleh script manager
            currentAmmo = weaponData.maxAmmo;
            OnAmmoChanged?.Invoke(currentAmmo, weaponData.maxAmmo);
        }

        private void OnDisable()
        {
            if (hideDuringReload != null)
            {
                foreach (var obj in hideDuringReload)
                {
                    if (obj != null) obj.SetActive(true);
                }
            }
            _isReloading = false;
            _isHoldingTrigger = false;
        }

        private void Update()
        {
            if (weaponData == null) return;

            // Kembalikan posisi/rotasi asli sebelum Animator mengevaluasi frame ini (untuk part yang di-animate)
            RestoreAnimatedParts();

            if (_fireCooldown > 0) _fireCooldown -= Time.deltaTime;

            HandleTimers();
            HandleCooling();
            HandleProceduralRecoilAnimation();
            HandleRotatableAnimation();
            HandleRotaryBarrel();
        }

        private void RestoreAnimatedParts()
        {
            if (weaponData.enableProceduralRecoil && recoilParts != null)
            {
                foreach (var part in recoilParts)
                {
                    if (part.mesh != null && part.isAnimated && part.hasSavedBasePos)
                    {
                        part.mesh.localPosition = part.savedBasePos;
                    }
                }
            }

            if (rotatableParts != null)
            {
                foreach (var part in rotatableParts)
                {
                    if (part.mesh != null && part.isAnimated && part.hasSavedBaseRot)
                    {
                        part.mesh.localRotation = part.savedBaseRot;
                    }
                }
            }
        }

        public void StopFiring()
        {
            _isHoldingTrigger = false;
        }

        public void TryFire()
        {
            _isHoldingTrigger = true;

            if (_isReloading) return;
            
            if (weaponData.maxAmmo > 0 && currentAmmo <= 0)
            {
                if (weaponData.autoReload)
                {
                    StartReload();
                }
                else
                {
                    if (Input.GetMouseButtonDown(0)) PlaySound(weaponData.emptyClickSound);
                }
                return;
            }

            // Jika senjata butuh spin up (Minigun), tunggu sampai putaran penuh
            if (weaponData.isRotaryBarrel && _currentSpinLerp < 0.95f) return;

            if (_fireCooldown <= 0f)
            {
                Fire();
            }
        }

        private void Fire()
        {
            if (weaponData.maxAmmo > 0)
            {
                currentAmmo--;
                OnAmmoChanged?.Invoke(currentAmmo, weaponData.maxAmmo);
            }

            if (weaponData.autoReload && weaponData.maxAmmo > 0 && currentAmmo <= 0)
            {
                StartReload();
            }

            float timeBetweenShots = 60f / weaponData.fireRateRPM;
            _fireCooldown = timeBetweenShots;

            // Tambah panas
            if (weaponData.enableOverheat)
            {
                _currentHeat = Mathf.Min(_currentHeat + weaponData.heatPerShot, weaponData.maxHeat);
            }

            // Efek suara & flash
            PlaySound(weaponData.shootSound, true);
            if (muzzleFlash != null) muzzleFlash.Play();

            // Spawn prefab muzzle flash tambahan jika ada
            Transform flashSpawnPoint = muzzleFlashTransform != null ? muzzleFlashTransform : muzzleTransform;
            if (weaponData.muzzleFlashPrefab != null && flashSpawnPoint != null)
            {
                GameObject flash = ObjectPool.Instance.Spawn(
                    weaponData.muzzleFlashPrefab,
                    flashSpawnPoint.position,
                    flashSpawnPoint.rotation
                );

                MuzzleFlashFX mfFX = flash.GetComponent<MuzzleFlashFX>();
                if (mfFX != null)
                {
                    mfFX.Play();
                }

                ObjectPool.Instance.Despawn(flash, weaponData.muzzleFlashDuration);
            }

            // Hitung multiplier dispersi dari kepanasan
            float heatFactor = weaponData.enableOverheat ? (_currentHeat / weaponData.maxHeat) : 0f;
            float currentDispersion = weaponData.baseDispersion;

            if (weaponData.enableOverheat)
            {
                float heatRatio = _currentHeat / weaponData.maxHeat; 
                float threshold = 0.8f; // 80% ambang batas

                if (heatRatio >= threshold)
                {
                    float normalizedFactor = (heatRatio - threshold) / (1f - threshold);
                    currentDispersion = Mathf.Lerp(weaponData.baseDispersion, weaponData.baseDispersion * weaponData.heatDispersionMultiplier, normalizedFactor);
                }
            }

            // Tembakkan peluru (Loop untuk Shotgun Pellet)
            for (int i = 0; i < weaponData.pelletCount; i++)
            {
                SpawnProjectile(currentDispersion);
            }

            // Lontarkan selongsong peluru dengan delay atau langsung
            if (weaponData.casingEjectDelay > 0f)
            {
                _pendingCasings.Enqueue(Time.time + weaponData.casingEjectDelay);
            }
            else
            {
                EjectCasing();
            }

            // Pukul mundur kendaraan (Fisika nyata)
            ApplyVehicleRecoil();

            // Efek Guncangan Kamera (Camera Shake) berdasarkan recoil
            if (VehicleCamera.Instance != null && weaponData.cameraShakeMultiplier > 0f)
            {
                VehicleCamera.Instance.Shake(weaponData.recoilForce * weaponData.cameraShakeMultiplier, weaponData.cameraShakeDuration);
            }

            // Picu animasi hentakan SEMUA recoil part
            if (weaponData.enableProceduralRecoil && recoilParts != null)
            {
                foreach (var part in recoilParts)
                {
                    if (part.mesh != null)
                    {
                        if (part.isAnimated)
                        {
                            // Tambahkan ke offset (bukan langsung ke posisi lokal)
                            part.currentOffset -= part.recoilAxis.normalized * part.recoilDistance;
                        }
                        else
                        {
                            // Snap mundur secara instan (menghentak)
                            part.mesh.localPosition -= part.recoilAxis.normalized * part.recoilDistance;
                        }
                    }
                }
            }

            // Picu rotasi SEMUA rotatable part (revolver cylinder, dll) dengan dukungan delay
            if (rotatableParts != null)
            {
                foreach (var part in rotatableParts)
                {
                    if (part.mesh != null)
                    {
                        if (part.rotationDelay > 0f)
                        {
                            part.pendingRotationTimes.Enqueue(Time.time + part.rotationDelay);
                        }
                        else
                        {
                            part.targetAngle += part.anglePerShot;
                        }
                    }
                }
            }
        }

        private void SpawnProjectile(float dispersionAngle)
        {
            if (weaponData.projectilePrefab == null || muzzleTransform == null) return;

            Vector3 finalDirection = muzzleTransform.forward;

            if (dispersionAngle > 0f)
            {
                float sigma = dispersionAngle * weaponData.chokeMultiplier;

                float u1 = Mathf.Max(Random.value, 0.0001f);
                float u2 = Random.value;
                float gaussMagnitude = Mathf.Sqrt(-2f * Mathf.Log(u1)) * sigma;
                float angle = u2 * 2f * Mathf.PI;

                float deviationX = gaussMagnitude * Mathf.Cos(angle);
                float deviationY = gaussMagnitude * Mathf.Sin(angle);

                Quaternion deviation = Quaternion.AngleAxis(deviationX, muzzleTransform.up) *
                                       Quaternion.AngleAxis(deviationY, muzzleTransform.right);

                finalDirection = deviation * muzzleTransform.forward;
            }

            GameObject projObj = ObjectPool.Instance.Spawn(weaponData.projectilePrefab, muzzleTransform.position, Quaternion.identity);
            if (projObj != null)
            {
                KinematicProjectile kp = projObj.GetComponent<KinematicProjectile>();
                if (kp != null)
                {
                    kp.Initialize(muzzleTransform.position, finalDirection, weaponData.muzzleVelocity);
                }
            }
        }

        private void EjectCasing()
        {
            if (weaponData.casingPrefab == null || ejectionPortTransform == null) return;

            GameObject casing = ObjectPool.Instance.Spawn(weaponData.casingPrefab, ejectionPortTransform.position, ejectionPortTransform.rotation);
            Rigidbody casingRb = casing.GetComponent<Rigidbody>();
            if (casingRb != null)
            {
                casingRb.velocity = Vector3.zero;
                
                float forceRight = weaponData.casingEjectForce.x + Random.Range(-weaponData.casingEjectRandomness.x, weaponData.casingEjectRandomness.x);
                float forceUp = weaponData.casingEjectForce.y + Random.Range(-weaponData.casingEjectRandomness.y, weaponData.casingEjectRandomness.y);
                float forceForward = weaponData.casingEjectForce.z + Random.Range(-weaponData.casingEjectRandomness.z, weaponData.casingEjectRandomness.z);

                Vector3 ejectForce = (ejectionPortTransform.right * forceRight) + 
                                     (ejectionPortTransform.up * forceUp) +
                                     (ejectionPortTransform.forward * forceForward);
                                     
                casingRb.AddForce(ejectForce, ForceMode.Impulse);
                casingRb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }

            ObjectPool.Instance.Despawn(casing, 3f);
        }

     private void ApplyVehicleRecoil()
    {
        if (_vehicleRb != null && muzzleTransform != null)
        {
            Vector3 recoilDir = -muzzleTransform.forward;
            float velocityRecoilForce = weaponData.recoilForce / _vehicleRb.mass;
            _vehicleRb.velocity += recoilDir * (velocityRecoilForce * 0.5f);
            _vehicleRb.AddForceAtPosition(recoilDir * (velocityRecoilForce * 0.5f), muzzleTransform.position, ForceMode.VelocityChange);
        }
    }

        private void HandleCooling()
        {
            if (!weaponData.enableOverheat) return;

            if (!_isHoldingTrigger || (weaponData.isRotaryBarrel && _currentSpinLerp < 0.95f))
            {
                _currentHeat = Mathf.Max(_currentHeat - (weaponData.coolingRate * Time.deltaTime), 0f);
            }
        }

        private void HandleProceduralRecoilAnimation()
        {
            if (!weaponData.enableProceduralRecoil || recoilParts == null) return;

            foreach (var part in recoilParts)
            {
                if (part.mesh == null) continue;

                float smoothTime = part.returnSpeed > 0f ? (1f / part.returnSpeed) : 0.1f;

                if (part.isAnimated)
                {
                    part.currentOffset = Vector3.SmoothDamp(
                        part.currentOffset, 
                        Vector3.zero, 
                        ref part.currentVelocity, 
                        smoothTime
                    );
                }
                else
                {
                    part.mesh.localPosition = Vector3.SmoothDamp(
                        part.mesh.localPosition, 
                        part.originalLocalPos, 
                        ref part.currentVelocity, 
                        smoothTime
                    );
                }
            }
        }

        private void LateUpdate()
        {
            if (weaponData == null) return;

            // Recoil Parts
            if (weaponData.enableProceduralRecoil && recoilParts != null)
            {
                foreach (var part in recoilParts)
                {
                    if (part.mesh != null && part.isAnimated)
                    {
                        part.savedBasePos = part.mesh.localPosition;
                        part.hasSavedBasePos = true;
                        part.mesh.localPosition += part.currentOffset;
                    }
                }
            }

            // Rotatable Parts (diaplikasikan di LateUpdate setelah Animator selesai mengevaluasi frame)
            if (rotatableParts != null)
            {
                foreach (var part in rotatableParts)
                {
                    if (part.mesh != null && part.isAnimated)
                    {
                        part.savedBaseRot = part.mesh.localRotation;
                        part.hasSavedBaseRot = true;
                        part.mesh.localRotation = part.savedBaseRot * Quaternion.AngleAxis(part.currentAngle, part.rotationAxis);
                    }
                }
            }
        }

        private void HandleRotatableAnimation()
        {
            if (rotatableParts == null) return;

            foreach (var part in rotatableParts)
            {
                if (part.mesh == null) continue;

                // Memproses antrean rotasi yang tertunda akibat delay
                while (part.pendingRotationTimes != null && part.pendingRotationTimes.Count > 0 && Time.time >= part.pendingRotationTimes.Peek())
                {
                    part.pendingRotationTimes.Dequeue();
                    part.targetAngle += part.anglePerShot;
                }

                // Lerp sudut saat ini menuju target
                part.currentAngle = Mathf.Lerp(part.currentAngle, part.targetAngle, Time.deltaTime * part.snapSpeed);

                // Jika tidak di-animate oleh Animator, terapkan rotasi secara langsung di Update
                if (!part.isAnimated)
                {
                    part.mesh.localRotation = part.originalLocalRot * Quaternion.AngleAxis(part.currentAngle, part.rotationAxis);
                }
            }
        }

        private void HandleRotaryBarrel()
        {
            if (!weaponData.isRotaryBarrel || rotaryParts == null || rotaryParts.Length == 0) return;

            float lerpTarget = _isHoldingTrigger ? 1f : 0f;
            float spinAccelRate = 1f / weaponData.spinUpTime;
            
            _currentSpinLerp = Mathf.MoveTowards(_currentSpinLerp, lerpTarget, spinAccelRate * Time.deltaTime);

            float currentSpinSpeed = _currentSpinLerp * weaponData.maxSpinSpeed;
            foreach (var part in rotaryParts)
            {
                if (part.mesh == null) continue;
                part.mesh.Rotate(part.rotationAxis * (currentSpinSpeed * Time.deltaTime), Space.Self);
            }
        }

        public void StartReload()
        {
            if (_isReloading || weaponData.maxAmmo <= 0 || currentAmmo == weaponData.maxAmmo) return;
            
            _isReloading = true;
            _reloadTimer = weaponData.reloadTime;
            PlaySound(weaponData.reloadSound);
            
            if (hideDuringReload != null)
            {
                foreach (var obj in hideDuringReload)
                {
                    if (obj != null) obj.SetActive(false);
                }
            }

            if (weaponData.magazineDropPrefab != null && magazineDropPoint != null)
            {
                _pendingMagDrop = Time.time + weaponData.magazineDropDelay;
            }

            OnReloadStart?.Invoke();
        }

        private void HandleTimers()
        {
            if (_isReloading)
            {
                _reloadTimer -= Time.deltaTime;
                if (_reloadTimer <= 0f)
                {
                    currentAmmo = weaponData.maxAmmo;
                    _isReloading = false;
                    OnAmmoChanged?.Invoke(currentAmmo, weaponData.maxAmmo);
                    OnReloadFinished?.Invoke();

                    if (hideDuringReload != null)
                    {
                        foreach (var obj in hideDuringReload)
                        {
                            if (obj != null) obj.SetActive(true);
                        }
                    }
                }
            }

            while (_pendingCasings.Count > 0 && Time.time >= _pendingCasings.Peek())
            {
                _pendingCasings.Dequeue();
                EjectCasing();
            }

            // Timer magazine drop
            if (_pendingMagDrop >= 0f && Time.time >= _pendingMagDrop)
            {
                _pendingMagDrop = -1f;
                SpawnDroppedMagazine();
            }
        }

        private void SpawnDroppedMagazine()
        {
            if (weaponData.magazineDropPrefab == null || magazineDropPoint == null) return;

            GameObject mag = ObjectPool.Instance.Spawn(weaponData.magazineDropPrefab, magazineDropPoint.position, magazineDropPoint.rotation);
            Rigidbody magRb = mag.GetComponent<Rigidbody>();
            if (magRb != null)
            {
                magRb.velocity = Vector3.zero;
                Vector3 localForce = magazineDropPoint.TransformDirection(weaponData.magazineDropForce);
                magRb.AddForce(localForce, ForceMode.Impulse);
                magRb.AddTorque(Random.insideUnitSphere * weaponData.magazineDropTorque, ForceMode.Impulse);
            }

            ObjectPool.Instance.Despawn(mag, weaponData.magazineDespawnTime);
        }

        private void PlaySound(AudioClip clip, bool randomize = false)
        {
            if (weaponAudioSource != null && clip != null)
            {
                if (randomize)
                {
                    // Acak dikit pitch & volume biar gak mendem pas ditiup/dispam
                    weaponAudioSource.pitch = Random.Range(0.85f, 0.98f); // Suara bakal lebih berat & ngebass
                    weaponAudioSource.volume = Random.Range(0.9f, 1.0f);
                }
                else
                {
                    // Kembalikan ke normal untuk suara lain (reload, dll)
                    weaponAudioSource.pitch = 1f;
                    weaponAudioSource.volume = 1f;
                }
                
                weaponAudioSource.PlayOneShot(clip);
            }
        }

        public bool IsReloading() => _isReloading;
        
        public float GetRemainingReloadTime()
        {
            if (!_isReloading) return 0f;
            return Mathf.Max(0f, _reloadTimer);
        }

        public bool IsOverheatEnabled() => weaponData != null && weaponData.enableOverheat;
        public float GetCurrentHeat() => _currentHeat;
        public float GetMaxHeat() => weaponData != null ? weaponData.maxHeat : 1f;
    }
}