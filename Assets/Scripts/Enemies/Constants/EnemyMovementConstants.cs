namespace Assets.Scripts.Enemies.Constants
{
    public static class EnemyMovementConstants
    {
        public const float GROUND_CHECK_ORIGIN_Y = 1.0f;
        public const float GROUND_CHECK_SPHERE_RADIUS = 0.3f;
        public const float GROUND_CHECK_DISTANCE = 3.0f;
        public const float GROUND_SNAP_LERP_SPEED = 20.0f;
        public const float FALL_GRAVITY = 25.0f;
        public const float FALL_DEATH_Y_THRESHOLD = -10.0f;
        public const float MOVING_TO_POSITION_ACCURACY = 0.02f;
        public const float OBSTACLE_CHECK_RADIUS = 0.4f;
        public const float OBSTACLE_SAFETY_BUFFER = 0.1f;
    }
}
