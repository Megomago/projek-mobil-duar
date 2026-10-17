using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [Tooltip("Tinggi kotak di atas pivot. Naikkan kalau kotak ketanam di dalam mesh (tidak kelihatan di Edit tapi kelihatan saat Play).")]
    public float yOffset = 0.06f;
    [Tooltip("Kalau true, kotak digambar tembus mesh di Scene view (seperti shader GridOverlay saat Play).")]
    public bool xRay = true;

    private void OnDrawGizmos()
    {
        if (!showGizmos || template == null) return;

#if UNITY_EDITOR
        // Simpan zTest agar tidak bocor ke Handles lain. Always = tembus mesh seperti GridOverlay saat Play.
        var prevZTest = UnityEditor.Handles.zTest;
        if (xRay) UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
#endif

        // Penanda pivot — biar ketahuan kalau kotak muncul jauh dari mesh (pivot tidak di tengah)
        Gizmos.color = baseColor;
        Gizmos.DrawSphere(transform.TransformPoint(new Vector3(0f, yOffset, 0f)), cellSize * 0.08f);

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
                Vector3 localPos = new Vector3(offsetX, yOffset, offsetZ);
                localPos.x -= (template.width * cellSize) / 2f;
                localPos.z -= (template.height * cellSize) / 2f;

                Vector3 worldPos = transform.TransformPoint(localPos);
                Vector3 wireSize = new Vector3(cellSize, 0.02f, cellSize);
                Gizmos.DrawWireCube(worldPos, wireSize);
                
                // Gambar isi kubus agak transparan
                Color fill = baseColor;
                fill.a *= 0.3f;
                Gizmos.color = fill;
                Gizmos.DrawCube(worldPos, new Vector3(cellSize, 0.01f, cellSize));
                Gizmos.color = baseColor;

#if UNITY_EDITOR
                if (xRay)
                {
                    UnityEditor.Handles.color = baseColor;
                    UnityEditor.Handles.DrawWireCube(worldPos, wireSize);
                }
#endif
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
                    
                    Vector3 localPos = new Vector3(offsetX, yOffset, offsetZ);
                    localPos.x -= (template.width * cellSize) / 2f;
                    localPos.z -= (template.height * cellSize) / 2f;

                    Vector3 worldPos = transform.TransformPoint(localPos);
                    Vector3 wireSize = new Vector3(cellSize, 0.02f, cellSize);
                    Gizmos.DrawWireCube(worldPos, wireSize);

                    Color fill = clearanceColor;
                    fill.a *= 0.3f;
                    Gizmos.color = fill;
                    Gizmos.DrawCube(worldPos, new Vector3(cellSize, 0.01f, cellSize));
                    Gizmos.color = clearanceColor;

#if UNITY_EDITOR
                    if (xRay)
                    {
                        UnityEditor.Handles.color = clearanceColor;
                        UnityEditor.Handles.DrawWireCube(worldPos, wireSize);
                    }
#endif
                }
            }
        }

#if UNITY_EDITOR
        UnityEditor.Handles.zTest = prevZTest;
#endif
    }
}
