using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Mengontrol rotasi turret secara horizontal dan vertikal agar mengikuti kursor mouse (Gaya War Thunder).
    /// </summary>
    public class ManualTurretController : MonoBehaviour
    {
        [Header("=== TRANSFORMS ===")]
        [Tooltip("Transform yang hanya berputar Kanan/Kiri (Yaw)")]
        public Transform turretBase;
        [Tooltip("Transform yang hanya berputar Atas/Bawah (Pitch)")]
        public Transform gunBarrel;

        [Header("=== SPEED SETTINGS ===")]
        [Tooltip("Kecepatan putar Turret Base (Kanan-Kiri)")]
        public float turretRotateSpeed = 45f;
        [Tooltip("Kecepatan naik-turun laras (Atas-Bawah)")]
        public float barrelElevateSpeed = 30f;

        [Header("=== ELEVATION LIMITS ===")]
        [Tooltip("Sudut terendah senjata bisa menunduk (biasanya negatif, misal -10)")]
        public float minElevation = -10f;
        [Tooltip("Sudut tertinggi senjata bisa mendongak (misal 20)")]
        public float maxElevation = 20f;

        [Header("=== RAYCASTING ===")]
        [Tooltip("Kamera utama (Jika kosong, akan auto-find Camera.main)")]
        public Camera mainCamera;
        [Tooltip("Layer tanah/objek yang bisa ditunjuk oleh kursor")]
        public LayerMask aimMask;
        [Tooltip("Jarak maksimal raycast bidikan")]
        public float maxAimDistance = 1000f;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Update()
        {
            AimAtMouse();
        }

        private void AimAtMouse()
        {
            if (mainCamera == null || turretBase == null || gunBarrel == null) return;

            // 1. Dapatkan titik di dunia nyata berdasarkan posisi kursor mouse
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Vector3 targetPoint;

            if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask))
            {
                targetPoint = hit.point;
            }
            else
            {
                // Jika kursor menghadap langit/kosong, asumsikan jarak sangat jauh
                targetPoint = ray.GetPoint(maxAimDistance);
            }

            // 2. Putar Turret Base (Hanya Yaw / Sumbu Y lokal)
            Vector3 directionToBase = (targetPoint - turretBase.position).normalized;
            // Buat arahnya rata dengan turret base agar tidak ikut mendongak
            Vector3 projectedDirBase = Vector3.ProjectOnPlane(directionToBase, turretBase.up).normalized;
            
            if (projectedDirBase != Vector3.zero)
            {
                Quaternion targetBaseRot = Quaternion.LookRotation(projectedDirBase, turretBase.up);
                turretBase.rotation = Quaternion.RotateTowards(turretBase.rotation, targetBaseRot, turretRotateSpeed * Time.deltaTime);
            }

            // 3. Putar Gun Barrel (Hanya Pitch / Sumbu X lokal relatif terhadap Turret Base)
            Vector3 directionToBarrel = (targetPoint - gunBarrel.position).normalized;
            
            // Konversi arah global ke arah lokal turret base
            Vector3 localTargetDir = turretBase.InverseTransformDirection(directionToBarrel);
            
            // Hitung sudut elevasi yang dibutuhkan (Atan2 Y terhadap Z)
            float targetElevation = -Mathf.Atan2(localTargetDir.y, localTargetDir.z) * Mathf.Rad2Deg;
            
            // Batasi elevasi laras (Clamp)
            targetElevation = Mathf.Clamp(targetElevation, minElevation, maxElevation);

            // Terapkan rotasi halus (Lerp/RotateTowards)
            Quaternion currentLocalRot = gunBarrel.localRotation;
            Quaternion targetLocalRot = Quaternion.Euler(targetElevation, 0f, 0f); // Barrel hanya muter X
            
            gunBarrel.localRotation = Quaternion.RotateTowards(currentLocalRot, targetLocalRot, barrelElevateSpeed * Time.deltaTime);
        }
    }
}
