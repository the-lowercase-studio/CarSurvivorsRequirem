using Assets.Scripts.Audio;
using Assets.Scripts.Audio.Constants;
using UnityEngine;

namespace Assets.Scripts.UI.Common
{
    [RequireComponent(typeof(AudioClipPlayer))]
    public class ButtonsAudioClipPlayer : MonoBehaviour
    {
        private AudioClipPlayer _audioClipPlayer;

        private void Awake()
        {
            _audioClipPlayer = GetComponent<AudioClipPlayer>();
        }

        public void PlayOnClickSound()
        {
            _audioClipPlayer.Play(AudioConstants.BUTTON_CLICK_CLIP_NAME);
        }

        public void PlayOnHoverSound()
        {
            _audioClipPlayer.Play(AudioConstants.BUTTON_CLICK_CLIP_NAME);
        }
    }
}

