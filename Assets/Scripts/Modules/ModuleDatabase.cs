using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ModuleDatabase", menuName = "Car Builder/Module Database")]
public class ModuleDatabase : ScriptableObject
{
    public List<ModuleTemplate> allModules = new List<ModuleTemplate>();

    private Dictionary<string, ModuleTemplate> _nameToModule;
    private Dictionary<string, ModuleTemplate> _uidToModule;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        _nameToModule = new Dictionary<string, ModuleTemplate>();
        _uidToModule = new Dictionary<string, ModuleTemplate>();
        if (allModules == null) return;

        foreach (var module in allModules)
        {
            if (module == null) continue;
            if (!string.IsNullOrEmpty(module.UID) && !_uidToModule.ContainsKey(module.UID))
                _uidToModule[module.UID] = module;
            if (!string.IsNullOrEmpty(module.moduleName) && !_nameToModule.ContainsKey(module.moduleName))
                _nameToModule[module.moduleName] = module;
        }
    }

    public ModuleTemplate GetModuleByName(string name)
    {
        if (string.IsNullOrEmpty(name) || _nameToModule == null) return null;
        _nameToModule.TryGetValue(name, out var result);
        return result;
    }

    public ModuleTemplate GetModuleByUID(string uid)
    {
        if (string.IsNullOrEmpty(uid) || _uidToModule == null) return null;
        _uidToModule.TryGetValue(uid, out var result);
        return result;
    }
}
