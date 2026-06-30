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
        if (_statsManager == null || _statsManager.gridOrigin == null) return;
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

        int width = _statsManager.gridCapacity.x;
        int height = _statsManager.gridCapacity.y;
        float cellSize = _statsManager.cellSize;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Posisi tengah untuk cell ini
                float offsetX = (x + 0.5f) * cellSize;
                float offsetZ = (y + 0.5f) * cellSize;

                Vector3 localPos = new Vector3(offsetX, 0f, offsetZ);

                GameObject cellObj = new GameObject($"GridCell_{x}_{y}");
                cellObj.transform.SetParent(_statsManager.gridOrigin);
                
                // Set posisi dan rotasi relatif ke origin
                cellObj.transform.localPosition = localPos;
                cellObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Putar sprite agar tidur di lantai (sumbu X)

                SpriteRenderer sr = cellObj.AddComponent<SpriteRenderer>();
                sr.sprite = cellSprite;
                sr.color = normalColor;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder;

                // Sesuaikan skala sprite agar pas dengan cellSize (asumsi sprite PPU disesuaikan, atau paksa scale)
                // Jika sprite ukurannya 1x1 meter pada scale 1, maka:
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

    public void ToggleGrid(bool show)
    {
        _isGridVisible = show;
        foreach (var obj in _cellObjects)
        {
            if (obj != null) obj.SetActive(_isGridVisible);
        }
    }
}
