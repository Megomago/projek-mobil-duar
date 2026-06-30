using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace Weapons
{
    public class LoadoutManager : MonoBehaviour
    {
        [Header("=== DATABASE ===")]
        public VehicleDatabase vehicleDatabase;
        public ModuleDatabase moduleDatabase; // <-- Database baru untuk semua modul (termasuk senjata)
        
        // WeaponDatabase dipertahankan jika masih dibutuhkan sistem lain, 
        // tapi inventaris akan menggunakan ModuleDatabase
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

        [Header("=== INVENTORY UI (CATALOG) ===")]
        public GameObject weaponGridPanel; 
        public Transform gridContainer; // Wadah UI list/grid tombol modul
        public GameObject uiModuleItemPrefab; // Prefab UIModuleItem

        private int _currentVehicleIndex = 0;
        private GameObject _currentPreviewVehicle;
        private VehicleStatsManager _currentStatsManager;
        private GridVisualizer _currentGridVisualizer;

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
    
    PlayerPrefs.SetString("SelectedVehicle", currentData.vehicleName);
    PlayerPrefs.Save();

    if (vehicleNameText != null) vehicleNameText.text = currentData.vehicleName;

    if (_currentPreviewVehicle != null) Destroy(_currentPreviewVehicle);

    if (currentData.vehiclePrefab != null && vehiclePreviewPivot != null)
    {
        _currentPreviewVehicle = Instantiate(currentData.vehiclePrefab, vehiclePreviewPivot.position, vehiclePreviewPivot.rotation);
        _currentPreviewVehicle.name = currentData.vehicleName;

        _currentStatsManager = _currentPreviewVehicle.GetComponent<VehicleStatsManager>();
        if (_currentStatsManager != null)
        {
            _currentStatsManager.isPreviewMode = true; // <--- TAMBAHIN INI
        }
        
        if (_currentPreviewVehicle.TryGetComponent<VehicleController>(out var vc)) vc.enabled = false;
        AudioSource[] audioSources = _currentPreviewVehicle.GetComponentsInChildren<AudioSource>();
        foreach (var audio in audioSources) audio.enabled = false;

        _currentStatsManager = _currentPreviewVehicle.GetComponent<VehicleStatsManager>();
        _currentGridVisualizer = _currentPreviewVehicle.GetComponent<GridVisualizer>();
        if (_currentGridVisualizer == null && _currentStatsManager != null)
        {
            _currentGridVisualizer = _currentPreviewVehicle.AddComponent<GridVisualizer>();
        }

        if (_currentStatsManager != null && moduleDatabase != null)
        {
            GridSaveSystem.LoadGrid(currentData.vehicleName, _currentStatsManager, moduleDatabase);
        }

        // === PAKEN COROUTINE BUAT DELAY 1 FRAME ===
        StartCoroutine(DisableAfterSpawn());
    }

    CloseInventoryMode();
}

// === TAMBAHIN INI ===
private System.Collections.IEnumerator DisableAfterSpawn()
{
    yield return null; // Tunggu 1 frame biar semua object fully spawned
    
    if (_currentPreviewVehicle == null) yield break;

    ManualTurretController[] allTurrets = _currentPreviewVehicle.GetComponentsInChildren<ManualTurretController>(true);
    foreach (var turret in allTurrets)
    {
        turret.enabled = false;
    }

    Animator[] allAnimators = _currentPreviewVehicle.GetComponentsInChildren<Animator>(true);
    foreach (var anim in allAnimators)
    {
        anim.enabled = false;
    }

    Rigidbody[] allRbs = _currentPreviewVehicle.GetComponentsInChildren<Rigidbody>(true);
    foreach (var rb in allRbs)
    {
        if (rb != _currentPreviewVehicle.GetComponent<Rigidbody>())
        {
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
    }
}

        // === UI MODE TOGGLE ===
        public void OpenInventoryMode()
        {
            if (mainUIPanel != null) mainUIPanel.SetActive(false);
            if (inventoryUIPanel != null) inventoryUIPanel.SetActive(true);
            if (weaponGridPanel != null) weaponGridPanel.SetActive(true); // Tampilkan panel katalog
            
            if (_currentGridVisualizer != null) _currentGridVisualizer.ToggleGrid(true);

            PopulateInventoryCatalog();

            // KAMERA MINGGIR SECARA OTOMATIS
            if (klikKananKamera != null)
            {
                klikKananKamera.ToggleInventoryMode(true);
            }
        }

        private void PopulateInventoryCatalog()
        {
            if (gridContainer == null || uiModuleItemPrefab == null || moduleDatabase == null) return;

            // Bersihkan isi grid lama
            foreach (Transform child in gridContainer)
            {
                Destroy(child.gameObject);
            }

            // Buat tombol untuk tiap modul di database
            foreach (ModuleTemplate template in moduleDatabase.allModules)
            {
                if (template == null) continue;

                GameObject itemObj = Instantiate(uiModuleItemPrefab, gridContainer);
                UIModuleItem uiItem = itemObj.GetComponent<UIModuleItem>();
                if (uiItem != null)
                {
                    uiItem.Initialize(template, _currentStatsManager);
                }
            }
        }

        public void CloseInventoryMode()
        {
            if (mainUIPanel != null) mainUIPanel.SetActive(true);
            if (inventoryUIPanel != null) inventoryUIPanel.SetActive(false);
            if (weaponGridPanel != null) weaponGridPanel.SetActive(false);

            if (_currentGridVisualizer != null) _currentGridVisualizer.ToggleGrid(false);

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

        // === GRID SYSTEM LAMA DIHAPUS ===
        // (Logika Inventory Grid 3D akan menggantikan bagian ini)
    }
}