using System;
using UnityEngine;

namespace Assets.Scripts.Audio
{
    [Serializable]
    public class AudioClipConfig
    {
        [SerializeField] private AudioClip _audioClip;

        [SerializeField] private float _volume = 0.5f;

        [SerializeField] private float _pitch = 1f;

        [SerializeField] private bool _loop = true;

        public AudioClip AudioClip => _audioClip;
        public float Volume => _volume;
        public float Pitch => _pitch;
        public bool Loop => _loop;
    }
}
