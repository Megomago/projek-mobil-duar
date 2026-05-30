using System.Collections;
using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Taruh script ini di prefab muzzle flash (yang ada SpriteRenderer-nya).
    /// Dia akan otomatis balik ke pool setelah durasinya habis.
    /// </summary>
    public class SpriteMuzzleFlash : MonoBehaviour
    {
        [Header("Durasi & Ukuran")]
        public float duration = 0.06f;
        public float scaleMin = 0.7f;
        public float scaleMax = 1.3f;

        [Header("Rotasi Acak (0=mati, 1=aktif)")]
        [Range(0f, 1f)] public float randomRotX = 0f;
        [Range(0f, 1f)] public float randomRotY = 0f;
        [Range(0f, 1f)] public float randomRotZ = 1f;

        private void OnEnable()
        {
            // Setiap kali di-spawn dari pool, langsung jalankan efeknya
            StartCoroutine(FlashRoutine());
        }

        private void LateUpdate()
        {
            // Billboard: selalu hadap kamera
            if (Camera.main != null)
                transform.forward = -Camera.main.transform.forward;
        }

        private IEnumerator FlashRoutine()
        {
            // Random scale
            float s = Random.Range(scaleMin, scaleMax);
            transform.localScale = new Vector3(s, s, s);

            // Random rotation
            float angle = Random.Range(0f, 360f);
            transform.localEulerAngles = new Vector3(
                randomRotX * angle,
                randomRotY * angle,
                randomRotZ * angle
            );

            // Tunggu durasi lalu kembalikan ke pool
            yield return new WaitForSeconds(duration);
            ObjectPool.Instance.Despawn(gameObject);
        }
    }
}
