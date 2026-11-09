using UnityEngine;

namespace Weapons
{
    public class ManualTurretController : MonoBehaviour
    {
        [Header("=== REFERENSI (BEBAS STRES SUMBU BLENDER) ===")]
        public Camera playerCamera;
        public Transform turretBase;
        public Transform gunBarrel;
        public Transform aimOrigin;

        [Header("=== SETTING AIMING ===")]
        public float aimingSpeed = 120f;
        public float maxAimDistance = 1000f;
        public LayerMask aimMask = ~0;

        [Header("=== DETEKSI OTOMATIS MOUNTING ===")]
        [Tooltip("Otomatis deteksi posisi pasang (atap vs samping) biar ga perlu bikin prefab berbeda")]
        public bool autoDetectSideMount = true;

        [Header("=== BATAS PITCH (ATAP / NATO STYLE) ===")]
        public float minPitch = -15f; // Depresi tangguh ala tank barat
        public float maxPitch = 45f;

        [Header("=== BATAS PITCH (SAMPING / RUSSIAN STYLE) ===")]
        [Tooltip("Nilai negatif kecil (misal -3 atau 0) biar laras gak nyodok masuk ke bodi samping mobil")]
        public float minPitchSide = -3f; 
        [Tooltip("Nilai positif (misal 45) biar laras bebas membidik ke arah luar")]
        public float maxPitchSide = 45f;

        [Header("=== BATAS YAW (OPSIONAL, DEFAULT FULL 360) ===")]
        [Tooltip("Centang kalau turret ini TIDAK boleh full 360. Default mati = yaw bebas penuh.")]
        public bool useYawLimit = false;
        [Tooltip("Yaw minimum relatif ke arah default pasang di garasi (negatif = ke kiri mount), rentang -180 s/d 180")]
        public float minYaw = -30f;
        [Tooltip("Yaw maximum relatif ke arah default pasang di garasi (positif = ke kanan mount), rentang -180 s/d 180")]
        public float maxYaw = 30f;

        [Header("=== FITUR ANTI-GEMETERAN (BLIND SPOT FOLD) ===")]
        [Tooltip("Jika target masuk ke bodi mobil/blind spot, senjata samping bakal melipat halus ke posisi lurus")]
        public bool foldInBlindSpot = true;

        [Header("=== INPUT SETTINGS ===")]
        public bool usePlayerInput = true;
        public KeyCode freeLookKey = KeyCode.C;

        private Vector3 _currentTargetPoint;
        private bool _isFreeLooking = false;

        private float _activeMinPitch;
        private float _activeMaxPitch;
        private bool _isSideMounted = false;
        private Transform _vehicleTransform;

        // Kuncian arah pasang dari garasi biar gak gila kiblat ke moncong mobil
        private Vector3 _localMountForward;

        private static readonly RaycastHit[] _turretRaycastBuffer = new RaycastHit[32];

        // Semua turret aktif mendaftar ke sini. WeaponAimIndicatorManager yang
        // menggambar indikator global untuk semuanya sekaligus.
        public static readonly System.Collections.Generic.List<ManualTurretController> ActiveTurrets
            = new System.Collections.Generic.List<ManualTurretController>();

        private void OnEnable()
        {
            if (!ActiveTurrets.Contains(this))
                ActiveTurrets.Add(this);
        }

        private void OnDisable()
        {
            ActiveTurrets.Remove(this);
        }

        void Start()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            if (aimOrigin == null) Debug.LogError("Tolong assign objek 'muz' ke aimOrigin!");

            // Default pake batas atap/datar
            _activeMinPitch = minPitch;
            _activeMaxPitch = maxPitch;

            // Cari Rigidbody di parent (pasti bodi utama mobil/tank)
            Rigidbody vehicleRB = GetComponentInParent<Rigidbody>();
            _vehicleTransform = (vehicleRB != null) ? vehicleRB.transform : transform.root;

