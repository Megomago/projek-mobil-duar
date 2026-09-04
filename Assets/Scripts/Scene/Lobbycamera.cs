using UnityEngine;
using Cinemachine;

public class KlikKananKamera : MonoBehaviour
{
    private CinemachineFreeLook kameraGue;
    
    // Variabel buat ngitung waktu
    private float waktuNganggur = 0f;

    [Header("=== SETTING KAMERA UTAMA ===")]
    public float batasWaktuNganggur = 5f; 
    public float kecepatanCinematic = 10f; 

    [Header("=== INVENTORY SHIFT SETTINGS ===")]
    [Tooltip("Centang ini lewat script UI lu pas masuk mode inventory")]
    public bool isInventoryMode = false;
    
    [Tooltip("0.5f = Tengah. Di atas 0.5f (misal 0.7f) bakal geser mobil ke kiri.")]
    [Range(0.1f, 0.9f)]
    public float screenXInventory = 0.72f;
    
    [Tooltip("Seberapa cepat transisi geser kameranya")]
    public float kecepatanTransisi = 5f;

    [Header("=== SCROLL ZOOM (EDITOR SAJA) ===")]
    [Tooltip("Scroll mouse jadi zoom. Cuma aktif saat isInventoryMode = true.")]
    public bool enableEditorZoom = true;

    [Tooltip("Kecepatan zoom scroll.")]
    public float zoomSpeed = 2f;

    [Tooltip("Radius orbit terdekat (paling zoom-in).")]
    public float minZoomRadius = 2f;

    [Tooltip("Radius orbit terjauh (paling zoom-out).")]
    public float maxZoomRadius = 12f;

    [Header("=== SUMBER MODE EDITOR ===")]
    [Tooltip("Opsional: panel editor yang sama dipakai ModuleSelectionManager. Kalau diisi, mode editor ngikut panel ini ( anti-nyangkut kalau ada tombol X lain yang nutup panel tanpa panggil Toggle).")]
    public GameObject editModePanel;

    // Cache komponen Composer dari 3 Rig Cinemachine
    private CinemachineComposer[] rigComposers = new CinemachineComposer[3];

    // Snapshot radius orbit sebelum di-zoom di editor, buat fallback pas keluar mode
    private float[] _preZoomRadii;
    private float[] _defaultRadii;
    private bool _wasInventoryMode;

    void Start()
    {
        kameraGue = GetComponent<CinemachineFreeLook>();

        for (int i = 0; i < 3; i++)
        {
            var rig = kameraGue.GetRig(i);
            if (rig != null)
            {
                rigComposers[i] = rig.GetCinemachineComponent<CinemachineComposer>();
            }
        }

        // Default bawaan scene — fallback utama kalau zoom kebablasan
        if (kameraGue != null)
        {
            _defaultRadii = new float[kameraGue.m_Orbits.Length];
            for (int i = 0; i < _defaultRadii.Length; i++)
                _defaultRadii[i] = kameraGue.m_Orbits[i].m_Radius;
        }
    }

    void Update()
    {
        // Sumber kebenaran tunggal: panel editor. Kalau ada tombol X lain yang
        // nutup panel tanpa lewat LoadoutManager, flag tetap ngikut realita.
        if (editModePanel != null)
            isInventoryMode = editModePanel.activeInHierarchy;

        // 1. LOGIKA INPUT & CINEMATIC ROTATION (KODE LAMA LU)
        if (Input.anyKey || Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0 || Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            waktuNganggur = 0f;
        }

        if (Input.GetMouseButton(1)) 
        {
            kameraGue.m_XAxis.m_InputAxisName = "Mouse X";
            kameraGue.m_YAxis.m_InputAxisName = "Mouse Y";
        }
        else 
        {
            kameraGue.m_XAxis.m_InputAxisName = "";
            kameraGue.m_YAxis.m_InputAxisName = "";
            kameraGue.m_XAxis.m_InputAxisValue = 0;
            kameraGue.m_YAxis.m_InputAxisValue = 0;

            waktuNganggur += Time.deltaTime;

            if (waktuNganggur >= batasWaktuNganggur)
            {
                kameraGue.m_XAxis.Value += kecepatanCinematic * Time.deltaTime;
            }
        }

        float targetScreenX = isInventoryMode ? screenXInventory : 0.5f;

        // Terapkan transisi halus (Lerp) ke semua Rig Composer
        for (int i = 0; i < 3; i++)
        {
            if (rigComposers[i] != null)
            {
                rigComposers[i].m_ScreenX = Mathf.Lerp(
                    rigComposers[i].m_ScreenX, 
                    targetScreenX, 
                    Time.deltaTime * kecepatanTransisi
                );
            }
        }

        // 2. SCROLL ZOOM — cuma pas editor/inventory mode
        // Masuk editor: snapshot radius asli. Keluar editor: balikin (fallback).
        if (kameraGue != null && isInventoryMode != _wasInventoryMode)
        {
            if (isInventoryMode)
            {
                _preZoomRadii = new float[kameraGue.m_Orbits.Length];
                for (int i = 0; i < _preZoomRadii.Length; i++)
                    _preZoomRadii[i] = kameraGue.m_Orbits[i].m_Radius;
            }
            else if (_preZoomRadii != null)
            {
                int n = Mathf.Min(_preZoomRadii.Length, kameraGue.m_Orbits.Length);
                for (int i = 0; i < n; i++)
                {
                    var orbit = kameraGue.m_Orbits[i];
                    orbit.m_Radius = _preZoomRadii[i];
                    kameraGue.m_Orbits[i] = orbit;
                }
                _preZoomRadii = null;
            }
            _wasInventoryMode = isInventoryMode;
        }

        if (enableEditorZoom && isInventoryMode && kameraGue != null)
        {
            // Delta di-clamp biar 1 tick scroll gede (touchpad/mouse tertentu)
            // ga langsung menghantam ke min/max. Exponential = ga pernah minus.
            float scroll = Mathf.Clamp(Input.GetAxis("Mouse ScrollWheel"), -0.25f, 0.25f);
            if (Mathf.Abs(scroll) > Mathf.Epsilon)
            {
                float factor = Mathf.Exp(-scroll * zoomSpeed);
                for (int i = 0; i < kameraGue.m_Orbits.Length; i++)
                {
                    var orbit = kameraGue.m_Orbits[i];
                    orbit.m_Radius = Mathf.Clamp(
                        orbit.m_Radius * factor,
                        minZoomRadius,
                        maxZoomRadius
                    );
                    kameraGue.m_Orbits[i] = orbit;
                }
            }
        }

    }
    
    public void ToggleInventoryMode(bool aktif)
    {
        if (editModePanel != null)
            editModePanel.SetActive(aktif);
        else
            isInventoryMode = aktif;
    }

    [ContextMenu("Reset Zoom ke Default")]
    public void ResetZoom()
    {
        if (kameraGue == null || _defaultRadii == null) return;
        int n = Mathf.Min(_defaultRadii.Length, kameraGue.m_Orbits.Length);
        for (int i = 0; i < n; i++)
        {
            var orbit = kameraGue.m_Orbits[i];
            orbit.m_Radius = _defaultRadii[i];
            kameraGue.m_Orbits[i] = orbit;
        }
        _preZoomRadii = null;
    }
}