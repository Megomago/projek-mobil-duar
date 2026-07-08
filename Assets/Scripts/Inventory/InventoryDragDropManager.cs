using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class InventoryDragDropManager : MonoBehaviour
{
    public static InventoryDragDropManager Instance;

    [Header("Settings")]
    [Tooltip("Material transparan warna hijau untuk menandakan posisi valid")]
    public Material validMaterial;   
    [Tooltip("Material transparan warna merah untuk menandakan posisi tidak valid")]
    public Material invalidMaterial; 

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

    // === OPTIMIZATION: Cache Variables ===
    private Camera _mainCam;
    private Renderer[] _cachedRenderers;
    private Collider[] _cachedColliders;
    private MonoBehaviour[] _cachedScripts;
    private Material _currentAppliedMat; // Biar gak nge-assign material mulu kalau warnanya sama
    
    public bool IsDragging => _isDragging;
    public ModuleTemplate CurrentTemplate => _currentTemplate;
    public Vector2Int CurrentGridPos => _lastValidGridPos;
    public string CurrentZoneName => _lastValidZoneName;
    public int CurrentAngle => _currentAngle;
    public bool CanPlace => _canPlace;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Cache Camera.main SEKALI di awal. JANGAN dipanggil di Update()!
        _mainCam = Camera.main;
        if (_mainCam == null) Debug.LogError("[InventoryDragDropManager] Main Camera tidak ditemukan! Kasih tag 'MainCamera' di Camera lu.");
    }

    public void StartDrag(ModuleTemplate template, VehicleStatsManager statsManager)
    {
        Debug.Log("[InventoryDragDropManager] StartDrag dipanggil!");
        
        if (template == null) { Debug.LogError("Template null!"); return; }
        if (statsManager == null) { Debug.LogError("StatsManager null!"); return; }
        // Pastikan ada setidaknya 1 zona grid yang valid
        bool hasZone = false;
        if (statsManager.gridZones != null)
        {
            foreach (var z in statsManager.gridZones) { if (z != null && z.origin != null) { hasZone = true; break; } }
        }
        if (!hasZone) { Debug.LogError("Grid zones tidak dikonfigurasi pada kendaraan! Tambahkan minimal 1 GridZone."); return; }

        _currentTemplate = template;
        _targetStatsManager = statsManager;
        _currentAngle = 0;
        _isDragging = true;
        _currentAppliedMat = null;

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
            return;
        }

        // Buat hologram/proxy
        _proxyObject = Instantiate(prefabToSpawn);
        _proxyObject.name = "DragProxy_" + template.moduleName;
            
        // === OPTIMIZATION: Cache semua component SEKALI SAAT INSTANTIATE ===
        _cachedRenderers = _proxyObject.GetComponentsInChildren<Renderer>();
        _cachedColliders = _proxyObject.GetComponentsInChildren<Collider>();
        _cachedScripts = _proxyObject.GetComponentsInChildren<MonoBehaviour>();

        // Matikan collider dan script
        foreach (var col in _cachedColliders) col.enabled = false;
        foreach (var s in _cachedScripts) s.enabled = false;

        ApplyMaterialToProxy(validMaterial);
    }

    private void Update()
    {
        if (!_isDragging || _currentTemplate == null || _targetStatsManager == null) return;

        // Handle Batal (Klik Kanan)
        if (Input.GetMouseButtonDown(1))
        {
            CancelDrag();
            return;
        }

        // Handle Rotasi (R)
        if (Input.GetKeyDown(KeyCode.R))
        {
            _currentAngle = (_currentAngle + 90) % 360;
        }

        // Gunakan _mainCam yang udah di-cache, BUKAN Camera.main!
        Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);

        GridZone bestZone = null;
        Vector3 bestHitPoint = Vector3.zero;
        float bestMetric = Mathf.Infinity;

        // Prefer actual collider under mouse: use Physics.Raycast to find geometry hit point
        RaycastHit physicsHit;
        bool hasPhysicsHit = Physics.Raycast(ray, out physicsHit, Mathf.Infinity);
        Vector3 physicsPoint = hasPhysicsHit ? physicsHit.point : Vector3.zero;

        // Cari zona yang valid dan prioritaskan yang paling mendekati titik hit fisik (jika ada)
        if (_targetStatsManager.gridZones != null)
        {
            foreach (var zone in _targetStatsManager.gridZones)
            {
                if (zone == null || zone.origin == null) continue;
                Plane gridPlane = new Plane(zone.origin.up, zone.origin.position);
                if (gridPlane.Raycast(ray, out float enter))
                {
                    Vector3 planeHit = ray.GetPoint(enter);

                    // Kalkulasi apakah titik pada plane itu berada di dalam batas zona (quick bounds check)
                    Vector3 localHitCheck = zone.origin.InverseTransformPoint(planeHit);
                    float checkCellSize = (zone.cellSize > 0f) ? zone.cellSize : 0.25f;
                    int effW = (_currentAngle == 90 || _currentAngle == 270) ? _currentTemplate.height : _currentTemplate.width;
                    int effH = (_currentAngle == 90 || _currentAngle == 270) ? _currentTemplate.width : _currentTemplate.height;
                    int testX = Mathf.FloorToInt((localHitCheck.x / checkCellSize) - (effW / 2f) + 0.5f);
                    int testY = Mathf.FloorToInt((localHitCheck.z / checkCellSize) - (effH / 2f) + 0.5f);

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
            float cellSize = (bestZone.cellSize > 0f) ? bestZone.cellSize : 0.25f;

            int effectiveWidth = (_currentAngle == 90 || _currentAngle == 270) ? _currentTemplate.height : _currentTemplate.width;
            int effectiveHeight = (_currentAngle == 90 || _currentAngle == 270) ? _currentTemplate.width : _currentTemplate.height;

            int gridX = Mathf.FloorToInt((localHit.x / cellSize) - (effectiveWidth / 2f) + 0.5f);
            int gridY = Mathf.FloorToInt((localHit.z / cellSize) - (effectiveHeight / 2f) + 0.5f);

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

            if (_proxyObject != null)
            {
                float offsetX = (gridX + effectiveWidth / 2f) * cellSize;
                float offsetZ = (gridY + effectiveHeight / 2f) * cellSize;
                Vector3 snappedLocalPos = new Vector3(offsetX, 0f, offsetZ);

                // Parent proxy to the zone origin so it inherits zone height and rotation
                _proxyObject.transform.SetParent(bestZone.origin, false);
                _proxyObject.transform.localPosition = snappedLocalPos;
                _proxyObject.transform.localRotation = Quaternion.Euler(0f, _currentAngle, 0f);

                ApplyMaterialToProxy(_canPlace ? validMaterial : invalidMaterial);
            }
        }
        else
        {
            _canPlace = false;
            _lastValidZoneName = null;
            if (_proxyObject != null)
            {
                // Unparent proxy so it doesn't stick to an old zone
                _proxyObject.transform.SetParent(null, true);
                ApplyMaterialToProxy(invalidMaterial);
            }
        }

        // Handle Drop (Lepas Klik Kiri)
        if (Input.GetMouseButtonUp(0))
        {
            if (_canPlace && _lastValidGridPos.x != -1 && !string.IsNullOrEmpty(_lastValidZoneName))
            {
                bool success = _targetStatsManager.InstallModule(_currentTemplate, _lastValidZoneName, _lastValidGridPos, _currentAngle);

                if (success)
                {
                    string vehicleName = _targetStatsManager.gameObject.name;
                    GridSaveSystem.SaveGrid(vehicleName, _targetStatsManager.gridSystem);
                }
            }
            CancelDrag();
        }
    }

    private void CancelDrag()
    {
        _isDragging = false;
        _currentTemplate = null;
        _targetStatsManager = null;
        
        // Bersihin cache biar kagak nge-memory leak nge-refer ke object yang udah hancur
        _cachedRenderers = null;
        _cachedColliders = null;
        _cachedScripts = null;
        _currentAppliedMat = null;

        if (_proxyObject != null)
        {
            Destroy(_proxyObject);
            _proxyObject = null;
        }
    }

    // === OPTIMIZATION: Fungsi ini sekarang cuma make array yang udah di-cache ===
    private void ApplyMaterialToProxy(Material mat)
    {
        // Kalau materialnya sama kayak yang lagi dipake, SKIP! Gak perlu di-assign lagi.
        if (mat == null || _cachedRenderers == null || _currentAppliedMat == mat) return;
        
        _currentAppliedMat = mat; // Update tracker

        foreach (var rend in _cachedRenderers)
        {
            Material[] mats = new Material[rend.materials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = mat;
            }
            rend.materials = mats;
        }
    }
}