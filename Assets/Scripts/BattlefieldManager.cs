using UnityEngine;

namespace Weapons
{
    public class BattlefieldManager : MonoBehaviour
    {
        [Header("=== DATABASE ===")]
        public VehicleDatabase vehicleDatabase;
        public ModuleDatabase moduleDatabase;

        [Header("=== SPAWN SETTINGS ===")]
        [Tooltip("Titik awal mobil di-spawn saat masuk Battlefield")]
        public Transform playerSpawnPoint;

        [Header("=== UI SETTINGS ===")]
        [Tooltip("Wadah HUD di Canvas Battlefield. Akan di-assign otomatis ke kendaraan yang di-spawn.")]
        public RectTransform mainHUDContainer;

        private void Start()
        {
            if (vehicleDatabase == null || vehicleDatabase.allVehicles.Count == 0)
            {
                Debug.LogError("[BattlefieldManager] VehicleDatabase kosong atau belum dipasang!");
                return;
            }

            // Baca mobil pilihan pemain dari PlayerPrefs
            string savedVehicle = PlayerPrefs.GetString("SelectedVehicle", "");
            VehicleData selectedData = null;

            if (!string.IsNullOrEmpty(savedVehicle))
            {
                selectedData = vehicleDatabase.GetVehicleByName(savedVehicle);
            }

            // Jika tidak ada data yang tersimpan, gunakan mobil pertama di database sebagai default
            if (selectedData == null)
            {
                selectedData = vehicleDatabase.allVehicles[0];
            }

            // --- SPAWN MOBIL ---
            Vector3 spawnPos = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
            Quaternion spawnRot = playerSpawnPoint != null ? playerSpawnPoint.rotation : Quaternion.identity;

            GameObject spawnedVehicle = Instantiate(selectedData.vehiclePrefab, spawnPos, spawnRot);
            spawnedVehicle.name = selectedData.vehicleName;

            // --- LOAD GRID INVENTARIS DARI SAVE ---
            VehicleStatsManager statsManager = spawnedVehicle.GetComponent<VehicleStatsManager>();
            if (statsManager != null && moduleDatabase != null)
            {
                GridSaveSystem.LoadGrid(selectedData.vehicleName, statsManager, moduleDatabase);
            }
            
            // --- SETUP WEAPON TRIGGER ---
            var weaponTrigger = spawnedVehicle.GetComponent<VehicleGridWeaponTrigger>();
            if (weaponTrigger == null)
            {
                weaponTrigger = spawnedVehicle.AddComponent<VehicleGridWeaponTrigger>();
            }
            
            if (weaponTrigger != null)
            {
                if (mainHUDContainer != null)
                {
                    weaponTrigger.hudContainer = mainHUDContainer;
                }
                // Inisialisasi senjata setelah grid di-load
                weaponTrigger.InitializeWeapons();
            }

            // --- HUBUNGKAN KE KAMERA ---
            if (VehicleCamera.Instance != null)
            {
                VehicleCamera.Instance.target = spawnedVehicle.transform;
            }
            else
            {
                Debug.LogWarning("[BattlefieldManager] VehicleCamera.Instance tidak ditemukan di scene! Kamera tidak akan mengikuti mobil.");
            }
        }
    }
}
