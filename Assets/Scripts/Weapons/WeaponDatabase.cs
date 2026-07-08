using UnityEngine;
using System.Collections.Generic;

namespace Weapons
{
    [CreateAssetMenu(fileName = "WeaponDatabase", menuName = "Weapons/Weapon Database")]
    public class WeaponDatabase : ScriptableObject
    {
        [Tooltip("Daftar semua senjata yang ada di game. Tarik semua WeaponData ke sini.")]
        public List<WeaponData> allWeapons;

        private Dictionary<string, WeaponData> _nameToWeapon;

        private void OnEnable()
        {
            BuildLookup();
        }

        private void BuildLookup()
        {
            _nameToWeapon = new Dictionary<string, WeaponData>();
            if (allWeapons == null) return;

            foreach (var weapon in allWeapons)
            {
                if (weapon == null || string.IsNullOrEmpty(weapon.weaponName)) continue;
                if (!_nameToWeapon.ContainsKey(weapon.weaponName))
                    _nameToWeapon[weapon.weaponName] = weapon;
            }
        }

        public WeaponData GetWeaponByName(string weaponName)
        {
            if (string.IsNullOrEmpty(weaponName) || _nameToWeapon == null) return null;
            _nameToWeapon.TryGetValue(weaponName, out var result);
            return result;
        }
    }
}
