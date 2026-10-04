using UnityEngine;

namespace Assets.Scripts.Navigation.Constants
{
    public static class GridConstants
    {
        public const int DEFAULT_FIELD_COST = 1;
        public const int OCCUPANCY_BUFFER_SIZE = 32;
        public const float GROUND_PROBE_ORIGIN_HEIGHT = 1.5f;
        public const float GROUND_PROBE_DISTANCE = 3.5f;
        public const float ROOT_GROUND_CLEARANCE = 0.02f;
        public const float MIN_SUPPORT_NORMAL_Y = 0.01f;
#if DEBUG
        internal const float PLAYER_CHUNK_DRAW_Y_OFFSET = 0.2f;
        internal const float DRAW_TIME_OFFSET = 0.02f;
#endif
        public static readonly Vector2Int INVALID_CHUNK_GRID_POS = new Vector2Int(-1, -1);
    }
}
