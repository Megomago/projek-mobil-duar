using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Weapons
{
    /// <summary>
    /// Indikator bidik GLOBAL ala War Thunder untuk SEMUA turret player sekaligus.
    /// Satu canvas Screen Space Overlay (jadi tetap kelihatan di build/exe),
    /// satu setting, satu toggle. Turret cuma mendaftar ke
    /// <see cref="ManualTurretController.ActiveTurrets"/>; manager yang menggambar.
    ///
    /// F3 = toggle indikator bulat.
    /// F2 = toggle hit marker impact (KinematicProjectile.ShowHitDebug).
    /// </summary>
    [DefaultExecutionOrder(1000)] // jalan setelah LateUpdate semua turret
    [DisallowMultipleComponent]
    public class WeaponAimIndicatorManager : MonoBehaviour
    {
        public static WeaponAimIndicatorManager Instance { get; private set; }

        [Header("=== GLOBAL TOGGLE ===")]
        [Tooltip("Tombol toggle indikator bulat untuk semua senjata.")]
        public KeyCode toggleKey = KeyCode.F3;

        [Tooltip("Tombol toggle hit marker impact (F2). Server-wide, cukup sekali tekan.")]
        public KeyCode hitDebugToggleKey = KeyCode.F2;

        [Tooltip("Indikator langsung aktif saat play (default). Matikan kalau mau harus tekan F3 dulu.")]
        public bool startEnabled = true;

        [Header("=== TAMPILAN RING ===")]
        [Tooltip("Warna ring saat laras masih menyusul crosshair.")]
        public Color ringColor = new Color(1f, 1f, 1f, 0.95f);

        [Tooltip("Warna ring saat laras sudah sejajar dengan crosshair kamera.")]
        public Color ringAlignedColor = new Color(0.25f, 1f, 0.35f, 1f);

        [Range(8f, 120f)]
        [Tooltip("Diameter ring di layar (piksel).")]
        public float ringSizePixels = 36f;

        [Range(0.02f, 0.35f)]
        [Tooltip("Ketebalan ring, relatif terhadap diameternya.")]
        public float ringThickness = 0.09f;

        [Tooltip("Tampilkan titik kecil di tengah layar sebagai referensi crosshair.")]
        public bool showCenterDot = true;

        [Range(1f, 20f)]
        [Tooltip("Diameter titik tengah (piksel).")]
        public float centerDotSizePixels = 3f;

        [Min(0f)]
        [Tooltip("Jarak (piksel) ring ke crosshair yang dianggap sudah sejajar.")]
        public float alignedThresholdPixels = 16f;

        [Tooltip("Sembunyikan canvas kalau tidak ada turret player yang aktif.")]
        public bool hideWhenNoTurrets = true;

        // ── Runtime state ───────────────────────────────────────────────────

        private class RingEntry
        {
            public GameObject root;
            public RectTransform rect;
            public Image image;
        }

        private readonly List<RingEntry> _rings = new List<RingEntry>();

        private Canvas _canvas;
        private RectTransform _centerDotRect;
        private Image _centerDotImage;

        private Sprite _ringSprite;
        private Sprite _centerDotSprite;
        private Texture2D _ringTexture;
        private Texture2D _centerDotTexture;

        private float _builtRingThickness = -1f;
        private float _builtCenterDotSize = -1f;

        private bool _enabled;

        private static bool _autoCreating;
        private bool _autoCreated;

        // ── Lifecycle ───────────────────────────────────────────────────────

        /// <summary>
        /// Auto-spawn saat play mode, jadi tidak perlu setup apa pun di scene.
        /// Kalau mau tuning dari Inspector, tinggal tambahkan komponen ini
        /// manual ke salah satu object — komponen manual menang dan auto-spawn
        /// tidak membuat duplikat.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Pastikan tidak dobel walau domain reload dimatikan.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureExists();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Kalau manager manual ikut hancur saat pindah scene, buat lagi
            // yang otomatis supaya indikator tidak mati total.
            EnsureExists();
        }

        private static void EnsureExists()
        {
            if (Instance != null) return;

            _autoCreating = true;

            var go = new GameObject("WeaponAimIndicatorManager (Runtime)");
            go.hideFlags = HideFlags.DontSave;
            go.AddComponent<WeaponAimIndicatorManager>();

            _autoCreating = false;
        }

        private void Awake()
        {
            _autoCreated = _autoCreating;

            if (Instance != null && Instance != this)
            {
                // Manager manual di scene lebih diprioritaskan daripada yang auto,
                // supaya setting hasil tuning di Inspector tetap kepakai.
                if (Instance._autoCreated && !_autoCreated)
                {
                    Destroy(Instance.gameObject);
                    Instance = null;
                }
                else
                {
                    Destroy(this);
                    return;
                }
            }

            Instance = this;

            // Yang auto dibuat harus tahan pindah scene. Yang manual biarkan
            // menempel di object scene-nya (jangan dipindah ke DontDestroyOnLoad).
            if (_autoCreated) DontDestroyOnLoad(gameObject);

            _enabled = startEnabled;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DestroyVisuals();
        }

        private void Update()
        {
            if (Instance != this) return;

            if (Input.GetKeyDown(toggleKey))
            {
                _enabled = !_enabled;
                if (!_enabled) HideCanvas();
#if UNITY_EDITOR
                Debug.Log($"[WEAPON] Aim indicator {(_enabled ? "ON" : "OFF")}");
#endif
            }

            if (Input.GetKeyDown(hitDebugToggleKey))
            {
                KinematicProjectile.ShowHitDebug = !KinematicProjectile.ShowHitDebug;
#if UNITY_EDITOR
                Debug.Log($"[WEAPON] Hit indicator {(KinematicProjectile.ShowHitDebug ? "ON" : "OFF")}");
#endif
            }
        }

        private void LateUpdate()
        {
            if (Instance != this) return;

            if (!_enabled)
            {
                HideCanvas();
                return;
            }

            DrawAll();
        }

        // ── Drawing ─────────────────────────────────────────────────────────

        private void DrawAll()
        {
            EnsureVisuals();

            int used = 0;
            bool anyAligned = false;

            var turrets = ManualTurretController.ActiveTurrets;
            for (int i = 0; i < turrets.Count; i++)
            {
                var turret = turrets[i];
                if (turret == null) continue;
                if (!turret.isActiveAndEnabled) continue;
                if (!turret.usePlayerInput) continue;

                if (!turret.TryGetMuzzleAimScreenPoint(out Vector3 screenPoint)) continue;

                RingEntry entry = GetOrCreateRing(used);
                entry.root.SetActive(true);

                bool aligned = PositionRing(entry, screenPoint);
                anyAligned |= aligned;
                used++;
            }

            // Ring yang tidak terpakai frame ini disembunyikan (pool tetap hidup).
            for (int i = used; i < _rings.Count; i++)
            {
                if (_rings[i].root != null && _rings[i].root.activeSelf)
                    _rings[i].root.SetActive(false);
            }

            if (used == 0 && hideWhenNoTurrets)
            {
                HideCanvas();
                return;
            }

            if (!_canvas.gameObject.activeSelf)
                _canvas.gameObject.SetActive(true);

            UpdateCenterDot(anyAligned);
        }

        /// <summary>Posisikan satu ring, clamp ke tepi layar. Return true kalau sejajar.</summary>
        private bool PositionRing(RingEntry entry, Vector3 screenPoint)
        {
            float margin = Mathf.Max(2f, ringSizePixels * 0.5f);
            float maxX = Mathf.Max(margin, Screen.width - margin);
            float maxY = Mathf.Max(margin, Screen.height - margin);

            float x = float.IsNaN(screenPoint.x) ? Screen.width * 0.5f : Mathf.Clamp(screenPoint.x, margin, maxX);
            float y = float.IsNaN(screenPoint.y) ? Screen.height * 0.5f : Mathf.Clamp(screenPoint.y, margin, maxY);

            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            float dx = x - centerX;
            float dy = y - centerY;
            bool aligned = (dx * dx + dy * dy) <= (alignedThresholdPixels * alignedThresholdPixels);

            // Overlay canvas: world position = koordinat layar (piksel).
            entry.rect.position = new Vector3(x, y, 0f);
            entry.rect.sizeDelta = new Vector2(ringSizePixels, ringSizePixels);
            entry.image.color = aligned ? ringAlignedColor : ringColor;

            return aligned;
        }

        private void UpdateCenterDot(bool anyAligned)
        {
            if (_centerDotRect == null) return;

            if (!showCenterDot)
            {
                if (_centerDotImage.enabled) _centerDotImage.enabled = false;
                return;
            }

            if (!_centerDotImage.enabled) _centerDotImage.enabled = true;

            _centerDotRect.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            _centerDotRect.sizeDelta = new Vector2(centerDotSizePixels, centerDotSizePixels);

            Color c = anyAligned ? ringAlignedColor : ringColor;
            c.a = Mathf.Max(c.a, 0.75f);
            _centerDotImage.color = c;
        }

        // ── Visual assets ───────────────────────────────────────────────────

        private void EnsureVisuals()
        {
            if (_canvas == null)
            {
                // Canvas bisa ikut hancur kalau manager manual ditaruh di object
                // scene. Buang pool lama yang sudah mati sebelum bikin yang baru.
                _rings.Clear();
                _centerDotRect = null;
                _centerDotImage = null;
                CreateCanvas();
            }

            if (_ringSprite == null || !Mathf.Approximately(_builtRingThickness, ClampedThickness()))
            {
                RebuildRingSprite();
                for (int i = 0; i < _rings.Count; i++)
                    _rings[i].image.sprite = _ringSprite;
            }

            if (_centerDotSprite == null || !Mathf.Approximately(_builtCenterDotSize, centerDotSizePixels))
            {
                RebuildCenterDotSprite();
                if (_centerDotImage != null) _centerDotImage.sprite = _centerDotSprite;
            }
        }

        private float ClampedThickness()
        {
            return Mathf.Clamp(ringThickness, 0.02f, 0.45f);
        }

        private void CreateCanvas()
        {
            // Rootless: Screen Space Overlay tidak boleh ikut transform apa pun.
            var root = new GameObject("___WeaponAimIndicatorCanvas (Runtime)");
            root.hideFlags = HideFlags.DontSave;

            // Ikut bertahan saat pindah scene; dihancurkan sendiri di OnDestroy.
            DontDestroyOnLoad(root);

            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500; // di atas semua canvas HUD (semuanya 0)

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            // Titik tengah (referensi crosshair kamera).
            var dotGO = new GameObject("CenterDot");
            dotGO.hideFlags = HideFlags.DontSave;
            dotGO.transform.SetParent(root.transform, false);

            _centerDotImage = dotGO.AddComponent<Image>();
            _centerDotImage.sprite = GetCenterDotSprite();
            _centerDotImage.raycastTarget = false;

            _centerDotRect = _centerDotImage.rectTransform;
            _centerDotRect.anchorMin = new Vector2(0.5f, 0.5f);
            _centerDotRect.anchorMax = new Vector2(0.5f, 0.5f);
            _centerDotRect.pivot = new Vector2(0.5f, 0.5f);
            _centerDotRect.sizeDelta = new Vector2(centerDotSizePixels, centerDotSizePixels);
        }

        private RingEntry GetOrCreateRing(int index)
        {
            while (_rings.Count <= index)
                _rings.Add(CreateRing(_rings.Count));

            return _rings[index];
        }

        private RingEntry CreateRing(int index)
        {
            var go = new GameObject($"AimRing_{index}");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(_canvas.transform, false);

            var image = go.AddComponent<Image>();
            image.sprite = GetRingSprite();
            image.raycastTarget = false;
            image.type = Image.Type.Simple;

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ringSizePixels, ringSizePixels);

            // Outline gelap tipis biar ring tetap kebaca di langit terang / tanah gelap.
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return new RingEntry { root = go, rect = rect, image = image };
        }

        private Sprite GetRingSprite()
        {
            if (_ringSprite == null) RebuildRingSprite();
            return _ringSprite;
        }

        private Sprite GetCenterDotSprite()
        {
            if (_centerDotSprite == null) RebuildCenterDotSprite();
            return _centerDotSprite;
        }

        private void RebuildRingSprite()
        {
            const int size = 128;

            DestroySprite(ref _ringSprite, ref _ringTexture);

            _ringTexture = NewTexture(size, "WeaponAimRingTexture");

            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float outerRadius = size * 0.5f - 2f;
            float thicknessTexels = Mathf.Clamp(ClampedThickness() * size, 2f, size * 0.45f);
            float innerRadius = outerRadius - thicknessTexels;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float outerAlpha = Mathf.Clamp01(outerRadius - dist + 1f);
                    float innerAlpha = Mathf.Clamp01(dist - innerRadius + 1f);
                    float alpha = Mathf.Min(outerAlpha, innerAlpha);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            _ringTexture.SetPixels(pixels);
            _ringTexture.Apply(false, false);

            _ringSprite = CreateSprite(_ringTexture, size, "WeaponAimRingSprite");
            _builtRingThickness = ClampedThickness();
        }

        private void RebuildCenterDotSprite()
        {
            const int size = 32;

            DestroySprite(ref _centerDotSprite, ref _centerDotTexture);

            _centerDotTexture = NewTexture(size, "WeaponAimCenterDotTexture");

            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f - 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(radius - dist + 1f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            _centerDotTexture.SetPixels(pixels);
            _centerDotTexture.Apply(false, false);

            _centerDotSprite = CreateSprite(_centerDotTexture, size, "WeaponAimCenterDotSprite");
            _builtCenterDotSize = centerDotSizePixels;
        }

        private static Texture2D NewTexture(int size, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = name;
            tex.hideFlags = HideFlags.DontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Sprite CreateSprite(Texture2D tex, int size, string name)
        {
            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static void DestroySprite(ref Sprite sprite, ref Texture2D texture)
        {
            if (sprite != null)
            {
                Destroy(sprite);
                sprite = null;
            }
            if (texture != null)
            {
                Destroy(texture);
                texture = null;
            }
        }

        // ── Canvas visibility / cleanup ─────────────────────────────────────

        private void HideCanvas()
        {
            if (_canvas != null && _canvas.gameObject.activeSelf)
                _canvas.gameObject.SetActive(false);
        }

        private void DestroyVisuals()
        {
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
                _canvas = null;
            }

            _rings.Clear();
            _centerDotRect = null;
            _centerDotImage = null;

            DestroySprite(ref _ringSprite, ref _ringTexture);
            DestroySprite(ref _centerDotSprite, ref _centerDotTexture);
        }
    }
}
