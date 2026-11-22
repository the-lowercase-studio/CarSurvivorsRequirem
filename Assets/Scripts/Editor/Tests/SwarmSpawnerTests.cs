using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.Player;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Spawners;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.Spawners.Swarm;
using Assets.Scripts.UI.HUD;
using Assets.Scripts.Waves;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class SwarmSpawnerTests
    {
        private const float TOLERANCE = 0.0001f;
        private const int DEFAULT_RANDOM_SEED = 424242;

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();
        private UnityEngine.Random.State _previousRandomState;

        [SetUp]
        public void SetUp()
        {
            _previousRandomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(DEFAULT_RANDOM_SEED);
        }

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
            UnityEngine.Random.state = _previousRandomState;
        }

        [Test]
        public void Start_WithoutRunSessionService_UsesBaseIntervalAndSizeBounds()
        {
            // Arrange
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 20f,
                maxInterval: 30f,
                minSize: 40,
                maxSize: 50,
                warningDuration: 0f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("BaseSwarmEnemy", 100) });

            // Act — Advance just below minimum interval, then across maximum interval
            harness.Spawner.AdvanceSwarm(19.99f);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);

            harness.Spawner.AdvanceSwarm(10.02f);

            // Assert
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);
            int spawnedCount = harness.EnemySpawner.SpawnCalls[0].Count;
            Assert.GreaterOrEqual(spawnedCount, 40);
            Assert.LessOrEqual(spawnedCount, 50);
        }

        [Test]
        public void Start_WithTierScalingMultipliers_ScalesIntervalAndRoundsSize()
        {
            // Arrange — 100 * 0.85 = 85s interval; 45 * 1.25 = 56.25 -> rounds to 56
            GameLoopProgressionConfigSO progression = CreateProgressionConfig(0.85f, 1.25f);
            RunSessionService runSession = new RunSessionService(progression);
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 100f,
                maxInterval: 100f,
                minSize: 45,
                maxSize: 45,
                warningDuration: 0f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: runSession,
                configs: new[] { CreateSpawnInfo("ScaledEnemy", 200) });

            // Act & Assert — Still idle just before 85s
            harness.Spawner.AdvanceSwarm(84.5f);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCalls.Count);

            // Act & Assert — Triggers at 85s (+epsilon for float32 Random.Range(a, a) rounding) with rounded size 56
            harness.Spawner.AdvanceSwarm(0.51f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(56, harness.EnemySpawner.SpawnCalls[0].Count);
        }

        [Test]
        public void Start_WithSubMinimumMultipliers_ClampsIntervalToFiveAndSizeToOne()
        {
            // Arrange — 0.01 clamps to 0.1 multiplier; 20 * 0.1 = 2 clamps to 5s; 4 * 0.1 = 0.4 -> 0 clamps to 1
            GameLoopProgressionConfigSO progression = CreateProgressionConfig(0.01f, 0.01f);
            RunSessionService runSession = new RunSessionService(progression);
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 20f,
                maxInterval: 20f,
                minSize: 4,
                maxSize: 4,
                warningDuration: 0f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: runSession,
                configs: new[] { CreateSpawnInfo("ClampedEnemy", 50) });

            // Act & Assert — Remains idle at 4.75s
            harness.Spawner.AdvanceSwarm(4.75f);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCalls.Count);

            // Act & Assert — Triggers at 5.0s with minimum size 1
            harness.Spawner.AdvanceSwarm(0.26f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls[0].Count);
        }

        [Test]
        public void Start_WithInvertedMinGreaterThanMax_ClampsEffectiveMaxToEffectiveMin()
        {
            // Arrange — Inverted bounds (min 30 > max 10, minSize 12 > maxSize 4) clamp max up to min
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 30f,
                maxInterval: 10f,
                minSize: 12,
                maxSize: 4,
                warningDuration: 0f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("InvertedBoundsEnemy", 50) });

            // Act & Assert — Must wait full 30s, not 10s
            harness.Spawner.AdvanceSwarm(29.75f);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCalls.Count);

            harness.Spawner.AdvanceSwarm(0.26f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(12, harness.EnemySpawner.SpawnCalls[0].Count);
        }

        [TestCase(0f, TestName = "AdvanceSwarm_ZeroDeltaTime_DoesNotAdvanceCooldown")]
        [TestCase(-1f, TestName = "AdvanceSwarm_NegativeDeltaTime_DoesNotAdvanceCooldown")]
        [TestCase(float.NaN, TestName = "AdvanceSwarm_NaNDeltaTime_DoesNotAdvanceCooldown")]
        [TestCase(float.PositiveInfinity, TestName = "AdvanceSwarm_PositiveInfinityDeltaTime_DoesNotAdvanceCooldown")]
        [TestCase(float.NegativeInfinity, TestName = "AdvanceSwarm_NegativeInfinityDeltaTime_DoesNotAdvanceCooldown")]
        public void AdvanceSwarm_InvalidDeltaTime_DoesNotAdvanceCooldownOrTriggerSwarm(float invalidDeltaTime)
        {
            // Arrange — Clamped minimum interval is 5s
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 5,
                maxSize: 5,
                warningDuration: 1f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("GuardEnemy", 20) });

            // Act — Invalid deltaTime is ignored; 4.75s leaves 0.25s, then 0.26s triggers exactly on schedule
            harness.Spawner.AdvanceSwarm(invalidDeltaTime);
            harness.Spawner.AdvanceSwarm(4.75f);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
            Assert.AreEqual(0, harness.Presenter.IncomingSeconds.Count);

            harness.Spawner.AdvanceSwarm(0.26f);

            // Assert
            Assert.IsTrue(harness.WaveFreezer.IsFrozen);
            CollectionAssert.AreEqual(new[] { 1 }, harness.Presenter.IncomingSeconds);
        }

        [Test]
        public void AdvanceSwarm_WhenSuppressedDuringCooldown_PausesCountdownUntilReleased()
        {
            // Arrange
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 10f,
                maxInterval: 10f,
                minSize: 6,
                maxSize: 6,
                warningDuration: 1f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("SuppressedEnemy", 20) });

            // Act — Suppress and advance well beyond 10s
            harness.Spawner.IsSuppressed = true;
            AdvanceSteps(harness.Spawner, 5f, 5);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
            Assert.AreEqual(0, harness.Presenter.IncomingSeconds.Count);

            // Act — Release suppression and advance up to just before boundary, then trigger
            harness.Spawner.IsSuppressed = false;
            harness.Spawner.AdvanceSwarm(9.75f);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);

            harness.Spawner.AdvanceSwarm(0.25f);

            // Assert
            Assert.IsTrue(harness.WaveFreezer.IsFrozen);
            CollectionAssert.AreEqual(new[] { 1 }, harness.Presenter.IncomingSeconds);
        }

        [Test]
        public void AdvanceSwarm_WhenTimeFrozenDuringCooldown_PausesCountdownUntilUnfrozen()
        {
            // Arrange
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 10f,
                maxInterval: 10f,
                minSize: 6,
                maxSize: 6,
                warningDuration: 1f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("FrozenCooldownEnemy", 20) });

            // Act — Freeze world time and advance 25s
            harness.TimeFreeze.IsFrozen = true;
            AdvanceSteps(harness.Spawner, 5f, 5);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);

            // Act — Unfreeze and verify full 10s remains
            harness.TimeFreeze.IsFrozen = false;
            harness.Spawner.AdvanceSwarm(9.75f);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);

            harness.Spawner.AdvanceSwarm(0.25f);

            // Assert
            Assert.IsTrue(harness.WaveFreezer.IsFrozen);
            CollectionAssert.AreEqual(new[] { 1 }, harness.Presenter.IncomingSeconds);
        }

        [Test]
        public void AdvanceSwarm_WhenEnemyConfigsEmpty_DoesNotFreezeWavesOrNotifyPresenter()
        {
            // Arrange
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 5,
                maxSize: 5,
                warningDuration: 2f,
                swarmDuration: 2f,
                tickInterval: 1f,
                runSessionService: null,
                configs: Array.Empty<EnemySpawnInfo>());

            // Act
            harness.Spawner.AdvanceSwarm(10f);

            // Assert
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
            Assert.AreEqual(0, harness.Presenter.IncomingSeconds.Count);
            Assert.AreEqual(0, harness.Presenter.OngoingCount);
            Assert.AreEqual(0, harness.Presenter.HideCount);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCalls.Count);
            FieldInfo nextSwarmField = typeof(SwarmSpawner).GetField("_nextSwarmTime", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.AreEqual(5f, (float)nextSwarmField.GetValue(harness.Spawner), TOLERANCE);
        }

        [Test]
        public void StartSwarm_MultipleSwarms_AdvancesEnemyIndexSequentiallyAndClampsToLastConfig()
        {
            // Arrange
            EnemySpawnInfo firstConfig = CreateSpawnInfo("SwarmEnemyOne", 20);
            EnemySpawnInfo secondConfig = CreateSpawnInfo("SwarmEnemyTwo", 20);
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 4,
                maxSize: 4,
                warningDuration: 0f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { firstConfig, secondConfig });

            // Act — Execute 3 full swarm cycles (single-tick immediate completion after 5s cooldown)
            harness.Spawner.AdvanceSwarm(5f);
            harness.Spawner.AdvanceSwarm(5f);
            harness.Spawner.AdvanceSwarm(5f);

            // Assert
            Assert.AreEqual(3, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreSame(firstConfig, harness.EnemySpawner.SpawnCalls[0].Info);
            Assert.AreSame(secondConfig, harness.EnemySpawner.SpawnCalls[1].Info);
            Assert.AreSame(secondConfig, harness.EnemySpawner.SpawnCalls[2].Info);
        }

        [Test]
        public void StartSwarm_RolledSizeExceedsSelectedConfigMaxAmount_ClampsToMaxAmount()
        {
            // Arrange
            EnemySpawnInfo cappedConfig = CreateSpawnInfo("CappedPoolEnemy", 12);
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 50,
                maxSize: 50,
                warningDuration: 0f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { cappedConfig });

            // Act
            harness.Spawner.AdvanceSwarm(5f);

            // Assert
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(12, harness.EnemySpawner.SpawnCalls[0].Count);
        }

        [Test]
        public void StartSwarm_SelectedConfigMaxAmountZero_CompletesLifecycleWithoutSpawningEnemies()
        {
            // Arrange
            EnemySpawnInfo zeroCapConfig = CreateSpawnInfo("ZeroCapEnemy", 0);
            EnemySpawnInfo nextConfig = CreateSpawnInfo("NextEnemy", 10);
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 10,
                maxSize: 10,
                warningDuration: 0f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { zeroCapConfig, nextConfig });

            // Act — Swarm 1 has MaxAmount 0; Swarm 2 advances to nextConfig
            harness.Spawner.AdvanceSwarm(5f);
            Assert.AreEqual(0, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(1, harness.Presenter.OngoingCount);
            Assert.AreEqual(1, harness.Presenter.HideCount);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);

            harness.Spawner.AdvanceSwarm(5f);

            // Assert
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreSame(nextConfig, harness.EnemySpawner.SpawnCalls[0].Info);
            Assert.AreEqual(10, harness.EnemySpawner.SpawnCalls[0].Count);
        }

        [Test]
        public void WarningPhase_NonIntegerDuration_EmitsCeilingSecondsWithoutZeroBeforeOngoing()
        {
            // Arrange — 2.3s warning emits Ceil(2.3)=3 on trigger, then 2 at 1.8s, 1 at 0.8s, and Ongoing at -0.2s
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 5,
                maxSize: 5,
                warningDuration: 2.3f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("WarningEnemy", 20) });

            // Act
            harness.Spawner.AdvanceSwarm(5f);
            AdvanceSteps(harness.Spawner, 0.5f, 5);

            // Assert
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, harness.Presenter.IncomingSeconds);
            Assert.AreEqual(1, harness.Presenter.OngoingCount);
            Assert.AreEqual(1, harness.Presenter.HideCount);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
        }

        [Test]
        public void WarningPhase_LargeStepSkippingSeconds_UpdatesToCurrentSecondAndTransitionsCleanly()
        {
            // Arrange
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 5,
                maxSize: 5,
                warningDuration: 5f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("SkipSecondsEnemy", 20) });

            // Act — Trigger (5s), step 3.5s -> 1.5s left (Ceil = 2), then step 2.0s -> -0.5s (Ongoing)
            harness.Spawner.AdvanceSwarm(5f);
            harness.Spawner.AdvanceSwarm(3.5f);
            Assert.AreEqual(0, harness.Presenter.OngoingCount);

            harness.Spawner.AdvanceSwarm(2.0f);

            // Assert
            CollectionAssert.AreEqual(new[] { 5, 2 }, harness.Presenter.IncomingSeconds);
            Assert.AreEqual(1, harness.Presenter.OngoingCount);
            Assert.AreEqual(1, harness.Presenter.HideCount);
        }

        [Test]
        public void WarningPhase_ZeroDuration_TransitionsDirectlyToOngoingAndSpawnsImmediately()
        {
            // Arrange
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 8,
                maxSize: 8,
                warningDuration: 0f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("ZeroWarningEnemy", 20) });

            // Act
            harness.Spawner.AdvanceSwarm(5f);

            // Assert
            CollectionAssert.AreEqual(new[] { 0 }, harness.Presenter.IncomingSeconds);
            Assert.AreEqual(1, harness.Presenter.OngoingCount);
            Assert.AreEqual(1, harness.Presenter.HideCount);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(8, harness.EnemySpawner.SpawnCalls[0].Count);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
        }

        [Test]
        public void WarningPhase_WhenTimeFrozen_PausesCountdownAndResumesWhenUnfrozen()
        {
            // Arrange
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 5,
                maxSize: 5,
                warningDuration: 3f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("FrozenWarningEnemy", 20) });

            // Act — Trigger warning (3s), freeze and advance 5s
            harness.Spawner.AdvanceSwarm(5f);
            harness.TimeFreeze.IsFrozen = true;
            AdvanceSteps(harness.Spawner, 1f, 5);

            Assert.AreEqual(1, harness.Presenter.IncomingSeconds.Count);
            Assert.AreEqual(0, harness.Presenter.OngoingCount);
            Assert.IsTrue(harness.WaveFreezer.IsFrozen);

            // Act — Unfreeze and step through the remaining 3s warning
            harness.TimeFreeze.IsFrozen = false;
            AdvanceSteps(harness.Spawner, 1f, 3);

            // Assert
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, harness.Presenter.IncomingSeconds);
            Assert.AreEqual(1, harness.Presenter.OngoingCount);
            Assert.AreEqual(1, harness.Presenter.HideCount);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
        }

        [Test]
        public void SpawningPhase_NonDivisibleTotalAcrossTicks_ConservesExactTotalAndEndsOnFinalTick()
        {
            // Arrange — 10 enemies across 3 ticks (3s / 1s):
            // Tick 0: Round(10 * 1 / 3 = 3.333) = 3 -> +3
            // Tick 1: Round(10 * 2 / 3 = 6.667) = 7 -> +4
            // Tick 2: Round(10 * 3 / 3 = 10.0)  = 10 -> +3; sum = 10
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 10,
                maxSize: 10,
                warningDuration: 1f,
                swarmDuration: 3f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("TickDistributedEnemy", 50) });

            // Act — Trigger cooldown, complete 1s warning (runs Tick 0 immediately)
            harness.Spawner.AdvanceSwarm(5f);
            harness.Spawner.AdvanceSwarm(1f);

            Assert.AreEqual(1, harness.Presenter.OngoingCount);
            Assert.AreEqual(0, harness.Presenter.HideCount);
            Assert.IsTrue(harness.WaveFreezer.IsFrozen);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(3, harness.EnemySpawner.SpawnCalls[0].Count);

            // Act — Tick 1 at +1s
            harness.Spawner.AdvanceSwarm(1f);
            Assert.AreEqual(0, harness.Presenter.HideCount);
            Assert.IsTrue(harness.WaveFreezer.IsFrozen);
            Assert.AreEqual(2, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(4, harness.EnemySpawner.SpawnCalls[1].Count);

            // Act — Tick 2 (final tick) at +1s ends swarm immediately
            harness.Spawner.AdvanceSwarm(1f);

            // Assert
            Assert.AreEqual(3, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(3, harness.EnemySpawner.SpawnCalls[2].Count);
            Assert.AreEqual(10, harness.EnemySpawner.TotalSpawnedCount);
            Assert.AreEqual(1, harness.Presenter.HideCount);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
        }

        [Test]
        public void SpawningPhase_DurationShorterThanHalfTick_ExecutesSingleTickAndEndsImmediately()
        {
            // Arrange — 0.2s / 1.0s = 0.2 -> RoundToInt = 0 -> clamped to 1 tick
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 7,
                maxSize: 7,
                warningDuration: 1f,
                swarmDuration: 0.2f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("SingleTickEnemy", 20) });

            // Act — Trigger swarm then complete 1s warning
            harness.Spawner.AdvanceSwarm(5f);
            harness.Spawner.AdvanceSwarm(1f);

            // Assert
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(7, harness.EnemySpawner.SpawnCalls[0].Count);
            Assert.AreEqual(1, harness.Presenter.HideCount);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
        }

        [Test]
        public void SpawningPhase_WhenTimeFrozenBetweenTicks_PausesTickWaitUntilUnfrozen()
        {
            // Arrange — 2 ticks across 2s/1s
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 6,
                maxSize: 6,
                warningDuration: 1f,
                swarmDuration: 2f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("FrozenSpawningEnemy", 20) });

            // Act — Reach Tick 0
            harness.Spawner.AdvanceSwarm(5f);
            harness.Spawner.AdvanceSwarm(1f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);

            // Act — Freeze and advance 4s without firing Tick 1
            harness.TimeFreeze.IsFrozen = true;
            AdvanceSteps(harness.Spawner, 1f, 4);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(0, harness.Presenter.HideCount);
            Assert.IsTrue(harness.WaveFreezer.IsFrozen);

            // Act — Unfreeze and complete remaining 1s tick interval
            harness.TimeFreeze.IsFrozen = false;
            harness.Spawner.AdvanceSwarm(1f);

            // Assert
            Assert.AreEqual(2, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(6, harness.EnemySpawner.TotalSpawnedCount);
            Assert.AreEqual(1, harness.Presenter.HideCount);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
        }

        [Test]
        public void EndSwarm_ResetsCooldownIntoScaledRangeAndAllowsSubsequentSwarm()
        {
            // Arrange
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 10f,
                maxInterval: 10f,
                minSize: 4,
                maxSize: 4,
                warningDuration: 0f,
                swarmDuration: 1f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("FirstEnemy", 10), CreateSpawnInfo("SecondEnemy", 10) });

            // Act — First swarm at 10s, then advance 9.75s (still in cooldown), then 0.25s
            harness.Spawner.AdvanceSwarm(10f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);

            harness.Spawner.AdvanceSwarm(9.75f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);

            harness.Spawner.AdvanceSwarm(0.25f);

            // Assert
            Assert.AreEqual(2, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(8, harness.EnemySpawner.TotalSpawnedCount);
            Assert.AreEqual(2, harness.Presenter.HideCount);
        }

        [Test]
        public void StartAndEndSwarm_EqualMinMaxIntervalWithOvershootRandomSeed_TriggersAtExactBoundary()
        {
            // Arrange — Seed 6 previously caused UnityEngine.Random.Range(5f, 5f) to round to 5.00000048f > 5f
            UnityEngine.Random.InitState(6);
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 4,
                maxSize: 4,
                warningDuration: 0f,
                swarmDuration: 2f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("SeedSixEnemy", 10) });

            // Act & Assert — Initial trigger occurs at exact 5f
            harness.Spawner.AdvanceSwarm(5f);
            Assert.AreEqual(1, harness.EnemySpawner.SpawnCalls.Count);

            // Re-seed to 6 immediately before the final tick calls EndSwarm(), then verify next cycle triggers at exact 5f
            UnityEngine.Random.InitState(6);
            harness.Spawner.AdvanceSwarm(1f);
            Assert.AreEqual(2, harness.EnemySpawner.SpawnCalls.Count);

            harness.Spawner.AdvanceSwarm(5f);
            Assert.AreEqual(3, harness.EnemySpawner.SpawnCalls.Count);
        }

        [TestCase(false, TestName = "OnDisable_DuringWarningPhase_UnfreezesWavesAndAbortsSwarm")]
        [TestCase(true, TestName = "OnDisable_DuringSpawningPhase_UnfreezesWavesAndAbortsSwarm")]
        public void OnDisable_DuringActiveSwarm_UnfreezesWavesAndDoesNotResumeOnReenable(bool advanceIntoSpawning)
        {
            // Arrange
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 5f,
                maxInterval: 5f,
                minSize: 9,
                maxSize: 9,
                warningDuration: 2f,
                swarmDuration: 3f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("DisableEnemy", 30) });

            AdvanceToActivePhase(harness.Spawner, advanceIntoSpawning);

            Assert.IsTrue(harness.WaveFreezer.IsFrozen);
            int callsBeforeDisable = harness.EnemySpawner.SpawnCalls.Count;
            int ongoingBeforeDisable = harness.Presenter.OngoingCount;

            // Act — Disable resets active swarm state back to Idle and unfreezes waves;
            // suppressing cooldown afterward proves active warning/spawning steps do not continue.
            InvokeLifecycleMethod(harness.Spawner, "OnDisable");
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);

            harness.Spawner.IsSuppressed = true;
            AdvanceSteps(harness.Spawner, 1f, 3);

            // Assert — Old warning/spawning ticks never resume and notification is hidden
            Assert.AreEqual(callsBeforeDisable, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(ongoingBeforeDisable, harness.Presenter.OngoingCount);
            Assert.AreEqual(1, harness.Presenter.HideCount);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
        }

        [TestCase(false, TestName = "IsSuppressed_DuringWarningPhase_CancelsSwarmHidesPresenterAndUnfreezesWaves")]
        [TestCase(true, TestName = "IsSuppressed_DuringSpawningPhase_CancelsSwarmHidesPresenterAndUnfreezesWaves")]
        public void IsSuppressed_DuringActiveSwarm_CancelsSwarmHidesPresenterAndUnfreezesWaves(bool advanceIntoSpawning)
        {
            // Arrange
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 10f,
                maxInterval: 10f,
                minSize: 9,
                maxSize: 9,
                warningDuration: 2f,
                swarmDuration: 3f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("SuppressedActiveEnemy", 30) });

            harness.Spawner.AdvanceSwarm(10f);
            if (advanceIntoSpawning)
            {
                harness.Spawner.AdvanceSwarm(2f);
            }

            Assert.IsTrue(harness.WaveFreezer.IsFrozen);
            int callsBeforeSuppress = harness.EnemySpawner.SpawnCalls.Count;
            int ongoingBeforeSuppress = harness.Presenter.OngoingCount;

            // Act — Boss encounter suppresses swarm mid-event
            harness.Spawner.IsSuppressed = true;
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
            Assert.AreEqual(1, harness.Presenter.HideCount);

            AdvanceSteps(harness.Spawner, 1f, 5);
            Assert.AreEqual(callsBeforeSuppress, harness.EnemySpawner.SpawnCalls.Count);
            Assert.AreEqual(ongoingBeforeSuppress, harness.Presenter.OngoingCount);

            // Release suppression -> waits full reset 10s interval rather than resuming old swarm
            harness.Spawner.IsSuppressed = false;
            harness.Spawner.AdvanceSwarm(9.5f);
            Assert.IsFalse(harness.WaveFreezer.IsFrozen);
            harness.Spawner.AdvanceSwarm(0.5f);

            // Assert
            Assert.IsTrue(harness.WaveFreezer.IsFrozen);
        }

        [Test]
        public void OnDisable_WhenIdle_PreservesExternalWaveFreezeState()
        {
            // Arrange
            SwarmSpawnerHarness harness = CreateHarness(
                minInterval: 20f,
                maxInterval: 20f,
                minSize: 5,
                maxSize: 5,
                warningDuration: 2f,
                swarmDuration: 2f,
                tickInterval: 1f,
                runSessionService: null,
                configs: new[] { CreateSpawnInfo("IdleDisableEnemy", 10) });
            harness.WaveFreezer.IsFrozen = true;

            // Act
            InvokeLifecycleMethod(harness.Spawner, "OnDisable");

            // Assert
            Assert.IsTrue(harness.WaveFreezer.IsFrozen);
        }

        private SwarmSpawnerHarness CreateHarness(
            float minInterval,
            float maxInterval,
            int minSize,
            int maxSize,
            float warningDuration,
            float swarmDuration,
            float tickInterval,
            IRunSessionService runSessionService,
            EnemySpawnInfo[] configs)
        {
            FakeSwarmEnemySpawner enemySpawner = new FakeSwarmEnemySpawner(configs);
            FakeWaveFreezer waveFreezer = new FakeWaveFreezer();
            RecordingSwarmNotificationPresenter presenter = new RecordingSwarmNotificationPresenter();
            FakeTimeFreezeState timeFreeze = new FakeTimeFreezeState();

            ContainerBuilder builder = new ContainerBuilder().SetName("SwarmSpawnerTestContainer");
            builder.AddSingleton(enemySpawner, typeof(ISwarmEnemySpawner));
            builder.AddSingleton(waveFreezer, typeof(IWaveFreezer));
            builder.AddSingleton(presenter, typeof(ISwarmNotificationPresenter));
            builder.AddSingleton(timeFreeze, typeof(ITimeFreezeState));
            if (runSessionService != null)
            {
                builder.AddSingleton(runSessionService, typeof(IRunSessionService));
            }
            else
            {
                builder.AddSingleton(new FakeNullTierRunSessionService(), typeof(IRunSessionService));
            }

            Container container = builder.Build();
            _createdContainers.Add(container);

            GameObject go = new GameObject("TestSwarmSpawner");
            go.SetActive(false);
            _createdObjects.Add(go);

            SwarmSpawner spawner = go.AddComponent<SwarmSpawner>();
            SetPrivateField(typeof(SwarmSpawner), spawner, "_minSwarmInterval", minInterval);
            SetPrivateField(typeof(SwarmSpawner), spawner, "_maxSwarmInterval", maxInterval);
            SetPrivateField(typeof(SwarmSpawner), spawner, "_minSwarmSize", minSize);
            SetPrivateField(typeof(SwarmSpawner), spawner, "_maxSwarmSize", maxSize);
            SetPrivateField(typeof(SwarmSpawner), spawner, "_swarmWarningDuration", warningDuration);
            SetPrivateField(typeof(SwarmSpawner), spawner, "_swarmDuration", swarmDuration);
            SetPrivateField(typeof(SwarmSpawner), spawner, "_spawnTickInterval", tickInterval);

            GameObjectInjector.InjectRecursive(go, container);
            go.SetActive(true);
            InvokeLifecycleMethod(spawner, "Start");

            return new SwarmSpawnerHarness(spawner, enemySpawner, waveFreezer, presenter, timeFreeze);
        }

        private GameLoopProgressionConfigSO CreateProgressionConfig(float swarmIntervalMultiplier, float swarmSizeMultiplier)
        {
            GameLoopProgressionConfigSO config = ScriptableObject.CreateInstance<GameLoopProgressionConfigSO>();
            _createdObjects.Add(config);

            LoopTierConfig tier = new LoopTierConfig();
            TierEnemySpawningConfig spawning = tier.EnemySpawning;
            SetPrivateField(typeof(TierEnemySpawningConfig), spawning, "_swarmIntervalMultiplier", swarmIntervalMultiplier);
            SetPrivateField(typeof(TierEnemySpawningConfig), spawning, "_swarmSizeMultiplier", swarmSizeMultiplier);

            List<LoopTierConfig> tiers = new List<LoopTierConfig> { tier };
            SetPrivateField(typeof(GameLoopProgressionConfigSO), config, "_tiers", tiers);
            return config;
        }

        private EnemySpawnInfo CreateSpawnInfo(string enemyName, ushort maxAmount)
        {
            GameObject prefabGo = new GameObject(enemyName);
            prefabGo.SetActive(false);
            _createdObjects.Add(prefabGo);
            Enemy enemy = prefabGo.AddComponent<Enemy>();
            return new EnemySpawnInfo(enemy, maxAmount, new SpawnChanceInfo { SpawnChance = 100f });
        }

        private static void AdvanceToActivePhase(SwarmSpawner spawner, bool advanceIntoSpawning)
        {
            spawner.AdvanceSwarm(5f);
            if (advanceIntoSpawning)
            {
                spawner.AdvanceSwarm(2f);
            }
        }

        private static void AdvanceSteps(SwarmSpawner spawner, float stepDeltaTime, int stepCount)
        {
            for (int i = 0; i < stepCount; i++)
            {
                spawner.AdvanceSwarm(stepDeltaTime);
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

        private readonly struct SwarmSpawnerHarness
        {
            public readonly SwarmSpawner Spawner;
            public readonly FakeSwarmEnemySpawner EnemySpawner;
            public readonly FakeWaveFreezer WaveFreezer;
            public readonly RecordingSwarmNotificationPresenter Presenter;
            public readonly FakeTimeFreezeState TimeFreeze;

            public SwarmSpawnerHarness(
                SwarmSpawner spawner,
                FakeSwarmEnemySpawner enemySpawner,
                FakeWaveFreezer waveFreezer,
                RecordingSwarmNotificationPresenter presenter,
                FakeTimeFreezeState timeFreeze)
            {
                Spawner = spawner;
                EnemySpawner = enemySpawner;
                WaveFreezer = waveFreezer;
                Presenter = presenter;
                TimeFreeze = timeFreeze;
            }
        }

        private readonly struct RecordedSwarmSpawnCall
        {
            public readonly EnemySpawnInfo Info;
            public readonly int Count;

            public RecordedSwarmSpawnCall(EnemySpawnInfo info, int count)
            {
                Info = info;
                Count = count;
            }
        }

        private sealed class FakeSwarmEnemySpawner : ISwarmEnemySpawner
        {
            private readonly List<EnemySpawnInfo> _configs;

            public List<RecordedSwarmSpawnCall> SpawnCalls { get; } = new List<RecordedSwarmSpawnCall>();
            public int TotalSpawnedCount { get; private set; }
            public IReadOnlyList<EnemySpawnInfo> EnemyConfigs { get { return _configs; } }

            public FakeSwarmEnemySpawner(EnemySpawnInfo[] configs)
            {
                _configs = new List<EnemySpawnInfo>();
                if (configs != null)
                {
                    for (int i = 0; i < configs.Length; i++)
                    {
                        _configs.Add(configs[i]);
                    }
                }
            }

            public void SpawnSpecificEnemy(EnemySpawnInfo enemyInfo, int count = 1)
            {
                SpawnCalls.Add(new RecordedSwarmSpawnCall(enemyInfo, count));
                TotalSpawnedCount += count;
            }

            public bool TrySpawnEnemyAt(EnemySpawnInfo enemyInfo, Vector3 position, out Enemy enemy)
            {
                enemy = null;
                return false;
            }
        }

        private sealed class FakeWaveFreezer : IWaveFreezer
        {
            public bool IsFrozen { get; set; }
        }

        private sealed class RecordingSwarmNotificationPresenter : ISwarmNotificationPresenter
        {
            public List<int> IncomingSeconds { get; } = new List<int>();
            public int OngoingCount { get; private set; }
            public int HideCount { get; private set; }

            public void ShowIncoming(int secondsRemaining)
            {
                IncomingSeconds.Add(secondsRemaining);
            }

            public void ShowOngoing()
            {
                OngoingCount++;
            }

            public void Hide()
            {
                HideCount++;
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
