using UnityEngine;

namespace C4
{
    /// <summary>
    /// Marker untuk C4 yang sudah ditempel di dunia.
    /// Saat ini hanya menyimpan data dasar. Sistem aktivasi/detonasi akan ditambahkan nanti.
    /// </summary>
    public class C4Explosive : MonoBehaviour
    {
        [Header("=== RUNTIME (READ ONLY) ===")]
        [Tooltip("Player yang menempelkan C4 ini.")]
        public Transform planter;

        [Tooltip("Waktu (Time.time) saat C4 ditempel.")]
        public float plantedTime;

        [Tooltip("Status armed. Belum dipakai sampai sistem aktivasi ditambahkan.")]
        public bool isArmed;

        public void Initialize(Transform planter)
        {
            this.planter = planter;
            plantedTime = Time.time;
        }
    }
}
