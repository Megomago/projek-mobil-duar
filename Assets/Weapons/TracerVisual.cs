using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Script untuk menunda pemunculan visual tracer (Mesh) agar tidak langsung menempel di ujung laras senapan saat ditembakkan.
    /// </summary>
    public class TracerVisual : MonoBehaviour
    {
        [Tooltip("Waktu tunda sebelum kilatan menyala (detik)")]
        public float delayToEnable = 0.02f;
        
        private MeshRenderer _meshRenderer;
        private float _timeAlive;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        private void OnEnable()
        {
            _timeAlive = 0f;
            if (_meshRenderer != null) 
            {
                _meshRenderer.enabled = false; // Matikan saat baru di-spawn dari Object Pool
            }
        }

        private void Update()
        {
            if (_meshRenderer != null && !_meshRenderer.enabled)
            {
                _timeAlive += Time.deltaTime;
                if (_timeAlive >= delayToEnable)
                {
                    _meshRenderer.enabled = true; // Nyalakan setelah melewati waktu tunda
                }
            }
        }
    }
}
