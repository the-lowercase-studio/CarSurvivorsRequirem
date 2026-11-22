using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Collisions;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.ObjectLifecycle.Actions;
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
    public class EnemyDropsAndCollectibleNotifierTests
    {
        private const float TOLERANCE = 0.0001f;
        private const int DEFAULT_RANDOM_SEED = 54321;

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
        public void EnemyDropHandler_WhenDropsSuppressedOrEntriesMissing_SkipsSpawning()
        {
            // Arrange
            Grid grid = CreateWalkableGrid(10, 10, 2f);
            GameObject prefab = CreatePlainObject("DropPrefab");
            CollectibleDropEntry entry = CreateDropEntry(prefab, chancePercent: 100f, yOffset: 0.5f);

            DropHarness suppressedHarness = CreateDropHandlerHarness(
                grid,
                scatterRadius: 2f,
                startPos: new Vector3(5f, 0f, 9f),
                entries: new[] { entry },
                areDropsSuppressed: true);

            DropHarness nullEntriesHarness = CreateDropHandlerHarness(
                grid,
                scatterRadius: 2f,
                startPos: new Vector3(5f, 0f, 9f),
                entries: null,
                areDropsSuppressed: false);

            // Act
            suppressedHarness.Enemy.TakeFullHpDamage();
            nullEntriesHarness.Enemy.TakeFullHpDamage();

            // Assert
            Assert.AreEqual(0, suppressedHarness.Notifier.Spawns.Count);
            Assert.AreEqual(0, nullEntriesHarness.Notifier.Spawns.Count);
        }

        [Test]
        public void EnemyDropHandler_ZeroPercentVsHundredPercentEntries_SpawnsGuaranteedDropsWithYOffsetAndScatterRadius()
        {
            // Arrange
            Grid grid = CreateWalkableGrid(10, 10, 2f);
            GameObject guaranteedPrefab = CreatePlainObject("GuaranteedDrop");
            GameObject zeroChancePrefab = CreatePlainObject("ZeroChanceDrop");
            CollectibleDropEntry[] entries =
            {
                CreateDropEntry(zeroChancePrefab, chancePercent: 0f, yOffset: 0.25f),
                CreateDropEntry(guaranteedPrefab, chancePercent: 100f, yOffset: 0.75f)
            };

            DropHarness harness = CreateDropHandlerHarness(
                grid,
                scatterRadius: 2.0f,
                startPos: new Vector3(5f, 1.0f, 9f),
                entries: entries,
                areDropsSuppressed: false);

            // Act
            harness.Enemy.TakeFullHpDamage();

            // Assert
            Assert.AreEqual(1, harness.Notifier.Spawns.Count);
            RecordedCollectibleSpawn spawn = harness.Notifier.Spawns[0];
            Assert.AreSame(guaranteedPrefab, spawn.Prefab);
            Assert.AreEqual(5f, spawn.SpawnPos.x, TOLERANCE);
            Assert.AreEqual(1.75f, spawn.SpawnPos.y, TOLERANCE);
            Assert.AreEqual(9f, spawn.SpawnPos.z, TOLERANCE);
            Assert.AreEqual(1.75f, spawn.TargetPos.y, TOLERANCE);

            float horizontalDistance = Vector2.Distance(
                new Vector2(spawn.SpawnPos.x, spawn.SpawnPos.z),
                new Vector2(spawn.TargetPos.x, spawn.TargetPos.z));
            Assert.AreEqual(2.0f, horizontalDistance, 0.001f);
        }

        [Test]
        public void EnemyDropHandler_OnDisable_UnsubscribesFromHealthOnNoHealth()
        {
            // Arrange
            Grid grid = CreateWalkableGrid(10, 10, 2f);
            GameObject prefab = CreatePlainObject("DropPrefab");
            CollectibleDropEntry entry = CreateDropEntry(prefab, chancePercent: 100f, yOffset: 0f);
            DropHarness harness = CreateDropHandlerHarness(
                grid,
                scatterRadius: 2f,
                startPos: new Vector3(5f, 0f, 9f),
                entries: new[] { entry },
                areDropsSuppressed: false);

            // Act
            InvokeLifecycleMethod(harness.DropHandler, "OnDisable");
            harness.DropHandler.enabled = false;
            harness.Enemy.TakeFullHpDamage();

            // Assert
            Assert.AreEqual(0, harness.Notifier.Spawns.Count);
        }

        [Test]
        public void EnemyDropHandler_WalkableResolution_StepsAlongRayThenStartCellThenSpiralThenFallback()
        {
            // Arrange — Start at cell (2,4) center (5, 0, 9), ScatterRadius = 3.5 -> target ALWAYS lands in cell (4,4).
            GameObject prefab = CreatePlainObject("ResolutionDrop");
            CollectibleDropEntry entry = CreateDropEntry(prefab, chancePercent: 100f, yOffset: 0.4f);

            // Step 2 (ray step 3/4): (4,4) blocked, (3,4) walkable -> distance = 3.5 * 0.75 = 2.625
            Grid rayGrid = CreateWalkableGrid(10, 10, 2f);
            rayGrid.Cells[4, 4].Cost = byte.MaxValue;
            DropHarness rayHarness = CreateDropHandlerHarness(rayGrid, 3.5f, new Vector3(5f, 0f, 9f), new[] { entry }, false);

            // Step 3 (start cell): start at east edge (5.95, 0, 9) of (2,4) so all ray steps land in blocked (3,4)/(4,4) -> returns exact startPos (5.95, 0.4, 9)
            Grid startGrid = CreateWalkableGrid(10, 10, 2f);
            startGrid.Cells[4, 4].Cost = byte.MaxValue;
            startGrid.Cells[3, 4].Cost = byte.MaxValue;
            DropHarness startHarness = CreateDropHandlerHarness(startGrid, 3.5f, new Vector3(5.95f, 0f, 9f), new[] { entry }, false);

            // Step 4 (5-ring spiral): all cells blocked EXCEPT (2, 7) at radius 3 -> lands at (5, 0.4, 15)
            Grid spiralGrid = CreateBlockedGrid(10, 10, 2f);
            spiralGrid.Cells[2, 7].Cost = GridConstants.DEFAULT_FIELD_COST;
            DropHarness spiralHarness = CreateDropHandlerHarness(spiralGrid, 3.5f, new Vector3(5f, 0f, 9f), new[] { entry }, false);

            // Step 5 (fallback): all cells blocked within 5 rings -> returns startPos (5, 0.4, 9)
            Grid blockedGrid = CreateBlockedGrid(10, 10, 2f);
            DropHarness blockedHarness = CreateDropHandlerHarness(blockedGrid, 3.5f, new Vector3(5f, 0f, 9f), new[] { entry }, false);

            // Act
            rayHarness.Enemy.TakeFullHpDamage();
            startHarness.Enemy.TakeFullHpDamage();
            spiralHarness.Enemy.TakeFullHpDamage();
            blockedHarness.Enemy.TakeFullHpDamage();

            // Assert
            float rayDistance = Vector2.Distance(
                new Vector2(5f, 9f),
                new Vector2(rayHarness.Notifier.Spawns[0].TargetPos.x, rayHarness.Notifier.Spawns[0].TargetPos.z));
            Assert.AreEqual(2.625f, rayDistance, 0.001f);

            AssertVector3Equal(new Vector3(5.95f, 0.4f, 9f), startHarness.Notifier.Spawns[0].TargetPos);
            AssertVector3Equal(new Vector3(5f, 0.4f, 15f), spiralHarness.Notifier.Spawns[0].TargetPos);
            AssertVector3Equal(new Vector3(5f, 0.4f, 9f), blockedHarness.Notifier.Spawns[0].TargetPos);
        }

        [Test]
        public void CollectibleDropNotifier_NestedSuppression_TracksDepthAndClampsAtZero()
        {
            // Arrange
            DropAnimationConfiguration animConfig = CreateDropAnimationConfig(scatterRadius: 2f);
            CollectibleDropNotifier notifier = CreateDropNotifier(animConfig, out _);

            // Act & Assert
            Assert.IsFalse(notifier.AreDropsSuppressed);

            notifier.EndDropSuppression();
            Assert.IsFalse(notifier.AreDropsSuppressed);

            notifier.BeginDropSuppression();
            notifier.BeginDropSuppression();
            Assert.IsTrue(notifier.AreDropsSuppressed);

            notifier.EndDropSuppression();
            Assert.IsTrue(notifier.AreDropsSuppressed);

            notifier.EndDropSuppression();
            Assert.IsFalse(notifier.AreDropsSuppressed);
        }

        [Test]
        public void CollectibleDropNotifier_SpawnCollectible_AnimatesScaleAndJumpAndEmitsEventsPerCollectibleType()
        {
            // Arrange
            DropAnimationConfiguration animConfig = CreateDropAnimationConfig(scatterRadius: 2f);
            CollectibleDropNotifier notifier = CreateDropNotifier(animConfig, out Transform parent);
            GameObject cratePrefab = CreateSkillCratePrefab();
            GameObject powerUpPrefab = CreatePowerUpPrefab(PowerUpType.FreezeTimer);

            int skillCollectedCount = 0;
            List<PowerUpType> collectedPowerUps = new List<PowerUpType>();
            notifier.OnSkillUpgradeCollectibleCollected += delegate { skillCollectedCount++; };
            notifier.OnPowerUpCollected += delegate (PowerUpType type) { collectedPowerUps.Add(type); };

            Vector3 crateTarget = new Vector3(4f, 0.5f, 6f);
            Vector3 powerUpTarget = new Vector3(-3f, 0.5f, 2f);

            // Act — Spawn both and complete DOTween jump/scale sequences
            notifier.SpawnCollectible(cratePrefab, Vector3.zero, crateTarget);
            notifier.SpawnCollectible(powerUpPrefab, Vector3.zero, powerUpTarget);

            GameObject crateInstance = parent.GetChild(0).gameObject;
            GameObject powerUpInstance = parent.GetChild(1).gameObject;
            DOTween.CompleteAll(withCallbacks: true);

            Vector3 crateEndPos = crateInstance.transform.position;
            Vector3 crateEndScale = crateInstance.transform.localScale;

            // Simulate player trigger collection on both
            Collider playerCollider = CreatePlayerCollider();
            InvokeTriggerEnter(crateInstance.GetComponent<SkillCrate>(), playerCollider);
            InvokeTriggerEnter(powerUpInstance.GetComponent<PowerUpCollectible>(), playerCollider);

            // Assert
            AssertVector3Equal(crateTarget, crateEndPos);
            AssertVector3Equal(Vector3.one, crateEndScale);
            Assert.AreEqual(1, skillCollectedCount);
            Assert.AreEqual(1, collectedPowerUps.Count);
            Assert.AreEqual(PowerUpType.FreezeTimer, collectedPowerUps[0]);
            Assert.IsFalse(crateInstance.activeSelf);
            Assert.IsFalse(powerUpInstance.activeSelf);
        }

        [Test]
        public void CollectibleDropNotifier_PoolReuse_ReusesReleasedInstanceWithoutDuplicateCollectedEvents()
        {
            // Arrange
            DropAnimationConfiguration animConfig = CreateDropAnimationConfig(scatterRadius: 2f);
            CollectibleDropNotifier notifier = CreateDropNotifier(animConfig, out Transform parent);
            GameObject cratePrefab = CreateSkillCratePrefab();
            Collider playerCollider = CreatePlayerCollider();

            int collectedCount = 0;
            notifier.OnSkillUpgradeCollectibleCollected += delegate { collectedCount++; };

            // Act — Spawn 1 -> collect -> release -> spawn 2 -> collect
            notifier.SpawnCollectible(cratePrefab, Vector3.zero, new Vector3(1f, 0f, 1f));
            GameObject firstInstance = parent.GetChild(0).gameObject;
            InvokeTriggerEnter(firstInstance.GetComponent<SkillCrate>(), playerCollider);

            notifier.SpawnCollectible(cratePrefab, Vector3.zero, new Vector3(2f, 0f, 2f));
            GameObject secondInstance = parent.GetChild(0).gameObject;
            InvokeTriggerEnter(secondInstance.GetComponent<SkillCrate>(), playerCollider);

            // Assert
            Assert.AreEqual(1, parent.childCount);
            Assert.AreSame(firstInstance, secondInstance);
            Assert.AreEqual(2, collectedCount);
        }

        private CollectibleDropNotifier CreateDropNotifier(DropAnimationConfiguration animConfig, out Transform itemsParent)
        {
            GameObject parentGo = new GameObject("CollectibleItemsParent");
            _createdObjects.Add(parentGo);
            itemsParent = parentGo.transform;

            GameObject notifierGo = new GameObject("TestCollectibleDropNotifier");
            notifierGo.SetActive(false);
            _createdObjects.Add(notifierGo);
            CollectibleDropNotifier notifier = notifierGo.AddComponent<CollectibleDropNotifier>();
            SetPrivateField(typeof(CollectibleDropNotifier), notifier, "_collectibleItemsParent", itemsParent);

            Container container = new ContainerBuilder()
                .AddSingleton(animConfig)
                .Build();
            _createdContainers.Add(container);
            GameObjectInjector.InjectRecursive(notifierGo, container);
            notifierGo.SetActive(true);

            return notifier;
        }

        private DropHarness CreateDropHandlerHarness(
            Grid grid,
            float scatterRadius,
            Vector3 startPos,
            CollectibleDropEntry[] entries,
            bool areDropsSuppressed)
        {
            GameObject root = new GameObject("DropTestEnemy");
            root.SetActive(false);
            root.transform.position = startPos;
            _createdObjects.Add(root);

            Health health = root.AddComponent<Health>();
            root.AddComponent<FakeCollisionsController>();
            root.AddComponent<FakeMovementController>();
            root.AddComponent<FakeDeathSequence>();
            Enemy enemy = root.AddComponent<Enemy>();
            EnemyDropHandler dropHandler = root.AddComponent<EnemyDropHandler>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<Animator>();
            EnemyAnimator animator = visual.AddComponent<EnemyAnimator>();

            EnemyConfigSO config = ScriptableObject.CreateInstance<EnemyConfigSO>();
            config.MaxHealth = 50f;
            _createdObjects.Add(config);

            DropAnimationConfiguration animConfig = CreateDropAnimationConfig(scatterRadius);

            SetPrivateField(typeof(Enemy), enemy, "<Config>k__BackingField", config);
            SetPrivateField(typeof(Enemy), enemy, "_visual", visual);
            SetPrivateField(typeof(EnemyAnimator), animator, "_enemy", enemy);
            SetPrivateField(typeof(EnemyDropHandler), dropHandler, "_dropEntries", entries);

            FakeDropNotifier fakeNotifier = new FakeDropNotifier();
            FakeDropSuppressor fakeSuppressor = new FakeDropSuppressor(areDropsSuppressed);

            Container container = new ContainerBuilder()
                .AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>))
                .AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState))
                .AddSingleton(fakeNotifier, typeof(ICollectibleDropNotifier))
                .AddSingleton(fakeSuppressor, typeof(ICollectibleDropSuppressor))
                .AddSingleton(new FakeGridManager(grid), typeof(IGridManager))
                .AddSingleton(animConfig)
                .Build();
            _createdContainers.Add(container);
            GameObjectInjector.InjectRecursive(root, container);

            root.SetActive(true);

            InvokeLifecycleMethod(animator, "Awake");
            InvokeLifecycleMethod(enemy, "Awake");
            InvokeLifecycleMethod(dropHandler, "Awake");

            InvokeLifecycleMethod(health, "OnEnable");
            InvokeLifecycleMethod(enemy, "OnEnable");
            InvokeLifecycleMethod(dropHandler, "OnEnable");

            return new DropHarness(root, enemy, dropHandler, fakeNotifier);
        }

        private DropAnimationConfiguration CreateDropAnimationConfig(float scatterRadius)
        {
            DropAnimationConfiguration config = ScriptableObject.CreateInstance<DropAnimationConfiguration>();
            SetPrivateField(typeof(DropAnimationConfiguration), config, "_scatterRadius", scatterRadius);
            _createdObjects.Add(config);
            return config;
        }

        private GameObject CreateSkillCratePrefab()
        {
            GameObject prefab = new GameObject("SkillCratePrefab");
            prefab.SetActive(false);
            prefab.AddComponent<SkillCrate>();
            _createdObjects.Add(prefab);
            return prefab;
        }

        private GameObject CreatePowerUpPrefab(PowerUpType type)
        {
            GameObject prefab = new GameObject("PowerUpPrefab");
            prefab.SetActive(false);
            PowerUpCollectible collectible = prefab.AddComponent<PowerUpCollectible>();
            SetPrivateField(typeof(PowerUpCollectible), collectible, "_type", type);
            _createdObjects.Add(prefab);
            return prefab;
        }

        private Collider CreatePlayerCollider()
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            Assert.GreaterOrEqual(playerLayer, 0);

            GameObject go = new GameObject("CollectiblePlayerCollider");
            go.layer = playerLayer;
            _createdObjects.Add(go);
            return go.AddComponent<BoxCollider>();
        }

        private GameObject CreatePlainObject(string name)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false);
            _createdObjects.Add(go);
            return go;
        }

        private CollectibleDropEntry CreateDropEntry(GameObject prefab, float chancePercent, float yOffset)
        {
            object boxed = new CollectibleDropEntry();
            SetPrivateField(typeof(CollectibleDropEntry), boxed, "_prefab", prefab);
            SetPrivateField(typeof(CollectibleDropEntry), boxed, "_dropChancePercent", chancePercent);
            SetPrivateField(typeof(CollectibleDropEntry), boxed, "_yOffset", yOffset);
            return (CollectibleDropEntry)boxed;
        }

        private static Grid CreateWalkableGrid(int width, int height, float cellSize)
        {
            return new Grid(new GridConfiguration(width, height, cellSize));
        }

        private static Grid CreateBlockedGrid(int width, int height, float cellSize)
        {
            Grid grid = new Grid(new GridConfiguration(width, height, cellSize));
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    grid.Cells[x, y].Cost = byte.MaxValue;
                }
            }

            return grid;
        }

        private static void AssertVector3Equal(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, TOLERANCE);
            Assert.AreEqual(expected.y, actual.y, TOLERANCE);
            Assert.AreEqual(expected.z, actual.z, TOLERANCE);
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

            Assert.Fail($"Method '{methodName}' not found on {target.GetType().Name}.");
        }

        private static void InvokeTriggerEnter(object target, Collider other)
        {
            MethodInfo method = target.GetType().GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing OnTriggerEnter on {target.GetType().Name}");
            method.Invoke(target, new object[] { other });
        }

        private readonly struct DropHarness
        {
            public readonly GameObject Root;
            public readonly Enemy Enemy;
            public readonly EnemyDropHandler DropHandler;
            public readonly FakeDropNotifier Notifier;

            public DropHarness(GameObject root, Enemy enemy, EnemyDropHandler dropHandler, FakeDropNotifier notifier)
            {
                Root = root;
                Enemy = enemy;
                DropHandler = dropHandler;
                Notifier = notifier;
            }
        }

        private sealed class FakeDropNotifier : ICollectibleDropNotifier
        {
            public List<RecordedCollectibleSpawn> Spawns { get; } = new List<RecordedCollectibleSpawn>();

            public event EventHandler OnSkillUpgradeCollectibleCollected { add { } remove { } }
            public event Action<PowerUpType> OnPowerUpCollected { add { } remove { } }

            public void SpawnCollectible(GameObject prefab, Vector3 spawnPos, Vector3 targetPos)
            {
                Spawns.Add(new RecordedCollectibleSpawn(prefab, spawnPos, targetPos));
            }
        }

        private readonly struct RecordedCollectibleSpawn
        {
            public readonly GameObject Prefab;
            public readonly Vector3 SpawnPos;
            public readonly Vector3 TargetPos;

            public RecordedCollectibleSpawn(GameObject prefab, Vector3 spawnPos, Vector3 targetPos)
            {
                Prefab = prefab;
                SpawnPos = spawnPos;
                TargetPos = targetPos;
            }
        }

        private sealed class FakeDropSuppressor : ICollectibleDropSuppressor
        {
            public bool AreDropsSuppressed { get; }

            public FakeDropSuppressor(bool areDropsSuppressed)
            {
                AreDropsSuppressed = areDropsSuppressed;
            }

            public void BeginDropSuppression()
            {
            }

            public void EndDropSuppression()
            {
            }
        }

        private sealed class FakeGridManager : IGridManager
        {
            public Cell DestinationCell { get { return null; } }
            public Grid GridPlayerChunk { get { return WorldGrid; } }
            public Grid WorldGrid { get; }

            public FakeGridManager(Grid worldGrid)
            {
                WorldGrid = worldGrid;
            }
        }

        private sealed class FakeCollisionsController : MonoBehaviour, ICollisionsController
        {
            public event EventHandler<CollisionEventArgs> OnCollisionWithPlayer { add { } remove { } }
        }

        private sealed class FakeMovementController : MonoBehaviour, IMovementController
        {
            public bool CanBeTeleported { get { return true; } }
            public void ResetAfterRelocation() { }
            public float GetCurrentMovementSpeed() { return 0f; }
            public bool IsOnGround() { return true; }
            public void MoveToPositionInTimeIgnoringSpeed(Vector3 pos, float time) { }
            public void SetMovementSpeedMultiplier(float multiplier) { }
        }

        private sealed class FakeDeathSequence : MonoBehaviour, INeedToCompleteBeforeDisable
        {
            public event EventHandler OnCompleted { add { } remove { } }
        }

        private sealed class FakeTimeFreezeState : ITimeFreezeState
        {
            public bool IsFrozen { get { return false; } }
            public float UnfrozenTime { get { return 0f; } }
            public event Action<bool> OnFrozenChanged { add { } remove { } }
        }

        private sealed class FakeDamageNumbersSpawner : IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>
        {
            public uint CurrentlySpawnedObjectsCount { get { return 0; } }
            public event EventHandler OnSpawnedEntityReleased { add { } remove { } }
            public void Spawn(Vector3 pos, DamageNubmersSpawnerConfig specificConfig, int count = 1) { }
        }
    }
}
