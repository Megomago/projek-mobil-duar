using UnityEngine;
using UnityEngine.Serialization;

namespace Weapons
{
    [CreateAssetMenu(fileName = "New VehicleData", menuName = "Weapons/Vehicle Data")]
    public class VehicleData : ScriptableObject
    {
        [Tooltip("Nama kendaraan yang akan muncul di UI Lobby")]
        public string vehicleName = "Unnamed Vehicle";
        
        [Tooltip("Prefab asli kendaraan (yang memiliki script VehicleWeaponManager)")]
        public GameObject vehiclePrefab;
        
        [Tooltip("Jumlah slot senjata yang dimiliki mobil ini (Masih dipertahankan sementara)")]
        public int weaponSlotCount = 1;

        [Header("=== GRID INVENTORY SETTINGS ===")]
        [Tooltip("Lebar grid (jumlah kotak sumbu X)")]
        [FormerlySerializedAs("gridColumns")]
        [Min(1)] public int gridSizeX = 6;

        [Tooltip("Tinggi grid (jumlah kotak sumbu Y)")]
        [FormerlySerializedAs("gridRows")]
        [Min(1)] public int gridSizeY = 4;

        [Tooltip("Ukuran 1 kotak grid di dunia 3D (meter). UI inventori pakai pixel terpisah.")]
        [Min(0.01f)] public float gridCellSize = 0.25f;
    }
}
