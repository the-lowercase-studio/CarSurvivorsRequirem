using UnityEngine;

namespace Assets.Scripts.Navigation.Constants
{
    public static class GridConstants
    {
        public const int DEFAULT_FIELD_COST = 1;
        public const int OCCUPANCY_BUFFER_SIZE = 32;
        public static readonly Vector2Int INVALID_CHUNK_GRID_POS = new Vector2Int(-1, -1);
    }
}
