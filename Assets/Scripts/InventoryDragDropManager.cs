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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartDrag(ModuleTemplate template, VehicleStatsManager statsManager)
    {
        Debug.Log("[InventoryDragDropManager] StartDrag dipanggil!");
        
        if (template == null) { Debug.LogError("Template null!"); return; }
        if (statsManager == null) { Debug.LogError("StatsManager null!"); return; }
        if (statsManager.gridOrigin == null) { Debug.LogError("GridOrigin null pada kendaraan!"); return; }

        _currentTemplate = template;
        _targetStatsManager = statsManager;
        _currentAngle = 0;
        _isDragging = true;

        // Tentukan prefab proxy
        GameObject prefabToSpawn = template.modulePrefab;
        if (template.moduleType == ModuleType.Weapon && template.weaponData != null && template.weaponData.weapon3DPrefab != null)
        {
            prefabToSpawn = template.weaponData.weapon3DPrefab;
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError($"[InventoryDragDropManager] Prefab 3D untuk {template.moduleName} kosong! Pastikan prefab terisi di ModuleTemplate atau WeaponData.");
            return;
        }

        // Buat hologram/proxy
        _proxyObject = Instantiate(prefabToSpawn);
        _proxyObject.name = "DragProxy_" + template.moduleName;
            
        // Matikan collider dan script pada proxy agar tidak konflik
        Collider[] colliders = _proxyObject.GetComponentsInChildren<Collider>();
        foreach (var col in colliders) col.enabled = false;
            
        MonoBehaviour[] scripts = _proxyObject.GetComponentsInChildren<MonoBehaviour>();
        foreach (var s in scripts) s.enabled = false;

        ApplyMaterialToProxy(_proxyObject, validMaterial);
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

        // Lakukan Raycast dari Mouse ke Plane gridOrigin
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane gridPlane = new Plane(_targetStatsManager.gridOrigin.up, _targetStatsManager.gridOrigin.position);
        
        if (gridPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);

            // Ubah dari World Space ke Local Space relative terhadap gridOrigin
            Vector3 localHit = _targetStatsManager.gridOrigin.InverseTransformPoint(hitPoint);

            // Hitung grid X dan Y (1 unit cellSize = 1 grid)
            float cellSize = _targetStatsManager.cellSize;
            
            int effectiveWidth = (_currentAngle == 90 || _currentAngle == 270) ? _currentTemplate.height : _currentTemplate.width;
            int effectiveHeight = (_currentAngle == 90 || _currentAngle == 270) ? _currentTemplate.width : _currentTemplate.height;

            // Offset koordinat agar mouse berada di tengah modul
            int gridX = Mathf.FloorToInt((localHit.x / cellSize) - (effectiveWidth / 2f) + 0.5f);
            int gridY = Mathf.FloorToInt((localHit.z / cellSize) - (effectiveHeight / 2f) + 0.5f);

            _lastValidGridPos = new Vector2Int(gridX, gridY);

            // Cek apakah valid
            _canPlace = _targetStatsManager.IsAreaFree(_lastValidGridPos, _currentTemplate.width, _currentTemplate.height, _currentAngle);

            // Update posisi dan rotasi Proxy
            if (_proxyObject != null)
            {
                float offsetX = (gridX + effectiveWidth / 2f) * cellSize;
                float offsetZ = (gridY + effectiveHeight / 2f) * cellSize;
                Vector3 snappedLocalPos = new Vector3(offsetX, 0f, offsetZ);

                _proxyObject.transform.position = _targetStatsManager.gridOrigin.TransformPoint(snappedLocalPos);
                _proxyObject.transform.rotation = _targetStatsManager.gridOrigin.rotation * Quaternion.Euler(0f, _currentAngle, 0f);

                // Update warna material proxy
                ApplyMaterialToProxy(_proxyObject, _canPlace ? validMaterial : invalidMaterial);
            }
        }
        else
        {
            // Mouse tidak mengenai area lantai grid
            _canPlace = false;
            if (_proxyObject != null)
            {
                ApplyMaterialToProxy(_proxyObject, invalidMaterial);
            }
        }

        // Handle Drop (Lepas Klik Kiri)
        if (Input.GetMouseButtonUp(0))
        {
            if (_canPlace && _lastValidGridPos.x != -1)
            {
                // Install modul ke grid
                bool success = _targetStatsManager.InstallModule(_currentTemplate, _lastValidGridPos, _currentAngle);

                // Auto-save setiap kali berhasil memasang modul
                if (success)
                {
                    string vehicleName = _targetStatsManager.gameObject.name;
                    GridSaveSystem.SaveGrid(vehicleName, _targetStatsManager);
                }
            }
            // Selesai drag (berhasil atau gagal)
            CancelDrag();
        }
    }

    private void CancelDrag()
    {
        _isDragging = false;
        _currentTemplate = null;
        _targetStatsManager = null;
        if (_proxyObject != null)
        {
            Destroy(_proxyObject);
        }
    }

    private void ApplyMaterialToProxy(GameObject obj, Material mat)
    {
        if (mat == null) return;
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
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
