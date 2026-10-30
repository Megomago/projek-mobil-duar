using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C4
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class C4Planter : MonoBehaviour
    {
        [Header("=== INPUT ===")]
        [Tooltip("Tombol buat equip/unequip C4.")]
        public KeyCode equipKey = KeyCode.Alpha4;

        [Tooltip("Tombol buat nempelin C4.")]
        public KeyCode placeKey = KeyCode.Mouse0;

        [Header("=== PREFAB ===")]
        [Tooltip("Prefab model C4 untuk preview.")]
        public GameObject c4Prefab;

        [Tooltip("Prefab C4 yang beneran ditempel (punya script C4Explosive & collider). Kosong = pake c4Prefab.")]
        public GameObject placedC4Prefab;

        [Header("=== PLACEMENT (RAYCAST) ===")]
        [Tooltip("Jarak jangkauan pasang C4 dari kamera.")]
        [Min(1f)]
        public float placementRange = 3.0f;

        [Tooltip("Jarak MINIMAL dari mata kamera. 0.5m biar ga nempel di jidat sendiri!")]
        [Min(0.3f)]
        public float minPlacementDistance = 0.5f;

        [Tooltip("Layer yang boleh ditempeli C4. Pastiin layer Player TIDAK dicentang!")]
        public LayerMask placementMask = ~0;

        [Tooltip("Cuma boleh di tanah/lantai datar.")]
        public bool groundOnly = false;

        [Range(0f, 1f)]
        [Tooltip("Toleransi kemiringan lantai kalau groundOnly aktif.")]
        public float minGroundNormalY = 0.65f;

        [Tooltip("Offset tipis dari dinding biar ga kedip-kedip (z-fighting).")]
        public float surfaceOffset = 0.01f;

        [Header("=== MODEL ROTATION OFFSET ===")]
        [Tooltip("Biarkan (0,0,0). Kalau teks angka keypad kebalik 180 derajat, isi Y atau Z jadi 180.")]
        public Vector3 modelRotationOffset = Vector3.zero;

        [Header("=== FPP VIEWMODEL (Saat Ga Ada Target) ===")]
        public Vector3 viewmodelPosition = new Vector3(0.14f, -0.12f, 0.4f);
        public Vector3 viewmodelEuler = new Vector3(-8f, 180f, 8f);
        public Vector3 viewmodelScale = Vector3.one;

        [Header("=== PREVIEW COLOR ===")]
        public Color validColor = new Color(0.1f, 1f, 0.25f, 1f);
        public Color invalidColor = new Color(1f, 0.1f, 0.1f, 1f);
        [Range(0f, 1f)] public float tintStrength = 0.45f;
        public float emissionIntensity = 2.5f;

        [Header("=== CAMERA ===")]
        [Tooltip("Jangan diaktifin kecuali kepepet, bikin z-fighting di background.")]
        public bool lowerNearClipWhileEquipped = false;
        public float nearClipWhileEquipped = 0.05f;

        public bool IsEquipped { get; private set; }
        public bool CanPlace { get; private set; }
        public event Action<C4Explosive> OnC4Placed;

        private Camera _camera;
        private GameObject _viewInstance;
        private C4PanelController _viewPanel;
        private Renderer[] _viewRenderers;
        private Material[] _viewMaterials;
        private Color[] _viewBaseColors;

        private string _pendingCode;

        private bool _hasAppliedColor;
        private bool _lastValidState;

        private bool _hasTarget;
        private RaycastHit _targetHit;

        private float _originalNearClip;
        private bool _nearClipChanged;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

        private bool _equipInput;
        private bool _placeInput;
        private bool _blockPlaceUntilRelease;

        private void Awake()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogError("[C4Planter] Camera.main ga ketemu le! Pastiin kamera player ada tag 'MainCamera'.", this);
                enabled = false;
                return;
            }

            CreatePreview();
            if (_viewInstance != null)
                _viewInstance.SetActive(false);
        }

        private void Update()
        {
            // Input ditangkap murni di Update
            if (Input.GetKeyDown(equipKey)) _equipInput = true;

            // Abis pickup, klik yang masih ditahan buat angkat C4 gak boleh
            // langsung nempel C4 baru. Tunggu tombolnya dilepas dulu.
            if (_blockPlaceUntilRelease)
            {
                if (!Input.GetKey(placeKey))
                    _blockPlaceUntilRelease = false;
            }
            else if (Input.GetKeyDown(placeKey))
            {
                _placeInput = true;
            }
        }

        private void LateUpdate()
        {
            // Eksekusi equip toggle
            if (_equipInput)
            {
                _equipInput = false;
                if (IsEquipped) Unequip();
                else Equip();
            }

            if (!IsEquipped)
            {
                _placeInput = false;
                return;
            }

            UpdatePreview();

            // Eksekusi penempelan C4
            if (_placeInput)
            {
                _placeInput = false;
                if (CanPlace)
                {
                    Place();
                }
            }
        }

        private void OnEnable()
        {
            _camera = Camera.main;
            IsEquipped = false;
            CanPlace = false;
            _equipInput = false;
            _placeInput = false;
            _blockPlaceUntilRelease = false;

            if (_viewInstance != null)
                _viewInstance.SetActive(false);
        }

        private void OnDisable()
        {
            IsEquipped = false;
            CanPlace = false;
            _equipInput = false;
            _placeInput = false;
            _blockPlaceUntilRelease = false;

            if (_viewInstance != null)
                _viewInstance.SetActive(false);

            RestoreNearClip();
        }

        private void OnDestroy()
        {
            RestoreNearClip();

            if (_viewInstance != null)
            {
                Destroy(_viewInstance);
                _viewInstance = null;
            }

            if (_viewMaterials != null)
            {
                for (int i = 0; i < _viewMaterials.Length; i++)
                {
                    if (_viewMaterials[i] != null)
                        Destroy(_viewMaterials[i]);
                }
                _viewMaterials = null;
            }
        }

        public void Equip()
        {
            if (IsEquipped) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            if (_viewInstance == null) CreatePreview();
            if (_viewInstance == null) return;

            // Pastikan ada PIN untuk C4 yang sedang dipegang di preview
            if (string.IsNullOrEmpty(_pendingCode))
            {
                GenerateNewPendingCode();
            }
            else if (_viewPanel != null)
            {
                _viewPanel.SetCode(_pendingCode);
            }

            IsEquipped = true;
            _hasAppliedColor = false;
            _viewInstance.SetActive(true);

            if (lowerNearClipWhileEquipped)
            {
                _originalNearClip = _camera.nearClipPlane;
                if (_originalNearClip > nearClipWhileEquipped)
                {
                    _camera.nearClipPlane = nearClipWhileEquipped;
                    _nearClipChanged = true;
                }
            }

            UpdatePreview();
        }

        /// <summary>
        /// Equip lagi setelah C4 yang sudah ditempel diambil (pickup).
        /// Menyimpan kode PIN dari C4 yang diambil agar tidak berubah.
        /// Klik yang dipakai buat pickup ditahan supaya gak langsung nempel C4 baru.
        /// </summary>
        public void EquipAfterPickup(string preservedCode = null)
        {
            _placeInput = false;
            _blockPlaceUntilRelease = true;

            if (!string.IsNullOrEmpty(preservedCode))
            {
                _pendingCode = preservedCode;
            }

            Equip();

            if (!string.IsNullOrEmpty(preservedCode) && _viewPanel != null)
            {
                _viewPanel.SetCode(preservedCode);
            }
        }

        public void Unequip()
        {
            if (!IsEquipped) return;

            IsEquipped = false;
            CanPlace = false;

            if (_viewInstance != null)
                _viewInstance.SetActive(false);

            RestoreNearClip();
        }

        private void RestoreNearClip()
        {
            if (_nearClipChanged && _camera != null)
                _camera.nearClipPlane = _originalNearClip;

            _nearClipChanged = false;
        }

        private void UpdatePreview()
        {
            if (_camera == null || _viewInstance == null) return;

            Transform cam = _camera.transform;
            Ray ray = new Ray(cam.position, cam.forward);

            bool found = TryGetPlacementHit(ray, out RaycastHit hit);
            bool valid = false;

            if (found)
            {
                float distance = Vector3.Distance(cam.position, hit.point);
                bool distanceValid = distance >= minPlacementDistance && distance <= placementRange;
                bool surfaceAllowed = !groundOnly || hit.normal.y >= minGroundNormalY;

                valid = distanceValid && surfaceAllowed;

                // Hitung rotasi yang bener secara matematis
                Quaternion surfaceRot = GetSurfaceRotation(hit.normal, cam);
                if (modelRotationOffset != Vector3.zero)
                    surfaceRot *= Quaternion.Euler(modelRotationOffset);

                Vector3 surfacePos = hit.point + hit.normal * surfaceOffset;

                _viewInstance.transform.SetPositionAndRotation(surfacePos, surfaceRot);
                _targetHit = hit;
                _hasTarget = true;
            }
            else
            {
                // Mode standby di tangan pas ga narget apa-apa
                _viewInstance.transform.SetPositionAndRotation(
                    cam.position + cam.rotation * viewmodelPosition,
                    cam.rotation * Quaternion.Euler(viewmodelEuler));

                _hasTarget = false;
            }

            _viewInstance.transform.localScale = viewmodelScale;
            CanPlace = valid;

            if (!_hasAppliedColor || _lastValidState != valid)
            {
                ApplyPreviewColor(valid ? validColor : invalidColor);
                _hasAppliedColor = true;
                _lastValidState = valid;
            }
        }

        private bool TryGetPlacementHit(Ray ray, out RaycastHit bestHit)
        {
            bestHit = default;

            int count = Physics.RaycastNonAlloc(
                ray,
                _hitBuffer,
                placementRange,
                placementMask,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _hitBuffer[i];
                if (h.collider == null) continue;

                // 1. FILTER ANTI-MUKA SENDIRI: Abaikan player
                if (h.collider.transform.root == transform.root) continue;
                if (h.collider.CompareTag("Player") || h.collider.transform.root.CompareTag("Player")) continue;

                // 2. Abaikan layer Ignore Raycast
                if (h.collider.gameObject.layer == LayerMask.NameToLayer("Ignore Raycast")) continue;

                // 3. Abaikan preview C4
                if (_viewInstance != null && h.collider.transform.IsChildOf(_viewInstance.transform)) continue;

                // 4. Abaikan C4 yang udah nempel sebelumnya
                if (h.collider.GetComponentInParent<C4Explosive>() != null) continue;

                // 5. Filter jarak minimal biar ga nempel di jidat
                if (h.distance < minPlacementDistance) continue;

                if (h.distance < bestDistance)
                {
                    bestDistance = h.distance;
                    bestHit = h;
                }
            }

            return bestDistance < float.PositiveInfinity;
        }

        private static Quaternion GetSurfaceRotation(Vector3 surfaceNormal, Transform cam)
        {
            Vector3 forwardDir;

            // Kalau permukaan horizontal (Lantai atau Plafon)
            if (Mathf.Abs(surfaceNormal.y) > 0.7f)
            {
                // Panjang C4 diarahkan searah hadap kamera
                forwardDir = Vector3.ProjectOnPlane(cam.forward, surfaceNormal).normalized;
                if (forwardDir.sqrMagnitude < 0.0001f)
                    forwardDir = Vector3.ProjectOnPlane(cam.up, surfaceNormal).normalized;
            }
            else
            {
                // Kalau di dinding atau bidang miring: Panjang C4 ngunci lurus ke atas dunia
                forwardDir = Vector3.ProjectOnPlane(Vector3.up, surfaceNormal).normalized;
            }

            if (forwardDir.sqrMagnitude < 0.0001f)
                forwardDir = Vector3.Cross(surfaceNormal, Vector3.right).normalized;

            // Forward (+Z) = Arah panjang bodi C4
            // Upwards (+Y) = Normal permukaan (Alas C4 selalu nempel pipih di bidang!)
            return Quaternion.LookRotation(forwardDir, surfaceNormal);
        }

        private void Place()
        {
            if (!CanPlace || !_hasTarget || _viewInstance == null) return;

            GameObject prefab = placedC4Prefab != null ? placedC4Prefab : c4Prefab;
            if (prefab == null) return;

            GameObject placed = Instantiate(
                prefab,
                _viewInstance.transform.position,
                _viewInstance.transform.rotation);

            placed.name = "C4_Placed";

            // Parenting cerdas: Rigidbody kalau ada (mobil), Collider kalau ga ada (pintu/platform gerak)
            Transform targetParent = _targetHit.rigidbody != null ? _targetHit.rigidbody.transform : _targetHit.collider.transform;
            placed.transform.SetParent(targetParent, true);

            C4Explosive explosive = placed.GetComponent<C4Explosive>();
            if (explosive == null)
                explosive = placed.AddComponent<C4Explosive>();

            explosive.Initialize(transform);

            // Pastikan C4Outline terpasang pada C4 yang baru ditempel
            C4Outline outline = placed.GetComponent<C4Outline>();
            if (outline == null)
                outline = placed.AddComponent<C4Outline>();

            // SAMAKAN PIN: PIN yang ada di preview harus sama persis dengan yang ditempel!
            C4PanelController panel = placed.GetComponent<C4PanelController>();
            if (panel != null && !string.IsNullOrEmpty(_pendingCode))
            {
                panel.SetCode(_pendingCode);
            }

            // Reset pending code agar jika ambil C4 baru nanti akan digenerate PIN baru yang fresh
            _pendingCode = null;

            OnC4Placed?.Invoke(explosive);
            Unequip();
        }

        private void CreatePreview()
        {
            GameObject prefab = c4Prefab;

            if (prefab == null)
            {
                _viewInstance = CreateFallbackPreview();
            }
            else
            {
                _viewInstance = Instantiate(prefab);
                _viewInstance.name = "C4_Viewmodel";

                // Hapus semua collider di preview biar ga ganggu raycast
                Collider[] colliders = _viewInstance.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                        Destroy(colliders[i]);
                }

                // Matikan C4Outline di viewmodel agar tidak ikut rendering outline
                C4Outline previewOutline = _viewInstance.GetComponent<C4Outline>();
                if (previewOutline != null)
                    Destroy(previewOutline);

                // Matikan C4Explosive di viewmodel agar tidak meledak di tangan
                C4Explosive previewExplosive = _viewInstance.GetComponent<C4Explosive>();
                if (previewExplosive != null)
                    Destroy(previewExplosive);

                // Disable C4PanelController di viewmodel agar tidak jalanin logic interaksi di tangan
                _viewPanel = _viewInstance.GetComponent<C4PanelController>();
                if (_viewPanel != null)
                {
                    _viewPanel.enabled = false;
                }
            }

            if (_viewInstance == null) return;

            _viewInstance.transform.localScale = viewmodelScale;

            if (string.IsNullOrEmpty(_pendingCode))
            {
                GenerateNewPendingCode();
            }
            else if (_viewPanel != null)
            {
                _viewPanel.SetCode(_pendingCode);
            }

            _viewRenderers = _viewInstance.GetComponentsInChildren<Renderer>(true);
            List<Material> materials = new List<Material>();
            List<Color> baseColors = new List<Color>();

            for (int i = 0; i < _viewRenderers.Length; i++)
            {
                Renderer r = _viewRenderers[i];
                if (r == null) continue;

                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;

                Material[] mats = r.materials;
                for (int j = 0; j < mats.Length; j++)
                {
                    Material m = mats[j];
                    if (m == null) continue;

                    Color baseColor = Color.white;
                    if (m.HasProperty("_BaseColor"))
                        baseColor = m.GetColor("_BaseColor");
                    else if (m.HasProperty("_Color"))
                        baseColor = m.GetColor("_Color");

                    materials.Add(m);
                    baseColors.Add(baseColor);
                }
            }

            _viewMaterials = materials.ToArray();
            _viewBaseColors = baseColors.ToArray();
        }

        private void GenerateNewPendingCode()
        {
            int randomNum = UnityEngine.Random.Range(10000, 100000);
            _pendingCode = randomNum.ToString();
            if (_viewPanel != null)
            {
                _viewPanel.SetCode(_pendingCode);
            }
        }

        private GameObject CreateFallbackPreview()
        {
            GameObject root = new GameObject("C4_Viewmodel_Fallback");

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Body";
            cube.transform.SetParent(root.transform, false);
            cube.transform.localScale = new Vector3(0.16f, 0.2f, 0.08f);

            Collider cubeCollider = cube.GetComponent<Collider>();
            if (cubeCollider != null)
                Destroy(cubeCollider);

            Renderer cubeRenderer = cube.GetComponent<Renderer>();
            if (cubeRenderer != null)
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                if (urpLit != null)
                    cubeRenderer.material = new Material(urpLit);
            }

            return root;
        }

        private void ApplyPreviewColor(Color target)
        {
            if (_viewMaterials == null || _viewBaseColors == null) return;

            for (int i = 0; i < _viewMaterials.Length; i++)
            {
                Material m = _viewMaterials[i];
                if (m == null) continue;

                Color tinted = Color.Lerp(_viewBaseColors[i], target, tintStrength);

                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", tinted);

                if (m.HasProperty("_Color"))
                    m.SetColor("_Color", tinted);

                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", target * emissionIntensity);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }
        }
    }
}
