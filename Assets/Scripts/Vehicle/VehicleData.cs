using UnityEngine;

namespace Weapons
{
    [CreateAssetMenu(fileName = "New VehicleData", menuName = "Vehicle/Vehicle Data")]
    public class VehicleData : ScriptableObject
    {
        [Tooltip("ID unik STABIL untuk save/load. Digenerate otomatis — jangan diubah manual.")]
        [SerializeField] private string uid;
        public string UID => uid;

        [Tooltip("Nama kendaraan (harus UNIK dan sama dengan VehicleBaseData.vehicleName).")]
        public string vehicleName;
        [Tooltip("Prefab kendaraan.")]
        public GameObject vehiclePrefab;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(uid))
                uid = System.Guid.NewGuid().ToString("N");
        }
    }
}
