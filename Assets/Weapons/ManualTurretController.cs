using UnityEngine;

namespace Weapons
{
    public class ManualTurretController : MonoBehaviour
    {
        [Header("=== REFERENSI (BEBAS STRES SUMBU BLENDER) ===")]
        public Camera playerCamera;
        [Tooltip("Pivot putaran kiri-kanan (Base Yaw)")]
        public Transform turretBase;
        [Tooltip("Pivot putaran atas-bawah (Gun Body Pitch)")]
        public Transform gunBarrel;
        [Tooltip("Wajib: Objek empty 'muz' di ujung laras! Pastikan panah BIRU (Z) lurus ke depan laras!")]
        public Transform aimOrigin;

        [Header("=== SETTING AIMING ===")]
        public float aimingSpeed = 120f;
        public float maxAimDistance = 1000f;
        public LayerMask aimMask = ~0;

        [Header("=== BATASAN PITCH ===")]
        public float minPitch = -15f;
        public float maxPitch = 45f;

        void Start()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            if (aimOrigin == null) Debug.LogError("Tolong assign objek 'muz' ke aimOrigin!");
        }

        void LateUpdate()
        {
            if (turretBase == null || gunBarrel == null || aimOrigin == null || playerCamera == null) return;

            Vector3 targetPt = GetCrosshairTarget();

            // 1. Arahkan Yaw (Kiri/Kanan)
            AimTurretYaw(targetPt);

            // 2. Arahkan Pitch (Atas/Bawah)
            AimBarrelPitch(targetPt);
        }

        private Vector3 GetCrosshairTarget()
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask))
                return hit.point;
            return ray.GetPoint(maxAimDistance);
        }

        private void AimTurretYaw(Vector3 targetPoint)
        {
            // Sumbu atas absolut (mengabaikan sumbu acak-acakan dari Blender)
            Vector3 upAxis = Vector3.up; 

            // Cari arah horizontal dari laras (muz) saat ini
            Vector3 currentMuzFlat = Vector3.ProjectOnPlane(aimOrigin.forward, upAxis).normalized;
            // Cari arah horizontal ke target
            Vector3 dirToTarget = targetPoint - turretBase.position;
            Vector3 targetFlat = Vector3.ProjectOnPlane(dirToTarget, upAxis).normalized;

            if (currentMuzFlat.sqrMagnitude > 0.001f && targetFlat.sqrMagnitude > 0.001f)
            {
                // Hitung berapa derajat harus muter
                float yawError = Vector3.SignedAngle(currentMuzFlat, targetFlat, upAxis);
                
                // Bikin rotasi baru berdasarkan sumbu UP murni
                Quaternion targetYawRot = Quaternion.AngleAxis(yawError, upAxis) * turretBase.rotation;
                
                // Terapkan rotasi secara halus
                turretBase.rotation = Quaternion.RotateTowards(turretBase.rotation, targetYawRot, aimingSpeed * Time.deltaTime);
            }
        }

        private void AimBarrelPitch(Vector3 targetPoint)
        {
            Vector3 upAxis = Vector3.up;
            
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

                // Hitung pitch aktual saat ini (0 derajat = sejajar tanah / horizontal)
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
            if (playerCamera == null || aimOrigin == null) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 targetPt;
            if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask))
            {
                targetPt = hit.point;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(playerCamera.transform.position, targetPt);
                if (Application.isPlaying) Gizmos.DrawSphere(targetPt, 0.2f);
            }
            else
            {
                targetPt = ray.GetPoint(maxAimDistance);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(playerCamera.transform.position, targetPt);
            }

            Gizmos.color = Color.blue;
            float dist = Vector3.Distance(aimOrigin.position, targetPt);
            // Garis biru 100% dari arah muz
            Gizmos.DrawLine(aimOrigin.position, aimOrigin.position + (aimOrigin.forward * dist));
        }
    }
}