using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("NNTI/Environment/Time Of Day")]
public class TimeOfDayController : MonoBehaviour
{
    [Header("=== TIME ===")]
    [Range(0f, 24f)] public float timeOfDay = 8f;

    [Tooltip("24 jam in-game selesai dalam berapa detik real time. 60 = 1 menit.")]
    [Min(1f)] public float dayLengthSeconds = 60f;

    public bool autoAdvance = true;

    [Tooltip("Pengali kecepatan waktu. 2 = dua kali lebih cepat.")]
    [Min(0f)] public float timeScale = 1f;

    [Header("=== REFERENCES ===")]
    [Tooltip("Directional Light matahari. Kosongin aja untuk auto cari.")]
    public Light sunLight;

    [Tooltip("Cloud Dome yang mau ikut berubah warnanya. Kosongin aja untuk auto cari.")]
    public CloudDome cloudDome;

    [Header("=== SUN PATH ===")]
    [Tooltip("Arah putaran matahari (derajat).")]
    public float sunAzimuth = 35f;

    [Tooltip("Kemiringan jalur matahari. Makin besar, matahari ga lewat tepat di atas kepala.")]
    [Range(0f, 45f)] public float sunTilt = 20f;

    [Header("=== LIGHTING ===")]
    [Min(0f)] public float maxSunIntensity = 2f;

    [Tooltip("Warna matahari dari horizon (kiri) ke tinggi (kanan).")]
    public Gradient sunColor = MakeGradient(
        new Color(1.00f, 0.45f, 0.20f),
        new Color(1.00f, 0.75f, 0.50f),
        new Color(1.00f, 0.95f, 0.85f),
        new Color(1.00f, 1.00f, 0.96f));

    [Tooltip("Warna ambient dari malam (kiri) ke siang (kanan).")]
    public Gradient ambientColor = MakeGradient(
        new Color(0.18f, 0.22f, 0.38f),
        new Color(0.30f, 0.28f, 0.36f),
        new Color(0.42f, 0.45f, 0.53f),
        new Color(0.45f, 0.49f, 0.57f));

    [Range(0f, 2f)] public float dayAmbientIntensity = 1f;

    [Range(0f, 2f)] public float nightAmbientIntensity = 1f;

    [Tooltip("0 = ambient malam tetap seperti setting biasa, 1 = ambient malam benar-benar hitam.")]
    [Range(0f, 1f)] public float nightDarkness = 1f;

    [Header("=== EXTRA LIGHTS (NIGHT) ===")]
    [Tooltip("Lampu buatan (spot/point) yang ikut diredupkan saat malam, misal lampu base/garasi. Kosongin untuk auto cari lampu root selain matahari.")]
    public Light[] extraLights;

    [Tooltip("Auto cari lampu buatan kalau daftar Extra Lights kosong. Hanya lampu root, bukan lampu kendaraan.")]
    public bool autoFindExtraLights = true;

    [Tooltip("Pengali intensitas lampu buatan saat malam penuh. 0 = mati total, 1 = tetap terang.")]
    [Range(0f, 1f)] public float nightExtraLightMultiplier = 0f;

    [Header("=== BAKED LIGHTING ===")]
    [Tooltip("Matikan baked lightmap terrain saat malam supaya terrain ikut benar-benar gelap.")]
    public bool disableTerrainLightmapAtNight = true;

    [Tooltip("Matikan semua baked lightmap (environment statis) saat malam supaya seluruh scene ikut gelap.")]
    public bool disableAllLightmapsAtNight = true;

    [Tooltip("Matikan baked light probe saat malam supaya objek dinamis (kendaraan) ikut gelap.")]
    public bool disableLightProbesAtNight = true;

    [Header("=== DEBUG ===")]
    public bool showDebugClock = true;

    GUIStyle _clockStyle;
    GUIStyle _debugStyle;

    [SerializeField, HideInInspector] int _nightSettingsVersion;

    Light[] _resolvedExtraLights;
    float[] _extraLightBaseIntensities;
    bool _extraLightsResolved;
    float _nextReferenceSearch;

    Terrain[] _terrains;
    int[] _terrainLightmapIndices;
    Renderer[] _lightmappedRenderers;
    int[] _rendererLightmapIndices;
    Renderer[] _probedRenderers;
    LightProbeUsage[] _rendererProbeUsages;
    bool _bakedLightingCached;
    bool _bakedLightingDisabled;

    public float NormalizedTime => timeOfDay / 24f;
    public int Hour => Mathf.FloorToInt(timeOfDay);
    public int Minute => Mathf.FloorToInt((timeOfDay - Mathf.Floor(timeOfDay)) * 60f);

