using UnityEngine;
using System.Collections.Generic;

namespace Weapons
{
    public class VehicleWeaponManager : MonoBehaviour
    {
        [Header("=== DATABASE (LOADOUT SYSTEM) ===")]
        public WeaponDatabase weaponDatabase;
        public VehicleData vehicleData;

        [Header("=== TETRIS GRID 3D SETTINGS ===")]
        [Tooltip("Titik awal (Kiri-Atas) dari area grid di mobil.")]
        public Transform gridOriginPivot;

        [Tooltip("Lebar grid (X). Di-override oleh VehicleData jika ada.")]
        [Min(1)] public int gridSizeX = 6;

        [Tooltip("Tinggi grid (Y). Di-override oleh VehicleData jika ada.")]
        [Min(1)] public int gridSizeY = 4;

        [Tooltip("Ukuran 1 kotak grid dalam meter. Di-override oleh VehicleData jika ada.")]
        [Min(0.01f)] public float gridCellSize = 0.25f;

        [Header("=== UI SETTINGS ===")]
        public RectTransform hudContainer;

        [Header("=== INPUT SETTINGS ===")]
        public bool usePlayerInput = true;

        [Header("=== DRAG PREVIEW (KSP STYLE) ===")]
        public Color previewValidColor = new Color(0.35f, 1f, 0.45f, 0.65f);
        public Color previewInvalidColor = new Color(1f, 0.3f, 0.25f, 0.65f);
        [Tooltip("Angkat preview sedikit di atas grid agar kelihatan")]
        public float previewLiftHeight = 0.04f;

        [HideInInspector] public string currentVehicleName = "";
        
        private struct SpawnedWeaponInfo
        {
            public GameObject obj;
            public int gridX;
            public int gridY;
            public string weaponName;
        }

        private List<ModularWeapon> _spawnedWeapons = new List<ModularWeapon>();
        private List<GameObject> _spawnedHUDs = new List<GameObject>();
        private List<SpawnedWeaponInfo> _spawnedWeaponInfos = new List<SpawnedWeaponInfo>();
        private Transform _weaponsContainer;
        private Transform _dragPreviewContainer;
        private GameObject _dragPreviewObject;
        private GameObject _hiddenPlacedWeapon;
        private WeaponData _previewWeaponData;
        private MaterialPropertyBlock _previewTintBlock;

        private void Awake()
        {
            SyncGridSettings();
            EnsureWeaponsContainer();
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(currentVehicleName))
            {
                currentVehicleName = vehicleData != null
                    ? vehicleData.vehicleName
                    : gameObject.name.Replace("(Clone)", "").Trim();
            }

            RefreshWeapons();
            RefreshGridVisual();
        }

        public void SyncGridSettings()
        {
            if (vehicleData == null) return;

            gridSizeX = vehicleData.gridSizeX;
            gridSizeY = vehicleData.gridSizeY;
            gridCellSize = vehicleData.gridCellSize;

            if (string.IsNullOrEmpty(currentVehicleName))
            {
                currentVehicleName = vehicleData.vehicleName;
            }
        }

        public void RefreshGridVisual()
        {
            VehicleGrid3D gridVisual = GetComponentInChildren<VehicleGrid3D>(true);
            if (gridVisual == null) return;

            gridVisual.gridOrigin = gridOriginPivot;
            gridVisual.vehicleData = vehicleData;
            gridVisual.SyncFromVehicleData();
            gridVisual.RebuildVisual();
        }

        private void EnsureWeaponsContainer()
        {
            if (gridOriginPivot == null) return;

            Transform existing = gridOriginPivot.Find("Weapons");
            if (existing != null)
            {
                _weaponsContainer = existing;
                return;
            }

            GameObject container = new GameObject("Weapons");
            container.transform.SetParent(gridOriginPivot, false);
            _weaponsContainer = container.transform;
        }

