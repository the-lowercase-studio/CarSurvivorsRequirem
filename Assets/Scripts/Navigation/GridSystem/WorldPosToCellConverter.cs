using UnityEngine;

namespace Assets.Scripts.Navigation.GridSystem
{
    public static class WorldPosToCellConverter
    {
        public static Cell GetCellFromGridByWorldPos(Grid grid, Vector3 worldPos)
        {
            int width = grid.Width;
            int height = grid.Height;
            float cellSize = grid.CellSize;

            float percentX = worldPos.x / (width * cellSize);
            float percentY = worldPos.z / (height * cellSize);

            percentX = Mathf.Clamp01(percentX);
            percentY = Mathf.Clamp01(percentY);

            int x = Mathf.Clamp(Mathf.FloorToInt(width * percentX), 0, width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(height * percentY), 0, height - 1);

            return grid.Cells[x, y];
        }
    }
}
