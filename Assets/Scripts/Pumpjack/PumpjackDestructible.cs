using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using C4;

/// <summary>
/// Destruksi khusus Pumpjack saat kena C4.
///
/// Pumpjack sengaja TIDAK punya health/damage receiver, jadi tidak bisa
/// dihancurkan oleh peluru atau amunisi explosive biasa. Script ini cuma
/// mendengarkan event C4Explosive.OnDetonated.
///
/// Dua mode:
/// 1. BROKEN PREFAB (disarankan) - kalau slot "brokenPrefab" diisi, pumpjack
///    asli disembunyikan lalu prefab rusak di-spawn di posisi yang sama.
///    Tiap mesh di prefab rusak dapat Rigidbody + collider otomatis dan
///    dilempar dari titik ledakan.
/// 2. PECAH MESH ASLI - kalau "brokenPrefab" kosong, mesh pumpjack asli
///    di-unparent dan dipecah seperti sebelumnya.
///
/// Tidak perlu menyiapkan Rigidbody/collider manual. Collider yang sudah ada
/// tetap dipakai; mesh yang belum punya collider diberi BoxCollider otomatis.
/// </summary>
[DisallowMultipleComponent]
public class PumpjackDestructible : MonoBehaviour
{
    [Header("=== DETEKSI LEDAKAN C4 ===")]
    [Tooltip("Tambahan radius dari radius ledakan C4. 0 = hanya pecah kalau titik ledakan masih di dalam radius C4.")]
    [Min(0f)]
    public float breakRadiusPadding = 1.0f;

    [Header("=== PREFAB RUSAK (DISARANKAN) ===")]
    [Tooltip("Prefab pumpjack rusak dengan mesh terpisah. Kalau diisi, pumpjack asli diganti prefab ini saat kena C4.")]
    public GameObject brokenPrefab;

    [Tooltip("Offset lokal prefab rusak dari pivot pumpjack asli.")]
    public Vector3 brokenLocalPosition = Vector3.zero;

    [Tooltip("Rotasi lokal prefab rusak. Biarkan 0 kalau model sudah tegak saat di-preview di Unity.")]
    public Vector3 brokenLocalEulerAngles = Vector3.zero;

    [Tooltip("Skala lokal prefab rusak.")]
    public Vector3 brokenLocalScale = Vector3.one;

    [Tooltip("Nama child di prefab rusak yang TIDAK ikut terlempar (misal base/pondasi). Kosong = semua mesh ikut terlempar.")]
    public string staticRootName = "";

    [Tooltip("Samakan layer prefab rusak dengan layer pumpjack asli.")]
    public bool copyLayerToBroken = true;

    [Tooltip("Hapus controller + sisa efek setelah VFX/suara api selesai. Debris tetap aman karena dilepas dari controller.")]
    public bool destroyAfterEffects = true;

    [Header("=== PECAH / DEBRIS ===")]
    [Tooltip("Hanya dipakai kalau brokenPrefab kosong: pecah tiap mesh jadi debris sendiri.")]
    public bool splitEveryMesh = true;

    [Tooltip("Kecepatan lempar debris (m/s). Dipakai ForceMode.VelocityChange, jadi tidak tergantung massa.")]
    [Min(0f)]
    public float debrisLaunchSpeed = 14f;

    [Tooltip("Dorongan tambahan ke atas (0 = datar, 1 = agak naik).")]
    [Range(0f, 2f)]
    public float upwardsModifier = 0.45f;

    [Tooltip("Putaran acak debris dalam radian/detik.")]
    [Min(0f)]
    public float randomTorque = 3f;

    [Tooltip("Aktifkan fisika debris. Kalau false, debris cuma terpisah tanpa terlempar.")]
    public bool enablePhysics = true;

    [Tooltip("Hapus debris setelah X detik. 0 = permanen.")]
    [Min(0f)]
    public float debrisLifetime = 0f;

    [Tooltip("Batas massa debris (kg). Massa dihitung otomatis dari volume bounds.")]
    [Min(0.1f)]
    public float minDebrisMass = 8f;

    [Min(0.1f)]
    public float maxDebrisMass = 80f;

    [Tooltip("Hanya dipakai kalau brokenPrefab kosong: matikan collider lama yang tidak ikut jadi debris.")]
    public bool disableLeftoverColliders = true;

