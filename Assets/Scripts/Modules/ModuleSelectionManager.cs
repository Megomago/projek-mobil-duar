using UnityEngine;
using UnityEngine.EventSystems;

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
        // Cegah seleksi pas lagi nge-drag item di UI
        if (InventoryDragDropManager.Instance != null && InventoryDragDropManager.Instance.IsDragging) return;

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

        if (Input.GetKeyDown(KeyCode.X) && _selectedModule != null)
        {
            DeleteSelectedModule();
        }

        // G = angkat/pindah modul terpilih. R = rotate saat drag, klik kiri = taruh, klik kanan = batal.
        if (Input.GetKeyDown(KeyCode.G) && _selectedModule != null)
        {
            StartMoveSelected();
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
        Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, moduleLayer))
        {
            PlacedModule clickedModule = FindModuleByCollider(hit.collider);
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

    private void DeselectModule()
    {
        if (_bracketRoot != null) _bracketRoot.SetActive(false);

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