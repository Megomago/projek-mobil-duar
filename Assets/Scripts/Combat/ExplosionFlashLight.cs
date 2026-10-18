using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Kilatan cahaya ledakan: intensitas memuncak 1 frame lalu meluruh cepat,
    /// plus flicker halus biar terasa organik. Dipakai oleh ProceduralExplosionBuilder
    /// pada node "8_FlashLight".
    /// </summary>
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    public class ExplosionFlashLight : MonoBehaviour
    {
        [Header("=== KILATAN CAHAYA LEDAKAN ===")]
        public float peakIntensity = 14f;
        public float duration = 0.4f;
        public float decayPower = 2.2f;
        public float flickerAmount = 0.15f;

        private Light _light;
        private float _time;
        private bool _done;

        private void Awake()
        {
            _light = GetComponent<Light>();
        }

        private void OnEnable()
        {
            if (_light == null) _light = GetComponent<Light>();
            _time = 0f;
            _done = false;
            if (_light != null)
            {
                _light.enabled = true;
                _light.intensity = peakIntensity;
            }
        }

        private void Update()
        {
            if (_done || _light == null) return;

            _time += Time.deltaTime;
            float t = _time / Mathf.Max(0.0001f, duration);

            if (t >= 1f)
            {
                _light.intensity = 0f;
                _light.enabled = false;
                _done = true;
                return;
            }

            float decay = Mathf.Pow(1f - t, decayPower);
            float flicker = 1f - flickerAmount * Mathf.PerlinNoise(_time * 35f, 0.37f);
            _light.intensity = peakIntensity * decay * flicker;
        }
    }
}
