using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD overlay feedback tembakan player (ala War Thunder).
/// Muncul saat peluru dari KENDARAAN YANG DIKENDARAI PLAYER mengenai target:
///   - Feed di kanan layar: nama part yang kena (kuning = rusak, merah = hancur)
///   - Teks besar merah di atas layar saat part hancur / target destroyed
/// Tanpa setup — singleton otomatis dibuat saat hit pertama. Render via OnGUI
/// (teks + warna dulu, ikon sprite bisa menyusul).
/// </summary>
public class HitFeedbackHUD : MonoBehaviour
{
    [Header("Tampilan")]
    [Tooltip("Matikan untuk menyembunyikan semua feedback (tanpa hapus script)")]
    public bool enableFeedback = true;

    [Tooltip("Berapa lama satu entry feed bertahan di layar (detik)")]
    public float entryLifetime = 2.5f;

    [Tooltip("Durasi fade-out di akhir lifetime entry (detik)")]
    public float fadeDuration = 0.6f;

    [Tooltip("Berapa lama teks besar (part hancur) tampil (detik)")]
    public float bigTextLifetime = 1.6f;

    [Tooltip("Maksimal entry yang tampil bersamaan")]
    public int maxEntries = 6;

    [Tooltip("Lebar area feed di kanan layar (px)")]
    public float feedWidth = 340f;

    [Tooltip("Tinggi satu baris feed (px)")]
    public float feedRowHeight = 26f;

    private struct FeedEntry
    {
        public string key;
        public string text;
        public Color color;
        public float expireTime;
    }

    private struct BigNotice
    {
        public string text;
        public float expireTime;
    }

    private static HitFeedbackHUD _instance;
    private static VehicleStatsManager _cachedPlayerStats;

    private readonly List<FeedEntry> _entries = new List<FeedEntry>(8);
    private BigNotice _bigNotice;
    private bool _hasBigNotice;
    private GUIStyle _feedStyle;
    private GUIStyle _bigStyle;

    private static readonly Color HitColor = new Color(1f, 0.72f, 0.2f);
    private static readonly Color DestroyedColor = new Color(1f, 0.28f, 0.18f);

    private static HitFeedbackHUD Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("HitFeedbackHUD");
                _instance = go.AddComponent<HitFeedbackHUD>();
            }
            return _instance;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>
    /// Cek apakah shooter adalah kendaraan yang sedang dikendarai player.
    /// Dipanggil dari KinematicProjectile.HandleHit — zero alloc (cache).
    /// </summary>
    public static bool IsPlayerShooter(VehicleStatsManager shooterStats)
    {
        if (shooterStats == null) return false;

        VehicleEntry active = VehicleEntry.ActiveVehicle;
        if (active == null)
        {
            _cachedPlayerStats = null;
            return false;
        }

        if (_cachedPlayerStats == null || _cachedPlayerStats.gameObject != active.gameObject)
            active.TryGetComponent(out _cachedPlayerStats);

        return _cachedPlayerStats == shooterStats;
    }

    /// <summary>
    /// Laporkan part musuh yang kena tembakan player.
    /// destroyed = true → entry + teks besar "PART DESTROYED" warna merah.
    /// </summary>
    public static void ReportHit(string partName, float damage, bool destroyed)
    {
        var hud = Instance;
        if (!hud.enableFeedback) return;

        if (string.IsNullOrEmpty(partName)) partName = "Target";

        string text;
        if (destroyed)
            text = partName.ToUpperInvariant() + " DESTROYED";
        else if (damage > 0f)
            text = partName + " hit  (" + damage.ToString("0") + ")";
        else
            text = partName + " hit";

        hud.Push(partName, text, destroyed ? DestroyedColor : HitColor, destroyed);
    }

    /// <summary>
    /// Laporkan target (dummy/SimpleTarget) hancur — teks besar "TARGET DESTROYED".
    /// </summary>
    public static void ReportKill(string targetName)
    {
        var hud = Instance;
        if (!hud.enableFeedback) return;

        if (string.IsNullOrEmpty(targetName)) targetName = "Target";
        hud.Push(targetName, targetName.ToUpperInvariant() + " DESTROYED", DestroyedColor, true);
    }

    private void Push(string key, string text, Color color, bool bigNotice)
    {
        float now = Time.time;

        // Merge: part yang sama kena lagi → refresh entry-nya (anti spam autocannon)
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].key == key)
                _entries.RemoveAt(i);
        }

        _entries.Add(new FeedEntry { key = key, text = text, color = color, expireTime = now + entryLifetime });

        while (_entries.Count > maxEntries)
            _entries.RemoveAt(0);

        if (bigNotice)
        {
            _hasBigNotice = true;
            _bigNotice.text = text;
            _bigNotice.expireTime = now + bigTextLifetime;
        }
    }

    private void OnGUI()
    {
        // Hanya gambar saat Repaint event (hemat CPU, tidak dobel per event)
        if (Event.current.type != EventType.Repaint) return;

        // Feedback hanya relevan saat player sedang mengendarai kendaraan
        if (VehicleEntry.ActiveVehicle == null)
        {
            if (_entries.Count > 0) _entries.Clear();
            _hasBigNotice = false;
            return;
        }

        // Styles HARUS siap sebelum menggambar apapun
        EnsureStyles();

        if (!enableFeedback) return;

        float now = Time.time;

        // ── Feed part yang kena — kanan layar, entry terbaru di atas ──
        float x = Screen.width - feedWidth - 16f;
        float y = Screen.height * 0.45f;

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            FeedEntry e = _entries[i];
            float remain = e.expireTime - now;
            if (remain <= 0f)
            {
                _entries.RemoveAt(i);
                continue;
            }

            float alpha = fadeDuration > 0f ? Mathf.Clamp01(remain / fadeDuration) : 1f;
            DrawOutlined(x, y, feedWidth, feedRowHeight, e.text, e.color, alpha, _feedStyle);
            y += feedRowHeight;
        }

        // ── Teks besar di atas layar (part hancur / target destroyed) ──
        if (_hasBigNotice)
        {
            float remain = _bigNotice.expireTime - now;
            if (remain <= 0f)
            {
                _hasBigNotice = false;
            }
            else
            {
                float alpha = fadeDuration > 0f ? Mathf.Clamp01(remain / fadeDuration) : 1f;
                DrawOutlined(0f, Screen.height * 0.10f, Screen.width, 64f, _bigNotice.text, DestroyedColor, alpha, _bigStyle);
            }
        }
    }

    private void DrawOutlined(float x, float y, float w, float h, string text, Color color, float alpha, GUIStyle style)
    {
        // Bayangan hitam + teks berwarna biar kebaca di background apapun
        GUI.color = new Color(0f, 0f, 0f, alpha * 0.9f);
        GUI.Label(new Rect(x + 1f, y + 1f, w, h), text, style);

        GUI.color = new Color(color.r, color.g, color.b, alpha);
        GUI.Label(new Rect(x, y, w, h), text, style);

        GUI.color = Color.white;
    }

    private void EnsureStyles()
    {
        if (_feedStyle == null)
        {
            _feedStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                richText = true,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
        }

        if (_bigStyle == null)
        {
            _bigStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                richText = true,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
        }
    }
}
