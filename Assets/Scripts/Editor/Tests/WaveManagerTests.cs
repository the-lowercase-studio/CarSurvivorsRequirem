using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.Player;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.Spawners.GridSpace;
using Assets.Scripts.Waves;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class WaveManagerTests
    {
        private const string PROGRESSION_CONFIG_PATH = "Assets/ScriptableObjects/GameFlow/GameLoopProgressionConfig.asset";
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
        public void Start_WithoutWaveConfigOrRunSession_UsesDefaultDelaysInitialCountAndRoundingRule()
        {
            // Arrange — Defaults: firstWaveDelay=1, startSpawnWaveDelay=8, initial=4, growth=1.2 -> RoundToInt(4*1.2)=5
            WaveManagerHarness harness = CreateHarness(waveConfig: null, runSessionService: null);

            // Act & Assert — Wave 1 waits full 1.0s
            harness.WaveManager.AdvanceWaves(0.75f);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCounts.Count);

            harness.WaveManager.AdvanceWaves(0.25f);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCounts.Count);

            harness.WaveManager.AdvanceWaves(0.0625f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCounts.Count);
            Assert.AreEqual(4, harness.EnemySpawner.SpawnCounts[0]);

            // Act & Assert — Wave 2 waits 8.0s while enemies remain alive
            harness.WaveManager.AdvanceWaves(7.75f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCounts.Count);

            harness.WaveManager.AdvanceWaves(0.25f);
            harness.WaveManager.AdvanceWaves(0.0625f);
            Assert.AreEqual(2, harness.EnemySpawner.SpawnCounts.Count);
            Assert.AreEqual(5, harness.EnemySpawner.SpawnCounts[1]);
        }

        [Test]
        public void Start_WithCustomWaveConfigAndNullRunSession_UsesConfiguredDelayAndGrowth()
        {
            // Arrange — 5 * 1.2 = 6; 6 * 1.2 = 7.2 -> (int)7.2 = 7
            WaveConfig config = CreateWaveConfig(
                firstWaveDelay: 2f,
                waveDelay: 6f,
                initialCount: 5,
                growthMultiplier: 1.2f);
            WaveManagerHarness harness = CreateHarness(waveConfig: config, runSessionService: null);

            // Act — Wave 1 at 2s, Wave 2 at +6s, Wave 3 at +6s
            AdvanceDelayAndTrigger(harness.WaveManager, 2f);
            AdvanceDelayAndTrigger(harness.WaveManager, 6f);
            AdvanceDelayAndTrigger(harness.WaveManager, 6f);

            // Assert
            CollectionAssert.AreEqual(new[] { 5, 6, 7 }, harness.EnemySpawner.SpawnCounts);
        }

        [Test]
        public void Start_WithRunSessionHavingNullActiveTierConfig_FallsBackToWaveConfigValues()
        {
            // Arrange
            WaveConfig config = CreateWaveConfig(
                firstWaveDelay: 1f,
                waveDelay: 5f,
                initialCount: 5,
                growthMultiplier: 1.2f);
            WaveManagerHarness harness = CreateHarness(
                waveConfig: config,
                runSessionService: new FakeNullTierRunSessionService());

            // Act
            AdvanceDelayAndTrigger(harness.WaveManager, 1f);
            harness.WaveManager.AdvanceWaves(4.75f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCounts.Count);

            harness.WaveManager.AdvanceWaves(0.25f);
            harness.WaveManager.AdvanceWaves(0.0625f);

            // Assert
            CollectionAssert.AreEqual(new[] { 5, 6 }, harness.EnemySpawner.SpawnCounts);
        }

        [Test]
        public void Start_WithTierScaling_ScalesDelayInitialCountGrowthAndEnforcesCap()
        {
            // Arrange — initial=RoundToInt(4*2.5)=10; delay=8*0.5=4s; growth=1.2*1.25=1.5; cap=20
            // Expected sequence: 10 -> 15 -> min(22.5, 20)=20 -> 20
            WaveConfig config = CreateWaveConfig(
                firstWaveDelay: 1f,
                waveDelay: 8f,
                initialCount: 4,
                growthMultiplier: 1.2f);
            GameLoopProgressionConfigSO progression = CreateProgressionWithTierWaveSettings(
                initialCountMultiplier: 2.5f,
                delayMultiplier: 0.5f,
                growthMultiplier: 1.25f,
                maxEnemiesCap: 20);
            RunSessionService runSession = new RunSessionService(progression);
            WaveManagerHarness harness = CreateHarness(waveConfig: config, runSessionService: runSession);

            // Act — Trigger Wave 1 (1s), then verify Wave 2 waits 4s (not spawned at 3.75s)
            AdvanceDelayAndTrigger(harness.WaveManager, 1f);
            harness.WaveManager.AdvanceWaves(3.75f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCounts.Count);

            harness.WaveManager.AdvanceWaves(0.25f);
            harness.WaveManager.AdvanceWaves(0.0625f);
            AdvanceDelayAndTrigger(harness.WaveManager, 4f);
            AdvanceDelayAndTrigger(harness.WaveManager, 4f);

            // Assert
            CollectionAssert.AreEqual(new[] { 10, 15, 20, 20 }, harness.EnemySpawner.SpawnCounts);
        }

        [Test]
        public void Start_WithSubMinimumTierMultipliers_ClampsDelayAndGrowthToPointOneAndCountToOne()
        {
            // Arrange — 1*0.01=0.01 clamps to 0.1s delay; 1*0.01 clamps to 0.1 growth; RoundToInt(2*0.1)=0 clamps to 1
            WaveConfig config = CreateWaveConfig(
                firstWaveDelay: 0.5f,
                waveDelay: 1f,
                initialCount: 2,
                growthMultiplier: 1f);
            GameLoopProgressionConfigSO progression = CreateProgressionWithTierWaveSettings(
                initialCountMultiplier: 0.1f,
                delayMultiplier: 0.01f,
                growthMultiplier: 0.01f,
                maxEnemiesCap: 5);
            RunSessionService runSession = new RunSessionService(progression);
            WaveManagerHarness harness = CreateHarness(waveConfig: config, runSessionService: runSession);

            // Act — Wave 1 at 0.5s, Wave 2 waits clamped 0.1s (idle at 0.05s)
            AdvanceDelayAndTrigger(harness.WaveManager, 0.5f);
            harness.WaveManager.AdvanceWaves(0.05f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCounts.Count);

            harness.WaveManager.AdvanceWaves(0.0625f);
            harness.WaveManager.AdvanceWaves(0.0625f);

            // Assert
            CollectionAssert.AreEqual(new[] { 1, 1 }, harness.EnemySpawner.SpawnCounts);
        }

        [Test]
        public void Start_WithScaledInitialCountExceedingTierCap_ClampsInitialWaveToCap()
        {
            // Arrange — RoundToInt(10 * 2) = 20 clamped down to cap 7
            WaveConfig config = CreateWaveConfig(
                firstWaveDelay: 0.5f,
                waveDelay: 2f,
                initialCount: 10,
                growthMultiplier: 1.5f);
            GameLoopProgressionConfigSO progression = CreateProgressionWithTierWaveSettings(
                initialCountMultiplier: 2f,
                delayMultiplier: 1f,
                growthMultiplier: 1f,
                maxEnemiesCap: 7);
            RunSessionService runSession = new RunSessionService(progression);
            WaveManagerHarness harness = CreateHarness(waveConfig: config, runSessionService: runSession);

            // Act
            AdvanceDelayAndTrigger(harness.WaveManager, 0.5f);
            AdvanceDelayAndTrigger(harness.WaveManager, 2f);

            // Assert
            CollectionAssert.AreEqual(new[] { 7, 7 }, harness.EnemySpawner.SpawnCounts);
        }

        [TestCase(0f, TestName = "AdvanceWaves_ZeroDeltaTime_DoesNotAdvanceTimer")]
        [TestCase(-1f, TestName = "AdvanceWaves_NegativeDeltaTime_DoesNotAdvanceTimer")]
        [TestCase(float.NaN, TestName = "AdvanceWaves_NaNDeltaTime_DoesNotAdvanceTimer")]
        [TestCase(float.PositiveInfinity, TestName = "AdvanceWaves_PositiveInfinityDeltaTime_DoesNotAdvanceTimer")]
        [TestCase(float.NegativeInfinity, TestName = "AdvanceWaves_NegativeInfinityDeltaTime_DoesNotAdvanceTimer")]
        public void AdvanceWaves_InvalidDeltaTime_DoesNotAdvanceTimerOrSpawn(float invalidDeltaTime)
        {
            // Arrange
            WaveConfig config = CreateWaveConfig(
                firstWaveDelay: 1f,
                waveDelay: 8f,
                initialCount: 4,
                growthMultiplier: 1.2f);
            WaveManagerHarness harness = CreateHarness(waveConfig: config, runSessionService: null);

            // Act — Invalid deltas are ignored; 0.75s leaves 0.25s remaining
            harness.WaveManager.AdvanceWaves(invalidDeltaTime);
            harness.WaveManager.AdvanceWaves(0.75f);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCounts.Count);

            harness.WaveManager.AdvanceWaves(0.25f);
            harness.WaveManager.AdvanceWaves(0.0625f);

            // Assert
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCounts.Count);
        }

        [Test]
        public void AdvanceWaves_WhenWaveFreezerIsFrozen_PausesCountdownUntilUnfrozen()
        {
            // Arrange
            WaveConfig config = CreateWaveConfig(
                firstWaveDelay: 1f,
                waveDelay: 8f,
                initialCount: 4,
                growthMultiplier: 1.2f);
            WaveManagerHarness harness = CreateHarness(waveConfig: config, runSessionService: null);

            // Act — Freeze and advance 10s without triggering Wave 1
            harness.WaveManager.IsFrozen = true;
            AdvanceSteps(harness.WaveManager, 2f, 5);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCounts.Count);

            // Act — Unfreeze and verify full 1.0s delay remains
            harness.WaveManager.IsFrozen = false;
            harness.WaveManager.AdvanceWaves(0.75f);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCounts.Count);

            harness.WaveManager.AdvanceWaves(0.25f);
            harness.WaveManager.AdvanceWaves(0.0625f);

            // Assert
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCounts.Count);
        }

        [Test]
        public void AdvanceWaves_WhenTimeFreezeStateIsFrozen_PausesCountdownUntilUnfrozen()
        {
            // Arrange
            WaveConfig config = CreateWaveConfig(
                firstWaveDelay: 1f,
                waveDelay: 8f,
                initialCount: 4,
                growthMultiplier: 1.2f);
            WaveManagerHarness harness = CreateHarness(waveConfig: config, runSessionService: null);

            // Act — World time freeze pauses wave progression
            harness.TimeFreeze.IsFrozen = true;
            AdvanceSteps(harness.WaveManager, 2f, 5);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCounts.Count);

            // Act — Unfreeze world time and verify full 1.0s remains
            harness.TimeFreeze.IsFrozen = false;
            harness.WaveManager.AdvanceWaves(0.75f);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCounts.Count);

            harness.WaveManager.AdvanceWaves(0.25f);
            harness.WaveManager.AdvanceWaves(0.0625f);

            // Assert
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCounts.Count);
        }

        [Test]
        public void AdvanceWaves_Wave1WithZeroActiveEnemies_DoesNotBypassFirstWaveDelay()
        {
            // Arrange
            WaveConfig config = CreateWaveConfig(
                firstWaveDelay: 2f,
                waveDelay: 8f,
                initialCount: 4,
                growthMultiplier: 1.2f);
            WaveManagerHarness harness = CreateHarness(waveConfig: config, runSessionService: null);
            harness.EnemySpawner.ActiveCount = 0;

            // Act — Multiple steps with 0 active enemies must still honor firstWaveDelay
            harness.WaveManager.AdvanceWaves(1.5f);
            harness.WaveManager.AdvanceWaves(0.25f);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCounts.Count);

            harness.WaveManager.AdvanceWaves(0.25f);
            harness.WaveManager.AdvanceWaves(0.0625f);

            // Assert
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCounts.Count);
        }

        [Test]
        public void AdvanceWaves_PostWave1WhenActiveEnemiesDropToZero_BypassesRemainingDelay()
        {
            // Arrange
            WaveConfig config = CreateWaveConfig(
                firstWaveDelay: 1f,
                waveDelay: 8f,
                initialCount: 5,
                growthMultiplier: 1.2f);
            WaveManagerHarness harness = CreateHarness(waveConfig: config, runSessionService: null);

            // Act — Spawn Wave 1 (5 active enemies, 8s delay starts) and advance 0.5s
            AdvanceDelayAndTrigger(harness.WaveManager, 1f);
            harness.WaveManager.AdvanceWaves(0.5f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCounts.Count);

            // Clear all active enemies -> next positive step bypasses remaining 7.5s delay
            harness.EnemySpawner.ActiveCount = 0;
            harness.WaveManager.AdvanceWaves(0.0625f);

            // Assert
            CollectionAssert.AreEqual(new[] { 5, 6 }, harness.EnemySpawner.SpawnCounts);
        }

        [Test]
        public void Start_WithTier1ProgressionAssetAndDefaultWaveConfig_ScalesAcrossWavesWithRounding()
        {
            // Arrange — Real GameLoopProgressionConfig.asset Tier 1 (1, 1, 1, cap 45) + default WaveConfig (4, 1.2)
            GameLoopProgressionConfigSO progression = AssetDatabase.LoadAssetAtPath<GameLoopProgressionConfigSO>(PROGRESSION_CONFIG_PATH);
            Assert.IsNotNull(progression);
            RunSessionService runSession = new RunSessionService(progression);
            WaveManagerHarness harness = CreateHarness(waveConfig: null, runSessionService: runSession);

            // Act — Wave 1 (1s), Wave 2 (8s), Wave 3 (8s)
            AdvanceDelayAndTrigger(harness.WaveManager, 1f);
            AdvanceDelayAndTrigger(harness.WaveManager, 8f);
            AdvanceDelayAndTrigger(harness.WaveManager, 8f);

            // Assert — RoundToInt(4 * 1.2f) = 5, RoundToInt(5 * 1.2f) = 6
            CollectionAssert.AreEqual(new[] { 4, 5, 6 }, harness.EnemySpawner.SpawnCounts);
        }

        [Test]
        public void Start_WithTier2ProgressionAssetAndDefaultWaveConfig_StartsAtTenGrowsAndCapsAtThirtySix()
        {
            // Arrange — Real GameLoopProgressionConfig.asset Tier 2:
            // initial=RoundToInt(4*2.5)=10; delay=8*0.85=6.8s; growth=1.2; cap=36
            // Sequence: 10, 12, 14, 17, 20, 24, 29, 35, 36, 36
            GameLoopProgressionConfigSO progression = AssetDatabase.LoadAssetAtPath<GameLoopProgressionConfigSO>(PROGRESSION_CONFIG_PATH);
            Assert.IsNotNull(progression);
            RunSessionService runSession = new RunSessionService(progression);
            runSession.ResetSessionToTier(1);
            WaveManagerHarness harness = CreateHarness(waveConfig: null, runSessionService: runSession);

            // Act — Wave 1 at 1s, then verify Wave 2 respects 6.8s delay (idle at 6.75s)
            AdvanceDelayAndTrigger(harness.WaveManager, 1f);
            harness.WaveManager.AdvanceWaves(6.75f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCounts.Count);

            harness.WaveManager.AdvanceWaves(0.125f);
            harness.WaveManager.AdvanceWaves(0.0625f);
            AdvanceWavesRepeated(harness.WaveManager, 6.85f, 8);

            // Assert
            CollectionAssert.AreEqual(
                new[] { 10, 12, 14, 17, 20, 24, 29, 35, 36, 36 },
                harness.EnemySpawner.SpawnCounts);
        }

        [Test]
        public void AuthoredAssets_GameLoopProgressionConfig_DefinesExpectedWaveMultipliersPerTier()
        {
            // Arrange
            GameLoopProgressionConfigSO progression = AssetDatabase.LoadAssetAtPath<GameLoopProgressionConfigSO>(PROGRESSION_CONFIG_PATH);
            Assert.IsNotNull(progression);

            // Act
            progression.ValidateConfiguration();
            LoopTierConfig tier1 = progression.GetTier(0);
            LoopTierConfig tier2 = progression.GetTier(1);

            // Assert — Tier 1
            Assert.AreEqual(1f, tier1.EnemySpawning.WaveInitialCountMultiplier, TOLERANCE);
            Assert.AreEqual(1f, tier1.EnemySpawning.WaveDelayMultiplier, TOLERANCE);
            Assert.AreEqual(1f, tier1.EnemySpawning.WaveGrowthMultiplier, TOLERANCE);
            Assert.AreEqual(45, tier1.EnemySpawning.MaxEnemiesInWaveCap);

            // Assert — Tier 2
            Assert.AreEqual(2.5f, tier2.EnemySpawning.WaveInitialCountMultiplier, TOLERANCE);
            Assert.AreEqual(0.85f, tier2.EnemySpawning.WaveDelayMultiplier, TOLERANCE);
            Assert.AreEqual(1f, tier2.EnemySpawning.WaveGrowthMultiplier, TOLERANCE);
            Assert.AreEqual(36, tier2.EnemySpawning.MaxEnemiesInWaveCap);
        }

        private WaveManagerHarness CreateHarness(WaveConfig waveConfig, IRunSessionService runSessionService)
        {
            FakeRandomWaveEnemySpawner enemySpawner = new FakeRandomWaveEnemySpawner();
            FakeTimeFreezeState timeFreeze = new FakeTimeFreezeState();

            ContainerBuilder builder = new ContainerBuilder().SetName("WaveManagerTestContainer");
            builder.AddSingleton(enemySpawner, typeof(IOnRandomGridPosSpawner<EnemiesSpawner>));
            builder.AddSingleton(timeFreeze, typeof(ITimeFreezeState));
            if (runSessionService != null)
            {
                builder.AddSingleton(runSessionService, typeof(IRunSessionService));
            }
            else
            {
                builder.AddSingleton<IRunSessionService>(delegate { return null; });
            }

            Container container = builder.Build();
            _createdContainers.Add(container);

            GameObject go = new GameObject("TestWaveManager");
            go.SetActive(false);
            _createdObjects.Add(go);

            WaveManager waveManager = go.AddComponent<WaveManager>();
            SetPrivateField(typeof(WaveManager), waveManager, "_config", waveConfig);

            GameObjectInjector.InjectRecursive(go, container);
            go.SetActive(true);
            InvokeLifecycleMethod(waveManager, "Start");

            return new WaveManagerHarness(waveManager, enemySpawner, timeFreeze);
        }

        private WaveConfig CreateWaveConfig(
            float firstWaveDelay,
            float waveDelay,
            ushort initialCount,
            float growthMultiplier)
        {
            WaveConfig config = ScriptableObject.CreateInstance<WaveConfig>();
            _createdObjects.Add(config);
            SetPrivateField(typeof(WaveConfig), config, "_firstWaveDelay", firstWaveDelay);
            SetPrivateField(typeof(WaveConfig), config, "_startSpawnWaveDelay", waveDelay);
            SetPrivateField(typeof(WaveConfig), config, "_initialMaxEnemiesInWave", initialCount);
            SetPrivateField(typeof(WaveConfig), config, "_maxEnemiesInWaveMultiplier", growthMultiplier);
            return config;
        }

        private GameLoopProgressionConfigSO CreateProgressionWithTierWaveSettings(
            float initialCountMultiplier,
            float delayMultiplier,
            float growthMultiplier,
            ushort maxEnemiesCap)
        {
            GameLoopProgressionConfigSO config = ScriptableObject.CreateInstance<GameLoopProgressionConfigSO>();
            _createdObjects.Add(config);

            LoopTierConfig tier = new LoopTierConfig();
            TierEnemySpawningConfig spawning = tier.EnemySpawning;
            SetPrivateField(typeof(TierEnemySpawningConfig), spawning, "_waveInitialCountMultiplier", initialCountMultiplier);
            SetPrivateField(typeof(TierEnemySpawningConfig), spawning, "_waveDelayMultiplier", delayMultiplier);
            SetPrivateField(typeof(TierEnemySpawningConfig), spawning, "_waveGrowthMultiplier", growthMultiplier);
            SetPrivateField(typeof(TierEnemySpawningConfig), spawning, "_maxEnemiesInWaveCap", maxEnemiesCap);

            SetPrivateField(typeof(GameLoopProgressionConfigSO), config, "_tiers", new List<LoopTierConfig> { tier });
            return config;
        }

        private static void AdvanceDelayAndTrigger(WaveManager waveManager, float exactDelay)
        {
            waveManager.AdvanceWaves(exactDelay);
            waveManager.AdvanceWaves(0.0625f);
        }

        private static void AdvanceWavesRepeated(WaveManager waveManager, float delayStep, int waveCount)
        {
            for (int i = 0; i < waveCount; i++)
            {
                AdvanceDelayAndTrigger(waveManager, delayStep);
            }
        }

        private static void AdvanceSteps(WaveManager waveManager, float stepDeltaTime, int stepCount)
        {
            for (int i = 0; i < stepCount; i++)
            {
                waveManager.AdvanceWaves(stepDeltaTime);
            }
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

        private readonly struct WaveManagerHarness
        {
            public readonly WaveManager WaveManager;
            public readonly FakeRandomWaveEnemySpawner EnemySpawner;
            public readonly FakeTimeFreezeState TimeFreeze;

            public WaveManagerHarness(
                WaveManager waveManager,
                FakeRandomWaveEnemySpawner enemySpawner,
                FakeTimeFreezeState timeFreeze)
            {
                WaveManager = waveManager;
                EnemySpawner = enemySpawner;
                TimeFreeze = timeFreeze;
            }
        }

        private sealed class FakeRandomWaveEnemySpawner : IOnRandomGridPosSpawner<EnemiesSpawner>
        {
            public List<int> SpawnCounts { get; } = new List<int>();
            public uint ActiveCount { get; set; }
            public uint CurrentlySpawnedObjectsCount { get { return ActiveCount; } }

            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public void SpawnAtRandomGridPos(int count = 1)
            {
                SpawnCounts.Add(count);
                ActiveCount += (uint)Mathf.Max(0, count);
            }
        }

        private sealed class FakeTimeFreezeState : ITimeFreezeState
        {
            public bool IsFrozen { get; set; }
            public float UnfrozenTime { get { return 0f; } }

            public event Action<bool> OnFrozenChanged
            {
                add { }
                remove { }
            }
        }

        private sealed class FakeNullTierRunSessionService : IRunSessionService
        {
            public int CurrentTierIndex { get { return 0; } }
            public int TotalTiersCount { get { return 1; } }
            public bool IsFinalTier { get { return true; } }
            public LoopTierConfig ActiveTierConfig { get { return null; } }
            public GameLoopProgressionConfigSO ProgressionConfig { get { return null; } }
            public bool HasPendingRestorationSnapshot { get { return false; } }
            public uint AccumulatedRunSeconds { get { return 0; } }
            public bool IsGodModeEnabled { get { return false; } }

            public void CaptureSnapshotAndAdvanceTier(IPlayerManager playerManager, uint elapsedSeconds)
            {
            }

            public void CaptureSnapshotAndSetTier(int tierIndex, IPlayerManager playerManager, uint elapsedSeconds)
            {
            }

            public void CaptureFinalRunTimer(uint elapsedSeconds)
            {
            }

            public bool TryGetRestoredLevel(out byte level, out float exp)
            {
                level = 1;
                exp = 0f;
                return false;
            }

            public bool TryGetRestoredSkills(out IReadOnlyList<SavedSkillSnapshot> skills)
            {
                skills = null;
                return false;
            }

            public void SetGodModeEnabled(bool isEnabled)
            {
            }

            public void ResetSession()
            {
            }

            public void ResetSessionToTier(int tierIndex)
            {
            }
        }
    }
}
