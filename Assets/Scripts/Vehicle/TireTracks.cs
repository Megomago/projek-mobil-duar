using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(VehicleController))]
public class TireTracks : MonoBehaviour
{
    [Header("=== TIRE TRACKS (jejak di pasir) ===")]
    public bool enableTracks = true;
    [Tooltip("Lebar jejak ban (meter)")]
    public float trackWidth = 0.26f;
    [Tooltip("Kecepatan minimal (km/h) sebelum mulai ninggalin jejak")]
    public float minSpeedKmh = 2f;
    [Tooltip("Cuma ninggalin jejak saat roda selip. Kalau false, selalu ninggalin jejak selama jalan")]
    public bool slipOnly = false;
    [Tooltip("Batas slip buat mode slipOnly")]
    public float slipThreshold = 0.35f;
    [Tooltip("Jarak antar titik segmen (meter) - makin kecil makin halus")]
    public float segmentSpacing = 0.18f;
    [Tooltip("Jarak jejak dari permukaan tanah biar gak z-fighting")]
    public float yOffset = 0.02f;
    [Tooltip("Berapa lama jejak bertahan (detik) sebelum memudar hilang")]
    public float markLifetime = 16f;
    [Tooltip("Jumlah maksimal titik per roda")]
    public int maxPointsPerWheel = 160;
    [Tooltip("Panjang 1 tile texture tread di dunia (meter)")]
    public float treadWorldLength = 0.7f;

    [Header("=== DUST INTERACTION ===")]
    [Tooltip("Sembunyikan TOTAL jejak ban saat debu aktif. Default OFF: debu digambar di atas track supaya track tetap ada tapi tertutup debu.")]
    public bool hideWhenDustActive = false;

    [Header("=== LOOK ===")]
    [Tooltip("Warna gelap cetakan. Alpha = seberapa pekat jejaknya")]
    public Color trackColor = new Color(0.32f, 0.27f, 0.20f, 0.65f);
    [Tooltip("Texture cetakan/tread (RGBA). Alpha = bentuk cetakan. Kosongkan kalau cuma pakai warna")]
    public Texture2D treadTexture;
    [Tooltip("Normal map cetakan. Import sebagai 'Normal map' di Unity. INI yang bikin tanah kelihatan terpijak/cekung")]
    public Texture2D treadNormalMap;
    [Tooltip("Kekuatan relief dari normal map")]
    [Range(0f, 3f)] public float normalStrength = 1.2f;
    [Tooltip("Kekuatan bayangan di tepi/ujung pijakan (diambil dari kemiringan normal map)")]
    [Range(0f, 3f)] public float edgeShadow = 1.4f;
    [Tooltip("Cahaya ambient biar jejak gak hitam total di tempat gelap")]
    [Range(0f, 1f)] public float ambient = 0.6f;
    [Tooltip("Material override (opsional). Kalau kosong, dibuat otomatis pakai shader Custom/TireTrack")]
    public Material trackMaterial;

    private class WheelTrack
    {
        public Mesh mesh;
        public MeshFilter filter;
        public MeshRenderer renderer;
        public readonly List<Vector3> points = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<float> times = new List<float>();
        public readonly List<bool> connect = new List<bool>();
        public bool wasGrounded;
        public bool dirty;
    }

    private const float FadeRebuildInterval = 0.1f;

    private VehicleController _vc;
    private WheelEffects _wheelEffects;
    private GameObject _container;
    private WheelTrack[] _tracks;
    private Material _material;
    private float _fadeTimer;
    private bool _hiddenByDust;

    private readonly List<Vector3> _verts = new List<Vector3>(1024);
    private readonly List<Vector3> _norms = new List<Vector3>(1024);
    private readonly List<Vector4> _tans = new List<Vector4>(1024);
    private readonly List<Vector2> _uvs = new List<Vector2>(1024);
    private readonly List<Color> _cols = new List<Color>(1024);
    private readonly List<int> _tris = new List<int>(1536);
    private readonly List<float> _vCoord = new List<float>(256);

    private void Awake()
    {
        _vc = GetComponent<VehicleController>();
        _wheelEffects = GetComponent<WheelEffects>();
        if (!enableTracks || _vc == null || _vc.wheels == null || _vc.wheels.Length == 0) return;

        _container = new GameObject("TireTracks_" + name);
        _container.transform.SetParent(null);

        _tracks = new WheelTrack[_vc.wheels.Length];
        for (int i = 0; i < _vc.wheels.Length; i++)
        {
            var go = new GameObject("Track_" + i);
            go.transform.SetParent(_container.transform, false);

            var track = new WheelTrack
            {
                mesh = new Mesh { name = "TireTrackMesh_" + i },
                filter = go.AddComponent<MeshFilter>(),
                renderer = go.AddComponent<MeshRenderer>()
            };
            track.mesh.MarkDynamic();
            track.filter.sharedMesh = track.mesh;
            track.renderer.sharedMaterial = GetTrackMaterial();
            track.renderer.shadowCastingMode = ShadowCastingMode.Off;
            track.renderer.receiveShadows = false;
            track.renderer.lightProbeUsage = LightProbeUsage.Off;
            track.renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            _tracks[i] = track;
        }
    }

