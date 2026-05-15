using UnityEngine;

namespace Assets.Scripts.Navigation.GridSystem
{
    public static class GridDebug
    {
        public static void DisplayGrid(Grid grid, Color cellBorderColor, Color blockedCellBorderColor, float yOffset = 0, float duration = 0.1f)
        {
            Cell[,] cells = grid.Cells;
            float cellSize = grid.CellSize;

            for (int i = 0; i < cells.GetLength(0); i++)
            {
                for (int j = 0; j < cells.GetLength(1); j++)
                {
                    Vector3 cellWorldPos = cells[i, j].WorldPos;
                    Color correctCellBorderColor = GetCorrectBorderColor(cells[i, j], cellBorderColor, blockedCellBorderColor);
                    Debug.DrawLine(new Vector3(cellWorldPos.x - cellSize / 2, yOffset, cellWorldPos.z - cellSize / 2),
                                   new Vector3(cellWorldPos.x - cellSize / 2, yOffset, cellWorldPos.z + cellSize / 2),
                                   correctCellBorderColor, duration);
                    Debug.DrawLine(new Vector3(cellWorldPos.x - cellSize / 2, yOffset, cellWorldPos.z - cellSize / 2),
                                   new Vector3(cellWorldPos.x + cellSize / 2, yOffset, cellWorldPos.z - cellSize / 2),
                                   correctCellBorderColor, duration);
                }
            }

            float horizontalCellsCount = grid.Width * cellSize;
            float verticalCellsCount = grid.Height * cellSize;
            float firstCellHorizontalPos = cells[0, 0].WorldPos.x;
            float firstCellVerticalPos = cells[0, 0].WorldPos.z;
            Debug.DrawLine(new Vector3(firstCellHorizontalPos - cellSize / 2, yOffset, firstCellVerticalPos + verticalCellsCount - cellSize / 2),
                           new Vector3(firstCellHorizontalPos + horizontalCellsCount - cellSize / 2, yOffset, firstCellVerticalPos + verticalCellsCount - cellSize / 2),
                           cellBorderColor, duration);
            Debug.DrawLine(new Vector3(firstCellHorizontalPos + horizontalCellsCount - cellSize / 2, yOffset, firstCellVerticalPos + verticalCellsCount - cellSize / 2),
                           new Vector3(firstCellHorizontalPos + horizontalCellsCount - cellSize / 2, yOffset, firstCellVerticalPos - cellSize / 2),
                           cellBorderColor, duration);
        }

        private static Color GetCorrectBorderColor(Cell cell, Color cellBorderColor, Color blockedCellBorderColor) =>
            cell.Cost == byte.MaxValue ? blockedCellBorderColor : cellBorderColor;
    }
}
