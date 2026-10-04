using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.LayerMasks;
using System.Collections.Generic;
using UnityEngine;
using NavigationGrid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Navigation.FlowFieldSystem
{
    public class FlowField
    {
        private readonly Collider[] _terrainColliderBuffer = new Collider[FlowFieldConstants.TERRAIN_COLLIDER_BUFFER_SIZE];
        private readonly Queue<Cell> _cellsToCheck = new();

        public void CreateCostField(NavigationGrid grid)
        {
            if (grid == null || grid.Cells == null)
            {
                return;
            }

            Vector3 halfExtents = new(
                grid.CellSize * 0.49f,
                FlowFieldConstants.QUERY_BOX_VERTICAL_HALF_EXTENT,
                grid.CellSize * 0.49f);

            for (int i = 0; i < grid.Cells.GetLength(0); i++)
            {
                for (int j = 0; j < grid.Cells.GetLength(1); j++)
                {
                    Cell cell = grid.Cells[i, j];
                    if (cell == null)
                    {
                        continue;
                    }

                    int obstacleCount = Physics.OverlapBoxNonAlloc(
                        cell.WorldPos,
                        halfExtents,
                        _terrainColliderBuffer,
                        Quaternion.identity,
                        TerrainLayers.All, QueryTriggerInteraction.Ignore);

                    cell.ResetCosts();

                    bool isImpassable = false;
                    bool isRough = false;

                    if (obstacleCount > 0)
                    {
                        // A full NonAlloc buffer may have truncated colliders, so block the cell conservatively.
                        if (obstacleCount == _terrainColliderBuffer.Length)
                        {
                            isImpassable = true;
                        }

                        for (int obstacleIndex = 0; obstacleIndex < obstacleCount; obstacleIndex++)
                        {
                            Collider obstacle = _terrainColliderBuffer[obstacleIndex];
                            int layerValue = 1 << obstacle.gameObject.layer;
                            if ((layerValue & TerrainLayers.Impassable.value) != 0)
                            {
                                isImpassable = true;
                                break;
                            }
                            if ((layerValue & TerrainLayers.Rough.value) != 0)
                            {
                                isRough = true;
                            }
                        }
                    }

                    bool hasSupport = GroundSupportQuery.TryGetSupportedPosition(cell.WorldPos, out _, out Collider support);
                    if (hasSupport && ((1 << support.gameObject.layer) & TerrainLayers.Rough.value) != 0)
                    {
                        isRough = true;
                    }
                    if (isImpassable || !hasSupport)
                    {
                        cell.IncreaseCost(FlowFieldConstants.IMPASSABLE_COST);
                    }
                    else if (isRough)
                    {
                        cell.IncreaseCost(FlowFieldConstants.ROUGH_TERRAIN_COST);
                    }
                }
            }
        }

        public void CreateIntegrationField(NavigationGrid grid, Cell destinationCell)
        {
            if (grid == null || grid.Cells == null || destinationCell == null)
            {
                return;
            }

            if (!TryGetCellGridPosition(grid, destinationCell, out _)
                || destinationCell.Cost >= FlowFieldConstants.IMPASSABLE_COST)
            {
                return;
            }

            destinationCell.BestCost = 0;

            _cellsToCheck.Clear();
            _cellsToCheck.Enqueue(destinationCell);
            while (_cellsToCheck.Count > 0)
            {
                Cell currentCell = _cellsToCheck.Dequeue();
                foreach (GridDirection gridDirection in GridDirection.CardinalDirections)
                {
                    Cell currentNeighbour = GetNeighbourCell(grid, currentCell, gridDirection);
                    if (currentNeighbour == null || currentNeighbour.Cost >= FlowFieldConstants.IMPASSABLE_COST)
                    {
                        continue;
                    }

                    if (currentNeighbour.Cost + currentCell.BestCost < currentNeighbour.BestCost)
                    {
                        currentNeighbour.BestCost = (ushort)(currentNeighbour.Cost + currentCell.BestCost);
                        _cellsToCheck.Enqueue(currentNeighbour);
                    }
                }
            }
        }

        public void CreateFlowField(NavigationGrid grid)
        {
            if (grid == null || grid.Cells == null)
            {
                return;
            }

            foreach (Cell currentCell in grid.Cells)
            {
                if (currentCell == null)
                {
                    continue;
                }

                if (currentCell.Cost >= FlowFieldConstants.IMPASSABLE_COST)
                {
                    currentCell.BestDirection = GridDirection.None;
                    continue;
                }

                Cell bestCostCell = currentCell;

                foreach (GridDirection gridDirection in GridDirection.AllDirections)
                {
                    Cell currentNeighbour = GetNeighbourCell(grid, currentCell, gridDirection);
                    if (currentNeighbour == null || !CanTraverse(grid, currentCell, currentNeighbour))
                    {
                        continue;
                    }

                    if (currentNeighbour.Cost < FlowFieldConstants.IMPASSABLE_COST
                        && currentNeighbour.BestCost < bestCostCell.BestCost)
                    {
                        bestCostCell = currentNeighbour;
                    }
                }

                if (bestCostCell != currentCell)
                {
                    currentCell.BestDirection =
                        GridDirection.GetDirectionFromV2I(bestCostCell.WorldGridPos - currentCell.WorldGridPos);
                }
                else
                {
                    currentCell.BestDirection = GridDirection.None;
                }
            }
        }

        public static bool CanTraverse(NavigationGrid grid, Cell from, Cell to)
        {
            if (from == null || to == null || to.Cost >= FlowFieldConstants.IMPASSABLE_COST
                || !TryGetCellGridPosition(grid, from, out _) || !TryGetCellGridPosition(grid, to, out _))
            {
                return false;
            }
            Vector2Int delta = to.WorldGridPos - from.WorldGridPos;
            if (Mathf.Abs(delta.x) > 1 || Mathf.Abs(delta.y) > 1)
            {
                return false;
            }
            if (delta.x != 0 && delta.y != 0)
            {
                if (!TryGetCellGridPosition(grid, from, out Vector2Int origin))
                {
                    return false;
                }
                Cell horizontal = grid.Cells[origin.x + delta.x, origin.y];
                Cell vertical = grid.Cells[origin.x, origin.y + delta.y];
                return horizontal != null && vertical != null
                    && horizontal.Cost < FlowFieldConstants.IMPASSABLE_COST
                    && vertical.Cost < FlowFieldConstants.IMPASSABLE_COST;
            }
            return true;
        }

        private Cell GetNeighbourCell(NavigationGrid grid, Cell currentCell, GridDirection gridDirection)
        {
            if (!TryGetCellGridPosition(grid, currentCell, out Vector2Int gridPos))
            {
                return null;
            }

            Vector2Int positionToCheck = gridPos + gridDirection.Vector;
            if (positionToCheck.x >= 0
                && positionToCheck.y >= 0
                && positionToCheck.x < grid.Cells.GetLength(0)
                && positionToCheck.y < grid.Cells.GetLength(1))
            {
                return grid.Cells[positionToCheck.x, positionToCheck.y];
            }

            return null;
        }

        private static bool TryGetCellGridPosition(NavigationGrid grid, Cell cell, out Vector2Int position)
        {
            if (cell == null || grid == null || grid.Cells == null)
            {
                position = Vector2Int.zero;
                return false;
            }

            Vector2Int chunkPos = cell.ChunkGridPos;
            if (chunkPos.x >= 0 && chunkPos.x < grid.Cells.GetLength(0)
                && chunkPos.y >= 0 && chunkPos.y < grid.Cells.GetLength(1)
                && grid.Cells[chunkPos.x, chunkPos.y] == cell)
            {
                position = chunkPos;
                return true;
            }

            Vector2Int worldPos = cell.WorldGridPos;
            if (worldPos.x >= 0 && worldPos.x < grid.Cells.GetLength(0)
                && worldPos.y >= 0 && worldPos.y < grid.Cells.GetLength(1)
                && grid.Cells[worldPos.x, worldPos.y] == cell)
            {
                position = worldPos;
                return true;
            }

            position = Vector2Int.zero;
            return false;
        }
    }
}
