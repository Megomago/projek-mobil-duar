using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modul yang terpasang di zona INTERNAL (GridZone.isInternalGrid) disembunyikan total
/// saat X-ray mati, dan baru di-render saat X-ray nyala (tombol O di Battlefield /
/// panel inventaris di garasi).
///
/// TIDAK ada ganti material: overlay camera URP sudah punya clearDepth = true, jadi
/// modul internal tampil tembus bodi dengan material aslinya (warna normal).
/// </summary>
public static class InternalModuleVisibility
{
    private class Entry
    {
        public PlacedModule module;
        public GameObject root;
        public Renderer[] renderers;
        public bool[] originalEnabled;
        public int state = -1; // -1 = belum diterapkan, 0 = disembunyikan, 1 = ditampilkan
    }

    private static readonly List<Entry> _entries = new List<Entry>();
    private static bool _xrayActive;
    private static Transform _focusRoot;
    private static bool _requireFocus;

    public static bool XRayActive => _xrayActive;

    /// <summary>
    /// Dipanggil oleh ModuleXRayCamera tiap kali state X-ray berubah.
    /// focusRoot null + requireFocus false = tampilkan modul internal semua kendaraan (garasi).
    /// focusRoot null + requireFocus true  = tidak ada kendaraan fokus, sembunyikan semua (Battlefield saat turun).
    /// </summary>
    public static void SetXRay(bool active, Transform focusRoot, bool requireFocus)
    {
        if (_xrayActive == active && _focusRoot == focusRoot && _requireFocus == requireFocus)
            return;

        _xrayActive = active;
        _focusRoot = focusRoot;
        _requireFocus = requireFocus;
        RefreshAll();
    }

    /// <summary>Daftarkan modul. isInternal = zona tempat modul ini berada adalah zona internal.</summary>
    public static void Register(PlacedModule module, bool isInternal)
    {
        if (!Application.isPlaying) return;
        if (!isInternal || module == null || module.spawnedPrefab == null) return;

        // Kalau modul yang sama sudah terdaftar (rebuild/duplikat), buang dulu.
        Unregister(module);

        GameObject root = module.spawnedPrefab;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        var entry = new Entry
        {
            module = module,
            root = root,
            renderers = renderers,
            originalEnabled = new bool[renderers.Length],
        };

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            entry.originalEnabled[i] = r.enabled;
        }

        _entries.Add(entry);
        Apply(entry, ShouldShow(entry));
    }

    public static void Unregister(PlacedModule module)
    {
        if (module == null) return;

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].module == module)
            {
                HideEntry(_entries[i]);
                _entries.RemoveAt(i);
            }
        }
    }

    private static bool ShouldShow(Entry entry)
    {
        if (!_xrayActive) return false;

        if (_focusRoot != null)
            return entry.root != null && entry.root.transform.IsChildOf(_focusRoot);

        // Tidak ada kendaraan fokus: garasi tetap tampilkan semua, Battlefield sembunyikan semua.
        return !_requireFocus;
    }

    private static void RefreshAll()
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry entry = _entries[i];
            if (entry == null || entry.root == null || entry.module == null)
            {
                _entries.RemoveAt(i);
                continue;
            }

            Apply(entry, ShouldShow(entry));
        }
    }

    private static void Apply(Entry entry, bool show)
    {
        int target = show ? 1 : 0;
        if (entry.state == target) return;

        if (show) ShowEntry(entry);
        else HideEntry(entry);
    }

    private static void ShowEntry(Entry entry)
    {
        for (int i = 0; i < entry.renderers.Length; i++)
        {
            Renderer r = entry.renderers[i];
            if (r == null) continue;
            r.enabled = entry.originalEnabled[i];
        }

        entry.state = 1;
    }

    private static void HideEntry(Entry entry)
    {
        for (int i = 0; i < entry.renderers.Length; i++)
        {
            Renderer r = entry.renderers[i];
            if (r == null) continue;
            r.enabled = false;
        }

        entry.state = 0;
    }
}
