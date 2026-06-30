using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ModuleDatabase", menuName = "Car Builder/Module Database")]
public class ModuleDatabase : ScriptableObject
{
    public List<ModuleTemplate> allModules = new List<ModuleTemplate>();

    public ModuleTemplate GetModuleByName(string name)
    {
        return allModules.Find(m => m != null && m.moduleName == name);
    }
}
