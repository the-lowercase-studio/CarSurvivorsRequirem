namespace Assets.Scripts.Player.Constants
{
    public static class CarControlConstants
    {
        public const float KEYBOARD_THROTTLE_DEADZONE = 0.05f;
        public const float KEYBOARD_DRIFT_START_THROTTLE_MIN = 0.1f;
        public const float KEYBOARD_REVERSE_STEER_THRESHOLD = -0.1f;
        public const float KEYBOARD_REVERSE_SPEED_THRESHOLD = -0.5f;
        public const float GAMEPAD_STEER_DIRECTION_THRESHOLD = 0.05f;
        public const float DRIFT_STEER_INIT_THRESHOLD = 0.2f;
        public const float DRIFT_STEER_EXIT_THRESHOLD = 0.1f;
        public const float CAMERA_AXIS_EPSILON_SQR = 0.0001f;
        public const float FULL_ROTATION_TIEBREAKER_DOT = -0.999f;
        public const float LATERAL_GRIP_LERP_SPEED = 4.0f;
        public const float TURN_MULTIPLIER_LERP_SPEED = 6.0f;
    }
}
