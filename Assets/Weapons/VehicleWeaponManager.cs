using UnityEngine;

namespace Weapons
{
    [System.Serializable]
    public class WeaponSlot
    {
        [Tooltip("WeaponData senjata yang dipasang di slot ini. Kosongkan jika slot ini tidak diisi.")]
        public WeaponData weaponData;

        [Tooltip("Titik (Transform kosong) di mobil tempat senjata 3D akan ditempel.")]
        public Transform pivot;

        // Runtime references (otomatis diisi saat game berjalan)
        [HideInInspector] public ModularWeapon spawnedWeapon;
        [HideInInspector] public GameObject spawnedHUD;
    }

    public class VehicleWeaponManager : MonoBehaviour
    {
        [Header("=== WEAPON SLOTS (PENGATURAN SENJATA MOBIL) ===")]
        [Tooltip("Tambah slot sesuai jumlah titik senjata di mobil ini.")]
        public WeaponSlot[] weaponSlots;

        [Header("=== UI SETTINGS ===")]
        [Tooltip("Wadah (RectTransform) di Canvas layar utama tempat HUD senjata berkumpul. Pasang Vertical Layout Group di objek ini.")]
        public RectTransform hudContainer;

        [Header("=== INPUT SETTINGS ===")]
        [Tooltip("Aktifkan jika kendaraan ini dikendalikan oleh Player.")]
        public bool usePlayerInput = true;

        private void Start()
        {
            InitializeAllSlots();
        }

        private void Update()
        {
            if (!usePlayerInput || weaponSlots == null) return;

            bool isFiring = Input.GetMouseButton(0);
            bool isReloading = Input.GetKeyDown(KeyCode.R);

            foreach (var slot in weaponSlots)
            {
                if (slot.spawnedWeapon != null)
                {
                    if (isFiring) slot.spawnedWeapon.TryFire();
                    else slot.spawnedWeapon.StopFiring();
                    
                    if (isReloading) slot.spawnedWeapon.StartReload();
                }
            }
        }

        private void InitializeAllSlots()
        {
            if (weaponSlots == null || weaponSlots.Length == 0) return;

            foreach (var slot in weaponSlots)
            {
                // Jika slot kosong (tidak ada WeaponData), skip
                if (slot.weaponData == null) continue;

                // --- 1. SPAWN SENJATA 3D ---
                SpawnWeapon3D(slot);

                // --- 2. SPAWN HUD ---
                SpawnHUD(slot);
            }
        }

        private void SpawnWeapon3D(WeaponSlot slot)
        {
            if (slot.weaponData.weapon3DPrefab == null)
            {
                Debug.LogWarning($"[VehicleWeaponManager] WeaponData '{slot.weaponData.weaponName}' tidak punya weapon3DPrefab!", this);
                return;
            }

            if (slot.pivot == null)
            {
                Debug.LogWarning($"[VehicleWeaponManager] Pivot untuk '{slot.weaponData.weaponName}' belum di-assign!", this);
                return;
            }

            // Spawn prefab 3D di posisi dan rotasi pivot
            GameObject spawnedObj = Instantiate(slot.weaponData.weapon3DPrefab, slot.pivot);
            spawnedObj.transform.localPosition = Vector3.zero;
            spawnedObj.transform.localRotation = Quaternion.identity;
            spawnedObj.name = slot.weaponData.weaponName;

            // Ambil & simpan referensi ModularWeapon
            ModularWeapon modularWeapon = spawnedObj.GetComponent<ModularWeapon>();
            if (modularWeapon != null)
            {
                modularWeapon.weaponData = slot.weaponData;
                slot.spawnedWeapon = modularWeapon;
            }
            else
            {
                Debug.LogWarning($"[VehicleWeaponManager] Prefab 3D '{slot.weaponData.weaponName}' tidak punya script ModularWeapon!", this);
            }
        }

        private void SpawnHUD(WeaponSlot slot)
        {
            if (slot.weaponData.hudPrefab == null) return;
            if (slot.spawnedWeapon == null) return;

            if (hudContainer == null)
            {
                Debug.LogWarning("[VehicleWeaponManager] hudContainer belum di-assign! HUD tidak bisa di-spawn.", this);
                return;
            }

            // Spawn prefab HUD langsung ke dalam wadah di Canvas layar
            GameObject hudObj = Instantiate(slot.weaponData.hudPrefab, hudContainer);
            slot.spawnedHUD = hudObj;
            hudObj.name = $"HUD_{slot.weaponData.weaponName}";

            // Reset transform agar rapi di dalam layout
            RectTransform hudRect = hudObj.GetComponent<RectTransform>();
            if (hudRect != null)
            {
                hudRect.localScale = Vector3.one;
                hudRect.localRotation = Quaternion.identity;
            }

            // Sambungkan HUD ke senjata 3D-nya
            WeaponUIManager uiManager = hudObj.GetComponent<WeaponUIManager>();
            if (uiManager == null)
            {
                uiManager = hudObj.GetComponentInChildren<WeaponUIManager>();
            }

            if (uiManager != null)
            {
                uiManager.Initialize(slot.spawnedWeapon, slot.weaponData.weaponName);
            }
        }
    }
}
