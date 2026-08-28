namespace Assets.Scripts.Navigation.Constants
{
    public static class FlowFieldConstants
    {
        public const int IMPASSABLE_COST = 255;
        public const int ROUGH_TERRAIN_COST = 3;
        public const int DEFAULT_FIELD_COST = 1;
        public const int TERRAIN_COLLIDER_BUFFER_SIZE = 16;
        public const int SEPARATION_COLLIDER_BUFFER_SIZE = 32;
        public const float EDGES_OFFSET = 0.0f;
        public const float QUERY_BOX_VERTICAL_HALF_EXTENT = 1.0f;
        public const float DESTINATION_ARRIVAL_DISTANCE = 0.35f;
        public const float DESTINATION_ARRIVAL_DISTANCE_SQR = 0.1225f;
        public const float MIN_MOVEMENT_SPEED_THRESHOLD = 0.05f;
    }
}
