using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Overhauled War Thunder Turret Controller.
    /// Standardized to Unity's Local Coordinate System (Z-Forward, Y-Up, X-Right).
    /// </summary>
    public class ManualTurretController : MonoBehaviour
    {
        [Header("=== TRANSFORMS (Use Pivot Objects!) ===")]
        [Tooltip("Object Pivot Turret (Yaw - Muter Kiri/Kanan)")]
        public Transform turretBase;
        [Tooltip("Object Pivot Laras (Pitch - Muter Atas/Bawah)")]
        public Transform gunBarrel;

        [Header("=== SPEED & WEIGHT (War Thunder Feel) ===")]
        [Tooltip("Kecepatan maksimal putaran turret (derajat/detik)")]
        public float turretYawSpeed = 40f;      
        [Tooltip("Kecepatan maksimal naik/turun laras (derajat/detik)")]
        public float gunPitchSpeed = 20f;       
        [Range(0.01f, 0.5f)]
        [Tooltip("Makin gede angkanya, makin kerasa 'berat berton-ton' turret lu")]
        public float turretWeight = 0.15f; 

        [Header("=== ELEVATION LIMITS ===")]
        public float minElevation = -10f;
        public float maxElevation = 25f;

        [Header("=== RAYCASTING ===")]
        public Camera mainCamera;
        public LayerMask aimMask;
        public float maxAimDistance = 1000f;

        // Internal Angles
        private float _currentYaw;
        private float _currentPitch;
        
        // Damp velocities untuk SmoothDamp
        private float _yawVelocity;
        private float _pitchVelocity;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Start()
        {
            // Sinkronisasi angle awal biar ga langsung 'njeklek' pas Play
            if (turretBase != null) _currentYaw = turretBase.localEulerAngles.y;
            if (gunBarrel != null) _currentPitch = FormatAngle(gunBarrel.localEulerAngles.x);
        }

        private void Update()
        {
            if (turretBase == null || gunBarrel == null) return;

            Vector3 targetWorldPosition = GetTargetPoint();
            AimAtTarget(targetWorldPosition);
        }

        private Vector3 GetTargetPoint()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask))
            {
                return hit.point;
            }
            return ray.GetPoint(maxAimDistance);
        }

        private void AimAtTarget(Vector3 targetPos)
        {
            // ─── YAW (TURRET BASE) ───
            // Dapatkan posisi target relatif terhadap HULL (parent turret)
            Transform hull = turretBase.parent != null ? turretBase.parent : transform;
            Vector3 targetLocalToHull = hull.InverseTransformPoint(targetPos);
            
            // Kita cuma peduli X dan Z untuk Yaw (horizontal)
            targetLocalToHull.y = 0; 
            
            if (targetLocalToHull.sqrMagnitude > 0.01f)
            {
                // Atan2 otomatis ngasih tau sudut ke target dalam local space
                float targetYaw = Mathf.Atan2(targetLocalToHull.x, targetLocalToHull.z) * Mathf.Rad2Deg;
                _currentYaw = Mathf.SmoothDampAngle(_currentYaw, targetYaw, ref _yawVelocity, turretWeight, turretYawSpeed);
            }

            // ─── PITCH (GUN BARREL) ───
            // Dapatkan posisi target relatif terhadap TURRET BASE (parent laras)
            Vector3 targetLocalToTurret = turretBase.InverseTransformPoint(targetPos);
            
            // Atan2 untuk pitch (Sumbu Y dan Z lokal)
            // Minus di depan karena di Unity, rotasi X positif itu nunduk kebawah
            float targetPitch = -Mathf.Atan2(targetLocalToTurret.y, targetLocalToTurret.z) * Mathf.Rad2Deg;
            targetPitch = Mathf.Clamp(targetPitch, minElevation, maxElevation);

            _currentPitch = Mathf.SmoothDampAngle(_currentPitch, targetPitch, ref _pitchVelocity, turretWeight, gunPitchSpeed);

            // ─── APPLY ROTATIONS ───
            // Sekarang sangat clean, cuma butuh local rotation standar
            turretBase.localRotation = Quaternion.Euler(0, _currentYaw, 0);
            gunBarrel.localRotation = Quaternion.Euler(_currentPitch, 0, 0);
        }

        // Helper buat ngebebasin angle dari format 0-360 ke -180 sampe 180
        private float FormatAngle(float angle)
        {
            if (angle > 180f) angle -= 360f;
            return angle;
        }
    }
}