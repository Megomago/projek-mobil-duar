using System.Collections.Generic;
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
        if (!showGizmos) return;

        ModuleTemplate tpl = template;
#if UNITY_EDITOR
        // Kalau template belum di-assign (mis. prefab baru), coba cari otomatis dari
        // ModularWeapon + WeaponData. Jangan assign balik ke field serialized supaya
        // prefab tidak ikut dirty tiap repaint Scene view.
        if (tpl == null)
        {
            var weapon = GetComponent<Weapons.ModularWeapon>();
            if (weapon != null) tpl = FindTemplateForWeapon(weapon);
        }
#endif
        if (tpl == null) return;

        DrawGrid(transform, tpl, cellSize, baseColor, clearanceColor, yOffset, xRay);
    }

    /// <summary>
    /// Gambar footprint grid sebuah modul/senjata di Scene view.
    /// Dipakai oleh komponen ini dan oleh auto-drawer [DrawGizmo] untuk prefab
    /// yang belum punya ModuleGizmoVisualizer.
    /// </summary>
    public static void DrawGrid(Transform anchor, ModuleTemplate tpl, float cellSize,
        Color baseCol, Color clearanceCol, float yOffset, bool xRay)
    {
        if (anchor == null || tpl == null) return;
        if (cellSize <= 0f) cellSize = 0.25f;

#if UNITY_EDITOR
        // Simpan zTest agar tidak bocor ke Handles lain. Always = tembus mesh seperti GridOverlay saat Play.
        var prevZTest = UnityEditor.Handles.zTest;
        if (xRay) UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
#endif

        // Penanda pivot — biar ketahuan kalau kotak muncul jauh dari mesh (pivot tidak di tengah)
        Gizmos.color = baseCol;
        Gizmos.DrawSphere(anchor.TransformPoint(new Vector3(0f, yOffset, 0f)), cellSize * 0.08f);

        // Gambar Base
        Gizmos.color = baseCol;
        for (int x = 0; x < tpl.width; x++)
        {
            for (int y = 0; y < tpl.height; y++)
            {
                // Asumsi pivot (0,0) ada di pojok kiri bawah, sama seperti perhitungan grid
                float offsetX = (x + 0.5f) * cellSize;
                float offsetZ = (y + 0.5f) * cellSize;

                // Geser ke tengah (menyesuaikan origin proxy saat drag and drop)
                Vector3 localPos = new Vector3(offsetX, yOffset, offsetZ);
                localPos.x -= (tpl.width * cellSize) / 2f;
                localPos.z -= (tpl.height * cellSize) / 2f;

                Vector3 worldPos = anchor.TransformPoint(localPos);
                Vector3 wireSize = new Vector3(cellSize, 0.02f, cellSize);
                Gizmos.DrawWireCube(worldPos, wireSize);

                // Gambar isi kubus agak transparan
                Color fill = baseCol;
                fill.a *= 0.3f;
                Gizmos.color = fill;
                Gizmos.DrawCube(worldPos, new Vector3(cellSize, 0.01f, cellSize));
                Gizmos.color = baseCol;

#if UNITY_EDITOR
                if (xRay)
                {
                    UnityEditor.Handles.color = baseCol;
                    UnityEditor.Handles.DrawWireCube(worldPos, wireSize);
                }
#endif
            }
        }

        // Gambar Clearance
        if (tpl.enableClearance)
        {
            Gizmos.color = clearanceCol;

            int minX = -tpl.clearanceLeft;
            int maxX = tpl.width - 1 + tpl.clearanceRight;
            int minY = -tpl.clearanceBack;
            int maxY = tpl.height - 1 + tpl.clearanceFront;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    // Lewati kotak base
                    bool isBase = (x >= 0 && x < tpl.width) && (y >= 0 && y < tpl.height);
                    if (isBase) continue;

                    float offsetX = (x + 0.5f) * cellSize;
                    float offsetZ = (y + 0.5f) * cellSize;

                    Vector3 localPos = new Vector3(offsetX, yOffset, offsetZ);
                    localPos.x -= (tpl.width * cellSize) / 2f;
                    localPos.z -= (tpl.height * cellSize) / 2f;

                    Vector3 worldPos = anchor.TransformPoint(localPos);
                    Vector3 wireSize = new Vector3(cellSize, 0.02f, cellSize);
                    Gizmos.DrawWireCube(worldPos, wireSize);

                    Color fill = clearanceCol;
                    fill.a *= 0.3f;
                    Gizmos.color = fill;
                    Gizmos.DrawCube(worldPos, new Vector3(cellSize, 0.01f, cellSize));
                    Gizmos.color = clearanceCol;

#if UNITY_EDITOR
                    if (xRay)
                    {
                        UnityEditor.Handles.color = clearanceCol;
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

#if UNITY_EDITOR
    // ── Auto-drawer ──
    // Supaya prefab senjata baru otomatis dapat grid di Scene / Prefab Mode tanpa
    // harus memasang ModuleGizmoVisualizer manual. Template dicari dari WeaponData.
    private static readonly Dictionary<Weapons.WeaponData, ModuleTemplate> _weaponTemplateCache =
        new Dictionary<Weapons.WeaponData, ModuleTemplate>();

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
    private static void AutoDrawWeaponGrid(Weapons.ModularWeapon weapon, GizmoType gizmoType)
    {
        if (weapon == null) return;

        // Prefab yang sudah punya ModuleGizmoVisualizer ditangani oleh komponen itu sendiri.
        if (weapon.GetComponentInChildren<ModuleGizmoVisualizer>(true) != null) return;

        ModuleTemplate tpl = FindTemplateForWeapon(weapon);
        if (tpl == null) return;

        DrawGrid(weapon.transform, tpl, 0.25f,
            new Color(1f, 0f, 0f, 0.8f),
            new Color(1f, 0.5f, 0f, 0.8f),
            0.06f, true);
    }

    private static ModuleTemplate FindTemplateForWeapon(Weapons.ModularWeapon weapon)
    {
        if (weapon == null || weapon.weaponData == null) return null;

        if (_weaponTemplateCache.TryGetValue(weapon.weaponData, out var cached) && cached != null)
            return cached;

        RebuildWeaponTemplateCache();

        _weaponTemplateCache.TryGetValue(weapon.weaponData, out var tpl);
        return tpl;
    }

    private static void RebuildWeaponTemplateCache()
    {
        _weaponTemplateCache.Clear();

        string[] guids = AssetDatabase.FindAssets("t:ModuleTemplate");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModuleTemplate tpl = AssetDatabase.LoadAssetAtPath<ModuleTemplate>(path);
            if (tpl == null || tpl.weaponData == null) continue;
            if (!_weaponTemplateCache.ContainsKey(tpl.weaponData))
                _weaponTemplateCache.Add(tpl.weaponData, tpl);
        }
    }
#endif
}
