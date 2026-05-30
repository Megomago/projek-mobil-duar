using UnityEngine;

/// <summary>
/// WheelEffects - Komponen pendamping VehicleController
/// Menangani: efek visual slip roda, partikel asap, suara tire squeal
/// Pasangkan pada GameObject yang sama dengan VehicleController
/// </summary>
[RequireComponent(typeof(VehicleController))]
public class WheelEffects : MonoBehaviour
{
    [Header("=== SKID MARK ===")]
    public bool enableSkidMarks = true;
    [Tooltip("Slip threshold untuk mulai gambar skid mark")]
    [Range(0.1f, 1f)] public float skidThreshold = 0.3f;

    [Header("=== PARTICLE EFFECTS ===")]
    public ParticleSystem[] tireSmokeSystems; // Assign 1 per drive wheel
    [Tooltip("Slip ratio untuk trigger smoke")]
    [Range(0.1f, 1f)] public float smokeThreshold = 0.4f;

    [Header("=== AUDIO ===")]
    public AudioSource tireSquealSource;
    [Range(0f, 1f)] public float squealMinVolume = 0f;
    [Range(0f, 1f)] public float squealMaxVolume = 0.8f;

    private VehicleController _vc;

    private void Awake()
    {
        _vc = GetComponent<VehicleController>();
    }

    private void Update()
    {
        float maxSlip = 0f;

        for (int i = 0; i < _vc.wheels.Length; i++)
        {
            var w = _vc.wheels[i];
            if (w.collider == null) continue;

            WheelHit hit;
            if (w.collider.GetGroundHit(out hit))
            {
                float slip = Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);
                if (slip > maxSlip) maxSlip = slip;

                // Tire smoke particles per wheel
                if (tireSmokeSystems != null && i < tireSmokeSystems.Length && tireSmokeSystems[i] != null)
                {
                    var emission = tireSmokeSystems[i].emission;
                    emission.enabled = slip > smokeThreshold;
                    if (slip > smokeThreshold)
                    {
                        // Posisikan particle di titik kontak roda
                        tireSmokeSystems[i].transform.position = hit.point;
                    }
                }
            }
        }

        _vc.wheelSlipRatio = maxSlip;

        // Tire squeal audio
        if (tireSquealSource != null)
        {
            float slipNorm = Mathf.Clamp01(maxSlip / 2f);
            tireSquealSource.volume = Mathf.Lerp(squealMinVolume, squealMaxVolume, slipNorm);
            if (slipNorm > 0.1f && !tireSquealSource.isPlaying) tireSquealSource.Play();
            if (slipNorm <= 0.1f && tireSquealSource.isPlaying)  tireSquealSource.Stop();
        }
    }
}
