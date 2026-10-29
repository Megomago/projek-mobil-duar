using UnityEngine;
using C4;

public class PlayerInteraction : MonoBehaviour
{
    [Header("=== INTERACTION ===")]
    public float interactRange = 2.0f;
    public LayerMask interactLayer; // Set ke Layer 'Interactable'

    [Header("=== UI CROSSHAIR ===")]
    [Tooltip("Drag GameObject Image CrosshairDot di Canvas lu ke sini.")]
    public GameObject crosshairDot;

    [Header("=== ZOOM (OPSIONAL KLIK KANAN) ===")]
    public bool enableZoom = true;
    public float normalFOV = 60f;
    public float zoomFOV = 30f;
    public float zoomSpeed = 10f;

    private Camera _cam;
    private C4Planter _planter;

    private void Awake()
    {
        _cam = Camera.main;
        _planter = GetComponentInParent<C4Planter>();

        // Default matiin titik pas baru mulai
        SetCrosshair(false);
    }

    private void Update()
    {
        HandleZoom();

        // Kalau lagi megang C4 buat nempel, sembunyiin interaksi tombol
        if (_planter != null && _planter.IsEquipped)
        {
            SetCrosshair(false);
            return;
        }

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayer, QueryTriggerInteraction.Collide);

        if (hitSomething)
        {
            C4Clickable clickable = hit.collider.GetComponent<C4Clickable>();

            if (clickable != null)
            {
                // Lagi ngarah ke tombol -> TAMPILIN TITIK CROSSHAIR!
                SetCrosshair(true);

                // Klik Kiri (Tekan Tombol)
                if (Input.GetMouseButtonDown(0))
                {
                    clickable.AnimatePress(); // Animasi tombol mendem

                    C4PanelController panel = hit.collider.GetComponentInParent<C4PanelController>();
                    if (panel != null)
                    {
                        panel.Interact(clickable);
                    }
                }
                return;
            }
        }

        // Ga ngarah ke tombol apa-apa -> HILANGIN TITIK!
        SetCrosshair(false);
    }

    private void SetCrosshair(bool state)
    {
        if (crosshairDot != null && crosshairDot.activeSelf != state)
        {
            crosshairDot.SetActive(state);
        }
    }

    private void HandleZoom()
    {
        if (!enableZoom || _cam == null) return;

        // Tahan Klik Kanan buat zoom in kamera
        float targetFOV = Input.GetMouseButton(1) ? zoomFOV : normalFOV;
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
    }
}