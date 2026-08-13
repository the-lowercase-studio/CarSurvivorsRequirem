using System.Collections.Generic;
using Assets.Scripts.Settings.Constants;
using Assets.Scripts.Storage;
using UnityEngine;

namespace Assets.Scripts.Settings.Resolution
{
    public class ResolutionSetting : ISetting<ResolutionSetting, SerializableResolution>
    {
        public SerializableResolution DefaultValue
        {
            get
            {
                IEnumerable<SerializableResolution> available = ScreenSerializableResolutionHelper.GetAvailableResolutions();
                foreach (SerializableResolution res in available)
                {
                    return res;
                }
                return default;
            }
        }

        private readonly ISetting<FullScreenSetting, FullScreenMode> _fullScreenSetting;

        public ResolutionSetting(ISetting<FullScreenSetting, FullScreenMode> fullScreenSetting)
        {
            _fullScreenSetting = fullScreenSetting;
        }

        public string GetKey()
        {
            return SettingsConstants.RESOLUTION_KEY;
        }

        public SerializableResolution GetValueOrStoredDefault()
        {
            if (AppStorage.TryGetValue(GetKey(), out SerializableResolution storedResolution))
            {
                return storedResolution;
            }

            return DefaultValue;
        }

        public void SaveValue(SerializableResolution value)
        {
            AppStorage.SetValue(GetKey(), value);
        }

        public void Load()
        {
            SerializableResolution storedValue = GetValueOrStoredDefault();

            SerializableResolution resolution = default;
            IEnumerable<SerializableResolution> available = ScreenSerializableResolutionHelper.GetAvailableResolutions();
            foreach (SerializableResolution r in available)
            {
                if (r.Equals(storedValue))
                {
                    resolution = r;
                    break;
                }
            }

            if (resolution.Equals(default(SerializableResolution)))
            {
                resolution = DefaultValue;
            }

            ScreenSerializableResolutionHelper.SetResolution(
                resolution,
                _fullScreenSetting.GetValueOrStoredDefault()
            );
        }
    }
}

