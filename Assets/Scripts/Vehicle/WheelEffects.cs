using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(VehicleController))]
public class WheelEffects : MonoBehaviour
{
    [Header("=== DUST TRAIL (kontinyu) ===")]
    public ParticleSystem[] tireDustSystems;
    [Tooltip("Rate minimal debu (saat jalan pelan)")]
    [Range(0f, 50f)] public float dustMinRate = 5f;
    [Tooltip("Rate maksimal debu (saat ngebut)")]
    [Range(0f, 200f)] public float dustMaxRate = 45f;
    [Tooltip("Kecepatan (kmh) saat rate debu mencapai maksimal")]
    public float dustMaxSpeed = 80f;
    [Tooltip("Kecepatan minimal (km/h) sebelum debu mulai keluar")]
    public float dustMinSpeedKmh = 2f;

    [Header("=== DUST APPEARANCE (auto-generate) ===")]
    [Tooltip("Kalau slot 'Tire Dust Systems' di atas kosong, partikel debu dibuat otomatis di tiap roda")]
    public bool autoCreateDustParticles = true;
    [Tooltip("Warna debu / pasir gurun")]
    public Color dustColor = new Color(0.82f, 0.74f, 0.55f, 0.5f);
    [Tooltip("Ukuran awal partikel debu (meter)")]
    public float dustStartSize = 0.8f;
    [Tooltip("Umur partikel debu (detik)")]
    public float dustLifetime = 1.4f;
    [Tooltip("Kecepatan debu naik & menyebar")]
    public float dustRiseSpeed = 1.2f;
    [Tooltip("Sudut sebaran kerucut debu (derajat)")]
    [Range(0f, 89f)] public float dustSpreadAngle = 28f;
    [Tooltip("Gravitasi debu (negatif = melayang naik, positif = jatuh)")]
    public float dustGravity = -0.02f;
    [Tooltip("Bonus kepadatan debu saat roda selip / drift")]
    [Range(0f, 3f)] public float driftDustBoost = 1.5f;
    [Tooltip("Pengali ketebalan debu (ukuran, opacity, dan kepadatan). 1 = normal, 2 = dua kali lebih tebal.")]
    [Range(0.5f, 3f)] public float dustThickness = 1.6f;
    [Tooltip("Material debu opsional. Kalau kosong, dibuat otomatis pakai URP Particles/Unlit")]
    public Material dustMaterial;

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

    public bool IsDustActive { get; private set; }

    private VehicleController _vc;

    private static Texture2D _softParticleTex;
    private static Material _runtimeDustMat;

    private void Awake()
    {
        _vc = GetComponent<VehicleController>();

        if (tireSquealSource != null)
        {
            tireSquealSource.loop = true;
            tireSquealSource.playOnAwake = false;
            tireSquealSource.volume = 0f;
            tireSquealSource.dopplerLevel = 0f;
            tireSquealSource.rolloffMode = AudioRolloffMode.Linear;
            tireSquealSource.maxDistance = 80f;
        }

        bool hasAssignedDust = tireDustSystems != null && tireDustSystems.Length > 0;
        if (autoCreateDustParticles)
            EnsureDustParticles();

        if (hasAssignedDust)
            ApplyDustThicknessToAssignedSystems();
    }

    private void Update()
    {
        if (_vc == null) return;

        if (_vc.movementLocked)
        {
            SetWheelEffectsIdle();
            UpdateSquealAudio(0f);
            IsDustActive = HasVisibleDustParticles();
            return;
        }

        float maxSlip = 0f;
        float speedT = Mathf.Clamp01(_vc.speedKmh / Mathf.Max(1f, dustMaxSpeed));
        bool moving = _vc.speedKmh > dustMinSpeedKmh;
        bool dustActive = false;

        for (int i = 0; i < _vc.wheels.Length; i++)
        {
            var w = _vc.wheels[i];

            bool hasDust = tireDustSystems != null && i < tireDustSystems.Length && tireDustSystems[i] != null;
            bool hasSmoke = tireSmokeSystems != null && i < tireSmokeSystems.Length && tireSmokeSystems[i] != null;

            if (w.collider != null && w.collider.GetGroundHit(out WheelHit hit))
            {
                float slip = Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);
                if (slip > maxSlip) maxSlip = slip;

                Vector3 contactPoint = hit.point;

                if (hasDust)
                {
                    var dust = tireDustSystems[i];
                    var emission = dust.emission;

                    if (moving)
                    {
                        if (!dust.isPlaying) dust.Play();

                        dust.transform.SetPositionAndRotation(contactPoint, Quaternion.identity);
                        emission.enabled = true;
                        dustActive = true;
                        float rate = Mathf.Lerp(dustMinRate, dustMaxRate, speedT);
                        rate *= 1f + Mathf.Clamp01(slip) * driftDustBoost;
                        rate *= Mathf.Lerp(1f, dustThickness, 0.35f);
                        emission.rateOverTime = rate;
                    }
                    else
                    {
                        emission.enabled = false;
                        emission.rateOverTime = 0f;
                        if (dust.isPlaying) dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }

                if (hasSmoke)
                {
                    var emission = tireSmokeSystems[i].emission;
                    emission.enabled = slip > smokeThreshold;
                    if (slip > smokeThreshold)
                        tireSmokeSystems[i].transform.position = contactPoint;
                }
            }
            else
            {
                if (hasDust)
                {
                    var emission = tireDustSystems[i].emission;
                    emission.enabled = false;
                    emission.rateOverTime = 0f;
                }
                if (hasSmoke)
                {
                    var emission = tireSmokeSystems[i].emission;
                    emission.enabled = false;
                }
            }
        }

        _vc.wheelSlipRatio = maxSlip;
        if (!dustActive) dustActive = HasVisibleDustParticles();
        IsDustActive = dustActive;
        UpdateSquealAudio(maxSlip);
    }

