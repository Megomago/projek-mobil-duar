using UnityEngine;
using System.Collections.Generic;

public readonly struct GridKey : System.IEquatable<GridKey>
{
    public readonly string zone;
    public readonly int x;
    public readonly int y;

    public GridKey(string zone, int x, int y)
    {
        this.zone = zone;
        this.x = x;
        this.y = y;
    }

    public bool Equals(GridKey other)
    {
        return x == other.x && y == other.y && zone == other.zone;
    }

    public override bool Equals(object obj)
    {
        return obj is GridKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        int hash = zone != null ? zone.GetHashCode() : 0;
        hash = (hash * 397) ^ x;
        hash = (hash * 397) ^ y;
        return hash;
    }

    public static bool operator ==(GridKey a, GridKey b) => a.Equals(b);
    public static bool operator !=(GridKey a, GridKey b) => !a.Equals(b);
}

[RequireComponent(typeof(VehicleStatsManager))]
[ExecuteAlways]
public class GridVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    [Tooltip("Sprite untuk 1 kotak grid kosong (ukuran disesuaikan dengan cellSize, misal 25x25cm)")]
    public Sprite cellSprite;
    [Tooltip("Warna grid saat kosong")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.3f);
    [Tooltip("Warna grid saat terisi modul")]
    public Color occupiedColor = new Color(1f, 0f, 0f, 0.4f);
    [Tooltip("Warna grid saat disorot/preview pas di-drag")]
    public Color previewColor = new Color(1f, 1f, 0f, 0.4f);
    [Tooltip("Warna grid untuk area clearance (zona tembak/laras)")]
    public Color clearanceColor = new Color(1f, 0.5f, 0f, 0.4f);
    
    [Tooltip("Material khusus grid. Kosongkan untuk pakai shader GridOverlay otomatis (agar tembus mesh).")]
    public Material gridMaterial;

    [Header("Sorting")]
    public string sortingLayerName = "Default"; 
    public int sortingOrder = 0;

    private VehicleStatsManager _statsManager;
    private List<GameObject> _cellObjects = new List<GameObject>();
    private Dictionary<GridKey, SpriteRenderer> _cellRenderers = new Dictionary<GridKey, SpriteRenderer>();

    // Material grid overlay dibagi antar semua instance (dibuat sekali, tidak bocor per spawn)
    private static Material _cachedOverlayMaterial;
    private bool _isGridVisible = false;

    private readonly List<Vector2Int> _clearanceCells = new List<Vector2Int>();
    private readonly List<Vector2Int> _occupiedCells = new List<Vector2Int>();
    private readonly List<Vector2Int> _previewBaseCells = new List<Vector2Int>();
    private readonly List<Vector2Int> _previewClearanceCells = new List<Vector2Int>();

    private void Awake()
    {
        _statsManager = GetComponent<VehicleStatsManager>();
    }

    private void OnDrawGizmos()
    {
        if (_statsManager == null) _statsManager = GetComponent<VehicleStatsManager>();
        if (_statsManager == null) return;
        if (cellSprite == null) return;

        if (_statsManager.gridZones == null) return;

        Color gizColor = normalColor;
        gizColor.a = Mathf.Clamp01(gizColor.a);
        Gizmos.color = gizColor;

        foreach (var zone in _statsManager.gridZones)
        {
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
                    Vector3 worldPos = zone.origin.TransformPoint(new Vector3(offsetX, 0f, offsetZ));

                    // Draw a thin wire cube on the plane to represent the cell
                    Vector3 size = new Vector3(cellSize, 0.01f, cellSize);
                    Gizmos.DrawWireCube(worldPos, size);
                }
            }
        }
    }

    private void Start()
    {
        GenerateGridVisuals();
        ToggleGrid(false); // Default sembunyi sampai masuk mode inventaris
    }
    
    private void Update()
    {
        if (Application.isPlaying && _isGridVisible)
        {
            UpdateGridColors();
        }
    }

    private void GenerateGridVisuals()
    {
        if (_statsManager == null) return;

        if (cellSprite == null)
        {
            Debug.LogWarning("[GridVisualizer] Sprite grid belum di-assign!");
            return;
        }

        // Hapus jika ada sisa grid runtime (hanya saat playing)
        if (Application.isPlaying)
        {
            foreach (var obj in _cellObjects)
            {
                if (obj != null) Destroy(obj);
            }
            _cellObjects.Clear();
            _cellRenderers.Clear();

            if (_statsManager.gridZones == null) return;

            // Buat grid visual runtime untuk setiap zona (hologram sprites)
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
                        // DontSave: sel runtime TIDAK BOLEH ikut ke-save ke scene/prefab.
                        // Kalau user drag mobil ke Project (bikin prefab) atau save scene,
                        // sel-sel ini ikut kebawa = prefab bengkak + visual dobel.
                        cellObj.hideFlags = HideFlags.DontSave;
                        cellObj.transform.SetParent(zone.origin);
                        cellObj.transform.localPosition = localPos;
                        cellObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                        SpriteRenderer sr = cellObj.AddComponent<SpriteRenderer>();
                        sr.sprite = cellSprite;
                        sr.color = normalColor;
                        sr.sortingLayerName = sortingLayerName;
                        sr.sortingOrder = sortingOrder;

                        if (gridMaterial != null)
                        {
                            // sharedMaterial: JANGAN .material (itu nge-clone per cell = bocor + jebol batching).
                            // Tint per-cell tetap jalan via sr.color (vertex color).
                            sr.sharedMaterial = gridMaterial;
                        }
                        else
                        {
                            // Gunakan shader Custom/GridOverlay yang bikin 100% nembus semua mesh.
                            // Material di-CACHE static — jangan new Material tiap spawn (bocor GPU memory).
                            if (_cachedOverlayMaterial == null)
                            {
                                Shader overlayShader = Shader.Find("Custom/GridOverlay");
                                if (overlayShader != null)
                                    _cachedOverlayMaterial = new Material(overlayShader);
                            }
                            if (_cachedOverlayMaterial != null)
                                sr.sharedMaterial = _cachedOverlayMaterial;
                        }

                        if (cellSprite.bounds.size.x > 0 && cellSprite.bounds.size.y > 0)
                        {
                            float scaleX = cellSize / cellSprite.bounds.size.x;
                            float scaleY = cellSize / cellSprite.bounds.size.y;
                            cellObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                        }

                        _cellObjects.Add(cellObj);
                        var key = new GridKey(zone.zoneName, x, y);
                        if (_cellRenderers.ContainsKey(key))
                        {
                            Debug.LogError($"[GridVisualizer] KEY KEMBAR {zone.zoneName}({x},{y})! Dua zona pakai nama SAMA — rename zona biar unik, kalau tidak warna grid ketuker zona.");
                        }
                        _cellRenderers[key] = sr;
                    }
                }
            }
        }
    }

    public void UpdateGridColors()
    {
        if (_statsManager == null || _statsManager.installedModules == null) return;

        // 1. Reset semua cell ke normalColor
        foreach (var kvp in _cellRenderers)
        {
            if (kvp.Value != null) kvp.Value.color = normalColor;
        }

        // 1.5. Warnai cell Clearance (supaya kalau ketimpa occupiedColor, occupiedColor yang menang)
        foreach (var mod in _statsManager.installedModules)
        {
            if (mod.moduleTemplate == null || !mod.moduleTemplate.enableClearance) continue;
            
            _statsManager.GetClearanceCells(mod.gridPosition, mod.moduleTemplate, mod.rotationAngle, _clearanceCells);
            foreach (var pos in _clearanceCells)
            {
                var key = new GridKey(mod.zoneName, pos.x, pos.y);
                if (_cellRenderers.TryGetValue(key, out SpriteRenderer sr) && sr != null)
                {
                    // Hanya timpa jika warnanya masih normal (biar nggak nimpa kotak merah base)
                    if (sr.color == normalColor)
                    {
                        sr.color = clearanceColor;
                    }
                }
            }
        }

        // 2. Warnai cell yang terisi modul fisik (Base) dengan occupiedColor
        foreach (var mod in _statsManager.installedModules)
        {
            if (mod.moduleTemplate == null) continue;

            _statsManager.GetOccupiedCells(mod.gridPosition, mod.moduleTemplate.width, mod.moduleTemplate.height, mod.rotationAngle, _occupiedCells);
            
            foreach (var pos in _occupiedCells)
            {
                var key = new GridKey(mod.zoneName, pos.x, pos.y);
                if (_cellRenderers.TryGetValue(key, out SpriteRenderer sr) && sr != null)
                {
                    sr.color = occupiedColor;
                }
            }
        }

        // 3. Warnai kotak yang sedang disorot/di-drag saat ini
        if (InventoryDragDropManager.Instance != null && InventoryDragDropManager.Instance.IsDragging)
        {
            var mgr = InventoryDragDropManager.Instance;
            if (mgr.CurrentTemplate != null && !string.IsNullOrEmpty(mgr.CurrentZoneName) && mgr.CurrentGridPos.x != -1)
            {
                _statsManager.GetOccupiedCells(mgr.CurrentGridPos, mgr.CurrentTemplate.width, mgr.CurrentTemplate.height, mgr.CurrentAngle, _previewBaseCells);
                _statsManager.GetClearanceCells(mgr.CurrentGridPos, mgr.CurrentTemplate, mgr.CurrentAngle, _previewClearanceCells);
                
                // Kuning kalau areanya kosong/bisa dipasang, Merah kalau nabrak/keluar batas
                Color highlightBaseColor = mgr.CanPlace ? previewColor : occupiedColor; 
                Color highlightClearanceColor = mgr.CanPlace ? clearanceColor : occupiedColor;
                
                // Gambar clearance preview dulu
                foreach (var pos in _previewClearanceCells)
                {
                    var key = new GridKey(mgr.CurrentZoneName, pos.x, pos.y);
                    if (_cellRenderers.TryGetValue(key, out SpriteRenderer sr) && sr != null)
                    {
                        sr.color = highlightClearanceColor;
                    }
                }

                // Gambar base preview (menimpa clearance preview kalau berpotongan, walau secara logika tak mungkin)
                foreach (var pos in _previewBaseCells)
                {
                    var key = new GridKey(mgr.CurrentZoneName, pos.x, pos.y);
                    if (_cellRenderers.TryGetValue(key, out SpriteRenderer sr) && sr != null)
                    {
                        sr.color = highlightBaseColor;
                    }
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
