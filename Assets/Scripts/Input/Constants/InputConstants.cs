namespace Assets.Scripts.Input.Constants
{
    public static class InputConstants
    {
        public const string PLAYER_MAP_NAME = "Player";
        public const string GLOBAL_MAP_NAME = "Global";
        public const string UI_MAP_NAME = "UI";
        public const string UPGRADE_MAP_NAME = "Upgrade";
        public const string DEV_CONSOLE_MAP_NAME = "DevConsole";

        public const string PLAYER_MOVE_ACTION = "Player/Move";
        public const string PLAYER_BRAKE_ACTION = "Player/Brake";
        public const string PLAYER_REVERSE_ACTION = "Player/Reverse";
        public const string PLAYER_INTERACT_ACTION = "Player/Interact";
        public const string GLOBAL_PAUSE_ACTION = "Global/Pause";
        public const string GLOBAL_TOGGLE_CONSOLE_ACTION = "Global/ToggleConsole";
        public const string UI_NAVIGATE_ACTION = "UI/Navigate";
        public const string UI_SUBMIT_ACTION = "UI/Submit";
        public const string UI_CANCEL_ACTION = "UI/Cancel";
        public const string UI_POINT_ACTION = "UI/Point";
        public const string UI_CLICK_ACTION = "UI/Click";
        public const string UI_SCROLL_WHEEL_ACTION = "UI/ScrollWheel";
        public const string UPGRADE_SELECT_CHOICE_1_ACTION = "Upgrade/SelectChoice1";
        public const string UPGRADE_SELECT_CHOICE_2_ACTION = "Upgrade/SelectChoice2";
        public const string UPGRADE_SELECT_CHOICE_3_ACTION = "Upgrade/SelectChoice3";
        public const string DEV_CONSOLE_CLOSE_ACTION = "DevConsole/Close";
        public const string DEV_CONSOLE_SUBMIT_ACTION = "DevConsole/Submit";
        public const string DEV_CONSOLE_HISTORY_PREVIOUS_ACTION = "DevConsole/HistoryPrevious";
        public const string DEV_CONSOLE_HISTORY_NEXT_ACTION = "DevConsole/HistoryNext";

        public const string KEYBOARD_MOUSE_GROUP = "Keyboard&Mouse";
        public const string GAMEPAD_GROUP = "Gamepad";

        public const string PART_UP = "up";
        public const string PART_DOWN = "down";
        public const string PART_LEFT = "left";
        public const string PART_RIGHT = "right";

        public const string UNBOUND_LABEL = "Unbound";
        public const string UNAVAILABLE_LABEL = "Unavailable";
        public const string DISCONNECTED_LABEL = "No Controller";

        public const string EXPECTED_VECTOR2_CONTROL_TYPE = "Vector2";
        public const string EXPECTED_AXIS_CONTROL_TYPE = "Axis";

        public const string GAMEPAD_BUTTON_SOUTH_PATH = "<Gamepad>/buttonSouth";
        public const string GAMEPAD_BUTTON_EAST_PATH = "<Gamepad>/buttonEast";
        public const string GAMEPAD_BUTTON_NORTH_PATH = "<Gamepad>/buttonNorth";
        public const string GAMEPAD_BUTTON_WEST_PATH = "<Gamepad>/buttonWest";

        public const int MAX_UPGRADE_SLOTS = 3;
        public const int INITIAL_COMMAND_BUFFER_CAPACITY = 16;
    }
}