    private bool HasVisibleDustParticles()
    {
        if (tireDustSystems == null) return false;

        for (int i = 0; i < tireDustSystems.Length; i++)
        {
            var dust = tireDustSystems[i];
            if (dust != null && dust.particleCount > 0) return true;
        }

        return false;
    }

    private void ApplyDustThicknessToAssignedSystems()
    {
        if (tireDustSystems == null) return;

        float thickness = Mathf.Max(0.1f, dustThickness);
        float alphaMul = Mathf.Lerp(1f, thickness, 0.5f);

        for (int i = 0; i < tireDustSystems.Length; i++)
        {
            var dust = tireDustSystems[i];
            if (dust == null) continue;

            var main = dust.main;
            var size = main.startSize;

            switch (size.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    main.startSize = new ParticleSystem.MinMaxCurve(size.constant * thickness);
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    main.startSize = new ParticleSystem.MinMaxCurve(size.constantMin * thickness, size.constantMax * thickness);
                    break;
                case ParticleSystemCurveMode.Curve:
                    main.startSize = new ParticleSystem.MinMaxCurve(size.curveMultiplier * thickness, size.curve);
                    break;
                case ParticleSystemCurveMode.TwoCurves:
                    main.startSize = new ParticleSystem.MinMaxCurve(size.curveMultiplier * thickness, size.curveMin, size.curveMax);
                    break;
            }

            var startColor = main.startColor;
            switch (startColor.mode)
            {
                case ParticleSystemGradientMode.Color:
                    Color c = startColor.color;
                    c.a = Mathf.Clamp01(c.a * alphaMul);
                    main.startColor = c;
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    Color cMin = startColor.colorMin;
                    Color cMax = startColor.colorMax;
                    cMin.a = Mathf.Clamp01(cMin.a * alphaMul);
                    cMax.a = Mathf.Clamp01(cMax.a * alphaMul);
                    main.startColor = new ParticleSystem.MinMaxGradient(cMin, cMax);
                    break;
            }

            main.maxParticles = Mathf.Min(main.maxParticles, 200);
            main.cullingMode = ParticleSystemCullingMode.Automatic;

            var renderer = dust.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Material mat = GetDustMaterial();
                if (mat != null) renderer.sharedMaterial = mat;

                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingFudge = -0.01f;
            }
        }
    }

    private void SetWheelEffectsIdle()
    {
        if (tireDustSystems != null)
        {
            for (int i = 0; i < tireDustSystems.Length; i++)
            {
                var dust = tireDustSystems[i];
                if (dust == null) continue;

                var emission = dust.emission;
                emission.enabled = false;
                emission.rateOverTime = 0f;
                if (dust.isPlaying) dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        if (tireSmokeSystems != null)
        {
            for (int i = 0; i < tireSmokeSystems.Length; i++)
            {
                if (tireSmokeSystems[i] == null) continue;
                var emission = tireSmokeSystems[i].emission;
                emission.enabled = false;
            }
        }
    }

    private void UpdateSquealAudio(float maxSlip)
    {
        if (tireSquealSource == null) return;

        float slipNorm = Mathf.Clamp01(maxSlip / 2f);
        float targetVol = slipNorm > 0.08f ? Mathf.Lerp(squealMinVolume, squealMaxVolume, slipNorm) : 0f;
        tireSquealSource.volume = Mathf.Lerp(tireSquealSource.volume, targetVol, Time.deltaTime * 10f);

        if (tireSquealSource.volume > 0.005f)
        {
            if (!tireSquealSource.isPlaying) tireSquealSource.Play();
        }
        else if (tireSquealSource.isPlaying)
        {
            tireSquealSource.Stop();
        }
    }

    private void EnsureDustParticles()
    {
        if (_vc == null || _vc.wheels == null || _vc.wheels.Length == 0) return;
        if (tireDustSystems != null && tireDustSystems.Length > 0) return;

        tireDustSystems = new ParticleSystem[_vc.wheels.Length];
        for (int i = 0; i < _vc.wheels.Length; i++)
            tireDustSystems[i] = CreateDustSystem(i);
    }

    private ParticleSystem CreateDustSystem(int index)
    {
        var go = new GameObject("DustTrail_" + index);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float thickness = Mathf.Max(0.1f, dustThickness);
        float startSize = dustStartSize * thickness;
        Color dustTint = dustColor;
        dustTint.a = Mathf.Clamp01(dustColor.a * Mathf.Lerp(1f, thickness, 0.5f));

        var main = ps.main;
        main.loop = true;
        main.prewarm = false;
        main.duration = 5f;
        main.startLifetime = dustLifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(dustRiseSpeed * 0.3f, dustRiseSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.5f, startSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = dustTint;
        main.gravityModifier = dustGravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.playOnAwake = false;
        main.cullingMode = ParticleSystemCullingMode.Automatic;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = dustSpreadAngle;
        shape.radius = 0.12f;
        shape.radiusThickness = 0.5f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 1.7f * Mathf.Lerp(1f, thickness, 0.35f))));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.12f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Material mat = GetDustMaterial();
            if (mat != null) renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = -0.01f;
        }

        return ps;
    }

    private Material GetDustMaterial()
    {
        if (dustMaterial != null) return dustMaterial;
        if (_runtimeDustMat != null) return _runtimeDustMat;

        Material baseMat = Resources.Load<Material>("VehicleFX/DustParticle");
        if (baseMat != null)
        {
            _runtimeDustMat = new Material(baseMat) { name = "DustTrail_Runtime" };
        }
        else
        {
            Shader sh = Shader.Find("Custom/DustParticle");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            if (sh == null) sh = Shader.Find("Mobile/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null)
            {
                Debug.LogError("WheelEffects: tidak ada shader debu yang tersedia!");
                return null;
            }

            _runtimeDustMat = new Material(sh) { name = "DustTrail_Runtime" };
        }

        Texture2D tex = GetSoftParticleTexture();
        if (_runtimeDustMat.HasProperty("_BaseMap")) _runtimeDustMat.SetTexture("_BaseMap", tex);
        if (_runtimeDustMat.HasProperty("_MainTex")) _runtimeDustMat.SetTexture("_MainTex", tex);
        if (_runtimeDustMat.HasProperty("_EmissionMap")) _runtimeDustMat.SetTexture("_EmissionMap", tex);

        if (_runtimeDustMat.HasProperty("_BaseColor")) _runtimeDustMat.SetColor("_BaseColor", Color.white);
        if (_runtimeDustMat.HasProperty("_Color")) _runtimeDustMat.SetColor("_Color", Color.white);
        if (_runtimeDustMat.HasProperty("_Surface")) _runtimeDustMat.SetFloat("_Surface", 1f);
        if (_runtimeDustMat.HasProperty("_Blend")) _runtimeDustMat.SetFloat("_Blend", 0f);
        if (_runtimeDustMat.HasProperty("_SrcBlend")) _runtimeDustMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (_runtimeDustMat.HasProperty("_DstBlend")) _runtimeDustMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (_runtimeDustMat.HasProperty("_SrcBlendAlpha")) _runtimeDustMat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (_runtimeDustMat.HasProperty("_DstBlendAlpha")) _runtimeDustMat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (_runtimeDustMat.HasProperty("_ZWrite")) _runtimeDustMat.SetFloat("_ZWrite", 0f);

        _runtimeDustMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _runtimeDustMat.DisableKeyword("_ALPHATEST_ON");
        // Render debu SETELAH jejak ban (TireTracks = Transparent + 10) supaya debu menutupi track.
        _runtimeDustMat.renderQueue = (int)RenderQueue.Transparent + 20;
        return _runtimeDustMat;
    }

    private static Texture2D GetSoftParticleTexture()
    {
        if (_softParticleTex != null) return _softParticleTex;

        const int size = 64;
        _softParticleTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "DustTrail_SoftParticle",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / radius;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        _softParticleTex.SetPixels32(pixels);
        _softParticleTex.Apply();
        return _softParticleTex;
    }
}