    private void Update()
    {
        if (_tracks == null) return;

        if (_wheelEffects == null) _wheelEffects = GetComponent<WheelEffects>();
        bool dustActive = hideWhenDustActive && _wheelEffects != null && _wheelEffects.IsDustActive;

        if (_hiddenByDust != dustActive)
        {
            _hiddenByDust = dustActive;
            if (_container != null) _container.SetActive(!dustActive);

            if (!dustActive)
            {
                for (int i = 0; i < _tracks.Length; i++)
                    if (_tracks[i] != null) _tracks[i].dirty = true;
            }
        }

        _fadeTimer -= Time.deltaTime;
        bool fadeTick = _fadeTimer <= 0f;
        if (fadeTick) _fadeTimer = FadeRebuildInterval;

        float speed = _vc.speedKmh;
        bool canTrack = !_vc.movementLocked;
        bool fastEnough = canTrack && speed > minSpeedKmh;

        for (int i = 0; i < _vc.wheels.Length && i < _tracks.Length; i++)
        {
            var w = _vc.wheels[i];
            var track = _tracks[i];

            if (canTrack)
            {
                WheelHit hit = default;
                bool grounded = w.collider != null && w.collider.GetGroundHit(out hit);
                float slip = 0f;
                if (grounded) slip = Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);

                bool qualified = grounded && fastEnough && (!slipOnly || slip > slipThreshold);

                if (qualified)
                {
                    Vector3 p = hit.point + hit.normal * yOffset;

                    if (track.points.Count == 0)
                    {
                        AddPoint(track, p, hit.normal, false);
                        track.dirty = true;
                    }
                    else
                    {
                        float d = Vector3.Distance(p, track.points[track.points.Count - 1]);
                        if (!track.wasGrounded || d > segmentSpacing * 8f)
                        {
                            AddPoint(track, p, hit.normal, false);
                            track.dirty = true;
                        }
                        else if (d >= segmentSpacing)
                        {
                            AddPoint(track, p, hit.normal, true);
                            track.dirty = true;
                        }
                    }
                }

                track.wasGrounded = grounded;
            }
            else
            {
                track.wasGrounded = false;
            }

            if (fadeTick)
            {
                int before = track.points.Count;
                DiscardOldest(track);
                if (track.points.Count != before) track.dirty = true;
            }

            if (!dustActive && (track.dirty || (fadeTick && track.points.Count > 0)))
            {
                Rebuild(track);
                track.dirty = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (_container != null) Destroy(_container);
        if (_material != null) Destroy(_material);
    }

    private void AddPoint(WheelTrack track, Vector3 point, Vector3 normal, bool connect)
    {
        track.points.Add(point);
        track.normals.Add(normal);
        track.times.Add(Time.time);
        track.connect.Add(connect);

        while (track.points.Count > maxPointsPerWheel)
        {
            track.points.RemoveAt(0);
            track.normals.RemoveAt(0);
            track.times.RemoveAt(0);
            track.connect.RemoveAt(0);
            if (track.connect.Count > 0) track.connect[0] = false;
        }
    }

    private void DiscardOldest(WheelTrack track)
    {
        while (track.points.Count > 0 && Time.time - track.times[0] > markLifetime)
        {
            track.points.RemoveAt(0);
            track.normals.RemoveAt(0);
            track.times.RemoveAt(0);
            track.connect.RemoveAt(0);
        }
        if (track.connect.Count > 0) track.connect[0] = false;
    }

    private void Rebuild(WheelTrack track)
    {
        var mesh = track.mesh;
        mesh.Clear();

        int n = track.points.Count;
        if (n < 2) return;

        // Hitung koordinat V (memanjang) tiap titik, reset di tiap break.
        _vCoord.Clear();
        float dist = 0f;
        for (int i = 0; i < n; i++)
        {
            if (i > 0)
            {
                if (track.connect[i]) dist += Vector3.Distance(track.points[i], track.points[i - 1]);
                else dist = 0f;
            }
            _vCoord.Add(dist / Mathf.Max(0.01f, treadWorldLength));
        }

        _verts.Clear();
        _norms.Clear();
        _tans.Clear();
        _uvs.Clear();
        _cols.Clear();
        _tris.Clear();

        float halfWidth = trackWidth * 0.5f;

        for (int i = 1; i < n; i++)
        {
            if (!track.connect[i]) continue;

            Vector3 a = track.points[i - 1];
            Vector3 b = track.points[i];
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 1e-6f) continue;
            dir.Normalize();

            Vector3 nA = track.normals[i - 1];
            Vector3 nB = track.normals[i];

            Vector3 sideA = Vector3.Cross(nA, dir);
            Vector3 sideB = Vector3.Cross(nB, dir);
            if (sideA.sqrMagnitude < 1e-6f || sideB.sqrMagnitude < 1e-6f) continue;
            sideA = sideA.normalized * halfWidth;
            sideB = sideB.normalized * halfWidth;

            float ageA = Time.time - track.times[i - 1];
            float ageB = Time.time - track.times[i];
            float alphaA = Mathf.Clamp01(1f - ageA / markLifetime);
            float alphaB = Mathf.Clamp01(1f - ageB / markLifetime);

            int baseIndex = _verts.Count;

            Vector3 va0 = a - sideA;
            Vector3 va1 = a + sideA;
            Vector3 vb0 = b - sideB;
            Vector3 vb1 = b + sideB;

            _verts.Add(va0); _verts.Add(va1); _verts.Add(vb0); _verts.Add(vb1);

            _norms.Add(nA); _norms.Add(nA); _norms.Add(nB); _norms.Add(nB);

            _tans.Add(new Vector4(sideA.normalized.x, sideA.normalized.y, sideA.normalized.z, -1f));
            _tans.Add(new Vector4(sideA.normalized.x, sideA.normalized.y, sideA.normalized.z, -1f));
            _tans.Add(new Vector4(sideB.normalized.x, sideB.normalized.y, sideB.normalized.z, -1f));
            _tans.Add(new Vector4(sideB.normalized.x, sideB.normalized.y, sideB.normalized.z, -1f));

            _uvs.Add(new Vector2(0f, _vCoord[i - 1]));
            _uvs.Add(new Vector2(1f, _vCoord[i - 1]));
            _uvs.Add(new Vector2(0f, _vCoord[i]));
            _uvs.Add(new Vector2(1f, _vCoord[i]));

            _cols.Add(new Color(1f, 1f, 1f, alphaA));
            _cols.Add(new Color(1f, 1f, 1f, alphaA));
            _cols.Add(new Color(1f, 1f, 1f, alphaB));
            _cols.Add(new Color(1f, 1f, 1f, alphaB));

            _tris.Add(baseIndex + 0); _tris.Add(baseIndex + 2); _tris.Add(baseIndex + 1);
            _tris.Add(baseIndex + 1); _tris.Add(baseIndex + 2); _tris.Add(baseIndex + 3);
        }

        if (_verts.Count == 0) return;

        mesh.SetVertices(_verts);
        mesh.SetNormals(_norms);
        mesh.SetTangents(_tans);
        mesh.SetUVs(0, _uvs);
        mesh.SetColors(_cols);
        mesh.SetTriangles(_tris, 0);
        mesh.RecalculateBounds();
    }

