using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Weapons
{
    /// <summary>
    /// Item katalog senjata. Drag → pasang langsung ke grid 3D di mobil.
    /// </summary>
    public class WeaponGridItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Image iconImage;
        public TextMeshProUGUI nameText;
        
        [Tooltip("Fallback lama: prefab drag UI 2D (hanya dipakai jika use3DGridPlacement = false)")]
        public GameObject draggablePrefab; 

        private WeaponData _weaponData;
        private LoadoutManager _manager;
        private float _cellSize = 50f;

        private DraggableWeaponUI _spawnedDraggable;
        private Canvas _parentCanvas;

        void Awake()
        {
            _parentCanvas = GetComponentInParent<Canvas>();
        }

        public void Initialize(WeaponData data, LoadoutManager manager)
        {
            _weaponData = data;
            _manager = manager;

            if (_manager != null && _manager.inventoryUIPanel != null)
            {
                TetrisGridUI grid = _manager.inventoryUIPanel.GetComponentInChildren<TetrisGridUI>(true);
                if (grid != null) _cellSize = grid.cellSize;
            }

            if (draggablePrefab == null && _manager != null && _manager.inventoryUIPanel != null)
            {
                TetrisGridUI grid = _manager.inventoryUIPanel.GetComponentInChildren<TetrisGridUI>(true);
                if (grid != null) draggablePrefab = grid.draggablePrefab;
            }

            if (_weaponData != null)
            {
                if (nameText != null) nameText.text = _weaponData.weaponName;
                if (iconImage != null && _weaponData.weaponIcon != null)
                {
                    iconImage.sprite = _weaponData.weaponIcon;
                    iconImage.gameObject.SetActive(true);
                }
                else if (iconImage != null)
                {
                    iconImage.gameObject.SetActive(false);
                }
            }
            else
            {
                if (nameText != null) nameText.text = "Lepas";
                if (iconImage != null) iconImage.gameObject.SetActive(false);
            }
        }

        public void OnClick()
        {
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_weaponData == null) return;

            if (_manager != null && _manager.Use3DGridPlacement)
            {
                VehicleGrid3DPlacer placer = _manager.PreviewGridPlacer;
                if (placer != null)
                {
                    placer.BeginPlacingFromCatalog(_weaponData);
                    return;
                }
            }

            BeginLegacy2DDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_manager != null && _manager.Use3DGridPlacement) return;

            if (_spawnedDraggable != null)
            {
                _spawnedDraggable.OnDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_manager != null && _manager.Use3DGridPlacement)
            {
                VehicleGrid3DPlacer placer = _manager.PreviewGridPlacer;
                if (placer != null)
                {
                    placer.EndPlacingFromCatalog();
                }
                return;
            }

            EndLegacy2DDrag(eventData);
        }

        private void BeginLegacy2DDrag(PointerEventData eventData)
        {
            if (draggablePrefab == null) return;

            GameObject newObj = Instantiate(draggablePrefab, _parentCanvas.transform);
            
            _spawnedDraggable = newObj.GetComponent<DraggableWeaponUI>();
            if (_spawnedDraggable != null)
            {
                _spawnedDraggable.Initialize(_weaponData, _cellSize);
                
                RectTransform rt = newObj.GetComponent<RectTransform>();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentCanvas.GetComponent<RectTransform>(), 
                    eventData.position, 
                    eventData.pressEventCamera, 
                    out Vector2 localPoint
                );
                rt.anchoredPosition = localPoint;

                _spawnedDraggable.OnBeginDrag(eventData);
            }
        }

        private void EndLegacy2DDrag(PointerEventData eventData)
        {
            if (_spawnedDraggable != null)
            {
                _spawnedDraggable.OnEndDrag(eventData);
                
                if (_spawnedDraggable.transform.parent == _parentCanvas.transform)
                {
                    Destroy(_spawnedDraggable.gameObject);
                }
                
                _spawnedDraggable = null;
            }
        }
    }
}
