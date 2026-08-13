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
        GameScene CurrentLoadedScene { get; }

        event EventHandler<ValueEventArgs<GameScene>> OnSceneLoaded;

        event EventHandler<ValueEventArgs<GameScene>> OnStartLoadingScene;

        AsyncOperation LoadNewSceneAsync(GameScene scene);

        AsyncOperation ReloadCurrentSceneAsync();
    }

    public class GameSceneLoader : IGameSceneLoader
    {
        public GameScene CurrentLoadedScene { get; private set; } = GameScene.MainMenu;

        public event EventHandler<ValueEventArgs<GameScene>> OnStartLoadingScene;

        public event EventHandler<ValueEventArgs<GameScene>> OnSceneLoaded;

        public AsyncOperation LoadNewSceneAsync(GameScene scene)
        {
            var result = SceneManager.LoadSceneAsync((int)scene, LoadSceneMode.Single);
            var eventArgs = new ValueEventArgs<GameScene>(scene);

            OnStartLoadingScene?.Invoke(this, eventArgs);

            result.completed += operation =>
            {
                GameTime.Resume();
                CurrentLoadedScene = scene;
                OnSceneLoaded?.Invoke(this, eventArgs);
            };

            return result;
        }

        public AsyncOperation ReloadCurrentSceneAsync()
        {
            return LoadNewSceneAsync(CurrentLoadedScene);
        }
    }
}
