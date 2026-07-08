using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ModuleDatabase", menuName = "Car Builder/Module Database")]
public class ModuleDatabase : ScriptableObject
{
    public List<ModuleTemplate> allModules = new List<ModuleTemplate>();

    private Dictionary<string, ModuleTemplate> _nameToModule;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        _nameToModule = new Dictionary<string, ModuleTemplate>();
        if (allModules == null) return;

        foreach (var module in allModules)
        {
            if (module == null || string.IsNullOrEmpty(module.moduleName)) continue;
            if (!_nameToModule.ContainsKey(module.moduleName))
                _nameToModule[module.moduleName] = module;
        }
    }

    public ModuleTemplate GetModuleByName(string name)
    {
        if (string.IsNullOrEmpty(name) || _nameToModule == null) return null;
        _nameToModule.TryGetValue(name, out var result);
        return result;
    }
}
