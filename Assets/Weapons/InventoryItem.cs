using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Merepresentasikan satu buah senjata/item fisik di dalam Grid Inventaris.
    /// Script ini adalah data murni (bukan MonoBehaviour), mengelola logika letak dan rotasi.
    /// </summary>
    public class InventoryItem
    {
        public WeaponData Data { get; private set; }
        
        // Posisi di dalam grid (titik origin: X paling kiri, Y paling atas)
        public int GridX { get; set; }
        public int GridY { get; set; }
        
        // Status apakah item sedang diputar 90 derajat
        public bool IsRotated { get; private set; }

        public InventoryItem(WeaponData data)
        {
            Data = data;
            IsRotated = false;
        }

        /// <summary>
        /// Lebar dinamis item: Menyesuaikan apakah sedang dirotasi atau tidak.
        /// </summary>
        public int Width => IsRotated ? Data.gridHeight : Data.gridWidth;

        /// <summary>
        /// Tinggi dinamis item: Menyesuaikan apakah sedang dirotasi atau tidak.
        /// </summary>
        public int Height => IsRotated ? Data.gridWidth : Data.gridHeight;

        /// <summary>
        /// Memutar item (menukar lebar dengan tinggi).
        /// </summary>
        public void Rotate()
        {
            IsRotated = !IsRotated;
        }
    }
}
