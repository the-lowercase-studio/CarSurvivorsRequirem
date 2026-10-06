namespace Assets.Scripts.Skills.PlayerSkills.Minigun.Constants
{
    public static class MinigunConstants
    {
        public const float QUERY_DEPTH = 0.001f;
        public const int FAST_QUERY_CAPACITY = 64;
        public const int OVERFLOW_QUERY_CAPACITY = 2048;
        public const int MAX_TARGETS = 17;

        // Tracer & Impact Presentation Constants
        public const float DEFAULT_TRACER_SPEED = 70f;
        public const float MAX_TRACER_LIFETIME = 0.5f;
        public const float MIN_TRACER_SPAWN_DISTANCE = 0.2f;
        public const int DEFAULT_IMPACT_SPARK_COUNT = 6;
        public const int MAX_PENDING_HITS = 64;
    }
}
