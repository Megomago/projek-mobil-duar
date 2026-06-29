using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Konversi koordinat grid (X kanan, Y turun) ke posisi lokal 3D di pivot mobil.
    /// Grid Y=0 ada di sisi atas; Y bertambah menuju bawah (-Z lokal).
    /// </summary>
    public static class VehicleGridUtility
    {
        public static Vector3 GridToLocalCenter(int gridX, int gridY, int sizeX, int sizeY, float cellSize)
        {
            float offsetX = (gridX + sizeX * 0.5f) * cellSize;
            float offsetZ = -(gridY + sizeY * 0.5f) * cellSize;
            return new Vector3(offsetX, 0f, offsetZ);
        }

        public static Vector3 GridCellCorner(int gridX, int gridY, float cellSize)
        {
            return new Vector3(gridX * cellSize, 0f, -gridY * cellSize);
        }

        public static bool TryLocalPointToGrid(
            Vector3 localPoint,
            float cellSize,
            int gridSizeX,
            int gridSizeY,
            out int gridX,
            out int gridY)
        {
            gridX = Mathf.FloorToInt(localPoint.x / cellSize);
            gridY = Mathf.FloorToInt(-localPoint.z / cellSize);

            return gridX >= 0 && gridY >= 0 && gridX < gridSizeX && gridY < gridSizeY;
        }
    }
}
