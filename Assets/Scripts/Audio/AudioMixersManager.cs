using Assets.Scripts.Audio.Constants;
using UnityEngine;
using UnityEngine.Audio;

namespace Assets.Scripts.Audio
{
    public interface IAudioMixersManager
    {
        public void SetMixerVolume(string mixerName = AudioConstants.DEFAULT_MIXER_NAME, float volume = AudioConstants.DEFAULT_VOLUME);
    }

    public class AudioMixersManager : MonoBehaviour, IAudioMixersManager
    {
        [SerializeField] private AudioMixer _mainAudioMixer;

        public void SetMixerVolume(string mixerName = AudioConstants.DEFAULT_MIXER_NAME, float volume = AudioConstants.DEFAULT_VOLUME)
        {
            _mainAudioMixer.SetFloat(AudioConstants.VOLUME_PARAMETER_NAME, volume);
        }
    }
}

