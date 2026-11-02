#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PumpjackTools
{
    /// <summary>
    /// Bikin collider compound (beberapa BoxCollider tumpuk) untuk tiap mesh
    /// di prefab pumpjack rusak. Hasilnya jauh lebih rapi dibanding satu
    /// BoxCollider besar yang dipasang otomatis saat runtime.
    ///
    /// Cara pakai:
    /// 1. Buka "Pumpjack broken asli.prefab" (double click), atau pilih
    ///    prefab-nya di Project window.
    /// 2. Pilih root prefab (GameObject paling atas).
    /// 3. Menu: Tools > Pumpjack > Compound Collider Builder.
    /// 4. Atur slider, klik "Generate Colliders".
    /// 5. Kalau di Prefab Mode, tekan Ctrl+S untuk menyimpan.
    ///
    /// Catatan: mesh FBX harus punya Read/Write enabled. Kalau belum,
    /// tool akan memberi error di Console.
    /// </summary>
    public class PumpjackColliderBuilder : EditorWindow
    {
        [SerializeField] private float maxBoxLength = 2f;
        [SerializeField] private int maxBoxesPerPart = 6;
        [SerializeField] private int minVerticesPerBox = 6;
        [SerializeField] private float padding = 0.01f;
        [SerializeField] private bool removeExisting = false;

        [MenuItem("Tools/Pumpjack/Compound Collider Builder")]
        private static void Open()
        {
            PumpjackColliderBuilder window = GetWindow<PumpjackColliderBuilder>(false, "Pumpjack Colliders");
            window.minSize = new Vector2(380f, 270f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

            GameObject selected = Selection.activeGameObject;
            EditorGUILayout.LabelField("Selected", selected != null ? selected.name : "(belum ada)");

            EditorGUILayout.Space(6f);
            maxBoxLength = EditorGUILayout.Slider("Max panjang box (m)", maxBoxLength, 0.25f, 10f);
            maxBoxesPerPart = EditorGUILayout.IntSlider("Max box per mesh", maxBoxesPerPart, 1, 16);
            minVerticesPerBox = EditorGUILayout.IntSlider("Min vertex per box", minVerticesPerBox, 1, 64);
            padding = EditorGUILayout.Slider("Padding (m)", padding, 0f, 0.1f);
            removeExisting = EditorGUILayout.Toggle("Hapus collider lama", removeExisting);

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Pilih root prefab di Project window atau di Prefab Mode, lalu klik Generate.\n" +
                "Box besar dipecah jadi beberapa box sampai panjangnya <= Max panjang box.",
                MessageType.Info);

            EditorGUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(selected == null))
            {
                if (GUILayout.Button("Generate Colliders", GUILayout.Height(32f)))
                    Generate(selected);

                if (GUILayout.Button("Hapus Semua Collider di Bawah Selection"))
                    RemoveAll(selected);
            }
        }

        // ============================================================
        // GENERATE
        // ============================================================

        private void Generate(GameObject selected)
        {
            EditRoot(selected, root =>
            {
                int meshCount = 0;
                int boxCount = 0;
                int skipped = 0;

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null) continue;

                    Mesh mesh = GetMesh(r);
                    if (mesh == null) continue;

                    if (!mesh.isReadable)
                    {
                        Debug.LogError(
                            $"[Pumpjack] Mesh '{mesh.name}' tidak readable. " +
                            "Aktifkan Read/Write di model importer FBX lalu coba lagi.", r);
                        continue;
                    }

                    GameObject part = r.gameObject;

                    if (removeExisting)
                    {
                        RemoveColliders(part);
                    }
                    else if (part.GetComponentInChildren<Collider>(true) != null)
                    {
                        skipped++;
                        continue;
                    }

                    int added = AddBoxes(part, mesh, r.transform, maxBoxLength, maxBoxesPerPart, minVerticesPerBox, padding);
                    if (added > 0)
                    {
                        meshCount++;
                        boxCount += added;
                    }
                }

                Debug.Log($"[Pumpjack] Compound collider selesai: {meshCount} mesh, {boxCount} BoxCollider, {skipped} di-skip (sudah ada collider).");
            });
        }

        private void RemoveAll(GameObject selected)
        {
            if (selected == null) return;

            if (!EditorUtility.DisplayDialog(
                    "Hapus Collider",
                    $"Hapus semua Collider di bawah '{selected.name}'?",
                    "Hapus",
                    "Batal"))
                return;

            EditRoot(selected, root =>
            {
                Collider[] cols = root.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                    if (cols[i] != null) Object.DestroyImmediate(cols[i]);

                Debug.Log($"[Pumpjack] {cols.Length} collider dihapus.");
            });
        }

        // ============================================================
        // CORE
        // ============================================================

        private static int AddBoxes(
            GameObject part,
            Mesh mesh,
            Transform rendererTransform,
            float maxBoxLength,
            int maxBoxes,
            int minVerts,
            float padding)
        {
            Vector3[] verts = mesh.vertices;
            if (verts == null || verts.Length == 0) return 0;

            Matrix4x4 toPart = part.transform.worldToLocalMatrix * rendererTransform.localToWorldMatrix;

            List<Vector3> points = new List<Vector3>(verts.Length);
            for (int i = 0; i < verts.Length; i++)
                points.Add(toPart.MultiplyPoint3x4(verts[i]));

            List<List<Vector3>> clusters = new List<List<Vector3>> { points };

            // Pecah cluster terbesar selama masih lebih panjang dari maxBoxLength.
            while (clusters.Count < maxBoxes)
            {
                int best = -1;
                float bestSize = -1f;

                for (int i = 0; i < clusters.Count; i++)
                {
                    List<Vector3> c = clusters[i];
                    if (c.Count < minVerts * 2) continue;

                    Bounds b = ComputeBounds(c);
                    float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    if (longest <= maxBoxLength) continue;

                    if (longest > bestSize)
                    {
                        bestSize = longest;
                        best = i;
                    }
                }

                if (best < 0) break;

                List<Vector3> src = clusters[best];
                Bounds srcBounds = ComputeBounds(src);
                int axis = LongestAxis(srcBounds);

                src.Sort((a, b) => Axis(a, axis).CompareTo(Axis(b, axis)));

                int mid = src.Count / 2;
                if (mid <= 0 || mid >= src.Count) break;

                List<Vector3> left = src.GetRange(0, mid);
                List<Vector3> right = src.GetRange(mid, src.Count - mid);

                clusters.RemoveAt(best);
                clusters.Add(left);
                clusters.Add(right);
            }

            int added = 0;

            for (int i = 0; i < clusters.Count; i++)
            {
                List<Vector3> c = clusters[i];
                if (c.Count < minVerts) continue;

                Bounds b = ComputeBounds(c);
                Vector3 size = b.size + Vector3.one * padding;
                if (size.x < 0.01f) size.x = 0.01f;
                if (size.y < 0.01f) size.y = 0.01f;
                if (size.z < 0.01f) size.z = 0.01f;

                BoxCollider box = part.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = size;
                added++;
            }

            // Jaga-jaga kalau semua cluster kebuang oleh minVerts.
            if (added == 0 && points.Count > 0)
            {
                Bounds b = ComputeBounds(points);
                Vector3 size = b.size + Vector3.one * padding;
                if (size.x < 0.01f) size.x = 0.01f;
                if (size.y < 0.01f) size.y = 0.01f;
                if (size.z < 0.01f) size.z = 0.01f;

                BoxCollider box = part.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = size;
                added = 1;
            }

            return added;
        }

        private static Bounds ComputeBounds(List<Vector3> pts)
        {
            Bounds b = new Bounds(pts[0], Vector3.zero);
            for (int i = 1; i < pts.Count; i++)
                b.Encapsulate(pts[i]);
            return b;
        }

        private static int LongestAxis(Bounds b)
        {
            if (b.size.x >= b.size.y && b.size.x >= b.size.z) return 0;
            if (b.size.y >= b.size.z) return 1;
            return 2;
        }

        private static float Axis(Vector3 v, int axis)
        {
            return axis == 0 ? v.x : (axis == 1 ? v.y : v.z);
        }

        private static Mesh GetMesh(Renderer r)
        {
            MeshRenderer mr = r as MeshRenderer;
            if (mr != null)
            {
                MeshFilter filter = mr.GetComponent<MeshFilter>();
                return filter != null ? filter.sharedMesh : null;
            }

            SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
            if (smr != null) return smr.sharedMesh;

            return null;
        }

        private static void RemoveColliders(GameObject part)
        {
            Collider[] cols = part.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null) Object.DestroyImmediate(cols[i]);
        }

        // ============================================================
        // PREFAB HANDLING
        // ============================================================

        private static void EditRoot(GameObject selected, System.Action<GameObject> action)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool inPrefabMode =
                stage != null &&
                stage.prefabContentsRoot != null &&
                (selected == stage.prefabContentsRoot ||
                 selected.transform.IsChildOf(stage.prefabContentsRoot.transform));

            string assetPath = AssetDatabase.GetAssetPath(selected);
            bool isProjectAsset =
                !inPrefabMode &&
                !string.IsNullOrEmpty(assetPath) &&
                AssetDatabase.Contains(selected);

            if (isProjectAsset)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    action(root);
                    PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            else
            {
                action(selected);
                EditorUtility.SetDirty(selected);
            }
        }
    }
}
#endif
