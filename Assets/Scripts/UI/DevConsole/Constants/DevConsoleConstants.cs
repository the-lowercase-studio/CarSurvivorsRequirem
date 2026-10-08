namespace Assets.Scripts.UI.DevConsole.Constants
{
    public static class DevConsoleConstants
    {
        public const int DEFAULT_MAX_LOG_LINES = 100;
        public const int DEFAULT_MAX_HISTORY_COUNT = 50;

        public const float EXP_BILLION_MULTIPLIER = 1_000_000_000f;
        public const float EXP_MILLION_MULTIPLIER = 1_000_000f;
        public const float EXP_THOUSAND_MULTIPLIER = 1_000f;

        public const float DEFAULT_SPAWN_FORWARD_DISTANCE = 8f;
        public const float DEFAULT_SPAWN_LATERAL_SPACING = 2f;
        public const int DEFAULT_SPAWN_COUNT = 1;
        public const int MAX_SPAWN_COUNT = 50;

        public const string AUTO_FLAG_LONG = "--auto";
        public const string AUTO_FLAG_SHORT = "-a";

        public const string COLOR_WARNING_HEX = "#FFCC00";
        public const string COLOR_ERROR_HEX = "#FF4444";
        public const string PROMPT_PREFIX = "> ";
    }
}
