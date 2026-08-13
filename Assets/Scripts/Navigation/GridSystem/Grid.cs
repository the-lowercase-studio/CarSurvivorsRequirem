using System;
using UnityEngine;

namespace Assets.Scripts.Navigation.GridSystem
{
    [Serializable]
    public class GridConfiguration
    {
        [field: SerializeField] public int Width { get; private set; }
        [field: SerializeField] public int Height { get; private set; }
        [field: SerializeField] public float CellSize { get; private set; }

        public GridConfiguration(int width, int height, float cellSize)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
        }
    }

    public class Grid
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public float CellSize { get; private set; }
        public Cell[,] Cells { get; private set; }

        public Grid(GridConfiguration gridConfiguration)
        {
            Width = gridConfiguration.Width;
            Height = gridConfiguration.Height;
            CellSize = gridConfiguration.CellSize;
            Cells = CreateCells(Width, Height);
        }

        public Grid(GridConfiguration gridConfiguration, Cell[,] existingCells)
        {
            Width = gridConfiguration.Width;
            Height = gridConfiguration.Height;
            CellSize = gridConfiguration.CellSize;
            Cells = existingCells;
        }

        private Cell[,] CreateCells(int width, int height)
        {
            Cell[,] cells = new Cell[width, height];
            Vector3 currentTilePos = new Vector3(CellSize * 0.5f, 0, CellSize * 0.5f);
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    Vector2Int gridPos = new Vector2Int(i, j);
                    cells[i, j] = new Cell(currentTilePos, gridPos, gridPos);
                    currentTilePos.z += CellSize;
                }
                currentTilePos = new Vector3(currentTilePos.x + CellSize, 0, CellSize * 0.5f);
            }

            return cells;
        }
    }
}
