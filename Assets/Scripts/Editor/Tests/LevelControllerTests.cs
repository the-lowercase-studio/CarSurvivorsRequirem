using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Audio;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.Skills;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class LevelControllerTests
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

        [Test]
        public void LevelData_DefaultAndExplicitConstructors_DeconstructAndFormatExpectedValues()
        {
            // Arrange & Act
            LevelData zeroInitData = new LevelData();
            LevelData defaultParamsData = new LevelData(maxExp: 100f);
            LevelData explicitData = new LevelData(4, 27.5f, 400f);
            (byte lvl, float exp, float maxExp) = explicitData;

            // Assert
            Assert.AreEqual(0, zeroInitData.Lvl);
            Assert.AreEqual(0f, zeroInitData.Exp, TOLERANCE);
            Assert.AreEqual(0f, zeroInitData.MaxExp, TOLERANCE);

            Assert.AreEqual(1, defaultParamsData.Lvl);
            Assert.AreEqual(0f, defaultParamsData.Exp, TOLERANCE);
            Assert.AreEqual(100f, defaultParamsData.MaxExp, TOLERANCE);

            Assert.AreEqual(4, lvl);
            Assert.AreEqual(27.5f, exp, TOLERANCE);
            Assert.AreEqual(400f, maxExp, TOLERANCE);
            Assert.AreEqual("LevelData(DangerLevel: 4, Exp: 27.5, MaxExp: 400)", explicitData.ToString());
        }

        [Test]
        public void Awake_WithoutRunSessionService_InitializesLevelOneWithCurveMaxExpAndNoEvents()
        {
            // Arrange
            AnimationCurve curve = CreateLinearCurve((1f, 120f), (2f, 240f));
            LevelController controller = CreateLevelController(curve, runSessionService: null, invokeAwake: false);
            int levelUpCount = 0;
            int expChangeCount = 0;
            controller.OnLvlUp += delegate { levelUpCount++; };
            controller.OnExpChange += delegate { expChangeCount++; };

            // Act
            InvokeLifecycleMethod(controller, "Awake");

            // Assert
            Assert.AreEqual(1, controller.LevelData.Lvl);
            Assert.AreEqual(0f, controller.LevelData.Exp, TOLERANCE);
            Assert.AreEqual(120f, controller.LevelData.MaxExp, TOLERANCE);
            Assert.AreEqual(0, levelUpCount);
            Assert.AreEqual(0, expChangeCount);
        }

        [Test]
        public void Awake_WithRunSessionServiceWithoutSnapshot_InitializesLevelOneWithCurveMaxExp()
        {
            // Arrange
            AnimationCurve curve = CreateLinearCurve((1f, 150f), (5f, 750f));
            FakeRunSessionService runSessionService = new FakeRunSessionService(hasSnapshot: false, restoredLevel: 5, restoredExp: 40f);
            LevelController controller = CreateLevelController(curve, runSessionService, invokeAwake: false);

            // Act
            InvokeLifecycleMethod(controller, "Awake");

            // Assert
            Assert.AreEqual(1, controller.LevelData.Lvl);
            Assert.AreEqual(0f, controller.LevelData.Exp, TOLERANCE);
            Assert.AreEqual(150f, controller.LevelData.MaxExp, TOLERANCE);
        }

        [Test]
        public void Awake_WithRestoredLevelAndExp_RestoresStateFromCurveWithoutEmittingEvents()
        {
            // Arrange
            AnimationCurve curve = CreateLinearCurve((1f, 100f), (5f, 500f));
            FakeRunSessionService runSessionService = new FakeRunSessionService(hasSnapshot: true, restoredLevel: 5, restoredExp: 42.5f);
            LevelController controller = CreateLevelController(curve, runSessionService, invokeAwake: false);
            int levelUpCount = 0;
            int expChangeCount = 0;
            controller.OnLvlUp += delegate { levelUpCount++; };
            controller.OnExpChange += delegate { expChangeCount++; };

            // Act
            InvokeLifecycleMethod(controller, "Awake");

            // Assert
            Assert.AreEqual(5, controller.LevelData.Lvl);
            Assert.AreEqual(42.5f, controller.LevelData.Exp, TOLERANCE);
            Assert.AreEqual(500f, controller.LevelData.MaxExp, TOLERANCE);
            Assert.AreEqual(0, levelUpCount);
            Assert.AreEqual(0, expChangeCount);
        }

        [TestCase((byte)0, -15f, (byte)1, 0f, 100f, TestName = "Awake_WithZeroLevelAndNegativeExp_ClampsToLevelOneAndZeroExp")]
        [TestCase((byte)3, -0.01f, (byte)3, 0f, 300f, TestName = "Awake_WithValidLevelAndNegativeExp_PreservesLevelAndClampsExpToZero")]
        public void Awake_WithOutOfRangeRestoredValues_ClampsLevelAndExp(
            byte restoredLevel,
            float restoredExp,
            byte expectedLevel,
            float expectedExp,
            float expectedMaxExp)
        {
            // Arrange
            AnimationCurve curve = CreateLinearCurve((1f, 100f), (3f, 300f));
            FakeRunSessionService runSessionService = new FakeRunSessionService(hasSnapshot: true, restoredLevel, restoredExp);
            LevelController controller = CreateLevelController(curve, runSessionService, invokeAwake: false);

            // Act
            InvokeLifecycleMethod(controller, "Awake");

            // Assert
            Assert.AreEqual(expectedLevel, controller.LevelData.Lvl);
            Assert.AreEqual(expectedExp, controller.LevelData.Exp, TOLERANCE);
            Assert.AreEqual(expectedMaxExp, controller.LevelData.MaxExp, TOLERANCE);
        }

        [Test]
        public void AddExp_SubThresholdGains_AccumulatesExpAndEmitsOnlyOnExpChange()
        {
            // Arrange
            AnimationCurve curve = CreateLinearCurve((1f, 100f), (2f, 200f));
            LevelController controller = CreateLevelController(curve, runSessionService: null, invokeAwake: true);
            int levelUpCount = 0;
            List<LevelData> expChanges = new();
            controller.OnLvlUp += delegate { levelUpCount++; };
            controller.OnExpChange += delegate(object sender, ValueEventArgs<LevelData> args)
            {
                expChanges.Add(args.Value);
            };

            // Act
            controller.AddExp(35f);
            controller.AddExp(25f);

            // Assert
            Assert.AreEqual(1, controller.LevelData.Lvl);
            Assert.AreEqual(60f, controller.LevelData.Exp, TOLERANCE);
            Assert.AreEqual(100f, controller.LevelData.MaxExp, TOLERANCE);
            Assert.AreEqual(0, levelUpCount);
            Assert.AreEqual(2, expChanges.Count);
            Assert.AreEqual(35f, expChanges[0].Exp, TOLERANCE);
            Assert.AreEqual(60f, expChanges[1].Exp, TOLERANCE);
        }

        [TestCase(100f, 0f, TestName = "AddExp_ExactThreshold_AdvancesOneLevelWithZeroCarryover")]
        [TestCase(145f, 45f, TestName = "AddExp_OverThreshold_CarriesOverExcessExpToNextLevel")]
        public void AddExp_SingleLevelThreshold_EmitsLvlUpThenExpChangeWithExpectedCarryover(
            float addedExp,
            float expectedRemainingExp)
        {
            // Arrange
            AnimationCurve curve = CreateLinearCurve((1f, 100f), (2f, 250f));
            LevelController controller = CreateLevelController(curve, runSessionService: null, invokeAwake: true);
            List<string> eventSequence = new();
            LevelData lastExpChange = default;

            controller.OnLvlUp += delegate(object sender, ValueEventArgs<LevelData> args)
            {
                eventSequence.Add($"LvlUp:{args.Value.Lvl}:{args.Value.Exp}:{args.Value.MaxExp}");
            };
            controller.OnExpChange += delegate(object sender, ValueEventArgs<LevelData> args)
            {
                eventSequence.Add($"ExpChange:{args.Value.Lvl}:{args.Value.Exp}:{args.Value.MaxExp}");
                lastExpChange = args.Value;
            };

            // Act
            controller.AddExp(addedExp);

            // Assert
            Assert.AreEqual(2, controller.LevelData.Lvl);
            Assert.AreEqual(expectedRemainingExp, controller.LevelData.Exp, TOLERANCE);
            Assert.AreEqual(250f, controller.LevelData.MaxExp, TOLERANCE);
            Assert.AreEqual(2, lastExpChange.Lvl);
            Assert.AreEqual(expectedRemainingExp, lastExpChange.Exp, TOLERANCE);
            CollectionAssert.AreEqual(
                new[]
                {
                    $"LvlUp:2:{expectedRemainingExp}:250",
                    $"ExpChange:2:{expectedRemainingExp}:250"
                },
                eventSequence);
        }

        [Test]
        public void AddExp_MultiLevelJumpInSingleCall_EmitsOrderedLvlUpPerLevelThenSingleFinalExpChange()
        {
            // Arrange
            AnimationCurve curve = CreateLinearCurve((1f, 100f), (2f, 200f), (3f, 300f), (4f, 400f));
            LevelController controller = CreateLevelController(curve, runSessionService: null, invokeAwake: true);

            List<LevelData> levelUps = new();
            List<LevelData> levelStateDuringCallbacks = new();
            List<string> eventOrder = new();
            int expChangeCount = 0;
            LevelData finalExpPayload = default;

            controller.OnLvlUp += delegate(object sender, ValueEventArgs<LevelData> args)
            {
                levelUps.Add(args.Value);
                levelStateDuringCallbacks.Add(controller.LevelData);
                eventOrder.Add($"LvlUp:{args.Value.Lvl}");
            };
            controller.OnExpChange += delegate(object sender, ValueEventArgs<LevelData> args)
            {
                expChangeCount++;
                finalExpPayload = args.Value;
                eventOrder.Add($"ExpChange:{args.Value.Lvl}");
            };

            // Act — 650 crosses Lvl 1 (100), Lvl 2 (200), and Lvl 3 (300) with 50 remaining at Lvl 4 (max 400)
            controller.AddExp(650f);

            // Assert
            Assert.AreEqual(3, levelUps.Count);
            Assert.AreEqual(2, levelUps[0].Lvl);
            Assert.AreEqual(550f, levelUps[0].Exp, TOLERANCE);
            Assert.AreEqual(200f, levelUps[0].MaxExp, TOLERANCE);
            Assert.AreEqual(3, levelUps[1].Lvl);
            Assert.AreEqual(350f, levelUps[1].Exp, TOLERANCE);
            Assert.AreEqual(300f, levelUps[1].MaxExp, TOLERANCE);
            Assert.AreEqual(4, levelUps[2].Lvl);
            Assert.AreEqual(50f, levelUps[2].Exp, TOLERANCE);
            Assert.AreEqual(400f, levelUps[2].MaxExp, TOLERANCE);

            for (int i = 0; i < levelUps.Count; i++)
            {
                Assert.AreEqual(levelUps[i].Lvl, levelStateDuringCallbacks[i].Lvl);
                Assert.AreEqual(levelUps[i].Exp, levelStateDuringCallbacks[i].Exp, TOLERANCE);
            }

            Assert.AreEqual(1, expChangeCount);
            Assert.AreEqual(4, finalExpPayload.Lvl);
            Assert.AreEqual(50f, finalExpPayload.Exp, TOLERANCE);
            Assert.AreEqual(400f, finalExpPayload.MaxExp, TOLERANCE);
            CollectionAssert.AreEqual(new[] { "LvlUp:2", "LvlUp:3", "LvlUp:4", "ExpChange:4" }, eventOrder);
        }

        [Test]
        public void AddExp_ReachingAndSittingAtByteMaxValue_StopsAtCapAndIgnoresSubsequentCalls()
        {
            // Arrange
            AnimationCurve curve = CreateLinearCurve((1f, 100f), (254f, 100f), (255f, 100f));
            FakeRunSessionService runSessionService = new FakeRunSessionService(hasSnapshot: true, restoredLevel: 254, restoredExp: 0f);
            LevelController controller = CreateLevelController(curve, runSessionService, invokeAwake: true);

            int levelUpCount = 0;
            int expChangeCount = 0;
            controller.OnLvlUp += delegate { levelUpCount++; };
            controller.OnExpChange += delegate { expChangeCount++; };

            // Act 1 — Crosses 254 -> 255 with excess exp >= next threshold; loop stops at byte.MaxValue
            controller.AddExp(250f);

            // Assert 1
            Assert.AreEqual(byte.MaxValue, controller.LevelData.Lvl);
            Assert.AreEqual(150f, controller.LevelData.Exp, TOLERANCE);
            Assert.AreEqual(100f, controller.LevelData.MaxExp, TOLERANCE);
            Assert.AreEqual(1, levelUpCount);
            Assert.AreEqual(1, expChangeCount);

            // Act 2 — Subsequent call at byte.MaxValue returns immediately without events or state mutation
            controller.AddExp(500f);

            // Assert 2
            Assert.AreEqual(byte.MaxValue, controller.LevelData.Lvl);
            Assert.AreEqual(150f, controller.LevelData.Exp, TOLERANCE);
            Assert.AreEqual(1, levelUpCount);
            Assert.AreEqual(1, expChangeCount);
        }

        [TestCase(0f, TestName = "AddExp_ZeroInput_IsIgnoredWithoutEvents")]
        [TestCase(-20f, TestName = "AddExp_NegativeInput_IsIgnoredWithoutEvents")]
        [TestCase(float.NaN, TestName = "AddExp_NaNInput_IsIgnoredWithoutEvents")]
        [TestCase(float.PositiveInfinity, TestName = "AddExp_PositiveInfinityInput_IsIgnoredWithoutEvents")]
        [TestCase(float.NegativeInfinity, TestName = "AddExp_NegativeInfinityInput_IsIgnoredWithoutEvents")]
        public void AddExp_InvalidOrNonPositiveInput_IsIgnoredWithoutEvents(float invalidDelta)
        {
            // Arrange
            AnimationCurve curve = CreateLinearCurve((1f, 100f), (2f, 200f));
            LevelController controller = CreateLevelController(curve, runSessionService: null, invokeAwake: true);
            controller.AddExp(50f);

            int levelUpCount = 0;
            int expChangeCount = 0;
            controller.OnLvlUp += delegate { levelUpCount++; };
            controller.OnExpChange += delegate { expChangeCount++; };

            // Act
            controller.AddExp(invalidDelta);

            // Assert
            Assert.AreEqual(1, controller.LevelData.Lvl);
            Assert.AreEqual(50f, controller.LevelData.Exp, TOLERANCE);
            Assert.AreEqual(0, levelUpCount);
            Assert.AreEqual(0, expChangeCount);
        }

        [Test]
        public void RunSessionServiceIntegration_CaptureSnapshotAndAdvanceTier_RestoresLevelAndExpInNextTierController()
        {
            // Arrange
            GameLoopProgressionConfigSO progressionConfig = CreateTwoTierProgressionConfig();
            RunSessionService runSessionService = new RunSessionService(progressionConfig);

            AnimationCurve steppedCurve = CreateConstantSegmentCurve(100f, 150f, 200f, 300f);
            LevelController steppedTier1 = CreateLevelController(steppedCurve, runSessionService, invokeAwake: true);
            steppedTier1.AddExp(533.5f); // 100 (L1->L2) + 150 (L2->L3) + 200 (L3->L4) = 450, leaving 83.5 at L4
            Assert.AreEqual(4, steppedTier1.LevelData.Lvl);
            Assert.AreEqual(83.5f, steppedTier1.LevelData.Exp, TOLERANCE);

            FakePlayerManager playerManager = new FakePlayerManager(steppedTier1);

            // Act — Advance from Tier 0 to Tier 1 and instantiate a fresh LevelController
            runSessionService.CaptureSnapshotAndAdvanceTier(playerManager, 95u);

            LevelController tier2Controller = CreateLevelController(steppedCurve, runSessionService, invokeAwake: false);
            int tier2LevelUpCount = 0;
            int tier2ExpChangeCount = 0;
            tier2Controller.OnLvlUp += delegate { tier2LevelUpCount++; };
            tier2Controller.OnExpChange += delegate { tier2ExpChangeCount++; };
            InvokeLifecycleMethod(tier2Controller, "Awake");

            // Assert
            Assert.AreEqual(1, runSessionService.CurrentTierIndex);
            Assert.IsTrue(runSessionService.HasPendingRestorationSnapshot);
            Assert.AreEqual(4, tier2Controller.LevelData.Lvl);
            Assert.AreEqual(83.5f, tier2Controller.LevelData.Exp, TOLERANCE);
            Assert.AreEqual(300f, tier2Controller.LevelData.MaxExp, TOLERANCE);
            Assert.AreEqual(0, tier2LevelUpCount);
            Assert.AreEqual(0, tier2ExpChangeCount);
        }

        private LevelController CreateLevelController(
            AnimationCurve expCurve,
            IRunSessionService runSessionService,
            bool invokeAwake)
        {
            GameObject go = new GameObject("TestLevelController");
            go.SetActive(false);
            _createdObjects.Add(go);

            LevelController controller = go.AddComponent<LevelController>();
            SetPrivateField(typeof(LevelController), controller, "_expCurve", expCurve);

            if (runSessionService != null)
            {
                Container container = BuildContainer(delegate(ContainerBuilder builder)
                {
                    builder.AddSingleton(runSessionService, typeof(IRunSessionService));
                });
                GameObjectInjector.InjectObject(go, container);
            }

            if (invokeAwake)
            {
                InvokeLifecycleMethod(controller, "Awake");
            }

            return controller;
        }

        private GameLoopProgressionConfigSO CreateTwoTierProgressionConfig()
        {
            GameLoopProgressionConfigSO config = ScriptableObject.CreateInstance<GameLoopProgressionConfigSO>();
            _createdObjects.Add(config);

            List<LoopTierConfig> tiers = new List<LoopTierConfig>
            {
                new LoopTierConfig(),
                new LoopTierConfig()
            };
            SetPrivateField(typeof(GameLoopProgressionConfigSO), config, "_tiers", tiers);
            return config;
        }

        private Container BuildContainer(Action<ContainerBuilder> configure)
        {
            ContainerBuilder builder = new ContainerBuilder().SetName("LevelControllerTestContainer");
            configure?.Invoke(builder);
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private static AnimationCurve CreateLinearCurve(params (float level, float maxExp)[] points)
        {
            Keyframe[] keyframes = new Keyframe[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                keyframes[i] = new Keyframe(points[i].level, points[i].maxExp);
            }

            AnimationCurve curve = new AnimationCurve(keyframes);
            for (int i = 0; i < keyframes.Length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }

            // Ensure linear interpolation across keyframes
            for (int i = 0; i < curve.length; i++)
            {
                Keyframe key = curve[i];
                if (i > 0)
                {
                    Keyframe prev = curve[i - 1];
                    key.inTangent = (key.value - prev.value) / (key.time - prev.time);
                }
                if (i < curve.length - 1)
                {
                    Keyframe next = curve[i + 1];
                    key.outTangent = (next.value - key.value) / (next.time - key.time);
                }

                curve.MoveKey(i, key);
            }

            return curve;
        }

        private static AnimationCurve CreateConstantSegmentCurve(params float[] levelValues)
        {
            Keyframe[] keyframes = new Keyframe[levelValues.Length];
            for (int i = 0; i < levelValues.Length; i++)
            {
                keyframes[i] = new Keyframe(i + 1, levelValues[i], 0f, 0f);
            }

            return new AnimationCurve(keyframes);
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

        private sealed class FakeRunSessionService : IRunSessionService
        {
            private readonly bool _hasSnapshot;
            private readonly byte _restoredLevel;
            private readonly float _restoredExp;

            public FakeRunSessionService(bool hasSnapshot, byte restoredLevel, float restoredExp)
            {
                _hasSnapshot = hasSnapshot;
                _restoredLevel = restoredLevel;
                _restoredExp = restoredExp;
            }

            public int CurrentTierIndex { get; private set; }
            public int TotalTiersCount { get; private set; }
            public bool IsFinalTier { get; private set; }
            public LoopTierConfig ActiveTierConfig { get; private set; }
            public GameLoopProgressionConfigSO ProgressionConfig { get; private set; }
            public bool HasPendingRestorationSnapshot { get { return _hasSnapshot; } }
            public uint AccumulatedRunSeconds { get; private set; }
            public bool IsGodModeEnabled { get; private set; }

            public void CaptureSnapshotAndAdvanceTier(IPlayerManager playerManager, uint elapsedSeconds) { }
            public void CaptureSnapshotAndSetTier(int tierIndex, IPlayerManager playerManager, uint elapsedSeconds) { }
            public void CaptureFinalRunTimer(uint elapsedSeconds) { }

            public bool TryGetRestoredLevel(out byte level, out float exp)
            {
                if (!_hasSnapshot)
                {
                    level = 1;
                    exp = 0f;
                    return false;
                }

                level = _restoredLevel;
                exp = _restoredExp;
                return true;
            }

            public bool TryGetRestoredSkills(out IReadOnlyList<SavedSkillSnapshot> skills)
            {
                skills = null;
                return false;
            }

            public void SetGodModeEnabled(bool isEnabled) { }
            public void ResetSession() { }
            public void ResetSessionToTier(int tierIndex) { }
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
