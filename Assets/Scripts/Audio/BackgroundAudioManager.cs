using System;
using Assets.Scripts.Audio.Constants;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.GameFlow;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Audio
{
    public interface IBackgroundAudioManager
    {
        public void ChangeAudioToDeathAudioMode();

        public void ChangeAudioToDefaultAudioMode();
    }

    [RequireComponent(typeof(AudioSource))]
    public class BackgroundAudioManager : MonoBehaviour, IBackgroundAudioManager
    {
        [Serializable]
        public class AudioClipInSceneConfig
        {
            [SerializeField] private AudioClipConfig _clipConfig;

            [SerializeField] private GameScene _scene;

            public AudioClipConfig ClipConfig
            {
                get
                {
                    return _clipConfig;
                }
            }

            public GameScene Scene
            {
                get
                {
                    return _scene;
                }
            }
        }

        [Inject] private readonly IGameSceneLoader _gameSceneLoader = null;

        [SerializeField] private AudioClipInSceneConfig[] _clipConfigInScenes;
        [SerializeField] private float _deathAudioPitch = AudioConstants.DEATH_AUDIO_PITCH;

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            _gameSceneLoader.OnSceneLoaded += SceneManager_OnSceneLoaded;
        }

        private void OnDisable()
        {
            _gameSceneLoader.OnSceneLoaded -= SceneManager_OnSceneLoaded;
        }

        public void ChangeAudioToDeathAudioMode()
        {
            _audioSource.pitch = _deathAudioPitch;
        }

        public void ChangeAudioToDefaultAudioMode()
        {
            _audioSource.pitch = AudioConstants.DEFAULT_PITCH;
        }

        private void SceneManager_OnSceneLoaded(object sender, ValueEventArgs<GameScene> args)
        {
            PlayOrContinuePlayingCorrectSceneBackgroundMusic(args.Value);
        }

        private void PlayOrContinuePlayingCorrectSceneBackgroundMusic(GameScene newScene)
        {
            AudioClipConfig clipConfig = GetClipConfigForScene(newScene);

            if (clipConfig is null)
            {
                return;
            }

            _audioSource.loop = clipConfig.Loop;
            _audioSource.pitch = clipConfig.Pitch;
            _audioSource.volume = clipConfig.Volume;

            if (clipConfig.AudioClip != _audioSource.clip)
            {
                _audioSource.clip = clipConfig.AudioClip;
                _audioSource.Play();
            }
        }

        private AudioClipConfig GetClipConfigForScene(GameScene scene)
        {
            if (_clipConfigInScenes == null)
            {
                Debug.LogWarning($"[{nameof(BackgroundAudioManager)}] No scene music configurations assigned on '{name}'.");
                return null;
            }

            foreach (AudioClipInSceneConfig config in _clipConfigInScenes)
            {
                if (config != null && config.Scene == scene)
                {
                    return config.ClipConfig;
                }
            }

            Debug.LogWarning($"[{nameof(BackgroundAudioManager)}] No background audio clip configured for scene '{scene}'.");
            return null;
        }
    }
}

