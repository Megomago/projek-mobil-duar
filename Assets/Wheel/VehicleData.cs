using UnityEngine;

namespace Weapons
{
    [CreateAssetMenu(fileName = "New VehicleData", menuName = "Vehicle/Vehicle Data")]
    public class VehicleData : ScriptableObject
    {
        [Tooltip("Prefab kendaraan. Nama & stat dibaca otomatis dari VehicleBaseData di dalam prefab.")]
        public GameObject vehiclePrefab;
    }
}
