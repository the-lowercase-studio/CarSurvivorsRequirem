namespace Assets.Scripts.Player.Constants
{
    public static class CarVfxConstants
    {
        public const float SPEED_CHECK_FOR_TRAIL_DELAY = 0.1f;
        public const string CAR_STOP_LIGHTS_MAT_NAME = "CarStopLights";
        public const float DRIFT_GROUND_PROBE_HEIGHT = 0.15f;
        public const float DRIFT_GROUND_PROBE_DISTANCE = 0.30f;
        public const float DRIFT_GROUND_SURFACE_OFFSET = 0.01f;
        public const int DRIFT_SEGMENTS_PER_EMITTER = 8;
        public const float DRIFT_CONTINUITY_SAMPLE_SPACING = 0.10f;
        public const int DRIFT_MAX_CONTINUITY_SAMPLES = 8;
        public const float DRIFT_NATIVE_SAMPLE_SUPPRESSION_DISTANCE = 1000000f;
    }
}
