using UnityEngine;

namespace C4
{
    [DisallowMultipleComponent]
    public class C4Explosive : MonoBehaviour
    {
        [Header("=== EXPLOSION STATS ===")]
        [Tooltip("Jarak radius ledakan (meter).")]
        public float explosionRadius = 8.0f;

        [Tooltip("Damage maksimal di titik tengah bom.")]
        public float maxDamage = 500f;

        [Tooltip("Kekuatan dorong fisika ke Rigidbody (mobil/props).")]
        public float explosionForce = 1800f;

        [Tooltip("Dorongan ke atas biar mobil salto dramatis ala Hollywood.")]
        public float upwardsModifier = 0.7f;

        [Tooltip("Layer apa aja yang kena dampak ledakan.")]
        public LayerMask explosionMask = ~0;

        [Header("=== VFX & SFX ===")]
        [Tooltip("Prefab partikel api ledakan.")]
        public GameObject explosionVfxPrefab;

        [Tooltip("Suara DUARR!")]
        public AudioClip explosionSound;

        [Range(0f, 1f)]
        [Tooltip("Volume master suara ledakan (0.0 - 1.0).")]
        public float explosionVolume = 1.0f;

        [Header("=== AUDIO 3D & SHOCKWAVE ===")]
        [Tooltip("Jarak di mana ledakan masih terdengar 100% full volume tanpa mengecil (meter).")]
        public float explosionMinDistance = 25f;

        [Tooltip("Jarak maksimal suara ledakan masih bisa terdengar sebelum hilang total (meter).")]
        public float explosionMaxDistance = 300f;

        [Tooltip("Aktifkan lapisan suara 2D (shockwave / bass menggelegar) jika player berada di sekitar area ledakan.")]
        public bool enableProximityShockwave = true;

        [Tooltip("Jarak maksimal pemain merasakan hentakan suara shockwave (meter).")]
        public float shockwaveDistance = 45f;

        private Transform _planterTransform;
        private bool _hasDetonated = false;
        public Transform planter => _planterTransform;

        /// <summary>
        /// Dipanggil tepat sebelum C4 di-Destroy. Listener masih bisa baca posisi & radius.
        /// </summary>
        public static event System.Action<C4Explosive> OnDetonated;

        // Dipanggil otomatis dari C4Planter pas C4 ditempel
        public void Initialize(Transform planter)
        {
            _planterTransform = planter;
        }

        public void Detonate()
        {
            if (_hasDetonated) return;
            _hasDetonated = true;

            Vector3 blastPosition = transform.position;

            // 1. SPAWN VFX LEDAKAN (Lepas dari parent C4)
            if (explosionVfxPrefab != null)
            {
                GameObject vfx = Instantiate(explosionVfxPrefab, blastPosition, Quaternion.identity);
                // Auto destroy VFX setelah 6 detik biar ga nyampah di memory
                Destroy(vfx, 6.0f);
            }

            // 2. PLAY SFX SUARA DUARR YANG MENGGELEGAR
            PlayExplosionAudio(blastPosition);

            // 3. DAMAGE MODUL, RODA, CRITICAL PART & FISIKA
            // Pakai sistem ledakan terpusat yang sama dengan amunisi HE:
            // - modul lewat moduleColliderMap + propagasi grid
            // - roda / critical part / SimpleTarget lewat HitboxProxy
            // - armor mitigation + force ke Rigidbody
            ExplosionManager.Detonate(
                blastPosition,
                explosionRadius,
                maxDamage,
                explosionForce,
                null,               // auto-detect kendaraan terdekat
                explosionMask,
                null,               // bukan self-damage prevention
                0f,                 // penetration
                upwardsModifier);

            // 4. EVENT DETONASI (kamera shake, dll) — masih sempat baca posisi & radius
            OnDetonated?.Invoke(this);

            // 5. HAPUS PREFAB C4 DARI MUKA BUMI
            Destroy(gameObject);
        }

        private void PlayExplosionAudio(Vector3 position)
        {
            if (explosionSound == null) return;

            // Buat host GameObject terpisah di world space agar suara tidak mati saat C4 di-Destroy
            GameObject audioHost = new GameObject("C4_Explosion_Audio");
            audioHost.transform.position = position;

            // 1. AudioSource 3D Utama
            // Berbeda dari PlayClipAtPoint bawaan Unity (yang pakai Logarithmic dengan minDistance 1m sehingga langsung senyap di 10m),
            // di sini kita gunakan Linear rolloff dan minDistance luas (misal 25m) agar suara tetap 100% full volume di sekitar ledakan.
            AudioSource src3D = audioHost.AddComponent<AudioSource>();
            src3D.clip = explosionSound;
            src3D.volume = Mathf.Clamp01(explosionVolume);
            src3D.spatialBlend = 1.0f; // 3D
            src3D.rolloffMode = AudioRolloffMode.Linear;
            src3D.minDistance = Mathf.Max(1f, explosionMinDistance);
            src3D.maxDistance = Mathf.Max(src3D.minDistance + 10f, explosionMaxDistance);
            src3D.dopplerLevel = 0f;
            src3D.playOnAwake = false;
            src3D.Play();

            // 2. Lapisan Shockwave 2D (Direct to listener / stereo)
            // Memberikan efek gemuruh/bass dahsyat ke telinga player jika berada dalam radius aman ledakan
            if (enableProximityShockwave)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    float dist = Vector3.Distance(cam.transform.position, position);
                    if (dist <= shockwaveDistance)
                    {
                        float proximityFactor = Mathf.Clamp01(1f - (dist / shockwaveDistance));
                        AudioSource src2D = audioHost.AddComponent<AudioSource>();
                        src2D.clip = explosionSound;
                        src2D.volume = Mathf.Clamp01(explosionVolume) * (0.35f + 0.65f * proximityFactor);
                        src2D.spatialBlend = 0.0f; // 2D Stereo langsung ke telinga
                        src2D.pitch = 0.92f; // Sedikit lebih berat untuk efek bass rumble
                        src2D.playOnAwake = false;
                        src2D.Play();
                    }
                }
            }

            // Hancurkan host otomatis setelah suara selesai
            Destroy(audioHost, explosionSound.length + 0.5f);
        }

        // Gambar bola kawat merah di Scene View biar lu tau seberapa gede radiusnya
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
