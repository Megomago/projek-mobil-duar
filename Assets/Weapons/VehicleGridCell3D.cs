using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Satu kotak grid 3D di atap mobil. Punya collider untuk raycast drag/drop langsung di dunia.
    /// </summary>
    public class VehicleGridCell3D : MonoBehaviour
    {
        public int gridX;
        public int gridY;
        public VehicleGrid3D owner;
    }
}
