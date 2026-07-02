using UnityEngine;
using System.Collections.Generic;

namespace Weapons
{
    [CreateAssetMenu(fileName = "VehicleDatabase", menuName = "Vehicle/Vehicle Database")]
    public class VehicleDatabase : ScriptableObject
    {
        [Tooltip("Daftar semua mobil yang ada di game. Tarik semua VehicleData ke sini.")]
        public List<VehicleData> allVehicles;

        public VehicleData GetVehicleByName(string vehicleName)
        {
            if (string.IsNullOrEmpty(vehicleName)) return null;

            foreach (var vehicle in allVehicles)
            {
                if (vehicle == null || vehicle.vehiclePrefab == null) continue;

                // Baca nama dari VehicleBaseData di dalam prefab
                VehicleStatsManager sm = vehicle.vehiclePrefab.GetComponent<VehicleStatsManager>();
                string nameInPrefab = (sm != null && sm.baseData != null)
                    ? sm.baseData.vehicleName
                    : vehicle.name; // fallback ke nama file asset

                if (nameInPrefab == vehicleName)
                    return vehicle;
            }
            return null;
        }
    }
}
