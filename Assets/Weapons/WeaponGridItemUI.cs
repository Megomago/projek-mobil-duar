using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Weapons
{
    public class WeaponGridItemUI : MonoBehaviour
    {
        public Image iconImage;
        public TextMeshProUGUI nameText;
        
        private WeaponData _weaponData;
        private LoadoutManager _manager;

        public void Initialize(WeaponData data, LoadoutManager manager)
        {
            _weaponData = data;
            _manager = manager;

            if (_weaponData != null)
            {
                if (nameText != null) nameText.text = _weaponData.weaponName;
                if (iconImage != null && _weaponData.weaponIcon != null)
                {
                    iconImage.sprite = _weaponData.weaponIcon;
                    iconImage.gameObject.SetActive(true);
                }
                else if (iconImage != null)
                {
                    iconImage.gameObject.SetActive(false); // Sembunyikan ikon jika kosong
                }
            }
            else
            {
                if (nameText != null) nameText.text = "Lepas";
                if (iconImage != null) iconImage.gameObject.SetActive(false);
            }
        }

        // Dipanggil saat tombol item ini diklik di UI
        public void OnClick()
        {
            if (_manager != null)
            {
                _manager.SelectWeaponFromGrid(_weaponData);
            }
        }
    }
}
