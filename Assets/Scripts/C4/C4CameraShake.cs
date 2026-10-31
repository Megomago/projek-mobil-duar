using UnityEngine;
using UnityEngine.SceneManagement;

namespace C4
{
    /// <summary>
    /// Getaran kamera saat C4 MELEDAK (bukan proximity terus-menerus).
    ///
    /// - Kekuatan getaran dihitung dari jarak kamera ke titik ledakan.
    /// - Radius getaran = explosionRadius C4 x radiusMultiplier (default 2x radius ledakan).
    ///   Di luar radius itu kamera tidak bergetar sama sekali.
    /// - Jalan di kamera FPP (PlayerController) maupun kamera kendaraan (VehicleCamera)
    ///   karena eksekusinya paling akhir, jadi offset-nya ditambahkan setelah pose kamera
    ///   ditulis oleh script lain.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public class C4CameraShake : MonoBehaviour
    {
        [Header("=== RADIUS ===")]
        [Tooltip("Kelipatan radius ledakan C4 yang dipakai sebagai radius getaran. 2 = 2x explosionRadius.")]
        [Min(0.1f)]
        public float radiusMultiplier = 2f;

        [Header("=== SHAKE STYLE ===")]
        [Tooltip("Offset posisi maksimum tepat di titik ledakan (meter).")]
        public float maxPositionOffset = 0.15f;

        [Tooltip("Rotasi maksimum tepat di titik ledakan (derajat).")]
        public float maxRotationAngle = 3.5f;

        [Tooltip("Frekuensi getaran per detik.")]
        public float shakeFrequency = 30f;

        [Tooltip("Lama getaran meluruh sampai hilang (detik).")]
        public float shakeDuration = 0.7f;

        [Tooltip("Kurva jarak. Makin besar, getaran makin cepat hilang di luar radius ledakan.")]
        [Range(1f, 4f)]
        public float falloffPower = 2f;

        private Camera _camera;
        private float _intensity;
        private float _decayRate;
        private float _noiseSeed;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
                _camera = Camera.main;

            _noiseSeed = Random.value * 100f;
        }

        private void OnEnable()
        {
            C4Explosive.OnDetonated += HandleC4Detonated;
        }

        private void OnDisable()
        {
            C4Explosive.OnDetonated -= HandleC4Detonated;
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }

            if (_intensity <= 0f)
                return;

            // Meluruh sampai habis dalam shakeDuration detik.
            _intensity = Mathf.MoveTowards(_intensity, 0f, _decayRate * Time.deltaTime);
            if (_intensity <= 0f)
                return;

            // Perlin noise -> offset posisi & rotasi.
            float time = Time.time * shakeFrequency;

            float noiseX = (Mathf.PerlinNoise(_noiseSeed + 1f, time) * 2f - 1f);
            float noiseY = (Mathf.PerlinNoise(_noiseSeed + 2f, time) * 2f - 1f);
            float noiseZ = (Mathf.PerlinNoise(_noiseSeed + 3f, time) * 2f - 1f);

            float positionAmplitude = maxPositionOffset * _intensity;
            float rotationAmplitude = maxRotationAngle * _intensity;

            Vector3 localOffset = new Vector3(noiseX, noiseY, noiseZ * 0.4f) * positionAmplitude;
            _camera.transform.position += _camera.transform.rotation * localOffset;

            float pitch = (Mathf.PerlinNoise(_noiseSeed + 4f, time) * 2f - 1f) * rotationAmplitude;
            float yaw = (Mathf.PerlinNoise(_noiseSeed + 5f, time) * 2f - 1f) * rotationAmplitude;
            float roll = (Mathf.PerlinNoise(_noiseSeed + 6f, time) * 2f - 1f) * (rotationAmplitude * 0.5f);

            _camera.transform.rotation *= Quaternion.Euler(pitch, yaw, roll);
        }

        private void HandleC4Detonated(C4Explosive explosive)
        {
            if (explosive == null) return;

            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return;

            float radius = Mathf.Max(0.01f, explosive.explosionRadius * radiusMultiplier);
            float sqrDistance = (explosive.transform.position - cam.transform.position).sqrMagnitude;

            // Di luar 2x radius ledakan -> tidak kerasa sama sekali.
            if (sqrDistance > radius * radius) return;

            float distance = Mathf.Sqrt(sqrDistance);
            float falloff = 1f - (distance / radius);
            falloff = Mathf.Pow(falloff, Mathf.Max(1f, falloffPower));

            if (falloff <= _intensity) return;

            _intensity = falloff;
            _decayRate = _intensity / Mathf.Max(0.01f, shakeDuration);
        }

        // ------------------------------------------------------------------
        // AUTO-ATTACH
        // Pasang sendiri ke Main Camera supaya tidak perlu drag komponen
        // manual di scene. Tetap aman kalau komponen sudah dipasang.
        // ------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            AttachToMainCamera();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AttachToMainCamera();
        }

        private static void AttachToMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            if (cam.GetComponent<C4CameraShake>() == null)
                cam.gameObject.AddComponent<C4CameraShake>();
        }
    }
}
