using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ModuleSelectionManager : MonoBehaviour
{
    public static ModuleSelectionManager Instance;

    [Header("Bracket")]
    [Tooltip("Warna garis bracket seleksi (RTS-style, cuma di ujung/bounds).")]
    public Color bracketColor = Color.yellow;

    [Tooltip("Jarak padding bracket dari bounds modul (meter).")]
    public float bracketPadding = 0.05f;

    [Tooltip("Tebal garis bracket.")]
    public float bracketWidth = 0.02f;
    
    [Tooltip("Layer khusus untuk modul yang terpasang biar raycast ga bocor.")]
    public LayerMask moduleLayer;

    [Tooltip("Panel inventory/edit mode. Selection cuma aktif kalo panel ini terbuka.")]
    public GameObject editModePanel;

    [Header("Hover & Stats UI")]
    [Tooltip("Tooltip nama saat hover. Kosongkan = dibuat otomatis.")]
    public bool showHoverTooltip = true;
    [Tooltip("Popup stat saat modul di-klik. Kosongkan = dibuat otomatis.")]
    public bool showStatsPopup = true;
    [Tooltip("Offset tooltip dari kursor (px). Default: kanan atas.")]
    public Vector2 tooltipOffset = new Vector2(18f, 16f);

    private PlacedModule _selectedModule;
    private VehicleStatsManager _currentVehicleManager;
    private Camera _mainCam;

    // Bracket reuse: 1 root + 12 LineRenderer (satu per rusuk box), dibuat sekali di Awake
    private GameObject _bracketRoot;
    private LineRenderer[] _bracketEdges;
    private Material _bracketMaterial;
    private Renderer[] _cachedRenderers;

    // 12 rusuk unit cube: pasangan indeks ke 8 titik sudut
    private static readonly Vector3[] CubeCorners = new Vector3[]
    {
        new Vector3(-0.5f, -0.5f, -0.5f), // 0
        new Vector3( 0.5f, -0.5f, -0.5f), // 1
        new Vector3( 0.5f, -0.5f,  0.5f), // 2
        new Vector3(-0.5f, -0.5f,  0.5f), // 3
        new Vector3(-0.5f,  0.5f, -0.5f), // 4
        new Vector3( 0.5f,  0.5f, -0.5f), // 5
        new Vector3( 0.5f,  0.5f,  0.5f), // 6
        new Vector3(-0.5f,  0.5f,  0.5f), // 7
    };
    private static readonly int[] CubeEdgePairs = new int[]
    {
        0,1, 1,2, 2,3, 3,0, // bawah
        4,5, 5,6, 6,7, 7,4, // atas
        0,4, 1,5, 2,6, 3,7, // tiang
    };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _mainCam = Camera.main;
        BuildBracket();

        // X-ray modul di dalam bodi: tanpa ini modul internal ketutup mesh mobil.
        // Zero scene setup — ikut hidup/mati bareng panel editor.
        var xray = GetComponent<ModuleXRayCamera>();
        if (xray == null) xray = gameObject.AddComponent<ModuleXRayCamera>();
        xray.xrayLayers = moduleLayer;
        xray.editModePanel = editModePanel;
    }

    private void Update()
    {   
        // Cegah seleksi pas lagi nge-drag item di UI.
        // Malah tampilkan ALASAN penolakan kalau hologram merah (biar ga nebak).
        if (InventoryDragDropManager.Instance != null && InventoryDragDropManager.Instance.IsDragging)
        {
            var dm = InventoryDragDropManager.Instance;
            if (!dm.CanPlace && !string.IsNullOrEmpty(dm.RejectReason))
                ShowDragReason(dm.RejectReason);
            else
                HideDragReason();
            return;
        }

        // Kalau edit mode ditutup, deselect otomatis
        if (editModePanel == null || !editModePanel.activeSelf)
        {
            if (_selectedModule != null) DeselectModule();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            // Jangan deteksi klik kalo kursor lagi di atas UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            HandleSelection();
        }

        // Hover tooltip: 1 raycast/frame, cuma di editor & tidak di atas UI
        UpdateHover();

        if (Input.GetKeyDown(KeyCode.X) && _selectedModule != null)
        {
            DeleteSelectedModule();
        }

        // G = angkat/pindah modul terpilih. Klik kiri = taruh, klik kanan = batal.
        if (Input.GetKeyDown(KeyCode.G) && _selectedModule != null)
        {
            StartMoveSelected();
        }

        // R = putar modul terpilih 90° di tempat (tanpa drag).
        // Saat drag, R diurus DragDropManager (branch drag return duluan di atas).
        if (Input.GetKeyDown(KeyCode.R) && _selectedModule != null)
        {
            RotateSelected();
        }

        // Ikuti modul terpilih (kendaraan bisa gerak / modul bisa berubah)
        if (_selectedModule != null)
        {
            if (_selectedModule.spawnedPrefab == null)
                DeselectModule();
            else
                UpdateBracketTransform();
        }
    }

    private void BuildBracket()
    {
        _bracketRoot = new GameObject("SelectionBracket");
        _bracketRoot.hideFlags = HideFlags.DontSave; // runtime-only, jangan ikut save scene
        _bracketRoot.transform.SetParent(transform);
        _bracketRoot.SetActive(false);

        // Bracket ikut layer X-ray biar tidak ketutup bodi mobil saat modul di dalam
        int xrayLayer = LayerMask.NameToLayer("PlacedModule");
        if (xrayLayer != -1)
            SetLayerRecursively(_bracketRoot, xrayLayer);

        // Shader khusus ZTest-Always: bracket selalu menang lawan mesh apapun.
        // Lapis 2 = GridOverlay (terbukti jalan di proyek ini, juga ZTest Always).
        Shader s = Shader.Find("Custom/XRayLine");
        if (s == null)
        {
            Debug.LogWarning("[ModuleSelectionManager] Custom/XRayLine tidak ketemu, pakai GridOverlay.");
            s = Shader.Find("Custom/GridOverlay");
        }
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");
        _bracketMaterial = new Material(s);
        _bracketMaterial.color = Color.white; // tint via LR start/end color, bukan material

        _bracketEdges = new LineRenderer[12];
        for (int i = 0; i < 12; i++)
        {
            var go = new GameObject($"Edge{i}");
            go.transform.SetParent(_bracketRoot.transform);
            // Layer TIDAK nurun dari parent — set eksplisit biar ikut X-ray overlay
            if (xrayLayer != -1)
                go.layer = xrayLayer;
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = false;
            lr.startWidth = bracketWidth;
            lr.endWidth = bracketWidth;
            lr.material = _bracketMaterial;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            _bracketEdges[i] = lr;
        }
        ApplyBracketStyle();
    }

    private void ApplyBracketStyle()
    {
        if (_bracketEdges != null)
        {
            foreach (var lr in _bracketEdges)
            {
                if (lr == null) continue;
                lr.startWidth = bracketWidth;
                lr.endWidth = bracketWidth;
                lr.startColor = bracketColor;
                lr.endColor = bracketColor;
            }
        }
    }

    private Bounds GetModuleBounds(GameObject target)
    {
        // Pakai cache dari SelectModule (0 alloc per frame); fallback kalau kosong
        Renderer[] renderers = _cachedRenderers;
        if (renderers == null || renderers.Length == 0)
            renderers = target.GetComponentsInChildren<Renderer>(includeInactive: false);
        Bounds b = new Bounds(target.transform.position, Vector3.zero);
        bool hasAny = false;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!hasAny) { b = r.bounds; hasAny = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private void UpdateBracketTransform()
    {
        if (_bracketRoot == null || _selectedModule?.spawnedPrefab == null) return;
        Bounds b = GetModuleBounds(_selectedModule.spawnedPrefab);
        b.Expand(bracketPadding * 2f);

        // Root di center bounds (tanpa rotasi, bounds world axis-aligned)
        _bracketRoot.transform.position = b.center;
        _bracketRoot.transform.rotation = Quaternion.identity;

        Vector3 size = b.size;
        for (int i = 0; i < 12; i++)
        {
            var lr = _bracketEdges[i];
            if (lr == null) continue;
            Vector3 a = CubeCorners[CubeEdgePairs[i * 2]];
            Vector3 c = CubeCorners[CubeEdgePairs[i * 2 + 1]];
            lr.SetPosition(0, new Vector3(a.x * size.x, a.y * size.y, a.z * size.z));
            lr.SetPosition(1, new Vector3(c.x * size.x, c.y * size.y, c.z * size.z));
        }
    }

    private void HandleSelection()
    {
        if (TryPickModule(out PlacedModule clickedModule))
        {
            if (clickedModule != null)
            {
                if (_selectedModule == clickedModule)
                {
                    DeselectModule();
                }
                else
                {
                    SelectModule(clickedModule);
                }
            }
            else
            {
                DeselectModule();
            }
        }
        else
        {
            DeselectModule();
        }
    }

    /// <summary>Raycast modul di bawah kursor. True = kena collider layer modul (modul bisa null).</summary>
    public bool TryPickModule(out PlacedModule module)
    {
        module = null;
        if (_mainCam == null) return false;
        Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, moduleLayer)) return false;
        module = FindModuleByCollider(hit.collider);
        return true;
    }

    // === HOVER TOOLTIP + STATS POPUP (auto-build, 0 setup) ===
    private GameObject _tooltipGo;
    private RectTransform _tooltipRect;
    private TextMeshProUGUI _tooltipText;
    private PlacedModule _hoveredModule;
    private bool _tooltipLocked; // true = lagi tampil reason (jangan di-overwrite hover)
    private string _lastReason = "";
    private float _reasonUntil = 0f;

    private GameObject _statsGo;
    private TextMeshProUGUI _statsTitle;
    private TextMeshProUGUI _statsBody;
    private static readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(1024);

    private void UpdateHover()
    {
        if (!showHoverTooltip || _mainCam == null)
        {
            SetTooltip(null);
            return;
        }
        // Flash alasan (misal rotate gagal): menang 1.5 detik walau hover berubah
        if (Time.time < _reasonUntil && !string.IsNullOrEmpty(_lastReason))
        {
            ShowDragReason(_lastReason);
            return;
        }
        // Jangan tooltip-an pas kursor di atas UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            SetTooltip(null);
            return;
        }

        PlacedModule hovered = null;
        if (TryPickModule(out PlacedModule picked))
            hovered = picked;
        SetTooltip(hovered);
    }

    private void SetTooltip(PlacedModule hovered)
    {
        // Kalau reason lagi tampil (drag/flash), jangan ditimpa hover — kecuali
        // pemanggilnya reason itu sendiri (flag dibersihkan di bawah).
        if (!_tooltipLocked && hovered == _hoveredModule) return; // teks cuma di-update pas ganti target
        _tooltipLocked = false;
        _hoveredModule = hovered;

        if (hovered == null || hovered.moduleTemplate == null)
        {
            if (_tooltipGo != null) _tooltipGo.SetActive(false);
            return;
        }

        EnsureTooltipBuilt();
        _tooltipText.SetText(GetDisplayName(hovered));
        _tooltipGo.SetActive(true);
        // Ikuti kursor tiap frame walau target sama
        PositionTooltip();
    }

    private void PositionTooltip()
    {
        if (_tooltipRect == null) return;
        Vector2 p = (Vector2)Input.mousePosition + tooltipOffset;
        p.x = Mathf.Min(p.x, Screen.width - 280f);
        // Kanan atas: pivot bawah-kiri, jangan sampai keluar layar atas
        p.y = Mathf.Min(p.y, Screen.height - 60f);
        _tooltipRect.position = p;
    }

    private void LateUpdate()
    {
        // Tooltip nempel kursor + sembunyi bareng panel editor
        if (_tooltipGo != null && _tooltipGo.activeSelf)
        {
            if (editModePanel == null || !editModePanel.activeSelf)
                _tooltipGo.SetActive(false);
            else
                PositionTooltip();
        }
    }

    private void ShowDragReason(string reason, float holdSeconds = 0f)
    {
        EnsureTooltipBuilt();
        _tooltipLocked = true;
        _hoveredModule = null; // paksa refresh teks hover pas reason selesai
        if (holdSeconds > 0f)
        {
            _lastReason = reason;
            _reasonUntil = Time.time + holdSeconds;
        }
        _tooltipText.SetText(reason);
        if (_tooltipGo != null && !_tooltipGo.activeSelf) _tooltipGo.SetActive(true);
        PositionTooltip();
    }

    private void HideDragReason()
    {
        _tooltipLocked = false;
        _reasonUntil = 0f;
        _hoveredModule = null;
        if (_tooltipGo != null) _tooltipGo.SetActive(false);
    }

    private static string GetDisplayName(PlacedModule mod)
    {
        var t = mod.moduleTemplate;
        if (t.moduleType == ModuleType.Weapon && t.weaponData != null && !string.IsNullOrEmpty(t.weaponData.weaponName))
            return t.weaponData.weaponName;
        return t.moduleName;
    }

    private Canvas FindOrCreateCanvas()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null) return canvas;
        var go = new GameObject("SelectionUICanvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        go.AddComponent<UnityEngine.UI.CanvasScaler>();
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return canvas;
    }

    private void EnsureTooltipBuilt()
    {
        if (_tooltipGo != null) return;
        Canvas canvas = FindOrCreateCanvas();

        _tooltipGo = new GameObject("ModuleHoverTooltip");
        _tooltipGo.transform.SetParent(canvas.transform, false);
        var bg = _tooltipGo.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.8f);
        _tooltipRect = _tooltipGo.GetComponent<RectTransform>();
        _tooltipRect.pivot = new Vector2(0f, 0f); // kanan atas kursor: jangkar kiri-bawah
        _tooltipRect.sizeDelta = new Vector2(260f, 34f);

        var txtGo = new GameObject("Label");
        txtGo.transform.SetParent(_tooltipGo.transform, false);
        _tooltipText = txtGo.AddComponent<TextMeshProUGUI>();
        _tooltipText.fontSize = 15f;
        _tooltipText.color = Color.white;
        var txtRect = _tooltipText.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(8f, 4f);
        txtRect.offsetMax = new Vector2(-8f, -4f);
        _tooltipGo.SetActive(false);
    }

    private void EnsureStatsBuilt()
    {
        if (_statsGo != null) return;
        Canvas canvas = FindOrCreateCanvas();

        _statsGo = new GameObject("ModuleStatsPopup");
        _statsGo.transform.SetParent(canvas.transform, false);
        var bg = _statsGo.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.82f);
        var root = _statsGo.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0f, 0.5f);
        root.anchorMax = new Vector2(0f, 0.5f);
        root.pivot = new Vector2(0f, 0.5f);
        root.anchoredPosition = new Vector2(12f, 40f);
        root.sizeDelta = new Vector2(300f, 60f);
        var fitter = _statsGo.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(_statsGo.transform, false);
        _statsTitle = titleGo.AddComponent<TextMeshProUGUI>();
        _statsTitle.fontSize = 18f;
        _statsTitle.fontStyle = FontStyles.Bold;
        _statsTitle.color = Color.yellow;

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(_statsGo.transform, false);
        _statsBody = bodyGo.AddComponent<TextMeshProUGUI>();
        _statsBody.fontSize = 14f;
        _statsBody.color = Color.white;

        // Layout manual vertikal (tanpa VerticalLayoutGroup biar 0 alloc runtime)
        var titleRect = _statsTitle.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(10f, -8f);
        titleRect.sizeDelta = new Vector2(-20f, 26f);
        var bodyRect = _statsBody.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0f, 1f);
        bodyRect.anchoredPosition = new Vector2(10f, -36f);
        bodyRect.sizeDelta = new Vector2(-20f, 400f);
        _statsGo.SetActive(false);
    }

    private void ShowStatsPopup(PlacedModule mod)
    {
        if (!showStatsPopup || mod == null || mod.moduleTemplate == null) return;
        EnsureStatsBuilt();
        var t = mod.moduleTemplate;
        _statsTitle.SetText(GetDisplayName(mod));

        _sb.Length = 0;
        _sb.AppendLine($"Tipe: {t.moduleType}  |  Grid: {t.width}x{t.height}{(t.enableClearance ? " +clear" : "")}");
        _sb.AppendLine($"Zona: {mod.zoneName}  |  Internal: {(t.canInternal ? "Ya" : "Tidak")}");
        _sb.AppendLine($"HP: {mod.currentHealth:0}/{t.maxHealth:0}  |  Armor: {t.armor:0}  |  Berat: {t.weight:0} kg");
        if (t.ammoPoint > 0)
            _sb.AppendLine($"Ammo: {mod.currentAmmoPoint:0}/{t.ammoPoint:0}");
        if (t.powerConsumption > 0f || t.powerGeneration > 0f || t.extraBatteryCapacity > 0f)
            _sb.AppendLine($"Listrik: -{t.powerConsumption:0}W / +{t.powerGeneration:0}W /Batt {t.extraBatteryCapacity:0}Wh");
        if (t.extraFuelCapacity > 0f)
            _sb.AppendLine($"Bensin: +{t.extraFuelCapacity:0} L");
        if (t.capacitorCapacity > 0f || t.extraMaxOutput > 0f)
            _sb.AppendLine($"Kapasitor: {t.capacitorCapacity:0}Wh / +{t.extraMaxOutput:0}W / {t.chargeRate:0}W");
        if (t.moduleType == ModuleType.Weapon && t.weaponData != null)
        {
            var w = t.weaponData;
            _sb.AppendLine($"ATK {w.attackPower:0} | PEN {w.penetration:0} | {w.fireRateRPM:0} RPM");
            _sb.AppendLine($"Vel {w.muzzleVelocity:0} m/s | Mag {w.maxAmmo:0} | Pellet x{w.pelletCount}");
            if (w.explosiveDamage > 0f)
                _sb.AppendLine($"Ledak: {w.explosiveDamage:0} dmg / R{w.explosiveRadius:0}m");
        }
        if (t.volatileExplosive)
            _sb.AppendLine($"<color=red>Mudah meledak! R{t.explosionRadius} / {t.explosionDamage:0} dmg</color>");
        _sb.AppendLine($"<color=#888888>G: pindah  R: putar  X: lepas</color>");
        _statsBody.SetText(_sb.ToString()); // ToString di sini aman: cuma pas klik, bukan per-frame
        _statsGo.SetActive(true);
    }

    private void HideStatsPopup()
    {
        if (_statsGo != null) _statsGo.SetActive(false);
    }

    private PlacedModule FindModuleByCollider(Collider hitCollider)
    {
        VehicleStatsManager manager = hitCollider.GetComponentInParent<VehicleStatsManager>();
        if (manager == null) return null;

        _currentVehicleManager = manager;
        
        if (manager.moduleColliderMap.TryGetValue(hitCollider, out PlacedModule clickedModule))
        {
            return clickedModule;
        }
        
        return null;
    }

    private void SelectModule(PlacedModule module)
    {
        if (module == null || module.spawnedPrefab == null) return;

        // FindModuleByCollider sudah isi _currentVehicleManager — amankan dulu
        // karena DeselectModule me-null-kan (bug lama: X mati pas ganti seleksi langsung)
        var mgr = _currentVehicleManager;
        if (_selectedModule != null)
        {
            DeselectModule();
        }

        SelectExternal(module, mgr);
    }

    /// <summary>Select dari luar (misal hasil move). Bisa dipasang ke tombol UI.</summary>
    public void SelectExternal(PlacedModule module, VehicleStatsManager mgr)
    {
        if (module == null || module.spawnedPrefab == null) return;
        if (_selectedModule != null) DeselectModule();

        _selectedModule = module;
        _currentVehicleManager = mgr;
        _cachedRenderers = module.spawnedPrefab.GetComponentsInChildren<Renderer>(includeInactive: false);

        ApplyBracketStyle();
        UpdateBracketTransform();
        if (_bracketRoot != null) _bracketRoot.SetActive(true);
        ShowStatsPopup(module);
    }

    /// <summary>Angkat modul terpilih buat dipindah. Bisa dipasang ke tombol UI "Pindah".</summary>
    public void StartMoveSelected()
    {
        if (_selectedModule == null || _currentVehicleManager == null) return;
        var dm = InventoryDragDropManager.Instance;
        if (dm == null || dm.IsDragging) return;

        var mod = _selectedModule;
        var mgr = _currentVehicleManager;
        DeselectModule();

        dm.onMoveFinished += OnMoveFinished;
        dm.StartMove(mod, mgr);
        // Kalau StartMove nolak (misal template null), cabut subscription biar ga nyangkut
        if (!dm.IsDragging)
            dm.onMoveFinished -= OnMoveFinished;
    }

    private void OnMoveFinished(PlacedModule result, VehicleStatsManager mgr)
    {
        var dm = InventoryDragDropManager.Instance;
        if (dm != null) dm.onMoveFinished -= OnMoveFinished;
        // Hasil move (baru / balik ke semula) langsung terselect lagi
        if (result != null)
            SelectExternal(result, mgr);
    }

    /// <summary>
    /// Putar modul terpilih 90° di tempat (jangkar titik tengah). Gagal = alasannya di-flash.
    /// Bisa dipasang ke tombol UI juga.
    /// </summary>
    public void RotateSelected()
    {
        var mod = _selectedModule;
        var mgr = _currentVehicleManager;
        if (mod == null || mod.moduleTemplate == null || mgr == null) return;
        var grid = mgr.gridSystem;
        if (grid == null) return;

        GridZone zone = null;
        if (grid.gridZones != null)
        {
            foreach (var z in grid.gridZones)
            {
                if (z != null && z.zoneName == mod.zoneName) { zone = z; break; }
            }
        }
        if (zone == null || zone.origin == null)
        {
            ShowDragReason("Zonanya hilang!", 1.5f);
            return;
        }

        var t = mod.moduleTemplate;
        int newAngle = (mod.rotationAngle + 90) % 360;

        // Pertahankan titik tengah modul (biar ga lompat anchor)
        int oldW = (mod.rotationAngle == 90 || mod.rotationAngle == 270) ? t.height : t.width;
        int oldH = (mod.rotationAngle == 90 || mod.rotationAngle == 270) ? t.width : t.height;
        int newW = (newAngle == 90 || newAngle == 270) ? t.height : t.width;
        int newH = (newAngle == 90 || newAngle == 270) ? t.width : t.height;
        float cx = mod.gridPosition.x + oldW / 2f;
        float cy = mod.gridPosition.y + oldH / 2f;
        int nx = Mathf.Clamp(Mathf.RoundToInt(cx - newW / 2f), 0, Mathf.Max(0, zone.capacity.x - newW));
        int ny = Mathf.Clamp(Mathf.RoundToInt(cy - newH / 2f), 0, Mathf.Max(0, zone.capacity.y - newH));
        var newPos = new Vector2Int(nx, ny);

        // Rotate di zona yang sama = bukan "masuk baru" → bypass gate internal
        if (!grid.IsAreaFree(zone, newPos, t, newAngle, mod, true))
        {
            ShowDragReason("Ga bisa putar: ketabrak / keluar zona", 1.5f);
            return;
        }

        mod.gridPosition = newPos;
        mod.rotationAngle = newAngle;

        if (mod.spawnedPrefab != null)
        {
            float cellSize = (zone.cellSize > 0f) ? zone.cellSize : 0.25f;
            mod.spawnedPrefab.transform.SetParent(zone.origin, false);
            mod.spawnedPrefab.transform.localPosition = new Vector3(
                (nx + newW / 2f) * cellSize, 0f, (ny + newH / 2f) * cellSize);
            mod.spawnedPrefab.transform.localRotation = Quaternion.Euler(0f, newAngle, 0f);
        }

        mgr.MarkStatsDirty();
        GridSaveSystem.SaveGrid(mgr.gameObject.name, grid);
        UpdateBracketTransform();
        ShowStatsPopup(mod);
    }

    private void DeselectModule()
    {
        if (_bracketRoot != null) _bracketRoot.SetActive(false);
        HideStatsPopup();

        _selectedModule = null;
        _currentVehicleManager = null;
        _cachedRenderers = null;
    }

    // Dipanggil dari Inspector saat warna/tebal diubah biar langsung kelihatan
    private void OnValidate()
    {
        if (_bracketRoot != null && _bracketRoot.activeSelf)
            ApplyBracketStyle();
    }

    private void DeleteSelectedModule()
    {
        if (_selectedModule != null && _currentVehicleManager != null)
        {
            var mgr = _currentVehicleManager;
            mgr.UninstallModule(_selectedModule);
            GridSaveSystem.SaveGrid(mgr.gameObject.name, mgr.gridSystem);
            DeselectModule();
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null) return;
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}