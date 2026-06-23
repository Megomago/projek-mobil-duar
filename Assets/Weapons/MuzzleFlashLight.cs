using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Script ringan untuk membuat Point Light berkedip cepat (fade out) 
    /// seperti kilatan cahaya tembakan peluru.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class MuzzleFlashLight : MonoBehaviour
    {
        [Tooltip("Berapa lama cahaya menyala sebelum mati total (detik)")]
        public float fadeDuration = 0.08f;
        
        [Tooltip("Terang maksimal dari cahaya ini")]
        public float maxIntensity = 2f;

        private Light _light;
        private float _timeAlive;

        private void Awake()
        {
            _light = GetComponent<Light>();
        }

        private void OnEnable()
        {
            // Reset nyala lampu setiap kali di-spawn dari Object Pool
            _timeAlive = 0f;
            if (_light != null)
            {
                _light.intensity = maxIntensity;
                _light.enabled = true;
            }
        }

        private void Update()
        {
            if (_light != null && _light.enabled)
            {
                _timeAlive += Time.deltaTime;
                
                // Kurangi intensitas cahaya secara perlahan sampai 0
                float progress = _timeAlive / fadeDuration;
                _light.intensity = Mathf.Lerp(maxIntensity, 0f, progress);

                // Matikan lampu kalau durasinya sudah habis
                if (_timeAlive >= fadeDuration)
                {
                    _light.enabled = false;
                }
            }
        }
    }
}
