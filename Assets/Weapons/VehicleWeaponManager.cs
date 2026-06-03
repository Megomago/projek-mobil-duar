using UnityEngine;
using UnityEngine.UI; // Untuk mengecek LayoutGroup jika diperlukan di masa depan

namespace Weapons
{
    [System.Serializable]
    public class WeaponSlot
    {
        [Tooltip("Prefab senjata yang mau dipasang di slot ini. Kosongkan jika tidak ada senjata.")]
        public GameObject weaponPrefab;

        [Tooltip("Titik (Transform kosong) di mobil tempat senjata akan ditempel.")]
        public Transform pivot;

        [HideInInspector] public ModularWeapon spawnedWeapon;
    }

    public class VehicleWeaponManager : MonoBehaviour
    {
        [Header("=== WEAPON SLOTS (PENGATURAN SENJATA MOBIL) ===")]
        public WeaponSlot[] weaponSlots;

        [Header("=== UI SETTINGS ===")]
        [Tooltip("Patokan UI Utama di layar. (Tanpa Vertical Layout Group)")]
        public RectTransform uiContainer;

        [Tooltip("Jarak UI ke atas (pixel UI). Gunakan angka seperti 50 atau 100.")]
        public float uiVerticalSpacing = 100f;

        private void Start()
        {
            InitializeWeapons();
        }

        private void InitializeWeapons()
        {
            if (weaponSlots == null || weaponSlots.Length == 0) return;

            int activeWeaponCount = 0;

            foreach (var slot in weaponSlots)
            {
                if (slot.weaponPrefab == null) continue;

                if (slot.pivot == null)
                {
                    Debug.LogWarning($"[VehicleWeaponManager] Pivot untuk '{slot.weaponPrefab.name}' kosong!", this);
                    continue;
                }

                // 1. Spawn Senjata di 3D World
                GameObject spawnedObj = Instantiate(slot.weaponPrefab, slot.pivot.position, slot.pivot.rotation);
                spawnedObj.transform.SetParent(slot.pivot);
                spawnedObj.transform.localPosition = Vector3.zero;
                spawnedObj.transform.localRotation = Quaternion.identity;
                spawnedObj.name = slot.weaponPrefab.name;

                ModularWeapon modularWeapon = spawnedObj.GetComponent<ModularWeapon>();
                slot.spawnedWeapon = modularWeapon;

                if (modularWeapon != null)
                {
                    // 2. Setup UI
                    WeaponUIManager uiManager = spawnedObj.GetComponentInChildren<WeaponUIManager>();
                    
                    if (uiManager != null)
                    {
                        uiManager.targetWeapon = modularWeapon;
                        uiManager.SetWeaponName(spawnedObj.name);

                        // Pindahkan UI senjata ke patokan
                        if (uiContainer != null)
                        {
                            RectTransform uiRect = uiManager.GetComponent<RectTransform>();
                            if (uiRect != null)
                            {
                                uiRect.SetParent(uiContainer, false);
                                
                                // Paksa Anchor dan Pivot ke tengah agar posisi murni berfungsi sebagai offset koordinat
                                uiRect.anchorMin = new Vector2(0.5f, 0.5f);
                                uiRect.anchorMax = new Vector2(0.5f, 0.5f);
                                uiRect.pivot = new Vector2(0.5f, 0.5f);
                                
                                // Atur posisinya berjejer ke atas (Sumbu Y lokal dari UI)
                                uiRect.anchoredPosition = new Vector2(0, activeWeaponCount * uiVerticalSpacing);
                                
                                // Pastikan skala normal
                                uiRect.localScale = Vector3.one;
                            }
                        }
                    }

                    activeWeaponCount++;
                }
            }
        }
    }
}
