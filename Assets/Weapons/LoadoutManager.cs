using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace Weapons
{
    public class LoadoutManager : MonoBehaviour
    {
        [Header("=== DATABASE ===")]
        public VehicleDatabase vehicleDatabase;
        public WeaponDatabase weaponDatabase;

        [Header("=== PREVIEW SETTINGS ===")]
        [Tooltip("Titik (Transform) di mana mobil 3D akan dimunculkan di Lobby")]
        public Transform vehiclePreviewPivot;

        [Header("=== INTEGRATED CAMERA SYSTEM ===")]
        [Tooltip("Tarik objek Cinemachine FreeLook yang ada script KlikKananKamera-nya kesini le!")]
        public KlikKananKamera klikKananKamera; // <--- INI INTEGRASI BARUNYA!

        [Header("=== UI SETTINGS ===")]
        [Tooltip("Teks untuk menampilkan nama mobil yang sedang dipilih")]
        public TextMeshProUGUI vehicleNameText;

        [Header("=== MODE PANELS ===")]
        public GameObject mainUIPanel;
        public GameObject inventoryUIPanel;

        [Header("=== INVENTORY UI ===")]
        public List<WeaponSlotUI> slotButtons;
        public GameObject weaponGridPanel;
        public Transform gridContainer;
        public GameObject gridItemPrefab;

        private int _activeSlotIndex = -1;

        private int _currentVehicleIndex = 0;
        private GameObject _currentPreviewVehicle;
        private VehicleWeaponManager _currentPreviewWeaponManager;

        private void Start()
        {
            if (vehicleDatabase == null || vehicleDatabase.allVehicles.Count == 0)
            {
                Debug.LogWarning("[LoadoutManager] VehicleDatabase kosong!");
                return;
            }

            // Muat mobil terakhir yang dipilih, atau mulai dari 0
            string savedVehicle = PlayerPrefs.GetString("SelectedVehicle", "");
            if (!string.IsNullOrEmpty(savedVehicle))
            {
                for (int i = 0; i < vehicleDatabase.allVehicles.Count; i++)
                {
                    if (vehicleDatabase.allVehicles[i] != null && vehicleDatabase.allVehicles[i].vehicleName == savedVehicle)
                    {
                        _currentVehicleIndex = i;
                        break;
                    }
                }
            }

            UpdateVehicleSelection();
        }

        public void NextVehicle()
        {
            if (vehicleDatabase == null) return;
            _currentVehicleIndex = (_currentVehicleIndex + 1) % vehicleDatabase.allVehicles.Count;
            UpdateVehicleSelection();
        }

        public void PrevVehicle()
        {
            if (vehicleDatabase == null) return;
            _currentVehicleIndex--;
            if (_currentVehicleIndex < 0) _currentVehicleIndex = vehicleDatabase.allVehicles.Count - 1;
            UpdateVehicleSelection();
        }

        private void UpdateVehicleSelection()
        {
            VehicleData currentData = vehicleDatabase.allVehicles[_currentVehicleIndex];
            
            // Simpan pilihan mobil
            PlayerPrefs.SetString("SelectedVehicle", currentData.vehicleName);
            PlayerPrefs.Save();

            if (vehicleNameText != null) vehicleNameText.text = currentData.vehicleName;

            // --- 1. SPAWN MOBIL PREVIEW ---
            if (_currentPreviewVehicle != null) Destroy(_currentPreviewVehicle);

            if (currentData.vehiclePrefab != null && vehiclePreviewPivot != null)
            {
                _currentPreviewVehicle = Instantiate(currentData.vehiclePrefab, vehiclePreviewPivot.position, vehiclePreviewPivot.rotation);
                _currentPreviewVehicle.name = currentData.vehicleName;
                
                // Matikan player input agar mobil tidak jalan-jalan, biarkan fisika nyala agar mobil "jatuh" ke lantai
                if (_currentPreviewVehicle.TryGetComponent<VehicleController>(out var vc)) vc.enabled = false;

                // Matikan semua suara (mesin, dsb) di mobil agar lobby tidak berisik
                AudioSource[] audioSources = _currentPreviewVehicle.GetComponentsInChildren<AudioSource>();
                foreach (var audio in audioSources)
                {
                    audio.enabled = false;
                }

                _currentPreviewWeaponManager = _currentPreviewVehicle.GetComponent<VehicleWeaponManager>();
                if (_currentPreviewWeaponManager != null)
                {
                    // Beritahu manajer nama kendaraannya untuk PlayerPrefs
                    _currentPreviewWeaponManager.currentVehicleName = currentData.vehicleName;
                    
                    // Supaya HUD tidak muncul menumpuk di Lobby, hilangkan hudContainer
                    _currentPreviewWeaponManager.hudContainer = null;
                    
                    // Matikan input player agar senjata tidak bisa menembak di Lobby
                    _currentPreviewWeaponManager.usePlayerInput = false;
                }
            }

            // --- 2. UPDATE UI SLOT BUTTONS ---
            int slotCount = currentData.weaponSlotCount;
            
            for (int i = 0; i < slotButtons.Count; i++)
            {
                WeaponSlotUI slotBtn = slotButtons[i];
                if (slotBtn == null) continue;

                if (i < slotCount)
                {
                    slotBtn.gameObject.SetActive(true);
                    slotBtn.Setup(i, this);
                    
                    // Baca senjata yang terpasang dari PlayerPrefs
                    string prefKey = $"WeaponSlot_{currentData.vehicleName}_{i}";
                    string savedWeaponName = PlayerPrefs.GetString(prefKey, "");
                    WeaponData savedWeapon = weaponDatabase.GetWeaponByName(savedWeaponName);
                    
                    slotBtn.UpdateVisual(savedWeapon);
                }
                else
                {
                    slotBtn.gameObject.SetActive(false);
                }
            }
            
            // Pastikan Mode Utama yang aktif saat ganti mobil
            CloseInventoryMode();
        }

        // === UI MODE TOGGLE ===
        public void OpenInventoryMode()
        {
            if (mainUIPanel != null) mainUIPanel.SetActive(false);
            if (inventoryUIPanel != null) inventoryUIPanel.SetActive(true);
            if (weaponGridPanel != null) weaponGridPanel.SetActive(false);
            
            // KAMERA MINGGIR SECARA OTOMATIS
            if (klikKananKamera != null)
            {
                klikKananKamera.ToggleInventoryMode(true);
            }
        }

        public void CloseInventoryMode()
        {
            if (mainUIPanel != null) mainUIPanel.SetActive(true);
            if (inventoryUIPanel != null) inventoryUIPanel.SetActive(false);
            if (weaponGridPanel != null) weaponGridPanel.SetActive(false);

            // KAMERA KEMBALI KE TENGAH
            if (klikKananKamera != null)
            {
                klikKananKamera.ToggleInventoryMode(false);
            }
        }

        // Dipanggil oleh tombol "X" atau "Back" yang ada di dalam panel Grid Senjata
        public void CloseWeaponGrid()
        {
            if (weaponGridPanel != null) weaponGridPanel.SetActive(false);
        }

        // === GRID SYSTEM ===
        public void OpenWeaponGrid(int slotIndex)
        {
            _activeSlotIndex = slotIndex;
            
            if (weaponGridPanel != null) weaponGridPanel.SetActive(true);

            // Bersihkan isi grid lama
            foreach (Transform child in gridContainer)
            {
                Destroy(child.gameObject);
            }

            // Buat tombol "Kosong" (Lepas Senjata)
            GameObject emptyItem = Instantiate(gridItemPrefab, gridContainer);
            emptyItem.GetComponent<WeaponGridItemUI>().Initialize(null, this);

            // Buat tombol untuk tiap senjata di database
            foreach (WeaponData wData in weaponDatabase.allWeapons)
            {
                if (wData == null) continue;

                GameObject item = Instantiate(gridItemPrefab, gridContainer);
                item.GetComponent<WeaponGridItemUI>().Initialize(wData, this);
            }
        }

        public void SelectWeaponFromGrid(WeaponData weapon)
        {
            if (_activeSlotIndex < 0) return;

            VehicleData currentData = vehicleDatabase.allVehicles[_currentVehicleIndex];
            string prefKey = $"WeaponSlot_{currentData.vehicleName}_{_activeSlotIndex}";

            if (weapon == null)
            {
                PlayerPrefs.SetString(prefKey, ""); // Kosong
            }
            else
            {
                PlayerPrefs.SetString(prefKey, weapon.weaponName);
            }
            
            PlayerPrefs.Save();

            // Refresh UI Tombol Slot
            if (_activeSlotIndex < slotButtons.Count)
            {
                slotButtons[_activeSlotIndex].UpdateVisual(weapon);
            }

            // Refresh Visual Senjata di Mobil 3D
            if (_currentPreviewWeaponManager != null)
            {
                _currentPreviewWeaponManager.RefreshWeapons();
            }

            // Tutup Panel Grid setelah memilih
            if (weaponGridPanel != null) weaponGridPanel.SetActive(false);
        }
    }
}