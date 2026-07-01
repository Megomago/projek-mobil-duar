using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(VehicleStatsManager))]
public class GridVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    [Tooltip("Sprite untuk 1 kotak grid kosong (ukuran disesuaikan dengan cellSize, misal 25x25cm)")]
    public Sprite cellSprite;
    [Tooltip("Warna grid saat kosong")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.3f);
    
    [Header("Sorting")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    private VehicleStatsManager _statsManager;
    private List<GameObject> _cellObjects = new List<GameObject>();
    private bool _isGridVisible = false;

    private void Awake()
    {
        _statsManager = GetComponent<VehicleStatsManager>();
    }

    private void Start()
    {
        GenerateGridVisuals();
        ToggleGrid(false); // Default sembunyi sampai masuk mode inventaris
    }

    private void GenerateGridVisuals()
    {
        if (_statsManager == null) return;

        if (cellSprite == null)
        {
            Debug.LogWarning("[GridVisualizer] Sprite grid belum di-assign!");
            return;
        }

        // Hapus jika ada sisa grid lama
        foreach (var obj in _cellObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _cellObjects.Clear();

        if (_statsManager.gridZones == null) return;

        // Buat grid visual untuk setiap zona
        for (int z = 0; z < _statsManager.gridZones.Count; z++)
        {
            var zone = _statsManager.gridZones[z];
            if (zone == null || zone.origin == null) continue;

            int width = zone.capacity.x;
            int height = zone.capacity.y;
            float cellSize = (zone.cellSize > 0f) ? zone.cellSize : 0.25f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float offsetX = (x + 0.5f) * cellSize;
                    float offsetZ = (y + 0.5f) * cellSize;

                    Vector3 localPos = new Vector3(offsetX, 0f, offsetZ);

                    GameObject cellObj = new GameObject($"GridCell_{zone.zoneName}_{x}_{y}");
                    cellObj.transform.SetParent(zone.origin);
                    cellObj.transform.localPosition = localPos;
                    cellObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                    SpriteRenderer sr = cellObj.AddComponent<SpriteRenderer>();
                    sr.sprite = cellSprite;
                    sr.color = normalColor;
                    sr.sortingLayerName = sortingLayerName;
                    sr.sortingOrder = sortingOrder;

                    if (cellSprite.bounds.size.x > 0 && cellSprite.bounds.size.y > 0)
                    {
                        float scaleX = cellSize / cellSprite.bounds.size.x;
                        float scaleY = cellSize / cellSprite.bounds.size.y;
                        cellObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                    }

                    _cellObjects.Add(cellObj);
                }
            }
        }
    }

    public void ToggleGrid(bool show)
    {
        _isGridVisible = show;
        foreach (var obj in _cellObjects)
        {
            if (obj != null) obj.SetActive(_isGridVisible);
        }
    }
}
