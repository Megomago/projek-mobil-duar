using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("NNTI/Environment/Cloud Dome")]
public class CloudDome : MonoBehaviour
{
    public const string ShaderName = "Custom/CloudDome";

    [Header("=== RENDERER ===")]
    [Tooltip("Shader awan. Kosongin aja kalau mau auto cari 'Custom/CloudDome'.")]
    public Shader cloudShader;

    [Tooltip("Kamera yang diikuti. Kosongin aja untuk auto pakai Camera.main.")]
    public Camera targetCamera;

    [Tooltip("Radius dome = far clip kamera x nilai ini. Harus di bawah 1 biar ga ke-clip.")]
    [Range(0.5f, 0.98f)] public float radiusFromFarClip = 0.9f;

    [Tooltip("Dome selalu ikut posisi kamera supaya awan terasa tanpa batas.")]
    public bool followCamera = true;

    [Header("=== CLOUD SHAPE ===")]
    [Range(0f, 1f)] public float coverage = 0.45f;

    [Range(0.01f, 0.8f)] public float softness = 0.28f;

    [Range(0f, 1f)] public float opacity = 1f;

    [Min(0.1f)] public float cloudScale = 2.6f;

    [Range(0.05f, 1f)] public float projectionAltitude = 0.32f;

    [Range(0.01f, 0.6f)] public float horizonFade = 0.12f;

    [Header("=== WIND ===")]
    [Min(0f)] public float windSpeed = 0.012f;

    public Vector2 windDirection = new Vector2(1f, 0.25f);

    [Header("=== DAY / NIGHT ===")]
    [Tooltip("Warna awan otomatis ngikutin ketinggian matahari/bulan.")]
    public bool autoTintFromSun = true;

    [Tooltip("Directional light matahari/bulan. Kosongin aja untuk auto pakai RenderSettings.sun.")]
    public Light sunLight;

    [Range(0f, 1f)] public float nightOpacityMultiplier = 0.72f;

    [Range(1f, 16f)] public float sunGlowPower = 4f;

    [Range(0f, 3f)] public float sunGlowStrength = 0.7f;

    [Tooltip("Gradient warna awan dari malam (kiri) ke siang (kanan).")]
    public Gradient litTint = MakeGradient(
        new Color(0.22f, 0.27f, 0.40f),
        new Color(0.95f, 0.50f, 0.34f),
        new Color(1.00f, 0.93f, 0.86f),
        new Color(1.00f, 1.00f, 1.00f));

    [Tooltip("Gradient bayangan awan dari malam (kiri) ke siang (kanan).")]
    public Gradient shadowTint = MakeGradient(
        new Color(0.09f, 0.12f, 0.21f),
        new Color(0.28f, 0.20f, 0.30f),
        new Color(0.48f, 0.53f, 0.64f),
        new Color(0.56f, 0.62f, 0.72f));

    [Tooltip("Gradient glow matahari dari malam (kiri) ke siang (kanan).")]
    public Gradient sunGlowTint = MakeGradient(
        new Color(0.35f, 0.45f, 0.75f),
        new Color(1.00f, 0.42f, 0.15f),
        new Color(1.00f, 0.82f, 0.55f),
        new Color(1.00f, 0.96f, 0.86f));

    [Tooltip("Dipakai kalau Auto Tint From Sun dimatikan.")]
    public Color litColor = Color.white;

    public Color shadowColor = new Color(0.56f, 0.62f, 0.72f);

    public Color sunGlowColor = new Color(1f, 0.93f, 0.8f);

    [Tooltip("Pengali opacity tambahan saat warna diatur manual (dipakai Time Of Day).")]
    [Range(0f, 1f)] public float manualOpacityMultiplier = 1f;

    [Tooltip("Arah matahari kalau scene ga punya directional light.")]
    public Vector3 fallbackSunDirection = new Vector3(0.35f, 0.6f, 0.2f);

