using System.Collections.Generic;
using Assets.Scripts.Common.Types;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.Spawners;
using Assets.Scripts.Spawners.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class EnemiesSpawnChanceRedistributionTests
    {
        private const string PROGRESSION_CONFIG_PATH = "Assets/ScriptableObjects/GameFlow/GameLoopProgressionConfig.asset";
        private const float TOLERANCE = 0.0001f;
        private const int DEFAULT_RANDOM_SEED = 424242;

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
            UnityEngine.Random.state = _previousRandomState;
        }

        [TestCase(24.9f, 25f, false, false, TestName = "HasReachedThreshold_BelowThreshold_ReturnsFalse")]
        [TestCase(25f, 25f, false, true, TestName = "HasReachedThreshold_AtExactThreshold_ReturnsTrue")]
        [TestCase(25.1f, 25f, false, true, TestName = "HasReachedThreshold_AboveThreshold_ReturnsTrue")]
        [TestCase(100f, 0f, true, false, TestName = "HasReachedThreshold_WhenSpawnChanceWillNotChange_ReturnsFalse")]
        public void SpawnChanceInfo_HasReachedThreshold_EvaluatesBoundaryAndWillNotChangeFlag(
            float spawnChance,
            float threshold,
            bool willNotChange,
            bool expectedResult)
        {
            // Arrange
            SpawnChanceInfo info = new SpawnChanceInfo
            {
                SpawnChance = spawnChance,
                TresholdToStartAddingSpawnChanceToOtherInfos = threshold,
                SpawnChanceWillNotChange = willNotChange
            };

            // Act
            bool reached = info.HasReachedTresholdToStartAddingSpawnChanceToOtherInfos();

            // Assert
            Assert.AreEqual(expectedResult, reached);
        }

        [Test]
        public void EnemySpawnInfo_CreateRuntimeCopy_WhenSourceSpawnChanceInfoIsNull_CreatesFreshDefault()
        {
            // Arrange
            EnemySpawnInfo source = new EnemySpawnInfo(null, 20, null);

            // Act
            EnemySpawnInfo copy = source.CreateRuntimeCopy();

            // Assert
            Assert.AreEqual(20, copy.MaxAmount);
            Assert.IsNotNull(copy.SpawnChanceInfo);
            Assert.AreEqual(0f, copy.SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(0f, copy.SpawnChanceInfo.TresholdToStartAddingSpawnChanceToOtherInfos, TOLERANCE);
            Assert.IsFalse(copy.SpawnChanceInfo.SpawnChanceWillNotChange);
            Assert.IsFalse(copy.SpawnChanceInfo.HasEverReachedThreshold);
        }

        [Test]
        public void RedistributeSpawnChance_BeforeInitialize_IsSafeNoOp()
        {
            // Arrange
            EnemiesSpawnChanceRedistributionSystem system = new EnemiesSpawnChanceRedistributionSystem();

            // Assert
            Assert.IsFalse(system.IsInitialized());
            Assert.DoesNotThrow(() => system.RedistributeSpawnChance());
        }

        [Test]
        public void RedistributeSpawnChance_WithEmptyList_SetsInitializedAndIsSafeNoOp()
        {
            // Arrange
            EnemiesSpawnChanceRedistributionSystem system = CreateSystem(new List<EnemySpawnInfo>(), 2f, 2f);

            // Assert
            Assert.IsTrue(system.IsInitialized());
            Assert.DoesNotThrow(() => system.RedistributeSpawnChance());
        }

        [Test]
        public void RedistributeSpawnChance_WithAllFixedEntries_PreservesAllChances()
        {
            // Arrange
            List<EnemySpawnInfo> entries = new List<EnemySpawnInfo>
            {
                CreateEntry(60f, 0f, willNotChange: true),
                CreateEntry(40f, 0f, willNotChange: true)
            };
            EnemiesSpawnChanceRedistributionSystem system = CreateSystem(entries, 10f, 10f);

            // Act
            system.RedistributeSpawnChance();

            // Assert
            Assert.AreEqual(60f, entries[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(40f, entries[1].SpawnChanceInfo.SpawnChance, TOLERANCE);
        }

        [Test]
        public void RedistributeSpawnChance_WithSingleMutableEntry_PreservesChance()
        {
            // Arrange
            List<EnemySpawnInfo> entries = new List<EnemySpawnInfo>
            {
                CreateEntry(100f, 0f, willNotChange: false),
                CreateEntry(25f, 0f, willNotChange: true)
            };
            EnemiesSpawnChanceRedistributionSystem system = CreateSystem(entries, 10f, 10f);

            // Act
            system.RedistributeSpawnChance();

            // Assert
            Assert.AreEqual(100f, entries[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(25f, entries[1].SpawnChanceInfo.SpawnChance, TOLERANCE);
        }

        [Test]
        public void RedistributeSpawnChance_WithFixedAndNullChanceEntries_ExcludesThemAndPreservesFixedChance()
        {
            // Arrange
            List<EnemySpawnInfo> entries = new List<EnemySpawnInfo>
            {
                CreateEntry(100f, 0f, willNotChange: false),
                CreateEntry(50f, 0f, willNotChange: true),
                new EnemySpawnInfo(null, 10, null),
                CreateEntry(0f, 0f, willNotChange: false)
            };
            EnemiesSpawnChanceRedistributionSystem system = CreateSystem(entries, 10f, 10f);

            // Act
            system.RedistributeSpawnChance();

            // Assert
            Assert.AreEqual(90f, entries[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(50f, entries[1].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.IsNull(entries[2].SpawnChanceInfo);
            Assert.AreEqual(10f, entries[3].SpawnChanceInfo.SpawnChance, TOLERANCE);
        }

        [Test]
        public void RedistributeSpawnChance_WhenCurrentSourceBelowThreshold_RoutesFullStepToLastMutableEntry()
        {
            // Arrange — Entry 0 (50 < 60 threshold) cannot unlock downstream cascade -> 100% of 10 goes to index 2
            List<EnemySpawnInfo> entries = new List<EnemySpawnInfo>
            {
                CreateEntry(50f, 60f, willNotChange: false),
                CreateEntry(30f, 0f, willNotChange: false),
                CreateEntry(20f, 0f, willNotChange: false)
            };
            EnemiesSpawnChanceRedistributionSystem system = CreateSystem(entries, 10f, 10f);

            // Act
            system.RedistributeSpawnChance();

            // Assert
            Assert.AreEqual(40f, entries[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(30f, entries[1].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(30f, entries[2].SpawnChanceInfo.SpawnChance, TOLERANCE);
        }

        [Test]
        public void RedistributeSpawnChance_WithThreeEligibleDownstreamEntries_SplitsGeometrically50_25_25()
        {
            // Arrange — Entries 0, 1, 2 all meet thresholds -> 20 splits: +10 to [1], +5 to [2], +5 remainder to [3]
            List<EnemySpawnInfo> entries = new List<EnemySpawnInfo>
            {
                CreateEntry(100f, 0f, willNotChange: false),
                CreateEntry(40f, 10f, willNotChange: false),
                CreateEntry(40f, 10f, willNotChange: false),
                CreateEntry(0f, 0f, willNotChange: false)
            };
            EnemiesSpawnChanceRedistributionSystem system = CreateSystem(entries, 20f, 20f);

            // Act
            system.RedistributeSpawnChance();

            // Assert
            Assert.AreEqual(80f, entries[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(50f, entries[1].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(45f, entries[2].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(5f, entries[3].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(180f, SumSpawnChances(entries), TOLERANCE);
        }

        [Test]
        public void RedistributeSpawnChance_HasEverReachedThresholdHysteresis_KeepsEligibilityAfterDroppingBelowThreshold()
        {
            // Arrange — Wave 1: 30 >= 20 and 30 >= 25 set HasEverReachedThreshold; [0] drops to 15 (< 20).
            // Wave 2: even though [0] is 15 < 20, HasEverReachedThreshold stays true so [1] & [2] split +5/+5.
            List<EnemySpawnInfo> entries = new List<EnemySpawnInfo>
            {
                CreateEntry(30f, 20f, willNotChange: false),
                CreateEntry(30f, 25f, willNotChange: false),
                CreateEntry(40f, 0f, willNotChange: false)
            };
            EnemiesSpawnChanceRedistributionSystem system = CreateSystem(entries, 15f, 15f);

            // Act — Wave 1 (step 15) -> 15, 37.5, 47.5; then change step via bonus or create 10f step:
            system.RedistributeSpawnChance();
            Assert.AreEqual(15f, entries[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(37.5f, entries[1].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(47.5f, entries[2].SpawnChanceInfo.SpawnChance, TOLERANCE);

            // Wave 2 (step 10 -> [0] drops from 15 to 5, splitting +5 to [1] and +5 to [2])
            List<EnemySpawnInfo> secondPhaseEntries = new List<EnemySpawnInfo>
            {
                entries[0],
                entries[1],
                entries[2]
            };
            EnemiesSpawnChanceRedistributionSystem secondStepSystem = CreateSystem(secondPhaseEntries, 10f, 10f);
            secondStepSystem.RedistributeSpawnChance();

            // Assert
            Assert.AreEqual(5f, entries[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(42.5f, entries[1].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(52.5f, entries[2].SpawnChanceInfo.SpawnChance, TOLERANCE);
        }

        [Test]
        public void RedistributeSpawnChance_SingleCallExceedingCurrentSource_RollsOverToNextSourceInSameWave()
        {
            // Arrange — Step=12 exceeds [0]=5:
            //   [0] zeroes and distributes 5 -> [1]=42.5, [2]=2.5;
            //   while loop advances to [1] in same call and subtracts 12 -> [1]=30.5, [2]=14.5.
            List<EnemySpawnInfo> entries = new List<EnemySpawnInfo>
            {
                CreateEntry(5f, 0f, willNotChange: false),
                CreateEntry(40f, 0f, willNotChange: false),
                CreateEntry(0f, 0f, willNotChange: false)
            };
            EnemiesSpawnChanceRedistributionSystem system = CreateSystem(entries, 12f, 12f);

            // Act
            system.RedistributeSpawnChance();

            // Assert
            Assert.AreEqual(0f, entries[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(30.5f, entries[1].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(14.5f, entries[2].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(45f, SumSpawnChances(entries), TOLERANCE);
        }

        [Test]
        public void RedistributeSpawnChance_PenultimateSourceExhausted_AddsToLastEntryAndStopsSubsequentCalls()
        {
            // Arrange
            List<EnemySpawnInfo> entries = new List<EnemySpawnInfo>
            {
                CreateEntry(10f, 0f, willNotChange: false),
                CreateEntry(90f, 0f, willNotChange: false)
            };
            EnemiesSpawnChanceRedistributionSystem system = CreateSystem(entries, 15f, 15f);

            // Act
            system.RedistributeSpawnChance();
            system.RedistributeSpawnChance();

            // Assert
            Assert.AreEqual(0f, entries[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(100f, entries[1].SpawnChanceInfo.SpawnChance, TOLERANCE);
        }

        [Test]
        public void IncreaseSpawnChanceRedistributionFactor_AccumulatesBonusAndAcceleratesReduction()
        {
            // Arrange
            List<EnemySpawnInfo> entries = new List<EnemySpawnInfo>
            {
                CreateEntry(100f, 0f, willNotChange: false),
                CreateEntry(0f, 0f, willNotChange: false)
            };
            EnemiesSpawnChanceRedistributionSystem system = CreateSystem(entries, 2f, 2f);

            // Act
            system.IncreaseSpawnChanceRedistributionFactor(2.6f);
            system.IncreaseSpawnChanceRedistributionFactor(1.4f);
            system.RedistributeSpawnChance();

            // Assert — step = 2.0 + (2.6 + 1.4) = 6.0
            Assert.AreEqual(4.0f, system.RedistributionFactorBonus, TOLERANCE);
            Assert.AreEqual(94f, entries[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(6f, entries[1].SpawnChanceInfo.SpawnChance, TOLERANCE);
        }

        [Test]
        public void RedistributeSpawnChance_WithVariableDecreaseRange_SubtractsWithinRangePlusBonusAndConservesTotal()
        {
            // Arrange — Range [1.5, 2.5] + bonus 1.0 => step in [2.5, 3.5]
            List<EnemySpawnInfo> entries = new List<EnemySpawnInfo>
            {
                CreateEntry(100f, 0f, willNotChange: false),
                CreateEntry(0f, 0f, willNotChange: false)
            };
            EnemiesSpawnChanceRedistributionSystem system = CreateSystem(entries, 1.5f, 2.5f);
            system.IncreaseSpawnChanceRedistributionFactor(1.0f);

            // Act
            system.RedistributeSpawnChance();

            // Assert
            Assert.GreaterOrEqual(entries[0].SpawnChanceInfo.SpawnChance, 96.5f - TOLERANCE);
            Assert.LessOrEqual(entries[0].SpawnChanceInfo.SpawnChance, 97.5f + TOLERANCE);
            Assert.AreEqual(100f, SumSpawnChances(entries), TOLERANCE);
        }

        [Test]
        public void AuthoredAssets_Tier1SevenEntryRoster_MatchesConfigAndConservesProbabilityAcrossWaves()
        {
            // Arrange
            GameLoopProgressionConfigSO progression = AssetDatabase.LoadAssetAtPath<GameLoopProgressionConfigSO>(PROGRESSION_CONFIG_PATH);
            Assert.IsNotNull(progression);
            progression.ValidateConfiguration();

            LoopTierConfig tier1 = progression.GetTier(0);
            IReadOnlyList<EnemySpawnInfo> authoredInfos = tier1.EnemySpawning.EnemySpawnInfos;

            Assert.AreEqual(7, authoredInfos.Count);
            Assert.AreEqual(680, SumMaxAmounts(authoredInfos));
            Assert.AreEqual(1.5f, tier1.EnemySpawning.SpawnChanceDecreaseFactor.Min, TOLERANCE);
            Assert.AreEqual(2.5f, tier1.EnemySpawning.SpawnChanceDecreaseFactor.Max, TOLERANCE);
            AssertTier1InitialEntry(authoredInfos[0], 180, 100f, 0f);
            AssertTier1InitialEntry(authoredInfos[1], 140, 0f, 30f);
            AssertTier1InitialEntry(authoredInfos[2], 30, 0f, 36f);
            AssertTier1InitialEntry(authoredInfos[3], 100, 0f, 30f);
            AssertTier1InitialEntry(authoredInfos[4], 80, 0f, 26f);
            AssertTier1InitialEntry(authoredInfos[5], 80, 0f, 20f);
            AssertTier1InitialEntry(authoredInfos[6], 70, 0f, 0f);

            List<EnemySpawnInfo> runtimeInfos = CloneEntries(authoredInfos);
            EnemiesSpawnChanceRedistributionSystem system = CreateSystem(runtimeInfos, 2f, 2f);

            // Act & Assert — Wave 1 moves 2% from index 0 to index 1
            system.RedistributeSpawnChance();
            Assert.AreEqual(98f, runtimeInfos[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(2f, runtimeInfos[1].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(100f, SumSpawnChances(runtimeInfos), TOLERANCE);

            // Act & Assert — After 19 more waves (20 total), index 1 has unlocked index 2 and sum remains 100
            RunWaves(system, 19);
            Assert.Greater(runtimeInfos[2].SpawnChanceInfo.SpawnChance, 0f);
            Assert.AreEqual(100f, SumSpawnChances(runtimeInfos), TOLERANCE);

            // Act & Assert — Converges cleanly to 100% on final entry
            RunWaves(system, 250);
            AssertOnlyFinalEntryActive(runtimeInfos, 100f);
        }

        private static EnemiesSpawnChanceRedistributionSystem CreateSystem(
            List<EnemySpawnInfo> entries,
            float decreaseMin,
            float decreaseMax)
        {
            EnemiesSpawnChanceRedistributionSystem system = new EnemiesSpawnChanceRedistributionSystem();
            system.Initialize(new EnemiesSpawnChanceRedistributionSystem.Configuration
            {
                EnemiesInfo = entries,
                SpawnChanceDecreaseFactor = new FloatValueRange(decreaseMin, decreaseMax)
            });
            return system;
        }

        private static EnemySpawnInfo CreateEntry(float spawnChance, float threshold, bool willNotChange)
        {
            return new EnemySpawnInfo(
                null,
                50,
                new SpawnChanceInfo
                {
                    SpawnChance = spawnChance,
                    TresholdToStartAddingSpawnChanceToOtherInfos = threshold,
                    SpawnChanceWillNotChange = willNotChange
                });
        }

        private static List<EnemySpawnInfo> CloneEntries(IReadOnlyList<EnemySpawnInfo> authored)
        {
            List<EnemySpawnInfo> cloned = new List<EnemySpawnInfo>(authored.Count);
            for (int i = 0; i < authored.Count; i++)
            {
                cloned.Add(authored[i].CreateRuntimeCopy());
            }

            return cloned;
        }

        private static void RunWaves(EnemiesSpawnChanceRedistributionSystem system, int waveCount)
        {
            for (int i = 0; i < waveCount; i++)
            {
                system.RedistributeSpawnChance();
            }
        }

        private static float SumSpawnChances(List<EnemySpawnInfo> entries)
        {
            float sum = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                sum += entries[i].SpawnChanceInfo.SpawnChance;
            }

            return sum;
        }

        private static int SumMaxAmounts(IReadOnlyList<EnemySpawnInfo> entries)
        {
            int sum = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                sum += entries[i].MaxAmount;
            }

            return sum;
        }

        private static void AssertTier1InitialEntry(
            EnemySpawnInfo entry,
            ushort expectedMaxAmount,
            float expectedChance,
            float expectedThreshold)
        {
            Assert.IsNotNull(entry.EnemyPrefab);
            Assert.IsNull(entry.ConfigOverride);
            Assert.AreEqual(expectedMaxAmount, entry.MaxAmount);
            Assert.AreEqual(expectedChance, entry.SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(expectedThreshold, entry.SpawnChanceInfo.TresholdToStartAddingSpawnChanceToOtherInfos, TOLERANCE);
        }

        private static void AssertOnlyFinalEntryActive(List<EnemySpawnInfo> entries, float expectedFinalChance)
        {
            int lastIndex = entries.Count - 1;
            for (int i = 0; i < lastIndex; i++)
            {
                Assert.AreEqual(0f, entries[i].SpawnChanceInfo.SpawnChance, TOLERANCE);
            }

            Assert.AreEqual(expectedFinalChance, entries[lastIndex].SpawnChanceInfo.SpawnChance, TOLERANCE);
        }
    }
}