    [Header("=== VFX API DARI PIPA BOR ===")]
    [Tooltip("Prefab VFX api. Contoh: WFX_GazFireBig (Black Smoke).")]
    public GameObject fireVfxPrefab;

    [Tooltip("Titik munculnya api. Kosong = otomatis di bawah tengah pumpjack.")]
    public Transform fireVfxPoint;

    [Tooltip("Offset tambahan dari fireVfxPoint / titik fallback.")]
    public Vector3 fireVfxOffset = Vector3.zero;

    [Tooltip("Jeda sebelum VFX api muncul (detik).")]
    [Min(0f)]
    public float fireVfxDelay = 0.8f;

    [Tooltip("Hapus VFX api setelah X detik. 0 = biarkan prefab mengatur sendiri.")]
    [Min(0f)]
    public float fireVfxLifetime = 8f;

    [Header("=== SUARA API ===")]
    [Tooltip("Clip suara api/kebakaran. Contoh: api kebakar.mp3.")]
    public AudioClip fireSound;

    [Tooltip("Jeda sebelum suara api mulai (detik).")]
    [Min(0f)]
    public float fireSoundDelay = 0.8f;

    [Range(0f, 1f)]
    public float fireSoundVolume = 1f;

    [Tooltip("Loop suara api selama fireSoundDuration.")]
    public bool loopFireSound = true;

    [Tooltip("Berapa lama suara api diputar. 0 = selama clip.")]
    [Min(0f)]
    public float fireSoundDuration = 25f;

    [Min(1f)]
    public float fireSoundMaxDistance = 200f;

    [Header("=== EVENT OPSIONAL ===")]
    [Tooltip("Dipanggil sekali setelah pumpjack pecah.")]
    public UnityEvent onBroken;

    private bool _isBroken;
    private Vector3 _firePoint;
    private Animator[] _animators;
    private readonly List<Collider> _allColliders = new List<Collider>();

    public bool IsBroken => _isBroken;

    private void Awake()
    {
        _animators = GetComponentsInChildren<Animator>(true);
        CacheColliders();
    }

    private void OnEnable()
    {
        C4Explosive.OnDetonated += HandleC4Detonated;
    }

    private void OnDisable()
    {
        C4Explosive.OnDetonated -= HandleC4Detonated;
    }

    private void HandleC4Detonated(C4Explosive explosive)
    {
        if (_isBroken || explosive == null) return;

        float radius = Mathf.Max(0.01f, explosive.explosionRadius);
        float distance = GetClosestDistance(explosive.transform.position);

        if (distance > radius + breakRadiusPadding) return;

        Break(explosive.transform.position, radius);
    }

    /// <summary>
    /// Pecah pumpjack. Bisa dipanggil manual dari script lain / Context Menu.
    /// </summary>
    public void Break(Vector3 blastPosition, float blastRadius)
    {
        if (_isBroken) return;
        _isBroken = true;

        // Cache titik api sebelum hierarchy / renderer dimatikan.
        _firePoint = GetFirePoint();

        if (brokenPrefab != null)
            SpawnBrokenPrefab(blastPosition, blastRadius);
        else
            BreakIntoSelfDebris(blastPosition, blastRadius);

        StartCoroutine(SpawnFireVfxRoutine());
        StartCoroutine(PlayFireSoundRoutine());

        onBroken?.Invoke();
    }

    // ================================================================
    // MODE 1: GANTI KE PREFAB RUSAK
    // ================================================================

    private void SpawnBrokenPrefab(Vector3 blastPosition, float blastRadius)
    {
        int intactLayer = gameObject.layer;

        // Sembunyikan model asli. Root tetap hidup sebagai controller supaya
        // coroutine VFX/suara api tidak ikut mati.
        DisableIntactVisuals();

        GameObject broken = Instantiate(brokenPrefab, transform);
        broken.name = brokenPrefab.name + " (Broken)";
        broken.transform.localPosition = brokenLocalPosition;
        broken.transform.localRotation = Quaternion.Euler(brokenLocalEulerAngles);
        broken.transform.localScale = brokenLocalScale;

        if (copyLayerToBroken)
            SetLayerRecursively(broken, intactLayer);

        // Animasi bawaan FBX bisa menggeser pose; matikan supaya diam di pose rusak.
        Animator[] animators = broken.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
                animators[i].enabled = false;
        }

