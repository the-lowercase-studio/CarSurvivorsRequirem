using UnityEngine;
using UnityEngine.Audio;

namespace Assets.Scripts.Audio
{
    public interface IAudioMixersManager
    {
        public void SetMixerVolume(string mixerName = "Main", float volume = 0.5f);
    }

    public class AudioMixersManager : MonoBehaviour, IAudioMixersManager
    {
        [SerializeField] private AudioMixer _mainAudioMixer;

        public void SetMixerVolume(string mixerName = "Main", float volume = 0.5f)
        {
            _mainAudioMixer.SetFloat("Volume", volume);
        }
    }
}
