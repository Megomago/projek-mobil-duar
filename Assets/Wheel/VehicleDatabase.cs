using UnityEngine;
using System.Collections.Generic;

namespace Weapons
{
    [CreateAssetMenu(fileName = "VehicleDatabase", menuName = "Weapons/Vehicle Database")]
    public class VehicleDatabase : ScriptableObject
    {
        [Tooltip("Daftar semua mobil yang ada di game. Tarik semua VehicleData ke sini.")]
        public List<VehicleData> allVehicles;

        public VehicleData GetVehicleByName(string vehicleName)
        {
            if (string.IsNullOrEmpty(vehicleName)) return null;

            foreach (var vehicle in allVehicles)
            {
                if (vehicle != null && vehicle.vehicleName == vehicleName)
                {
                    return vehicle;
                }
            }
            return null;
        }
    }
}
