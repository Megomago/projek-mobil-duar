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
        [Tooltip("Teks untuk menampilkan jumlah amunisi (contoh: 30 / 30)")]
        public TextMeshProUGUI ammoText;
        [Tooltip("Teks untuk menampilkan status reload (contoh: Reloading... 1.5s)")]
        public TextMeshProUGUI reloadText;

        private void Update()
        {
            if (targetWeapon == null) return;

            UpdateAmmoUI();
            UpdateReloadUI();
        }

        private void UpdateAmmoUI()
        {
            if (ammoText == null) return;

            if (targetWeapon.weaponData == null || targetWeapon.weaponData.maxAmmo <= 0)
            {
                ammoText.text = "Ammo: &infin;"; // Simbol infinity
            }
            else
            {
                // Menampilkan format: Ammo: 30 / 30
                ammoText.text = $"Ammo: {targetWeapon.currentAmmo} / {targetWeapon.weaponData.maxAmmo}";
                
                // Ubah warna merah jika amunisi habis
                if (targetWeapon.currentAmmo <= 0)
                {
                    ammoText.color = Color.red;
                }
                else
                {
                    ammoText.color = Color.white;
                }
            }
        }

        private void UpdateReloadUI()
        {
            if (reloadText == null) return;

            if (targetWeapon.IsReloading())
            {
                reloadText.gameObject.SetActive(true);
                // Menampilkan sisa detik dengan 1 angka di belakang koma (contoh: 1.5s)
                float remainingTime = targetWeapon.GetRemainingReloadTime();
                reloadText.text = $"Reloading... {remainingTime:F1}s";
            }
            else if (targetWeapon.weaponData != null && targetWeapon.weaponData.maxAmmo > 0 && targetWeapon.currentAmmo <= 0)
            {
                reloadText.gameObject.SetActive(true);
                reloadText.text = "Press 'R' to Reload";
                reloadText.color = Color.yellow;
            }
            else
            {
                // Sembunyikan tulisan reload jika tidak sedang reload dan masih ada peluru
                reloadText.gameObject.SetActive(false);
                reloadText.color = Color.white;
            }
        }
    }
}