    static readonly int TintId = Shader.PropertyToID("_Tint");
    static readonly int ShadowTintId = Shader.PropertyToID("_ShadowTint");
    static readonly int SunColorId = Shader.PropertyToID("_SunColor");
    static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
    static readonly int CoverageId = Shader.PropertyToID("_Coverage");
    static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    static readonly int ScaleId = Shader.PropertyToID("_Scale");
    static readonly int AltitudeId = Shader.PropertyToID("_Altitude");
    static readonly int HorizonFadeId = Shader.PropertyToID("_HorizonFade");
    static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
    static readonly int WindDirectionId = Shader.PropertyToID("_WindDirection");
    static readonly int SunPowerId = Shader.PropertyToID("_SunPower");
    static readonly int SunStrengthId = Shader.PropertyToID("_SunStrength");

    Mesh _mesh;
    Material _material;
    MeshFilter _meshFilter;
    MeshRenderer _meshRenderer;
    Vector3 _sunDirection = new Vector3(0.35f, 0.6f, 0.2f);
    bool _sunSearched;

    void OnEnable()
    {
        EnsureResources();
        Refresh();
    }

    void OnDisable()
    {
        ReleaseResources();
    }

    void OnValidate()
    {
        if (cloudShader == null)
            cloudShader = Shader.Find(ShaderName);

        if (!isActiveAndEnabled || _material == null)
            return;

        Refresh();
    }

    void LateUpdate()
    {
        if (!isActiveAndEnabled)
            return;

        EnsureResources();
        Refresh();
    }

    public void SetManualColors(Color cloudLit, Color cloudShadow, Color sunGlow)
    {
        litColor = cloudLit;
        shadowColor = cloudShadow;
        sunGlowColor = sunGlow;
        autoTintFromSun = false;

        if (_material != null)
            ApplyMaterial();
    }

    public void SetOpacity(float value)
    {
        opacity = Mathf.Clamp01(value);

        if (_material != null)
            ApplyMaterial();
    }

    public void ForceRefresh()
    {
        EnsureResources();
        Refresh();
    }

    public void ApplyManualPalette(Color cloudLit, Color cloudShadow, Color sunGlow, float opacityMultiplier)
    {
        autoTintFromSun = false;
        litColor = cloudLit;
        shadowColor = cloudShadow;
        sunGlowColor = sunGlow;
        manualOpacityMultiplier = Mathf.Clamp01(opacityMultiplier);

        if (_material != null)
            ApplyMaterial();
    }

    void Refresh()
    {
        if (_material == null)
            return;

        Camera cam = ResolveCamera();

        if (cam != null && followCamera)
        {
            float radius = Mathf.Max(1f, cam.farClipPlane * radiusFromFarClip);
            transform.SetPositionAndRotation(cam.transform.position, Quaternion.identity);
            transform.localScale = Vector3.one * radius;
        }

        UpdateSunDirection();
        ApplyMaterial();
    }

    void ApplyMaterial()
    {
        _material.SetFloat(CoverageId, coverage);
        _material.SetFloat(SoftnessId, softness);
        _material.SetFloat(ScaleId, cloudScale);
        _material.SetFloat(AltitudeId, projectionAltitude);
        _material.SetFloat(HorizonFadeId, horizonFade);
        _material.SetFloat(WindSpeedId, windSpeed);
        _material.SetFloat(SunPowerId, sunGlowPower);
        _material.SetFloat(SunStrengthId, sunGlowStrength);
        _material.SetVector(WindDirectionId, new Vector4(windDirection.x, 0f, windDirection.y, 0f));
        _material.SetVector(SunDirectionId, new Vector4(_sunDirection.x, _sunDirection.y, _sunDirection.z, 0f));

        Color lit;
        Color shadow;
        Color glow;
        float finalOpacity;

        if (autoTintFromSun)
        {
            float t = Mathf.InverseLerp(-0.12f, 0.45f, _sunDirection.y);

            lit = litTint != null ? litTint.Evaluate(t) : litColor;
            shadow = shadowTint != null ? shadowTint.Evaluate(t) : shadowColor;
            glow = sunGlowTint != null ? sunGlowTint.Evaluate(t) : sunGlowColor;
            finalOpacity = opacity * Mathf.Lerp(nightOpacityMultiplier, 1f, t);
        }
        else
        {
            lit = litColor;
            shadow = shadowColor;
            glow = sunGlowColor;
            finalOpacity = opacity * manualOpacityMultiplier;
        }

        _material.SetColor(TintId, lit);
        _material.SetColor(ShadowTintId, shadow);
        _material.SetColor(SunColorId, glow);
        _material.SetFloat(OpacityId, finalOpacity);
    }

