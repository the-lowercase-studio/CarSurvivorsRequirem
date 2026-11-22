using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Input;
using Assets.Scripts.Interactables;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.Skills;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.UI.Input;
using Assets.Scripts.VFX;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Editor.Tests
{
    public class IncreaseDifficultyTotemTests
    {
        private const string TOTEM_PREFAB_PATH = "Assets/Prefabs/Interactables/IncreaseDifficultyTotem.prefab";
        private const float TOLERANCE = 0.0001f;

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdContainers.Count; i++)
            {
                _createdContainers[i]?.Dispose();
            }

            _createdContainers.Clear();

            for (int i = 0; i < _createdObjects.Count; i++)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Start_MissingGameplayInput_ThrowsInvalidOperationException()
        {
            // Arrange
            TotemHarness harness = CreateHarness(
                includeGameplayInput: false,
                includePromptProvider: true,
                includePromptView: true,
                includeVfxPlayer: false,
                runStart: false);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => InvokeLifecycleMethod(harness.Totem, "Start"));
        }

        [Test]
        public void Start_MissingPromptProvider_ThrowsInvalidOperationException()
        {
            // Arrange
            TotemHarness harness = CreateHarness(
                includeGameplayInput: true,
                includePromptProvider: false,
                includePromptView: true,
                includeVfxPlayer: false,
                runStart: false);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => InvokeLifecycleMethod(harness.Totem, "Start"));
        }

        [Test]
        public void Start_MissingInteractPromptView_ThrowsInvalidOperationException()
        {
            // Arrange
            TotemHarness harness = CreateHarness(
                includeGameplayInput: true,
                includePromptProvider: true,
                includePromptView: false,
                includeVfxPlayer: false,
                runStart: false);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => InvokeLifecycleMethod(harness.Totem, "Start"));
        }

        [Test]
        public void Start_WithValidDependencies_BindsPromptViewToPlayerInteract()
        {
            // Arrange & Act — Canvas starts inactive; Bind records request and refreshes on OnEnable when shown
            TotemHarness harness = CreateHarness(
                includeGameplayInput: true,
                includePromptProvider: true,
                includePromptView: true,
                includeVfxPlayer: false,
                runStart: true);

            Assert.IsTrue(harness.PromptView.HasBinding);

            harness.InteractionCanvas.SetActive(true);
            InvokeLifecycleMethod(harness.PromptView, "OnEnable");

            // Assert
            Assert.AreEqual(InputActionId.PlayerInteract, harness.PromptProvider.LastRequestedActionId);
            Assert.AreEqual("E", harness.PromptView.FallbackLabel.text);
        }

        [Test]
        public void Update_PlayerOutsideRadius_HidesCanvasAndIgnoresInteractPress()
        {
            // Arrange — Radius is 3.4f; place player at 3.5f
            TotemHarness harness = CreateHarness(
                includeGameplayInput: true,
                includePromptProvider: true,
                includePromptView: true,
                includeVfxPlayer: true,
                runStart: true);

            harness.InteractionCanvas.SetActive(true);
            harness.PlayerObject.transform.position = new Vector3(3.5f, 0f, 0f);
            harness.GameplayInput.WasInteractPressedThisFrame = true;

            // Act
            InvokeLifecycleMethod(harness.Totem, "Update");

            // Assert
            Assert.IsFalse(harness.InteractionCanvas.activeSelf);
            Assert.AreEqual(0, harness.DifficultyController.CallCount);
            Assert.IsTrue(harness.Totem.enabled);
        }

        [Test]
        public void Update_PlayerAtExactRadiusWithoutInteractPress_ShowsCanvasWithoutBoostingDifficulty()
        {
            // Arrange — Exact boundary distance 3.4f <= 3.4f
            TotemHarness harness = CreateHarness(
                includeGameplayInput: true,
                includePromptProvider: true,
                includePromptView: true,
                includeVfxPlayer: true,
                runStart: true);

            harness.InteractionCanvas.SetActive(false);
            harness.PlayerObject.transform.position = new Vector3(3.4f, 0f, 0f);
            harness.GameplayInput.WasInteractPressedThisFrame = false;

            // Act
            InvokeLifecycleMethod(harness.Totem, "Update");

            // Assert
            Assert.IsTrue(harness.InteractionCanvas.activeSelf);
            Assert.AreEqual(0, harness.DifficultyController.CallCount);
            Assert.IsTrue(harness.Totem.enabled);
        }

        [Test]
        public void Update_PlayerWithinRadiusAndInteractPressed_BoostsDifficultyHidesVisualsPlaysVfxAndIsOneShot()
        {
            // Arrange
            TotemHarness harness = CreateHarness(
                includeGameplayInput: true,
                includePromptProvider: true,
                includePromptView: true,
                includeVfxPlayer: true,
                runStart: true);

            harness.InteractionCanvas.SetActive(true);
            harness.TotemVisuals.SetActive(true);
            harness.PlayerObject.transform.position = new Vector3(1.5f, 0f, 0f);
            harness.GameplayInput.WasInteractPressedThisFrame = true;

            // Act — First interact triggers activation
            InvokeLifecycleMethod(harness.Totem, "Update");

            // Assert
            Assert.AreEqual(1, harness.DifficultyController.CallCount);
            Assert.AreEqual(2.6f, harness.DifficultyController.LastAmount, TOLERANCE);
            Assert.IsFalse(harness.InteractionCanvas.activeSelf);
            Assert.IsFalse(harness.TotemVisuals.activeSelf);
            Assert.IsFalse(harness.Totem.enabled);
            Assert.AreEqual(1.5f, harness.VfxPlayer.LastScheduledDuration, TOLERANCE);

            // Act — Subsequent Update call even with interact pressed does not re-trigger
            harness.InteractionCanvas.SetActive(true);
            InvokeLifecycleMethod(harness.Totem, "Update");

            // Assert
            Assert.AreEqual(1, harness.DifficultyController.CallCount);
            Assert.IsFalse(harness.InteractionCanvas.activeSelf);
        }

        [Test]
        public void Update_WithoutOptionalVfxPlayer_StillCompletesInteractionCleanly()
        {
            // Arrange
            TotemHarness harness = CreateHarness(
                includeGameplayInput: true,
                includePromptProvider: true,
                includePromptView: true,
                includeVfxPlayer: false,
                runStart: true);

            harness.PlayerObject.transform.position = Vector3.zero;
            harness.GameplayInput.WasInteractPressedThisFrame = true;

            // Act
            InvokeLifecycleMethod(harness.Totem, "Update");

            // Assert
            Assert.AreEqual(1, harness.DifficultyController.CallCount);
            Assert.AreEqual(2.6f, harness.DifficultyController.LastAmount, TOLERANCE);
            Assert.IsFalse(harness.TotemVisuals.activeSelf);
            Assert.IsFalse(harness.Totem.enabled);
        }

        [Test]
        public void Update_WhenPlayerGameObjectIsNull_HidesCanvasWithoutThrowing()
        {
            // Arrange
            TotemHarness harness = CreateHarness(
                includeGameplayInput: true,
                includePromptProvider: true,
                includePromptView: true,
                includeVfxPlayer: false,
                runStart: true);

            harness.PlayerManager.SetGameObject(null);
            harness.InteractionCanvas.SetActive(true);
            harness.GameplayInput.WasInteractPressedThisFrame = true;

            // Act
            Assert.DoesNotThrow(() => InvokeLifecycleMethod(harness.Totem, "Update"));

            // Assert
            Assert.IsFalse(harness.InteractionCanvas.activeSelf);
            Assert.AreEqual(0, harness.DifficultyController.CallCount);
        }

        [Test]
        public void OnDisable_HidesActiveInteractionCanvas()
        {
            // Arrange
            TotemHarness harness = CreateHarness(
                includeGameplayInput: true,
                includePromptProvider: true,
                includePromptView: true,
                includeVfxPlayer: false,
                runStart: true);

            harness.InteractionCanvas.SetActive(true);

            // Act
            InvokeLifecycleMethod(harness.Totem, "OnDisable");

            // Assert
            Assert.IsFalse(harness.InteractionCanvas.activeSelf);
        }

        [Test]
        public void AuthoredAssets_IncreaseDifficultyTotemPrefab_HasValidReferencesAndConfiguredValues()
        {
            // Arrange
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TOTEM_PREFAB_PATH);
            Assert.IsNotNull(prefab);

            // Act
            IncreaseDifficultyTotem totem = prefab.GetComponent<IncreaseDifficultyTotem>();
            Assert.IsNotNull(totem);

            float radius = GetPrivateValue<float>(typeof(IncreaseDifficultyTotem), totem, "_interactionRadius");
            float difficultyIncrease = GetPrivateValue<float>(typeof(IncreaseDifficultyTotem), totem, "_difficultyIncreaseAmount");
            GameObject canvas = GetPrivateReference<GameObject>(typeof(IncreaseDifficultyTotem), totem, "_interactionCanvas");
            InputPromptView promptView = GetPrivateReference<InputPromptView>(typeof(IncreaseDifficultyTotem), totem, "_interactPromptView");
            GameObject visuals = GetPrivateReference<GameObject>(typeof(IncreaseDifficultyTotem), totem, "_totemVisuals");
            VFXPlayer vfx = GetPrivateReference<VFXPlayer>(typeof(IncreaseDifficultyTotem), totem, "_vfxPlayer");

            // Assert
            Assert.AreEqual(3.4f, radius, TOLERANCE);
            Assert.AreEqual(2.6f, difficultyIncrease, TOLERANCE);
            Assert.IsNotNull(canvas);
            Assert.IsNotNull(promptView);
            Assert.IsNotNull(promptView.IconImage);
            Assert.IsNotNull(promptView.FallbackLabel);
            Assert.IsNotNull(visuals);
            Assert.IsNotNull(vfx);
        }

        private TotemHarness CreateHarness(
            bool includeGameplayInput,
            bool includePromptProvider,
            bool includePromptView,
            bool includeVfxPlayer,
            bool runStart)
        {
            GameObject playerGo = new GameObject("TotemTestPlayer");
            _createdObjects.Add(playerGo);
            FakePlayerManager playerManager = new FakePlayerManager(playerGo);
            FakeDifficultyController difficultyController = new FakeDifficultyController();
            FakeGameplayInput gameplayInput = new FakeGameplayInput();
            FakePromptProvider promptProvider = new FakePromptProvider();

            GameObject totemGo = new GameObject("TestIncreaseDifficultyTotem");
            totemGo.SetActive(false);
            _createdObjects.Add(totemGo);

            GameObject interactionCanvas = new GameObject("InteractionCanvas");
            interactionCanvas.transform.SetParent(totemGo.transform, false);
            interactionCanvas.SetActive(false);

            GameObject visuals = new GameObject("TotemVisuals");
            visuals.transform.SetParent(totemGo.transform, false);
            visuals.SetActive(true);

            InputPromptView promptView = null;
            if (includePromptView)
            {
                promptView = CreatePromptView(interactionCanvas.transform);
            }

            VFXPlayer vfxPlayer = null;
            if (includeVfxPlayer)
            {
                GameObject vfxGo = new GameObject("TotemVfx");
                vfxGo.transform.SetParent(totemGo.transform, false);
                ParticleSystem ps = vfxGo.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = ps.main;
                main.duration = 1.5f;
                main.playOnAwake = false;
                vfxPlayer = vfxGo.AddComponent<VFXPlayer>();
                InvokeLifecycleMethod(vfxPlayer, "Awake");
            }

            IncreaseDifficultyTotem totem = totemGo.AddComponent<IncreaseDifficultyTotem>();
            SetPrivateField(typeof(IncreaseDifficultyTotem), totem, "_interactionRadius", 3.4f);
            SetPrivateField(typeof(IncreaseDifficultyTotem), totem, "_interactionCanvas", interactionCanvas);
            SetPrivateField(typeof(IncreaseDifficultyTotem), totem, "_interactPromptView", promptView);
            SetPrivateField(typeof(IncreaseDifficultyTotem), totem, "_totemVisuals", visuals);
            SetPrivateField(typeof(IncreaseDifficultyTotem), totem, "_vfxPlayer", vfxPlayer);
            SetPrivateField(typeof(IncreaseDifficultyTotem), totem, "_difficultyIncreaseAmount", 2.6f);

            ContainerBuilder builder = new ContainerBuilder().SetName("IncreaseDifficultyTotemTestContainer");
            builder.AddSingleton(playerManager, typeof(IPlayerManager));
            builder.AddSingleton(difficultyController, typeof(IEnemySpawnDifficultyController));

            if (includeGameplayInput)
            {
                builder.AddSingleton(gameplayInput, typeof(IGameplayInput));
            }
            else
            {
                builder.AddSingleton<IGameplayInput>(delegate { return null; });
            }

            if (includePromptProvider)
            {
                builder.AddSingleton(promptProvider, typeof(IInputPromptProvider));
            }
            else
            {
                builder.AddSingleton<IInputPromptProvider>(delegate { return null; });
            }

            Container container = builder.Build();
            _createdContainers.Add(container);

            GameObjectInjector.InjectRecursive(totemGo, container);
            totemGo.SetActive(true);
            if (runStart)
            {
                InvokeLifecycleMethod(totem, "Start");
            }

            return new TotemHarness(
                totem,
                playerGo,
                playerManager,
                difficultyController,
                gameplayInput,
                promptProvider,
                promptView,
                interactionCanvas,
                visuals,
                vfxPlayer);
        }

        private InputPromptView CreatePromptView(Transform parent)
        {
            GameObject viewGo = new GameObject("PromptView", typeof(RectTransform));
            viewGo.transform.SetParent(parent, false);

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(viewGo.transform, false);
            Image iconImage = iconGo.AddComponent<Image>();

            GameObject labelGo = new GameObject("FallbackLabel", typeof(RectTransform));
            labelGo.transform.SetParent(viewGo.transform, false);
            TextMeshProUGUI fallbackLabel = labelGo.AddComponent<TextMeshProUGUI>();

            InputPromptView promptView = viewGo.AddComponent<InputPromptView>();
            SetPrivateField(typeof(InputPromptView), promptView, "_iconImage", iconImage);
            SetPrivateField(typeof(InputPromptView), promptView, "_fallbackLabel", fallbackLabel);
            InvokeLifecycleMethod(promptView, "Awake");
            return promptView;
        }

        private static T GetPrivateReference<T>(Type ownerType, object target, string fieldName) where T : class
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {ownerType.Name}");
            return field.GetValue(target) as T;
        }

        private static T GetPrivateValue<T>(Type ownerType, object target, string fieldName) where T : struct
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {ownerType.Name}");
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {ownerType.Name}");
            field.SetValue(target, value);
        }

        private static void InvokeLifecycleMethod(object target, string methodName)
        {
            Type type = target.GetType();
            while (type != null)
            {
                MethodInfo method = type.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                if (method != null)
                {
                    Action callback = (Action)Delegate.CreateDelegate(typeof(Action), target, method);
                    callback();
                    return;
                }

                type = type.BaseType;
            }
        }

        private readonly struct TotemHarness
        {
            public readonly IncreaseDifficultyTotem Totem;
            public readonly GameObject PlayerObject;
            public readonly FakePlayerManager PlayerManager;
            public readonly FakeDifficultyController DifficultyController;
            public readonly FakeGameplayInput GameplayInput;
            public readonly FakePromptProvider PromptProvider;
            public readonly InputPromptView PromptView;
            public readonly GameObject InteractionCanvas;
            public readonly GameObject TotemVisuals;
            public readonly VFXPlayer VfxPlayer;

            public TotemHarness(
                IncreaseDifficultyTotem totem,
                GameObject playerObject,
                FakePlayerManager playerManager,
                FakeDifficultyController difficultyController,
                FakeGameplayInput gameplayInput,
                FakePromptProvider promptProvider,
                InputPromptView promptView,
                GameObject interactionCanvas,
                GameObject totemVisuals,
                VFXPlayer vfxPlayer)
            {
                Totem = totem;
                PlayerObject = playerObject;
                PlayerManager = playerManager;
                DifficultyController = difficultyController;
                GameplayInput = gameplayInput;
                PromptProvider = promptProvider;
                PromptView = promptView;
                InteractionCanvas = interactionCanvas;
                TotemVisuals = totemVisuals;
                VfxPlayer = vfxPlayer;
            }
        }

        private sealed class FakeDifficultyController : IEnemySpawnDifficultyController
        {
            public int CallCount { get; private set; }
            public float LastAmount { get; private set; }

            public void IncreaseSpawnChanceRedistributionFactor(float amount)
            {
                CallCount++;
                LastAmount = amount;
            }
        }

        private sealed class FakeGameplayInput : IGameplayInput
        {
            public Vector2 MoveInput { get { return Vector2.zero; } }
            public bool IsBrakePressed { get { return false; } }
            public float ReverseInput { get { return 0f; } }
            public bool WasInteractPressedThisFrame { get; set; }

            public event EventHandler OnBrakeStarted
            {
                add { }
                remove { }
            }

            public event EventHandler OnBrakeCanceled
            {
                add { }
                remove { }
            }
        }

        private sealed class FakePromptProvider : IInputPromptProvider
        {
            public InputActionId LastRequestedActionId { get; private set; }

            public event Action OnPromptsChanged
            {
                add { }
                remove { }
            }

            public InputPromptDescriptor GetPrompt(InputPromptRequest request)
            {
                LastRequestedActionId = request.ActionId;
                return new InputPromptDescriptor(
                    isValid: true,
                    isBound: true,
                    isAvailable: true,
                    family: InputDeviceFamily.KeyboardMouse,
                    primarySprite: null,
                    primaryLabel: "E",
                    controlPath: "e");
            }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            private GameObject _gameObject;

            public GameObject GameObject { get { return _gameObject; } }
            public Assets.Scripts.HealthSystem.IHealth Health { get { return null; } }
            public Assets.Scripts.Audio.IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public ICarController CarController { get { return null; } }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get { return null; } }
            public ISkillsRegistry SkillsRegistry { get { return null; } }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }

            public FakePlayerManager(GameObject gameObject)
            {
                _gameObject = gameObject;
            }

            public void SetGameObject(GameObject gameObject)
            {
                _gameObject = gameObject;
            }
        }
    }
}
