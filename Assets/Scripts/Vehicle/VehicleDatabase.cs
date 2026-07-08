using UnityEngine;
using System.Collections.Generic;

namespace Weapons
{
    [CreateAssetMenu(fileName = "VehicleDatabase", menuName = "Vehicle/Vehicle Database")]
    public class VehicleDatabase : ScriptableObject
    {
        [Tooltip("Daftar semua mobil yang ada di game. Tarik semua VehicleData ke sini.")]
        public List<VehicleData> allVehicles;

        private Dictionary<string, VehicleData> _nameToVehicle;

        private void OnEnable()
        {
            BuildLookup();
        }

        private void BuildLookup()
        {
            _nameToVehicle = new Dictionary<string, VehicleData>();
            if (allVehicles == null) return;

            foreach (var vehicle in allVehicles)
            {
                if (vehicle == null || string.IsNullOrEmpty(vehicle.vehicleName)) continue;
                if (!_nameToVehicle.ContainsKey(vehicle.vehicleName))
                    _nameToVehicle[vehicle.vehicleName] = vehicle;
            }
        }

        public VehicleData GetVehicleByName(string vehicleName)
        {
            if (string.IsNullOrEmpty(vehicleName) || _nameToVehicle == null) return null;
            _nameToVehicle.TryGetValue(vehicleName, out var result);
            return result;
        }
    }
}
