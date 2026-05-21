using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Navigation.GridSystem
{
    public static class GridCellsNotVisibleByMainCamera
    {
        public static IEnumerable<Cell> GetRandomWalkableCells(Grid grid, Camera camera)
        {
            List<Cell> cells = new List<Cell>();
            FillWalkableCells(grid, camera, cells);
            Shuffle(cells);
            return cells;
        }

        public static IEnumerable<Cell> GetRandomWalkableCells(Grid grid, Camera camera, int count)
        {
            List<Cell> cells = new List<Cell>();
            FillWalkableCells(grid, camera, cells);
            Shuffle(cells);

            int resultCount = Mathf.Min(count, cells.Count);
            for (int i = 0; i < resultCount; i++)
            {
                yield return cells[i];
            }
        }

        public static IEnumerable<Cell> GetWalkableCells(Grid grid, Camera camera)
        {
            List<Cell> cells = new List<Cell>();
            FillWalkableCells(grid, camera, cells);
            return cells;
        }

        public static Cell GetRandomWalkableCell(Grid grid, Camera camera)
        {
            Cell selectedCell = null;
            int candidateCount = 0;
            Cell[,] cells = grid.Cells;
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Cell cell = cells[x, y];
                    if (cell != null
                        && CellStatusDescriber.IsWalkable(cell)
                        && !CellCameraVisibilityChecker.IsCellVisibleFromCamera(cell.WorldPos, camera))
                    {
                        candidateCount++;
                        if (UnityEngine.Random.Range(0, candidateCount) == 0)
                        {
                            selectedCell = cell;
                        }
                    }
                }
            }

            return selectedCell;
        }

        public static void FillWalkableCells(Grid grid, Camera camera, List<Cell> results)
        {
            results.Clear();
            Cell[,] cells = grid.Cells;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Cell cell = cells[x, y];
                    if (cell != null
                        && CellStatusDescriber.IsWalkable(cell)
                        && !CellCameraVisibilityChecker.IsCellVisibleFromCamera(cell.WorldPos, camera))
                    {
                        results.Add(cell);
                    }
                }
            }
        }

        private static void Shuffle(List<Cell> cells)
        {
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                (cells[i], cells[randomIndex]) = (cells[randomIndex], cells[i]);
            }
        }
    }
}
