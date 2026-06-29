using UnityEngine;
using UnityEngine.EventSystems;

namespace Weapons
{
    public class TetrisGridUI : MonoBehaviour, IDropHandler
    {
        [Header("Referensi Data")]
        public VehicleData currentVehicle;
        public WeaponDatabase weaponDatabase;
        public GameObject draggablePrefab;
        
        [Header("Pengaturan UI")]
        [Tooltip("Ukuran pixel untuk 1x1 grid (Misal: 50 artinya kotak 1x1 = 50x50 pixel)")]
        public float cellSize = 50f; 

        public InventoryGridBackend Backend { get; private set; }
        private RectTransform _rectTransform;

        void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        void Start()
        {
            if (currentVehicle != null)
            {
                InitializeGrid(currentVehicle);
                LoadVisualGrid();
            }
        }

        public void InitializeGrid(VehicleData vehicle)
        {
            currentVehicle = vehicle;
            Backend = new InventoryGridBackend(vehicle.gridSizeX, vehicle.gridSizeY);
            _rectTransform.sizeDelta = new Vector2(vehicle.gridSizeX * cellSize, vehicle.gridSizeY * cellSize);

            if (weaponDatabase == null)
            {
                LoadoutManager loadout = FindObjectOfType<LoadoutManager>();
                if (loadout != null) weaponDatabase = loadout.weaponDatabase;
            }
        }

        public void RefreshVisualGrid()
        {
            ClearVisualItems();
            LoadVisualGrid();
        }

        private void ClearVisualItems()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private void LoadVisualGrid()
        {
            if (weaponDatabase == null || draggablePrefab == null) return;

            // Minta backend me-load data (ini hanya menyusun logika array 2D-nya)
            Backend.LoadFromPlayerPrefs(currentVehicle.vehicleName, weaponDatabase);

            // Sekarang, kita wujudkan visual UI-nya dari data yang di-load tersebut
            foreach (var item in Backend.ItemsInGrid)
            {
                GameObject newObj = Instantiate(draggablePrefab, transform);
                DraggableWeaponUI draggableUI = newObj.GetComponent<DraggableWeaponUI>();
                
                if (draggableUI != null)
                {
                    draggableUI.InitializeFromSave(item.Data, cellSize, item, this);
                    
                    RectTransform rt = newObj.GetComponent<RectTransform>();
                    rt.anchoredPosition = GetLocalPosition(item.GridX, item.GridY);
                }
            }
        }

        public void SaveGrid()
        {
            if (currentVehicle != null && Backend != null)
            {
                Backend.SaveToPlayerPrefs(currentVehicle.vehicleName);

                VehicleWeaponManager weaponManager = null;
                LoadoutManager loadout = FindObjectOfType<LoadoutManager>();
                if (loadout != null) weaponManager = loadout.PreviewWeaponManager;
                if (weaponManager == null) weaponManager = FindObjectOfType<VehicleWeaponManager>();

                if (weaponManager != null)
                {
                    weaponManager.RefreshWeapons();
                }
            }
        }

        public bool TryGetGridCoordsFromPointer(PointerEventData eventData, Vector2 itemSizeUi, out Vector2Int coords)
        {
            return TryGetGridCoordsFromScreen(eventData.position, itemSizeUi, eventData.pressEventCamera, out coords);
        }

        /// <summary>
        /// Konversi posisi layar ke koordinat grid. Pakai sudut kiri-atas item UI (pivot 0,1).
        /// </summary>
        public bool TryGetGridCoordsFromScreen(Vector2 screenPoint, Vector2 itemSizeUi, Camera uiCamera, out Vector2Int coords)
        {
            coords = default;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform,
                screenPoint,
                uiCamera,
                out Vector2 localPointer);

            coords = GetGridCoordinates(localPointer);
            return true;
        }

        /// <summary>
        /// Baca koordinat grid langsung dari posisi visual item yang sedang di-drag di UI.
        /// Ini yang dipakai supaya preview 3D selalu sinkron dengan icon di panel Tetris.
        /// </summary>
        public bool TryGetGridCoordsFromDraggable(RectTransform draggable, Camera uiCamera, out Vector2Int coords)
        {
            coords = default;
            if (draggable == null) return false;

            Vector3[] corners = new Vector3[4];
            draggable.GetWorldCorners(corners);

            // corners[1] = kiri-atas (sesuai pivot 0,1 pada DraggableWeaponUI)
            return TryGetGridCoordsFromScreen(corners[1], draggable.sizeDelta, uiCamera, out coords);
        }

        public Vector2Int ClampGridCoords(Vector2Int coords, InventoryItem item)
        {
            if (item == null || Backend == null) return coords;

            int maxX = Backend.SizeX - item.Width;
            int maxY = Backend.SizeY - item.Height;

            return new Vector2Int(
                Mathf.Clamp(coords.x, 0, Mathf.Max(0, maxX)),
                Mathf.Clamp(coords.y, 0, Mathf.Max(0, maxY)));
        }

        public bool IsPointerOverGrid(Vector2 screenPoint, Camera uiCamera)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, screenPoint, uiCamera);
        }

        public bool OverlapsDraggable(RectTransform draggable, Camera uiCamera)
        {
            if (draggable == null) return false;

            Vector3[] corners = new Vector3[4];
            draggable.GetWorldCorners(corners);

            foreach (Vector3 corner in corners)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, corner, uiCamera))
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanPlaceAt(InventoryItem item, int gridX, int gridY)
        {
            return Backend != null && Backend.CanPlaceItem(item, gridX, gridY);
        }

        public Vector2Int GetGridCoordinates(Vector2 localPointerPosition)
        {
            int x = Mathf.FloorToInt(localPointerPosition.x / cellSize);
            int y = Mathf.FloorToInt(-localPointerPosition.y / cellSize);
            return new Vector2Int(x, y);
        }

        public Vector2 GetLocalPosition(int x, int y)
        {
            return new Vector2(x * cellSize, -y * cellSize);
        }

        public void OnDrop(PointerEventData eventData)
        {
        }
    }
}