    void UpdateSunDirection()
    {
        Light light = sunLight != null ? sunLight : RenderSettings.sun;

        if (light == null && !_sunSearched)
        {
            _sunSearched = true;

            Light[] lights = FindObjectsOfType<Light>();
            foreach (Light candidate in lights)
            {
                if (candidate.type == LightType.Directional && candidate.isActiveAndEnabled)
                {
                    sunLight = candidate;
                    light = candidate;
                    break;
                }
            }
        }

        _sunDirection = light != null
            ? (-light.transform.forward).normalized
            : fallbackSunDirection.normalized;
    }

    Camera ResolveCamera()
    {
        if (targetCamera != null)
            return targetCamera;

        Camera main = Camera.main;
        if (main != null)
            return main;

        Camera[] cameras = Camera.allCameras;
        return cameras.Length > 0 ? cameras[0] : null;
    }

    void EnsureResources()
    {
        if (cloudShader == null)
            cloudShader = Shader.Find(ShaderName);

        if (_meshFilter == null)
        {
            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null)
                _meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        if (_meshRenderer == null)
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        if (_mesh == null)
            _mesh = BuildDomeMesh();

        if (_meshFilter.sharedMesh != _mesh)
            _meshFilter.sharedMesh = _mesh;

        if (cloudShader != null && (_material == null || _material.shader != cloudShader))
        {
            ReleaseMaterial();

            _material = new Material(cloudShader)
            {
                name = "CloudDome (Generated)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (_material != null && _meshRenderer.sharedMaterial != _material)
            _meshRenderer.sharedMaterial = _material;

        _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
        _meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    void ReleaseResources()
    {
        if (_meshRenderer != null)
            _meshRenderer.sharedMaterial = null;

        if (_meshFilter != null)
            _meshFilter.sharedMesh = null;

        ReleaseMaterial();

        if (_mesh != null)
        {
            DestroySafe(_mesh);
            _mesh = null;
        }
    }

    void ReleaseMaterial()
    {
        if (_material == null)
            return;

        DestroySafe(_material);
        _material = null;
    }

    static void DestroySafe(Object target)
    {
        if (Application.isPlaying)
            Object.Destroy(target);
        else
            Object.DestroyImmediate(target);
    }

    static Mesh BuildDomeMesh()
    {
        const int segments = 72;
        const int rings = 28;
        const float bottomAngle = -20f * Mathf.Deg2Rad;

        List<Vector3> vertices = new List<Vector3>((segments + 1) * (rings + 1));
        List<int> triangles = new List<int>(segments * rings * 6);

        for (int ring = 0; ring <= rings; ring++)
        {
            float t = ring / (float)rings;
            float phi = Mathf.Lerp(bottomAngle, Mathf.PI * 0.5f, t);
            float y = Mathf.Sin(phi);
            float radius = Mathf.Cos(phi);

            for (int segment = 0; segment <= segments; segment++)
            {
                float theta = segment / (float)segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(theta) * radius, y, Mathf.Sin(theta) * radius));
            }
        }

        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int a = ring * (segments + 1) + segment;
                int b = a + segments + 1;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(a + 1);

                triangles.Add(a + 1);
                triangles.Add(b);
                triangles.Add(b + 1);
            }
        }

        Mesh mesh = new Mesh
        {
            name = "CloudDome (Generated)",
            hideFlags = HideFlags.HideAndDontSave
        };

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        return mesh;
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