        public void RefreshWeapons()
        {
            EnsureWeaponsContainer();
            SyncGridSettings();

            if (_weaponsContainer != null)
            {
                for (int i = _weaponsContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(_weaponsContainer.GetChild(i).gameObject);
                }
            }

            foreach (GameObject hud in _spawnedHUDs)
            {
                if (hud != null) Destroy(hud);
            }
            
            _spawnedWeapons.Clear();
            _spawnedHUDs.Clear();
            _spawnedWeaponInfos.Clear();

            LoadAndSpawnGridWeapons();
        }

        public void BeginDragPreview(WeaponData data, int gridX, int gridY, bool isRotated, bool hideExistingAtOrigin)
        {
            EndDragPreview();

            SyncGridSettings();
            EnsureWeaponsContainer();

            if (data == null || data.weapon3DPrefab == null || gridOriginPivot == null) return;

            _previewWeaponData = data;
            EnsureDragPreviewContainer();

            _dragPreviewObject = Instantiate(data.weapon3DPrefab, _dragPreviewContainer);
            _dragPreviewObject.name = $"DragPreview_{data.weaponName}";

            DisableGameplayComponents(_dragPreviewObject);
            ApplyPreviewTransform(_dragPreviewObject, gridX, gridY, isRotated, data);
            ApplyPreviewTint(_dragPreviewObject, previewValidColor);

            if (hideExistingAtOrigin)
            {
                HidePlacedWeaponAt(gridX, gridY, data.weaponName);
            }
        }

        public void UpdateDragPreview(int gridX, int gridY, bool isRotated, bool isValid, bool visible)
        {
            if (_dragPreviewObject == null || _previewWeaponData == null) return;

            _dragPreviewObject.SetActive(visible);
            if (!visible) return;

            ApplyPreviewTransform(_dragPreviewObject, gridX, gridY, isRotated, _previewWeaponData);
            ApplyPreviewTint(_dragPreviewObject, isValid ? previewValidColor : previewInvalidColor);
        }

        public void EndDragPreview()
        {
            _hiddenPlacedWeapon = null;

            if (_dragPreviewObject != null)
            {
                Destroy(_dragPreviewObject);
                _dragPreviewObject = null;
            }

            _previewWeaponData = null;
        }

        private void EnsureDragPreviewContainer()
        {
            if (gridOriginPivot == null) return;

            Transform existing = gridOriginPivot.Find("DragPreview");
            if (existing != null)
            {
                _dragPreviewContainer = existing;
                return;
            }

            GameObject container = new GameObject("DragPreview");
            container.transform.SetParent(gridOriginPivot, false);
            _dragPreviewContainer = container.transform;
        }

        private void HidePlacedWeaponAt(int gridX, int gridY, string weaponName)
        {
            foreach (SpawnedWeaponInfo info in _spawnedWeaponInfos)
            {
                if (info.obj == null) continue;
                if (info.gridX == gridX && info.gridY == gridY && info.weaponName == weaponName)
                {
                    _hiddenPlacedWeapon = info.obj;
                    _hiddenPlacedWeapon.SetActive(false);
                    return;
                }
            }
        }

        private void ApplyPreviewTransform(GameObject obj, int gridX, int gridY, bool isRotated, WeaponData data)
        {
            int sizeX = isRotated ? data.gridHeight : data.gridWidth;
            int sizeY = isRotated ? data.gridWidth : data.gridHeight;
            Vector3 pos = VehicleGridUtility.GridToLocalCenter(
                gridX, gridY, sizeX, sizeY, gridCellSize);
            pos.y = previewLiftHeight;
            obj.transform.localPosition = pos;
            obj.transform.localRotation = isRotated
                ? Quaternion.Euler(0f, -90f, 0f)
                : Quaternion.identity;
        }

        private void ApplyPreviewTint(GameObject obj, Color color)
        {
            if (_previewTintBlock == null) _previewTintBlock = new MaterialPropertyBlock();

            foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Material[] mats = renderer.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material mat = mats[i];
                    SetupTransparentMaterial(mat, color);
                }

