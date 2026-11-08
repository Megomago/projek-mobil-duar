using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class InventoryDragDropManager : MonoBehaviour
{
    private static InventoryDragDropManager _instance;

    /// <summary>
    /// Lazy singleton: kalau belum ada di scene (lupa pasang), dibuat otomatis saat Play.
    /// Di EditMode tetap null biar script ExecuteAlways ga ngotorin scene.
    /// </summary>
    public static InventoryDragDropManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<InventoryDragDropManager>();
                if (_instance == null && Application.isPlaying)
                {
                    var go = new GameObject("InventoryDragDropManager (Auto)");
                    _instance = go.AddComponent<InventoryDragDropManager>();
                }
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    private enum GhostMode
    {
        Free,   // mengambang di permukaan dunia tapi di luar zona grid
        Snap    // nempel ke sel grid
    }

    [Header("Preview Tint (Hologram)")]
    [Tooltip("Tint saat posisi valid (bisa dipasang).")]
    public Color validTint = new Color(0.05f, 1f, 0.12f, 1f);
    [Tooltip("Tint saat posisi invalid / di luar zona grid.")]
    public Color invalidTint = new Color(1f, 0.03f, 0.02f, 1f);
    [Min(0.01f)]
    [Tooltip("Kecepatan blend pergantian warna hologram (makin besar makin cepat).")]
    public float tintSharpness = 14f;

    [Header("Follow / Feel")]
    [Range(0f, 0.3f)]
    [Tooltip("Waktu SmoothDamp posisi ghost. 0 = instan, 0.06 = halus tapi responsif.")]
    public float followSmoothTime = 0.06f;
    [Min(0.01f)]
    [Tooltip("Kecepatan ghost berputar menuju rotasi target.")]
    public float rotationSharpness = 14f;
    [Min(0.1f)]
    [Tooltip("Kalau target loncat lebih jauh dari ini (meter), ghost langsung snap (anti 'terbang lewat map').")]
    public float maxSmoothDistance = 1.5f;

    [Header("Floating (di luar grid)")]
    [Tooltip("Jarak angkat ghost dari permukaan saat kursor di luar zona grid.")]
    [Min(0f)]
    public float freeFloatOffset = 0.07f;
    [Tooltip("Amplitudo bob halus biar ghost kelihatan mengambang, bukan diam kaku.")]
    [Min(0f)]
    public float floatBobAmplitude = 0.012f;
    [Min(0f)]
    public float floatBobSpeed = 2.5f;

    [Header("Footprint Grid (di bawah modul)")]
    [Tooltip("Tampilkan grid tapak di bawah modul saat drag. Ukurannya mengikuti width x height modul.")]
    public bool showFootprintGrid = true;
    [Range(0f, 1f)]
    [Tooltip("Alpha isi tiap kotak grid tapak.")]
    public float footprintFillAlpha = 0.2f;
    [Range(0f, 1f)]
    [Tooltip("Alpha garis kotak grid tapak.")]
    public float footprintLineAlpha = 1f;
    [Range(0.01f, 0.25f)]
    [Tooltip("Tebal garis grid, relatif terhadap 1 kotak.")]
    public float footprintLineWidth = 0.06f;
    [Min(0f)]
    [Tooltip("Jarak angkat grid dari permukaan biar tidak z-fighting.")]
    public float footprintSurfaceOffset = 0.008f;

    [Header("Snap Feedback")]
    [Range(0f, 0.2f)]
    [Tooltip("Pop skala kecil tiap pindah state (masuk grid / keluar grid / dipegang).")]
    public float snapPulse = 0.05f;
    [Min(0.01f)]
    public float snapPulseDecay = 1.1f;

    [Header("Raycast")]
    [Tooltip("Layer yang dianggap permukaan untuk ghost & pemilihan zona.")]
    public LayerMask placementMask = ~0;
    [Min(1f)]
    public float maxRayDistance = 100f;
    [Min(0f)]
    [Tooltip("Hit yang lebih dekat dari ini diabaikan (anti ghost nempel di kamera).")]
    public float minSurfaceDistance = 0.25f;

    private ModuleTemplate _currentTemplate;
    private GameObject _proxyObject;
    private int _currentAngle = 0;
    private VehicleStatsManager _targetStatsManager;
    private bool _isDragging = false;

    private Vector2Int _lastValidGridPos = new Vector2Int(-1, -1);
    private bool _canPlace = false;
    private string _lastValidZoneName;

    // === OPTIMIZATION: Cache IsAreaFree biar gak tiap frame ===
    private Vector2Int _lastCheckPos = new Vector2Int(-999, -999);
    private string _lastCheckZone = "";
    private int _lastCheckAngle = -1;
    private bool _lastCheckResult = false;

    // === OPTIMIZATION / VISUAL CACHE ===
    private Camera _mainCam;
    private Renderer[] _cachedRenderers;
    private Transform[] _proxyTransforms;
    private int[] _proxyOriginalLayers;
    private Material _proxyHologramMaterial;
    private Vector3 _proxyBaseScale = Vector3.one;
    private bool _proxyUsingXray = false;
    private int _xrayLayer = -1;

    // Visual smoothing state
    private GhostMode _ghostMode = GhostMode.Free;
    private Vector3 _visualPosition;
    private Quaternion _visualRotation = Quaternion.identity;
    private Vector3 _visualVelocity;
    private float _visualScale = 1f;
    private bool _hasVisualState = false;
    private float _scalePulse = 0f;

    // Smooth tint state
    private Color _currentTint = Color.white;
    private Color _targetTint = Color.white;
    private bool _hasTint = false;

    // Footprint grid state
    private GameObject _footprintObject;
    private Material _footprintMaterial;
    private Texture2D _footprintTexture;
    private Color _footprintColor = Color.white;
    private bool _footprintHasColor = false;
    private static Mesh _footprintQuad;
    private float _fallbackCellSize = 0.25f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public bool IsDragging => _isDragging;
    public ModuleTemplate CurrentTemplate => _currentTemplate;
    public Vector2Int CurrentGridPos => _lastValidGridPos;
    public string CurrentZoneName => _lastValidZoneName;
    public int CurrentAngle => _currentAngle;
    public bool CanPlace => _canPlace;

    // Alasan penolakan terakhir saat hologram merah. Kosong = bisa dipasang.
    public string RejectReason => _rejectReason;
    private string _rejectReason = "";

    [Header("Move Mode")]
    [Tooltip("Event sekali-pakai saat drag-move selesai: (hasil install/restore, manager). Null = hilang (seharusnya tak terjadi).")]
    public System.Action<PlacedModule, VehicleStatsManager> onMoveFinished;

    // Backup modul yang sedang dipindah (di-uninstall dulu, direstore kalau batal/gagal)
    private bool _isMoveMode = false;
    private ModuleTemplate _moveTemplate;
    private string _moveZone;
    private Vector2Int _movePos;
    private int _moveAngle;
    private VehicleStatsManager _moveManager;

    private void Awake()
    {
        // Pakai backing field langsung — JANGAN via property Instance
        // (getter-nya bisa auto-create / Find yang bikin logika terbalik di sini)
        if (_instance == null)
            _instance = this;
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Cache Camera.main SEKALI di awal, tapi tetap bisa di-reacquire
        // kalau kamera berganti (scene / gameplay camera).
        _mainCam = Camera.main;
        _xrayLayer = LayerMask.NameToLayer("PlacedModule");

        if (_mainCam == null)
            Debug.LogWarning("[InventoryDragDropManager] Main Camera belum ada saat Awake. Akan dicari ulang saat drag.");
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private bool EnsureCamera()
    {
        if (_mainCam == null)
            _mainCam = Camera.main;

        if (_mainCam == null)
        {
            Debug.LogError("[InventoryDragDropManager] Main Camera tidak ditemukan! Kasih tag 'MainCamera' di Camera lu.");
            return false;
        }
        return true;
    }

    public void StartDrag(ModuleTemplate template, VehicleStatsManager statsManager)
    {
        #if UNITY_EDITOR
        Debug.Log("[InventoryDragDropManager] StartDrag dipanggil!");
        #endif

        if (_isDragging) return;
        if (template == null) { Debug.LogError("Template null!"); return; }
        if (statsManager == null) { Debug.LogError("StatsManager null!"); return; }

        if (!EnsureCamera()) return;

        // Pastikan ada setidaknya 1 zona grid yang valid
        bool hasZone = false;
        if (statsManager.gridZones != null)
        {
            foreach (var z in statsManager.gridZones) { if (z != null && z.origin != null) { hasZone = true; break; } }
        }
        if (!hasZone) { Debug.LogError("Grid zones tidak dikonfigurasi pada kendaraan! Tambahkan minimal 1 GridZone."); return; }

        // Ukuran sel cadangan buat footprint saat kursor di luar zona grid.
        _fallbackCellSize = 0.25f;
        if (statsManager.gridZones != null)
        {
            foreach (var z in statsManager.gridZones)
            {
                if (z != null && z.origin != null && z.cellSize > 0f)
                {
                    _fallbackCellSize = z.cellSize;
                    break;
                }
            }
        }

        _currentTemplate = template;
        _targetStatsManager = statsManager;
        _currentAngle = 0;
        _isDragging = true;
        _canPlace = false;
        _rejectReason = "";
        _lastValidGridPos = new Vector2Int(-1, -1);
        _lastValidZoneName = null;

        // Reset cache grid check
        _lastCheckPos = new Vector2Int(-999, -999);
        _lastCheckZone = "";
        _lastCheckAngle = -1;

        // Tentukan prefab proxy
        GameObject prefabToSpawn = template.modulePrefab;
        if (template.moduleType == ModuleType.Weapon && template.weaponData != null && template.weaponData.weapon3DPrefab != null)
        {
            prefabToSpawn = template.weaponData.weapon3DPrefab;
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError($"[InventoryDragDropManager] Prefab 3D untuk {template.moduleName} kosong!");
            // Reset state biar IsDragging ga nyangkut true selamanya (nge-block seleksi)
            _isDragging = false;
            _currentTemplate = null;
            _targetStatsManager = null;
            return;
        }

        // Buat hologram/proxy
        _proxyObject = Instantiate(prefabToSpawn);
        _proxyObject.name = "DragProxy_" + template.moduleName;
        _proxyBaseScale = _proxyObject.transform.localScale;

        SetupProxyVisuals();

        // Jangan tampilkan apa-apa selama kursor masih di UI inventory.
        // Ghost baru muncul begitu kursor keluar panel dan mengenai dunia.
        _proxyObject.SetActive(false);
        _hasVisualState = false;
        _scalePulse = 0f;

        // Grid tapak di bawah modul (bukan cuma prefab ngambang).
        CreateFootprintVisual();
    }

    /// <summary>
    /// Ghost digambar pakai SATU material hologram unlit untuk semua renderer.
    /// Tujuannya: warna yang tampil persis warna tint — tidak kena lighting,
    /// tidak pudar karena metalik/tekstur terang, dan tidak blowout putih.
    /// </summary>
    private void SetupProxyVisuals()
    {
        _cachedRenderers = _proxyObject.GetComponentsInChildren<Renderer>(true);

        _proxyTransforms = _proxyObject.GetComponentsInChildren<Transform>(true);
        _proxyOriginalLayers = new int[_proxyTransforms.Length];
        for (int i = 0; i < _proxyTransforms.Length; i++)
            _proxyOriginalLayers[i] = _proxyTransforms[i].gameObject.layer;
        _proxyUsingXray = false;

        // Shader unlit: output = warna material, tanpa cahaya/emission.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            _proxyHologramMaterial = new Material(shader);
            _proxyHologramMaterial.name = "DragProxyHologram";

            // Pastikan opaque + depth test normal.
            if (_proxyHologramMaterial.HasProperty("_Surface")) _proxyHologramMaterial.SetFloat("_Surface", 0f);
            if (_proxyHologramMaterial.HasProperty("_Blend")) _proxyHologramMaterial.SetFloat("_Blend", 0f);
            if (_proxyHologramMaterial.HasProperty("_SrcBlend")) _proxyHologramMaterial.SetFloat("_SrcBlend", 1f);
            if (_proxyHologramMaterial.HasProperty("_DstBlend")) _proxyHologramMaterial.SetFloat("_DstBlend", 0f);
            if (_proxyHologramMaterial.HasProperty("_ZWrite")) _proxyHologramMaterial.SetFloat("_ZWrite", 1f);
            if (_proxyHologramMaterial.HasProperty("_Cull")) _proxyHologramMaterial.SetFloat("_Cull", 2f);
            _proxyHologramMaterial.renderQueue = 2000;
        }
        else
        {
            Debug.LogWarning("[InventoryDragDropManager] Shader hologram tidak ditemukan. Ghost pakai material asli.");
        }

        foreach (var rend in _cachedRenderers)
        {
            if (rend == null) continue;

            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;

            // Particle/trail/line renderer sering putih atau additive — matikan
            // biar tidak ada kilatan putih di ghost.
            if (!(rend is MeshRenderer) && !(rend is SkinnedMeshRenderer))
            {
                rend.enabled = false;
                continue;
            }

            if (_proxyHologramMaterial == null) continue;

            // Panjang array harus sama dengan jumlah submesh aslinya.
            int count = (rend.sharedMaterials != null && rend.sharedMaterials.Length > 0)
                ? rend.sharedMaterials.Length : 1;
            var mats = new Material[count];
            for (int i = 0; i < count; i++)
                mats[i] = _proxyHologramMaterial;
            rend.sharedMaterials = mats;
        }

        // Matikan collider, script, dan animator.
        foreach (var col in _proxyObject.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (var s in _proxyObject.GetComponentsInChildren<MonoBehaviour>(true)) s.enabled = false;
        foreach (var anim in _proxyObject.GetComponentsInChildren<Animator>(true)) anim.enabled = false;

        _hasVisualState = false;
        _hasTint = true;
        _currentTint = validTint;
        _targetTint = validTint;
        ApplyTintToMaterials(validTint);
    }

    /// <summary>
    /// Pindah modul yang sudah terpasang: lepas dulu, drag proxy seperti biasa.
    /// Batal (klik kanan) / drop gagal = balik ke posisi semula. R = rotate saat drag.
    /// </summary>
    public void StartMove(PlacedModule module, VehicleStatsManager statsManager)
    {
        if (_isDragging) return;
        if (module == null || module.moduleTemplate == null || statsManager == null) return;

        // Backup posisi semula sebelum di-uninstall
        _isMoveMode = true;
        _moveTemplate = module.moduleTemplate;
        _moveZone = module.zoneName;
        _movePos = module.gridPosition;
        _moveAngle = ((module.rotationAngle % 360) + 360) % 360;
        _moveManager = statsManager;

        statsManager.UninstallModule(module);

        StartDrag(_moveTemplate, statsManager);
        if (!_isDragging)
        {
            // StartDrag nolak (misal zona invalid) — kembalikan ke semula, jangan hilangkan modul
            RestoreMoveBackup();
            CancelDrag();
            return;
        }
        // Lanjut dari rotasi semula, bukan 0
        _currentAngle = _moveAngle;
        _lastCheckAngle = -1; // paksa re-check area
    }

    private PlacedModule RestoreMoveBackup()
    {
        if (!_isMoveMode || _moveTemplate == null || _moveManager == null) return null;
        // Restore = kembali ke posisi SEMULA, bukan masuk baru → bypass gate internal
        PlacedModule restored = _moveManager.InstallModule(_moveTemplate, _moveZone, _movePos, _moveAngle, true);
        if (restored != null)
            GridSaveSystem.SaveGrid(_moveManager.gameObject.name, _moveManager.gridSystem);
        return restored;
    }

    private void FinishMove(PlacedModule result, VehicleStatsManager mgr)
    {
        var cb = onMoveFinished;
        onMoveFinished = null;
        if (cb != null && mgr != null)
            cb.Invoke(result, mgr);
    }

    private void Update()
    {
        if (!_isDragging || _currentTemplate == null || _targetStatsManager == null) return;
        if (!EnsureCamera()) return;

        // Handle Batal (Klik Kanan)
        if (Input.GetMouseButtonDown(1))
        {
            PlacedModule restored = RestoreMoveBackup();
            VehicleStatsManager restoredMgr = _moveManager;
            bool wasMove = _isMoveMode;
            CancelDrag();
            if (wasMove) FinishMove(restored, restoredMgr);
            return;
        }

        // Handle Rotasi (R)
        if (Input.GetKeyDown(KeyCode.R))
        {
            _currentAngle = (_currentAngle + 90) % 360;
        }

        float dt = Time.unscaledDeltaTime;
        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        GridZone bestZone = null;
        Vector3 bestHitPoint = Vector3.zero;
        float bestMetric = Mathf.Infinity;

        RaycastHit physicsHit = default;
        bool hasPhysicsHit = false;
        int gridX = 0, gridY = 0;
        int effectiveWidth = (_currentAngle == 90 || _currentAngle == 270) ? _currentTemplate.height : _currentTemplate.width;
        int effectiveHeight = (_currentAngle == 90 || _currentAngle == 270) ? _currentTemplate.width : _currentTemplate.height;
        float cellSize = _fallbackCellSize;

        if (!pointerOverUI)
        {
            Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);

            // Hit permukaan dunia untuk fallback "mengambang" saat di luar grid.
            hasPhysicsHit = Physics.Raycast(
                ray, out physicsHit, maxRayDistance, placementMask, QueryTriggerInteraction.Ignore);

            if (hasPhysicsHit)
            {
                if (physicsHit.distance < minSurfaceDistance)
                    hasPhysicsHit = false;
                else if (_proxyObject != null && physicsHit.transform.IsChildOf(_proxyObject.transform))
                    hasPhysicsHit = false;
            }

            // Cari zona yang valid dan prioritaskan yang paling mendekati titik hit fisik (jika ada)
            Vector3 physicsPoint = hasPhysicsHit ? physicsHit.point : Vector3.zero;

            if (_targetStatsManager.gridZones != null)
            {
                foreach (var zone in _targetStatsManager.gridZones)
                {
                    if (zone == null || zone.origin == null) continue;

                    Plane gridPlane = new Plane(zone.origin.up, zone.origin.position);
                    if (!gridPlane.Raycast(ray, out float enter)) continue;

                    Vector3 planeHit = ray.GetPoint(enter);

                    // Quick bounds check: titik di plane harus di dalam area zona
                    Vector3 localHitCheck = zone.origin.InverseTransformPoint(planeHit);
                    float checkCellSize = (zone.cellSize > 0f) ? zone.cellSize : 0.25f;
                    int checkW = (_currentAngle == 90 || _currentAngle == 270) ? _currentTemplate.height : _currentTemplate.width;
                    int checkH = (_currentAngle == 90 || _currentAngle == 270) ? _currentTemplate.width : _currentTemplate.height;
                    int testX = Mathf.FloorToInt((localHitCheck.x / checkCellSize) - (checkW / 2f) + 0.5f);
                    int testY = Mathf.FloorToInt((localHitCheck.z / checkCellSize) - (checkH / 2f) + 0.5f);

                    bool withinBounds = (testX >= 0 && testX < zone.capacity.x && testY >= 0 && testY < zone.capacity.y);
                    if (!withinBounds) continue;

                    float metric = hasPhysicsHit ? Vector3.Distance(planeHit, physicsPoint) : enter;
                    if (metric < bestMetric)
                    {
                        bestMetric = metric;
                        bestZone = zone;
                        bestHitPoint = planeHit;
                    }
                }
            }
        }

        if (bestZone != null)
        {
            Vector3 localHit = bestZone.origin.InverseTransformPoint(bestHitPoint);
            cellSize = (bestZone.cellSize > 0f) ? bestZone.cellSize : 0.25f;

            gridX = Mathf.FloorToInt((localHit.x / cellSize) - (effectiveWidth / 2f) + 0.5f);
            gridY = Mathf.FloorToInt((localHit.z / cellSize) - (effectiveHeight / 2f) + 0.5f);

            // Cache: skip IsAreaFree kalo posisi/angle gak berubah
            bool posChanged = (gridX != _lastCheckPos.x || gridY != _lastCheckPos.y ||
                               bestZone.zoneName != _lastCheckZone || _currentAngle != _lastCheckAngle);
            if (posChanged)
            {
                _canPlace = _targetStatsManager.IsAreaFree(bestZone, new Vector2Int(gridX, gridY), _currentTemplate, _currentAngle);
                _lastCheckPos = new Vector2Int(gridX, gridY);
                _lastCheckZone = bestZone.zoneName;
                _lastCheckAngle = _currentAngle;
                _lastCheckResult = _canPlace;
            }
            else
            {
                _canPlace = _lastCheckResult;
            }

            _lastValidGridPos = new Vector2Int(gridX, gridY);
            _lastValidZoneName = bestZone.zoneName;

            // Alasan penolakan (biar hologram merah ada tulisannya, ga nebak-nebak)
            if (bestZone.isInternalGrid && !_currentTemplate.canInternal)
                _rejectReason = "Zona internal: modul tak berizin (Can Internal mati)";
            else if (!_canPlace)
                _rejectReason = "Area penuh / ketabrak modul lain";
            else
                _rejectReason = "";
        }
        else
        {
            _canPlace = false;
            _lastValidGridPos = new Vector2Int(-1, -1);
            _lastValidZoneName = null;

            if (pointerOverUI)
                _rejectReason = "";
            else if (hasPhysicsHit)
                _rejectReason = "Di luar zona grid";
            else
                _rejectReason = "Arahkan kursor ke kendaraan";
        }

        // ── Update visual ghost ─────────────────────────────────────────────
        // Ghost cuma boleh tampil kalau kursor keluar dari UI DAN mengenai dunia.
        // Selama masih di panel inventory: sembunyi total (ga ada prefab nongol di tengah).
        bool showGhost = !pointerOverUI && (bestZone != null || hasPhysicsHit);

        if (_proxyObject != null)
        {
            if (!showGhost)
            {
                HideGhost();
                HideFootprint();
            }
            else
            {
                bool wasHidden = !_proxyObject.activeSelf;
                if (wasHidden)
                {
                    _proxyObject.SetActive(true);
                    _hasVisualState = false; // muncul langsung di target, bukan terbang dari tengah layar
                }

                Color targetTint = (bestZone != null && _canPlace) ? validTint : invalidTint;
                _targetTint = targetTint;

                if (wasHidden)
                {
                    // Warna langsung "matang" saat pertama muncul (tanpa blend dari warna lama).
                    _currentTint = targetTint;
                    _hasTint = true;
                    _footprintHasColor = false;
                    ApplyTintToMaterials(targetTint);
                }

                float bob = Mathf.Sin(Time.unscaledTime * floatBobSpeed) * floatBobAmplitude;

                if (bestZone != null)
                {
                    // Nempel ke sel grid
                    SetGhostMode(GhostMode.Snap);
                    SetProxyXray(bestZone.isInternalGrid);

                    float offsetX = (gridX + effectiveWidth / 2f) * cellSize;
                    float offsetZ = (gridY + effectiveHeight / 2f) * cellSize;
                    Vector3 snappedLocalPos = new Vector3(offsetX, 0f, offsetZ);

                    Vector3 worldPos = bestZone.origin.TransformPoint(snappedLocalPos);
                    Quaternion worldRot = bestZone.origin.rotation * Quaternion.Euler(0f, _currentAngle, 0f);

                    UpdateGhost(worldPos, worldRot, bestZone.origin, 1f, dt);

                    // Grid tapak ikut nempel ke sel grid.
                    UpdateFootprint(
                        bestZone.origin.TransformPoint(snappedLocalPos + Vector3.up * footprintSurfaceOffset),
                        bestZone.origin.rotation,
                        effectiveWidth, effectiveHeight, cellSize, targetTint, dt);
                }
                else
                {
                    // Di luar grid: mengambang di atas permukaan yang kena raycast.
                    SetGhostMode(GhostMode.Free);
                    SetProxyXray(false);

                    Vector3 freePos = physicsHit.point + physicsHit.normal * freeFloatOffset + Vector3.up * bob;
                    Quaternion freeRot = Quaternion.Euler(0f, _currentAngle, 0f);
                    UpdateGhost(freePos, freeRot, null, 1f, dt);

                    // Grid tapak tetap tampil di atas permukaan (bukan cuma prefab ngambang).
                    Quaternion footprintRot =
                        Quaternion.FromToRotation(Vector3.up, physicsHit.normal) *
                        Quaternion.Euler(0f, _currentAngle, 0f);
                    UpdateFootprint(
                        physicsHit.point + physicsHit.normal * footprintSurfaceOffset,
                        footprintRot,
                        _currentTemplate.width, _currentTemplate.height, _fallbackCellSize, targetTint, dt);
                }
            }
        }

        if (_proxyObject != null && _proxyObject.activeSelf)
            UpdateTint(dt);

        // Handle Drop (Lepas Klik Kiri)
        if (Input.GetMouseButtonUp(0))
        {
            PlacedModule result = null;
            VehicleStatsManager resultMgr = _targetStatsManager;
            bool wasMove = _isMoveMode;

            // Jangan pasang kalau kursor dilepas di atas UI (misal balik ke panel inventory).
            if (!pointerOverUI && _canPlace && _lastValidGridPos.x != -1 && !string.IsNullOrEmpty(_lastValidZoneName))
            {
                PlacedModule installedMod = _targetStatsManager.InstallModule(_currentTemplate, _lastValidZoneName, _lastValidGridPos, _currentAngle);

                if (installedMod != null)
                {
                    string vehicleName = _targetStatsManager.gameObject.name;
                    GridSaveSystem.SaveGrid(vehicleName, _targetStatsManager.gridSystem);
                    result = installedMod;
                }
                else if (wasMove)
                {
                    // Install gagal (seharusnya tak terjadi) — kembalikan ke semula
                    result = RestoreMoveBackup();
                    resultMgr = _moveManager;
                }
            }
            else if (wasMove)
            {
                // Drop di posisi invalid / di atas UI saat move = balik ke semula, bukan hilang
                result = RestoreMoveBackup();
                resultMgr = _moveManager;
            }
            CancelDrag();
            if (wasMove) FinishMove(result, resultMgr);
        }
    }

    // ── Ghost follow / feel ────────────────────────────────────────────────

    private void HideGhost()
    {
        if (_proxyObject == null) return;

        if (_proxyObject.activeSelf)
            _proxyObject.SetActive(false);

        // Reset visual state biar saat muncul lagi langsung nempel di target,
        // bukan meluncur dari posisi terakhir.
        _hasVisualState = false;
        _visualVelocity = Vector3.zero;
        _scalePulse = 0f;
    }

    // ── Footprint Grid ─────────────────────────────────────────────────────

    /// <summary>
    /// Bikin grid tapak sekali per drag. Satu kotak tekstur = satu sel grid,
    /// jadi ukuran garis tetap konsisten walau modul 1x1 atau 3x2.
    /// </summary>
    private void CreateFootprintVisual()
    {
        DestroyFootprint();

        if (!showFootprintGrid) return;

        _footprintObject = new GameObject("DragFootprintGrid");
        _footprintObject.hideFlags = HideFlags.DontSave;
        _footprintObject.SetActive(false);

        MeshFilter mf = _footprintObject.AddComponent<MeshFilter>();
        if (_footprintQuad == null)
            _footprintQuad = CreateFootprintQuad();
        mf.sharedMesh = _footprintQuad;

        MeshRenderer mr = _footprintObject.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        // Pakai shader overlay yang sudah dipakai GridVisualizer (unlit + tembus mesh).
        Shader shader = Shader.Find("Custom/GridOverlay");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("[InventoryDragDropManager] Shader grid tidak ditemukan, footprint dimatikan.");
            Destroy(_footprintObject);
            _footprintObject = null;
            return;
        }

        _footprintMaterial = new Material(shader);
        _footprintMaterial.SetTexture("_MainTex", CreateFootprintTexture());
        _footprintMaterial.SetVector("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f));
        _footprintMaterial.SetColor("_Color", validTint);
        mr.sharedMaterial = _footprintMaterial;

        _footprintHasColor = false;
    }

    private void UpdateFootprint(
        Vector3 center, Quaternion rotation,
        int cellsX, int cellsY, float cellSize,
        Color color, float dt)
    {
        if (!showFootprintGrid || _footprintObject == null || _footprintMaterial == null) return;

        if (!_footprintObject.activeSelf)
        {
            _footprintObject.SetActive(true);
            _footprintHasColor = false;
        }

        int cx = Mathf.Max(1, cellsX);
        int cy = Mathf.Max(1, cellsY);

        _footprintObject.transform.SetPositionAndRotation(center, rotation);
        _footprintObject.transform.localScale = new Vector3(cx * cellSize, 1f, cy * cellSize);

        // Satu tile tekstur = satu sel, biar ketebalan garis konsisten.
        _footprintMaterial.SetVector("_MainTex_ST", new Vector4(cx, cy, 0f, 0f));

        if (!_footprintHasColor)
        {
            _footprintColor = color;
            _footprintHasColor = true;
        }
        else
        {
            _footprintColor = Color.Lerp(_footprintColor, color, 1f - Mathf.Exp(-tintSharpness * dt));
            if (ColorsClose(_footprintColor, color))
                _footprintColor = color;
        }

        _footprintMaterial.SetColor("_Color", _footprintColor);
    }

    private void HideFootprint()
    {
        if (_footprintObject != null && _footprintObject.activeSelf)
            _footprintObject.SetActive(false);

        _footprintHasColor = false;
    }

    private void DestroyFootprint()
    {
        if (_footprintObject != null)
        {
            Destroy(_footprintObject);
            _footprintObject = null;
        }
        if (_footprintMaterial != null)
        {
            Destroy(_footprintMaterial);
            _footprintMaterial = null;
        }
        if (_footprintTexture != null)
        {
            Destroy(_footprintTexture);
            _footprintTexture = null;
        }
        _footprintHasColor = false;
    }

    private Texture2D CreateFootprintTexture()
    {
        const int size = 64;
        int border = Mathf.Clamp(Mathf.RoundToInt(size * footprintLineWidth), 1, size / 4);

        _footprintTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        _footprintTexture.name = "DragFootprintGridTex";
        _footprintTexture.wrapMode = TextureWrapMode.Repeat;
        _footprintTexture.filterMode = FilterMode.Bilinear;
        _footprintTexture.hideFlags = HideFlags.DontSave;

        Color fill = new Color(1f, 1f, 1f, footprintFillAlpha);
        Color line = new Color(1f, 1f, 1f, footprintLineAlpha);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool onBorder = x < border || y < border || x >= size - border || y >= size - border;
                pixels[y * size + x] = onBorder ? line : fill;
            }
        }

        _footprintTexture.SetPixels(pixels);
        _footprintTexture.Apply(false, false);
        return _footprintTexture;
    }

    private static Mesh CreateFootprintQuad()
    {
        Mesh mesh = new Mesh();
        mesh.name = "DragFootprintQuad";
        mesh.hideFlags = HideFlags.DontSave;
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3(-0.5f, 0f,  0.5f),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void UpdateGhost(Vector3 targetPos, Quaternion targetRot, Transform parent, float targetScale, float dt)
    {
        if (_proxyObject == null) return;

        if (!_hasVisualState)
        {
            // Muncul pertama kali / abis disembunyikan: taruh langsung di target
            // (jangan terbang dari tengah layar), plus pop skala kecil.
            _visualPosition = targetPos;
            _visualRotation = targetRot;
            _visualVelocity = Vector3.zero;
            _visualScale = targetScale;
            _scalePulse = Mathf.Max(_scalePulse, snapPulse);
            _hasVisualState = true;

            if (parent != _proxyObject.transform.parent)
                _proxyObject.transform.SetParent(parent, true);

            _proxyObject.transform.SetPositionAndRotation(targetPos, targetRot);
            _proxyObject.transform.localScale = _proxyBaseScale * (targetScale * (1f + _scalePulse));
            return;
        }

        // Target loncat terlalu jauh (pindah zona / dari tangan ke world):
        // snap biar ghost ga "terbang" nyebrang map.
        if ((targetPos - _visualPosition).sqrMagnitude > maxSmoothDistance * maxSmoothDistance)
        {
            _visualPosition = targetPos;
            _visualVelocity = Vector3.zero;
        }
        else if (followSmoothTime <= 0.0001f)
        {
            _visualPosition = targetPos;
            _visualVelocity = Vector3.zero;
        }
        else
        {
            _visualPosition = Vector3.SmoothDamp(
                _visualPosition, targetPos, ref _visualVelocity,
                followSmoothTime, Mathf.Infinity, dt);
        }

        _visualRotation = Quaternion.Slerp(
            _visualRotation, targetRot, 1f - Mathf.Exp(-rotationSharpness * dt));

        _visualScale = Mathf.Lerp(
            _visualScale, targetScale, 1f - Mathf.Exp(-rotationSharpness * dt));

        _scalePulse = Mathf.MoveTowards(_scalePulse, 0f, snapPulseDecay * dt);

        if (parent != _proxyObject.transform.parent)
            _proxyObject.transform.SetParent(parent, true);

        _proxyObject.transform.SetPositionAndRotation(_visualPosition, _visualRotation);
        _proxyObject.transform.localScale = _proxyBaseScale * (_visualScale * (1f + _scalePulse));
    }

    private void SetGhostMode(GhostMode mode)
    {
        if (_ghostMode == mode) return;

        _ghostMode = mode;

        // Pop kecil tiap ganti state: masuk grid, keluar grid, atau dipegang.
        _scalePulse = Mathf.Max(_scalePulse, snapPulse);
    }

    /// <summary>
    /// Layer X-ray cuma dipakai kalau target-nya zona internal.
    /// Di zona normal, ghost render normal (tidak tembus bodi) biar tidak terlihat melayang.
    /// </summary>
    private void SetProxyXray(bool on)
    {
        if (_proxyTransforms == null || _proxyOriginalLayers == null) return;
        if (on == _proxyUsingXray) return;
        if (on && _xrayLayer == -1) return;

        _proxyUsingXray = on;
        for (int i = 0; i < _proxyTransforms.Length; i++)
        {
            Transform t = _proxyTransforms[i];
            if (t == null) continue;
            t.gameObject.layer = on ? _xrayLayer : _proxyOriginalLayers[i];
        }
    }

    // ── Tint ───────────────────────────────────────────────────────────────

    private void UpdateTint(float dt)
    {
        if (!_hasTint || _proxyHologramMaterial == null) return;

        if (ColorsClose(_currentTint, _targetTint))
        {
            _currentTint = _targetTint;
            return;
        }

        _currentTint = Color.Lerp(_currentTint, _targetTint, 1f - Mathf.Exp(-tintSharpness * dt));

        if (ColorsClose(_currentTint, _targetTint))
            _currentTint = _targetTint;

        ApplyTintToMaterials(_currentTint);
    }

    private void ApplyTintToMaterials(Color tint)
    {
        if (_proxyHologramMaterial == null) return;

        // Unlit: warna final = warna ini, jadi pekatnya konsisten.
        if (_proxyHologramMaterial.HasProperty(BaseColorId))
            _proxyHologramMaterial.SetColor(BaseColorId, tint);

        if (_proxyHologramMaterial.HasProperty(ColorId))
            _proxyHologramMaterial.SetColor(ColorId, tint);
    }

    private static bool ColorsClose(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.004f
            && Mathf.Abs(a.g - b.g) < 0.004f
            && Mathf.Abs(a.b - b.b) < 0.004f
            && Mathf.Abs(a.a - b.a) < 0.004f;
    }

    // ── Cleanup ────────────────────────────────────────────────────────────

    private void CancelDrag()
    {
        _isDragging = false;
        _currentTemplate = null;
        _targetStatsManager = null;
        _rejectReason = "";
        _isMoveMode = false;
        _moveTemplate = null;
        _moveManager = null;
        _moveZone = null;

        _canPlace = false;
        _lastValidGridPos = new Vector2Int(-1, -1);
        _lastValidZoneName = null;

        // Bersihin cache biar kagak nge-memory leak nge-refer ke object yang udah hancur
        _cachedRenderers = null;
        _proxyTransforms = null;
        _proxyOriginalLayers = null;
        _visualVelocity = Vector3.zero;
        _hasVisualState = false;
        _hasTint = false;
        _proxyUsingXray = false;

        // Material hologram dibuat per drag, jadi harus dimatikan manual.
        if (_proxyHologramMaterial != null)
        {
            Destroy(_proxyHologramMaterial);
            _proxyHologramMaterial = null;
        }

        if (_proxyObject != null)
        {
            Destroy(_proxyObject);
            _proxyObject = null;
        }

        DestroyFootprint();
    }
}
