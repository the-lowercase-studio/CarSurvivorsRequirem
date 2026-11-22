using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Common.Types;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.Player;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Spawners;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.Spawners.WorldSpace;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class EnemiesSpawnerStandardWaveTests
    {
        private const float TOLERANCE = 0.0001f;
        private const int DEFAULT_RANDOM_SEED = 424242;
        private static readonly Vector3 TEST_ORIGIN = new Vector3(18000f, 500f, 18000f);

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();
        private readonly List<EnemiesSpawner> _createdSpawners = new();
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
            for (int i = 0; i < _createdSpawners.Count; i++)
            {
                EnemiesSpawner spawner = _createdSpawners[i];
                if (spawner == null)
                {
                    continue;
                }

                FieldInfo stagingField = typeof(EnemiesSpawner).GetField("_injectionStagingParent", BindingFlags.Instance | BindingFlags.NonPublic);
                Transform stagingParent = stagingField != null ? stagingField.GetValue(spawner) as Transform : null;
                if (stagingParent != null)
                {
                    UnityEngine.Object.DestroyImmediate(stagingParent.gameObject);
                    stagingField.SetValue(spawner, null);
                }
            }

            _createdSpawners.Clear();

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
            Physics.SyncTransforms();
            UnityEngine.Random.state = _previousRandomState;
        }

        [Test]
        public void Start_PreWarmsExactMaxAmountAppliesOverridesAndLeavesZeroActiveEnemies()
        {
            // Arrange
            EnemyConfigSO overrideConfig = CreateEnemyConfig(maxHealth: 450f);
            EnemySpawnInfo entryA = CreateEnemySpawnInfo("WaveEnemyA", maxAmount: 3, spawnChance: 60f, baseMaxHealth: 100f, configOverride: overrideConfig);
            EnemySpawnInfo entryB = CreateEnemySpawnInfo("WaveEnemyB", maxAmount: 2, spawnChance: 40f, baseMaxHealth: 120f, configOverride: null);

            WorldAndChunkHarness grid = CreateWorldAndChunkHarness();
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { entryA, entryB },
                sceneDecreaseFactor: new FloatValueRange(2f, 2f),
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: grid.GridManager,
                camera: CreateAllHiddenCamera(),
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: 2,
                callStart: false,
                out Transform enemiesParent);

            int prewarmReleaseCount = 0;
            spawner.OnSpawnedEntityReleased += delegate { prewarmReleaseCount++; };

            // Act
            InvokeLifecycleMethod(spawner, "Start");

            // Assert — 3 + 2 = 5 pooled inactive children under _enemiesParent; 0 active; override applied to entryA instances
            List<Enemy> activeBuffer = new List<Enemy>();
            int activeCount = spawner.CopyActiveEnemies(activeBuffer);

            Assert.AreEqual(0u, spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(0, activeCount);
            Assert.AreEqual(0, prewarmReleaseCount);
            Assert.AreEqual(5, enemiesParent.childCount);
            AssertAllChildrenInactiveAndCountConfigOverrides(enemiesParent, overrideConfig, expectedOverrideCount: 3);
        }

        [Test]
        public void Start_DecreaseFactorResolution_UsesTierOverrideThenSceneFallbackThenDefaultRange()
        {
            // Arrange — Case 1: Tier entries + Tier decrease factor (10, 10) override scene (1, 1)
            EnemySpawnInfo tierEntryA = CreateEnemySpawnInfo("TierA", 4, 100f, 100f);
            EnemySpawnInfo tierEntryB = CreateEnemySpawnInfo("TierB", 4, 0f, 100f);
            GameLoopProgressionConfigSO progression = CreateProgressionWithTierEntries(
                new[] { tierEntryA, tierEntryB },
                new FloatValueRange(10f, 10f));
            EnemiesSpawner tierSpawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { CreateEnemySpawnInfo("Unused", 2, 100f, 100f) },
                sceneDecreaseFactor: new FloatValueRange(1f, 1f),
                runSessionService: new RunSessionService(progression),
                gridManager: CreateWorldAndChunkHarness().GridManager,
                camera: CreateAllHiddenCamera(),
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: 2,
                callStart: true,
                out _);

            // Case 2: Empty tier -> uses scene decrease factor (7, 7)
            EnemiesSpawner sceneSpawner = CreateEnemiesSpawner(
                sceneDefaults: new[]
                {
                    CreateEnemySpawnInfo("SceneA", 4, 100f, 100f),
                    CreateEnemySpawnInfo("SceneB", 4, 0f, 100f)
                },
                sceneDecreaseFactor: new FloatValueRange(7f, 7f),
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: CreateWorldAndChunkHarness().GridManager,
                camera: CreateAllHiddenCamera(),
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: 2,
                callStart: true,
                out _);

            // Case 3: Both null -> defaults to [1.5, 2.5]
            EnemiesSpawner defaultSpawner = CreateEnemiesSpawner(
                sceneDefaults: new[]
                {
                    CreateEnemySpawnInfo("DefA", 4, 100f, 100f),
                    CreateEnemySpawnInfo("DefB", 4, 0f, 100f)
                },
                sceneDecreaseFactor: null,
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: CreateWorldAndChunkHarness().GridManager,
                camera: CreateAllHiddenCamera(),
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: 2,
                callStart: true,
                out _);

            // Act
            tierSpawner.SpawnAtRandomGridPos(0);
            sceneSpawner.SpawnAtRandomGridPos(0);
            defaultSpawner.SpawnAtRandomGridPos(0);

            // Assert
            Assert.AreEqual(90f, tierSpawner.EnemyConfigs[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(93f, sceneSpawner.EnemyConfigs[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.GreaterOrEqual(defaultSpawner.EnemyConfigs[0].SpawnChanceInfo.SpawnChance, 97.5f - TOLERANCE);
            Assert.LessOrEqual(defaultSpawner.EnemyConfigs[0].SpawnChanceInfo.SpawnChance, 98.5f + TOLERANCE);
        }

        [Test]
        public void SpawnAtRandomGridPos_SpawnsOutsideChunkOnHiddenSupportedCellsAndResetsMovement()
        {
            // Arrange
            WorldAndChunkHarness grid = CreateWorldAndChunkHarness();
            CreateFloorCollider(TEST_ORIGIN + new Vector3(7f, -0.5f, 7f), new Vector3(20f, 1f, 20f));
            Camera camera = CreateCameraLookingAtPlayerChunk(grid.ChunkCenterWorldPos);

            EnemySpawnInfo entry = CreateEnemySpawnInfo("OuterWaveEnemy", maxAmount: 10, spawnChance: 100f, baseMaxHealth: 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { entry },
                sceneDecreaseFactor: new FloatValueRange(2f, 2f),
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: grid.GridManager,
                camera: camera,
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: 2,
                callStart: true,
                out _);

            List<Enemy> spawned = new List<Enemy>();

            // Act
            spawner.SpawnAtRandomGridPos(4);
            int copied = spawner.CopyActiveEnemies(spawned);

            // Assert
            Assert.AreEqual(4u, spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(4, copied);
            AssertAllSpawnedOutsideChunkHiddenSupportedAndReset(spawned, grid.PlayerChunk, camera);
        }

        [Test]
        public void SpawnAtRandomGridPos_RespectsMaxEnemiesPerCellOccupancyAcrossWaveCalls()
        {
            // Arrange — Only cell (0,3) outside the chunk is walkable; maxEnemiesPerCell = 2 -> max 2 total slots
            WorldAndChunkHarness grid = CreateWorldAndChunkHarness();
            MarkAllCellsUnwalkable(grid.WorldGrid);
            grid.WorldGrid.Cells[0, 3].Cost = GridConstants.DEFAULT_FIELD_COST;
            CreateFloorCollider(TEST_ORIGIN + new Vector3(7f, -0.5f, 7f), new Vector3(20f, 1f, 20f));
            Camera camera = CreateAllHiddenCamera();

            EnemySpawnInfo entry = CreateEnemySpawnInfo("SingleOuterCellEnemy", maxAmount: 5, spawnChance: 100f, baseMaxHealth: 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { entry },
                sceneDecreaseFactor: new FloatValueRange(2f, 2f),
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: grid.GridManager,
                camera: camera,
                outerSpawnBufferCells: 3,
                maxEnemiesPerCell: 2,
                callStart: true,
                out _);

            // Act — Wave 1 fills both occupancy slots on (0,3); SyncTransforms lets Wave 2 detect both colliders
            spawner.SpawnAtRandomGridPos(3);
            Assert.AreEqual(2u, spawner.CurrentlySpawnedObjectsCount);

            Physics.SyncTransforms();
            spawner.SpawnAtRandomGridPos(2);

            // Assert
            Assert.AreEqual(2u, spawner.CurrentlySpawnedObjectsCount);
        }

        [Test]
        public void SpawnAtRandomGridPos_RedistributesOncePerCallEvenWhenCountIsZero()
        {
            // Arrange
            WorldAndChunkHarness grid = CreateWorldAndChunkHarness();
            CreateFloorCollider(TEST_ORIGIN + new Vector3(7f, -0.5f, 7f), new Vector3(20f, 1f, 20f));
            Camera camera = CreateAllHiddenCamera();

            EnemySpawnInfo primary = CreateEnemySpawnInfo("Primary", maxAmount: 6, spawnChance: 100f, baseMaxHealth: 100f);
            EnemySpawnInfo secondary = CreateEnemySpawnInfo("Secondary", maxAmount: 6, spawnChance: 0f, baseMaxHealth: 200f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { primary, secondary },
                sceneDecreaseFactor: new FloatValueRange(10f, 10f),
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: grid.GridManager,
                camera: camera,
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: 2,
                callStart: true,
                out _);

            // Act — Batch of 3 enemies triggers redistribution once (100 -> 90 / 10); count 0 also triggers once (90 -> 80 / 20)
            spawner.SpawnAtRandomGridPos(3);
            Assert.AreEqual(90f, spawner.EnemyConfigs[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(10f, spawner.EnemyConfigs[1].SpawnChanceInfo.SpawnChance, TOLERANCE);

            spawner.SpawnAtRandomGridPos(0);
            Assert.AreEqual(80f, spawner.EnemyConfigs[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(20f, spawner.EnemyConfigs[1].SpawnChanceInfo.SpawnChance, TOLERANCE);
        }

        [Test]
        public void IncreaseSpawnChanceRedistributionFactor_AcceleratesShiftAndChangesSpawnedEnemyTypes()
        {
            // Arrange — Entry 0 starts at 100%, Entry 1 at 0%. Base decrease 5 + bonus 95 = 100 -> shifts all weight to Entry 1
            WorldAndChunkHarness grid = CreateWorldAndChunkHarness();
            CreateFloorCollider(TEST_ORIGIN + new Vector3(7f, -0.5f, 7f), new Vector3(20f, 1f, 20f));
            Camera camera = CreateAllHiddenCamera();

            EnemyConfigSO configA = CreateEnemyConfig(maxHealth: 111f);
            EnemyConfigSO configB = CreateEnemyConfig(maxHealth: 333f);
            EnemySpawnInfo entryA = CreateEnemySpawnInfo("EarlyEnemy", maxAmount: 4, spawnChance: 100f, baseMaxHealth: 100f, configOverride: configA);
            EnemySpawnInfo entryB = CreateEnemySpawnInfo("LateEnemy", maxAmount: 4, spawnChance: 0f, baseMaxHealth: 200f, configOverride: configB);

            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { entryA, entryB },
                sceneDecreaseFactor: new FloatValueRange(5f, 5f),
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: grid.GridManager,
                camera: camera,
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: 2,
                callStart: true,
                out _);

            // Act — Wave 1 spawns Entry A (2), redistributes 5% -> 95% / 5%
            spawner.SpawnAtRandomGridPos(2);
            List<Enemy> wave1Enemies = new List<Enemy>();
            spawner.CopyActiveEnemies(wave1Enemies);
            AssertAllHaveConfig(wave1Enemies, configA);
            ReleaseAll(wave1Enemies);

            // Boost redistribution factor by 95 -> next wave drains Entry A completely to 0% / 100%
            spawner.IncreaseSpawnChanceRedistributionFactor(95f);
            spawner.SpawnAtRandomGridPos(1);
            List<Enemy> wave2Enemies = new List<Enemy>();
            spawner.CopyActiveEnemies(wave2Enemies);
            ReleaseAll(wave2Enemies);

            Assert.AreEqual(0f, spawner.EnemyConfigs[0].SpawnChanceInfo.SpawnChance, TOLERANCE);
            Assert.AreEqual(100f, spawner.EnemyConfigs[1].SpawnChanceInfo.SpawnChance, TOLERANCE);

            // Wave 3 spawns exclusively Entry B (0% Entry A vs 100% Entry B)
            spawner.SpawnAtRandomGridPos(3);
            List<Enemy> wave3Enemies = new List<Enemy>();
            spawner.CopyActiveEnemies(wave3Enemies);

            // Assert
            Assert.AreEqual(3, wave3Enemies.Count);
            AssertAllHaveConfig(wave3Enemies, configB);
        }

        [Test]
        public void SpawnAtRandomGridPos_WhenAllEntriesHaveZeroSpawnChance_SpawnsNothing()
        {
            // Arrange
            WorldAndChunkHarness grid = CreateWorldAndChunkHarness();
            CreateFloorCollider(TEST_ORIGIN + new Vector3(7f, -0.5f, 7f), new Vector3(20f, 1f, 20f));
            Camera camera = CreateAllHiddenCamera();

            EnemyConfigSO firstConfig = CreateEnemyConfig(maxHealth: 111f);
            EnemyConfigSO secondConfig = CreateEnemyConfig(maxHealth: 222f);
            EnemySpawnInfo entryA = CreateEnemySpawnInfo("ZeroA", maxAmount: 4, spawnChance: 0f, baseMaxHealth: 100f, configOverride: firstConfig);
            EnemySpawnInfo entryB = CreateEnemySpawnInfo("ZeroB", maxAmount: 4, spawnChance: 0f, baseMaxHealth: 200f, configOverride: secondConfig);

            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { entryA, entryB },
                sceneDecreaseFactor: new FloatValueRange(2f, 2f),
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: grid.GridManager,
                camera: camera,
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: 2,
                callStart: true,
                out _);

            // Act
            spawner.SpawnAtRandomGridPos(2);
            List<Enemy> spawned = new List<Enemy>();
            spawner.CopyActiveEnemies(spawned);

            // Assert
            Assert.AreEqual(0, spawned.Count);
        }

        [Test]
        public void CopyActiveEnemies_NullBuffer_ThrowsArgumentNullException()
        {
            // Arrange
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { CreateEnemySpawnInfo("NullArgEnemy", 2, 100f, 100f) },
                sceneDecreaseFactor: new FloatValueRange(1f, 1f),
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: CreateWorldAndChunkHarness().GridManager,
                camera: CreateAllHiddenCamera(),
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: 2,
                callStart: true,
                out _);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => spawner.CopyActiveEnemies(null));
        }

        [Test]
        public void CopyActiveEnemies_ClearsExistingBufferBeforePopulatingActiveInstances()
        {
            // Arrange
            WorldAndChunkHarness grid = CreateWorldAndChunkHarness();
            CreateFloorCollider(TEST_ORIGIN + new Vector3(7f, -0.5f, 7f), new Vector3(20f, 1f, 20f));
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { CreateEnemySpawnInfo("BufferEnemy", 4, 100f, 100f) },
                sceneDecreaseFactor: new FloatValueRange(1f, 1f),
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: grid.GridManager,
                camera: CreateAllHiddenCamera(),
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: 2,
                callStart: true,
                out _);

            GameObject staleGo = new GameObject("StaleDummyEnemy");
            _createdObjects.Add(staleGo);
            Enemy staleDummy = staleGo.AddComponent<Enemy>();
            List<Enemy> buffer = new List<Enemy> { staleDummy };

            // Act
            spawner.SpawnAtRandomGridPos(2);
            int count = spawner.CopyActiveEnemies(buffer);

            // Assert
            Assert.AreEqual(2, count);
            Assert.AreEqual(2, buffer.Count);
            CollectionAssert.DoesNotContain(buffer, staleDummy);
        }

        [Test]
        public void DeathSequenceCompletion_ReleasesEnemyToPoolDecrementsCountAndAvoidsDuplicateReleaseOnReuse()
        {
            // Arrange
            WorldAndChunkHarness grid = CreateWorldAndChunkHarness();
            CreateFloorCollider(TEST_ORIGIN + new Vector3(7f, -0.5f, 7f), new Vector3(20f, 1f, 20f));
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { CreateEnemySpawnInfo("DeathReleaseEnemy", 1, 100f, 100f) },
                sceneDecreaseFactor: new FloatValueRange(1f, 1f),
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: grid.GridManager,
                camera: CreateAllHiddenCamera(),
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: 2,
                callStart: true,
                out _);

            int releasedEvents = 0;
            object lastReleasedSender = null;
            spawner.OnSpawnedEntityReleased += (sender, args) =>
            {
                releasedEvents++;
                lastReleasedSender = sender;
            };

            List<Enemy> activeBuffer = new List<Enemy>();

            // Act — Cycle 1: spawn 1 and complete its death sequence
            spawner.SpawnAtRandomGridPos(1);
            spawner.CopyActiveEnemies(activeBuffer);
            Enemy firstInstance = activeBuffer[0];

            firstInstance.GetComponent<FakeDeathSequence>().TriggerCompleted();

            Assert.AreEqual(0u, spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(0, spawner.CopyActiveEnemies(activeBuffer));
            Assert.AreEqual(1, releasedEvents);
            Assert.AreSame(firstInstance, lastReleasedSender);
            Assert.IsFalse(firstInstance.gameObject.activeSelf);

            // Act — Cycle 2: spawn again (reuses same pooled instance) and complete death sequence again
            spawner.SpawnAtRandomGridPos(1);
            spawner.CopyActiveEnemies(activeBuffer);
            Assert.AreSame(firstInstance, activeBuffer[0]);
            Assert.IsTrue(firstInstance.gameObject.activeSelf);

            firstInstance.GetComponent<FakeDeathSequence>().TriggerCompleted();

            // Assert — Exactly 1 additional release event (no duplicate OnCanBeReleased subscription)
            Assert.AreEqual(0u, spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(2, releasedEvents);
        }

        private EnemiesSpawner CreateEnemiesSpawner(
            EnemySpawnInfo[] sceneDefaults,
            FloatValueRange sceneDecreaseFactor,
            IRunSessionService runSessionService,
            IGridManager gridManager,
            Camera camera,
            int outerSpawnBufferCells,
            int maxEnemiesPerCell,
            bool callStart,
            out Transform enemiesParent)
        {
            GameObject parentGo = new GameObject("TestWaveEnemiesParent");
            _createdObjects.Add(parentGo);
            enemiesParent = parentGo.transform;

            GameObject spawnerGo = new GameObject("TestWaveEnemiesSpawner");
            spawnerGo.SetActive(false);
            _createdObjects.Add(spawnerGo);

            EnemiesSpawner spawner = spawnerGo.AddComponent<EnemiesSpawner>();
            _createdSpawners.Add(spawner);

            SetPrivateField(typeof(EnemiesSpawner), spawner, "_enemiesParent", enemiesParent);
            SetPrivateField(typeof(EnemiesSpawner), spawner, "_poolEnemiesInfo", new List<EnemySpawnInfo>(sceneDefaults));
            SetPrivateField(typeof(EnemiesSpawner), spawner, "_spawnChanceDecreaseFactor", sceneDecreaseFactor);
            SetPrivateField(typeof(EnemiesSpawner), spawner, "_outerSpawnBufferCells", outerSpawnBufferCells);
            SetPrivateField(typeof(EnemiesSpawner), spawner, "_maxEnemiesPerCell", maxEnemiesPerCell);

            ContainerBuilder builder = new ContainerBuilder().SetName("EnemiesSpawnerWaveTestContainer");
            builder.AddSingleton(gridManager, typeof(IGridManager));
            builder.AddSingleton(camera);
            builder.AddSingleton(runSessionService, typeof(IRunSessionService));
            builder.AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>));
            builder.AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState));
            Container container = builder.Build();
            _createdContainers.Add(container);

            GameObjectInjector.InjectRecursive(spawnerGo, container);
            spawnerGo.SetActive(true);
            InvokeLifecycleMethod(spawner, "Awake");
            if (callStart)
            {
                InvokeLifecycleMethod(spawner, "Start");
            }

            return spawner;
        }

        private EnemySpawnInfo CreateEnemySpawnInfo(
            string name,
            ushort maxAmount,
            float spawnChance,
            float baseMaxHealth,
            EnemyConfigSO configOverride = null)
        {
            GameObject templateHost = new GameObject($"{name}_TemplateHost");
            templateHost.SetActive(false);
            _createdObjects.Add(templateHost);

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0);

            GameObject root = new GameObject(name);
            root.layer = enemyLayer;
            root.transform.SetParent(templateHost.transform, false);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.4f, 1f, 0.4f);
            root.AddComponent<EditModeEnemyAwakeShim>();
            root.AddComponent<Health>();
            root.AddComponent<FakeDeathSequence>();
            root.AddComponent<FakeMovementController>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<Animator>();
            visual.AddComponent<EnemyAnimator>();

            EnemyConfigSO baseConfig = CreateEnemyConfig(baseMaxHealth);

            Enemy enemy = root.AddComponent<Enemy>();
            SetPrivateField(typeof(Enemy), enemy, "<Config>k__BackingField", baseConfig);
            SetPrivateField(typeof(Enemy), enemy, "_visual", visual);

            return new EnemySpawnInfo(
                enemy,
                maxAmount,
                new SpawnChanceInfo { SpawnChance = spawnChance },
                configOverride);
        }

        private EnemyConfigSO CreateEnemyConfig(float maxHealth)
        {
            EnemyConfigSO config = ScriptableObject.CreateInstance<EnemyConfigSO>();
            _createdObjects.Add(config);
            config.MaxHealth = maxHealth;
            return config;
        }

        private GameLoopProgressionConfigSO CreateProgressionWithTierEntries(
            EnemySpawnInfo[] tierEntries,
            FloatValueRange decreaseFactor)
        {
            GameLoopProgressionConfigSO config = ScriptableObject.CreateInstance<GameLoopProgressionConfigSO>();
            _createdObjects.Add(config);

            LoopTierConfig tier = new LoopTierConfig();
            TierEnemySpawningConfig spawning = tier.EnemySpawning;
            SetPrivateField(typeof(TierEnemySpawningConfig), spawning, "_enemySpawnInfos", new List<EnemySpawnInfo>(tierEntries));
            SetPrivateField(typeof(TierEnemySpawningConfig), spawning, "_spawnChanceDecreaseFactor", decreaseFactor);

            SetPrivateField(typeof(GameLoopProgressionConfigSO), config, "_tiers", new List<LoopTierConfig> { tier });
            return config;
        }

        private static WorldAndChunkHarness CreateWorldAndChunkHarness()
        {
            int width = 7;
            int height = 7;
            float cellSize = 2f;
            Cell[,] worldCells = new Cell[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    Vector3 worldPos = TEST_ORIGIN + new Vector3((x + 0.5f) * cellSize, 0f, (y + 0.5f) * cellSize);
                    worldCells[x, y] = new Cell(worldPos, pos, pos);
                }
            }

            Grid worldGrid = new Grid(new GridConfiguration(width, height, cellSize), worldCells);

            int chunkSize = 3;
            Cell[,] chunkCells = new Cell[chunkSize, chunkSize];
            for (int x = 0; x < chunkSize; x++)
            {
                for (int y = 0; y < chunkSize; y++)
                {
                    Cell cell = worldCells[2 + x, 2 + y];
                    cell.ChunkGridPos = new Vector2Int(x, y);
                    chunkCells[x, y] = cell;
                }
            }

            Grid playerChunk = new Grid(new GridConfiguration(chunkSize, chunkSize, cellSize), chunkCells);
            return new WorldAndChunkHarness(new FakeGridManager(worldGrid, playerChunk), worldGrid, playerChunk, worldCells[3, 3].WorldPos);
        }

        private static void MarkAllCellsUnwalkable(Grid grid)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    grid.Cells[x, y].Cost = byte.MaxValue;
                }
            }
        }

        private GameObject CreateFloorCollider(Vector3 center, Vector3 size)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.GreaterOrEqual(groundLayer, 0);

            GameObject floor = new GameObject("TestWaveFloor");
            floor.layer = groundLayer;
            floor.transform.position = center;
            BoxCollider collider = floor.AddComponent<BoxCollider>();
            collider.size = size;
            _createdObjects.Add(floor);
            Physics.SyncTransforms();
            return floor;
        }

        private Camera CreateAllHiddenCamera()
        {
            GameObject cameraGo = new GameObject("WaveAllHiddenCamera");
            cameraGo.transform.position = TEST_ORIGIN + new Vector3(0f, 50f, 0f);
            cameraGo.transform.rotation = Quaternion.LookRotation(Vector3.up);
            _createdObjects.Add(cameraGo);

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private Camera CreateCameraLookingAtPlayerChunk(Vector3 chunkCenter)
        {
            GameObject cameraGo = new GameObject("WaveChunkViewCamera");
            cameraGo.transform.position = chunkCenter + new Vector3(0f, 4f, -3f);
            cameraGo.transform.LookAt(chunkCenter);
            _createdObjects.Add(cameraGo);

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private static void AssertAllChildrenInactiveAndCountConfigOverrides(
            Transform enemiesParent,
            EnemyConfigSO expectedOverride,
            int expectedOverrideCount)
        {
            int overrideMatches = 0;
            for (int i = 0; i < enemiesParent.childCount; i++)
            {
                GameObject child = enemiesParent.GetChild(i).gameObject;
                Assert.IsFalse(child.activeSelf);

                Enemy enemy = child.GetComponent<Enemy>();
                Assert.IsNotNull(enemy);
                if (ReferenceEquals(enemy.Config, expectedOverride))
                {
                    overrideMatches++;
                    Assert.AreEqual(expectedOverride.MaxHealth, enemy.Health.MaxHealth, TOLERANCE);
                }
            }

            Assert.AreEqual(expectedOverrideCount, overrideMatches);
        }

        private static void AssertAllSpawnedOutsideChunkHiddenSupportedAndReset(
            List<Enemy> spawned,
            Grid playerChunk,
            Camera camera)
        {
            float halfCell = playerChunk.CellSize * 0.5f;
            float minX = playerChunk.Cells[0, 0].WorldPos.x - halfCell;
            float maxX = playerChunk.Cells[playerChunk.Width - 1, playerChunk.Height - 1].WorldPos.x + halfCell;
            float minZ = playerChunk.Cells[0, 0].WorldPos.z - halfCell;
            float maxZ = playerChunk.Cells[playerChunk.Width - 1, playerChunk.Height - 1].WorldPos.z + halfCell;

            for (int i = 0; i < spawned.Count; i++)
            {
                Enemy enemy = spawned[i];
                Vector3 pos = enemy.transform.position;
                bool insideChunk = pos.x >= minX && pos.x <= maxX && pos.z >= minZ && pos.z <= maxZ;
                Assert.IsFalse(insideChunk, $"Wave enemy spawned inside GridPlayerChunk at {pos}.");
                Assert.IsFalse(CellCameraVisibilityChecker.IsCellVisibleFromCamera(pos, camera));
                Assert.AreEqual(TEST_ORIGIN.y + GridConstants.ROOT_GROUND_CLEARANCE, pos.y, TOLERANCE);
                Assert.AreEqual(1, enemy.GetComponent<FakeMovementController>().ResetCount);
            }
        }

        private static void AssertAllHaveConfig(List<Enemy> enemies, EnemyConfigSO expectedConfig)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                Assert.AreSame(expectedConfig, enemies[i].Config);
            }
        }

        private static void ReleaseAll(List<Enemy> enemies)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].ReturnToPool();
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

        private readonly struct WorldAndChunkHarness
        {
            public readonly FakeGridManager GridManager;
            public readonly Grid WorldGrid;
            public readonly Grid PlayerChunk;
            public readonly Vector3 ChunkCenterWorldPos;

            public WorldAndChunkHarness(
                FakeGridManager gridManager,
                Grid worldGrid,
                Grid playerChunk,
                Vector3 chunkCenterWorldPos)
            {
                GridManager = gridManager;
                WorldGrid = worldGrid;
                PlayerChunk = playerChunk;
                ChunkCenterWorldPos = chunkCenterWorldPos;
            }
        }

        [ExecuteAlways]
        private sealed class EditModeEnemyAwakeShim : MonoBehaviour
        {
            private bool _initialized;

            private void Awake()
            {
                if (_initialized || Application.isPlaying)
                {
                    return;
                }

                _initialized = true;
                EnemyAnimator animator = GetComponentInChildren<EnemyAnimator>(true);
                if (animator != null)
                {
                    InvokeLifecycleMethod(animator, "Awake");
                }

                Enemy enemy = GetComponent<Enemy>();
                if (enemy != null)
                {
                    InvokeLifecycleMethod(enemy, "Awake");
                }
            }
        }

        private sealed class FakeDeathSequence : MonoBehaviour, INeedToCompleteBeforeDisable
        {
            public event EventHandler OnCompleted;

            public void TriggerCompleted()
            {
                OnCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class FakeMovementController : MonoBehaviour, IMovementController
        {
            public int ResetCount { get; private set; }
            public bool CanBeTeleported { get { return true; } }

            public void ResetAfterRelocation()
            {
                ResetCount++;
            }

            public float GetCurrentMovementSpeed()
            {
                return 0f;
            }

            public bool IsOnGround()
            {
                return true;
            }

            public void MoveToPositionInTimeIgnoringSpeed(Vector3 pos, float time)
            {
            }

            public void SetMovementSpeedMultiplier(float multiplier)
            {
            }
        }

        private sealed class FakeGridManager : IGridManager
        {
            public Cell DestinationCell { get { return null; } }
            public Grid GridPlayerChunk { get; }
            public Grid WorldGrid { get; }

            public FakeGridManager(Grid worldGrid, Grid playerChunk)
            {
                WorldGrid = worldGrid;
                GridPlayerChunk = playerChunk;
            }
        }

        private sealed class FakeDamageNumbersSpawner : IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>
        {
            public uint CurrentlySpawnedObjectsCount { get { return 0; } }

            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public void Spawn(Vector3 pos, DamageNubmersSpawnerConfig specificConfig, int count = 1)
            {
            }
        }

        private sealed class FakeTimeFreezeState : ITimeFreezeState
        {
            public bool IsFrozen { get { return false; } }
            public float UnfrozenTime { get { return Time.time; } }

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