            Vector3 upAxis = -transform.forward;

            // CATAT ARAH PASANG DARI GARASI
            // Kita rekam arah laras awal yang udah diproyeksikan ke upAxis, lalu simpen dalam koordinat lokal mobil
            if (aimOrigin != null)
            {
                Vector3 initialAimFlat = Vector3.ProjectOnPlane(aimOrigin.forward, upAxis).normalized;
                if (initialAimFlat.sqrMagnitude > 0.001f)
                {
                    _localMountForward = _vehicleTransform.InverseTransformDirection(initialAimFlat);
                }
                else
                {
                    _localMountForward = _vehicleTransform.InverseTransformDirection(transform.up);
                }
            }

            if (autoDetectSideMount)
            {
                Vector3 localTurretUp = _vehicleTransform.InverseTransformDirection(-transform.forward);

                if (Mathf.Abs(localTurretUp.y) < 0.5f)
                {
                    _isSideMounted = true;
                    _activeMinPitch = minPitchSide;
                    _activeMaxPitch = maxPitchSide;
                    #if UNITY_EDITOR
                    Debug.Log($"{gameObject.name} terdeteksi di SAMPING mobil. Limit samping: {_activeMinPitch} s/d {_activeMaxPitch}");
                    #endif
                }
                else
                {
                    _isSideMounted = false;
                    _activeMinPitch = minPitch;
                    _activeMaxPitch = maxPitch;
                    #if UNITY_EDITOR
                    Debug.Log($"{gameObject.name} terdeteksi di ATAP mobil. Limit atap: {_activeMinPitch} s/d {_activeMaxPitch}");
                    #endif
                }
            }
        }

        void Update() 
        {
            if (usePlayerInput && playerCamera != null)
            {
                _isFreeLooking = Input.GetKey(freeLookKey);
            }
        }

        void LateUpdate()
        {
            if (turretBase == null || gunBarrel == null || aimOrigin == null) return;

            // Turret non-player tetap bisa dibidik oleh AI/script lain.
            if (!usePlayerInput) return;

            if (playerCamera != null && !_isFreeLooking)
            {
                _currentTargetPoint = GetCrosshairTarget();
            }

            if (!_isFreeLooking)
            {
                float desiredPitch = CalculateOriginalDesiredPitch(_currentTargetPoint);

                bool targetInBlindSpot = _isSideMounted && (desiredPitch < _activeMinPitch);

                if (foldInBlindSpot && targetInBlindSpot)
                {
                    turretBase.localRotation = Quaternion.RotateTowards(turretBase.localRotation, Quaternion.identity, aimingSpeed * Time.deltaTime);
                    gunBarrel.localRotation = Quaternion.RotateTowards(gunBarrel.localRotation, Quaternion.identity, aimingSpeed * Time.deltaTime);
                }
                else
                {
                    AimTurretYaw(_currentTargetPoint);
                    AimBarrelPitch(_currentTargetPoint);
                }
            }
        }

        private float CalculateOriginalDesiredPitch(Vector3 targetPoint)
        {
            Vector3 upAxis = -transform.forward;
            Vector3 currentMuzFlat = Vector3.ProjectOnPlane(aimOrigin.forward, upAxis).normalized;
            if (currentMuzFlat.sqrMagnitude < 0.001f) return 0f;

            Vector3 pitchHingeAxis = Vector3.Cross(upAxis, currentMuzFlat).normalized;

            Vector3 dirToTarget = targetPoint - aimOrigin.position;
            Vector3 currentAimFlat = Vector3.ProjectOnPlane(aimOrigin.forward, pitchHingeAxis).normalized;
            Vector3 targetAimFlat = Vector3.ProjectOnPlane(dirToTarget, pitchHingeAxis).normalized;

            if (currentAimFlat.sqrMagnitude > 0.001f && targetAimFlat.sqrMagnitude > 0.001f)
            {
                float pitchError = Vector3.SignedAngle(currentAimFlat, targetAimFlat, pitchHingeAxis);
                float currentPitch = Vector3.SignedAngle(currentMuzFlat, currentAimFlat, pitchHingeAxis);
                return currentPitch + pitchError;
            }
            return 0f;
        }

