using UnityEngine;
using TMPro; // Membutuhkan TextMeshPro

namespace Weapons
{
    public class WeaponUIManager : MonoBehaviour
    {
        [Header("=== REFERENCES ===")]
        [Tooltip("Senjata yang sedang aktif/dipakai")]
        public ModularWeapon targetWeapon;

        [Header("=== UI ELEMENTS ===")]
        [Tooltip("Teks untuk menampilkan nama senjata (diambil otomatis dari nama prefab)")]
        public TextMeshProUGUI weaponNameText;
        [Tooltip("Teks untuk menampilkan jumlah amunisi (contoh: 30 / 30)")]
        public TextMeshProUGUI ammoText;
        [Tooltip("Teks untuk menampilkan status reload (contoh: Reloading... 1.5s)")]
        public TextMeshProUGUI reloadText;

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

                // Setup tampilan awal
                HandleAmmoChanged(targetWeapon.currentAmmo, targetWeapon.weaponData.maxAmmo);
                _isReloadingActive = targetWeapon.IsReloading();
                UpdateReloadUIVisibility();
            }
        }

        private void OnDestroy()
        {
            // Pastikan tidak ada memory leak
            if (targetWeapon != null)
            {
                targetWeapon.OnAmmoChanged -= HandleAmmoChanged;
                targetWeapon.OnReloadStart -= HandleReloadStart;
                targetWeapon.OnReloadFinished -= HandleReloadFinished;
            }
        }

        private void Update()
        {
            // Teks reload hanya di-update tiap frame SAAT sedang reload
            if (_isReloadingActive && targetWeapon != null)
            {
                UpdateReloadTimer();
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
                ammoText.text = "Ammo: &infin;"; // Simbol infinity
                ammoText.color = Color.white;
            }
            else
            {
                ammoText.text = $"Ammo: {currentAmmo} / {maxAmmo}";
                ammoText.color = currentAmmo <= 0 ? Color.red : Color.white;
            }

            // Saat tembak / peluru berubah, pastikan pesan "Press R" update
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
                UpdateReloadTimer(); // Segera tampilkan angkanya
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
            reloadText.text = $"Reloading... {remainingTime:F1}s";
        }
    }
}
