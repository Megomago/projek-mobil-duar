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
    }

    public void ClearHUDs()
    {
        foreach (var hud in _spawnedHUDs)
        {
            if (hud != null) Destroy(hud);
        }
        _spawnedHUDs.Clear();
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
            }
        }
    }

    private void SpawnHUD(ModularWeapon weapon)
    {
        if (weapon.weaponData == null || weapon.weaponData.hudPrefab == null) return;
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

        // Jangan proses input tembak jika sedang drag modul di Lobby
        if (InventoryDragDropManager.Instance != null) return;

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
