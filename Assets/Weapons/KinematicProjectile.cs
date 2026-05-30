using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Kinematic Projectile - Sub-step raycast projectile logic.
    /// Sangat ringan, menggunakan Raycast alih-alih Rigidbody collision untuk mencegah projectile menembus dinding.
    /// </summary>
    public class KinematicProjectile : MonoBehaviour
    {
        [Header("Projectile Stats")]
        [Tooltip("Lifetime peluru dalam detik sebelum hancur otomatis")]
        public float maxLifetime = 5f;
        
        [Tooltip("Massa peluru (kg), berpengaruh pada push force ke target jika target punya Rigidbody")]
        public float mass = 0.05f;

        [Tooltip("Layer mask untuk mendeteksi apa yang bisa ditembak")]
        public LayerMask hitMask;

        [Header("Visuals (Opsional)")]
        [Tooltip("Efek partikel saat peluru mengenai sesuatu")]
        public GameObject hitImpactPrefab;

        // Internal State
        private Vector3 _currentVelocity;
        private Vector3 _currentPosition;
        private float _aliveTime;

        // Gravitasi bumi (-9.81), bisa diubah jika butuh balistik spesifik
        private readonly Vector3 _gravity = new Vector3(0f, -9.81f, 0f);

        /// <summary>
        /// Dipanggil oleh senjata saat menembakkan peluru ini.
        /// </summary>
        /// <param name="startPos">Posisi keluar laras</param>
        /// <param name="startDir">Arah tembakan</param>
        /// <param name="muzzleVelocity">Kecepatan awal (m/s)</param>
        public void Initialize(Vector3 startPos, Vector3 startDir, float muzzleVelocity)
        {
            _currentPosition = startPos;
            transform.position = startPos;
            transform.forward = startDir;

            _currentVelocity = startDir.normalized * muzzleVelocity;
            _aliveTime = 0f;

            // FIX: Bersihkan jejak TrailRenderer kalau peluru ini hasil daur ulang dari Object Pool
            // Biar gak ngebentuk "laser" dari posisi matinya peluru balik ke ujung laras
            TrailRenderer tr = GetComponent<TrailRenderer>();
            if (tr != null)
            {
                tr.Clear();
            }
        }

        private void FixedUpdate()
        {
            _aliveTime += Time.fixedDeltaTime;

            // 1. Cek umur peluru (lifetime)
            if (_aliveTime >= maxLifetime)
            {
                DestroyProjectile();
                return;
            }

            // 2. Terapkan gaya gravitasi ke kecepatan saat ini
            _currentVelocity += _gravity * Time.fixedDeltaTime;

            // 3. Prediksi posisi berikutnya
            Vector3 nextPosition = _currentPosition + (_currentVelocity * Time.fixedDeltaTime);
            Vector3 directionToNext = nextPosition - _currentPosition;
            float distanceToNext = directionToNext.magnitude;

            // 4. Lakukan Raycast dari posisi saat ini ke posisi berikutnya
            if (Physics.Raycast(_currentPosition, directionToNext.normalized, out RaycastHit hit, distanceToNext, hitMask))
            {
                // JIKA KENA SESUATU!
                HandleHit(hit);
                return; // Berhenti memproses posisi, karena peluru sudah hancur
            }

            // 5. Jika tidak menabrak, pindahkan peluru ke posisi baru
            _currentPosition = nextPosition;
            transform.position = _currentPosition;
            transform.forward = _currentVelocity.normalized; // Putar visual peluru menghadap arah jatuhnya
        }

        private void HandleHit(RaycastHit hit)
        {
            // Spawn efek ledakan/percikan api
            if (hitImpactPrefab != null)
            {
                ObjectPool.Instance.Spawn(hitImpactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            }

            // Tambahkan dorongan fisik (Push) jika target adalah Rigidbody
            Rigidbody hitRb = hit.collider.GetComponentInParent<Rigidbody>();
            if (hitRb != null)
            {
                // F = m * a. Kita simulasikan transfer momentum sederhana
                Vector3 force = _currentVelocity.normalized * (_currentVelocity.magnitude * mass);
                hitRb.AddForceAtPosition(force, hit.point, ForceMode.Impulse);
            }

            // TODO: Integrasi Modular Damage System (Sasis, Engine, Roda) disini nantinya
            // Contoh: var dmg = hit.collider.GetComponent<DamageablePart>();
            // if (dmg != null) dmg.TakeDamage(100f, hit.point);

            // Hancurkan peluru (Kembalikan ke pool)
            DestroyProjectile();
        }

        private void DestroyProjectile()
        {
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
