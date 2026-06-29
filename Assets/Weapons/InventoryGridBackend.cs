using System.Collections.Generic;
using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Menangani logika backend (tanpa UI) dari sistem grid inventaris gaya Tetris.
    /// Script UI Anda (USER) nanti dapat memanggil fungsi di sini untuk memvalidasi penempatan.
    /// </summary>
    public class InventoryGridBackend
    {
        public int SizeX { get; private set; }
        public int SizeY { get; private set; }
        
        // Array 2D menyimpan referensi item di setiap kotak grid. Null jika kosong.
        // Array dibaca [X, Y].
        private InventoryItem[,] _grid;
        
        // Daftar semua item yang berhasil dimasukkan ke grid
        public List<InventoryItem> ItemsInGrid { get; private set; }

        public InventoryGridBackend(int sizeX, int sizeY)
        {
            SizeX = sizeX;
            SizeY = sizeY;
            _grid = new InventoryItem[sizeX, sizeY];
            ItemsInGrid = new List<InventoryItem>();
        }

        /// <summary>
        /// Mengecek apakah item bisa diletakkan di koordinat grid tertentu (X, Y).
        /// Posisi X dan Y merepresentasikan sudut kiri-atas dari item.
        /// </summary>
        public bool CanPlaceItem(InventoryItem item, int startX, int startY)
        {
            int itemWidth = item.Width;
            int itemHeight = item.Height;

            // 1. Cek apakah menabrak batas batas ukuran grid
            if (startX < 0 || startY < 0 || startX + itemWidth > SizeX || startY + itemHeight > SizeY)
            {
                return false;
            }

            // 2. Cek apakah ada tabrakan (overlap) dengan item lain di area tersebut
            for (int x = startX; x < startX + itemWidth; x++)
            {
                for (int y = startY; y < startY + itemHeight; y++)
                {
                    // Jika diabaikan item-nya sendiri (saat memindahkannya)
                    if (_grid[x, y] != null && _grid[x, y] != item)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Meletakkan item di grid. Mengembalikan true jika berhasil.
        /// </summary>
        public bool PlaceItem(InventoryItem item, int startX, int startY)
        {
            if (!CanPlaceItem(item, startX, startY))
            {
                return false;
            }

            // Jika item sudah ada di grid sebelumnya, hapus posisi lamanya dulu
            if (ItemsInGrid.Contains(item))
            {
                RemoveItem(item);
            }

            // Set posisi baru pada item
            item.GridX = startX;
            item.GridY = startY;

            // Isi array 2D
            for (int x = startX; x < startX + item.Width; x++)
            {
                for (int y = startY; y < startY + item.Height; y++)
                {
                    _grid[x, y] = item;
                }
            }

            ItemsInGrid.Add(item);
            return true;
        }

        /// <summary>
        /// Menghapus item dari grid.
        /// </summary>
        public void RemoveItem(InventoryItem item)
        {
            if (!ItemsInGrid.Contains(item)) return;

            // Kosongkan referensi di array 2D
            for (int x = item.GridX; x < item.GridX + item.Width; x++)
            {
                for (int y = item.GridY; y < item.GridY + item.Height; y++)
                {
                    if (_grid[x, y] == item)
                    {
                        _grid[x, y] = null;
                    }
                }
            }

            ItemsInGrid.Remove(item);
        }

        /// <summary>
        /// Mendapatkan item apa yang berada pada koordinat X, Y (untuk deteksi klik/hover di UI).
        /// </summary>
        public InventoryItem GetItemAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= SizeX || y >= SizeY) return null;
            return _grid[x, y];
        }

        // --- SISTEM PENYIMPANAN DATA (SAVE/LOAD) ---

        public void SaveToPlayerPrefs(string vehicleName)
        {
            SavedGridData dataToSave = new SavedGridData();
            foreach (var item in ItemsInGrid)
            {
                dataToSave.items.Add(new SavedGridItem()
                {
                    weaponName = item.Data.weaponName,
                    x = item.GridX,
                    y = item.GridY,
                    isRotated = item.IsRotated
                });
            }

            string json = JsonUtility.ToJson(dataToSave);
            PlayerPrefs.SetString("TetrisGrid_" + vehicleName, json);
            PlayerPrefs.Save();
        }

        public void LoadFromPlayerPrefs(string vehicleName, WeaponDatabase db)
        {
            string key = "TetrisGrid_" + vehicleName;
            if (!PlayerPrefs.HasKey(key)) return;

            string json = PlayerPrefs.GetString(key);
            SavedGridData loadedData = JsonUtility.FromJson<SavedGridData>(json);

            if (loadedData != null && loadedData.items != null)
            {
                foreach (var savedItem in loadedData.items)
                {
                    WeaponData wData = db.GetWeaponByName(savedItem.weaponName);
                    if (wData != null)
                    {
                        InventoryItem newItem = new InventoryItem(wData);
                        // Jika status rotasi di save file adalah true, kita rotate
                        if (savedItem.isRotated) newItem.Rotate();
                        
                        PlaceItem(newItem, savedItem.x, savedItem.y);
                    }
                }
            }
        }
    }

    [System.Serializable]
    public class SavedGridItem
    {
        public string weaponName;
        public int x;
        public int y;
        public bool isRotated;
    }

    [System.Serializable]
    public class SavedGridData
    {
        public List<SavedGridItem> items = new List<SavedGridItem>();
    }
}
