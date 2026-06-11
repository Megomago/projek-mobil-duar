using UnityEngine;
using System.Collections.Generic;

namespace Weapons
{
    [CreateAssetMenu(fileName = "WeaponDatabase", menuName = "Weapons/Weapon Database")]
    public class WeaponDatabase : ScriptableObject
    {
        [Tooltip("Daftar semua senjata yang ada di game. Tarik semua WeaponData ke sini.")]
        public List<WeaponData> allWeapons;

        /// <summary>
        /// Mencari WeaponData berdasarkan namanya.
        /// </summary>
        public WeaponData GetWeaponByName(string weaponName)
        {
            if (string.IsNullOrEmpty(weaponName)) return null;

            foreach (var weapon in allWeapons)
            {
                if (weapon != null && weapon.weaponName == weaponName)
                {
                    return weapon;
                }
            }
            return null;
        }
    }
}
