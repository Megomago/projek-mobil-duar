using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;

/// <summary>
/// Sistem penyimpanan Grid Inventaris antar scene.
/// Menyimpan daftar modul yang terpasang beserta posisi dan rotasinya ke file JSON di persistentDataPath.
/// </summary>
public static class GridSaveSystem
{
    private static string SavePath(string vehicleName)
    {
        return Path.Combine(Application.persistentDataPath, $"Grid_{vehicleName}.json");
    }

    [System.Serializable]
    public class SavedModule
    {
        public string zoneName;
        public string moduleName;
        [Tooltip("ID unik modul — prioritas load. Fallback ke moduleName untuk save lama.")]
        public string moduleUid;
        public int gridX;
        public int gridY;
        public int rotationAngle;
        [Tooltip("Sisa amunisi modul (v2+). Disimpan supaya ammo bertahan antar sesi.")]
        public float ammoLeft;
    }

    [System.Serializable]
    public class SavedGridLayout
    {
        // v1 = tanpa ammoLeft (save lama → modul dimuat penuh).
        // v2 = ammoLeft disimpan & diterapkan saat load (0 = box benar-benar kosong).
        public int version = 2;
        public List<SavedModule> modules = new List<SavedModule>();
    }

    // --- SAVE ---
    public static void SaveGrid(string vehicleName, VehicleGridSystem gridSystem)
    {
        if (gridSystem == null || string.IsNullOrEmpty(vehicleName)) return;

        SavedGridLayout layout = new SavedGridLayout();

        foreach (var placed in gridSystem.installedModules)
        {
            if (placed.moduleTemplate == null) continue;

            SavedModule saved = new SavedModule
            {
                zoneName = placed.zoneName,
                moduleName = placed.moduleTemplate.moduleName,
                moduleUid = placed.moduleTemplate.UID,
                gridX = placed.gridPosition.x,
                gridY = placed.gridPosition.y,
                rotationAngle = placed.rotationAngle,
                ammoLeft = placed.currentAmmoPoint
            };
            layout.modules.Add(saved);
        }

        string json = JsonUtility.ToJson(layout);
        File.WriteAllText(SavePath(vehicleName), json);

        #if UNITY_EDITOR
        Debug.Log($"[GridSaveSystem] Disimpan {layout.modules.Count} modul untuk '{vehicleName}'");
        #endif
    }

    // --- LOAD (sync) ---
    public static void LoadGrid(string vehicleName, VehicleGridSystem gridSystem, ModuleDatabase moduleDatabase)
    {
        if (gridSystem == null || moduleDatabase == null || string.IsNullOrEmpty(vehicleName)) return;

        string path = SavePath(vehicleName);
        if (!File.Exists(path))
        {
            #if UNITY_EDITOR
            Debug.Log($"[GridSaveSystem] Tidak ada data tersimpan untuk '{vehicleName}'");
            #endif
            return;
        }

        string json = File.ReadAllText(path);
        SavedGridLayout layout = JsonUtility.FromJson<SavedGridLayout>(json);
        if (layout == null || layout.modules.Count == 0) return;

        gridSystem.ClearAllModules();

        int loaded = 0;
        foreach (var saved in layout.modules)
        {
            ModuleTemplate template = moduleDatabase.GetModuleByUID(saved.moduleUid);
            if (template == null)
                template = moduleDatabase.GetModuleByName(saved.moduleName);
            if (template == null)
            {
                Debug.LogWarning($"[GridSaveSystem] ModuleTemplate '{saved.moduleName}' tidak ditemukan di database! Dilewati.");
                continue;
            }

            Vector2Int pos = new Vector2Int(saved.gridX, saved.gridY);
            // Load = grandfather: aturan internal tidak boleh menggugurkan modul lama
            PlacedModule newModule = gridSystem.InstallModule(template, saved.zoneName, pos, saved.rotationAngle, true);
            if (newModule != null)
            {
                loaded++;
                ApplyAmmoToModule(newModule, saved, layout.version);
            }
        }
        #if UNITY_EDITOR
        Debug.Log($"[GridSaveSystem] Dimuat {loaded}/{layout.modules.Count} modul untuk '{vehicleName}'");
        #endif
    }

    /// <summary>
    /// Terapkan sisa amunisi dari save ke modul yang baru saja di-install.
    /// Save versi 1 (lama) tidak punya data ammo → modul tetap penuh (default).
    /// </summary>
    private static void ApplyAmmoToModule(PlacedModule placed, SavedModule saved, int layoutVersion)
    {
        if (layoutVersion < 2) return;
        if (placed == null) return;

        placed.currentAmmoPoint = Mathf.Max(0f, saved.ammoLeft);
    }

    // --- LOAD (async — spread across frames) ---
    public static IEnumerator LoadGridAsync(string vehicleName, VehicleGridSystem gridSystem, ModuleDatabase moduleDatabase, System.Action<int, int> onProgress = null)
    {
        if (gridSystem == null || moduleDatabase == null || string.IsNullOrEmpty(vehicleName)) yield break;

        string path = SavePath(vehicleName);
        if (!File.Exists(path))
        {
            #if UNITY_EDITOR
            Debug.Log($"[GridSaveSystem] Tidak ada data tersimpan untuk '{vehicleName}'");
            #endif
            onProgress?.Invoke(0, 0);
            yield break;
        }

        string json = File.ReadAllText(path);
        SavedGridLayout layout = JsonUtility.FromJson<SavedGridLayout>(json);
        if (layout == null || layout.modules.Count == 0)
        {
            onProgress?.Invoke(0, 0);
            yield break;
        }

        gridSystem.ClearAllModules();

        int loaded = 0;
        int total = layout.modules.Count;
        int counter = 0;

        foreach (var saved in layout.modules)
        {
            if (gridSystem == null) yield break;

            ModuleTemplate template = moduleDatabase.GetModuleByUID(saved.moduleUid);
            if (template == null)
                template = moduleDatabase.GetModuleByName(saved.moduleName);
            if (template == null)
            {
                Debug.LogWarning($"[GridSaveSystem] ModuleTemplate '{saved.moduleName}' tidak ditemukan di database! Dilewati.");
                continue;
            }

            Vector2Int pos = new Vector2Int(saved.gridX, saved.gridY);
            // Load = grandfather: aturan internal tidak boleh menggugurkan modul lama
            PlacedModule newModule = gridSystem.InstallModule(template, saved.zoneName, pos, saved.rotationAngle, true);
            if (newModule != null)
            {
                loaded++;
                ApplyAmmoToModule(newModule, saved, layout.version);
            }

            counter++;
            onProgress?.Invoke(counter, total);

            if (counter % 5 == 0)
                yield return null;
        }
        #if UNITY_EDITOR
        Debug.Log($"[GridSaveSystem] Dimuat {loaded}/{total} modul untuk '{vehicleName}'");
        #endif
    }

    // --- DELETE ---
    public static void DeleteGrid(string vehicleName)
    {
        string path = SavePath(vehicleName);
        if (File.Exists(path))
        {
            File.Delete(path);
            #if UNITY_EDITOR
            Debug.Log($"[GridSaveSystem] Data grid untuk '{vehicleName}' dihapus.");
            #endif
        }
    }

    // --- CEK ADA DATA ---
    public static bool HasSavedGrid(string vehicleName)
    {
        return File.Exists(SavePath(vehicleName));
    }
}
