using System;
using System.Linq;
using Assets.Scripts.CustomEventArgs;
using Assets.Scripts.GameManipulators;
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

            public AudioClipConfig ClipConfig => _clipConfig;
            public GameScene Scene => _scene;
        }

        [Inject] private readonly IGameSceneLoader _gameSceneLoader;

        [SerializeField] private AudioClipInSceneConfig[] _clipConfigInScenes;

        private AudioSource _audioSource;
        private float _deathAudioPitch = 0.6f;

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
            _audioSource.pitch = 1f;
        }

        private void SceneManager_OnSceneLoaded(object sender, ValueEventArgs<GameScene> args)
        {
            PlayOrContinuePlayingCorrectSceneBackgroundMusic(args.Value);
        }

        private void PlayOrContinuePlayingCorrectSceneBackgroundMusic(GameScene newScene)
        {
            AudioClipConfig clipConfig = _clipConfigInScenes
                .FirstOrDefault(config => config.Scene == newScene)
                ?.ClipConfig;

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
    }
}
