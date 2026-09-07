using UnityEngine;

/// <summary>
/// AudioPool - Pengganti AudioSource.PlayClipAtPoint dengan Zero GC Allocation.
/// Menggunakan object pooling AudioSource di runtime agar tidak membuat/menghancurkan
/// GameObject baru setiap kali ledakan, tembakan, atau part hancur.
/// </summary>
public class AudioPool : MonoBehaviour
{
    public static AudioPool Instance { get; private set; }

    [Header("Pool Settings")]
    [Tooltip("Jumlah AudioSource yang disiapkan dari awal")]
    public int poolSize = 24;

    private struct PooledVoice
    {
        public AudioSource source;
        public float busyUntil;
    }

    private PooledVoice[] _voices;
    private int _roundRobinIndex = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializePool()
    {
        _voices = new PooledVoice[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            GameObject voiceObj = new GameObject($"AudioVoice_{i:D2}");
            voiceObj.transform.SetParent(transform);

            AudioSource src = voiceObj.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.dopplerLevel = 0f; // Matikan kalkulasi doppler yang berat
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2f;
            src.maxDistance = 120f;
            src.spatialBlend = 1f; // 3D by default

            _voices[i] = new PooledVoice
            {
                source = src,
                busyUntil = 0f
            };
        }
    }

    private static void EnsureInstance()
    {
        if (Instance != null) return;

        GameObject host = new GameObject("[AudioPool]");
        Instance = host.AddComponent<AudioPool>();
        DontDestroyOnLoad(host);
        Instance.InitializePool();
    }

    /// <summary>
    /// Memutar suara 3D di posisi tertentu tanpa alokasi memori (pengganti PlayClipAtPoint).
    /// </summary>
    public static void PlayClipAtPoint(
        AudioClip clip,
        Vector3 position,
        float volume = 1f,
        float pitch = 1f,
        float spatialBlend = 1f,
        float maxDistance = 120f
    )
    {
        if (clip == null) return;
        EnsureInstance();

        Instance.PlayInternal(clip, position, volume, pitch, spatialBlend, maxDistance);
    }

    /// <summary>
    /// Memutar suara 2D (UI/HUD/Global) tanpa alokasi memori.
    /// </summary>
    public static void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        EnsureInstance();

        Instance.PlayInternal(clip, Vector3.zero, volume, pitch, 0f, 100f);
    }

    private void PlayInternal(
        AudioClip clip,
        Vector3 position,
        float volume,
        float pitch,
        float spatialBlend,
        float maxDistance
    )
    {
        if (_voices == null || _voices.Length == 0) return;

        float now = Time.time;
        int chosenIndex = -1;

        // 1. Cari voice yang sedang nganggur (idle)
        for (int i = 0; i < _voices.Length; i++)
        {
            if (now >= _voices[i].busyUntil || !_voices[i].source.isPlaying)
            {
                chosenIndex = i;
                break;
            }
        }

        // 2. Jika semua penuh, ambil voice dengan round-robin (voice stealing aman)
        if (chosenIndex == -1)
        {
            chosenIndex = _roundRobinIndex;
            _roundRobinIndex = (_roundRobinIndex + 1) % _voices.Length;
        }

        var voice = _voices[chosenIndex];
        AudioSource src = voice.source;

        src.transform.position = position;
        src.spatialBlend = spatialBlend;
        src.maxDistance = maxDistance;
        src.pitch = pitch;
        src.volume = Mathf.Clamp01(volume);
        src.clip = clip;
        src.Play();

        float duration = clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch));
        voice.busyUntil = now + duration;
        _voices[chosenIndex] = voice;
    }
}
