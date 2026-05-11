using System.Linq;

namespace Assets.Scripts.Navigation.GridSystem
{
    public static class RandomWalkableCellsFinder
    {
        public static Cell FindCellWithoutCollectible(Grid grid)
        {
            Cell[] notOccupiedCells = grid
                .Cells
                .Cast<Cell>()
                .Where(c => !c.IsOccupiedByCollectible && CellStatusDescriber.IsWalkable(c))
                .ToArray();

            if (notOccupiedCells.Length == 0)
            {
                return null;
            }

            int randomIndex = UnityEngine.Random.Range(0, notOccupiedCells.Length);

            return notOccupiedCells[randomIndex];
        }
    }
}
