namespace Assets.Scripts.Navigation.Constants
{
    public static class FlowFieldConstants
    {
        public const int IMPASSABLE_COST = 255;
        public const int ROUGH_TERRAIN_COST = 3;
        public const int DEFAULT_FIELD_COST = 1;
        public const int TERRAIN_COLLIDER_BUFFER_SIZE = 16;
        public const int SEPARATION_COLLIDER_BUFFER_SIZE = 32;
        public const float EDGES_OFFSET = -0.05f;
    }
}
