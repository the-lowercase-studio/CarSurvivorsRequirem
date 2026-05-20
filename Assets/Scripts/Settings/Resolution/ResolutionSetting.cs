using Assets.Scripts.Storage;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Settings.Resolution
{
    public class ResolutionSetting : ISetting<ResolutionSetting, SerializableResolution>
    {
        public SerializableResolution DefaultValue => ScreenSerializableResolutionHelper
            .GetAvailableResolutions()
            .FirstOrDefault();

        private readonly ISetting<FullScreenSetting, FullScreenMode> _fullScreenSetting;

        public ResolutionSetting(ISetting<FullScreenSetting, FullScreenMode> fullScreenSetting)
        {
            _fullScreenSetting = fullScreenSetting;
        }

        public string GetKey()
        {
            return "Resolution";
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
            var storedValue = GetValueOrStoredDefault();

            SerializableResolution resolution = ScreenSerializableResolutionHelper
                .GetAvailableResolutions()
                .FirstOrDefault(r => r.Equals(storedValue));

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
