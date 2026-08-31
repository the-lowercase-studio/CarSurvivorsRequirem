using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Audio
{
    public interface IAudioClipPlayer
    {
        public void Play(string name);

        public void PlayOneShot(string name);

        public event EventHandler OnAudioClipFinished;
    }

    [RequireComponent(typeof(AudioSource))]
    public class AudioClipPlayer : MonoBehaviour, IAudioClipPlayer
    {
        [Serializable]
        public class AudioClipPlayerConfig
        {
            [SerializeField] private string _name;

            [SerializeField] private AudioClipConfig[] _clipVariants;

            public string Name
            {
                get
                {
                    return _name;
                }
            }

            public AudioClipConfig[] ClipVariants
            {
                get
                {
                    return _clipVariants;
                }
            }
        }

        [SerializeField] private AudioClipPlayerConfig[] _audioClipPlayerConfigs;

        public event EventHandler OnAudioClipFinished;

        private AudioSource _audioSource;
        private readonly Dictionary<string, AudioClipPlayerConfig> _configsByName = new();

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            CacheConfigsByName();
        }

        public void Play(string name)
        {
            AudioClipConfig clipConfigVariant = GetRandomAudioClipVariantFromConfigByName(name);

            if (clipConfigVariant is null)
            {
                return;
            }

            PrepareAudioSourceToPlayClip(clipConfigVariant);

            if (IsInvoking(nameof(OnAudioClipPlayFinished)))
            {
                CancelInvoke(nameof(OnAudioClipPlayFinished));
            }

            _audioSource.Play();

            Invoke(nameof(OnAudioClipPlayFinished), _audioSource.clip.length);
        }

        public void PlayOneShot(string name)
        {
            AudioClipConfig clipConfigVariant = GetRandomAudioClipVariantFromConfigByName(name);

            if (clipConfigVariant is null)
            {
                return;
            }

            PrepareAudioSourceToPlayClip(clipConfigVariant);

            _audioSource.PlayOneShot(_audioSource.clip);

            Invoke(nameof(OnAudioClipPlayFinished), _audioSource.clip.length);
        }

        private AudioClipConfig GetRandomAudioClipVariantFromConfigByName(string name)
        {
            if (!_configsByName.TryGetValue(name, out AudioClipPlayerConfig config)
                || config.ClipVariants.Length == 0)
            {
                Debug.LogError($"AudioClipPlayer: No audio clip found for name '{name}'");
                return null;
            }

            return config.ClipVariants[UnityEngine.Random.Range(0, config.ClipVariants.Length)];
        }

        private void CacheConfigsByName()
        {
            if (_audioClipPlayerConfigs == null)
            {
                return;
            }

            foreach (AudioClipPlayerConfig config in _audioClipPlayerConfigs)
            {
                if (config is null)
                {
                    Debug.LogError($"[{nameof(AudioClipPlayer)}] Null config entry found in configs array on '{name}'.");
                    continue;
                }

                if (string.IsNullOrEmpty(config.Name))
                {
                    Debug.LogError($"[{nameof(AudioClipPlayer)}] Config entry has empty name on '{name}'.");
                    continue;
                }

                if (_configsByName.ContainsKey(config.Name))
                {
                    Debug.LogError($"[{nameof(AudioClipPlayer)}] Duplicate config entry for '{config.Name}' on '{name}'.");
                    continue;
                }

                _configsByName.Add(config.Name, config);
            }
        }

        private void OnAudioClipPlayFinished()
        {
            OnAudioClipFinished?.Invoke(this, EventArgs.Empty);
        }

        private void PrepareAudioSourceToPlayClip(AudioClipConfig config)
        {
            _audioSource.clip = config.AudioClip;
            _audioSource.volume = config.Volume;
            _audioSource.pitch = config.Pitch;
            _audioSource.loop = config.Loop;
        }
    }
}