    public float SunElevation { get; private set; }

    [Tooltip("0 = siang terang, 1 = malam penuh.")]
    public float NightAmount { get; private set; }

    void OnEnable()
    {
        MigrateNightSettings();
        ResolveReferences();
        Apply();
    }

    void OnDisable()
    {
        RestoreExtraLights();
        RestoreBakedLighting();
    }

    void OnValidate()
    {
        MigrateNightSettings();
        ResolveReferences();
        Apply();
    }

    void MigrateNightSettings()
    {
        if (_nightSettingsVersion >= 1)
            return;

        _nightSettingsVersion = 1;

        if (nightDarkness <= 0f)
            nightDarkness = 1f;

        if (!autoFindExtraLights)
            autoFindExtraLights = true;
    }

    void Update()
    {
        if (!Application.isPlaying || !autoAdvance)
            return;

        float hoursPerSecond = 24f / Mathf.Max(1f, dayLengthSeconds) * timeScale;
        timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime * hoursPerSecond, 24f);
        Apply();
    }

    public void SetTime(float hour)
    {
        timeOfDay = Mathf.Repeat(hour, 24f);
        Apply();
    }

    void Apply()
    {
        if (sunLight == null && Time.realtimeSinceStartup >= _nextReferenceSearch)
        {
            _nextReferenceSearch = Time.realtimeSinceStartup + 2f;
            ResolveReferences();
        }

        EnsureExtraLightsResolved();

        float angleRad = (timeOfDay / 24f * 360f - 90f) * Mathf.Deg2Rad;

        Vector3 sunDirection = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);
        sunDirection = Quaternion.Euler(sunTilt, sunAzimuth, 0f) * sunDirection;
        sunDirection.Normalize();

        float sunHeight = sunDirection.y;
        float dayAmount = Mathf.Clamp01(sunHeight * 3.5f);
        float dayFactor = Mathf.InverseLerp(-0.15f, 0.35f, sunHeight);

        SunElevation = sunHeight;
        NightAmount = Mathf.Clamp01(-sunHeight * 3.5f);

        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.LookRotation(-sunDirection, Vector3.up);
            sunLight.useColorTemperature = false;

            if (sunHeight >= 0f)
            {
                sunLight.color = sunColor.Evaluate(Mathf.Clamp01(sunHeight / 0.6f));
                sunLight.intensity = maxSunIntensity * Mathf.SmoothStep(0f, 1f, dayAmount);
            }
            else
            {
                sunLight.intensity = 0f;
            }
        }

        float ambientNightScale = 1f - Mathf.Clamp01(nightDarkness * NightAmount);

        RenderSettings.ambientMode = AmbientMode.Flat;
        
        // FIX 1: Warna ambient-nya LANGSUNG dikaliin sama scale malam biar beneran jatuh ke Color.black!
        Color targetAmbient = ambientColor.Evaluate(dayFactor) * ambientNightScale;
        RenderSettings.ambientLight = targetAmbient;
        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, dayFactor) * ambientNightScale;

        // FIX 2: Matikan pantulan refleksi langit pas malam! (Biar mobil ga abu-abu mengkilap)
        RenderSettings.reflectionIntensity = Mathf.Lerp(0f, 1f, dayFactor) * ambientNightScale;

        // FIX 3: Paksa kabut (Fog) ikut jadi hitam pekat pas malam!
        if (RenderSettings.fog)
        {
            // Ambil warna kabut siang dari ambient, tapi pas malam lerp ke hitam pekat
            RenderSettings.fogColor = Color.Lerp(Color.black, ambientColor.Evaluate(dayFactor), dayFactor * ambientNightScale);
        }

        ApplyExtraLights();
        ApplyBakedLighting();

        if (cloudDome != null)
        {
            float cloudTint = Mathf.InverseLerp(-0.12f, 0.45f, sunHeight);

            cloudDome.ApplyManualPalette(
                cloudDome.litTint != null ? cloudDome.litTint.Evaluate(cloudTint) : cloudDome.litColor,
                cloudDome.shadowTint != null ? cloudDome.shadowTint.Evaluate(cloudTint) : cloudDome.shadowColor,
                cloudDome.sunGlowTint != null ? cloudDome.sunGlowTint.Evaluate(cloudTint) : cloudDome.sunGlowColor,
                Mathf.Lerp(cloudDome.nightOpacityMultiplier, 1f, cloudTint));
        }
    }

    void EnsureExtraLightsResolved()
    {
        if (_extraLightsResolved || !Application.isPlaying)
            return;

        _extraLightsResolved = true;

        List<Light> resolved = new List<Light>();

        if (extraLights != null && extraLights.Length > 0)
        {
            foreach (Light light in extraLights)
            {
                if (light != null && light != sunLight)
                    resolved.Add(light);
            }
        }
        else if (autoFindExtraLights)
        {
            Light[] lights = FindObjectsOfType<Light>();

            foreach (Light light in lights)
            {
                if (light == null || light == sunLight)
                    continue;

                if (light.type == LightType.Directional)
                    continue;

                if (light.transform.parent != null)
                    continue;

                resolved.Add(light);
            }
        }

        _resolvedExtraLights = resolved.ToArray();
        _extraLightBaseIntensities = new float[_resolvedExtraLights.Length];

        for (int i = 0; i < _resolvedExtraLights.Length; i++)
            _extraLightBaseIntensities[i] = _resolvedExtraLights[i].intensity;
    }

    void ApplyExtraLights()
    {
        if (!Application.isPlaying || _resolvedExtraLights == null)
            return;

        float multiplier = Mathf.Lerp(0f, 1f, NightAmount);

        for (int i = 0; i < _resolvedExtraLights.Length; i++)
        {
            if (_resolvedExtraLights[i] == null)
                continue;

            _resolvedExtraLights[i].intensity = _extraLightBaseIntensities[i] * multiplier;
        }
    }

    void RestoreExtraLights()
    {
        if (_resolvedExtraLights == null || _extraLightBaseIntensities == null)
            return;

        for (int i = 0; i < _resolvedExtraLights.Length; i++)
        {
            if (_resolvedExtraLights[i] != null)
                _resolvedExtraLights[i].intensity = _extraLightBaseIntensities[i];
        }
    }

    void EnsureBakedLightingCached()
    {
        if (_bakedLightingCached || !Application.isPlaying)
            return;

        _bakedLightingCached = true;
        _terrains = FindObjectsOfType<Terrain>();
        _terrainLightmapIndices = new int[_terrains.Length];

        for (int i = 0; i < _terrains.Length; i++)
            _terrainLightmapIndices[i] = _terrains[i] != null ? _terrains[i].lightmapIndex : -1;

        List<Renderer> lightmapped = new List<Renderer>();
        List<Renderer> probed = new List<Renderer>();
        Renderer[] renderers = FindObjectsOfType<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (renderer.lightmapIndex >= 0)
                lightmapped.Add(renderer);

            if (renderer.lightProbeUsage != LightProbeUsage.Off)
                probed.Add(renderer);
        }

        _lightmappedRenderers = lightmapped.ToArray();
        _rendererLightmapIndices = new int[_lightmappedRenderers.Length];

        for (int i = 0; i < _lightmappedRenderers.Length; i++)
            _rendererLightmapIndices[i] = _lightmappedRenderers[i].lightmapIndex;

        _probedRenderers = probed.ToArray();
        _rendererProbeUsages = new LightProbeUsage[_probedRenderers.Length];

        for (int i = 0; i < _probedRenderers.Length; i++)
            _rendererProbeUsages[i] = _probedRenderers[i].lightProbeUsage;
    }

    void ApplyBakedLighting()
    {
        if (!Application.isPlaying)
            return;

        EnsureBakedLightingCached();

        bool shouldDisable = NightAmount >= 0.98f;
        bool shouldRestore = SunElevation > 0.05f;

        bool night = _bakedLightingDisabled;

        if (shouldDisable)
            night = true;
        else if (shouldRestore)
            night = false;
        else
            return;

        if (night == _bakedLightingDisabled)
            return;

        _bakedLightingDisabled = night;

        if (disableTerrainLightmapAtNight && _terrains != null)
        {
            for (int i = 0; i < _terrains.Length; i++)
            {
                if (_terrains[i] == null)
                    continue;

                _terrains[i].lightmapIndex = night ? -1 : _terrainLightmapIndices[i];
                _terrains[i].Flush();
            }
        }

        if (disableAllLightmapsAtNight && _lightmappedRenderers != null)
        {
            for (int i = 0; i < _lightmappedRenderers.Length; i++)
            {
                if (_lightmappedRenderers[i] == null)
                    continue;

                _lightmappedRenderers[i].lightmapIndex = night ? -1 : _rendererLightmapIndices[i];
            }
        }

        if (disableLightProbesAtNight && _probedRenderers != null)
        {
            for (int i = 0; i < _probedRenderers.Length; i++)
            {
                if (_probedRenderers[i] == null)
                    continue;

                _probedRenderers[i].lightProbeUsage = night ? LightProbeUsage.Off : _rendererProbeUsages[i];
            }
        }
    }

    void RestoreBakedLighting()
    {
        if (!_bakedLightingCached)
            return;

        if (disableTerrainLightmapAtNight && _terrains != null)
        {
            for (int i = 0; i < _terrains.Length; i++)
            {
                if (_terrains[i] == null)
                    continue;

                _terrains[i].lightmapIndex = _terrainLightmapIndices[i];
                _terrains[i].Flush();
            }
        }

        if (disableAllLightmapsAtNight && _lightmappedRenderers != null)
        {
            for (int i = 0; i < _lightmappedRenderers.Length; i++)
            {
                if (_lightmappedRenderers[i] == null)
                    continue;

                _lightmappedRenderers[i].lightmapIndex = _rendererLightmapIndices[i];
            }
        }

        if (disableLightProbesAtNight && _probedRenderers != null)
        {
            for (int i = 0; i < _probedRenderers.Length; i++)
            {
                if (_probedRenderers[i] != null)
                    _probedRenderers[i].lightProbeUsage = _rendererProbeUsages[i];
            }
        }

        _bakedLightingDisabled = false;
    }

    void ResolveReferences()
    {
        if (cloudDome == null)
            cloudDome = FindObjectOfType<CloudDome>();

        if (sunLight == null)
            sunLight = RenderSettings.sun;

        if (sunLight == null)
        {
            Light[] lights = FindObjectsOfType<Light>();

            foreach (Light candidate in lights)
            {
                if (candidate.type == LightType.Directional && candidate.isActiveAndEnabled)
                {
                    sunLight = candidate;
                    break;
                }
            }
        }

        if (cloudDome != null && sunLight != null)
            cloudDome.sunLight = sunLight;
    }

    void OnGUI()
    {
        if (!showDebugClock || !Application.isPlaying)
            return;

        if (_clockStyle == null)
        {
            _clockStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
        }

        if (_debugStyle == null)
        {
            _debugStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
        }

        string text = string.Format("{0:00}:{1:00}", Hour, Minute);

        float firstLampIntensity = -1f;

        if (_resolvedExtraLights != null && _resolvedExtraLights.Length > 0 && _resolvedExtraLights[0] != null)
            firstLampIntensity = _resolvedExtraLights[0].intensity;

        Terrain terrain = Terrain.activeTerrain;
        int terrainLightmap = terrain != null ? terrain.lightmapIndex : -1;
        int lightmapCount = LightmapSettings.lightmaps != null ? LightmapSettings.lightmaps.Length : 0;

        int lampCount = _resolvedExtraLights != null ? _resolvedExtraLights.Length : 0;
        int rendererLightmapCount = _lightmappedRenderers != null ? _lightmappedRenderers.Length : 0;
        int probedCount = _probedRenderers != null ? _probedRenderers.Length : 0;
        int probeOffCount = 0;

        if (_probedRenderers != null)
        {
            for (int i = 0; i < _probedRenderers.Length; i++)
            {
                if (_probedRenderers[i] != null && _probedRenderers[i].lightProbeUsage == LightProbeUsage.Off)
                    probeOffCount++;
            }
        }

        string debug = string.Format(
            "amb {0:0.00} | night {1:0.00} | dark {2:0.00} | lamps {3}@{4:0.#}\nlm {5} | terLM {6} | rLM {7} | pOff {8}/{9}",
            RenderSettings.ambientIntensity,
            NightAmount,
            nightDarkness,
            lampCount,
            firstLampIntensity,
            lightmapCount,
            terrainLightmap,
            rendererLightmapCount,
            probeOffCount,
            probedCount);

        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(12f, 12f, 180f, 30f), text, _clockStyle);
        GUI.Label(new Rect(12f, 40f, 720f, 40f), debug, _debugStyle);
        GUI.color = previous;
        GUI.Label(new Rect(10f, 10f, 180f, 30f), text, _clockStyle);
        GUI.Label(new Rect(10f, 38f, 720f, 40f), debug, _debugStyle);
    }

    static Gradient MakeGradient(params Color[] colors)
    {
        Gradient gradient = new Gradient();
        GradientColorKey[] colorKeys = new GradientColorKey[colors.Length];
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[colors.Length];

        for (int i = 0; i < colors.Length; i++)
        {
            float t = colors.Length > 1 ? i / (float)(colors.Length - 1) : 0f;
            colorKeys[i] = new GradientColorKey(colors[i], t);
            alphaKeys[i] = new GradientAlphaKey(colors[i].a, t);
        }

        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }
}