    private Material GetTrackMaterial()
    {
        if (trackMaterial != null) return trackMaterial;
        if (_material != null) return _material;

        Material baseMat = Resources.Load<Material>("VehicleFX/TireTrack");
        if (baseMat != null)
        {
            _material = new Material(baseMat) { name = "TireTrack_Runtime" };
        }
        else
        {
            Shader sh = Shader.Find("Custom/TireTrack");
            if (sh == null)
            {
                Debug.LogWarning("TireTracks: shader Custom/TireTrack tidak ketemu. Menggunakan fallback shader URP.");
                sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh == null) sh = Shader.Find("Unlit/Transparent");
            }

            if (sh == null)
            {
                Debug.LogError("TireTracks: Tidak ditemukan shader fallback yang valid!");
                return null;
            }

            _material = new Material(sh) { name = "TireTrack_Runtime" };
        }
        if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", trackColor);
        if (_material.HasProperty("_Color")) _material.SetColor("_Color", trackColor);
        if (_material.HasProperty("_NormalStrength")) _material.SetFloat("_NormalStrength", normalStrength);
        if (_material.HasProperty("_EdgeShadow")) _material.SetFloat("_EdgeShadow", edgeShadow);
        if (_material.HasProperty("_Ambient")) _material.SetFloat("_Ambient", ambient);

        if (treadTexture != null)
        {
            if (_material.HasProperty("_BaseMap")) _material.SetTexture("_BaseMap", treadTexture);
            if (_material.HasProperty("_MainTex")) _material.SetTexture("_MainTex", treadTexture);
        }

        if (treadNormalMap != null && _material.HasProperty("_NormalMap"))
            _material.SetTexture("_NormalMap", treadNormalMap);

        _material.renderQueue = (int)RenderQueue.Transparent + 10;
        return _material;
    }
}
