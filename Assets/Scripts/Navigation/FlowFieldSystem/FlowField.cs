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
            Vector3 halfExtents = Vector3.one * (grid.CellSize / 2 + FlowFieldConstants.EDGES_OFFSET);

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
                        TerrainLayers.All);

                    cell.ResetCosts();

                    int maxCost = 0;
                    if (obstacleCount > 0)
                    {
                        // A full NonAlloc buffer may have truncated colliders, so block the cell conservatively.
                        if (obstacleCount == _terrainColliderBuffer.Length)
                        {
                            maxCost = FlowFieldConstants.IMPASSABLE_COST;
                        }

                        for (int obstacleIndex = 0; obstacleIndex < obstacleCount; obstacleIndex++)
                        {
                            Collider obstacle = _terrainColliderBuffer[obstacleIndex];
                            int layerValue = 1 << obstacle.gameObject.layer;
                            if (maxCost < FlowFieldConstants.IMPASSABLE_COST
                                && (layerValue & TerrainLayers.Impassable.value) == TerrainLayers.Impassable.value)
                            {
                                maxCost = FlowFieldConstants.IMPASSABLE_COST;
                            }
                            else if (maxCost < FlowFieldConstants.ROUGH_TERRAIN_COST
                                && (layerValue & TerrainLayers.Rough.value) == TerrainLayers.Rough.value)
                            {
                                maxCost = FlowFieldConstants.ROUGH_TERRAIN_COST;
                            }
                        }

                        if (maxCost > FlowFieldConstants.DEFAULT_FIELD_COST)
                        {
                            cell.IncreaseCost(maxCost);
                        }
                    }
                    else
                    {
                        cell.IncreaseCost(FlowFieldConstants.IMPASSABLE_COST);
                    }
                }
            }
        }

        public void CreateIntegrationField(NavigationGrid grid, Cell destinationCell)
        {
            if (destinationCell == null)
            {
                return;
            }

            destinationCell.Cost = 0;
            destinationCell.BestCost = 0;

            _cellsToCheck.Clear();
            _cellsToCheck.Enqueue(destinationCell);
            while (_cellsToCheck.Count > 0)
            {
                Cell currentCell = _cellsToCheck.Dequeue();
                foreach (GridDirection gridDirection in GridDirection.CardinalDirections)
                {
                    Cell currentNeighbour = GetNeighbourCell(grid, currentCell, gridDirection);
                    if (currentNeighbour == null)
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
            foreach (Cell currentCell in grid.Cells)
            {
                if (currentCell == null)
                {
                    continue;
                }

                Cell bestCostCell = currentCell;

                foreach (GridDirection gridDirection in GridDirection.AllDirections)
                {
                    Cell currentNeighbour = GetNeighbourCell(grid, currentCell, gridDirection);
                    if (currentNeighbour == null)
                    {
                        continue;
                    }

                    if (currentNeighbour.BestCost < bestCostCell.BestCost)
                    {
                        bestCostCell = currentNeighbour;
                    }
                }

                if (bestCostCell != currentCell)
                {
                    currentCell.BestDirection =
                        GridDirection.GetDirectionFromV2I(bestCostCell.WorldGridPos - currentCell.WorldGridPos);
                }
            }
        }

        private Cell GetNeighbourCell(NavigationGrid grid, Cell currentCell, GridDirection gridDirection)
        {
            Vector2Int gridPos = currentCell.ChunkGridPos;
            Vector2Int positionToCheck = gridPos + gridDirection.Vector;
            bool isCellOnPositionExistingInGrid = positionToCheck.x >= 0
                                                  && positionToCheck.y >= 0
                                                  && positionToCheck.x < grid.Cells.GetLength(0)
                                                  && positionToCheck.y < grid.Cells.GetLength(1);

            if (isCellOnPositionExistingInGrid)
            {
                return grid.Cells[positionToCheck.x, positionToCheck.y];
            }

            return null;
        }
    }
}
