using System.Collections.Generic;

namespace Assets.Scripts.Navigation.GridSystem
{
    public static class GridEdgeHelper
    {
        public static List<Cell> GetWalkableEdgeCells(Grid grid)
        {
            List<Cell> edgeCells = new List<Cell>();
            Cell[,] cells = grid.Cells;

            int horizontalCellsCount = cells.GetLength(0);
            int verticalCellsCount = cells.GetLength(1);
            for (int x = 0; x < horizontalCellsCount; x++)
            {
                if (CellStatusDescriber.IsWalkable(cells[x, 0])) { edgeCells.Add(cells[x, 0]); }
                if (CellStatusDescriber.IsWalkable(cells[x, verticalCellsCount - 1])) { edgeCells.Add(cells[x, verticalCellsCount - 1]); }
            }

            for (int y = 0; y < verticalCellsCount; y++)
            {
                if (CellStatusDescriber.IsWalkable(cells[0, y])) { edgeCells.Add(cells[0, y]); }
                if (CellStatusDescriber.IsWalkable(cells[horizontalCellsCount - 1, y])) { edgeCells.Add(cells[horizontalCellsCount - 1, y]); }
            }

            return edgeCells;
        }
    }
}
