using UnityEngine;
using UnityEngine.EventSystems;

public class ModuleSelectionManager : MonoBehaviour
{
    public static ModuleSelectionManager Instance;

    [Header("Settings")]
    [Tooltip("Material untuk outline (Bikin material Unlit/Transparent warna kuning/cyan)")]
    public Material outlineMaterial;
    
    [Tooltip("Layer khusus untuk modul yang terpasang. Biar raycast gak kena lantai!")]
    public LayerMask moduleLayer;

    private PlacedModule _selectedModule;
    private GameObject _outlineObject;
    private VehicleStatsManager _currentVehicleManager;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (InventoryDragDropManager.Instance != null && InventoryDragDropManager.Instance.IsDragging) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
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

        if (Input.GetKeyDown(KeyCode.X) && _selectedModule != null)
        {
            DeleteSelectedModule();
        }
    }

    private PlacedModule FindModuleByPrefab(GameObject hitObject)
    {
        VehicleStatsManager[] allManagers = FindObjectsOfType<VehicleStatsManager>();
        foreach (var manager in allManagers)
        {
            foreach (var mod in manager.installedModules)
            {
                if (mod.spawnedPrefab != null && IsChildOrSelf(hitObject.transform, mod.spawnedPrefab.transform))
                {
                    _currentVehicleManager = manager;
                    return mod;
                }
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

    // === BAGIAN INI YANG GUE PERBAIKI ===
    private void SelectModule(PlacedModule module)
    {
        if (_selectedModule == module) return; 

        // FIX: Jangan panggil DeselectModule() di sini! 
        // Itu bakal nge-reset _currentVehicleManager jadi null.
        // Cukup hancurkan outline lama aja secara manual.
        if (_outlineObject != null)
        {
            Destroy(_outlineObject);
            _outlineObject = null;
        }

        _selectedModule = module;

        if (outlineMaterial != null && module.spawnedPrefab != null)
        {
            _outlineObject = Instantiate(module.spawnedPrefab, module.spawnedPrefab.transform);
            _outlineObject.SetActive(false); 
            
            _outlineObject.name = "SelectionOutline";
            
            _outlineObject.transform.localPosition = Vector3.zero;
            _outlineObject.transform.localRotation = Quaternion.identity;
            _outlineObject.transform.localScale = Vector3.one * 1.05f; 
            
            foreach(var col in _outlineObject.GetComponentsInChildren<Collider>()) col.enabled = false;
            foreach(var scr in _outlineObject.GetComponentsInChildren<MonoBehaviour>()) scr.enabled = false;
            
            foreach(var rend in _outlineObject.GetComponentsInChildren<Renderer>())
            {
                rend.material = outlineMaterial;
            }
            
            _outlineObject.SetActive(true); 
        }
    }
    // === SELESAI PERBAIKAN ===

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