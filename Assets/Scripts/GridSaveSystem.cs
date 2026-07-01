using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistem penyimpanan Grid Inventaris antar scene.
/// Menyimpan daftar modul yang terpasang beserta posisi dan rotasinya ke PlayerPrefs dalam format JSON.
/// </summary>
public static class GridSaveSystem
{
    private const string SAVE_KEY_PREFIX = "GridLayout_";

    // --- DATA YANG BISA DI-SERIALISASI ---
    [System.Serializable]
    public class SavedModule
    {
        public string zoneName; // Zona tempat modul terpasang
        public string moduleName;  // Nama modul di ModuleTemplate (digunakan sebagai key lookup)
        public int gridX;
        public int gridY;
        public int rotationAngle;
    }

    [System.Serializable]
    public class SavedGridLayout
    {
        public List<SavedModule> modules = new List<SavedModule>();
    }

    // --- SAVE ---
    public static void SaveGrid(string vehicleName, VehicleStatsManager statsManager)
    {
        if (statsManager == null || string.IsNullOrEmpty(vehicleName)) return;

        SavedGridLayout layout = new SavedGridLayout();

        foreach (var placed in statsManager.installedModules)
        {
            if (placed.moduleTemplate == null) continue;

            SavedModule saved = new SavedModule
            {
                zoneName = placed.zoneName,
                moduleName = placed.moduleTemplate.moduleName,
                gridX = placed.gridPosition.x,
                gridY = placed.gridPosition.y,
                rotationAngle = placed.rotationAngle
            };
            layout.modules.Add(saved);
        }

        string json = JsonUtility.ToJson(layout);
        string key = SAVE_KEY_PREFIX + vehicleName;
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();

        Debug.Log($"[GridSaveSystem] Disimpan {layout.modules.Count} modul untuk '{vehicleName}'");
    }

    // --- LOAD ---
    public static void LoadGrid(string vehicleName, VehicleStatsManager statsManager, ModuleDatabase moduleDatabase)
    {
        if (statsManager == null || moduleDatabase == null || string.IsNullOrEmpty(vehicleName)) return;

        string key = SAVE_KEY_PREFIX + vehicleName;
        string json = PlayerPrefs.GetString(key, "");

        if (string.IsNullOrEmpty(json))
        {
            Debug.Log($"[GridSaveSystem] Tidak ada data tersimpan untuk '{vehicleName}'");
            return;
        }

        SavedGridLayout layout = JsonUtility.FromJson<SavedGridLayout>(json);
        if (layout == null || layout.modules.Count == 0) return;

        // Bersihkan modul yang sudah terpasang sebelum me-load
        statsManager.ClearAllModules();

        int loaded = 0;
        foreach (var saved in layout.modules)
        {
            ModuleTemplate template = moduleDatabase.GetModuleByName(saved.moduleName);
            if (template == null)
            {
                Debug.LogWarning($"[GridSaveSystem] ModuleTemplate '{saved.moduleName}' tidak ditemukan di database! Dilewati.");
                continue;
            }

            Vector2Int pos = new Vector2Int(saved.gridX, saved.gridY);
            bool success = statsManager.InstallModule(template, saved.zoneName, pos, saved.rotationAngle);
            if (success) loaded++;
        }

        Debug.Log($"[GridSaveSystem] Dimuat {loaded}/{layout.modules.Count} modul untuk '{vehicleName}'");
    }

    // --- DELETE ---
    public static void DeleteGrid(string vehicleName)
    {
        string key = SAVE_KEY_PREFIX + vehicleName;
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        Debug.Log($"[GridSaveSystem] Data grid untuk '{vehicleName}' dihapus.");
    }

    // --- CEK ADA DATA ---
    public static bool HasSavedGrid(string vehicleName)
    {
        string key = SAVE_KEY_PREFIX + vehicleName;
        return PlayerPrefs.HasKey(key);
    }
}
