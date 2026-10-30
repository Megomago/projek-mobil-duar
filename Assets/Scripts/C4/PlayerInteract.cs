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

    [Header("=== C4 PICKUP ===")]
    [Tooltip("Tahan klik kiri selama ini pas crosshair di body C4 buat angkat C4-nya lagi.")]
    [Min(0.05f)]
    public float c4PickupHoldDuration = 0.35f;

    private Camera _cam;
    private C4Planter _planter;
    private C4Outline _currentOutline;
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[32];

    private C4Explosive _pickupTarget;
    private float _pickupHeldTime;
    private bool _blockPickupUntilRelease;

    private void Awake()
    {
        _cam = Camera.main;
        _planter = GetComponentInParent<C4Planter>();

        if (interactLayer.value == 0)
        {
            int layer = LayerMask.NameToLayer("Interactable");
            interactLayer = layer >= 0 ? (1 << layer) : (1 << 8);
        }

        if (_planter != null)
            _planter.OnC4Placed += HandleC4Placed;

        // Default matiin titik pas baru mulai
        SetCrosshair(false);
    }

    private void Update()
    {
        HandleZoom();

        // Klik yang dipakai buat masang C4 jangan langsung dipakai buat pickup.
        if (_blockPickupUntilRelease && !Input.GetMouseButton(0))
            _blockPickupUntilRelease = false;

        // Kalau lagi megang C4 buat nempel, sembunyiin interaksi tombol
        if (_planter != null && _planter.IsEquipped)
        {
            ClearOutline();
            ResetC4Pickup();
            SetCrosshair(false);
            return;
        }

        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) return;
        }

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            _hitBuffer,
            interactRange,
            interactLayer,
            QueryTriggerInteraction.Collide);

        if (hitCount > 0)
        {
            C4Clickable nearestClickable = null;
            float nearestClickableDistance = float.PositiveInfinity;

            C4Outline nearestOutline = null;
            C4Explosive nearestExplosive = null;
            float nearestBodyDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _hitBuffer[i];
                if (candidate.collider == null) continue;

                C4Clickable clickable = candidate.collider.GetComponentInParent<C4Clickable>();
                if (clickable != null)
                {
                    if (candidate.distance < nearestClickableDistance)
                    {
                        nearestClickableDistance = candidate.distance;
                        nearestClickable = clickable;
                    }
                }
                else
                {
                    if (candidate.distance < nearestBodyDistance)
                    {
                        nearestBodyDistance = candidate.distance;
                        C4Outline outline = candidate.collider.GetComponentInParent<C4Outline>();
                        if (outline != null) nearestOutline = outline;

                        C4Explosive explosive = candidate.collider.GetComponentInParent<C4Explosive>();
                        if (explosive != null) nearestExplosive = explosive;
                    }
                }
            }

            // PRIORITAS 1: Jika mengarah ke Tombol atau Knob keypad C4
            if (nearestClickable != null)
            {
                ResetC4Pickup();
                SetCrosshair(true);

                C4Outline outline = nearestClickable.GetComponentInParent<C4Outline>();
                if (outline == null && nearestOutline != null)
                    outline = nearestOutline;

                if (outline != null)
                {
                    HoverOutline(outline, nearestClickable);
                }

                // Klik Kiri (Tekan Tombol)
                if (Input.GetMouseButtonDown(0))
                {
                    nearestClickable.AnimatePress(); // Animasi tombol mendem

                    C4PanelController panel = nearestClickable.GetComponentInParent<C4PanelController>();
                    if (panel != null)
                    {
                        panel.Interact(nearestClickable);
                    }
                }
                return;
            }

            // PRIORITAS 2: Jika mengarah ke bodi C4 (bukan tombol)
            if (nearestOutline != null || nearestExplosive != null)
            {
                SetCrosshair(false);

                if (nearestOutline != null)
                {
                    HoverOutline(nearestOutline, null);
                }
                else if (nearestExplosive != null)
                {
                    C4Outline expOutline = nearestExplosive.GetComponent<C4Outline>();
                    if (expOutline != null)
                        HoverOutline(expOutline, null);
                }

                HandleC4PickupHold(nearestExplosive);
                return;
            }
        }

        // PRIORITAS 3: Tidak mengenai C4 apapun
        ResetC4Pickup();
        ClearOutline();
        SetCrosshair(false);
    }

    private void HoverOutline(C4Outline outline, C4Clickable part)
    {
        if (_currentOutline != outline)
        {
            ClearOutline();
            _currentOutline = outline;
        }

        if (outline == null) return;

        if (part != null)
            outline.HoverPart(part);
        else
            outline.HoverWhole();
    }

    private void ClearOutline()
    {
        if (_currentOutline != null)
            _currentOutline.Clear();

        _currentOutline = null;
    }

    private void HandleC4PickupHold(C4Explosive explosive)
    {
        if (explosive == null || _planter == null || _blockPickupUntilRelease)
        {
            ResetC4Pickup();
            return;
        }

        // C4 yang ditempel player lain jangan diambil.
        if (explosive.planter != null && explosive.planter != _planter.transform)
        {
            ResetC4Pickup();
            return;
        }

        if (_pickupTarget != explosive)
        {
            _pickupTarget = explosive;
            _pickupHeldTime = 0f;
        }

        if (!Input.GetMouseButton(0))
        {
            _pickupHeldTime = 0f;
            return;
        }

        _pickupHeldTime += Time.deltaTime;
        if (_pickupHeldTime < Mathf.Max(0.05f, c4PickupHoldDuration))
            return;

        PickUpC4(explosive);
    }

    private void PickUpC4(C4Explosive explosive)
    {
        if (explosive == null || _planter == null) return;

        string preservedCode = null;
        C4PanelController panel = explosive.GetComponent<C4PanelController>();
        if (panel != null)
        {
            preservedCode = panel.GeneratedCode;
        }

        ClearOutline();
        ResetC4Pickup();
        SetCrosshair(false);

        // Equip dulu, baru hapus C4 di dunia. Kalau equip gagal, C4-nya jangan ilang.
        _planter.EquipAfterPickup(preservedCode);
        if (!_planter.IsEquipped) return;

        Destroy(explosive.gameObject);
    }

    private void HandleC4Placed(C4Explosive explosive)
    {
        // Jangan pickup C4 yang baru aja ditempel selama kliknya masih ditahan.
        _blockPickupUntilRelease = true;
        ResetC4Pickup();
    }

    private void ResetC4Pickup()
    {
        _pickupTarget = null;
        _pickupHeldTime = 0f;
    }

    private void SetCrosshair(bool state)
    {
        if (crosshairDot != null && crosshairDot.activeSelf != state)
        {
            crosshairDot.SetActive(state);
        }
    }

    private void OnDisable()
    {
        ResetC4Pickup();
        ClearOutline();
        SetCrosshair(false);
    }

    private void OnDestroy()
    {
        if (_planter != null)
            _planter.OnC4Placed -= HandleC4Placed;
    }

    private void HandleZoom()
    {
        if (!enableZoom || _cam == null) return;

        // Tahan Klik Kanan buat zoom in kamera
        float targetFOV = Input.GetMouseButton(1) ? zoomFOV : normalFOV;
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
    }
}
