using UnityEngine;
using System.Collections.Generic;
using Weapons;

public class VehicleGridWeaponTrigger : MonoBehaviour
{
    [Header("=== UI SETTINGS ===")]
    [Tooltip("Wadah (RectTransform) di Canvas layar utama tempat HUD senjata berkumpul. Pasang Vertical Layout Group di objek ini.")]
    public RectTransform hudContainer;

    [Header("=== INPUT SETTINGS ===")]
    [Tooltip("Aktifkan jika kendaraan ini dikendalikan oleh Player.")]
    public bool usePlayerInput = true;

    private VehicleStatsManager _statsManager;
    private List<ModularWeapon> _activeWeapons = new List<ModularWeapon>();
    private List<GameObject> _spawnedHUDs = new List<GameObject>();

    private void Awake()
    {
        _statsManager = GetComponent<VehicleStatsManager>();

        // Fallback: cari container otomatis kalau belum di-assign (mis. kendaraan di-scene manual)
        if (hudContainer == null)
        {
            GameObject containerObj = GameObject.Find("HUD_Container");
            if (containerObj == null) containerObj = GameObject.Find("HUD container");
            if (containerObj != null) hudContainer = containerObj.GetComponent<RectTransform>();
        }
    }

    public void ClearHUDs()
    {
        for (int i = _spawnedHUDs.Count - 1; i >= 0; i--)
        {
            GameObject hud = _spawnedHUDs[i];
            if (hud == null) continue;
            hud.SetActive(false); // Hilang dari layar seketika (Destroy dieksekusi end-of-frame)
            Destroy(hud);
        }
        _spawnedHUDs.Clear();
    }

    /// <summary>
    /// Sinkronkan ulang HUD senjata dengan modul terpasang (dipanggil dari VehicleGridSystem
    /// saat senjata di-install/uninstall). No-op kalau kendaraan belum dikendarai player.
    /// </summary>
    public void RebuildWeaponHUDs()
    {
        if (!usePlayerInput) return;
        InitializeWeapons();
    }

    private void OnDestroy()
    {
        // Kendaraan hancur/di-despawn → pastikan HUD senjatanya ikut bersih (anti numpuk)
        ClearHUDs();
    }

    public void InitializeWeapons()
    {
        ClearHUDs();
        _activeWeapons.Clear();

        if (_statsManager == null) return;

        // Cari senjata di dalam grid
        foreach (var mod in _statsManager.installedModules)
        {
            if (mod.moduleTemplate != null && mod.moduleTemplate.moduleType == ModuleType.Weapon && mod.spawnedPrefab != null)
            {
                ModularWeapon weapon = mod.spawnedPrefab.GetComponent<ModularWeapon>();
                if (weapon != null)
                {
                    _activeWeapons.Add(weapon);
                    SpawnHUD(weapon);
}
        // Warmup pool sekali saat enter vehicle — hilang spike tembakan pertama
        if (ObjectPool.Instance != null)
        {
            foreach (var w in _activeWeapons)
            {
                if (w.weaponData != null)
                {
                    ObjectPool.Instance.Warmup(w.weaponData.projectilePrefab, Mathf.Max(w.weaponData.pelletCount + 8, 16));
                    ObjectPool.Instance.Warmup(w.weaponData.muzzleFlashPrefab, 2);
                }
            }
        }
    }
        }
    }

    private void SpawnHUD(ModularWeapon weapon)
    {
        if (weapon.weaponData == null) return;
        if (weapon.weaponData.hudPrefab == null)
        {
            #if UNITY_EDITOR
            Debug.LogWarning($"[VehicleGridWeaponTrigger] '{weapon.weaponData.weaponName}' tidak punya hudPrefab di WeaponData — HUD senjata tidak muncul!");
            #endif
            return;
        }
        // Jangan spawn HUD jika container belum di-assign (misalnya di Lobby)
        if (hudContainer == null) return;

        // Pastikan hudContainer adalah objek aktif di scene (bukan persistent/prefab)
        if (!hudContainer.gameObject.activeInHierarchy) return;

        GameObject hudObj = Instantiate(weapon.weaponData.hudPrefab, hudContainer);
        _spawnedHUDs.Add(hudObj);
        hudObj.name = $"HUD_{weapon.weaponData.weaponName}";

        RectTransform hudRect = hudObj.GetComponent<RectTransform>();
        if (hudRect != null)
        {
            hudRect.localScale = Vector3.one;
            hudRect.localRotation = Quaternion.identity;
        }

        WeaponUIManager uiManager = hudObj.GetComponent<WeaponUIManager>();
        if (uiManager == null)
        {
            uiManager = hudObj.GetComponentInChildren<WeaponUIManager>();
        }

        if (uiManager != null)
        {
            uiManager.Initialize(weapon, weapon.weaponData.weaponName);
        }
    }

    private void Update()
    {
        if (!usePlayerInput || _activeWeapons.Count == 0) return;

        // Blokir input tembak HANYA saat benar-benar sedang drag modul di Lobby
        // (bukan selama manager-nya saja ada — dulu bikin senjata mati senyap
        // kalau scene battle punya objek InventoryDragDropManager).
        if (InventoryDragDropManager.Instance != null && InventoryDragDropManager.Instance.IsDragging) return;

        bool isFiring = Input.GetMouseButton(0);
        bool isReloading = Input.GetKeyDown(KeyCode.R);

        foreach (var weapon in _activeWeapons)
        {
            if (weapon != null)
            {
                if (isFiring) weapon.TryFire();
                else weapon.StopFiring();
                
                if (isReloading) weapon.StartReload();
            }
        }
    }
}
