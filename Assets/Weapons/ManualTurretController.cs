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

        private static readonly RaycastHit[] _turretRaycastBuffer = new RaycastHit[32];

        void Start()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            if (aimOrigin == null) Debug.LogError("Tolong assign objek 'muz' ke aimOrigin!");

            // Default pake batas atap/datar
            _activeMinPitch = minPitch;
            _activeMaxPitch = maxPitch;

            if (autoDetectSideMount)
            {
                // Cari Rigidbody di parent (pasti bodi utama mobil/tank)
                Rigidbody vehicleRB = GetComponentInParent<Rigidbody>();
                Transform vehicleTransform = (vehicleRB != null) ? vehicleRB.transform : transform.root;
                
                // Konversi arah atas Oerlikon (-transform.forward) ke koordinat lokal bodi mobil
                Vector3 localTurretUp = vehicleTransform.InverseTransformDirection(-transform.forward);

                // JIKA NILAI Y LOKAL DEKAT 0 = Senjata Samping (arah atasnya madep kiri/kanan mobil)
                // JIKA NILAI Y LOKAL DEKAT 1 = Senjata Atap (arah atasnya searah atap mobil)
                if (Mathf.Abs(localTurretUp.y) < 0.5f)
                {
                    _isSideMounted = true;
                    _activeMinPitch = minPitchSide; // Pake -3 derajat untuk samping
                    _activeMaxPitch = maxPitchSide;
                    Debug.Log($"{gameObject.name} terdeteksi di SAMPING mobil. Limit samping aktif: {_activeMinPitch} s/d {_activeMaxPitch}");
                }
                else
                {
                    _isSideMounted = false;
                    _activeMinPitch = minPitch; // Pake -15 derajat untuk atap (Depresi NATO!)
                    _activeMaxPitch = maxPitch;
                    Debug.Log($"{gameObject.name} terdeteksi di ATAP mobil. Limit atap aktif: {_activeMinPitch} s/d {_activeMaxPitch}");
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

            if (usePlayerInput && playerCamera != null && !_isFreeLooking)
            {
                _currentTargetPoint = GetCrosshairTarget();
            }

            if (!_isFreeLooking)
            {
                float desiredPitch = CalculateOriginalDesiredPitch(_currentTargetPoint);

                // Blind spot melipat hanya berlaku untuk senjata samping yang mencoba menembus bodi mobil (pitch < minPitchSide)
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
                Quaternion targetYawRot = Quaternion.AngleAxis(yawError, upAxis) * turretBase.rotation;
                turretBase.rotation = Quaternion.RotateTowards(turretBase.rotation, targetYawRot, aimingSpeed * Time.deltaTime);
            }
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

        void OnDrawGizmos()
        {
            if (aimOrigin == null) return;
            Vector3 targetPt = _currentTargetPoint;

            if (usePlayerInput && playerCamera != null)
            {
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                targetPt = ray.GetPoint(maxAimDistance);
                
                int hitCount = Physics.RaycastNonAlloc(ray, _turretRaycastBuffer, maxAimDistance, aimMask);
                float closestDistance = float.MaxValue;
                bool hitValid = false;

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

                if (hitValid)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(playerCamera.transform.position, targetPt);
                    if (Application.isPlaying) Gizmos.DrawSphere(targetPt, 0.2f);
                }
                else
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(playerCamera.transform.position, targetPt);
                }
            }
            else
            {
                Gizmos.color = Color.yellow;
                if (Application.isPlaying) Gizmos.DrawSphere(targetPt, 0.5f);
            }

            Gizmos.color = Color.blue;
            float dist = Vector3.Distance(aimOrigin.position, targetPt);
            Gizmos.DrawLine(aimOrigin.position, aimOrigin.position + (aimOrigin.forward * dist));
        }
    }
}