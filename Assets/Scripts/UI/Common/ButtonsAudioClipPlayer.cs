using Assets.Scripts.Audio;
using UnityEngine;

namespace Assets.Scripts.UI.Common
{
    [RequireComponent(typeof(AudioClipPlayer))]
    public class ButtonsAudioClipPlayer : MonoBehaviour
    {
        private AudioClipPlayer _audioClipPlayer;

        public void PlayOnClickSound()
        {
            _audioClipPlayer.Play("Click");
        }

        public void PlayOnHoverSound()
        {
            _audioClipPlayer.Play("Click");
        }
    }
}