        Transform staticRoot = null;
        if (!string.IsNullOrEmpty(staticRootName))
        {
            staticRoot = FindDeepChild(broken.transform, staticRootName);

            if (staticRoot == null)
            {
                Debug.LogWarning(
                    $"[Pumpjack] Static root '{staticRootName}' tidak ditemukan di prefab rusak. Semua mesh akan ikut terlempar.", this);
            }
            else
            {
                Debug.Log($"[Pumpjack] Static root ditemukan: '{staticRoot.name}' (tidak ikut terlempar).", this);
            }
        }

        List<GameObject> parts = CollectMeshParts(broken, staticRoot);
        int launched = 0;

        for (int i = 0; i < parts.Count; i++)
        {
            GameObject part = parts[i];
            if (part == null) continue;

            if (enablePhysics)
            {
                EnsureCollider(part);

                Rigidbody rb = part.GetComponent<Rigidbody>();
                if (rb == null)
                    rb = part.AddComponent<Rigidbody>();

                rb.useGravity = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.drag = 0.03f;
                rb.angularDrag = 0.05f;
                rb.mass = EstimateMass(part);

                rb.AddExplosionForce(
                    debrisLaunchSpeed,
                    blastPosition,
                    Mathf.Max(0.1f, blastRadius),
                    upwardsModifier,
                    ForceMode.VelocityChange);

                if (randomTorque > 0f)
                {
                    rb.AddTorque(
                        Random.onUnitSphere * randomTorque,
                        ForceMode.VelocityChange);
                }

                launched++;
            }

            if (debrisLifetime > 0f)
                Destroy(part, debrisLifetime);
        }

        // Lepas dari controller supaya debris tetap ada walaupun root asli dihapus.
        if (destroyAfterEffects)
        {
            broken.transform.SetParent(null, true);

            float vfxEnd = fireVfxPrefab != null ? fireVfxDelay + fireVfxLifetime : 0f;
            float soundEnd = 0f;
            if (fireSound != null)
            {
                float soundLength = fireSound.length;
                if (loopFireSound && fireSoundDuration > 0f)
                    soundLength = Mathf.Max(soundLength, fireSoundDuration);
                soundEnd = fireSoundDelay + soundLength;
            }

            float cleanupDelay = Mathf.Max(vfxEnd, soundEnd) + 1f;
            Destroy(gameObject, cleanupDelay);
        }

