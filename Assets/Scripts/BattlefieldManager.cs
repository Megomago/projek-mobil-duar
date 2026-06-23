using UnityEngine;

namespace Weapons
{
    public class BattlefieldManager : MonoBehaviour
    {
        [Header("=== DATABASE ===")]
        public VehicleDatabase vehicleDatabase;

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
            spawnedVehicle.name = selectedData.vehicleName; // Penting: agar VehicleWeaponManager baca nama ini
            
            // Set nama eksplisit di VehicleWeaponManager sebelum Start()-nya dipanggil (meski di Awake sudah ter-Instantiate, Start belum)
            // Atau cukup biarkan namanya diganti di atas, VehicleWeaponManager akan baca dari gameObject.name.
            var weaponManager = spawnedVehicle.GetComponent<VehicleWeaponManager>();
            if (weaponManager != null)
            {
                weaponManager.currentVehicleName = selectedData.vehicleName;
                
                // Assign HUD container dari BattlefieldManager ke mobil yang baru di-spawn
                if (mainHUDContainer != null)
                {
                    weaponManager.hudContainer = mainHUDContainer;
                }
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
