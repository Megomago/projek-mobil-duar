using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ModuleXRayCamera (Simon Fix Edition)
/// URP Overlay Camera dengan sistem Dynamic Stack Attachment.
/// Pas OFF: kamera overlay dicabut dari cameraStack URP (Cost 0 ms murni).
/// Pas ON : kamera overlay dimasukkan ke cameraStack URP.
/// Execution order 9999: Sinkronisasi posisi paling akhir (Anti-Jitter / Anti-Lag).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(9999)]
public class ModuleXRayCamera : MonoBehaviour
{
    [Tooltip("Layer yang di-X-ray (biasanya PlacedModule).")]
    public LayerMask xrayLayers;

    [Tooltip("Panel UI edit mode/garasi (opsional). Otomatis nyala kalau panel ini aktif.")]
    public GameObject editModePanel;

    [Tooltip("True = paksa nyala terus.")]
    public bool alwaysOn = false;

    private Camera _baseCam;
    private UniversalAdditionalCameraData _baseData;
    private Camera _overlayCam;
    private GameObject _overlayGo;
    private bool _isCurrentlyStacked = false;

    private void OnEnable()
    {
        RebindBaseCamera();
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
        // Rebind jika kamera utama hilang / ganti scene
        if (_baseCam == null || !_baseCam.isActiveAndEnabled || _baseData == null)
        {
            RebindBaseCamera();
            if (_baseCam == null || _baseData == null)
            {
                SetOverlayEnabled(false);
                return;
            }
        }

        bool wantOn = alwaysOn || (editModePanel != null && editModePanel.activeInHierarchy);
        SetOverlayEnabled(wantOn);

        if (!wantOn || _overlayCam == null) return;

        // Sinkronisasi posisi & rotasi kamera overlay (Pasti mulus karena Order 9999)
        _overlayGo.transform.SetPositionAndRotation(_baseCam.transform.position, _baseCam.transform.rotation);
        _overlayCam.fieldOfView = _baseCam.fieldOfView;
        _overlayCam.nearClipPlane = _baseCam.nearClipPlane;
        _overlayCam.farClipPlane = _baseCam.farClipPlane;
        _overlayCam.cullingMask = xrayLayers;
    }

    private void EnsureOverlayBuilt()
    {
        if (_overlayCam != null) return;

        if (_baseCam == null)
        {
            _baseCam = Camera.main;
            if (_baseCam == null) return;
        }

        _baseData = _baseCam.GetComponent<UniversalAdditionalCameraData>();
        if (_baseData == null) return;

        _overlayGo = new GameObject("[ModuleXRay_Overlay_Camera]");
        _overlayGo.hideFlags = HideFlags.DontSave;
        _overlayGo.transform.SetParent(transform, false);

        _overlayCam = _overlayGo.AddComponent<Camera>();
        _overlayCam.clearFlags = CameraClearFlags.Nothing;
        _overlayCam.cullingMask = xrayLayers;
        _overlayCam.depth = _baseCam.depth + 1;

        var overlayData = _overlayGo.GetComponent<UniversalAdditionalCameraData>();
        if (overlayData == null)
            overlayData = _overlayGo.AddComponent<UniversalAdditionalCameraData>();

        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderShadows = false; // Modul X-Ray jangan render shadow 2x!
    }

    public void SetOverlayEnabled(bool on)
    {
        if (on)
        {
            EnsureOverlayBuilt();
            if (_overlayCam == null || _baseData == null) return;

            _overlayCam.enabled = true;

            // MASUKKAN KE STACK HANYA SAAT AKTIF
            if (!_isCurrentlyStacked && !_baseData.cameraStack.Contains(_overlayCam))
            {
                _baseData.cameraStack.Add(_overlayCam);
                _isCurrentlyStacked = true;
            }
        }
        else
        {
            // CABUT DARI STACK SAAT MATI (RAHASIA BALIK KE 150 FPS!)
            if (_baseData != null && _overlayCam != null)
            {
                if (_isCurrentlyStacked || _baseData.cameraStack.Contains(_overlayCam))
                {
                    _baseData.cameraStack.Remove(_overlayCam);
                    _isCurrentlyStacked = false;
                }
            }

            if (_overlayCam != null)
                _overlayCam.enabled = false;
        }
    }

    public void RebindBaseCamera()
    {
        // Lepas dari stack lama sebelum rebind kamera baru
        if (_baseData != null && _overlayCam != null)
            _baseData.cameraStack.Remove(_overlayCam);

        _isCurrentlyStacked = false;
        _baseCam = Camera.main;
        _baseData = _baseCam != null ? _baseCam.GetComponent<UniversalAdditionalCameraData>() : null;
    }

    private void Teardown()
    {
        SetOverlayEnabled(false);

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