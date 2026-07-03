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
        [Tooltip("Referensi ke VehicleHUD untuk update stat saat ganti mobil")]
        public VehicleHUD vehicleHUD;

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

            // Cari mobil yang cocok dengan save terakhir
            string savedVehicle = PlayerPrefs.GetString("SelectedVehicle", "");
            if (!string.IsNullOrEmpty(savedVehicle))
            {
                for (int i = 0; i < vehicleDatabase.allVehicles.Count; i++)
                {
                    VehicleData vd = vehicleDatabase.allVehicles[i];
                    if (vd == null || vd.vehiclePrefab == null) continue;

                    // Baca nama dari VehicleBaseData di dalam prefab
                    VehicleStatsManager sm = vd.vehiclePrefab.GetComponent<VehicleStatsManager>();
                    string nameInPrefab = (sm != null && sm.baseData != null)
                        ? sm.baseData.vehicleName
                        : vd.name;

                    if (nameInPrefab == savedVehicle)
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

            if (currentData.vehiclePrefab == null)
            {
                Debug.LogWarning("[LoadoutManager] VehicleData tidak punya prefab!");
                return;
            }

            if (_currentPreviewVehicle != null) Destroy(_currentPreviewVehicle);

            _currentPreviewVehicle = Instantiate(currentData.vehiclePrefab, vehiclePreviewPivot.position, vehiclePreviewPivot.rotation);

            // ── Baca nama dari VehicleBaseData di dalam Prefab ────────────
            _currentStatsManager = _currentPreviewVehicle.GetComponent<VehicleStatsManager>();
            string vehicleName = (_currentStatsManager != null && _currentStatsManager.baseData != null)
                ? _currentStatsManager.baseData.vehicleName
                : currentData.name; // fallback ke nama file asset

            _currentPreviewVehicle.name = vehicleName;
            if (vehicleNameText != null) vehicleNameText.text = vehicleName;

            // Simpan pilihan terakhir pakai nama dari BaseData
            PlayerPrefs.SetString("SelectedVehicle", vehicleName);
            PlayerPrefs.Save();
            // ─────────────────────────────────────────────────────────────

            if (_currentStatsManager != null)
            {
                _currentStatsManager.isPreviewMode = true;
                _currentStatsManager.hud = vehicleHUD;
                if (vehicleHUD != null) vehicleHUD.SetVehicle(_currentStatsManager);

                VehicleModuleListUI moduleList = FindObjectOfType<VehicleModuleListUI>();
                if (moduleList != null) moduleList.Initialize(_currentStatsManager);
            }

            if (_currentPreviewVehicle.TryGetComponent<VehicleController>(out var vc)) vc.enabled = false;
            AudioSource[] audioSources = _currentPreviewVehicle.GetComponentsInChildren<AudioSource>();
            foreach (var audio in audioSources) audio.enabled = false;

            _currentGridVisualizer = _currentPreviewVehicle.GetComponent<GridVisualizer>();
            if (_currentGridVisualizer == null && _currentStatsManager != null)
                _currentGridVisualizer = _currentPreviewVehicle.AddComponent<GridVisualizer>();

            // Load grid layout pakai nama dari BaseData sebagai key
            if (_currentStatsManager != null && moduleDatabase != null)
                GridSaveSystem.LoadGrid(vehicleName, _currentStatsManager, moduleDatabase);

            StartCoroutine(DisableAfterSpawn());
            CloseInventoryMode();
        }

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
    }
}