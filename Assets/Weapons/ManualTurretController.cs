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

        [Header("=== BATASAN PITCH ===")]
        public float minPitch = -15f;
        public float maxPitch = 45f;

        [Header("=== INPUT SETTINGS ===")]
        public bool usePlayerInput = true;
        
        [Tooltip("Tombol untuk Free Look (Kamera muter tapi turret diam)")]
        public KeyCode freeLookKey = KeyCode.C; // <-- INI HOTKEY-NYA

        private Vector3 _currentTargetPoint;
        private bool _isFreeLooking = false;

        public void SetAimTarget(Vector3 targetPoint)
        {
            _currentTargetPoint = targetPoint;
        }

        void Start()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            if (aimOrigin == null) Debug.LogError("Tolong assign objek 'muz' ke aimOrigin!");
        }

        // Gw pindahin cek input ke Update biar lebih responsif dibanding LateUpdate
        void Update() 
        {
            if (usePlayerInput && playerCamera != null)
            {
                // Ngecek apakah tombol 'C' lagi ditekan
                _isFreeLooking = Input.GetKey(freeLookKey);
            }
        }

        void LateUpdate()
        {
            if (turretBase == null || gunBarrel == null || aimOrigin == null) return;

            if (usePlayerInput && playerCamera != null)
            {
                // Kalo LAGI GAK NEKAN 'C', update titik aim ke arah crosshair kamera.
                // Tapi kalo LAGI NEKAN 'C', biarin aja, jangan di-update!
                if (!_isFreeLooking) 
                {
                    _currentTargetPoint = GetCrosshairTarget();
                }
            }

            // --- INI PERUBAHAN PENTING ---
            // Turret cuma boleh muter ngikutin titik aim JIKA GAK LAGI FREE LOOK!
            // (Jadi pas nekan C, laras meriamnya bener-bener diem membeku di tempat)
            if (!_isFreeLooking)
            {
                // 1. Arahkan Yaw (Kiri/Kanan)
                AimTurretYaw(_currentTargetPoint);

                // 2. Arahkan Pitch (Atas/Bawah)
                AimBarrelPitch(_currentTargetPoint);
            }
        }

        private RaycastHit[] _raycastHits = new RaycastHit[10];

        private Vector3 GetCrosshairTarget()
        {
            // (Sama persis kayak script lu sebelumnya)
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int hitCount = Physics.RaycastNonAlloc(ray, _raycastHits, maxAimDistance, aimMask);
            
            float closestDistance = float.MaxValue;
            Vector3 targetPoint = ray.GetPoint(maxAimDistance);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _raycastHits[i];
                if (hit.transform.root == transform.root) continue;

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
            // Sumbu atas kita ambil dari ROOT kendaraan (mobil utamanya).
            // Ini menjamin sumbu atas selalu sejajar atap mobil, TAPI tetap mengabaikan 
            // sumbu turret/barrel yang mungkin acak-acakan dari Blender.
            Vector3 upAxis = transform.root.up; 
            
            // Cari arah horizontal dari laras (muz) saat ini (relatif terhadap atap kendaraan)
            Vector3 currentMuzFlat = Vector3.ProjectOnPlane(aimOrigin.forward, upAxis).normalized;
            // Cari arah horizontal ke target (relatif terhadap atap kendaraan)
            Vector3 dirToTarget = targetPoint - turretBase.position;
            Vector3 targetFlat = Vector3.ProjectOnPlane(dirToTarget, upAxis).normalized;

            if (currentMuzFlat.sqrMagnitude > 0.001f && targetFlat.sqrMagnitude > 0.001f)
            {
                // Hitung berapa derajat harus muter
                float yawError = Vector3.SignedAngle(currentMuzFlat, targetFlat, upAxis);
                
                // Bikin rotasi baru berdasarkan sumbu UP kendaraan
                Quaternion targetYawRot = Quaternion.AngleAxis(yawError, upAxis) * turretBase.rotation;
                
                // Terapkan rotasi secara halus
                turretBase.rotation = Quaternion.RotateTowards(turretBase.rotation, targetYawRot, aimingSpeed * Time.deltaTime);
            }
        }

        private void AimBarrelPitch(Vector3 targetPoint)
        {
            // Sama, gunakan UP dari root mobil agar pitch tetap sejajar bodi mobil
            Vector3 upAxis = transform.root.up;
            
            // Sumbu engsel pitch (kiri-kanan) dibuat murni dari hasil silang (cross product) arah moncong & atas.
            // Ini membuat script SAMA SEKALI TIDAK PEDULI mau sumbu X, Y, Z larasnya kebalik atau ngacak.
            Vector3 currentMuzFlat = Vector3.ProjectOnPlane(aimOrigin.forward, upAxis).normalized;
            if (currentMuzFlat.sqrMagnitude < 0.001f) return; 

            Vector3 pitchHingeAxis = Vector3.Cross(upAxis, currentMuzFlat).normalized;

            // Cari arah pitch saat ini dan target pada bidang engsel
            Vector3 dirToTarget = targetPoint - aimOrigin.position;
            Vector3 currentAimFlat = Vector3.ProjectOnPlane(aimOrigin.forward, pitchHingeAxis).normalized;
            Vector3 targetAimFlat = Vector3.ProjectOnPlane(dirToTarget, pitchHingeAxis).normalized;

            if (currentAimFlat.sqrMagnitude > 0.001f && targetAimFlat.sqrMagnitude > 0.001f)
            {
                float pitchError = Vector3.SignedAngle(currentAimFlat, targetAimFlat, pitchHingeAxis);

                // Hitung pitch aktual saat ini (0 derajat = sejajar atap kendaraan / horizontal lokal)
                float currentPitch = Vector3.SignedAngle(currentMuzFlat, currentAimFlat, pitchHingeAxis);

                // Tambahkan error ke pitch saat ini untuk dapat target pitch
                float desiredPitch = currentPitch + pitchError;
                
                // Clamp target pitch biar laras nggak tembus body tank
                float clampedPitch = Mathf.Clamp(desiredPitch, minPitch, maxPitch);
                
                // Hitung sisa rotasi yang diizinkan setelah di-clamp
                float allowedError = clampedPitch - currentPitch;

                // Terapkan rotasi secara halus murni di sumbu engsel dunia
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
                
                RaycastHit[] hits = Physics.RaycastAll(ray, maxAimDistance, aimMask);
                float closestDistance = float.MaxValue;
                bool hitValid = false;

                foreach (var hit in hits)
                {
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
            // Garis biru 100% dari arah muz
            Gizmos.DrawLine(aimOrigin.position, aimOrigin.position + (aimOrigin.forward * dist));
        }
    }
}