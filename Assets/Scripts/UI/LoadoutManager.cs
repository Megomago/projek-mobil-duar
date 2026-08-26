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
        [Tooltip("Panel daftar modul (VehicleModuleListUI). Kalau kosong, dicari otomatis.")]
        public VehicleModuleListUI moduleListUI;

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

            // Cari mobil yang cocok dengan save terakhir (prioritas UID, fallback nama untuk save lama)
            string savedVehicleUid = PlayerPrefs.GetString("SelectedVehicleUID", "");
            string savedVehicle = PlayerPrefs.GetString("SelectedVehicle", "");
            VehicleData found = null;
            if (!string.IsNullOrEmpty(savedVehicleUid))
                found = vehicleDatabase.GetVehicleByUID(savedVehicleUid);
            if (found == null && !string.IsNullOrEmpty(savedVehicle))
                found = vehicleDatabase.GetVehicleByName(savedVehicle);
            if (found != null)
            {
                _currentVehicleIndex = vehicleDatabase.allVehicles.IndexOf(found);
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

            // ── Baca nama dari VehicleData ────────────
            string vehicleName = currentData.vehicleName;

            _currentPreviewVehicle.name = vehicleName;
            if (vehicleNameText != null) vehicleNameText.text = vehicleName;

            _currentStatsManager = _currentPreviewVehicle.GetComponent<VehicleStatsManager>();

            // Simpan pilihan terakhir pakai nama + UID (UID anti-bug saat rename mobil)
            PlayerPrefs.SetString("SelectedVehicle", vehicleName);
            PlayerPrefs.SetString("SelectedVehicleUID", currentData.UID);
            PlayerPrefs.Save();
            // ─────────────────────────────────────────

            if (_currentStatsManager != null)
            {
                _currentStatsManager.isPreviewMode = true;
                _currentStatsManager.hud = vehicleHUD;
                if (vehicleHUD != null) vehicleHUD.SetVehicle(_currentStatsManager);

                // Load grid async — spread across frames.
                // Capture statet manager secara lokal agar callback tidak menyasar
                // ke kendaraan lain kalau user ganti mobil di tengah loading.
                VehicleStatsManager targetStats = _currentStatsManager;
                if (moduleDatabase != null && targetStats.gridSystem != null)
                {
                    var gridSys = targetStats.gridSystem;
                    StartCoroutine(GridSaveSystem.LoadGridAsync(vehicleName, gridSys, moduleDatabase, (current, total) =>
                    {
                        if (current >= total)
                        {
                            // Mobil bisa saja sudah di-destroy (user ganti mobil cepat
                            // saat grid besar masih loading) — jangan sentuh objek mati.
                            if (targetStats == null) return;
                            targetStats.isGridFullyLoaded = true;
                            GetModuleList()?.Initialize(targetStats);
                        }
                    }));
                }
                else
                {
                    targetStats.isGridFullyLoaded = true;
                    GetModuleList()?.Initialize(targetStats);
                }
            }

            if (_currentPreviewVehicle.TryGetComponent<VehicleController>(out var vc)) vc.enabled = false;

            // Preview garasi TIDAK boleh bisa nembak — jangan bergantung pada
            // InventoryDragDropManager untuk memblokir input (gate-nya sekarang
            // hanya aktif saat drag). Matikan trigger + HUD senjata di sini.
            var previewTrigger = _currentPreviewVehicle.GetComponent<VehicleGridWeaponTrigger>();
            if (previewTrigger != null)
            {
                previewTrigger.usePlayerInput = false;
                previewTrigger.ClearHUDs();
            }
            AudioSource[] audioSources = _currentPreviewVehicle.GetComponentsInChildren<AudioSource>();
            foreach (var audio in audioSources) audio.enabled = false;

            Light[] lights = _currentPreviewVehicle.GetComponentsInChildren<Light>();
            foreach (var light in lights) light.enabled = false;

            _currentGridVisualizer = _currentPreviewVehicle.GetComponent<GridVisualizer>();
            if (_currentGridVisualizer == null && _currentStatsManager != null)
                _currentGridVisualizer = _currentPreviewVehicle.AddComponent<GridVisualizer>();

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
        // Anak-anak (modul dkk) langsung di-freeze
        if (rb != _currentPreviewVehicle.GetComponent<Rigidbody>())
        {
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    // Root kendaraan: tunggu jatuh & settle dulu (biar tidak melayang kalau
    // posisi spawn preview di atas tanah), baru di-freeze supaya tidak
    // terdorong player jalan.
    Rigidbody rootRb = _currentPreviewVehicle.GetComponent<Rigidbody>();
    if (rootRb != null)
    {
        float settleTimeout = 4f;
        float elapsed = 0f;
        while (elapsed < settleTimeout && !rootRb.IsSleeping())
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
            if (rootRb == null) yield break;
        }
        if (rootRb != null)
        {
            rootRb.isKinematic = true;
            rootRb.constraints = RigidbodyConstraints.FreezeAll;
        }
    }
}

    private VehicleModuleListUI GetModuleList()
    {
        if (moduleListUI != null) return moduleListUI;
        return FindObjectOfType<VehicleModuleListUI>();
    }

    /// <summary>
    /// Isi ulang amunisi kendaraan yang sedang dilihat di garasi (GRATIS untuk sekarang).
    /// Bisa di-bind ke tombol UI "Isi Ulang Amunisi".
    /// Nanti tinggal tambahkan biaya resource di sini tanpa mengubah alur lain (opsi refill bayar).
    /// </summary>
    [ContextMenu("Refill Ammo (Free)")]
    public void RefillAmmo()
    {
        if (_currentStatsManager == null)
        {
            Debug.LogWarning("[LoadoutManager] Tidak ada kendaraan preview — refill dibatalkan.");
            return;
        }

        _currentStatsManager.RefillAmmo();
        GetModuleList()?.Initialize(_currentStatsManager);

        #if UNITY_EDITOR
        Debug.Log($"[LoadoutManager] Amunisi '{_currentStatsManager.gameObject.name}' diisi ulang (gratis).");
        #endif
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

        private List<UIModuleItem> _catalogItems = new List<UIModuleItem>();

        private void PopulateInventoryCatalog()
        {
            if (gridContainer == null || uiModuleItemPrefab == null || moduleDatabase == null) return;

            // Bangun katalog SEKALI (jangan Instantiate ulang tiap buka inventory — skalabilitas
            // saat modul sudah puluhan/banyak)
            if (_catalogItems.Count == 0)
            {
                foreach (ModuleTemplate template in moduleDatabase.allModules)
                {
                    if (template == null) continue;

                    GameObject itemObj = Instantiate(uiModuleItemPrefab, gridContainer);
                    UIModuleItem uiItem = itemObj.GetComponent<UIModuleItem>();
                    if (uiItem != null)
                    {
                        // WAJIB inisialisasi saat dibangun — CurrentTemplate hanya ter-set lewat Initialize()
                        uiItem.Initialize(template, _currentStatsManager);
                        _catalogItems.Add(uiItem);
                    }
                }
            }

            // Refresh referensi stats manager tiap buka — bisa berganti saat ganti mobil
            foreach (var item in _catalogItems)
            {
                if (item != null)
                    item.Initialize(item.CurrentTemplate, _currentStatsManager);
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