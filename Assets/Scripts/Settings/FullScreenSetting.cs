using Assets.Scripts.Settings.Constants;
using Assets.Scripts.Storage;
using UnityEngine;

namespace Assets.Scripts.Settings
{
    public class FullScreenSetting : ISetting<FullScreenSetting, FullScreenMode>
    {
        public FullScreenMode DefaultValue => FullScreenMode.MaximizedWindow;

        public string GetKey()
        {
            return SettingsConstants.FULL_SCREEN_MODE_KEY;
        }

        public FullScreenMode GetValueOrStoredDefault()
        {
            if (AppStorage.TryGetValue(GetKey(), out FullScreenMode storedMode))
            {
                return storedMode;
            }

            return DefaultValue;
        }

        public void SaveValue(FullScreenMode value)
        {
            AppStorage.SetValue(GetKey(), value);
        }

        public void Load()
        {
            FullScreenMode mode = GetValueOrStoredDefault();
            Screen.fullScreenMode = mode;
        }
    }
}

