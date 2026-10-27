using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// FPSCounter - menampilkan FPS kecil di pojok kiri atas scene Battlefield.
/// Self-bootstrapping: otomatis muncul tanpa perlu di-drag ke scene (jalan di editor & build).
/// </summary>
public class FPSCounter : MonoBehaviour
{
    private const string TARGET_SCENE = "Battlefield";

    [Header("=== SETTINGS ===")]
    [Tooltip("Interval refresh tampilan (detik) — kecil = responsif, besar = angka stabil")]
    public float updateInterval = 0.5f;
    [Tooltip("Ukuran font")]
    public float fontSize = 14f;

    private static FPSCounter _instance;
    private TextMeshProUGUI _text;
    private float _elapsed;
    private int _frames;
    private float _currentFps;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshForScene(SceneManager.GetActiveScene().name);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshForScene(scene.name);
    }

    private static void RefreshForScene(string sceneName)
    {
        if (sceneName == TARGET_SCENE)
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[FPSCounter]");
                _instance = go.AddComponent<FPSCounter>();
            }
        }
        else if (_instance != null)
        {
            Destroy(_instance.gameObject);
            _instance = null;
        }
    }

    private void Awake()
    {
        _text = BuildLabel();
    }

    private TextMeshProUGUI BuildLabel()
    {
        Canvas canvas = FindMainCanvas();
        if (canvas == null)
            canvas = CreateOverlayCanvas();

        GameObject labelObj = new GameObject("FPS_Label");
        labelObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = labelObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(10f, -10f);
        rect.sizeDelta = new Vector2(120f, 24f);

        TextMeshProUGUI tmp = labelObj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.raycastTarget = false;
        tmp.outlineWidth = 0.15f;
        tmp.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        tmp.text = "... FPS";
        return tmp;
    }

    private static Canvas FindMainCanvas()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
        foreach (Canvas c in canvases)
        {
            if (c.isActiveAndEnabled && c.isRootCanvas)
                return c;
        }
        return canvases.Length > 0 ? canvases[0] : null;
    }

    private static Canvas CreateOverlayCanvas()
    {
        GameObject canvasObj = new GameObject("FPS_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        return canvas;
    }

    private void Update()
    {
        _frames++;
        _elapsed += Time.unscaledDeltaTime;

        if (_elapsed >= updateInterval)
        {
            _currentFps = _frames / Mathf.Max(_elapsed, 0.0001f);
            _frames = 0;
            _elapsed = 0f;
            RefreshLabel();
        }
    }

    private void RefreshLabel()
    {
        if (_text == null) return;

        int fps = Mathf.RoundToInt(_currentFps);
        _text.SetText("{0} FPS", fps);
        _text.color = fps >= 50
            ? new Color(0.5f, 1f, 0.5f)
            : (fps >= 30 ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.45f, 0.45f));
    }
}
