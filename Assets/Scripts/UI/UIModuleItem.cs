using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UIModuleItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    
    private ModuleTemplate _moduleTemplate;
    private VehicleStatsManager _currentStatsManager;

    public void Initialize(ModuleTemplate template, VehicleStatsManager statsManager)
    {
        _moduleTemplate = template;
        _currentStatsManager = statsManager;

        if (template != null)
        {
            if (nameText != null) 
            {
                // Jika senjata, ambil nama dari weaponData jika ada
                if (template.moduleType == ModuleType.Weapon && template.weaponData != null)
                {
                    nameText.text = template.weaponData.weaponName;
                }
                else
                {
                    nameText.text = template.moduleName;
                }
            }

            if (iconImage != null)
            {
                // Jika senjata, ambil ikon dari weaponData jika ada
                if (template.moduleType == ModuleType.Weapon && template.weaponData != null && template.weaponData.weaponIcon != null)
                {
                    iconImage.sprite = template.weaponData.weaponIcon;
                }
                else
                {
                    iconImage.sprite = template.moduleIcon;
                }
                iconImage.enabled = (iconImage.sprite != null);
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (_moduleTemplate == null)
        {
            Debug.LogError("[UIModuleItem] ModuleTemplate kosong!");
            return;
        }

        if (InventoryDragDropManager.Instance == null)
        {
            Debug.LogError("[UIModuleItem] InventoryDragDropManager.Instance belum ada di Scene! Pastikan Anda sudah membuat GameObject dengan script InventoryDragDropManager.");
            return;
        }

        if (_currentStatsManager == null)
        {
            Debug.LogError("[UIModuleItem] VehicleStatsManager tidak ditemukan di mobil preview! Apakah mobil belum di-spawn?");
            return;
        }

        #if UNITY_EDITOR
        Debug.Log($"[UIModuleItem] Mulai Drag: {_moduleTemplate.moduleName}");
        #endif
        InventoryDragDropManager.Instance.StartDrag(_moduleTemplate, _currentStatsManager);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Fungsi ini wajib ada agar OnBeginDrag dan OnEndDrag bisa terpanggil, 
        // sekaligus memblokir event drag agar tidak tembus ke ScrollRect (elastisitas panel).
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Karena logika drop diurus oleh Update() di InventoryDragDropManager saat mendeteksi GetMouseButtonUp,
        // kita tidak perlu melakukan apa-apa di sini. Fungsi ini ada untuk melengkapi interface drag UI.
    }
}
