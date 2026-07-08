using UnityEngine;
using UnityEngine.EventSystems;

public class ModuleSelectionManager : MonoBehaviour
{
    public static ModuleSelectionManager Instance;

    [Header("Settings")]
    [Tooltip("Material untuk outline (Gunakan shader outline/unlit)")]
    public Material outlineMaterial;
    
    [Tooltip("Layer khusus untuk modul yang terpasang biar raycast ga bocor.")]
    public LayerMask moduleLayer;

    [Tooltip("Panel inventory/edit mode. Selection cuma aktif kalo panel ini terbuka.")]
    public GameObject editModePanel;

    private PlacedModule _selectedModule;
    private GameObject _outlineObject;
    private VehicleStatsManager _currentVehicleManager;
    private Camera _mainCam;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        _mainCam = Camera.main;
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
    }

    private void HandleSelection()
    {
        Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, moduleLayer))
        {
            PlacedModule clickedModule = FindModuleByPrefab(hit.collider.gameObject);
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

    private PlacedModule FindModuleByPrefab(GameObject hitObject)
    {
        VehicleStatsManager manager = hitObject.GetComponentInParent<VehicleStatsManager>();
        if (manager == null) return null;

        _currentVehicleManager = manager;
        foreach (var mod in manager.installedModules)
        {
            if (mod.spawnedPrefab != null && IsChildOrSelf(hitObject.transform, mod.spawnedPrefab.transform))
            {
                return mod;
            }
        }
        return null;
    }

    private bool IsChildOrSelf(Transform child, Transform parent)
    {
        while (child != null)
        {
            if (child == parent) return true;
            child = child.parent;
        }
        return false;
    }

    private void SelectModule(PlacedModule module)
    {
        if (module == null || module.spawnedPrefab == null) return;

        // Bersihin seleksi lama dulu biar ga numpuk outline-nya
        if (_selectedModule != null)
        {
            DeselectModule();
        }

        _selectedModule = module;

        // Bikin objek outline (Tanpa matiin objek asli!)
        if (outlineMaterial != null)
        {
            CreateOutline(module.spawnedPrefab);
        }
    }

    private void CreateOutline(GameObject target)
    {
        // Clone objeknya langsung jadi child dari target
        _outlineObject = Instantiate(target, target.transform);
        _outlineObject.name = "SelectionOutline";
        
        // Reset posisi biar pas presisi di tengah target
        _outlineObject.transform.localPosition = Vector3.zero;
        _outlineObject.transform.localRotation = Quaternion.identity;
        _outlineObject.transform.localScale = Vector3.one * 1.03f; // Atur tebal tipisnya di sini

        // Hancurin komponen ga guna biar ga bentrok physics atau running script ganda
        foreach (var col in _outlineObject.GetComponentsInChildren<Collider>()) 
        {
            Destroy(col); 
        }
        
        foreach (var scr in _outlineObject.GetComponentsInChildren<MonoBehaviour>()) 
        {
            if (scr != null) Destroy(scr);
        }

        // Terapin material outline ke semua mesh renderer di kloningan
        foreach (var rend in _outlineObject.GetComponentsInChildren<Renderer>())
        {
            rend.material = outlineMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // Outline ga perlu nge-shadow
            rend.receiveShadows = false;
        }
    }

    private void DeselectModule()
    {   
        _selectedModule = null;
        _currentVehicleManager = null;
        
        if (_outlineObject != null)
        {
            Destroy(_outlineObject);
            _outlineObject = null;
        }
    }

    private void DeleteSelectedModule()
    {
        if (_selectedModule != null && _currentVehicleManager != null)
        {
            _currentVehicleManager.UninstallModule(_selectedModule);
            DeselectModule();
        }
    }
}