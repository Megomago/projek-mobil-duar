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
        // Jika belum diisi, kita coba cari container secara otomatis berdasarkan nama (opsional)
        if (hudContainer == null)
        {
            GameObject containerObj = GameObject.Find("HUD_Container");
            if (containerObj != null) hudContainer = containerObj.GetComponent<RectTransform>();
        }

        if (vehicleHudPrefab == null || hudContainer == null)
        {
            Debug.LogWarning("[VehicleHUDSpawner] Prefab atau Container belum diisi!");
            return;
        }

        // 1. Munculkan (Spawn) HUD ke dalam wadah di Canvas
        _spawnedHUD = Instantiate(vehicleHudPrefab, hudContainer);
        _spawnedHUD.name = $"HUD_Vehicle_{gameObject.name}";
        
        // 2. Rapikan ukurannya agar tidak rusak di dalam Layout Group
        RectTransform rect = _spawnedHUD.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        // 3. Sambungkan (Initialize) script UIManager dengan mobil ini
        VehicleUIManager uiManager = _spawnedHUD.GetComponent<VehicleUIManager>();
        VehicleController controller = GetComponent<VehicleController>();
        VehicleStatsManager stats = GetComponent<VehicleStatsManager>();
        
        if (uiManager != null && controller != null)
        {
            string carName = gameObject.name.Replace("(Clone)", "").Trim();
            uiManager.Initialize(controller, stats, carName);
        }

        // 4. Initialize Module List UI (panel terpisah di Canvas) dengan StatsManager kendaraan ini
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
        if (_spawnedHUD != null)
        {
            Destroy(_spawnedHUD);
        }
    }
}
