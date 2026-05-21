using Assets.Scripts.Audio;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.GameFlow;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.Spawners.WorldSpace;
using Reflex.Attributes;
using Reflex.Core;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.ReflexDI
{
    public class BootLoader : MonoBehaviour
    {
        [Inject] private readonly IGameSceneLoader _gameSceneLoader;

        [SerializeField] private AudioMixersManager _audioMixersManager;
        [SerializeField] private BackgroundAudioManager _backgroundAudioManager;
        [SerializeField] private DamageNumbersSpawner _damageNumbersSpawner;

        private void Start()
        {
            SceneScope.OnSceneContainerBuilding += InstallExtra;

            StartCoroutine(LoadNewSceneAsyncWithOneFrameDelay());
        }

        private void OnDisable()
        {
            SceneScope.OnSceneContainerBuilding -= InstallExtra;
        }

        private void InstallExtra(Scene scene, ContainerBuilder builder)
        {
            InitializeSceneCameraDependencies(scene);

            builder.AddSingleton(_audioMixersManager, typeof(IAudioMixersManager));
            builder.AddSingleton(_backgroundAudioManager, typeof(IBackgroundAudioManager));
            builder.AddSingleton(
                _damageNumbersSpawner,
                typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>),
                typeof(IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>)
            );
        }

        private void InitializeSceneCameraDependencies(Scene scene)
        {
            foreach (GameObject rootGameObject in scene.GetRootGameObjects())
            {
                DefaultGameplaySceneInstaller gameplaySceneInstaller =
                    rootGameObject.GetComponentInChildren<DefaultGameplaySceneInstaller>();

                if (gameplaySceneInstaller != null)
                {
                    _damageNumbersSpawner.Initialize(gameplaySceneInstaller.MainCamera);
                    return;
                }
            }
        }

        private IEnumerator LoadNewSceneAsyncWithOneFrameDelay()
        {
            yield return new WaitForEndOfFrame();
            _gameSceneLoader.LoadNewSceneAsync(GameScene.MainMenu);
        }
    }
}
