using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Navigation.Constants;

namespace Assets.Scripts.Navigation.GridSystem
{
    public static class GridCellsNotVisibleByMainCamera
    {
        private static readonly Collider[] _occupancyBuffer = new Collider[GridConstants.OCCUPANCY_BUFFER_SIZE];

        private static int GetEnemyCountOnCell(Cell cell, float cellSize, int maxEnemiesPerCell)
        {
            if (maxEnemiesPerCell <= 0)
            {
                return 0;
            }

            int count = Physics.OverlapBoxNonAlloc(
                cell.WorldPos,
                new Vector3(cellSize * 0.45f, 2f, cellSize * 0.45f),
                _occupancyBuffer,
                Quaternion.identity,
                EntityLayers.Enemy
            );

            for (int i = 0; i < count; i++)
            {
                _occupancyBuffer[i] = null;
            }

            return count;
        }

        public static IEnumerable<Cell> GetRandomWalkableCells(Grid grid, Camera camera)
        {
            List<Cell> cells = new List<Cell>();
            FillWalkableCells(grid, camera, cells);
            Shuffle(cells);
            return cells;
        }

        public static IEnumerable<Cell> GetRandomWalkableCells(Grid grid, Camera camera, int count, int maxEnemiesPerCell = -1)
        {
            List<Cell> cells = new List<Cell>();
            FillWalkableCells(grid, camera, cells, maxEnemiesPerCell);
            Shuffle(cells);

            int resultCount = Mathf.Min(count, cells.Count);
            for (int i = 0; i < resultCount; i++)
            {
                yield return cells[i];
            }
        }

        public static IEnumerable<Cell> GetRandomWalkableCellsOutsidePlayerChunk(
            Grid worldGrid,
            Grid playerChunk,
            Camera camera,
            int count,
            int outerSpawnBufferCells,
            int maxEnemiesPerCell)
        {
            List<Cell> cells = new List<Cell>();
            int currentBuffer = outerSpawnBufferCells;
            int maxBuffer = Mathf.Max(worldGrid.Width, worldGrid.Height);
            int lastCount = -1;

            while (cells.Count < count && currentBuffer <= maxBuffer)
            {
                FillWalkableCellsOutsidePlayerChunk(worldGrid, playerChunk, camera, currentBuffer, maxEnemiesPerCell, cells);
                if (cells.Count >= count || cells.Count == lastCount)
                {
                    break;
                }
                lastCount = cells.Count;
                currentBuffer += 4;
            }

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

        public static void FillWalkableCells(Grid grid, Camera camera, List<Cell> results, int maxEnemiesPerCell = -1)
        {
            results.Clear();
            Cell[,] cells = grid.Cells;
            float cellSize = grid.CellSize;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Cell cell = cells[x, y];
                    if (cell != null
                        && CellStatusDescriber.IsWalkable(cell)
                        && !CellCameraVisibilityChecker.IsCellVisibleFromCamera(cell.WorldPos, camera))
                    {
                        int currentEnemies = GetEnemyCountOnCell(cell, cellSize, maxEnemiesPerCell);
                        int slots = maxEnemiesPerCell > 0 ? Mathf.Max(0, maxEnemiesPerCell - currentEnemies) : 1;
                        for (int i = 0; i < slots; i++)
                        {
                            results.Add(cell);
                        }
                    }
                }
            }
        }

        public static void FillWalkableCellsOutsidePlayerChunk(
            Grid worldGrid,
            Grid playerChunk,
            Camera camera,
            int outerSpawnBufferCells,
            int maxEnemiesPerCell,
            List<Cell> results)
        {
            results.Clear();

            Cell centerCell = playerChunk.Cells[playerChunk.Width / 2, playerChunk.Height / 2];
            if (centerCell == null)
            {
                return;
            }

            Vector2Int centerGridPos = centerCell.WorldGridPos;
            int halfWidth = playerChunk.Width / 2;
            int halfHeight = playerChunk.Height / 2;

            int spawnRangeX = halfWidth + outerSpawnBufferCells;
            int spawnRangeY = halfHeight + outerSpawnBufferCells;

            int minX = Mathf.Max(0, centerGridPos.x - spawnRangeX);
            int maxX = Mathf.Min(worldGrid.Width - 1, centerGridPos.x + spawnRangeX);
            int minY = Mathf.Max(0, centerGridPos.y - spawnRangeY);
            int maxY = Mathf.Min(worldGrid.Height - 1, centerGridPos.y + spawnRangeY);

            Cell[,] cells = worldGrid.Cells;
            float cellSize = worldGrid.CellSize;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (Mathf.Abs(x - centerGridPos.x) <= halfWidth && Mathf.Abs(y - centerGridPos.y) <= halfHeight)
                    {
                        continue;
                    }

                    Cell cell = cells[x, y];
                    if (cell != null
                        && CellStatusDescriber.IsWalkable(cell)
                        && !CellCameraVisibilityChecker.IsCellVisibleFromCamera(cell.WorldPos, camera))
                    {
                        int currentEnemies = GetEnemyCountOnCell(cell, cellSize, maxEnemiesPerCell);
                        int slots = maxEnemiesPerCell > 0 ? Mathf.Max(0, maxEnemiesPerCell - currentEnemies) : 1;
                        for (int i = 0; i < slots; i++)
                        {
                            results.Add(cell);
                        }
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
