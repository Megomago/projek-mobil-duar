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
            AddItem(string.IsNullOrEmpty(part.partName) ? part.partType.ToString() : part.partName, baseModuleColor);
        }

        // ── INSTALLED MODULES ──
        foreach (var module in statsManager.installedModules)
        {
            if (module?.moduleTemplate == null) continue;
            if (module.moduleTemplate.hideFromModuleList) continue;
            AddItem(module.moduleTemplate.moduleName, installedModuleColor);
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
        foreach (var obj in _items)
            Destroy(obj);
        _items.Clear();
    }
}
