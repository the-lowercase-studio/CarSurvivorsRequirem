namespace Assets.Scripts.Navigation.GridSystem
{
    public static class CellStatusDescriber
    {
        public static bool IsWalkable(Cell cell) =>
            cell.Cost < byte.MaxValue;
    }
}
