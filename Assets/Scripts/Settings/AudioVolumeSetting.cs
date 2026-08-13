using Assets.Scripts.Audio;
using Assets.Scripts.Settings.Constants;
using Assets.Scripts.Storage;

namespace Assets.Scripts.Settings
{
    public class AudioVolumeSetting : ISetting<AudioVolumeSetting, float>
    {
        private readonly IAudioMixersManager _audioMixersManager;

        public float DefaultValue => SettingsConstants.DEFAULT_AUDIO_VOLUME;

        public AudioVolumeSetting(IAudioMixersManager audioMixersManager)
        {
            _audioMixersManager = audioMixersManager;
        }

        public string GetKey()
        {
            return SettingsConstants.AUDIO_VOLUME_KEY;
        }

        public float GetValueOrStoredDefault()
        {
            if (AppStorage.TryGetValue(GetKey(), out float value))
            {
                return value;
            }

            return DefaultValue;
        }

        public void SaveValue(float value)
        {
            AppStorage.SetValue(GetKey(), value);
        }

        public void Load()
        {
            _audioMixersManager.SetMixerVolume(volume: GetValueOrStoredDefault());
        }
    }
}