                _previewTintBlock.Clear();
                _previewTintBlock.SetColor("_Color", color);
                _previewTintBlock.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(_previewTintBlock);
            }
        }

        private static void SetupTransparentMaterial(Material mat, Color color)
        {
            if (mat == null) return;

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.color = color;
        }

        private static void DisableGameplayComponents(GameObject obj)
        {
            foreach (Collider col in obj.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            foreach (ModularWeapon weapon in obj.GetComponentsInChildren<ModularWeapon>())
            {
                weapon.enabled = false;
            }

            foreach (AudioSource audio in obj.GetComponentsInChildren<AudioSource>())
            {
                audio.enabled = false;
            }
        }

        private void LoadAndSpawnGridWeapons()
        {
            if (weaponDatabase == null || gridOriginPivot == null || _weaponsContainer == null) return; 

            string key = "TetrisGrid_" + currentVehicleName;
            if (!PlayerPrefs.HasKey(key)) return;

            string json = PlayerPrefs.GetString(key);
            SavedGridData loadedData = JsonUtility.FromJson<SavedGridData>(json);

            if (loadedData == null || loadedData.items == null) return;

            foreach (var savedItem in loadedData.items)
            {
                WeaponData wData = weaponDatabase.GetWeaponByName(savedItem.weaponName);
                if (wData == null) continue;

                SpawnWeapon3D(wData, savedItem.x, savedItem.y, savedItem.isRotated);
            }
        }

        private void SpawnWeapon3D(WeaponData weaponData, int gridX, int gridY, bool isRotated)
        {
            if (weaponData.weapon3DPrefab == null) return;

            GameObject spawnedObj = Instantiate(weaponData.weapon3DPrefab, _weaponsContainer);
            
            int sizeX = isRotated ? weaponData.gridHeight : weaponData.gridWidth;
            int sizeY = isRotated ? weaponData.gridWidth : weaponData.gridHeight;
            spawnedObj.transform.localPosition = VehicleGridUtility.GridToLocalCenter(
                gridX, gridY, sizeX, sizeY, gridCellSize);

            if (isRotated)
            {
                spawnedObj.transform.localRotation = Quaternion.Euler(0, -90, 0);
            }
            else
            {
                spawnedObj.transform.localRotation = Quaternion.identity;
            }
            
            spawnedObj.name = weaponData.weaponName;

            _spawnedWeaponInfos.Add(new SpawnedWeaponInfo
            {
                obj = spawnedObj,
                gridX = gridX,
                gridY = gridY,
                weaponName = weaponData.weaponName
            });

            ModularWeapon modularWeapon = spawnedObj.GetComponent<ModularWeapon>();
            if (modularWeapon != null)
            {
                modularWeapon.weaponData = weaponData;
                modularWeapon.currentAmmo = weaponData.maxAmmo;
                _spawnedWeapons.Add(modularWeapon);
                
                SpawnHUD(weaponData, modularWeapon);
            }
        }

        private void SpawnHUD(WeaponData weaponData, ModularWeapon modularWeapon)
        {
            if (weaponData.hudPrefab == null || hudContainer == null) return;

            GameObject hudObj = Instantiate(weaponData.hudPrefab, hudContainer);
            _spawnedHUDs.Add(hudObj);
            hudObj.name = $"HUD_{weaponData.weaponName}";

            RectTransform hudRect = hudObj.GetComponent<RectTransform>();
            if (hudRect != null)
            {
                hudRect.localScale = Vector3.one;
                hudRect.localRotation = Quaternion.identity;
            }

            WeaponUIManager uiManager = hudObj.GetComponent<WeaponUIManager>();
            if (uiManager == null) uiManager = hudObj.GetComponentInChildren<WeaponUIManager>();

            if (uiManager != null)
            {
                uiManager.Initialize(modularWeapon, weaponData.weaponName);
            }
        }

        private void Update()
        {
            if (!usePlayerInput || _spawnedWeapons == null || _spawnedWeapons.Count == 0) return;

            bool isFiring = Input.GetMouseButton(0);
            bool isReloading = Input.GetKeyDown(KeyCode.R);

            foreach (var weapon in _spawnedWeapons)
            {
                if (weapon != null)
                {
                    if (isFiring) weapon.TryFire();
                    else weapon.StopFiring();
                    
                    if (isReloading) weapon.StartReload();
                }
            }
        }
    }
}
