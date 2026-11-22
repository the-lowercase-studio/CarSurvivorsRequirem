using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Enemies.Constants;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.FlowFieldSystem;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Spawners.WorldSpace;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class EnemiesOutsidePlayerChunkTeleporterTests
    {
        private const float TOLERANCE = 0.0001f;
        private const int DEFAULT_RANDOM_SEED = 424242;
        private static readonly Vector3 TEST_ORIGIN = new Vector3(24000f, 500f, 24000f);

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
            Physics.SyncTransforms();
            UnityEngine.Random.state = _previousRandomState;
        }

        [TestCase(0, TestName = "TeleportEnemies_NullPlayerChunk_ReturnsSafely")]
        [TestCase(1, TestName = "TeleportEnemies_NullCellsArray_ReturnsSafely")]
        [TestCase(2, TestName = "TeleportEnemies_ZeroDimensionChunk_ReturnsSafely")]
        [TestCase(3, TestName = "TeleportEnemies_NullCornerCell_ReturnsSafely")]
        public void TeleportEnemies_WhenPlayerChunkDegenerate_ReturnsSafelyWithoutMovingEnemies(int degenerateKind)
        {
            // Arrange
            Grid chunk = CreateDegenerateChunk(degenerateKind);
            Transform enemiesHolder = CreateEnemiesHolder();
            Vector3 startPos = TEST_ORIGIN + new Vector3(100f, 0.02f, 100f);
            Enemy enemy = CreateFakeMovementEnemy("StrayEnemy", enemiesHolder, startPos, isAlive: true, canBeTeleported: true, active: true);
            EnemiesOutsidePlayerChunkTeleporter teleporter = CreateTeleporter(
                new FakeGridManager(chunk),
                CreateAllHiddenCamera(),
                enemiesHolder);

            // Act
            Assert.DoesNotThrow(() => teleporter.TeleportEnemiesFromOutsideToInsidePlayerChunk());

            // Assert
            AssertVector3Equal(startPos, enemy.transform.position);
            Assert.AreEqual(0, enemy.GetComponent<FakeMovementController>().ResetCount);
        }

        [Test]
        public void TeleportEnemies_EnemiesInsideOrOnExactChunkBounds_RemainUntouched()
        {
            // Arrange — 3x3 chunk with cellSize=2 spans [TEST_ORIGIN.x + 0 .. +6] x [TEST_ORIGIN.z + 0 .. +6]
            Grid chunk = CreateChunkGrid(3, 3, 2f);
            CreateFloorCollider(TEST_ORIGIN + new Vector3(3f, -0.5f, 3f), new Vector3(12f, 1f, 12f));
            Transform enemiesHolder = CreateEnemiesHolder();

            Vector3 centerPos = TEST_ORIGIN + new Vector3(3f, 0.02f, 3f);
            Vector3 minCornerPos = TEST_ORIGIN + new Vector3(0f, 0.02f, 0f);
            Vector3 maxCornerPos = TEST_ORIGIN + new Vector3(6f, 0.02f, 6f);

            Enemy centerEnemy = CreateFakeMovementEnemy("Center", enemiesHolder, centerPos, true, true, true);
            Enemy minEnemy = CreateFakeMovementEnemy("MinCorner", enemiesHolder, minCornerPos, true, true, true);
            Enemy maxEnemy = CreateFakeMovementEnemy("MaxCorner", enemiesHolder, maxCornerPos, true, true, true);

            EnemiesOutsidePlayerChunkTeleporter teleporter = CreateTeleporter(
                new FakeGridManager(chunk),
                CreateAllHiddenCamera(),
                enemiesHolder);

            // Act
            teleporter.TeleportEnemiesFromOutsideToInsidePlayerChunk();

            // Assert
            AssertVector3Equal(centerPos, centerEnemy.transform.position);
            AssertVector3Equal(minCornerPos, minEnemy.transform.position);
            AssertVector3Equal(maxCornerPos, maxEnemy.transform.position);
            Assert.AreEqual(0, centerEnemy.GetComponent<FakeMovementController>().ResetCount);
            Assert.AreEqual(0, minEnemy.GetComponent<FakeMovementController>().ResetCount);
            Assert.AreEqual(0, maxEnemy.GetComponent<FakeMovementController>().ResetCount);
        }

        [TestCase(-0.01f, 3f, TestName = "TeleportEnemies_OutsideMinusX_RelocatesInsideHiddenChunkCell")]
        [TestCase(6.01f, 3f, TestName = "TeleportEnemies_OutsidePlusX_RelocatesInsideHiddenChunkCell")]
        [TestCase(3f, -0.01f, TestName = "TeleportEnemies_OutsideMinusZ_RelocatesInsideHiddenChunkCell")]
        [TestCase(3f, 6.01f, TestName = "TeleportEnemies_OutsidePlusZ_RelocatesInsideHiddenChunkCell")]
        public void TeleportEnemies_EnemyStrictlyOutsideAnyBound_RelocatesToHiddenWalkableSupportedCellAndResets(
            float outsideOffsetX,
            float outsideOffsetZ)
        {
            // Arrange — Mark only cell (0,0) walkable inside 3x3 chunk; all others unwalkable
            Grid chunk = CreateChunkGrid(3, 3, 2f);
            MarkAllCellsUnwalkableExcept(chunk, 0, 0);
            Cell expectedCell = chunk.Cells[0, 0];
            CreateFloorCollider(TEST_ORIGIN + new Vector3(3f, -0.5f, 3f), new Vector3(12f, 1f, 12f));

            Transform enemiesHolder = CreateEnemiesHolder();
            Vector3 outsidePos = TEST_ORIGIN + new Vector3(outsideOffsetX, 0.02f, outsideOffsetZ);
            Enemy strayEnemy = CreateFakeMovementEnemy("Stray", enemiesHolder, outsidePos, isAlive: true, canBeTeleported: true, active: true);

            EnemiesOutsidePlayerChunkTeleporter teleporter = CreateTeleporter(
                new FakeGridManager(chunk),
                CreateAllHiddenCamera(),
                enemiesHolder);

            // Act
            teleporter.TeleportEnemiesFromOutsideToInsidePlayerChunk();

            // Assert
            Assert.AreEqual(expectedCell.WorldPos.x, strayEnemy.transform.position.x, TOLERANCE);
            Assert.AreEqual(TEST_ORIGIN.y + GridConstants.ROOT_GROUND_CLEARANCE, strayEnemy.transform.position.y, TOLERANCE);
            Assert.AreEqual(expectedCell.WorldPos.z, strayEnemy.transform.position.z, TOLERANCE);
            Assert.AreEqual(1, strayEnemy.GetComponent<FakeMovementController>().ResetCount);
        }

        [Test]
        public void TeleportEnemies_SkipsInactiveNonEnemyDeadAndNonTeleportableChildren()
        {
            // Arrange
            Grid chunk = CreateChunkGrid(3, 3, 2f);
            CreateFloorCollider(TEST_ORIGIN + new Vector3(3f, -0.5f, 3f), new Vector3(12f, 1f, 12f));
            Transform enemiesHolder = CreateEnemiesHolder();

            Vector3 p1 = TEST_ORIGIN + new Vector3(20f, 0.02f, 20f);
            Vector3 p2 = TEST_ORIGIN + new Vector3(22f, 0.02f, 20f);
            Vector3 p3 = TEST_ORIGIN + new Vector3(24f, 0.02f, 20f);
            Vector3 p4 = TEST_ORIGIN + new Vector3(26f, 0.02f, 20f);

            Enemy inactiveEnemy = CreateFakeMovementEnemy("Inactive", enemiesHolder, p1, isAlive: true, canBeTeleported: true, active: false);

            GameObject nonEnemy = new GameObject("NonEnemyChild");
            nonEnemy.transform.SetParent(enemiesHolder, false);
            nonEnemy.transform.position = p2;
            _createdObjects.Add(nonEnemy);

            Enemy deadEnemy = CreateFakeMovementEnemy("Dead", enemiesHolder, p3, isAlive: false, canBeTeleported: true, active: true);
            Enemy lockedEnemy = CreateFakeMovementEnemy("NonTeleportable", enemiesHolder, p4, isAlive: true, canBeTeleported: false, active: true);

            EnemiesOutsidePlayerChunkTeleporter teleporter = CreateTeleporter(
                new FakeGridManager(chunk),
                CreateAllHiddenCamera(),
                enemiesHolder);

            // Act
            teleporter.TeleportEnemiesFromOutsideToInsidePlayerChunk();

            // Assert
            AssertVector3Equal(p1, inactiveEnemy.transform.position);
            AssertVector3Equal(p2, nonEnemy.transform.position);
            AssertVector3Equal(p3, deadEnemy.transform.position);
            AssertVector3Equal(p4, lockedEnemy.transform.position);
            Assert.AreEqual(0, inactiveEnemy.GetComponent<FakeMovementController>().ResetCount);
            Assert.AreEqual(0, deadEnemy.GetComponent<FakeMovementController>().ResetCount);
            Assert.AreEqual(0, lockedEnemy.GetComponent<FakeMovementController>().ResetCount);
        }

        [Test]
        public void TeleportEnemies_WhenNoHiddenWalkableSupportedCellsExistInChunk_LeavesStraysUntouched()
        {
            // Arrange — All chunk cells are unwalkable (Cost = byte.MaxValue)
            Grid chunk = CreateChunkGrid(3, 3, 2f);
            MarkAllCellsUnwalkable(chunk);
            Transform enemiesHolder = CreateEnemiesHolder();

            Vector3 outsidePos = TEST_ORIGIN + new Vector3(20f, 0.02f, 20f);
            Enemy strayEnemy = CreateFakeMovementEnemy("UnplacedStray", enemiesHolder, outsidePos, isAlive: true, canBeTeleported: true, active: true);

            EnemiesOutsidePlayerChunkTeleporter teleporter = CreateTeleporter(
                new FakeGridManager(chunk),
                CreateAllHiddenCamera(),
                enemiesHolder);

            // Act
            teleporter.TeleportEnemiesFromOutsideToInsidePlayerChunk();

            // Assert
            AssertVector3Equal(outsidePos, strayEnemy.transform.position);
            Assert.AreEqual(0, strayEnemy.GetComponent<FakeMovementController>().ResetCount);
        }

        [Test]
        public void EnemyMovementController_CanBeTeleported_EnforcesThreeSecondUnfrozenGraceAcrossFreezeAndRelocation()
        {
            // Arrange — Enable at UnfrozenTime = 10s -> grace lasts until 13.0s
            CreateFloorCollider(TEST_ORIGIN + new Vector3(3f, -0.5f, 3f), new Vector3(30f, 1f, 30f));
            FakeTimeFreezeState timeFreeze = new FakeTimeFreezeState { UnfrozenTime = 10f, IsFrozen = false };
            Grid chunk = CreateChunkGrid(3, 3, 2f);
            Transform enemiesHolder = CreateEnemiesHolder();

            RealMovementEnemyHarness enemyHarness = CreateRealMovementEnemy(
                enemiesHolder,
                TEST_ORIGIN + new Vector3(1f, 0.02f, 1f),
                new FakeGridManager(chunk),
                timeFreeze);

            // Assert — Blocked immediately after OnEnable and just before 3.0s
            Assert.IsFalse(enemyHarness.Movement.CanBeTeleported);
            timeFreeze.UnfrozenTime = 10f + EnemyMovementConstants.POST_RELOCATION_TELEPORT_GRACE_SECONDS - 0.01f;
            Assert.IsFalse(enemyHarness.Movement.CanBeTeleported);

            // Freeze does not allow teleportation and paused UnfrozenTime does not expire grace
            timeFreeze.IsFrozen = true;
            Assert.IsFalse(enemyHarness.Movement.CanBeTeleported);

            timeFreeze.IsFrozen = false;
            Assert.IsFalse(enemyHarness.Movement.CanBeTeleported);

            // Reach exact 3.0s unfrozen boundary -> allowed
            timeFreeze.UnfrozenTime = 10f + EnemyMovementConstants.POST_RELOCATION_TELEPORT_GRACE_SECONDS;
            Assert.IsTrue(enemyHarness.Movement.CanBeTeleported);

            // ResetAfterRelocation restarts the 3.0s grace period from current UnfrozenTime (13.0s -> 16.0s)
            enemyHarness.Movement.ResetAfterRelocation();
            Assert.IsFalse(enemyHarness.Movement.CanBeTeleported);

            timeFreeze.UnfrozenTime = 13f + EnemyMovementConstants.POST_RELOCATION_TELEPORT_GRACE_SECONDS - 0.01f;
            Assert.IsFalse(enemyHarness.Movement.CanBeTeleported);

            timeFreeze.UnfrozenTime = 13f + EnemyMovementConstants.POST_RELOCATION_TELEPORT_GRACE_SECONDS;
            Assert.IsTrue(enemyHarness.Movement.CanBeTeleported);
        }

        [Test]
        public void EnemyMovementController_CanBeTeleported_ReturnsFalseWhenStoppedKnockedBackOrOffGround()
        {
            // Arrange
            GameObject floor = CreateFloorCollider(TEST_ORIGIN + new Vector3(3f, -0.5f, 3f), new Vector3(30f, 1f, 30f));
            FakeTimeFreezeState timeFreeze = new FakeTimeFreezeState { UnfrozenTime = 10f, IsFrozen = false };
            Grid chunk = CreateChunkGrid(3, 3, 2f);
            Transform enemiesHolder = CreateEnemiesHolder();

            RealMovementEnemyHarness stoppedEnemy = CreateRealMovementEnemy(
                enemiesHolder,
                TEST_ORIGIN + new Vector3(1f, 0.02f, 1f),
                new FakeGridManager(chunk),
                timeFreeze);
            RealMovementEnemyHarness knockedEnemy = CreateRealMovementEnemy(
                enemiesHolder,
                TEST_ORIGIN + new Vector3(3f, 0.02f, 1f),
                new FakeGridManager(chunk),
                timeFreeze);
            RealMovementEnemyHarness offGroundEnemy = CreateRealMovementEnemy(
                enemiesHolder,
                TEST_ORIGIN + new Vector3(5f, 0.02f, 1f),
                new FakeGridManager(chunk),
                timeFreeze);

            timeFreeze.UnfrozenTime = 15f;
            Assert.IsTrue(stoppedEnemy.Movement.CanBeTeleported);
            Assert.IsTrue(knockedEnemy.Movement.CanBeTeleported);
            Assert.IsTrue(offGroundEnemy.Movement.CanBeTeleported);

            // Act
            stoppedEnemy.Movement.StopMovement();
            knockedEnemy.Movement.MoveToPositionInTimeIgnoringSpeed(TEST_ORIGIN + new Vector3(5f, 0.02f, 5f), 0.5f);
            UnityEngine.Object.DestroyImmediate(floor);
            Physics.SyncTransforms();

            // Assert
            Assert.IsFalse(stoppedEnemy.Movement.CanBeTeleported);
            Assert.IsFalse(knockedEnemy.Movement.CanBeTeleported);
            Assert.IsFalse(offGroundEnemy.Movement.CanBeTeleported);
        }

        [Test]
        public void TeleporterAndRealMovementController_PreventsThrashingDuringGraceThenRelocatesAndRearmsGrace()
        {
            // Arrange — 3x3 chunk [0..6, 0..6]; only cell (0,0) walkable; enemy placed outside at (12, 0.02, 12)
            Grid chunk = CreateChunkGrid(3, 3, 2f);
            MarkAllCellsUnwalkableExcept(chunk, 0, 0);
            Cell targetCell = chunk.Cells[0, 0];
            CreateFloorCollider(TEST_ORIGIN + new Vector3(6f, -0.5f, 6f), new Vector3(30f, 1f, 30f));

            FakeTimeFreezeState timeFreeze = new FakeTimeFreezeState { UnfrozenTime = 0f, IsFrozen = false };
            Transform enemiesHolder = CreateEnemiesHolder();
            Vector3 outsidePos = TEST_ORIGIN + new Vector3(12f, 0.02f, 12f);

            RealMovementEnemyHarness enemyHarness = CreateRealMovementEnemy(
                enemiesHolder,
                outsidePos,
                new FakeGridManager(chunk),
                timeFreeze);

            EnemiesOutsidePlayerChunkTeleporter teleporter = CreateTeleporter(
                new FakeGridManager(chunk),
                CreateAllHiddenCamera(),
                enemiesHolder);

            // Act & Assert — At 2.9s grace blocks relocation
            timeFreeze.UnfrozenTime = 2.9f;
            teleporter.TeleportEnemiesFromOutsideToInsidePlayerChunk();
            AssertVector3Equal(outsidePos, enemyHarness.Enemy.transform.position);

            // At 3.0s grace expires -> relocated into cell (0,0) and grace immediately re-arms
            timeFreeze.UnfrozenTime = 3.0f;
            teleporter.TeleportEnemiesFromOutsideToInsidePlayerChunk();
            Assert.AreEqual(targetCell.WorldPos.x, enemyHarness.Enemy.transform.position.x, TOLERANCE);
            Assert.AreEqual(TEST_ORIGIN.y + GridConstants.ROOT_GROUND_CLEARANCE, enemyHarness.Enemy.transform.position.y, TOLERANCE);
            Assert.AreEqual(targetCell.WorldPos.z, enemyHarness.Enemy.transform.position.z, TOLERANCE);
            Assert.IsFalse(enemyHarness.Movement.CanBeTeleported);
        }

        private EnemiesOutsidePlayerChunkTeleporter CreateTeleporter(
            IGridManager gridManager,
            Camera camera,
            Transform enemiesHolder)
        {
            GameObject go = new GameObject("TestTeleporter");
            go.SetActive(false);
            _createdObjects.Add(go);

            EnemiesOutsidePlayerChunkTeleporter teleporter = go.AddComponent<EnemiesOutsidePlayerChunkTeleporter>();
            SetPrivateField(typeof(EnemiesOutsidePlayerChunkTeleporter), teleporter, "_enemiesHolder", enemiesHolder);

            ContainerBuilder builder = new ContainerBuilder().SetName("TeleporterTestContainer");
            builder.AddSingleton(gridManager, typeof(IGridManager));
            builder.AddSingleton(camera);
            Container container = builder.Build();
            _createdContainers.Add(container);

            GameObjectInjector.InjectRecursive(go, container);
            go.SetActive(true);
            return teleporter;
        }

        private Transform CreateEnemiesHolder()
        {
            GameObject holder = new GameObject("TestEnemiesHolder");
            _createdObjects.Add(holder);
            return holder.transform;
        }

        private Enemy CreateFakeMovementEnemy(
            string name,
            Transform parent,
            Vector3 position,
            bool isAlive,
            bool canBeTeleported,
            bool active)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.SetActive(false);
            _createdObjects.Add(go);

            Health health = go.AddComponent<Health>();
            health.MaxHealth = 100f;
            go.AddComponent<FakeDeathSequence>();
            FakeMovementController movement = go.AddComponent<FakeMovementController>();
            movement.SetCanBeTeleported(canBeTeleported);

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.AddComponent<Animator>();
            visual.AddComponent<EnemyAnimator>();

            EnemyConfigSO config = ScriptableObject.CreateInstance<EnemyConfigSO>();
            _createdObjects.Add(config);
            config.MaxHealth = 100f;

            Enemy enemy = go.AddComponent<Enemy>();
            SetPrivateField(typeof(Enemy), enemy, "<Config>k__BackingField", config);
            SetPrivateField(typeof(Enemy), enemy, "_visual", visual);

            InvokeLifecycleMethod(enemy, "Awake");
            InvokeLifecycleMethod(health, "OnEnable");
            if (!isAlive)
            {
                health.DecreaseHealth(100f);
            }

            go.SetActive(active);
            return enemy;
        }

        private RealMovementEnemyHarness CreateRealMovementEnemy(
            Transform parent,
            Vector3 position,
            IGridManager gridManager,
            FakeTimeFreezeState timeFreeze)
        {
            GameObject go = new GameObject("RealMovementEnemy");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.SetActive(false);
            _createdObjects.Add(go);

            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.4f, 1f, 0.4f);

            Health health = go.AddComponent<Health>();
            health.MaxHealth = 100f;
            go.AddComponent<FakeDeathSequence>();
            FlowFieldMovementController flowFieldMovement = go.AddComponent<FlowFieldMovementController>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.AddComponent<Animator>();
            EnemyAnimator animator = visual.AddComponent<EnemyAnimator>();

            EnemyConfigSO config = ScriptableObject.CreateInstance<EnemyConfigSO>();
            _createdObjects.Add(config);
            config.MaxHealth = 100f;
            config.MovementSpeed = 4f;

            Enemy enemy = go.AddComponent<Enemy>();
            SetPrivateField(typeof(Enemy), enemy, "<Config>k__BackingField", config);
            SetPrivateField(typeof(Enemy), enemy, "_visual", visual);

            EnemyMovementController movement = go.AddComponent<EnemyMovementController>();

            ContainerBuilder builder = new ContainerBuilder().SetName("RealMovementEnemyContainer");
            builder.AddSingleton(gridManager, typeof(IGridManager));
            builder.AddSingleton(timeFreeze, typeof(ITimeFreezeState));
            builder.AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>));
            Container container = builder.Build();
            _createdContainers.Add(container);

            GameObjectInjector.InjectRecursive(go, container);
            go.SetActive(true);

            InvokeLifecycleMethod(animator, "Awake");
            InvokeLifecycleMethod(flowFieldMovement, "Awake");
            InvokeLifecycleMethod(enemy, "Awake");
            InvokeLifecycleMethod(movement, "Awake");

            InvokeLifecycleMethod(health, "OnEnable");
            InvokeLifecycleMethod(enemy, "OnEnable");
            InvokeLifecycleMethod(movement, "OnEnable");

            return new RealMovementEnemyHarness(enemy, movement);
        }

        private static Grid CreateDegenerateChunk(int kind)
        {
            if (kind == 0)
            {
                return null;
            }

            if (kind == 1)
            {
                return new Grid(new GridConfiguration(3, 3, 2f), null);
            }

            if (kind == 2)
            {
                return new Grid(new GridConfiguration(0, 0, 2f), new Cell[0, 0]);
            }

            Grid chunk = CreateChunkGrid(3, 3, 2f);
            chunk.Cells[0, 0] = null;
            return chunk;
        }

        private static Grid CreateChunkGrid(int width, int height, float cellSize)
        {
            Cell[,] cells = new Cell[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    Vector3 worldPos = TEST_ORIGIN + new Vector3((x + 0.5f) * cellSize, 0f, (y + 0.5f) * cellSize);
                    cells[x, y] = new Cell(worldPos, pos, pos);
                }
            }

            return new Grid(new GridConfiguration(width, height, cellSize), cells);
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

        private static void MarkAllCellsUnwalkableExcept(Grid grid, int allowedX, int allowedY)
        {
            MarkAllCellsUnwalkable(grid);
            grid.Cells[allowedX, allowedY].Cost = GridConstants.DEFAULT_FIELD_COST;
        }

        private GameObject CreateFloorCollider(Vector3 center, Vector3 size)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.GreaterOrEqual(groundLayer, 0);

            GameObject floor = new GameObject("TeleporterTestFloor");
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
            GameObject cameraGo = new GameObject("TeleporterAllHiddenCamera");
            cameraGo.transform.position = TEST_ORIGIN + new Vector3(0f, 50f, 0f);
            cameraGo.transform.rotation = Quaternion.LookRotation(Vector3.up);
            _createdObjects.Add(cameraGo);

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
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
        }

        private readonly struct RealMovementEnemyHarness
        {
            public readonly Enemy Enemy;
            public readonly EnemyMovementController Movement;

            public RealMovementEnemyHarness(Enemy enemy, EnemyMovementController movement)
            {
                Enemy = enemy;
                Movement = movement;
            }
        }

        private sealed class FakeDeathSequence : MonoBehaviour, INeedToCompleteBeforeDisable
        {
            public event EventHandler OnCompleted
            {
                add { }
                remove { }
            }
        }

        private sealed class FakeMovementController : MonoBehaviour, IMovementController
        {
            private bool _canBeTeleported;

            public int ResetCount { get; private set; }
            public bool CanBeTeleported { get { return _canBeTeleported; } }

            public void SetCanBeTeleported(bool value)
            {
                _canBeTeleported = value;
            }

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

            public FakeGridManager(Grid chunk)
            {
                GridPlayerChunk = chunk;
                WorldGrid = chunk;
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
            public bool IsFrozen { get; set; }
            public float UnfrozenTime { get; set; }

            public event Action<bool> OnFrozenChanged
            {
                add { }
                remove { }
            }
        }
    }
}
