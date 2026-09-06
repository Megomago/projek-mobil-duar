using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// X-ray modul TANPA ganti material: kamera overlay URP yang render ulang
/// layer modul di atas segalanya. Modul di dalam bodi mobil tetap kelihatan.
/// Aktif cuma saat editor. Zero setup — dipasang otomatis oleh ModuleSelectionManager.
/// </summary>
[DisallowMultipleComponent]
public class ModuleXRayCamera : MonoBehaviour
{
    [Tooltip("Layer yang di-X-ray (biasanya PlacedModule).")]
    public LayerMask xrayLayers;

    [Tooltip("Aktif cuma saat panel ini terlihat.")]
    public GameObject editModePanel;

    [Tooltip("True = selalu X-ray walau panel tutup.")]
    public bool alwaysOn = false;

    private Camera _baseCam;
    private UniversalAdditionalCameraData _baseData;
    private Camera _overlayCam;
    private GameObject _overlayGo;

    private void OnEnable()
    {
        TryBuild();
    }

    private void OnDisable()
    {
        SetOverlayEnabled(false);
    }

    private void OnDestroy()
    {
        Teardown();
    }

    private void LateUpdate()
    {
        if (_overlayCam == null)
        {
            TryBuild();
            if (_overlayCam == null) return;
        }

        // Base camera bisa ganti (pindah scene) — re-resolve kalau mati/hilang
        if (_baseCam == null || !_baseCam.isActiveAndEnabled)
        {
            RebindBaseCamera();
            if (_baseCam == null)
            {
                SetOverlayEnabled(false);
                return;
            }
        }

        bool wantOn = alwaysOn || (editModePanel != null && editModePanel.activeInHierarchy);
        SetOverlayEnabled(wantOn);
        if (!wantOn) return;

        // Ikuti kamera utama persis (posisi, rotasi, lensa)
        _overlayGo.transform.SetPositionAndRotation(_baseCam.transform.position, _baseCam.transform.rotation);
        _overlayCam.fieldOfView = _baseCam.fieldOfView;
        _overlayCam.nearClipPlane = _baseCam.nearClipPlane;
        _overlayCam.farClipPlane = _baseCam.farClipPlane;
        _overlayCam.cullingMask = xrayLayers;
    }

    private void TryBuild()
    {
        if (_overlayCam != null) return;

        _baseCam = Camera.main;
        if (_baseCam == null) return;

        _baseData = _baseCam.GetComponent<UniversalAdditionalCameraData>();
        if (_baseData == null) return; // bukan URP — mundur teratur

        _overlayGo = new GameObject("ModuleXRay Overlay (Auto)");
        _overlayGo.hideFlags = HideFlags.DontSave; // runtime-only, jangan ikut save scene
        _overlayGo.transform.SetParent(transform, false);

        _overlayCam = _overlayGo.AddComponent<Camera>();
        _overlayCam.enabled = false;
        _overlayCam.clearFlags = CameraClearFlags.Nothing;
        _overlayCam.cullingMask = xrayLayers;

        var overlayData = _overlayGo.GetComponent<UniversalAdditionalCameraData>();
        if (overlayData == null)
            overlayData = _overlayGo.AddComponent<UniversalAdditionalCameraData>();
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderShadows = false; // modul jangan render shadow 2x

        if (!_baseData.cameraStack.Contains(_overlayCam))
            _baseData.cameraStack.Add(_overlayCam);
    }

    private void RebindBaseCamera()
    {
        // Lepas dari stack kamera lama
        if (_baseData != null && _overlayCam != null)
            _baseData.cameraStack.Remove(_overlayCam);

        _baseCam = Camera.main;
        _baseData = _baseCam != null ? _baseCam.GetComponent<UniversalAdditionalCameraData>() : null;

        if (_baseData != null && _overlayCam != null && !_baseData.cameraStack.Contains(_overlayCam))
            _baseData.cameraStack.Add(_overlayCam);
    }

    private void SetOverlayEnabled(bool on)
    {
        if (_overlayCam != null)
            _overlayCam.enabled = on;
    }

    private void Teardown()
    {
        if (_baseData != null && _overlayCam != null)
            _baseData.cameraStack.Remove(_overlayCam);

        _baseData = null;
        _baseCam = null;
        _overlayCam = null;

        if (_overlayGo != null)
        {
            if (Application.isPlaying)
                Destroy(_overlayGo);
            else
                DestroyImmediate(_overlayGo);
            _overlayGo = null;
        }
    }
}
