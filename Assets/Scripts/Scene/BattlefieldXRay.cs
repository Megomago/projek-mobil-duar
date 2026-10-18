using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// X-ray modul di scene Battlefield (toggle tombol O).
/// Memakai ulang ModuleXRayCamera (overlay URP, tanpa ganti material).
/// HANYA modul kendaraan yang sedang dikendarai (VehicleEntry.ActiveVehicle) yang
/// di-X-ray: selagi ON, renderer modul kendaraan lain dipindah sementara dari layer
/// PlacedModule → Default supaya overlay camera (cullingMask PlacedModule) tidak
/// merender mereka. Raycast/damage tidak terpengaruh (hitMask peluru = semua layer).
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

    // Renderer modul kendaraan lain yang sedang dipindah ke Default selama X-ray ON
    private readonly List<GameObject> _swappedModuleObjects = new List<GameObject>();
    private VehicleEntry _focusedVehicle;

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

        // Pemain ganti kendaraan / keluar kendaraan saat X-ray ON → filter ulang
        if (_enabled && _focusedVehicle != VehicleEntry.ActiveVehicle)
            ApplyModuleFilter();
    }

    private void OnDisable()
    {
        RestoreOtherModules();
    }

    private void SetXRay(bool on)
    {
        _enabled = on;

        if (on) ApplyModuleFilter();
        else RestoreOtherModules();

        _xray.alwaysOn = on;
        _xray.SetOverlayEnabled(on);
        _xray.enabled = on;

    #if UNITY_EDITOR
        Debug.Log("[BattlefieldXRay] X-ray " + (on ? "ON" : "OFF"));
    #endif
    }

    /// <summary>
    /// Cuma modul kendaraan yang sedang dikendarai yang boleh tetap di layer
    /// PlacedModule (biar kelihatan tembus bodi oleh overlay X-ray). Renderer modul
    /// kendaraan lain dipindah sementara ke Default — render kamera utama tidak
    /// berubah, tapi mereka kebal dari overlay camera.
    /// </summary>
    private void ApplyModuleFilter()
    {
        RestoreOtherModules();

        _focusedVehicle = VehicleEntry.ActiveVehicle;
        Transform focus = _focusedVehicle != null ? _focusedVehicle.transform : null;

        int moduleLayer = LayerMask.NameToLayer(MODULE_LAYER_NAME);
        if (moduleLayer == -1) return;

        Renderer[] renderers = FindObjectsOfType<Renderer>();
        foreach (var r in renderers)
        {
            GameObject go = r.gameObject;
            if (go.layer != moduleLayer) continue;
            if (focus != null && go.transform.IsChildOf(focus)) continue;

            _swappedModuleObjects.Add(go);
            go.layer = 0; // Default
        }
    }

    private void RestoreOtherModules()
    {
        int moduleLayer = LayerMask.NameToLayer(MODULE_LAYER_NAME);
        if (moduleLayer != -1)
        {
            for (int i = 0; i < _swappedModuleObjects.Count; i++)
            {
                if (_swappedModuleObjects[i] != null)
                    _swappedModuleObjects[i].layer = moduleLayer;
            }
        }

        _swappedModuleObjects.Clear();
        _focusedVehicle = null;
    }
}
