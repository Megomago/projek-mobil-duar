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

        [Header("=== UI SETTINGS ===")]
        [Tooltip("Teks untuk menampilkan nama mobil yang sedang dipilih")]
        public TextMeshProUGUI vehicleNameText;
        [Tooltip("Wadah Dropdown. Dropdown yang tidak terpakai akan disembunyikan.")]
        public List<TMP_Dropdown> slotDropdowns;

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
                
                // Matikan komponen fisika & player input agar mobil tidak jalan-jalan di lobby
                if (_currentPreviewVehicle.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
                if (_currentPreviewVehicle.TryGetComponent<VehicleController>(out var vc)) vc.enabled = false;

                _currentPreviewWeaponManager = _currentPreviewVehicle.GetComponent<VehicleWeaponManager>();
                if (_currentPreviewWeaponManager != null)
                {
                    // Beritahu manajer nama kendaraannya untuk PlayerPrefs
                    _currentPreviewWeaponManager.currentVehicleName = currentData.vehicleName;
                    
                    // Supaya HUD tidak muncul menumpuk di Lobby, hilangkan hudContainer
                    _currentPreviewWeaponManager.hudContainer = null;
                }
            }

            // --- 2. UPDATE UI DROPDOWN ---
            int slotCount = currentData.weaponSlotCount;
            
            for (int i = 0; i < slotDropdowns.Count; i++)
            {
                TMP_Dropdown dropdown = slotDropdowns[i];
                if (dropdown == null) continue;

                // Hilangkan listener lama agar tidak trigger saat kita set value manual
                dropdown.onValueChanged.RemoveAllListeners();

                if (i < slotCount)
                {
                    dropdown.gameObject.SetActive(true);
                    SetupDropdown(dropdown, i, currentData.vehicleName);
                    
                    int slotIndex = i; // local copy for delegate
                    string vehName = currentData.vehicleName;
                    dropdown.onValueChanged.AddListener((int val) => OnDropdownChanged(vehName, slotIndex, val));
                }
                else
                {
                    dropdown.gameObject.SetActive(false);
                }
            }
        }

        private void SetupDropdown(TMP_Dropdown dropdown, int slotIndex, string vehicleName)
        {
            dropdown.ClearOptions();
            List<string> options = new List<string> { "Kosong (Tidak Dipasang)" };

            int savedIndex = 0;
            string prefKey = $"WeaponSlot_{vehicleName}_{slotIndex}";
            string savedWeaponName = PlayerPrefs.GetString(prefKey, "");

            for (int j = 0; j < weaponDatabase.allWeapons.Count; j++)
            {
                WeaponData wData = weaponDatabase.allWeapons[j];
                if (wData != null)
                {
                    options.Add(wData.weaponName);
                    if (wData.weaponName == savedWeaponName)
                    {
                        savedIndex = j + 1; // +1 karena index 0 = Kosong
                    }
                }
            }

            dropdown.AddOptions(options);
            dropdown.value = savedIndex;
        }

        private void OnDropdownChanged(string vehicleName, int slotIndex, int dropdownIndex)
        {
            string prefKey = $"WeaponSlot_{vehicleName}_{slotIndex}";

            if (dropdownIndex == 0)
            {
                PlayerPrefs.SetString(prefKey, "");
            }
            else
            {
                WeaponData selectedWeapon = weaponDatabase.allWeapons[dropdownIndex - 1];
                PlayerPrefs.SetString(prefKey, selectedWeapon.weaponName);
            }
            
            PlayerPrefs.Save();

            // --- REFRESH VISUAL SENJATA DI MOBIL PREVIEW ---
            if (_currentPreviewWeaponManager != null)
            {
                _currentPreviewWeaponManager.RefreshWeapons();
            }
        }
    }
}
