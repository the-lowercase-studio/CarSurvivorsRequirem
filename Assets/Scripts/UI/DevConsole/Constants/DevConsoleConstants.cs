using Assets.Scripts.GameFlow.Constants;

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

        public const string LOAD_TIER_COMMAND_NAME = "loadtier";
        public const string LOAD_TIER_COMMAND_SYNTAX = "loadtier <tier> [--reset]";
        public const string LOAD_TIER_COMMAND_DESCRIPTION = "Loads the specified map tier (e.g. 1 or 2), preserving run progress by default or resetting with --reset (-r).";
        public const string RESET_FLAG_LONG = "--reset";
        public const string RESET_FLAG_SHORT = "-r";
        public const int MIN_TIER_NUMBER = 1;

        public const string POWERUP_COMMAND_NAME = "powerup";
        public const string POWERUP_COMMAND_SYNTAX = "powerup <nuke|freeze|shield|armor|firetails|crate>";
        public const string POWERUP_COMMAND_DESCRIPTION = "Spawns a collectible power-up or skill crate ahead of the player.";
        public const float POWERUP_SPAWN_Y_OFFSET = 0.85f;

        public const string GOD_MODE_COMMAND_NAME = "godmode";
        public const string GOD_MODE_COMMAND_SYNTAX = "godmode [on|off]";
        public const string GOD_MODE_COMMAND_DESCRIPTION = "Toggles or sets player death protection (minimum 1 HP on damage; DeathVolume still kills) for the current run.";
        public const string GOD_MODE_ON_ARG = "on";
        public const string GOD_MODE_OFF_ARG = "off";
        public const string GOD_MODE_TRUE_ARG = "true";
        public const string GOD_MODE_FALSE_ARG = "false";
        public const string GOD_MODE_ONE_ARG = "1";
        public const string GOD_MODE_ZERO_ARG = "0";

        public const string TIMESCALE_COMMAND_NAME = "timescale";
        public const string TIMESCALE_COMMAND_SYNTAX = "timescale [multiplier]";
        public const string TIMESCALE_COMMAND_DESCRIPTION = "Gets or sets the global game time scale multiplier (0.05 to 100, use 'timescale 1' to reset).";
        public const float TIMESCALE_MIN_MULTIPLIER = GameTimeConstants.MIN_TIME_SCALE;
        public const float TIMESCALE_MAX_MULTIPLIER = GameTimeConstants.MAX_TIME_SCALE;
        public const string TIMESCALE_DISPLAY_FORMAT = "0.###";

        public const string NUKE_PREFAB_PATH = "Assets/Prefabs/Collectibles/Nuke.prefab";
        public const string FREEZE_TIMER_PREFAB_PATH = "Assets/Prefabs/Collectibles/FreezeTimer.prefab";
        public const string BUBBLE_SHIELD_PREFAB_PATH = "Assets/Prefabs/Collectibles/BubbleShield.prefab";
        public const string ARMOR_PREFAB_PATH = "Assets/Prefabs/Collectibles/Armor.prefab";
        public const string FIRE_TAILS_PREFAB_PATH = "Assets/Prefabs/Collectibles/FireTails.prefab";
        public const string SKILL_CRATE_PREFAB_PATH = "Assets/Prefabs/Collectibles/SkilCrate.prefab";

        public const string COLOR_WARNING_HEX = "#FFCC00";
        public const string COLOR_ERROR_HEX = "#FF4444";
        public const string PROMPT_PREFIX = "> ";
    }
}
