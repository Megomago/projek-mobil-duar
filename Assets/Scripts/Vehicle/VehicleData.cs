using UnityEngine;

namespace Weapons
{
    [CreateAssetMenu(fileName = "New VehicleData", menuName = "Vehicle/Vehicle Data")]
    public class VehicleData : ScriptableObject
    {
        [Tooltip("Nama kendaraan (harus UNIK dan sama dengan VehicleBaseData.vehicleName).")]
        public string vehicleName;
        [Tooltip("Prefab kendaraan.")]
        public GameObject vehiclePrefab;
    }
}