        private Vector3 GetCrosshairTarget()
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int hitCount = Physics.RaycastNonAlloc(ray, _turretRaycastBuffer, maxAimDistance, aimMask);
            
            float closestDistance = float.MaxValue;
            Vector3 targetPoint = ray.GetPoint(maxAimDistance);

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _turretRaycastBuffer[i];
                if (hit.transform.root == transform.root) continue;
                if (hit.collider != null && hit.collider.isTrigger) continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    targetPoint = hit.point;
                }
            }
            return targetPoint;
        }

        private void AimTurretYaw(Vector3 targetPoint)
        {
            Vector3 upAxis = -transform.forward; 
            
            Vector3 currentMuzFlat = Vector3.ProjectOnPlane(aimOrigin.forward, upAxis).normalized;
            Vector3 dirToTarget = targetPoint - turretBase.position;
            Vector3 targetFlat = Vector3.ProjectOnPlane(dirToTarget, upAxis).normalized;

            if (currentMuzFlat.sqrMagnitude > 0.001f && targetFlat.sqrMagnitude > 0.001f)
            {
                float yawError = Vector3.SignedAngle(currentMuzFlat, targetFlat, upAxis);

                if (useYawLimit)
                {
                    yawError = ClampYawError(yawError, currentMuzFlat, upAxis);
                }

                Quaternion targetYawRot = Quaternion.AngleAxis(yawError, upAxis) * turretBase.rotation;
                turretBase.rotation = Quaternion.RotateTowards(turretBase.rotation, targetYawRot, aimingSpeed * Time.deltaTime);
            }
        }

        // Clamp yawError relatif terhadap ARAH PASANG DEFAULT DI GARASI
        private float ClampYawError(float yawError, Vector3 currentMuzFlat, Vector3 upAxis)
        {
            if (_vehicleTransform == null) return yawError;

            // Balikin arah mount lokal ke world space saat kendaraan jalan/manuver
            Vector3 worldMountFwd = _vehicleTransform.TransformDirection(_localMountForward);
            Vector3 mountFlatFwd = Vector3.ProjectOnPlane(worldMountFwd, upAxis).normalized;
            
            if (mountFlatFwd.sqrMagnitude < 0.001f) return yawError;

            // Hitung sudut sekarang relatif ke arah hadap pasang di garasi
            float currentYaw = Vector3.SignedAngle(mountFlatFwd, currentMuzFlat, upAxis);
            float desiredYaw = currentYaw + yawError;
            float clampedYaw = Mathf.Clamp(desiredYaw, minYaw, maxYaw);
            
            return clampedYaw - currentYaw;
        }

        private void AimBarrelPitch(Vector3 targetPoint)
        {
            Vector3 upAxis = -transform.forward;
            
            Vector3 currentMuzFlat = Vector3.ProjectOnPlane(aimOrigin.forward, upAxis).normalized;
            if (currentMuzFlat.sqrMagnitude < 0.001f) return; 

            Vector3 pitchHingeAxis = Vector3.Cross(upAxis, currentMuzFlat).normalized;

            Vector3 dirToTarget = targetPoint - aimOrigin.position;
            Vector3 currentAimFlat = Vector3.ProjectOnPlane(aimOrigin.forward, pitchHingeAxis).normalized;
            Vector3 targetAimFlat = Vector3.ProjectOnPlane(dirToTarget, pitchHingeAxis).normalized;

            if (currentAimFlat.sqrMagnitude > 0.001f && targetAimFlat.sqrMagnitude > 0.001f)
            {
                float pitchError = Vector3.SignedAngle(currentAimFlat, targetAimFlat, pitchHingeAxis);
                float currentPitch = Vector3.SignedAngle(currentMuzFlat, currentAimFlat, pitchHingeAxis);
                float desiredPitch = currentPitch + pitchError;
                
                float clampedPitch = Mathf.Clamp(desiredPitch, _activeMinPitch, _activeMaxPitch);
                float allowedError = clampedPitch - currentPitch;

                Quaternion targetPitchRot = Quaternion.AngleAxis(allowedError, pitchHingeAxis) * gunBarrel.rotation;
                gunBarrel.rotation = Quaternion.RotateTowards(gunBarrel.rotation, targetPitchRot, aimingSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Titik layar (piksel) yang dituju moncong senjata. Selalu mengembalikan
        /// true selama referensinya ada — termasuk saat titik bidik berada di
        /// belakang kamera. Dulu kasus itu yang bikin ring "hilang-hilangan".
        /// Yang menggambar ring adalah WeaponAimIndicatorManager (global).
        /// </summary>
        public bool TryGetMuzzleAimScreenPoint(out Vector3 screenPoint)
        {
            screenPoint = Vector3.zero;

            if (aimOrigin == null) return false;
            if (playerCamera == null) playerCamera = Camera.main;
            if (playerCamera == null) return false;

            Vector3 worldPoint = GetMuzzleAimPoint();
            Vector3 viewport = playerCamera.WorldToViewportPoint(worldPoint);

            // Di belakang kamera, proyeksi x/y kebalik arah. Mirror biar ring
            // tetap menunjuk sisi yang benar, bukan malah disembunyikan.
            if (viewport.z < 0f)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            screenPoint = new Vector3(
                viewport.x * Screen.width,
                viewport.y * Screen.height,
                viewport.z);
            return true;
        }

        /// <summary>
        /// Titik dunia yang dituju moncong: raycast dari aimOrigin searah forward.
        /// Kalau tidak kena apa-apa, pakai titik jauh di ujung maxAimDistance.
        /// </summary>
        private Vector3 GetMuzzleAimPoint()
        {
            Vector3 origin = aimOrigin.position;
            Vector3 direction = aimOrigin.forward;
            Vector3 point = origin + direction * maxAimDistance;

            int hitCount = Physics.RaycastNonAlloc(
                origin, direction, _turretRaycastBuffer,
                maxAimDistance, aimMask, QueryTriggerInteraction.Ignore);

            float closestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _turretRaycastBuffer[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.transform.root == transform.root) continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    point = hit.point;
                }
            }

            return point;
        }

        void OnDrawGizmos()
        {
            if (aimOrigin == null) return;

            Vector3 targetPt = _currentTargetPoint;
            bool hasTarget = false;
            bool hitValid = false;

            if (usePlayerInput && playerCamera != null)
            {
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                targetPt = ray.GetPoint(maxAimDistance);
                hasTarget = true;

                int hitCount = Physics.RaycastNonAlloc(ray, _turretRaycastBuffer, maxAimDistance, aimMask);
                float closestDistance = float.MaxValue;

                for (int i = 0; i < hitCount; i++)
                {
                    var hit = _turretRaycastBuffer[i];
                    if (hit.transform.root == transform.root) continue;

                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        targetPt = hit.point;
                        hitValid = true;
                    }
                }
            }
            else if (targetPt == Vector3.zero)
            {
                targetPt = aimOrigin.position + aimOrigin.forward * 20f;
            }

            // Gizmo editor: bulat saja, tanpa garis sambung.
            Gizmos.color = hasTarget ? (hitValid ? Color.green : Color.red) : Color.yellow;
            Gizmos.DrawWireSphere(targetPt, Application.isPlaying ? 0.25f : 0.4f);

            Gizmos.color = Color.blue;
            float dist = Mathf.Max(1f, Vector3.Distance(aimOrigin.position, targetPt));
            Gizmos.DrawWireSphere(aimOrigin.position + aimOrigin.forward * dist, 0.2f);
        }
    }
}