using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Weapons
{
    public class WeaponSlotUI : MonoBehaviour
    {
        public int slotIndex;
        public Image iconImage;
        public TextMeshProUGUI nameText;

        private LoadoutManager _manager;

        public void Setup(int index, LoadoutManager manager)
        {
            slotIndex = index;
            _manager = manager;
        }

        public void UpdateVisual(WeaponData data)
        {
            if (data != null)
            {
                if (nameText != null) nameText.text = data.weaponName;
                if (iconImage != null && data.weaponIcon != null)
                {
                    iconImage.sprite = data.weaponIcon;
                    iconImage.gameObject.SetActive(true);
                }
                else if (iconImage != null)
                {
                    iconImage.gameObject.SetActive(false);
                }
            }
            else
            {
                if (nameText != null) nameText.text = "Slot Kosong";
                if (iconImage != null) iconImage.gameObject.SetActive(false);
            }
        }

        // Dipanggil saat tombol Slot ini diklik di layar Inventory
        public void OnClick()
        {
            if (_manager != null)
            {
                _manager.OpenWeaponGrid(slotIndex);
            }
        }
    }
}
