using System.Collections.Generic;
using UnityEngine;

namespace C4
{
    /// <summary>
    /// Status outline global yang dibaca sama C4OutlineFeature (screen-space outline).
    /// Gak ada mesh/GameObject outline yang dibikin, cuma daftar renderer yang mau di-outline.
    /// </summary>
    internal static class C4OutlineStatus
    {
        public static readonly List<Renderer> Renderers = new List<Renderer>(64);

        public static C4Outline Owner { get; private set; }
        public static Color Color { get; private set; } = new Color(0.1f, 0.85f, 1f, 1f);
        public static float WidthPixels { get; private set; } = 2f;

        public static bool Visible
        {
            get
            {
                if (Owner == null) return false;

                for (int i = 0; i < Renderers.Count; i++)
                {
                    if (Renderers[i] != null && Renderers[i].enabled && Renderers[i].gameObject.activeInHierarchy)
                        return true;
                }

                return false;
            }
        }

        public static void Show(C4Outline owner, IReadOnlyList<Renderer> targets, Color color, float widthPixels)
        {
            Owner = owner;
            Renderers.Clear();

            for (int i = 0; i < targets.Count; i++)
            {
                Renderer target = targets[i];
                if (target != null && target.enabled && target.gameObject.activeInHierarchy)
                    Renderers.Add(target);
            }

            Color = color;
            WidthPixels = widthPixels;
        }

        public static void Hide(C4Outline owner)
        {
            if (Owner != owner) return;

            Owner = null;
            Renderers.Clear();
        }

        public static void UpdateStyle(C4Outline owner, Color color, float widthPixels)
        {
            if (Owner != owner) return;

            Color = color;
            WidthPixels = widthPixels;
        }
    }

    /// <summary>
    /// Outline hover buat C4:
    /// - Crosshair ngarah ke numpad/knob -> cuma bagian itu yang di-outline.
    /// - Masih ngarah ke mesh C4 tapi bukan tombol -> outline seluruh C4.
    /// Outline-nya digambar screen-space (C4OutlineFeature), jadi tetap kelihatan
    /// walau objeknya bevel atau dilihat pas dari depan.
    /// </summary>
    [DisallowMultipleComponent]
    public class C4Outline : MonoBehaviour
    {
        [Header("=== OUTLINE STYLE ===")]
        [Tooltip("Warna garis outline.")]
        public Color outlineColor = new Color(0.1f, 0.85f, 1f, 1f);

        [Range(0.5f, 8f)]
        [Tooltip("Tebal outline dalam pixel layar.")]
        public float outlineWidthPixels = 2f;

        [Header("=== HIT DETECTION ===")]
        [Tooltip("Layer buat collider bodi C4. Samain sama layer Interactable di PlayerInteraction.")]
        public LayerMask bodyHitLayer = 1 << 8;

        private bool _initialized;

        private readonly List<Renderer> _sourceRenderers = new List<Renderer>(32);
        private readonly List<Renderer> _partTargets = new List<Renderer>(1);

        private C4Clickable _hoveredPart;
        private bool _wholeHovered;

        private void Awake()
        {
            Initialize();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }

        public void SetStyle(Color color, float widthPixels)
        {
            outlineColor = color;
            outlineWidthPixels = Mathf.Clamp(widthPixels, 0.5f, 8f);

            if (!_initialized)
                Initialize();

            C4OutlineStatus.UpdateStyle(this, outlineColor, outlineWidthPixels);
        }

        /// <summary>Nyalahin outline cuma buat tombol/knob yang disorot.</summary>
        public void HoverPart(C4Clickable part)
        {
            if (part == null)
            {
                HoverWhole();
                return;
            }

            if (_hoveredPart == part && !_wholeHovered && C4OutlineStatus.Owner == this)
                return;

            Renderer target = part.buttonRenderer != null
                ? part.buttonRenderer
                : part.GetComponentInChildren<Renderer>();

            if (target == null)
            {
                HoverWhole();
                return;
            }

            _partTargets.Clear();
            _partTargets.Add(target);
            C4OutlineStatus.Show(this, _partTargets, outlineColor, outlineWidthPixels);

            _hoveredPart = part;
            _wholeHovered = false;
        }

        /// <summary>Nyalahin outline seluruh mesh C4 (bukan cuma satu tombol).</summary>
        public void HoverWhole()
        {
            if (_wholeHovered && C4OutlineStatus.Owner == this)
                return;

            C4OutlineStatus.Show(this, _sourceRenderers, outlineColor, outlineWidthPixels);

            _hoveredPart = null;
            _wholeHovered = true;
        }

        public void Clear()
        {
            C4OutlineStatus.Hide(this);

            _hoveredPart = null;
            _wholeHovered = false;
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (bodyHitLayer.value == 0)
            {
                int layer = LayerMask.NameToLayer("Interactable");
                bodyHitLayer = layer >= 0 ? (1 << layer) : (1 << 8);
            }

            CacheRenderers();
            EnsureBodyHitboxes();
        }

        private void CacheRenderers()
        {
            _sourceRenderers.Clear();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !(renderer is MeshRenderer)) continue;

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                _sourceRenderers.Add(renderer);
            }
        }

        /// <summary>
        /// Bikin collider bodi buat semua mesh yang belum punya collider.
        /// Collider-nya trigger + layer Interactable, jadi cuma dipakai buat raycast crosshair,
        /// bukan buat nabrak player / peluru.
        /// </summary>
        private void EnsureBodyHitboxes()
        {
            int hitLayer = FirstLayer(bodyHitLayer);

            for (int i = 0; i < _sourceRenderers.Count; i++)
            {
                Renderer renderer = _sourceRenderers[i];
                if (renderer == null) continue;

                // Tombol/knob udah punya collider sendiri, jangan ditimpa hitbox bodi.
                if (renderer.GetComponent<Collider>() != null) continue;

                // Cegah duplicate hitbox
                if (renderer.transform.Find("C4_BodyHitbox") != null) continue;

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                Bounds meshBounds = filter.sharedMesh.bounds;

                GameObject hitbox = new GameObject("C4_BodyHitbox");
                hitbox.hideFlags = HideFlags.DontSave;
                hitbox.layer = hitLayer;
                hitbox.transform.SetParent(renderer.transform, false);
                hitbox.transform.localPosition = Vector3.zero;
                hitbox.transform.localRotation = Quaternion.identity;
                hitbox.transform.localScale = Vector3.one;

                BoxCollider box = hitbox.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = meshBounds.center;
                // Mesh tipis (sticky note) bisa punya size 0 di satu sumbu.
                box.size = Vector3.Max(meshBounds.size, new Vector3(0.0001f, 0.0001f, 0.0001f));
            }
        }

        private static int FirstLayer(LayerMask mask)
        {
            int value = mask.value;
            for (int i = 0; i < 32; i++)
            {
                if ((value & (1 << i)) != 0)
                    return i;
            }

            return 0;
        }
    }
}
