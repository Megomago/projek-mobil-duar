using UnityEngine;

namespace Weapons
{
    [CreateAssetMenu(fileName = "New VehicleData", menuName = "Vehicle/Vehicle Data")]
    public class VehicleData : ScriptableObject
    {
        [Tooltip("Nama kendaraan yang akan muncul di UI Lobby")]
        public string vehicleName = "Unnamed Vehicle";
        
        [Tooltip("Prefab asli kendaraan (yang memiliki script VehicleWeaponManager)")]
        public GameObject vehiclePrefab;
        
        [Tooltip("Jumlah slot senjata yang dimiliki mobil ini (hanya untuk referensi UI UI Dropdown)")]
        public int weaponSlotCount = 1;
    }
}
