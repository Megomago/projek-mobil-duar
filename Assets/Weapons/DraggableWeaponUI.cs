using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Weapons
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class DraggableWeaponUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public WeaponData weaponData;
        public InventoryItem BackendItem { get; private set; }

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        
        private TetrisGridUI _currentGrid;
        
        private int _originalGridX;
        private int _originalGridY;
        private bool _originalRotated;
        private TetrisGridUI _previousGrid;

        private bool _isDragging = false;
        private Canvas _parentCanvas;
        private VehicleWeaponManager _previewManager;
        private TetrisGridUI _activeGrid;
        private Camera _uiCamera;

        void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _parentCanvas = GetComponentInParent<Canvas>();
        }

        public void Initialize(WeaponData data, float cellSize)
        {
            weaponData = data;
            BackendItem = new InventoryItem(data);

            _rectTransform.sizeDelta = new Vector2(data.gridWidth * cellSize, data.gridHeight * cellSize);
            _rectTransform.pivot = new Vector2(0, 1);
            ApplyWeaponIcon(data);
        }

        public void InitializeFromSave(WeaponData data, float cellSize, InventoryItem item, TetrisGridUI grid)
        {
            weaponData = data;
            BackendItem = item;
            _currentGrid = grid;

            if (item.IsRotated)
            {
                _rectTransform.sizeDelta = new Vector2(data.gridHeight * cellSize, data.gridWidth * cellSize);
                if (transform.childCount > 0)
                {
                    Transform icon = transform.GetChild(0);
                    icon.localRotation = Quaternion.Euler(0, 0, -90);
                }
            }
            else
            {
                _rectTransform.sizeDelta = new Vector2(data.gridWidth * cellSize, data.gridHeight * cellSize);
                if (transform.childCount > 0)
                {
                    Transform icon = transform.GetChild(0);
                    icon.localRotation = Quaternion.identity;
                }
            }
            
            _rectTransform.pivot = new Vector2(0, 1);
            ApplyWeaponIcon(data);
        }

        private void ApplyWeaponIcon(WeaponData data)
        {
            if (transform.childCount == 0 || data == null) return;

            Image icon = transform.GetChild(0).GetComponent<Image>();
            if (icon == null) return;

            icon.sprite = data.weaponIcon;
            icon.preserveAspect = true;
            icon.gameObject.SetActive(data.weaponIcon != null);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _uiCamera = eventData.pressEventCamera != null
                ? eventData.pressEventCamera
                : _parentCanvas != null ? _parentCanvas.worldCamera : null;

            if (_currentGrid != null && _currentGrid.Backend != null)
            {
                _originalGridX = BackendItem.GridX;
                _originalGridY = BackendItem.GridY;
                _originalRotated = BackendItem.IsRotated;
                _previousGrid = _currentGrid;

                _currentGrid.Backend.RemoveItem(BackendItem);
                _currentGrid = null;
            }
            else
            {
                _previousGrid = null;
            }

            transform.SetAsLastSibling();
            _canvasGroup.blocksRaycasts = false;

            _activeGrid = ResolveActiveGrid();
            _previewManager = ResolvePreviewManager();

            if (_previewManager != null)
            {
                int startX = _previousGrid != null ? _originalGridX : 0;
                int startY = _previousGrid != null ? _originalGridY : 0;

                _previewManager.BeginDragPreview(
                    weaponData,
                    startX,
                    startY,
                    BackendItem.IsRotated,
                    hideExistingAtOrigin: _previousGrid != null);
            }

            RefreshDragPreview3D();
        }

        public void OnDrag(PointerEventData eventData)
        {
            _rectTransform.anchoredPosition += eventData.delta / _parentCanvas.scaleFactor;
        }

        void Update()
        {
            if (!_isDragging) return;

            if (Input.GetKeyDown(KeyCode.R))
            {
                RotateItem();
            }

            RefreshDragPreview3D();
        }

        private void RefreshDragPreview3D()
        {
            if (_previewManager == null || weaponData == null || BackendItem == null) return;

            if (_activeGrid == null)
            {
                _activeGrid = ResolveActiveGrid();
            }

            if (_activeGrid == null)
            {
                int x = _previousGrid != null ? _originalGridX : 0;
                int y = _previousGrid != null ? _originalGridY : 0;
                _previewManager.UpdateDragPreview(x, y, BackendItem.IsRotated, false, true);
                return;
            }

            Camera uiCamera = _uiCamera != null ? _uiCamera : _parentCanvas != null ? _parentCanvas.worldCamera : null;

            _activeGrid.TryGetGridCoordsFromDraggable(_rectTransform, uiCamera, out Vector2Int coords);
            coords = _activeGrid.ClampGridCoords(coords, BackendItem);

            bool overlapsGrid = _activeGrid.OverlapsDraggable(_rectTransform, uiCamera);
            bool isValid = overlapsGrid && _activeGrid.CanPlaceAt(BackendItem, coords.x, coords.y);

            _previewManager.UpdateDragPreview(coords.x, coords.y, BackendItem.IsRotated, isValid, true);
        }

        private TetrisGridUI ResolveActiveGrid()
        {
            LoadoutManager loadout = FindObjectOfType<LoadoutManager>();
            if (loadout != null)
            {
                TetrisGridUI grid = loadout.GetActiveTetrisGrid();
                if (grid != null) return grid;
            }

            return FindObjectOfType<TetrisGridUI>();
        }

        private VehicleWeaponManager ResolvePreviewManager()
        {
            LoadoutManager loadout = FindObjectOfType<LoadoutManager>();
            if (loadout != null && loadout.PreviewWeaponManager != null)
            {
                return loadout.PreviewWeaponManager;
            }

            return FindObjectOfType<VehicleWeaponManager>();
        }

        private void RotateItem()
        {
            BackendItem.Rotate();

            float temp = _rectTransform.sizeDelta.x;
            _rectTransform.sizeDelta = new Vector2(_rectTransform.sizeDelta.y, temp);

            if (transform.childCount > 0)
            {
                Transform icon = transform.GetChild(0);
                icon.Rotate(0, 0, -90); 
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;

            TetrisGridUI gridUI = _activeGrid != null ? _activeGrid : ResolveActiveGrid();

            if (gridUI != null && gridUI.OverlapsDraggable(_rectTransform, _uiCamera))
            {
                _previewManager?.EndDragPreview();
                TryPlaceInGrid(gridUI, eventData);
                _canvasGroup.blocksRaycasts = true;
                return;
            }

            _previewManager?.EndDragPreview();

            if (_previousGrid != null)
            {
                _previousGrid.SaveGrid();
            }

            _canvasGroup.blocksRaycasts = true;
            Destroy(gameObject);
        }

        private void TryPlaceInGrid(TetrisGridUI gridUI, PointerEventData eventData)
        {
            Camera uiCamera = _uiCamera != null ? _uiCamera : eventData.pressEventCamera;
            gridUI.TryGetGridCoordsFromDraggable(_rectTransform, uiCamera, out Vector2Int gridCoords);
            gridCoords = gridUI.ClampGridCoords(gridCoords, BackendItem);

            if (gridUI.Backend.PlaceItem(BackendItem, gridCoords.x, gridCoords.y))
            {
                _currentGrid = gridUI;
                transform.SetParent(gridUI.transform);
                _rectTransform.anchoredPosition = gridUI.GetLocalPosition(gridCoords.x, gridCoords.y);
                gridUI.SaveGrid();
            }
            else
            {
                if (_previousGrid != null)
                {
                    if (BackendItem.IsRotated != _originalRotated)
                    {
                        RotateItem();
                    }

                    _previousGrid.Backend.PlaceItem(BackendItem, _originalGridX, _originalGridY);
                    _currentGrid = _previousGrid;
                    transform.SetParent(_previousGrid.transform);
                    _rectTransform.anchoredPosition = _previousGrid.GetLocalPosition(_originalGridX, _originalGridY);
                    _previousGrid.SaveGrid();
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
