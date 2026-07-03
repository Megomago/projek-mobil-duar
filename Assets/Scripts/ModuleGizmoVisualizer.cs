using UnityEngine;

[ExecuteAlways]
public class ModuleGizmoVisualizer : MonoBehaviour
{
    [Tooltip("Masukkan Module Template senjata/modul ini untuk melihat preview ukurannya via Gizmos")]
    public ModuleTemplate template;

    [Header("Settings")]
    public float cellSize = 0.25f; // Samakan dengan GridZone mobil
    public Color baseColor = new Color(1f, 0f, 0f, 0.8f); // Merah untuk base
    public Color clearanceColor = new Color(1f, 0.5f, 0f, 0.8f); // Oranye untuk clearance
    public bool showGizmos = true;

    private void OnDrawGizmos()
    {
        if (!showGizmos || template == null) return;

        // Gambar Base
        Gizmos.color = baseColor;
        for (int x = 0; x < template.width; x++)
        {
            for (int y = 0; y < template.height; y++)
            {
                // Asumsi pivot (0,0) ada di pojok kiri bawah, sama seperti perhitungan grid
                float offsetX = (x + 0.5f) * cellSize;
                float offsetZ = (y + 0.5f) * cellSize;
                
                // Geser ke tengah (menyesuaikan origin proxy saat drag and drop)
                Vector3 localPos = new Vector3(offsetX, 0.01f, offsetZ);
                localPos.x -= (template.width * cellSize) / 2f;
                localPos.z -= (template.height * cellSize) / 2f;

                Vector3 worldPos = transform.TransformPoint(localPos);
                Gizmos.DrawWireCube(worldPos, new Vector3(cellSize, 0.02f, cellSize));
                
                // Gambar isi kubus agak transparan
                Color fill = baseColor;
                fill.a *= 0.3f;
                Gizmos.color = fill;
                Gizmos.DrawCube(worldPos, new Vector3(cellSize, 0.01f, cellSize));
                Gizmos.color = baseColor;
            }
        }

        // Gambar Clearance
        if (template.enableClearance)
        {
            Gizmos.color = clearanceColor;
            
            int minX = -template.clearanceLeft;
            int maxX = template.width - 1 + template.clearanceRight;
            int minY = -template.clearanceBack;
            int maxY = template.height - 1 + template.clearanceFront;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    // Lewati kotak base
                    bool isBase = (x >= 0 && x < template.width) && (y >= 0 && y < template.height);
                    if (isBase) continue;

                    float offsetX = (x + 0.5f) * cellSize;
                    float offsetZ = (y + 0.5f) * cellSize;
                    
                    Vector3 localPos = new Vector3(offsetX, 0.01f, offsetZ);
                    localPos.x -= (template.width * cellSize) / 2f;
                    localPos.z -= (template.height * cellSize) / 2f;

                    Vector3 worldPos = transform.TransformPoint(localPos);
                    Gizmos.DrawWireCube(worldPos, new Vector3(cellSize, 0.02f, cellSize));

                    Color fill = clearanceColor;
                    fill.a *= 0.3f;
                    Gizmos.color = fill;
                    Gizmos.DrawCube(worldPos, new Vector3(cellSize, 0.01f, cellSize));
                    Gizmos.color = clearanceColor;
                }
            }
        }
    }
}
