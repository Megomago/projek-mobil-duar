using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
[AddComponentMenu("NNTI/Environment/Particle Day Night Tint")]
public class ParticleDayNightTint : MonoBehaviour
{
    [Header("=== TIME OF DAY ===")]
    [Tooltip("Kosongin aja untuk auto cari Time Of Day di scene.")]
    public TimeOfDayController timeOfDay;

    [Header("=== MATERIAL ===")]
    [Tooltip("Material template Custom/DustParticleAlpha. Dibuat otomatis lewat menu GameObject > NNTI.")]
    public Material dustMaterial;

    [Header("=== DAY ===")]
    public Color dayColor = new Color(0.80f, 0.72f, 0.58f, 1f);

    [Tooltip("Opacity debu saat siang. Makin besar makin menutup pandangan.")]
    [Range(0f, 1f)] public float dayOpacity = 0.35f;

    [Header("=== NIGHT ===")]
    public Color nightColor = new Color(0.18f, 0.22f, 0.30f, 1f);

    [Tooltip("Opacity debu saat malam. Tetap terlihat samar, tapi tidak oranye.")]
    [Range(0f, 1f)] public float nightOpacity = 0.25f;

    static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    ParticleSystem _particles;
    ParticleSystemRenderer _particleRenderer;
    Material _originalMaterial;
    Material _material;
    bool _ready;

    void Awake()
    {
        _particles = GetComponent<ParticleSystem>();
        _particleRenderer = GetComponent<ParticleSystemRenderer>();

        if (timeOfDay == null)
            timeOfDay = FindObjectOfType<TimeOfDayController>();

        SetupMaterial();
    }

    void SetupMaterial()
    {
        if (_particleRenderer == null)
            return;

        _originalMaterial = _particleRenderer.sharedMaterial;

        if (dustMaterial != null)
        {
            _material = new Material(dustMaterial);
        }
        else
        {
            Shader shader = Shader.Find("Custom/DustParticleAlpha");

            if (shader == null)
            {
                Debug.LogWarning("ParticleDayNightTint: shader Custom/DustParticleAlpha tidak ditemukan.", this);
                return;
            }

            _material = new Material(shader);

            if (_originalMaterial != null && _originalMaterial.HasProperty(MainTexId))
                _material.SetTexture(MainTexId, _originalMaterial.GetTexture(MainTexId));
        }

        if (_originalMaterial != null && _material.HasProperty(MainTexId)
            && _material.GetTexture(MainTexId) == null && _originalMaterial.HasProperty(MainTexId))
        {
            _material.SetTexture(MainTexId, _originalMaterial.GetTexture(MainTexId));
        }

        _material.name = "Dust Particle (Runtime)";
        _particleRenderer.sharedMaterial = _material;
        _ready = true;
    }

    void LateUpdate()
    {
        if (!_ready)
            return;

        if (timeOfDay == null)
        {
            timeOfDay = FindObjectOfType<TimeOfDayController>();

            if (timeOfDay == null)
                return;
        }

        Apply(timeOfDay.NightAmount);
    }

    public void Refresh()
    {
        if (!_ready)
            return;

        if (timeOfDay == null)
            timeOfDay = FindObjectOfType<TimeOfDayController>();

        if (timeOfDay != null)
            Apply(timeOfDay.NightAmount);
    }

    void Apply(float night)
    {
        _material.SetColor(TintColorId, Color.Lerp(dayColor, nightColor, night));
        _material.SetFloat(AlphaId, Mathf.Lerp(dayOpacity, nightOpacity, night));
    }

    void OnDisable()
    {
        if (_particleRenderer != null && _originalMaterial != null)
            _particleRenderer.sharedMaterial = _originalMaterial;
    }

    void OnDestroy()
    {
        if (_particleRenderer != null && _originalMaterial != null)
            _particleRenderer.sharedMaterial = _originalMaterial;

        if (_material != null)
            Destroy(_material);
    }
}

