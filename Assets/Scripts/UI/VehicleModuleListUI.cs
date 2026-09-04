using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class VehicleModuleListUI : MonoBehaviour
{
    [Header("References")]
    public VehicleStatsManager statsManager;
    public ScrollRect scrollRect;
    public Transform contentParent;
    public GameObject moduleItemPrefab;

    [Header("Colors")]
    public Color baseModuleColor = Color.white;
    public Color installedModuleColor = Color.cyan;

    private List<GameObject> _items = new List<GameObject>();

    private void Start()
    {
        if (statsManager == null)
            statsManager = FindObjectOfType<VehicleStatsManager>();
        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Initialize(VehicleStatsManager stats)
    {
        statsManager = stats;
        Refresh();
    }

    public void Refresh()
    {
        ClearItems();
        if (statsManager == null) return;

        // ── BASE MODULES (VehicleCriticalPart) ──
        VehicleCriticalPart[] criticalParts = statsManager.GetComponentsInChildren<VehicleCriticalPart>(false);
        foreach (var part in criticalParts)
        {
            if (!part.gameObject.activeInHierarchy) continue;
            if (part.hideFromModuleList) continue;
            string label = string.IsNullOrEmpty(part.partName) ? part.partType.ToString() : part.partName;
            if (part.ammoPoint > 0f)
                label += $" ({part.currentAmmoPoint:0}/{part.ammoPoint:0})";
            AddItem(label, baseModuleColor);
        }

        // ── INSTALLED MODULES ──
        foreach (var module in statsManager.installedModules)
        {
            if (module?.moduleTemplate == null) continue;
            if (module.moduleTemplate.hideFromModuleList) continue;
            string label = module.moduleTemplate.moduleName;
            if (module.moduleTemplate.ammoPoint > 0f)
                label += $" ({module.currentAmmoPoint:0}/{module.moduleTemplate.ammoPoint:0})";
            AddItem(label, installedModuleColor);
        }
    }

    private void AddItem(string text, Color color)
    {
        if (moduleItemPrefab == null || contentParent == null) return;

        GameObject item = Instantiate(moduleItemPrefab, contentParent);
        TextMeshProUGUI tmp = item.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = text;
            tmp.color = color;
        }
        _items.Add(item);
    }

    private void ClearItems()
    {
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            GameObject obj = _items[i];
            if (obj == null) continue;
            obj.SetActive(false); // Hilang seketika (Destroy dieksekusi end-of-frame) — anti numpuk
            Destroy(obj);
        }
        _items.Clear();
    }
}
