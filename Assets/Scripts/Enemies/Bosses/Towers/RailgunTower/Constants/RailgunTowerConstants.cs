namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Constants
{
    public static class RailgunTowerConstants
    {
        public const int ORB_POOL_CAPACITY = 8;
        public const int ORB_POOL_MAX_SIZE = 16;
        public const int WAVE_POOL_CAPACITY = 8;
        public const int WAVE_POOL_MAX_SIZE = 16;

        public const float DEFAULT_PLAYER_MAX_FORWARD_SPEED = 16f;
        public const float FALLBACK_MUZZLE_HEIGHT = 6.75f;
        public const float DAMAGE_NUMBER_Y_OFFSET = 3.5f;
        public const float FOOTPRINT_CELL_PADDING = 0.25f;
        public const float BEAM_HITBOX_HEIGHT = 2.5f;
        public const float BEAM_GROUND_Y_OFFSET = 0.1f;
        public const float WAVE_FRONT_DEPTH = 1.2f;
        public const int ARC_LASER_SEGMENTS = 32;
        public const int MIN_ARC_LASER_SEGMENTS = 4;
        public const float ARC_LASER_MIN_RADIUS = 0.25f;
        public const float ARC_LASER_MUZZLE_HALF_WIDTH = 0.3f;
        public const float ARC_LASER_BAND_VISUAL_HALF_DEPTH = 0.65f;
        public const float ARC_LASER_HITBOX_TOLERANCE_SQR = 0.0025f;
        public const float CHASE_CENTER_HOLD_DISTANCE = 0.35f;
        public const float CHASE_CENTER_HOLD_DISTANCE_SQR = CHASE_CENTER_HOLD_DISTANCE * CHASE_CENTER_HOLD_DISTANCE;
        public const float SWEEP_PITCH_MIN_HORIZ_DISTANCE = 4.5f;
        public const float BARREL_PITCH_RESET_DURATION = 0.35f;
        public const float CROSS_BURST_PRE_AIM_DEG_PER_SEC = 240f;
        public const float CROSS_BURST_MIN_PRE_AIM_DURATION = 0.12f;
        public const float CROSS_BURST_MAX_PRE_AIM_DURATION = 0.35f;
        public const float CROSS_BURST_BARREL_PITCH_DEGREES = 10f;
        public const float CROSS_BURST_BARREL_KICK_DEGREES = 4.5f;
        public const float CROSS_BURST_HOUSING_KICK_DISTANCE = 0.16f;
        public const float CROSS_BURST_MAX_RECOIL_DURATION = 0.17f;
        public const int CROSS_BURST_TARGET_SAMPLE_ATTEMPTS = 48;
        public const int CROSS_BURST_BEST_CANDIDATE_ATTEMPTS = 32;
        public const float CROSS_BURST_ZONE_CENTER_EDGE_PULL_FACTOR = 0.45f;
        public const float ORB_RELEASE_DELAY_MIN = 0.5f;
        public const float ORB_RELEASE_DELAY_MAX = 1.5f;
        public const float ARENA_EDGE_SAFETY_MARGIN = 2f;

        public const int OVERLAP_BUFFER_SIZE = 16;
        public const int ATTACK_COUNT = 3;

        public const string DEFAULT_ARENA_DISPLAY_NAME = "RAILGUN TOWER";

        public const string SFX_RAILGUN_CHARGE = "RailgunCharge";
        public const string SFX_RAILGUN_SWEEP = "RailgunSweep";
        public const string SFX_RAILGUN_CHASE_BEAM = "RailgunChaseBeam";
        public const string SFX_RAILGUN_ORB_FIRE = "RailgunOrbFire";
        public const string SFX_RAILGUN_ORB_IMPACT = "RailgunOrbImpact";
        public const string SFX_RAILGUN_WAVE_BURST = "RailgunWaveBurst";
        public const string SFX_ENRAGE_ROAR = "RailgunEnrage";
    }
}
