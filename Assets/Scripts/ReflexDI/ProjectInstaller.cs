using System;
using Assets.ScriptableObjects.Input;
using Assets.Scripts.GameFlow;
using Assets.Scripts.Input;
using Assets.Scripts.ScoreBoard;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.ReflexDI
{
    public class ProjectInstaller : MonoBehaviour, IInstaller
    {
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private InputGlyphCatalog _inputGlyphCatalog;
        [SerializeField] private InputRuntimeConfig _inputRuntimeConfig;

        public void InstallBindings(ContainerBuilder builder)
        {
            if (_inputActions == null)
            {
                throw new InvalidOperationException($"[{nameof(ProjectInstaller)}] _inputActions is unassigned.");
            }

            if (_inputGlyphCatalog == null)
            {
                throw new InvalidOperationException($"[{nameof(ProjectInstaller)}] _inputGlyphCatalog is unassigned.");
            }

            if (_inputRuntimeConfig == null)
            {
                throw new InvalidOperationException($"[{nameof(ProjectInstaller)}] _inputRuntimeConfig is unassigned.");
            }

            //Input
            builder.AddSingleton(_inputActions);
            builder.AddSingleton(_inputGlyphCatalog);
            builder.AddSingleton(_inputRuntimeConfig);
            builder.AddSingleton(
                typeof(GameInputService),
                typeof(IGameplayInput),
                typeof(IInputCommands),
                typeof(IInputContexts),
                typeof(IInputDeviceContext),
                typeof(IInputActionSource),
                typeof(IInputRuntime));
            builder.AddSingleton(typeof(InputPromptService), typeof(IInputPromptProvider));
            builder.OnContainerBuilt += SpawnInputRuntimeDriver;

            //Scenes Loading
            builder.AddSingleton(typeof(GameSceneLoader), typeof(IGameSceneLoader));

            //ScoreBoard
            builder.AddSingleton(typeof(StoredScoreBoard));
            builder.AddSingleton(typeof(ScoreBoardNewScoreSaver), typeof(IScoreBoardNewScoreSaver));
            builder.AddSingleton(typeof(ScoreBoardBestScoreGetter), typeof(IScoreBoardBestScoreGetter));
        }

        private static void SpawnInputRuntimeDriver(Container container)
        {
            GameObject driverObject = new GameObject(nameof(InputRuntimeDriver));
            driverObject.SetActive(false);

            InputRuntimeDriver driver = driverObject.AddComponent<InputRuntimeDriver>();
            AttributeInjector.Inject(driver, container);

            driverObject.SetActive(true);
        }
    }
}
