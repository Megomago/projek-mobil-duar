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
        private Dictionary<string, VehicleData> _uidToVehicle;

        private void OnEnable()
        {
            BuildLookup();
        }

        private void BuildLookup()
        {
            _nameToVehicle = new Dictionary<string, VehicleData>();
            _uidToVehicle = new Dictionary<string, VehicleData>();
            if (allVehicles == null) return;

            foreach (var vehicle in allVehicles)
            {
                if (vehicle == null) continue;
                if (!string.IsNullOrEmpty(vehicle.UID) && !_uidToVehicle.ContainsKey(vehicle.UID))
                    _uidToVehicle[vehicle.UID] = vehicle;
                if (!string.IsNullOrEmpty(vehicle.vehicleName) && !_nameToVehicle.ContainsKey(vehicle.vehicleName))
                    _nameToVehicle[vehicle.vehicleName] = vehicle;
            }
        }

        public VehicleData GetVehicleByName(string vehicleName)
        {
            if (string.IsNullOrEmpty(vehicleName) || _nameToVehicle == null) return null;
            _nameToVehicle.TryGetValue(vehicleName, out var result);
            return result;
        }

        public VehicleData GetVehicleByUID(string uid)
        {
            if (string.IsNullOrEmpty(uid) || _uidToVehicle == null) return null;
            _uidToVehicle.TryGetValue(uid, out var result);
            return result;
        }
    }
}