        Debug.Log($"[Pumpjack] Diganti prefab rusak: {parts.Count} debris, {launched} dilempar dari {blastPosition}.", this);
    }

    private void DisableIntactVisuals()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }

        for (int i = 0; i < _allColliders.Count; i++)
        {
            if (_allColliders[i] != null)
                _allColliders[i].enabled = false;
        }

        if (_animators != null)
        {
            for (int i = 0; i < _animators.Length; i++)
            {
                if (_animators[i] != null)
                    _animators[i].enabled = false;
            }
        }
    }

    private static List<GameObject> CollectMeshParts(GameObject root, Transform staticRoot)
    {
        List<GameObject> result = new List<GameObject>();
        HashSet<GameObject> seen = new HashSet<GameObject>();

        MeshRenderer[] meshes = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshes.Length; i++)
        {
            MeshRenderer mr = meshes[i];
            if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;
            if (mr.gameObject == root) continue;
            if (staticRoot != null && IsUnder(mr.transform, staticRoot)) continue;

            if (seen.Add(mr.gameObject))
                result.Add(mr.gameObject);
        }

        SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++)
        {
            SkinnedMeshRenderer smr = skinned[i];
            if (smr == null || !smr.enabled || !smr.gameObject.activeInHierarchy) continue;
            if (smr.gameObject == root) continue;
            if (staticRoot != null && IsUnder(smr.transform, staticRoot)) continue;

            if (seen.Add(smr.gameObject))
                result.Add(smr.gameObject);
        }

        return result;
    }

    // ================================================================
    // MODE 2: PECAH MESH ASLI (FALLBACK)
    // ================================================================

    private void BreakIntoSelfDebris(Vector3 blastPosition, float blastRadius)
    {
        if (_animators != null)
        {
            for (int i = 0; i < _animators.Length; i++)
            {
                if (_animators[i] != null)
                    _animators[i].enabled = false;
            }
        }

        List<GameObject> parts = CollectParts();
        HashSet<GameObject> partSet = new HashSet<GameObject>(parts);

        // Pass 1: lepas semua part ke world space. Urutan parent -> child supaya
        // posisi/rotasi dunia tiap mesh tetap sama saat di-unparent.
        for (int i = 0; i < parts.Count; i++)
        {
            GameObject part = parts[i];
            if (part == null || part == gameObject) continue;
            part.transform.SetParent(null, true);
        }

        // Pass 2: beri collider + Rigidbody + lemparan.
        for (int i = 0; i < parts.Count; i++)
        {
            GameObject part = parts[i];
            if (part == null || part == gameObject) continue;

            if (enablePhysics)
            {
                EnsureCollider(part);

                Rigidbody rb = part.GetComponent<Rigidbody>();
                if (rb == null)
                    rb = part.AddComponent<Rigidbody>();

                rb.useGravity = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.drag = 0.03f;
                rb.angularDrag = 0.05f;
                rb.mass = EstimateMass(part);

                rb.AddExplosionForce(
                    debrisLaunchSpeed,
                    blastPosition,
                    Mathf.Max(0.1f, blastRadius),
                    upwardsModifier,
                    ForceMode.VelocityChange);

                if (randomTorque > 0f)
                {
                    rb.AddTorque(
                        Random.onUnitSphere * randomTorque,
                        ForceMode.VelocityChange);
                }
            }

            if (debrisLifetime > 0f)
                Destroy(part, debrisLifetime);
        }

        // Collider yang tidak ikut jadi debris (misal empty parent) dimatikan.
        if (disableLeftoverColliders)
        {
            for (int i = 0; i < _allColliders.Count; i++)
            {
                Collider col = _allColliders[i];
                if (col == null) continue;

                if (!partSet.Contains(col.gameObject) &&
                    !IsUnderAnyPart(col.transform, partSet))
                {
                    col.enabled = false;
                }
            }
        }

        Debug.Log($"[Pumpjack] Pecah! {parts.Count} debris dilempar dari {blastPosition}.", this);
    }

    private List<GameObject> CollectParts()
    {
        List<GameObject> result = new List<GameObject>();

        if (splitEveryMesh)
        {
            HashSet<GameObject> seen = new HashSet<GameObject>();

            MeshRenderer[] meshes = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshes.Length; i++)
            {
                MeshRenderer mr = meshes[i];
                if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                if (mr.transform == transform) continue;

                if (seen.Add(mr.gameObject))
                    result.Add(mr.gameObject);
            }

            SkinnedMeshRenderer[] skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++)
            {
                SkinnedMeshRenderer smr = skinned[i];
                if (smr == null || !smr.enabled || !smr.gameObject.activeInHierarchy) continue;
                if (smr.transform == transform) continue;

                if (seen.Add(smr.gameObject))
                    result.Add(smr.gameObject);
            }
        }
        else
        {
            // Pakai collider yang sudah ada sebagai chunk. Ambil yang paling atas
            // saja supaya child collider tidak dapat Rigidbody dobel.
            List<GameObject> candidates = new List<GameObject>();
            HashSet<GameObject> seen = new HashSet<GameObject>();

            for (int i = 0; i < _allColliders.Count; i++)
            {
                Collider col = _allColliders[i];
                if (col == null || !col.enabled) continue;
                if (col.gameObject == gameObject) continue;

                if (seen.Add(col.gameObject))
                    candidates.Add(col.gameObject);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                GameObject candidate = candidates[i];
                bool nested = false;

                for (int j = 0; j < candidates.Count; j++)
                {
                    if (i == j) continue;
                    if (IsAncestorOf(candidates[j].transform, candidate.transform))
                    {
                        nested = true;
                        break;
                    }
                }

                if (!nested)
                    result.Add(candidate);
            }

            // Kalau ternyata tidak ada collider sama sekali, fallback ke tiap mesh.
            if (result.Count == 0)
            {
                splitEveryMesh = true;
                return CollectParts();
            }
        }

        // Parent dulu, child belakangan. Dengan begitu saat child di-unparent,
        // posisi dunianya masih mengikuti parent yang sudah di-unparent.
        result.Sort((a, b) => Depth(a.transform).CompareTo(Depth(b.transform)));
        return result;
    }

    // ================================================================
    // HELPER FISIKA
    // ================================================================

    private void EnsureCollider(GameObject part)
    {
        // Pakai collider manual kalau ada, termasuk yang dipasang di child
        // (compound / tumpuk). Baru bikin BoxCollider kalau benar-benar kosong.
        Collider col = part.GetComponentInChildren<Collider>(true);
        if (col == null)
        {
            Renderer r = part.GetComponent<Renderer>();
            if (r == null)
                r = part.GetComponentInChildren<Renderer>(true);

            if (r == null) return;

            Bounds localBounds = r.localBounds;
            BoxCollider box = part.AddComponent<BoxCollider>();
            box.center = localBounds.center;
            box.size = localBounds.size;

            // Jaga-jaga kalau bounds kosong.
            if (box.size.sqrMagnitude < 0.000001f)
                box.size = Vector3.one * 0.2f;

            col = box;
        }

        // MeshCollider non-convex tidak boleh dipakai di Rigidbody dinamis.
        // Cek juga collider di child, karena bisa ikut Rigidbody part ini.
        Collider[] childColliders = part.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childColliders.Length; i++)
        {
            Collider child = childColliders[i];
            if (child == null) continue;

            MeshCollider meshCol = child as MeshCollider;
            if (meshCol != null)
                meshCol.convex = true;

            if (child.isTrigger)
                child.isTrigger = false;
        }
    }

    private float EstimateMass(GameObject part)
    {
        Renderer r = part.GetComponent<Renderer>();
        if (r == null)
            r = part.GetComponentInChildren<Renderer>(true);

        if (r == null)
            return minDebrisMass;

        Vector3 size = r.bounds.size;
        float volume = Mathf.Max(0.001f, size.x * size.y * size.z);
        float mass = volume * 80f; // kira-kira baja berongga
        return Mathf.Clamp(mass, minDebrisMass, Mathf.Max(minDebrisMass, maxDebrisMass));
    }

    // ================================================================
    // HELPER POSISI / HIERARCHY
    // ================================================================

    private float GetClosestDistance(Vector3 point)
    {
        float best = float.PositiveInfinity;

        for (int i = 0; i < _allColliders.Count; i++)
        {
            Collider col = _allColliders[i];
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

            Vector3 closest = col.ClosestPoint(point);
            float distance = Vector3.Distance(point, closest);
            if (distance < best)
                best = distance;
        }

        if (!float.IsPositiveInfinity(best))
            return best;

        // Fallback kalau pumpjack tidak punya collider.
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;

            Vector3 closest = r.bounds.ClosestPoint(point);
            float distance = Vector3.Distance(point, closest);
            if (distance < best)
                best = distance;
        }

        return float.IsPositiveInfinity(best) ? 0f : best;
    }

    private Vector3 GetFirePoint()
    {
        if (fireVfxPoint != null)
            return fireVfxPoint.position + fireVfxOffset;

        Bounds bounds = GetCombinedBounds();
        Vector3 point = new Vector3(bounds.center.x, bounds.min.y + 0.2f, bounds.center.z);
        return point + fireVfxOffset;
    }

    private Bounds GetCombinedBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds)
            bounds = new Bounds(transform.position, Vector3.one);

        return bounds;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name)) return null;

        string target = name.Trim();
        Transform[] all = parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (string.Equals(all[i].name.Trim(), target, System.StringComparison.OrdinalIgnoreCase))
                return all[i];
        }

        return null;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;

        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    private static int Depth(Transform t)
    {
        int depth = 0;
        while (t != null && t.parent != null)
        {
            depth++;
            t = t.parent;
        }
        return depth;
    }

    private static bool IsAncestorOf(Transform ancestor, Transform child)
    {
        if (ancestor == null || child == null) return false;

        Transform current = child.parent;
        while (current != null)
        {
            if (current == ancestor) return true;
            current = current.parent;
        }

        return false;
    }

    /// <summary>True kalau t adalah staticRoot atau berada di bawahnya.</summary>
    private static bool IsUnder(Transform t, Transform root)
    {
        if (t == null || root == null) return false;

        Transform current = t;
        while (current != null)
        {
            if (current == root) return true;
            current = current.parent;
        }

        return false;
    }

    private static bool IsUnderAnyPart(Transform t, HashSet<GameObject> parts)
    {
        Transform current = t;
        while (current != null)
        {
            if (parts.Contains(current.gameObject))
                return true;
            current = current.parent;
        }

        return false;
    }

    // ================================================================
    // EFEK API
    // ================================================================

    private IEnumerator SpawnFireVfxRoutine()
    {
        if (fireVfxDelay > 0f)
            yield return new WaitForSeconds(fireVfxDelay);

        if (fireVfxPrefab == null)
            yield break;

        GameObject vfx = Instantiate(fireVfxPrefab, _firePoint, Quaternion.identity);
        vfx.transform.SetParent(transform, true);

        ParticleSystem[] systems = vfx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null)
                systems[i].Play(true);
        }

        if (fireVfxLifetime > 0f)
            Destroy(vfx, fireVfxLifetime);
    }

    private IEnumerator PlayFireSoundRoutine()
    {
        if (fireSoundDelay > 0f)
            yield return new WaitForSeconds(fireSoundDelay);

        if (fireSound == null)
            yield break;

        if (!loopFireSound)
        {
            AudioPool.PlayClipAtPoint(
                fireSound,
                _firePoint,
                fireSoundVolume,
                1f,
                1f,
                fireSoundMaxDistance);
            yield break;
        }

        GameObject host = new GameObject("PumpjackFireAudio");
        host.transform.position = _firePoint;

        AudioSource source = host.AddComponent<AudioSource>();
        source.clip = fireSound;
        source.loop = true;
        source.volume = Mathf.Clamp01(fireSoundVolume);
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 3f;
        source.maxDistance = Mathf.Max(10f, fireSoundMaxDistance);
        source.dopplerLevel = 0f;
        source.playOnAwake = false;
        source.Play();

        float duration = fireSoundDuration > 0f ? fireSoundDuration : fireSound.length;
        Destroy(host, duration);
    }

    private void CacheColliders()
    {
        _allColliders.Clear();
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                _allColliders.Add(colliders[i]);
        }
    }

    // ================================================================
    // TEST / EDITOR
    // ================================================================

    [ContextMenu("Test Break (Play Mode)")]
    private void TestBreak()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Pumpjack] Masuk Play Mode dulu buat test pecah.", this);
            return;
        }

        Break(transform.position + Vector3.up, 8f);
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Assign Default Fire")]
    private void AutoAssignDefaultFire()
    {
        if (fireVfxPrefab == null)
        {
            fireVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/JMO Assets/WarFX/_Effects/Fire/Black Smoke/WFX_GazFireBig (Black Smoke).prefab");
        }

        if (fireSound == null)
        {
            fireSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Scenes/Pumpjack/api kebakar.mp3");
        }

        if (brokenPrefab == null)
        {
            brokenPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Scenes/Pumpjack/pumpjack broken.prefab");

            if (brokenPrefab == null)
            {
                brokenPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/pumpjack broken.fbx");
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.55f, 0f, 0.9f);
        Vector3 point = GetFirePoint();
        Gizmos.DrawWireSphere(point, 0.35f);
        Gizmos.DrawLine(transform.position, point);
    }

    private void OnValidate()
    {
        breakRadiusPadding = Mathf.Max(0f, breakRadiusPadding);
        debrisLaunchSpeed = Mathf.Max(0f, debrisLaunchSpeed);
        randomTorque = Mathf.Max(0f, randomTorque);
        debrisLifetime = Mathf.Max(0f, debrisLifetime);
        fireVfxDelay = Mathf.Max(0f, fireVfxDelay);
        fireVfxLifetime = Mathf.Max(0f, fireVfxLifetime);
        fireSoundDelay = Mathf.Max(0f, fireSoundDelay);
        fireSoundDuration = Mathf.Max(0f, fireSoundDuration);
        fireSoundMaxDistance = Mathf.Max(1f, fireSoundMaxDistance);
        maxDebrisMass = Mathf.Max(minDebrisMass, maxDebrisMass);
    }
}
