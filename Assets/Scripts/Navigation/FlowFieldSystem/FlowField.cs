using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.LayerMasks;
using System.Collections.Generic;
using UnityEngine;
using NavigationGrid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Navigation.FlowFieldSystem
{
    public class FlowField
    {
        private const int IMPASSABLE_COST = 255;
        private const int ROUGH_TERRAIN_COST = 3;
        private const int DEFAULT_FIELD_COST = 1;

        public void CreateCostField(NavigationGrid grid)
        {
            for (int i = 0; i < grid.Cells.GetLength(0); i++)
            {
                for (int j = 0; j < grid.Cells.GetLength(1); j++)
                {
                    Cell cell = grid.Cells[i, j];
                    float edgesOffset = -0.05f;
                    Vector3 halfExtents = Vector3.one * (grid.CellSize / 2 + edgesOffset);

                    Collider[] obstacles = Physics.OverlapBox(
                        cell.WorldPos,
                        halfExtents,
                        Quaternion.identity,
                        TerrainLayers.All);

                    cell.ResetCosts();

                    int maxCost = 0;
                    if (obstacles.Length > 0)
                    {
                        foreach (Collider obstacle in obstacles)
                        {
                            int layerValue = 1 << obstacle.gameObject.layer;
                            if (maxCost < IMPASSABLE_COST
                                && (layerValue & TerrainLayers.Impassable.value) == TerrainLayers.Impassable.value)
                            {
                                maxCost = IMPASSABLE_COST;
                            }
                            else if (maxCost < ROUGH_TERRAIN_COST
                                && (layerValue & TerrainLayers.Rough.value) == TerrainLayers.Rough.value)
                            {
                                maxCost = ROUGH_TERRAIN_COST;
                            }
                        }

                        if (maxCost > DEFAULT_FIELD_COST)
                        {
                            cell.IncreaseCost(maxCost);
                        }
                    }
                    else
                    {
                        cell.IncreaseCost(IMPASSABLE_COST);
                    }
                }
            }
        }

        public void CreateIntegrationField(NavigationGrid grid, Cell destinationCell)
        {
            destinationCell.Cost = 0;
            destinationCell.BestCost = 0;

            Queue<Cell> cellsToCheck = new Queue<Cell>();
            cellsToCheck.Enqueue(destinationCell);
            while (cellsToCheck.Count > 0)
            {
                Cell currentCell = cellsToCheck.Dequeue();
                List<Cell> currentNeighbours = GetNeighbourCells(grid, currentCell, GridDirection.CardinalDirections);
                foreach (Cell currentNeighbour in currentNeighbours)
                {
                    if (currentNeighbour.Cost + currentCell.BestCost < currentNeighbour.BestCost)
                    {
                        currentNeighbour.BestCost = (ushort)(currentNeighbour.Cost + currentCell.BestCost);
                        cellsToCheck.Enqueue(currentNeighbour);
                    }
                }
            }
        }

        public void CreateFlowField(NavigationGrid grid)
        {
            foreach (Cell currentCell in grid.Cells)
            {
                List<Cell> currentNeighbours = GetNeighbourCells(grid, currentCell, GridDirection.AllDirections);
                Cell bestCostCell = currentCell;

                foreach (Cell currentNeighbour in currentNeighbours)
                {
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

        private List<Cell> GetNeighbourCells(NavigationGrid grid, Cell currentCell, IEnumerable<GridDirection> gridDirections)
        {
            List<Cell> cells = new List<Cell>();
            Vector2Int gridPos = currentCell.ChunkGridPos;
            foreach (GridDirection gridDirection in gridDirections)
            {
                Vector2Int positionToCheck = gridPos + gridDirection.Vector;
                bool isCellOnPositionExistingInGrid = positionToCheck.x >= 0
                                                      && positionToCheck.y >= 0
                                                      && positionToCheck.x < grid.Cells.GetLength(0)
                                                      && positionToCheck.y < grid.Cells.GetLength(1);

                if (isCellOnPositionExistingInGrid)
                {
                    cells.Add(grid.Cells[positionToCheck.x, positionToCheck.y]);
                }
            }
            return cells;
        }
    }
}
