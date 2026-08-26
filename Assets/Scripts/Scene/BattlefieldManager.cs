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
        public Transform vehicleSpawnPoint;

        [Tooltip("Prefab player (capsule + PlayerController). Kosongin kalo mau spawn default capsule.")]
        public GameObject playerPrefab;

        [Tooltip("Titik spawn player (pake vehicleSpawnPoint kalo kosong)")]
        public Transform playerSpawnPoint;

        [Header("=== UI SETTINGS ===")]
        [Tooltip("Wadah HUD di Canvas Battlefield")]
        public RectTransform mainHUDContainer;

        private void Start()
        {
            if (vehicleDatabase == null || vehicleDatabase.allVehicles.Count == 0)
            {
                Debug.LogError("[BattlefieldManager] VehicleDatabase kosong atau belum dipasang!");
                return;
            }

            // Sama seperti LoadoutManager: prioritas UID (anti-bug saat rename mobil),
            // fallback nama untuk save lama, terakhir fallback ke mobil pertama.
            string savedVehicleUid = PlayerPrefs.GetString("SelectedVehicleUID", "");
            string savedVehicle = PlayerPrefs.GetString("SelectedVehicle", "");
            VehicleData selectedData = null;

            if (!string.IsNullOrEmpty(savedVehicleUid))
                selectedData = vehicleDatabase.GetVehicleByUID(savedVehicleUid);
            if (selectedData == null && !string.IsNullOrEmpty(savedVehicle))
                selectedData = vehicleDatabase.GetVehicleByName(savedVehicle);

            if (selectedData == null)
            {
                selectedData = vehicleDatabase.allVehicles[0];
            }

            // --- SPAWN PLAYER ---
            SpawnPlayer();

            // --- SPAWN MOBIL ---
            Vector3 spawnPos = vehicleSpawnPoint != null ? vehicleSpawnPoint.position : Vector3.zero;
            Quaternion spawnRot = vehicleSpawnPoint != null ? vehicleSpawnPoint.rotation : Quaternion.identity;

            GameObject spawnedVehicle = Instantiate(selectedData.vehiclePrefab, spawnPos, spawnRot);

            string vehicleName = selectedData.vehicleName;
            spawnedVehicle.name = vehicleName;

            VehicleStatsManager statsManager = spawnedVehicle.GetComponent<VehicleStatsManager>();

            var vehicleGridSystem = statsManager != null ? statsManager.gridSystem : null;

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
            }

            VehicleController vc = spawnedVehicle.GetComponent<VehicleController>();
            if (vc != null)
            {
                // Kunci input sekarang, TAPI jangan freeze rigidbody dulu —
                // biarkan mobil jatuh & settle ke tanah, baru di-freeze.
                // (Kalau langsung di-freeze, mobil melayang kalau spawn point di atas tanah.)
                vc.SetMovementLocked(true, freezeRigidbody: false);
                StartCoroutine(FreezeVehicleAfterSettle(spawnedVehicle));
            }

            if (weaponTrigger != null)
                weaponTrigger.usePlayerInput = false;

            #if UNITY_EDITOR
            Debug.Log("[BattlefieldManager] Vehicle spawned: " + vehicleName + " | Player is on foot. Walk to car and press E to enter.");
            #endif

            // Load grid modules async — spread across frames to avoid freeze
            if (vehicleGridSystem != null && moduleDatabase != null)
                StartCoroutine(GridSaveSystem.LoadGridAsync(vehicleName, vehicleGridSystem, moduleDatabase, (current, total) =>
                {
                    if (current >= total)
                    {
                        // Grid selesai di-load → ammo dari save boleh di-persist lagi
                        if (statsManager != null)
                            statsManager.isGridFullyLoaded = true;

                        // Matikan turret lagi karena module baru di-install dengan turret aktif
                        var turrets = spawnedVehicle.GetComponentsInChildren<Weapons.ManualTurretController>(true);
                        foreach (var t in turrets)
                            if (t != null) t.enabled = false;
                    }
                }));
        }

        private System.Collections.IEnumerator FreezeVehicleAfterSettle(GameObject vehicle)
        {
            if (vehicle == null) yield break;

            Rigidbody rb = vehicle.GetComponent<Rigidbody>();
            var vc = vehicle.GetComponent<VehicleController>();

            // Tunggu mobil jatuh & settle (suspension) sampai rigidbody tidur, maks 4 detik.
            float timeout = 4f;
            float elapsed = 0f;
            while (rb != null && elapsed < timeout && !rb.IsSleeping())
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            // Player sudah masuk mobil duluan → jangan di-freeze ulang (anti terkunci).
            if (VehicleEntry.ActiveVehicle != null && VehicleEntry.ActiveVehicle.gameObject == vehicle)
                yield break;

            if (vc != null)
                vc.SetMovementLocked(true);
        }

        private void SpawnPlayer()
        {
            Transform spawnT = playerSpawnPoint != null ? playerSpawnPoint : vehicleSpawnPoint;
            Vector3 pos = spawnT != null ? spawnT.position : Vector3.zero;
            Quaternion rot = spawnT != null ? spawnT.rotation : Quaternion.identity;

            // Offset player spawn a few meters behind/left of vehicle spawn
            Vector3 offset = (spawnT ? -spawnT.forward * 3f - spawnT.right * 1.5f : new Vector3(-3f, 0f, -3f));
            pos += offset;

            if (playerPrefab != null)
            {
                Instantiate(playerPrefab, pos, rot);
            }
            else
            {
                GameObject playerObj = new GameObject("PlayerCapsule");
                playerObj.transform.position = pos;
                playerObj.transform.rotation = rot;
                playerObj.AddComponent<PlayerController>();
            }

            if (VehicleCamera.Instance != null)
            {
                VehicleCamera.Instance.SetTarget(null);
            }
            else
            {
                Debug.LogWarning("[BattlefieldManager] VehicleCamera.Instance tidak ditemukan!");
            }
        }
    }
}
