namespace Assets.Scripts.Navigation.GridSystem
{
    public static class RandomWalkableCellsFinder
    {
        public static Cell FindCellWithoutCollectible(Grid grid)
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
                        && !cell.IsOccupiedByCollectible
                        && CellStatusDescriber.IsWalkable(cell))
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
    }
}
