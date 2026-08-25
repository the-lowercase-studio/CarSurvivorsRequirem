namespace Assets.Scripts.Enemies.Bosses.Golem.Constants
{
    public static class GolemBossConstants
    {
        public const string SLAM_SFX_KEY = "GolemSlam";
        public const string ROCKET_SFX_KEY = "GolemRocket";
        public const string ROAR_SFX_KEY = "GolemRoar";
        public const string DEATH_SFX_KEY = "GolemDeath";
        public const string STOMP_SFX_KEY = "GolemStomp";

        public const string EMISSION_COLOR_PROPERTY = "_EmissionColor";
        public const string BASE_COLOR_PROPERTY = "_BaseColor";

        public const string ANIM_PARAM_IS_MOVING = "IsMoving";
        public const string ANIM_PARAM_SPEED = "Speed";
        public const string ANIM_TRIGGER_LEAP_SLAM = "LeapSlam";
        public const string ANIM_TRIGGER_LEAP_TAKEOFF = "LeapTakeoff";
        public const string ANIM_TRIGGER_LEAP_LAND = "LeapLand";
        public const string ANIM_TRIGGER_STOMP = "Stomp";
        public const string ANIM_TRIGGER_LINEAR_FIST = "LinearFist";
        public const string ANIM_TRIGGER_SKY_BARRAGE = "SkyBarrage";

        public const string ANIM_STATE_WALKING = "Walking";

        public const float INITIAL_LINEAR_FIST_COOLDOWN = 2.5f;
        public const float INITIAL_SKY_BARRAGE_COOLDOWN = 5.5f;
        public const float INITIAL_LEAP_SLAM_COOLDOWN = 8.0f;
        public const float INITIAL_STOMP_COOLDOWN = 1.0f;

        public const float STOMP_IMPACT_DELAY = 0.7f;
        public const float STOMP_TOTAL_DURATION = 1.25f;
        public const float SKY_BARRAGE_LAUNCH_DURATION = 1.6f;

        public const float DEFAULT_LEAP_TAKEOFF_DURATION = 0.57f;
        public const float DEFAULT_LEAP_LANDING_DURATION = 1.27f;
        public const float DEFAULT_LINEAR_FIST_RELEASE_DELAY = 1.2f;
        public const float DEFAULT_SKY_BARRAGE_RELEASE_DELAY = 1.5f;
    }
}
