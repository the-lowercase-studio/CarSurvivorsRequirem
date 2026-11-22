using System;
using Assets.Scripts.Common.EventArgs;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.GameFlow
{
    [Serializable]
    public enum GameScene
    {
        MainMenu = 1,
        RuinedBloodCity = 2
    }

    public interface IGameSceneLoader
    {
        bool IsLoading { get; }

        GameScene CurrentLoadedScene { get; }

        event EventHandler<ValueEventArgs<GameScene>> OnSceneLoaded;

        event EventHandler<ValueEventArgs<GameScene>> OnStartLoadingScene;

        AsyncOperation LoadNewSceneAsync(GameScene scene);

        AsyncOperation ReloadCurrentSceneAsync();
    }

    public class GameSceneLoader : IGameSceneLoader
    {
        private readonly IGameTimeService _gameTimeService;
        private bool _isLoading;
        private AsyncOperation _activeLoadOperation;

        public GameSceneLoader(IGameTimeService gameTimeService)
        {
            _gameTimeService = gameTimeService ?? throw new ArgumentNullException(nameof(gameTimeService));
        }

        public bool IsLoading
        {
            get
            {
                return _isLoading;
            }
        }

        public GameScene CurrentLoadedScene { get; private set; } = GameScene.MainMenu;

        public event EventHandler<ValueEventArgs<GameScene>> OnStartLoadingScene;

        public event EventHandler<ValueEventArgs<GameScene>> OnSceneLoaded;

        public AsyncOperation LoadNewSceneAsync(GameScene scene)
        {
            if (_isLoading)
            {
                Debug.LogWarning($"[{nameof(GameSceneLoader)}] Ignored duplicate scene load request for {scene} while another load is in progress.");
                return _activeLoadOperation;
            }

            AsyncOperation result = SceneManager.LoadSceneAsync((int)scene, LoadSceneMode.Single);
            if (result == null)
            {
                Debug.LogError($"[{nameof(GameSceneLoader)}] Failed to start async load for scene {scene}.");
                return null;
            }

            _isLoading = true;
            _activeLoadOperation = result;

            var eventArgs = new ValueEventArgs<GameScene>(scene);
            OnStartLoadingScene?.Invoke(this, eventArgs);

            result.completed += operation =>
            {
                _isLoading = false;
                _activeLoadOperation = null;
                _gameTimeService.Reset();
                CurrentLoadedScene = scene;
                OnSceneLoaded?.Invoke(this, eventArgs);
            };

            return result;
        }

        public AsyncOperation ReloadCurrentSceneAsync()
        {
            int activeBuildIndex = SceneManager.GetActiveScene().buildIndex;
            if (Enum.IsDefined(typeof(GameScene), activeBuildIndex))
            {
                CurrentLoadedScene = (GameScene)activeBuildIndex;
            }

            return LoadNewSceneAsync(CurrentLoadedScene);
        }
    }
}
