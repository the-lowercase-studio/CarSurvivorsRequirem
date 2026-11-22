using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.PowerUps;
using Assets.Scripts.PowerUps.Collectibles;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using Assets.Scripts.Spawners.WorldSpace;
using DG.Tweening;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class EnemyDropHandlerTests
    {
        private const float TOLERANCE = 0.001f;
        private const int DEFAULT_RANDOM_SEED = 98765;
        private const float GRID_CELL_SIZE = 8f;
        private const int GRID_DIMENSION = 15;

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();
        private UnityEngine.Random.State _previousRandomState;
        private bool _previousDotweenInitialized;

        [SetUp]
        public void SetUp()
        {
            _previousRandomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(DEFAULT_RANDOM_SEED);

            FieldInfo initializedField = typeof(DOTween).GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic);
            _previousDotweenInitialized = (bool)initializedField.GetValue(null);
            initializedField.SetValue(null, true);
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            DOTween.Clear(true);

            FieldInfo initializedField = typeof(DOTween).GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic);
            initializedField.SetValue(null, _previousDotweenInitialized);

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
        public void HealthOnNoHealth_NullDropEntries_DoesNotSpawnDrops()
        {
            // Arrange
            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: null,
                gridManager: new FakeGridManager(CreateWalkableGrid()));

            // Act
            KillEnemy(harness);

            // Assert
            Assert.AreEqual(0, harness.FakeNotifier.SpawnCalls.Count);
        }

        [Test]
        public void HealthOnNoHealth_EmptyDropEntries_DoesNotSpawnDrops()
        {
            // Arrange
            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: Array.Empty<CollectibleDropEntry>(),
                gridManager: new FakeGridManager(CreateWalkableGrid()));

            // Act
            KillEnemy(harness);

            // Assert
            Assert.AreEqual(0, harness.FakeNotifier.SpawnCalls.Count);
        }

        [Test]
        public void HealthOnNoHealth_WhenDropSuppressorActive_DoesNotSpawnDrops()
        {
            // Arrange
            GameObject prefab = CreateDummyPrefab("SuppressedDropPrefab");
            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: new[] { CreateDropEntry(prefab, 100f, 0.85f) },
                gridManager: new FakeGridManager(CreateWalkableGrid()),
                isSuppressed: true);

            // Act
            KillEnemy(harness);

            // Assert
            Assert.AreEqual(0, harness.FakeNotifier.SpawnCalls.Count);
        }

        [TestCase(true, TestName = "HealthOnNoHealth_SuppressorNotSuppressed_SpawnsDrop")]
        [TestCase(false, TestName = "HealthOnNoHealth_SuppressorNull_SpawnsDrop")]
        public void HealthOnNoHealth_WhenSuppressorAllowsDrops_SpawnsDrop(bool includeSuppressor)
        {
            // Arrange
            GameObject prefab = CreateDummyPrefab("AllowedDropPrefab");
            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: new[] { CreateDropEntry(prefab, 100f, 0.85f) },
                gridManager: new FakeGridManager(CreateWalkableGrid()),
                includeSuppressor: includeSuppressor,
                isSuppressed: false);

            // Act
            KillEnemy(harness);

            // Assert
            Assert.AreEqual(1, harness.FakeNotifier.SpawnCalls.Count);
            Assert.AreSame(prefab, harness.FakeNotifier.SpawnCalls[0].Prefab);
        }

        [Test]
        public void OnDisableAndReEnable_UnsubscribesWhileDisabledAndAvoidsDuplicateDropsOnReuse()
        {
            // Arrange
            GameObject prefab = CreateDummyPrefab("PooledEnemyDropPrefab");
            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: new[] { CreateDropEntry(prefab, 100f, 0.85f) },
                gridManager: new FakeGridManager(CreateWalkableGrid()));

            // Act 1 — Disable drop handler before enemy dies -> zero drops
            InvokeLifecycleMethod(harness.DropHandler, "OnDisable");
            KillEnemy(harness);
            Assert.AreEqual(0, harness.FakeNotifier.SpawnCalls.Count);

            // Act 2 — Re-enable enemy/health/dropHandler across two pooled lives -> 1 drop per death
            ResetEnemyForNextPoolLife(harness);
            KillEnemy(harness);
            Assert.AreEqual(1, harness.FakeNotifier.SpawnCalls.Count);

            ResetEnemyForNextPoolLife(harness);
            KillEnemy(harness);

            // Assert
            Assert.AreEqual(2, harness.FakeNotifier.SpawnCalls.Count);
        }

        [Test]
        public void HealthOnNoHealth_DropChancePartitions_SpawnsGuaranteedAndSkipsZeroAndNegativeChances()
        {
            // Arrange
            GameObject guaranteedPrefab = CreateDummyPrefab("GuaranteedPrefab");
            GameObject zeroPrefab = CreateDummyPrefab("ZeroChancePrefab");
            GameObject negativePrefab = CreateDummyPrefab("NegativeChancePrefab");

            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: new[]
                {
                    CreateDropEntry(zeroPrefab, 0f, 0.85f),
                    CreateDropEntry(guaranteedPrefab, 100f, 0.85f),
                    CreateDropEntry(negativePrefab, -5f, 0.85f)
                },
                gridManager: new FakeGridManager(CreateWalkableGrid()));

            // Act
            KillEnemy(harness);

            // Assert
            Assert.AreEqual(1, harness.FakeNotifier.SpawnCalls.Count);
            Assert.AreSame(guaranteedPrefab, harness.FakeNotifier.SpawnCalls[0].Prefab);
        }

        [Test]
        public void HealthOnNoHealth_MultipleGuaranteedDrops_ClearsPreviousChosenDropsAndAppliesScatterAndYOffset()
        {
            // Arrange
            GameObject prefabA = CreateDummyPrefab("DropA");
            GameObject prefabB = CreateDummyPrefab("DropB");
            Vector3 enemyPos = new Vector3(44f, 0f, 60f);

            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: new[]
                {
                    CreateDropEntry(prefabA, 100f, 0.85f),
                    CreateDropEntry(prefabB, 100f, 1.5f)
                },
                gridManager: new FakeGridManager(CreateWalkableGrid()),
                enemyPosition: enemyPos,
                scatterRadius: 3f);

            // Act 1 — First death spawns two drops spaced ~0 deg and ~180 deg with entry YOffsets
            KillEnemy(harness);

            Assert.AreEqual(2, harness.FakeNotifier.SpawnCalls.Count);
            SpawnRecord firstDrop = harness.FakeNotifier.SpawnCalls[0];
            SpawnRecord secondDrop = harness.FakeNotifier.SpawnCalls[1];

            Assert.AreSame(prefabA, firstDrop.Prefab);
            AssertVector3Equal(new Vector3(44f, 0.85f, 60f), firstDrop.SpawnPos);
            Assert.AreEqual(0.85f, firstDrop.TargetPos.y, TOLERANCE);
            AssertHorizontalRadiusAndAngle(enemyPos, firstDrop.TargetPos, 3f, expectedCosSign: 1);

            Assert.AreSame(prefabB, secondDrop.Prefab);
            AssertVector3Equal(new Vector3(44f, 1.5f, 60f), secondDrop.SpawnPos);
            Assert.AreEqual(1.5f, secondDrop.TargetPos.y, TOLERANCE);
            AssertHorizontalRadiusAndAngle(enemyPos, secondDrop.TargetPos, 3f, expectedCosSign: -1);

            // Act 2 — Re-enable and kill again -> _chosenDrops was cleared, so only 2 new drops spawn
            ResetEnemyForNextPoolLife(harness);
            KillEnemy(harness);

            // Assert
            Assert.AreEqual(4, harness.FakeNotifier.SpawnCalls.Count);
        }

        [Test]
        public void GetWalkablePosition_WalkableTargetCell_DropsAtFullScatterRadius()
        {
            // Arrange — CellSize=8, enemy at center of cell (5,7) = (44, 0, 60), scatterRadius=14 -> cell (7,7)
            GameObject prefab = CreateDummyPrefab("WalkableTargetDrop");
            Vector3 enemyPos = new Vector3(44f, 0f, 60f);
            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: new[] { CreateDropEntry(prefab, 100f, 0.85f) },
                gridManager: new FakeGridManager(CreateWalkableGrid()),
                enemyPosition: enemyPos,
                scatterRadius: 14f);

            // Act
            KillEnemy(harness);

            // Assert
            Assert.AreEqual(1, harness.FakeNotifier.SpawnCalls.Count);
            SpawnRecord drop = harness.FakeNotifier.SpawnCalls[0];
            Assert.AreEqual(0.85f, drop.TargetPos.y, TOLERANCE);
            Assert.AreEqual(14f, HorizontalDistance(enemyPos, drop.TargetPos), TOLERANCE);
            Assert.GreaterOrEqual(drop.TargetPos.x, 56f);
        }

        [Test]
        public void GetWalkablePosition_BlockedTargetWithWalkableLineStep_ResolvesNearestLineStep()
        {
            // Arrange — Block cell (7,7); step ratio 0.75 (10.5 units East) lands in walkable cell (6,7)
            Grid grid = CreateWalkableGrid();
            grid.Cells[7, 7].Cost = byte.MaxValue;

            GameObject prefab = CreateDummyPrefab("LineStepDrop");
            Vector3 enemyPos = new Vector3(44f, 0f, 60f);
            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: new[] { CreateDropEntry(prefab, 100f, 0.85f) },
                gridManager: new FakeGridManager(grid),
                enemyPosition: enemyPos,
                scatterRadius: 14f);

            // Act
            KillEnemy(harness);

            // Assert
            Assert.AreEqual(1, harness.FakeNotifier.SpawnCalls.Count);
            SpawnRecord drop = harness.FakeNotifier.SpawnCalls[0];
            Assert.AreEqual(0.85f, drop.TargetPos.y, TOLERANCE);
            Assert.AreEqual(10.5f, HorizontalDistance(enemyPos, drop.TargetPos), TOLERANCE);
            Assert.GreaterOrEqual(drop.TargetPos.x, 48f);
            Assert.Less(drop.TargetPos.x, 56f);
        }

        [Test]
        public void GetWalkablePosition_BlockedTargetAndLineSteps_FallsBackToWalkableStartPos()
        {
            // Arrange — Enemy near East edge of (5,7) at (47.5, 0, 60); all 0.25..1.0 steps fall into blocked (6,7) or (7,7)
            Grid grid = CreateWalkableGrid();
            grid.Cells[7, 7].Cost = byte.MaxValue;
            grid.Cells[6, 7].Cost = byte.MaxValue;

            GameObject prefab = CreateDummyPrefab("StartPosFallbackDrop");
            Vector3 enemyPos = new Vector3(47.5f, 0f, 60f);
            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: new[] { CreateDropEntry(prefab, 100f, 0.85f) },
                gridManager: new FakeGridManager(grid),
                enemyPosition: enemyPos,
                scatterRadius: 14f);

            // Act
            KillEnemy(harness);

            // Assert
            Assert.AreEqual(1, harness.FakeNotifier.SpawnCalls.Count);
            AssertVector3Equal(new Vector3(47.5f, 0.85f, 60f), harness.FakeNotifier.SpawnCalls[0].TargetPos);
        }

        [Test]
        public void GetWalkablePosition_BlockedStartAndLine_SpiralSearchFindsWalkableCellAtRadius5()
        {
            // Arrange — Block every cell except (10,12), at exact Chebyshev radius 5 from startCell (5,7)
            Grid grid = CreateBlockedGrid();
            grid.Cells[10, 12].Cost = 1;

            GameObject prefab = CreateDummyPrefab("Radius5Drop");
            Vector3 enemyPos = new Vector3(44f, 0f, 60f);
            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: new[] { CreateDropEntry(prefab, 100f, 0.85f) },
                gridManager: new FakeGridManager(grid),
                enemyPosition: enemyPos,
                scatterRadius: 14f);

            // Act
            KillEnemy(harness);

            // Assert — cell (10,12).WorldPos = (10*8+4, 0, 12*8+4) = (84, 0, 100), with YOffset applied
            Assert.AreEqual(1, harness.FakeNotifier.SpawnCalls.Count);
            AssertVector3Equal(new Vector3(84f, 0.85f, 100f), harness.FakeNotifier.SpawnCalls[0].TargetPos);
        }

        [Test]
        public void GetWalkablePosition_NearestWalkableCellBeyondRadius5_FallsBackToStartPos()
        {
            // Arrange — Block every cell except (11,7), at Chebyshev radius 6 (outside radius-5 spiral cap)
            Grid grid = CreateBlockedGrid();
            grid.Cells[11, 7].Cost = 1;

            GameObject prefab = CreateDummyPrefab("BeyondRadius5Drop");
            Vector3 enemyPos = new Vector3(44f, 0f, 60f);
            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: new[] { CreateDropEntry(prefab, 100f, 0.85f) },
                gridManager: new FakeGridManager(grid),
                enemyPosition: enemyPos,
                scatterRadius: 14f);

            // Act
            KillEnemy(harness);

            // Assert — Falls back to startPos with YOffset
            Assert.AreEqual(1, harness.FakeNotifier.SpawnCalls.Count);
            AssertVector3Equal(new Vector3(44f, 0.85f, 60f), harness.FakeNotifier.SpawnCalls[0].TargetPos);
        }

        [Test]
        public void GetWalkablePosition_EnemyOutsideWorldBoundsWithBlockedEdge_SpiralSearchClampsInsideGrid()
        {
            // Arrange — (-20, 0, -20) clamps to edge cell (0,0); only (0,1) is walkable at WorldPos=(4, 0, 12)
            Grid grid = CreateBlockedGrid();
            grid.Cells[0, 1].Cost = 1;

            GameObject prefab = CreateDummyPrefab("ClampedEdgeDrop");
            Vector3 enemyPos = new Vector3(-20f, 0f, -20f);
            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: new[] { CreateDropEntry(prefab, 100f, 0.85f) },
                gridManager: new FakeGridManager(grid),
                enemyPosition: enemyPos,
                scatterRadius: 14f);

            // Act
            KillEnemy(harness);

            // Assert
            Assert.AreEqual(1, harness.FakeNotifier.SpawnCalls.Count);
            AssertVector3Equal(new Vector3(4f, 0.85f, 12f), harness.FakeNotifier.SpawnCalls[0].TargetPos);
        }

        [Test]
        public void HealthOnNoHealth_WhenWorldGridIsNull_FallsBackToSpawnPositionWithoutThrowing()
        {
            // Arrange — Documented fallback contract in collectibles-system.md when IGridManager is uninitialized
            GameObject prefab = CreateDummyPrefab("NullGridFallbackDrop");
            Vector3 enemyPos = new Vector3(12f, 0f, -8f);
            DropHandlerHarness harness = CreateDropHandlerHarness(
                entries: new[] { CreateDropEntry(prefab, 100f, 0.85f) },
                gridManager: new FakeGridManager(null),
                enemyPosition: enemyPos);

            // Act & Assert
            Assert.DoesNotThrow(delegate { KillEnemy(harness); });
            Assert.AreEqual(1, harness.FakeNotifier.SpawnCalls.Count);
            AssertVector3Equal(new Vector3(12f, 0.85f, -8f), harness.FakeNotifier.SpawnCalls[0].SpawnPos);
            AssertVector3Equal(new Vector3(12f, 0.85f, -8f), harness.FakeNotifier.SpawnCalls[0].TargetPos);
        }

        [Test]
        public void HealthOnNoHealth_NullPrefabEntryBeforeValidEntry_SkipsNullEntryAndSpawnsValidDrop()
        {
            // Arrange
            GameObject cratePrefab = CreateSkillCrateTemplate("ValidSecondEntryCrate");
            IntegrationHarness harness = CreateIntegrationHarness(
                entries: new[]
                {
                    CreateDropEntry(null, 100f, 0.85f),
                    CreateDropEntry(cratePrefab, 100f, 0.85f)
                },
                grid: CreateWalkableGrid(),
                enemyPosition: new Vector3(44f, 0f, 60f));

            // Act & Assert
            Assert.DoesNotThrow(delegate { KillEnemy(harness.Health); });
            Assert.AreEqual(1, harness.ItemsParent.childCount);
            Assert.IsNotNull(harness.ItemsParent.GetChild(0).GetComponent<SkillCrate>());
        }

        [Test]
        public void EndToEnd_EnemyDeathToCollectiblePickup_SpawnsRoutesEventsAndReturnsToPool()
        {
            // Arrange
            GameObject cratePrefab = CreateSkillCrateTemplate("E2ESkillCrate");
            GameObject nukePrefab = CreatePowerUpTemplate("E2ENuke", PowerUpType.Nuke);
            IntegrationHarness harness = CreateIntegrationHarness(
                entries: new[]
                {
                    CreateDropEntry(cratePrefab, 100f, 0.85f),
                    CreateDropEntry(nukePrefab, 100f, 0.85f)
                },
                grid: CreateWalkableGrid(),
                enemyPosition: new Vector3(44f, 0f, 60f));

            int skillEventCount = 0;
            List<PowerUpType> powerUpEvents = new();
            harness.Notifier.OnSkillUpgradeCollectibleCollected += delegate { skillEventCount++; };
            harness.Notifier.OnPowerUpCollected += delegate(PowerUpType type) { powerUpEvents.Add(type); };

            // Act 1 — Enemy death triggers EnemyDropHandler -> CollectibleDropNotifier
            KillEnemy(harness.Health);

            Assert.AreEqual(2, harness.ItemsParent.childCount);
            SkillCrate spawnedCrate = harness.ItemsParent.GetChild(0).GetComponent<SkillCrate>();
            PowerUpCollectible spawnedNuke = harness.ItemsParent.GetChild(1).GetComponent<PowerUpCollectible>();
            Assert.IsTrue(spawnedCrate.gameObject.activeSelf);
            Assert.IsTrue(spawnedNuke.gameObject.activeSelf);

            // Act 2 — Player picks up both spawned collectibles
            Collider playerCollider = CreatePlayerCollider();
            InvokeTriggerEnter(spawnedCrate, playerCollider);
            InvokeTriggerEnter(spawnedNuke, playerCollider);

            // Assert
            Assert.AreEqual(1, skillEventCount);
            CollectionAssert.AreEqual(new[] { PowerUpType.Nuke }, powerUpEvents);
            Assert.IsFalse(spawnedCrate.gameObject.activeSelf);
            Assert.IsFalse(spawnedNuke.gameObject.activeSelf);
            Assert.AreEqual(0, GetActiveTweenCount(spawnedCrate.transform));
            Assert.AreEqual(0, GetActiveTweenCount(spawnedNuke.transform));
        }

        [Test]
        public void EndToEnd_DropSuppressionWindow_BlocksEnemyDropsDuringSuppressionAndRestoresAfterEnd()
        {
            // Arrange
            GameObject cratePrefab = CreateSkillCrateTemplate("SuppressedE2ECrate");
            IntegrationHarness harness = CreateIntegrationHarness(
                entries: new[] { CreateDropEntry(cratePrefab, 100f, 0.85f) },
                grid: CreateWalkableGrid(),
                enemyPosition: new Vector3(44f, 0f, 60f));

            // Act 1 — Enemy dies while Nuke-style suppression is active -> 0 drops
            harness.Suppressor.BeginDropSuppression();
            KillEnemy(harness.Health);
            harness.Suppressor.EndDropSuppression();
            Assert.AreEqual(0, harness.ItemsParent.childCount);

            // Act 2 — Enemy resets and dies after suppression ends -> drop spawns
            ResetEnemyForNextPoolLife(harness.Health, harness.DropHandler);
            KillEnemy(harness.Health);

            // Assert
            Assert.AreEqual(1, harness.ItemsParent.childCount);
            Assert.IsTrue(harness.ItemsParent.GetChild(0).gameObject.activeSelf);
        }

        private DropHandlerHarness CreateDropHandlerHarness(
            CollectibleDropEntry[] entries,
            IGridManager gridManager,
            Vector3? enemyPosition = null,
            float scatterRadius = 2f,
            bool includeSuppressor = true,
            bool isSuppressed = false)
        {
            FakeCollectibleDropNotifier fakeNotifier = new FakeCollectibleDropNotifier();
            FakeCollectibleDropSuppressor fakeSuppressor = new FakeCollectibleDropSuppressor(isSuppressed);
            DropAnimationConfiguration config = CreateAnimationConfig(scatterRadius);

            Container container = BuildContainer(delegate(ContainerBuilder builder)
            {
                builder.AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>));
                builder.AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState));
                builder.AddSingleton(fakeNotifier, typeof(ICollectibleDropNotifier));
                builder.AddSingleton(fakeSuppressor, typeof(ICollectibleDropSuppressor));
                if (gridManager != null)
                {
                    builder.AddSingleton(gridManager, typeof(IGridManager));
                }
                builder.AddSingleton(config);
            });

            SetupEnemyObject(
                entries,
                enemyPosition ?? new Vector3(44f, 0f, 60f),
                container,
                out Health health,
                out EnemyDropHandler dropHandler);

            if (!includeSuppressor)
            {
                SetPrivateField(typeof(EnemyDropHandler), dropHandler, "_dropSuppressor", null);
            }

            return new DropHandlerHarness(health, dropHandler, fakeNotifier);
        }

        private IntegrationHarness CreateIntegrationHarness(
            CollectibleDropEntry[] entries,
            Grid grid,
            Vector3 enemyPosition,
            float scatterRadius = 2f)
        {
            DropAnimationConfiguration config = CreateAnimationConfig(scatterRadius);

            GameObject itemsParentGo = new GameObject("IntegrationItemsParent");
            _createdObjects.Add(itemsParentGo);

            GameObject notifierGo = new GameObject("IntegrationDropNotifier");
            notifierGo.SetActive(false);
            _createdObjects.Add(notifierGo);
            CollectibleDropNotifier notifier = notifierGo.AddComponent<CollectibleDropNotifier>();
            SetPrivateField(typeof(CollectibleDropNotifier), notifier, "_collectibleItemsParent", itemsParentGo.transform);

            FakeGridManager gridManager = new FakeGridManager(grid);

            Container container = BuildContainer(delegate(ContainerBuilder builder)
            {
                builder.AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>));
                builder.AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState));
                builder.AddSingleton(config);
                builder.AddSingleton(notifier, typeof(ICollectibleDropNotifier));
                builder.AddSingleton(notifier, typeof(ICollectibleDropSuppressor));
                builder.AddSingleton(gridManager, typeof(IGridManager));
            });

            GameObjectInjector.InjectObject(notifierGo, container);
            SetupEnemyObject(entries, enemyPosition, container, out Health health, out EnemyDropHandler dropHandler);

            return new IntegrationHarness(health, dropHandler, notifier, notifier, itemsParentGo.transform);
        }

        private void SetupEnemyObject(
            CollectibleDropEntry[] entries,
            Vector3 position,
            Container container,
            out Health health,
            out EnemyDropHandler dropHandler)
        {
            GameObject enemyGo = new GameObject("TestEnemy");
            enemyGo.SetActive(false);
            enemyGo.transform.position = position;
            _createdObjects.Add(enemyGo);

            health = enemyGo.AddComponent<Health>();
            health.MaxHealth = 100f;
            Enemy enemy = enemyGo.AddComponent<Enemy>();
            dropHandler = enemyGo.AddComponent<EnemyDropHandler>();
            SetPrivateField(typeof(EnemyDropHandler), dropHandler, "_dropEntries", entries);

            GameObjectInjector.InjectRecursive(enemyGo, container);

            InvokeLifecycleMethod(health, "OnEnable");
            InvokeLifecycleMethod(enemy, "Awake");
            InvokeLifecycleMethod(enemy, "OnEnable");
            InvokeLifecycleMethod(dropHandler, "Awake");
            InvokeLifecycleMethod(dropHandler, "OnEnable");
        }

        private DropAnimationConfiguration CreateAnimationConfig(float scatterRadius)
        {
            DropAnimationConfiguration config = ScriptableObject.CreateInstance<DropAnimationConfiguration>();
            _createdObjects.Add(config);
            SetPrivateField(typeof(DropAnimationConfiguration), config, "_scatterRadius", scatterRadius);
            return config;
        }

        private GameObject CreateDummyPrefab(string name)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false);
            _createdObjects.Add(go);
            return go;
        }

        private GameObject CreateSkillCrateTemplate(string name)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false);
            _createdObjects.Add(go);
            go.AddComponent<SkillCrate>();
            return go;
        }

        private GameObject CreatePowerUpTemplate(string name, PowerUpType type)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false);
            _createdObjects.Add(go);
            PowerUpCollectible collectible = go.AddComponent<PowerUpCollectible>();
            SetPrivateField(typeof(PowerUpCollectible), collectible, "_type", type);
            return go;
        }

        private Collider CreatePlayerCollider()
        {
            GameObject go = new GameObject("PlayerCollider");
            int layer = GetLayerIndexFromMask(EntityLayers.Player.value);
            go.layer = layer < 0 ? 0 : layer;
            _createdObjects.Add(go);
            return go.AddComponent<BoxCollider>();
        }

        private Container BuildContainer(Action<ContainerBuilder> configure)
        {
            ContainerBuilder builder = new ContainerBuilder().SetName("EnemyDropHandlerTestContainer");
            configure?.Invoke(builder);
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private static Grid CreateWalkableGrid()
        {
            return new Grid(new GridConfiguration(GRID_DIMENSION, GRID_DIMENSION, GRID_CELL_SIZE));
        }

        private static Grid CreateBlockedGrid()
        {
            Grid grid = CreateWalkableGrid();
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    grid.Cells[x, y].Cost = byte.MaxValue;
                }
            }

            return grid;
        }

        private static CollectibleDropEntry CreateDropEntry(GameObject prefab, float dropChancePercent, float yOffset)
        {
            object boxed = default(CollectibleDropEntry);
            Type entryType = typeof(CollectibleDropEntry);
            entryType.GetField("_prefab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boxed, prefab);
            entryType.GetField("_dropChancePercent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boxed, dropChancePercent);
            entryType.GetField("_yOffset", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boxed, yOffset);
            return (CollectibleDropEntry)boxed;
        }

        private static void KillEnemy(DropHandlerHarness harness)
        {
            KillEnemy(harness.Health);
        }

        private static void KillEnemy(Health health)
        {
            health.DecreaseHealth(health.MaxHealth);
        }

        private static void ResetEnemyForNextPoolLife(DropHandlerHarness harness)
        {
            ResetEnemyForNextPoolLife(harness.Health, harness.DropHandler);
        }

        private static void ResetEnemyForNextPoolLife(Health health, EnemyDropHandler dropHandler)
        {
            InvokeLifecycleMethod(dropHandler, "OnDisable");
            InvokeLifecycleMethod(health, "OnDisable");
            InvokeLifecycleMethod(health, "OnEnable");
            InvokeLifecycleMethod(dropHandler, "OnEnable");
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static void AssertHorizontalRadiusAndAngle(Vector3 origin, Vector3 target, float expectedRadius, int expectedCosSign)
        {
            float dx = target.x - origin.x;
            float dz = target.z - origin.z;
            float radius = Mathf.Sqrt(dx * dx + dz * dz);
            Assert.AreEqual(expectedRadius, radius, TOLERANCE);

            if (expectedCosSign > 0)
            {
                Assert.Greater(dx, 0f);
            }
            else
            {
                Assert.Less(dx, 0f);
            }

            float maxSin15Ratio = Mathf.Tan(15f * Mathf.Deg2Rad) + TOLERANCE;
            Assert.LessOrEqual(Mathf.Abs(dz / dx), maxSin15Ratio);
        }

        private static void AssertVector3Equal(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, TOLERANCE);
            Assert.AreEqual(expected.y, actual.y, TOLERANCE);
            Assert.AreEqual(expected.z, actual.z, TOLERANCE);
        }

        private static int GetLayerIndexFromMask(int mask)
        {
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int GetActiveTweenCount(Transform target)
        {
            List<Tween> tweens = DOTween.TweensByTarget(target);
            if (tweens == null)
            {
                return 0;
            }

            int active = 0;
            for (int i = 0; i < tweens.Count; i++)
            {
                if (tweens[i] != null && tweens[i].IsActive())
                {
                    active++;
                }
            }

            return active;
        }

        private static void InvokeTriggerEnter(MonoBehaviour behaviour, Collider other)
        {
            MethodInfo method = behaviour.GetType().GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
            Action<Collider> callback = (Action<Collider>)Delegate.CreateDelegate(typeof(Action<Collider>), behaviour, method);
            callback(other);
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

        private readonly struct SpawnRecord
        {
            public readonly GameObject Prefab;
            public readonly Vector3 SpawnPos;
            public readonly Vector3 TargetPos;

            public SpawnRecord(GameObject prefab, Vector3 spawnPos, Vector3 targetPos)
            {
                Prefab = prefab;
                SpawnPos = spawnPos;
                TargetPos = targetPos;
            }
        }

        private readonly struct DropHandlerHarness
        {
            public readonly Health Health;
            public readonly EnemyDropHandler DropHandler;
            public readonly FakeCollectibleDropNotifier FakeNotifier;

            public DropHandlerHarness(
                Health health,
                EnemyDropHandler dropHandler,
                FakeCollectibleDropNotifier fakeNotifier)
            {
                Health = health;
                DropHandler = dropHandler;
                FakeNotifier = fakeNotifier;
            }
        }

        private readonly struct IntegrationHarness
        {
            public readonly Health Health;
            public readonly EnemyDropHandler DropHandler;
            public readonly CollectibleDropNotifier Notifier;
            public readonly ICollectibleDropSuppressor Suppressor;
            public readonly Transform ItemsParent;

            public IntegrationHarness(
                Health health,
                EnemyDropHandler dropHandler,
                CollectibleDropNotifier notifier,
                ICollectibleDropSuppressor suppressor,
                Transform itemsParent)
            {
                Health = health;
                DropHandler = dropHandler;
                Notifier = notifier;
                Suppressor = suppressor;
                ItemsParent = itemsParent;
            }
        }

        private sealed class FakeCollectibleDropNotifier : ICollectibleDropNotifier
        {
            public List<SpawnRecord> SpawnCalls { get; private set; } = new();

            public event EventHandler OnSkillUpgradeCollectibleCollected;
            public event Action<PowerUpType> OnPowerUpCollected;

            public void SpawnCollectible(GameObject prefab, Vector3 spawnPos, Vector3 targetPos)
            {
                SpawnCalls.Add(new SpawnRecord(prefab, spawnPos, targetPos));
            }

            public void SilenceUnusedEventWarnings()
            {
                OnSkillUpgradeCollectibleCollected?.Invoke(this, EventArgs.Empty);
                OnPowerUpCollected?.Invoke(PowerUpType.Nuke);
            }
        }

        private sealed class FakeCollectibleDropSuppressor : ICollectibleDropSuppressor
        {
            private bool _isSuppressed;

            public FakeCollectibleDropSuppressor(bool isSuppressed)
            {
                _isSuppressed = isSuppressed;
            }

            public bool AreDropsSuppressed { get { return _isSuppressed; } }

            public void BeginDropSuppression()
            {
                _isSuppressed = true;
            }

            public void EndDropSuppression()
            {
                _isSuppressed = false;
            }
        }

        private sealed class FakeGridManager : IGridManager
        {
            public FakeGridManager(Grid worldGrid)
            {
                WorldGrid = worldGrid;
            }

            public Cell DestinationCell { get { return null; } }
            public Grid GridPlayerChunk { get { return WorldGrid; } }
            public Grid WorldGrid { get; private set; }
        }

        private sealed class FakeTimeFreezeState : ITimeFreezeState
        {
            public bool IsFrozen { get { return false; } }
            public float UnfrozenTime { get { return 0f; } }

            public event Action<bool> OnFrozenChanged
            {
                add { }
                remove { }
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
    }
}
