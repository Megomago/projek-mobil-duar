using UnityEngine;

[RequireComponent(typeof(VehicleController))]
public class WheelEffects : MonoBehaviour
{
    [Header("=== DUST TRAIL (kontinyu) ===")]
    public ParticleSystem[] tireDustSystems;
    [Tooltip("Rate minimal debu (saat jalan pelan)")]
    [Range(0f, 50f)] public float dustMinRate = 5f;
    [Tooltip("Rate maksimal debu (saat ngebut)")]
    [Range(0f, 100f)] public float dustMaxRate = 40f;
    [Tooltip("Kecepatan (kmh) saat rate debu mencapai maksimal")]
    public float dustMaxSpeed = 80f;

    [Header("=== SLIP SMOKE ===")]
    public ParticleSystem[] tireSmokeSystems;
    [Range(0.1f, 1f)] public float smokeThreshold = 0.4f;

    [Header("=== SKID MARK ===")]
    public bool enableSkidMarks = true;
    [Range(0.1f, 1f)] public float skidThreshold = 0.3f;

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

                Vector3 contactPoint = hit.point;

                // Dust trail — kontinyu selama roda menyentuh tanah
                if (tireDustSystems != null && i < tireDustSystems.Length && tireDustSystems[i] != null)
                {
                    var emission = tireDustSystems[i].emission;
                    emission.enabled = true;
                    float t = Mathf.Clamp01(_vc.speedKmh / dustMaxSpeed);
                    emission.rateOverTime = Mathf.Lerp(dustMinRate, dustMaxRate, t);
                    tireDustSystems[i].transform.position = contactPoint;
                }

                // Slip smoke — hanya saat kehilangan traksi
                if (tireSmokeSystems != null && i < tireSmokeSystems.Length && tireSmokeSystems[i] != null)
                {
                    var emission = tireSmokeSystems[i].emission;
                    emission.enabled = slip > smokeThreshold;
                    if (slip > smokeThreshold)
                    {
                        tireSmokeSystems[i].transform.position = contactPoint;
                    }
                }
            }
            else
            {
                if (tireDustSystems != null && i < tireDustSystems.Length && tireDustSystems[i] != null)
                {
                    var emission = tireDustSystems[i].emission;
                    emission.enabled = false;
                }
                if (tireSmokeSystems != null && i < tireSmokeSystems.Length && tireSmokeSystems[i] != null)
                {
                    var emission = tireSmokeSystems[i].emission;
                    emission.enabled = false;
                }
            }
        }

        _vc.wheelSlipRatio = maxSlip;

        if (tireSquealSource != null)
        {
            float slipNorm = Mathf.Clamp01(maxSlip / 2f);
            tireSquealSource.volume = Mathf.Lerp(squealMinVolume, squealMaxVolume, slipNorm);
            if (slipNorm > 0.1f && !tireSquealSource.isPlaying) tireSquealSource.Play();
            if (slipNorm <= 0.1f && tireSquealSource.isPlaying) tireSquealSource.Stop();
        }
    }
}
