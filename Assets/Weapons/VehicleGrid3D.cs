using UnityEngine;

namespace Weapons
{
    [ExecuteAlways]
    public class VehicleGrid3D : MonoBehaviour
    {
        [Header("Referensi")]
        public Transform gridOrigin;
        public VehicleData vehicleData;

        [Header("Grid (X = lebar, Y = tinggi)")]
        [Min(1)] public int gridSizeX = 6;
        [Min(1)] public int gridSizeY = 4;
        [Min(0.01f)] public float gridCellSize = 0.25f;

        [Header("Tampilan")]
        public bool showGrid = true;
        public float visualHeight = 0.002f;
        [Range(0f, 1f)] public float cellFill = 0.92f;
        public Color cellColor = new Color(0.2f, 0.85f, 1f, 0.18f);
        public Color borderColor = new Color(0.2f, 0.85f, 1f, 0.55f);

        private Transform _visualRoot;
        private Material _cellMaterial;

        void OnEnable()
        {
            SyncFromVehicleData();
            RebuildVisual();
        }

        void OnValidate()
        {
            SyncFromVehicleData();
            RebuildVisual();
        }

        public void SyncFromVehicleData()
        {
            if (vehicleData == null) return;

            gridSizeX = vehicleData.gridSizeX;
            gridSizeY = vehicleData.gridSizeY;
            gridCellSize = vehicleData.gridCellSize;
        }

        public void RebuildVisual()
        {
            if (gridOrigin == null) return;

            ClearVisual();

            if (!showGrid) return;

            _visualRoot = new GameObject("GridVisual").transform;
            _visualRoot.SetParent(gridOrigin, false);

            EnsureMaterial();

            for (int y = 0; y < gridSizeY; y++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    CreateCell(x, y);
                }
            }
        }

        private void CreateCell(int x, int y)
        {
            Vector3 center = VehicleGridUtility.GridToLocalCenter(x, y, 1, 1, gridCellSize);
            center.y = visualHeight;

            GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cell.name = $"Cell_{x}_{y}";
            cell.transform.SetParent(_visualRoot, false);
            cell.transform.localPosition = center;
            cell.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            float filledSize = gridCellSize * cellFill;
            cell.transform.localScale = new Vector3(filledSize, filledSize, 1f);

            Collider oldCollider = cell.GetComponent<Collider>();
            if (oldCollider != null)
            {
                if (Application.isPlaying) Destroy(oldCollider);
                else DestroyImmediate(oldCollider);
            }

            BoxCollider box = cell.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 0.15f, 1f);
            box.center = Vector3.zero;

            VehicleGridCell3D cellData = cell.AddComponent<VehicleGridCell3D>();
            cellData.gridX = x;
            cellData.gridY = y;
            cellData.owner = this;

            MeshRenderer renderer = cell.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _cellMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void EnsureMaterial()
        {
            if (_cellMaterial != null) return;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return;

            _cellMaterial = new Material(shader);
            _cellMaterial.color = cellColor;
        }

        private void ClearVisual()
        {
            if (_visualRoot == null) return;

            if (Application.isPlaying) Destroy(_visualRoot.gameObject);
            else DestroyImmediate(_visualRoot.gameObject);

            _visualRoot = null;
        }

        void OnDrawGizmosSelected()
        {
            if (gridOrigin == null) return;

            Gizmos.matrix = gridOrigin.localToWorldMatrix;
            Gizmos.color = borderColor;

            float width = gridSizeX * gridCellSize;
            float height = gridSizeY * gridCellSize;

            for (int x = 0; x <= gridSizeX; x++)
            {
                float px = x * gridCellSize;
                Gizmos.DrawLine(new Vector3(px, 0f, 0f), new Vector3(px, 0f, -height));
            }

            for (int y = 0; y <= gridSizeY; y++)
            {
                float pz = -y * gridCellSize;
                Gizmos.DrawLine(new Vector3(0f, 0f, pz), new Vector3(width, 0f, pz));
            }
        }
    }
}
