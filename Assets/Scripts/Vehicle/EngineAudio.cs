using UnityEngine;

/// <summary>
/// EngineAudio - Sinkronisasi suara mesin dengan RPM
/// Pasangkan pada GameObject yang sama dengan VehicleController
/// </summary>
[RequireComponent(typeof(VehicleController))]
public class EngineAudio : MonoBehaviour
{
    [Header("=== ENGINE SOUND ===")]
    public AudioSource engineAudioSource;

    [Header("=== STARTER SOUND ===")]
    [Tooltip("Suara dinamo starter (non-looping clip)")]
    public AudioSource starterAudioSource;

    [Tooltip("Pitch terendah (idle RPM)")]
    public float minPitch = 0.6f;
    [Tooltip("Pitch tertinggi (max RPM)")]
    public float maxPitch = 2.8f;

    [Tooltip("Volume saat idle")]
    [Range(0f, 1f)] public float idleVolume = 0.3f;
    [Tooltip("Volume saat full throttle")]
    [Range(0f, 1f)] public float maxVolume = 1.0f;

    private VehicleController _vc;
    private float _fadeSpeed = 2.5f;
    private bool _prevEngineRunning;

    private void Awake()
    {
        _vc = GetComponent<VehicleController>();
        // PENTING: selalu mulai silent. Jangan pernah auto-Play di Awake berdasarkan
        // _vc.engineRunning — nilai itu belum valid di sini karena execution order
        // Awake antar komponen tidak dijamin (prefab juga bisa ke-save dengan
        // engineRunning=1). Kalau sempat Play sekilas, kedengaran bocor pas spawn.
        // Engine yang benar-benar nyala akan di-Play oleh Update() di frame berikutnya.
        if (engineAudioSource != null)
        {
            engineAudioSource.loop = true;
            engineAudioSource.playOnAwake = false;
            engineAudioSource.dopplerLevel = 0f;
            engineAudioSource.rolloffMode = AudioRolloffMode.Linear;
            engineAudioSource.maxDistance = 120f;
            engineAudioSource.volume = 0f;
            engineAudioSource.pitch = minPitch * 0.3f;
            engineAudioSource.Stop();
        }

        if (starterAudioSource != null)
        {
            starterAudioSource.playOnAwake = false;
            starterAudioSource.dopplerLevel = 0f;
            starterAudioSource.rolloffMode = AudioRolloffMode.Linear;
            starterAudioSource.maxDistance = 60f;
            starterAudioSource.Stop();
        }

        _prevEngineRunning = false;
    }

    private void Start()
    {
        // Guard kedua: saat Start semua Awake sudah jalan sehingga
        // _vc.engineRunning sudah definitif. Pastikan tetap silent kalau mesin mati.
        if (_vc != null && !_vc.engineRunning && engineAudioSource != null)
        {
            engineAudioSource.volume = 0f;
            if (engineAudioSource.isPlaying)
                engineAudioSource.Stop();
        }
        if (_vc != null)
            _prevEngineRunning = _vc.engineRunning;
    }

    private void Update()
    {
        if (engineAudioSource == null) return;

        // ── TRACK ENGINE START TRANSITION ──
        if (_vc.engineRunning && !_prevEngineRunning && starterAudioSource != null)
        {
            starterAudioSource.Stop();
            starterAudioSource.Play();
        }
        _prevEngineRunning = _vc.engineRunning;

        if (!_vc.engineRunning)
        {
            // Stall: fade volume & pitch lerp ke minimal
            engineAudioSource.volume = Mathf.Lerp(engineAudioSource.volume, 0f, Time.deltaTime * _fadeSpeed);
            engineAudioSource.pitch  = Mathf.Lerp(engineAudioSource.pitch, minPitch * 0.3f, Time.deltaTime * _fadeSpeed);

            if (engineAudioSource.volume < 0.005f && engineAudioSource.isPlaying)
                engineAudioSource.Stop();
            return;
        }

        if (!engineAudioSource.isPlaying)
        {
            engineAudioSource.volume = 0f;
            engineAudioSource.Play();
        }

        float rpmNorm = Mathf.Clamp01(_vc.currentRPM / _vc.engine.maxRPM);
        engineAudioSource.pitch  = Mathf.Lerp(minPitch, maxPitch, rpmNorm);
        
        float baseVolume = Mathf.Lerp(idleVolume, maxVolume, rpmNorm);
        float throttleBoost = _vc.throttleInput * 0.15f; 
        
        engineAudioSource.volume = Mathf.Lerp(engineAudioSource.volume, Mathf.Clamp(baseVolume + throttleBoost, idleVolume, maxVolume), Time.deltaTime * _fadeSpeed * 2f);
    }
}
