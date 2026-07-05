using UnityEngine;

public class VehicleHUDSpawner : MonoBehaviour
{
    [Header("=== HUD SETTINGS ===")]
    [Tooltip("Masukkan Prefab HUD Speedometer yang sudah kamu buat di sini.")]
    public GameObject vehicleHudPrefab;
    
    [Tooltip("Wadah (Container) di Canvas utama tempat HUD akan muncul. Bisa gunakan Container yang sama dengan senjata.")]
    public RectTransform hudContainer;

    private GameObject _spawnedHUD;

    private void Start()
    {
        // Cari container otomatis — spawning ditunda sampai VehicleEntry.EnterVehicle
        if (hudContainer == null)
        {
            GameObject containerObj = GameObject.Find("HUD_Container");
            if (containerObj != null) hudContainer = containerObj.GetComponent<RectTransform>();
        }
    }

    public void ClearHUD()
    {
        if (_spawnedHUD != null)
        {
            Destroy(_spawnedHUD);
            _spawnedHUD = null;
        }
    }

    public void ReinitializeHUD()
    {
        ClearHUD();

        if (vehicleHudPrefab == null || hudContainer == null)
            return;

        _spawnedHUD = Instantiate(vehicleHudPrefab, hudContainer);
        _spawnedHUD.name = $"HUD_Vehicle_{gameObject.name}";

        RectTransform rect = _spawnedHUD.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        VehicleUIManager uiManager = _spawnedHUD.GetComponent<VehicleUIManager>();
        VehicleController controller = GetComponent<VehicleController>();
        VehicleStatsManager stats = GetComponent<VehicleStatsManager>();

        if (uiManager != null && controller != null)
        {
            string carName = gameObject.name.Replace("(Clone)", "").Trim();
            uiManager.Initialize(controller, stats, carName);
        }

        if (stats != null)
        {
            VehicleModuleListUI moduleList = FindObjectOfType<VehicleModuleListUI>();
            if (moduleList != null)
                moduleList.Initialize(stats);
        }
    }

    private void OnDestroy()
    {
        // Jika mobil ini hancur (destroyed) atau ganti mobil, hapus juga HUD-nya dari layar
        ClearHUD();
    }
}
