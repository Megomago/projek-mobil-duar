using UnityEngine;
using UnityEngine.UI; // Butuh ini untuk Slider dan Image
using TMPro;

namespace Weapons
{
    public class WeaponUIManager : MonoBehaviour
    {
        [Header("=== REFERENCES ===")]
        [Tooltip("Senjata yang sedang aktif/dipakai")]
        public ModularWeapon targetWeapon;

        [Header("=== UI ELEMENTS ===")]
        [Tooltip("Teks untuk menampilkan nama senjata")]
        public TextMeshProUGUI weaponNameText;
        [Tooltip("Teks untuk menampilkan jumlah amunisi")]
        public TextMeshProUGUI ammoText;
        [Tooltip("Teks untuk menampilkan status reload")]
        public TextMeshProUGUI reloadText;

        [Header("=== OVERHEAT UI ===")]
        [Tooltip("Slider visual untuk bar overheat")]
        public Slider overheatSlider;
        [Tooltip("Container utama bar overheat (akan otomatis disembunyikan jika senjata tidak menggunakan sistem overheat)")]
        public GameObject overheatContainer;
        [Tooltip("Bagian Fill dari Slider untuk efek transisi warna panas (opsional)")]
        public Image overheatFillImage;
        [Tooltip("Warna bar saat senjata dingin")]
        public Color coldColor = Color.cyan;
        [Tooltip("Warna bar saat senjata hampir overheat")]
        public Color hotColor = Color.red;

        private bool _isReloadingActive = false;

        public void Initialize(ModularWeapon weapon, string weaponName)
        {
            // Unsubscribe dari senjata lama jika ada (untuk reuse UI)
            if (targetWeapon != null)
            {
                targetWeapon.OnAmmoChanged -= HandleAmmoChanged;
                targetWeapon.OnReloadStart -= HandleReloadStart;
                targetWeapon.OnReloadFinished -= HandleReloadFinished;
            }

            targetWeapon = weapon;
            SetWeaponName(weaponName);

            if (targetWeapon != null)
            {
                // Subscribe ke event
                targetWeapon.OnAmmoChanged += HandleAmmoChanged;
                targetWeapon.OnReloadStart += HandleReloadStart;
                targetWeapon.OnReloadFinished += HandleReloadFinished;

                // Setup tampilan awal ammo & reload
                HandleAmmoChanged(targetWeapon.currentAmmo, targetWeapon.weaponData.maxAmmo);
                _isReloadingActive = targetWeapon.IsReloading();
                UpdateReloadUIVisibility();

                // Tampilkan/sembunyikan bar overheat sesuai spec senjata
                if (overheatContainer != null)
                {
                    overheatContainer.SetActive(targetWeapon.IsOverheatEnabled());
                }
            }
        }

        private void OnDestroy()
        {
            if (targetWeapon != null)
            {
                targetWeapon.OnAmmoChanged -= HandleAmmoChanged;
                targetWeapon.OnReloadStart -= HandleReloadStart;
                targetWeapon.OnReloadFinished -= HandleReloadFinished;
            }
        }

        private void Update()
        {
            if (targetWeapon == null) return;

            // Teks reload hanya di-update tiap frame SAAT sedang reload
            if (_isReloadingActive)
            {
                UpdateReloadTimer();
            }

            // Update nilai slider overheat tiap frame jika senjatanya memang bisa panas
            if (targetWeapon.IsOverheatEnabled())
            {
                UpdateOverheatUI();
            }
        }

        public void SetWeaponName(string name)
        {
            if (weaponNameText != null)
            {
                weaponNameText.text = name;
            }
        }

        private void HandleAmmoChanged(int currentAmmo, int maxAmmo)
        {
            if (ammoText == null) return;

            if (maxAmmo <= 0)
            {
                ammoText.text = "Ammo: &infin;";
                ammoText.color = Color.white;
            }
            else
            {
                ammoText.text = $"Ammo: {currentAmmo} / {maxAmmo}";
                ammoText.color = currentAmmo <= 0 ? Color.red : Color.white;
            }

            UpdateReloadUIVisibility();
        }

        private void HandleReloadStart()
        {
            _isReloadingActive = true;
            UpdateReloadUIVisibility();
        }

        private void HandleReloadFinished()
        {
            _isReloadingActive = false;
            UpdateReloadUIVisibility();
        }

        private void UpdateReloadUIVisibility()
        {
            if (reloadText == null || targetWeapon == null) return;

            if (_isReloadingActive)
            {
                reloadText.gameObject.SetActive(true);
                reloadText.color = Color.white;
                UpdateReloadTimer();
            }
            else if (targetWeapon.weaponData != null && targetWeapon.weaponData.maxAmmo > 0 && targetWeapon.currentAmmo <= 0)
            {
                reloadText.gameObject.SetActive(true);
                reloadText.text = "Press 'R' to Reload";
                reloadText.color = Color.yellow;
            }
            else
            {
                reloadText.gameObject.SetActive(false);
            }
        }

        private void UpdateReloadTimer()
        {
            if (reloadText == null) return;
            float remainingTime = targetWeapon.GetRemainingReloadTime();
            reloadText.text = $"{remainingTime:F1}s";
        }

        private void UpdateOverheatUI()
        {
            if (overheatSlider == null) return;

            float currentHeat = targetWeapon.GetCurrentHeat();
            float maxHeat = targetWeapon.GetMaxHeat();

            // Cegah error pembagian dengan nol kalau lu bego ngisi Max Heat = 0 di ScriptableObject
            float heatRatio = maxHeat > 0f ? (currentHeat / maxHeat) : 0f;

            overheatSlider.value = heatRatio;

            // Transisi warna dari dingin ke panas biar keliatan dinamis
            if (overheatFillImage != null)
            {
                overheatFillImage.color = Color.Lerp(coldColor, hotColor, heatRatio);
            }
        }
    }
}