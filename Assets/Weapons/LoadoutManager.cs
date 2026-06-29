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

        [Header("=== MODE PANELS ===")]
        public GameObject mainUIPanel;
        public GameObject inventoryUIPanel;

        [Header("=== INVENTORY UI (KATALOG SCROLL) ===")]
        [Tooltip("Parent/Wadah tempat daftar senjata di-spawn (Biasanya di dalam Content Scroll View)")]
        public Transform gridContainer;
        [Tooltip("Prefab untuk tombol/item daftar senjata (WeaponGridItemUI)")]
        public GameObject gridItemPrefab;

        [Header("=== PLACEMENT MODE ===")]
        [Tooltip("Pasang senjata langsung ke grid 3D di mobil (KSP style). Matikan untuk pakai panel Tetris 2D lama.")]
        public bool use3DGridPlacement = true;

        private int _currentVehicleIndex = 0;
        private GameObject _currentPreviewVehicle;
        private VehicleWeaponManager _currentPreviewWeaponManager;
        private VehicleGrid3DPlacer _currentGridPlacer;

        public TetrisGridUI GetActiveTetrisGrid()
        {
            if (inventoryUIPanel == null) return null;
            return inventoryUIPanel.GetComponentInChildren<TetrisGridUI>(true);
        }

        public VehicleWeaponManager PreviewWeaponManager => _currentPreviewWeaponManager;
        public VehicleGrid3DPlacer PreviewGridPlacer => _currentGridPlacer;
        public bool Use3DGridPlacement => use3DGridPlacement;

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

            PopulateWeaponCatalog();
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
                
                // Matikan player input agar mobil tidak jalan-jalan
                if (_currentPreviewVehicle.TryGetComponent<VehicleController>(out var vc)) vc.enabled = false;

                // Matikan semua suara di lobby
                AudioSource[] audioSources = _currentPreviewVehicle.GetComponentsInChildren<AudioSource>();
                foreach (var audio in audioSources)
                {
                    audio.enabled = false;
                }

                _currentPreviewWeaponManager = _currentPreviewVehicle.GetComponent<VehicleWeaponManager>();
                _currentGridPlacer = _currentPreviewVehicle.GetComponent<VehicleGrid3DPlacer>();

                if (_currentPreviewWeaponManager != null)
                {
                    _currentPreviewWeaponManager.vehicleData = currentData;
                    _currentPreviewWeaponManager.currentVehicleName = currentData.vehicleName;
                    _currentPreviewWeaponManager.hudContainer = null;
                    _currentPreviewWeaponManager.usePlayerInput = false;
                    _currentPreviewWeaponManager.weaponDatabase = weaponDatabase;
                    _currentPreviewWeaponManager.SyncGridSettings();
                    _currentPreviewWeaponManager.RefreshGridVisual();
                    _currentPreviewWeaponManager.RefreshWeapons();
                }

                if (_currentGridPlacer != null)
                {
                    _currentGridPlacer.vehicleData = currentData;
                    _currentGridPlacer.weaponDatabase = weaponDatabase;
                    _currentGridPlacer.enabled = false;
                }
            }

            // Jika Panel Tetris sedang terbuka, kita perlu me-restart-nya untuk mobil baru
            TetrisGridUI tetrisGrid = inventoryUIPanel.GetComponentInChildren<TetrisGridUI>(true);
            if (tetrisGrid != null && inventoryUIPanel.activeSelf)
            {
                tetrisGrid.InitializeGrid(currentData);
                tetrisGrid.RefreshVisualGrid();
            }

            CloseInventoryMode();
        }

        // === UI MODE TOGGLE ===
        public void OpenInventoryMode()
        {
            if (mainUIPanel != null) mainUIPanel.SetActive(false);
            
            if (inventoryUIPanel != null) 
            {
                inventoryUIPanel.SetActive(true);

                VehicleData currentData = vehicleDatabase.allVehicles[_currentVehicleIndex];

                SetTetrisGrid2DVisible(!use3DGridPlacement);

                if (!use3DGridPlacement)
                {
                    TetrisGridUI tetrisGrid = GetActiveTetrisGrid();
                    if (tetrisGrid != null)
                    {
                        tetrisGrid.InitializeGrid(currentData);
                        tetrisGrid.RefreshVisualGrid();
                    }
                }

                if (_currentGridPlacer != null && use3DGridPlacement)
                {
                    _currentGridPlacer.placementCamera = Camera.main;
                    _currentGridPlacer.enabled = true;
                    _currentGridPlacer.LoadGrid();
                }

                if (_currentPreviewWeaponManager != null)
                {
                    _currentPreviewWeaponManager.RefreshGridVisual();
                    _currentPreviewWeaponManager.RefreshWeapons();
                }
            }
        }

        public void CloseInventoryMode()
        {
            _currentGridPlacer?.CancelPlacing();
            if (_currentGridPlacer != null) _currentGridPlacer.enabled = false;

            if (mainUIPanel != null) mainUIPanel.SetActive(true);
            if (inventoryUIPanel != null) inventoryUIPanel.SetActive(false);
        }

        private void SetTetrisGrid2DVisible(bool visible)
        {
            TetrisGridUI tetrisGrid = GetActiveTetrisGrid();
            if (tetrisGrid != null)
            {
                tetrisGrid.gameObject.SetActive(visible);
            }
        }

        // === KATALOG SENJATA ===
        private void PopulateWeaponCatalog()
        {
            if (gridContainer == null || gridItemPrefab == null || weaponDatabase == null) return;

            // Bersihkan isi scroll view lama
            foreach (Transform child in gridContainer)
            {
                Destroy(child.gameObject);
            }

            // Munculkan daftar senjata untuk di-drag
            foreach (WeaponData wData in weaponDatabase.allWeapons)
            {
                if (wData == null) continue;

                GameObject item = Instantiate(gridItemPrefab, gridContainer);
                WeaponGridItemUI itemUI = item.GetComponent<WeaponGridItemUI>();
                if (itemUI != null)
                {
                    itemUI.Initialize(wData, this);
                }
            }
        }
    }
}
