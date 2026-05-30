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

    [Tooltip("Pitch terendah (idle RPM)")]
    public float minPitch = 0.6f;
    [Tooltip("Pitch tertinggi (max RPM)")]
    public float maxPitch = 2.8f;

    [Tooltip("Volume saat idle")]
    [Range(0f, 1f)] public float idleVolume = 0.3f;
    [Tooltip("Volume saat full throttle")]
    [Range(0f, 1f)] public float maxVolume = 1.0f;

    private VehicleController _vc;

    private void Awake()
    {
        _vc = GetComponent<VehicleController>();
        if (engineAudioSource != null)
        {
            engineAudioSource.loop = true;
            engineAudioSource.Play();
        }
    }

    private void Update()
    {
        if (engineAudioSource == null || !_vc.engineRunning) return;

        float rpmNorm = Mathf.Clamp01(_vc.currentRPM / _vc.engine.maxRPM);
        engineAudioSource.pitch  = Mathf.Lerp(minPitch, maxPitch, rpmNorm);
        
        // FIX: Volume harusnya naik seiring tingginya RPM mesin (misal lagi engine braking di gigi rendah),
        // bukan murni berdasarkan injekan gas aja.
        // Kita bikin perpaduan: base volume dari RPM, dan dikasih sedikit boost kalau digas keras.
        float baseVolume = Mathf.Lerp(idleVolume, maxVolume, rpmNorm);
        float throttleBoost = _vc.throttleInput * 0.15f; 
        
        engineAudioSource.volume = Mathf.Clamp(baseVolume + throttleBoost, idleVolume, maxVolume);
    }
}
