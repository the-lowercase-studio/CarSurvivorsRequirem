using Assets.Scripts.Extensions;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Navigation.GridSystem
{
    public static class GridCellsNotVisibleByMainCamera
    {
        public static IEnumerable<Cell> GetRandomWalkableCells(Grid grid, Camera camera)
        {
            return GetWalkableCells(grid, camera).Shuffle();
        }

        public static IEnumerable<Cell> GetRandomWalkableCells(Grid grid, Camera camera, int count)
        {
            return GetRandomWalkableCells(grid, camera).Take(count);
        }

        public static IEnumerable<Cell> GetWalkableCells(Grid grid, Camera camera)
        {
            List<Cell> notVisibleCells = new List<Cell>();
            Cell[,] cells = grid.Cells;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Cell cell = cells[x, y];
                    if (CellStatusDescriber.IsWalkable(cell)
                        && !IsCellVisibleFromCamera(cell.WorldPos, camera))
                    {
                        notVisibleCells.Add(cell);
                    }
                }
            }

            return notVisibleCells;
        }

        private static bool IsCellVisibleFromCamera(Vector3 cellPosition, Camera camera)
        {
            Vector3 viewportPoint = camera.WorldToViewportPoint(cellPosition);
            return viewportPoint.x >= 0 && viewportPoint.x <= 1 &&
                   viewportPoint.y >= 0 && viewportPoint.y <= 1 &&
                   viewportPoint.z > 0;
        }
    }
}
