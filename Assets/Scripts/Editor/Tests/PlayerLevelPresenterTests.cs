using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Audio;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.Skills;
using Assets.Scripts.UI.Level;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Editor.Tests
{
    public class PlayerLevelPresenterTests
    {
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

        [TestCase((byte)1, 0f, 100f, 0f, "1", TestName = "Start_DefaultLevelOne_InitializesSliderAndLevelText")]
        [TestCase((byte)6, 45f, 600f, 45f, "6", TestName = "Start_RestoredMidRunLevel_AssignsMaxValueBeforeValueWithoutClamping")]
        public void Start_InitializesSliderAndLevelTextFromControllerLevelData(
            byte initialLevel,
            float initialExp,
            float initialMaxExp,
            float expectedSliderValue,
            string expectedLevelText)
        {
            // Arrange
            PresenterHarness harness = CreateHarness(new LevelData(initialLevel, initialExp, initialMaxExp), invokeStart: false);

            // Act
            InvokeLifecycleMethod(harness.Presenter, "Start");
            harness.Presenter.CancelInvoke();

            // Assert
            Assert.AreEqual(initialMaxExp, harness.Slider.maxValue, TOLERANCE);
            Assert.AreEqual(expectedSliderValue, harness.Slider.value, TOLERANCE);
            Assert.AreEqual(expectedLevelText, harness.LevelText.text);
        }

        [Test]
        public void FastForward_WithQueuedMultiLevelUpsAndFinalExp_EmitsOrderedReachedEventsAndSyncsHud()
        {
            // Arrange
            PresenterHarness harness = CreateHarness(new LevelData(1, 0f, 100f), invokeStart: true);
            List<LevelData> reachedEvents = new();
            harness.Presenter.OnExpSliderVisualEndValueReached += delegate(object sender, ValueEventArgs<LevelData> args)
            {
                reachedEvents.Add(args.Value);
            };

            harness.LevelController.SimulateMultiLevelExpGain(
                new[]
                {
                    new LevelData(2, 150f, 200f),
                    new LevelData(3, 25f, 300f)
                },
                new LevelData(3, 25f, 300f));

            // Act
            harness.Presenter.FastForward();

            // Assert
            Assert.AreEqual(2, reachedEvents.Count);
            Assert.AreEqual(2, reachedEvents[0].Lvl);
            Assert.AreEqual(150f, reachedEvents[0].Exp, TOLERANCE);
            Assert.AreEqual(200f, reachedEvents[0].MaxExp, TOLERANCE);
            Assert.AreEqual(3, reachedEvents[1].Lvl);
            Assert.AreEqual(25f, reachedEvents[1].Exp, TOLERANCE);
            Assert.AreEqual(300f, reachedEvents[1].MaxExp, TOLERANCE);

            Assert.AreEqual(300f, harness.Slider.maxValue, TOLERANCE);
            Assert.AreEqual(25f, harness.Slider.value, TOLERANCE);
            Assert.AreEqual("3", harness.LevelText.text);
        }

        [Test]
        public void SameLevelExpQueue_IgnoresMismatchedLevelAndConsumesLatestSameLevelUpdate()
        {
            // Arrange
            PresenterHarness harness = CreateHarness(new LevelData(1, 0f, 100f), invokeStart: true);

            // Act 1 — Mismatched level OnExpChange while visible level is still 1 is ignored
            harness.LevelController.EmitExpChangeOnly(new LevelData(2, 40f, 200f));
            InvokeLifecycleMethod(harness.Presenter, "HandleTweensAnimations");

            Coroutine expCoroutineAfterMismatch = GetPrivateField<Coroutine>(typeof(PlayerLevelPresenter), harness.Presenter, "_expIncreaseCoroutine");
            Assert.IsNull(expCoroutineAfterMismatch);

            // Act 2 — Multiple same-level OnExpChange events coalesce to the latest value
            harness.LevelController.EmitExpChangeOnly(new LevelData(1, 20f, 100f));
            harness.LevelController.EmitExpChangeOnly(new LevelData(1, 65f, 100f));
            InvokeLifecycleMethod(harness.Presenter, "HandleTweensAnimations");

            // Assert — Visible level data advances to latest queued same-level exp and queue slot clears
            LevelData visibleData = GetPrivateField<LevelData>(typeof(PlayerLevelPresenter), harness.Presenter, "_currentlyVisibleLevelData");
            object pendingSameLevel = typeof(PlayerLevelPresenter)
                .GetField("_lastQueuedExpInSameLevelIncreaseEvent", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(harness.Presenter);

            Assert.AreEqual(1, visibleData.Lvl);
            Assert.AreEqual(65f, visibleData.Exp, TOLERANCE);
            Assert.IsNull(pendingSameLevel);
        }

        [Test]
        public void OnDestroy_UnsubscribesFromLevelControllerEvents()
        {
            // Arrange
            PresenterHarness harness = CreateHarness(new LevelData(1, 0f, 100f), invokeStart: true);
            int reachedCount = 0;
            harness.Presenter.OnExpSliderVisualEndValueReached += delegate { reachedCount++; };

            // Act
            InvokeLifecycleMethod(harness.Presenter, "OnDestroy");
            harness.LevelController.SimulateMultiLevelExpGain(
                new[] { new LevelData(2, 10f, 200f) },
                new LevelData(2, 10f, 200f));
            harness.Presenter.FastForward();

            // Assert — Queued visual queue remained empty because presenter unsubscribed in OnDestroy
            Assert.AreEqual(0, reachedCount);
            Assert.AreEqual(200f, harness.Slider.maxValue, TOLERANCE);
            Assert.AreEqual(10f, harness.Slider.value, TOLERANCE);
            Assert.AreEqual("2", harness.LevelText.text);
        }

        private PresenterHarness CreateHarness(LevelData initialData, bool invokeStart)
        {
            GameObject root = new GameObject("TestPlayerLevelPresenter", typeof(RectTransform));
            root.SetActive(false);
            _createdObjects.Add(root);

            GameObject textGo = new GameObject("LevelText", typeof(RectTransform));
            textGo.transform.SetParent(root.transform, false);
            TextMeshProUGUI levelText = textGo.AddComponent<TextMeshProUGUI>();

            GameObject sliderGo = new GameObject("ExpSlider", typeof(RectTransform));
            sliderGo.transform.SetParent(root.transform, false);
            Slider slider = sliderGo.AddComponent<Slider>();

            PlayerLevelPresenter presenter = root.AddComponent<PlayerLevelPresenter>();
            SetPrivateField(typeof(PlayerLevelPresenter), presenter, "_levelText", levelText);
            SetPrivateField(typeof(PlayerLevelPresenter), presenter, "_expSlider", slider);

            FakeLevelController levelController = new FakeLevelController(initialData);
            FakePlayerManager playerManager = new FakePlayerManager(levelController);

            Container container = BuildContainer(delegate(ContainerBuilder builder)
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
            });
            GameObjectInjector.InjectObject(root, container);
            root.SetActive(true);

            if (invokeStart)
            {
                InvokeLifecycleMethod(presenter, "Start");
                presenter.CancelInvoke();
            }

            return new PresenterHarness(presenter, slider, levelText, levelController);
        }

        private Container BuildContainer(Action<ContainerBuilder> configure)
        {
            ContainerBuilder builder = new ContainerBuilder().SetName("PlayerLevelPresenterTestContainer");
            configure?.Invoke(builder);
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private static T GetPrivateField<T>(Type ownerType, object target, string fieldName)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
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

        private sealed class PresenterHarness
        {
            public PresenterHarness(
                PlayerLevelPresenter presenter,
                Slider slider,
                TextMeshProUGUI levelText,
                FakeLevelController levelController)
            {
                Presenter = presenter;
                Slider = slider;
                LevelText = levelText;
                LevelController = levelController;
            }

            public PlayerLevelPresenter Presenter { get; private set; }
            public Slider Slider { get; private set; }
            public TextMeshProUGUI LevelText { get; private set; }
            public FakeLevelController LevelController { get; private set; }
        }

        private sealed class FakeLevelController : ILevelController
        {
            public FakeLevelController(LevelData initialData)
            {
                LevelData = initialData;
            }

            public LevelData LevelData { get; private set; }

            public event EventHandler<ValueEventArgs<LevelData>> OnExpChange;
            public event EventHandler<ValueEventArgs<LevelData>> OnLvlUp;

            public void AddExp(float value)
            {
            }

            public void EmitExpChangeOnly(LevelData data)
            {
                LevelData = data;
                OnExpChange?.Invoke(this, new ValueEventArgs<LevelData>(data));
            }

            public void SimulateMultiLevelExpGain(LevelData[] levelUps, LevelData finalData)
            {
                for (int i = 0; i < levelUps.Length; i++)
                {
                    LevelData = levelUps[i];
                    OnLvlUp?.Invoke(this, new ValueEventArgs<LevelData>(levelUps[i]));
                }

                LevelData = finalData;
                OnExpChange?.Invoke(this, new ValueEventArgs<LevelData>(finalData));
            }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            public FakePlayerManager(ILevelController levelController)
            {
                LevelController = levelController;
            }

            public IHealth Health { get { return null; } }
            public GameObject GameObject { get { return null; } }
            public IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public ICarController CarController { get { return null; } }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get; private set; }
            public ISkillsRegistry SkillsRegistry { get { return null; } }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }
        }
    }
}
