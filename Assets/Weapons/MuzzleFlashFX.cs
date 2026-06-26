using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Script optimasi untuk menghindari GetComponentsInChildren<ParticleSystem>() setiap kali senjata ditembakkan.
    /// Attach script ini ke root object prefab Muzzle Flash.
    /// </summary>
    public class MuzzleFlashFX : MonoBehaviour
    {
        private ParticleSystem[] _particleSystems;

        private void Awake()
        {
            // Cache semua particle system saat pertama kali spawn/awaken.
            _particleSystems = GetComponentsInChildren<ParticleSystem>();
        }

        public void Play()
        {
            if (_particleSystems == null) return;

            foreach (var ps in _particleSystems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(false);
            }
        }
    }
}
