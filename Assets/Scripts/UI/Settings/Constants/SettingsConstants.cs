namespace Assets.Scripts.UI.Settings.Constants
{
    /// <summary>
    /// Tuning constants for settings UI controls.
    /// </summary>
    public static class SettingsConstants
    {
        public const float VOLUME_MIN_PERCENT = 0f;
        public const float VOLUME_MAX_PERCENT = 100f;
        public const float VOLUME_MIN_LINEAR = 0.0001f;
        public const float VOLUME_DECIBEL_SCALE = 20f;
        public const float VOLUME_NAVIGATE_THRESHOLD = 0.5f;
        public const float VOLUME_HOLD_DELAY_SECONDS = 0.35f;
        public const float VOLUME_FAST_HOLD_SECONDS = 0.90f;
        public const float VOLUME_TURBO_HOLD_SECONDS = 1.60f;
        public const float VOLUME_SLOW_TICK_INTERVAL = 0.075f;
        public const float VOLUME_FAST_TICK_INTERVAL = 0.055f;
        public const float VOLUME_TURBO_TICK_INTERVAL = 0.045f;
        public const int VOLUME_SLOW_STEP = 1;
        public const int VOLUME_FAST_STEP = 2;
        public const int VOLUME_TURBO_STEP = 4;
    }
}
