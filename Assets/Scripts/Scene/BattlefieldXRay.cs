using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// X-ray modul di scene Battlefield (toggle tombol O).
/// Memakai ulang ModuleXRayCamera (overlay URP, tanpa ganti material).
/// Mati secara default; saat mati komponen X-ray di-disable total sehingga
/// cost per-frame NOL (overlay camera tidak ikut di-render URP).
/// Self-bootstrapping: otomatis aktif tanpa perlu di-drag ke scene.
/// </summary>
[DisallowMultipleComponent]
public class BattlefieldXRay : MonoBehaviour
{
    private const string TARGET_SCENE = "Battlefield";
    private const string MODULE_LAYER_NAME = "PlacedModule";

    [Header("=== SETTINGS ===")]
    [Tooltip("Tombol toggle X-ray")]
    public KeyCode toggleKey = KeyCode.O;
    [Tooltip("Nyala sejak awal masuk Battlefield (default mati demi performa)")]
    public bool startEnabled = false;

    private static BattlefieldXRay _instance;
    private ModuleXRayCamera _xray;
    private bool _enabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshForScene(SceneManager.GetActiveScene().name);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshForScene(scene.name);
    }

    private static void RefreshForScene(string sceneName)
    {
        if (sceneName == TARGET_SCENE)
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[BattlefieldXRay]");
                _instance = go.AddComponent<BattlefieldXRay>();
            }
        }
        else if (_instance != null)
        {
            Destroy(_instance.gameObject);
            _instance = null;
        }
    }

    private void Awake()
    {
        int moduleLayer = LayerMask.NameToLayer(MODULE_LAYER_NAME);
        if (moduleLayer == -1)
        {
            Debug.LogWarning("[BattlefieldXRay] Layer 'PlacedModule' tidak ditemukan — X-ray dimatikan.");
            enabled = false;
            return;
        }

        _xray = gameObject.AddComponent<ModuleXRayCamera>();
        _xray.xrayLayers = 1 << moduleLayer;
        _xray.editModePanel = null;

        SetXRay(startEnabled);
    }

    private void Update()
    {
        if (_xray == null) return;

        if (Input.GetKeyDown(toggleKey))
            SetXRay(!_enabled);
    }

   private void SetXRay(bool on)
    {
        _enabled = on;
        _xray.alwaysOn = on;
        _xray.SetOverlayEnabled(on); // <--- Tambahin baris ini!
        _xray.enabled = on;

    #if UNITY_EDITOR
        Debug.Log("[BattlefieldXRay] X-ray " + (on ? "ON" : "OFF"));
    #endif
    }
}   
