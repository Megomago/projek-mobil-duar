using UnityEngine;
using UnityEngine.EventSystems;

namespace Weapons
{
    /// <summary>
    /// Pasang / pindah senjata langsung ke grid 3D di mobil (KSP-style).
    /// Raycast ke kotak grid, bukan panel UI 2D mengambang.
    /// </summary>
    public class VehicleGrid3DPlacer : MonoBehaviour
    {
        [Header("Referensi")]
        public Camera placementCamera;
        public VehicleWeaponManager weaponManager;
        public VehicleGrid3D gridVisual;
        public Transform gridOrigin;
        public VehicleData vehicleData;
        public WeaponDatabase weaponDatabase;

        [Header("Input")]
        public float raycastDistance = 200f;
        public KeyCode rotateKey = KeyCode.R;
        public KeyCode cancelKey = KeyCode.Escape;

        public InventoryGridBackend Backend { get; private set; }

        private bool _isPlacing;
        private InventoryItem _placingItem;
        private int _lastGridX;
        private int _lastGridY;
        private bool _hasLastGrid;
        private bool _pickedFromWorld;

        public bool IsPlacing => _isPlacing;

        void Awake()
        {
            if (weaponManager == null) weaponManager = GetComponent<VehicleWeaponManager>();
            if (gridVisual == null) gridVisual = GetComponentInChildren<VehicleGrid3D>(true);
            if (gridOrigin == null && weaponManager != null) gridOrigin = weaponManager.gridOriginPivot;
        }

        public void SyncFromVehicleData()
        {
            if (vehicleData == null) return;

            if (weaponManager != null)
            {
                weaponManager.vehicleData = vehicleData;
                weaponManager.SyncGridSettings();
            }

            if (gridVisual != null)
            {
                gridVisual.vehicleData = vehicleData;
                gridVisual.SyncFromVehicleData();
            }
        }

        public void LoadGrid()
        {
            SyncFromVehicleData();

            if (vehicleData == null || weaponDatabase == null) return;

            Backend = new InventoryGridBackend(vehicleData.gridSizeX, vehicleData.gridSizeY);
            Backend.LoadFromPlayerPrefs(vehicleData.vehicleName, weaponDatabase);
            weaponManager?.RefreshWeapons();
        }

        public void SaveGrid()
        {
            if (vehicleData == null || Backend == null) return;

            Backend.SaveToPlayerPrefs(vehicleData.vehicleName);
            weaponManager?.EndDragPreview();
            weaponManager?.RefreshWeapons();
        }

        public void BeginPlacingFromCatalog(WeaponData data)
        {
            if (data == null) return;

            CancelPlacing();

            _placingItem = new InventoryItem(data);
            _pickedFromWorld = false;
            _isPlacing = true;
            _hasLastGrid = false;

            weaponManager?.BeginDragPreview(data, 0, 0, false, false);
        }

        public void EndPlacingFromCatalog()
        {
            if (!_isPlacing || _pickedFromWorld) return;

            if (_hasLastGrid && Backend != null && Backend.PlaceItem(_placingItem, _lastGridX, _lastGridY))
            {
                SaveGrid();
            }

            CancelPlacing();
        }

        public void CancelPlacing()
        {
            _isPlacing = false;
            _placingItem = null;
            _hasLastGrid = false;
            _pickedFromWorld = false;
            weaponManager?.EndDragPreview();
        }

        void Update()
        {
            if (!_isPlacing || _placingItem == null) return;

            if (Input.GetKeyDown(rotateKey))
            {
                _placingItem.Rotate();
            }

            if (Input.GetKeyDown(cancelKey) || Input.GetMouseButtonDown(1))
            {
                CancelPlacing();
                return;
            }

            if (TryGetGridUnderMouse(out int gridX, out int gridY))
            {
                _lastGridX = gridX;
                _lastGridY = gridY;
                _hasLastGrid = true;

                bool valid = Backend != null && Backend.CanPlaceItem(_placingItem, gridX, gridY);
                weaponManager?.UpdateDragPreview(gridX, gridY, _placingItem.IsRotated, valid, true);
            }
            else if (_hasLastGrid)
            {
                weaponManager?.UpdateDragPreview(_lastGridX, _lastGridY, _placingItem.IsRotated, false, true);
            }

            if (Input.GetMouseButtonUp(0) && !IsPointerOverBlockingUI())
            {
                if (_hasLastGrid && Backend != null && Backend.PlaceItem(_placingItem, _lastGridX, _lastGridY))
                {
                    SaveGrid();
                    CancelPlacing();
                }
            }
        }

        private bool TryGetGridUnderMouse(out int gridX, out int gridY)
        {
            gridX = 0;
            gridY = 0;

            if (gridOrigin == null || vehicleData == null) return false;

            Camera cam = placementCamera != null ? placementCamera : Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, raycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            VehicleGridCell3D cell = hit.collider.GetComponent<VehicleGridCell3D>();
            if (cell != null)
            {
                gridX = cell.gridX;
                gridY = cell.gridY;
                return true;
            }

            if (hit.collider.transform.IsChildOf(gridOrigin))
            {
                Vector3 localPoint = gridOrigin.InverseTransformPoint(hit.point);
                return VehicleGridUtility.TryLocalPointToGrid(
                    localPoint,
                    vehicleData.gridCellSize,
                    vehicleData.gridSizeX,
                    vehicleData.gridSizeY,
                    out gridX,
                    out gridY);
            }

            return false;
        }

        private static bool IsPointerOverBlockingUI()
        {
            if (EventSystem.current == null) return false;

            if (!EventSystem.current.IsPointerOverGameObject()) return false;

            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (RaycastResult result in results)
            {
                if (result.gameObject == null) continue;

                // Katalog senjata boleh; yang blok hanya panel grid 2D lama.
                if (result.gameObject.GetComponentInParent<TetrisGridUI>() != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
