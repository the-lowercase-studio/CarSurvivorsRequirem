using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Audio;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.FlowFieldSystem;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.Skills;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.TestTools;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class GridManagerTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();
        private readonly List<GridManager> _createdGridManagers = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdGridManagers.Count; i++)
            {
                if (_createdGridManagers[i] != null)
                {
                    InvokeLifecycleMethod(_createdGridManagers[i], "OnDisable");
                }
            }

            _createdGridManagers.Clear();

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
        }

        [Test]
        public void Awake_WithoutDebugConfig_AllocatesGridsAndResolvesViaIGridManagerBinding()
        {
            // Arrange & Act
            GridManagerHarness harness = CreateHarness(
                worldWidth: 6,
                worldHeight: 4,
                worldCellSize: 2f,
                chunkWidth: 3,
                chunkHeight: 2,
                chunkCellSize: 2f,
                debugConfig: null,
                callOnEnable: false);

            // Assert
            Assert.AreSame(harness.GridManager, harness.Container.Resolve<IGridManager>());
            Assert.IsNotNull(harness.GridManager.WorldGrid);
            Assert.AreEqual(6, harness.GridManager.WorldGrid.Width);
            Assert.AreEqual(4, harness.GridManager.WorldGrid.Height);
            Assert.AreEqual(2f, harness.GridManager.WorldGrid.CellSize, 0.0001f);
            Assert.IsNotNull(harness.GridManager.GridPlayerChunk);
            Assert.AreEqual(3, harness.GridManager.GridPlayerChunk.Width);
            Assert.AreEqual(2, harness.GridManager.GridPlayerChunk.Height);
        }

        [Test]
        public void Awake_WithDebugConfig_AssignsWorldGridReference()
        {
            // Arrange
            FlowFieldDebugConfiguration debugConfig = new FlowFieldDebugConfiguration(null, null);

            // Act
            GridManagerHarness harness = CreateHarness(
                worldWidth: 4,
                worldHeight: 4,
                worldCellSize: 1f,
                chunkWidth: 2,
                chunkHeight: 2,
                chunkCellSize: 1f,
                debugConfig: debugConfig,
                callOnEnable: false);

            // Assert
            Assert.AreSame(harness.GridManager.WorldGrid, debugConfig.Grid);
        }

        [Test]
        public void OnEnable_ValidWorldGrid_ComputesInitialWorldFlowFieldTargetingCenterCell()
        {
            // Arrange — 5x5 world grid with cellSize 2 covers [0..10] x [0..10]
            CreateBoxCollider("WorldGround", new Vector3(5f, -0.5f, 5f), new Vector3(14f, 1f, 14f), "Ground");
            GridManagerHarness harness = CreateHarness(
                worldWidth: 5,
                worldHeight: 5,
                worldCellSize: 2f,
                chunkWidth: 3,
                chunkHeight: 3,
                chunkCellSize: 2f,
                callOnEnable: false);

            // Act
            InvokeLifecycleMethod(harness.GridManager, "OnEnable");

            // Assert
            Grid world = harness.GridManager.WorldGrid;
            Assert.AreSame(world.Cells[2, 2], harness.GridManager.DestinationCell);
            Assert.AreEqual((ushort)0, world.Cells[2, 2].BestCost);
            Assert.AreEqual((ushort)4, world.Cells[0, 0].BestCost);
            Assert.AreSame(GridDirection.NorthEast, world.Cells[0, 0].BestDirection);
        }

        [Test]
        public void OnEnable_ZeroDimensionWorldGrid_LogsWarningWithoutThrowing()
        {
            // Arrange
            GridManagerHarness harness = CreateHarness(
                worldWidth: 0,
                worldHeight: 0,
                worldCellSize: 1f,
                chunkWidth: 0,
                chunkHeight: 0,
                chunkCellSize: 1f,
                callOnEnable: false);

            LogAssert.Expect(LogType.Warning, "[GridManager] WorldGrid configuration is uninitialized or invalid.");

            // Act
            Assert.DoesNotThrow(delegate { InvokeLifecycleMethod(harness.GridManager, "OnEnable"); });

            // Assert
            Assert.IsNull(harness.GridManager.DestinationCell);
        }

        [Test]
        public void StepPlayerChunkFlowField_CentersChunkOnPlayerAndSharesCellReferencesWithWorldGrid()
        {
            // Arrange — 7x7 world grid, 3x3 chunk; player at WorldGrid[3, 4] center => chunk covers [2..4, 3..5]
            CreateBoxCollider("WorldGround", new Vector3(7f, -0.5f, 7f), new Vector3(18f, 1f, 18f), "Ground");
            GridManagerHarness harness = CreateHarness(
                worldWidth: 7,
                worldHeight: 7,
                worldCellSize: 2f,
                chunkWidth: 3,
                chunkHeight: 3,
                chunkCellSize: 2f,
                callOnEnable: false);

            harness.PlayerObject.transform.position = harness.GridManager.WorldGrid.Cells[3, 4].WorldPos;

            // Act
            harness.GridManager.StepPlayerChunkFlowField();

            // Assert
            Grid world = harness.GridManager.WorldGrid;
            Grid chunk = harness.GridManager.GridPlayerChunk;
            AssertChunkMatchesWorldSlice(world, chunk, startX: 2, startY: 3);
        }

        [TestCase(-5f, -5f, 0, 0, TestName = "StepPlayerChunkFlowField_SouthWestOutOfBounds_ClampsChunkToZeroZero")]
        [TestCase(50f, -5f, 3, 0, TestName = "StepPlayerChunkFlowField_SouthEastOutOfBounds_ClampsChunkToEastBorder")]
        [TestCase(-5f, 50f, 0, 3, TestName = "StepPlayerChunkFlowField_NorthWestOutOfBounds_ClampsChunkToNorthBorder")]
        [TestCase(50f, 50f, 3, 3, TestName = "StepPlayerChunkFlowField_NorthEastOutOfBounds_ClampsChunkToNorthEastCorner")]
        public void StepPlayerChunkFlowField_PlayerNearWorldBorders_ClampsChunkWithinWorldBounds(
            float playerX,
            float playerZ,
            int expectedStartX,
            int expectedStartY)
        {
            // Arrange — 6x6 world grid with 3x3 chunk (valid minGrid range is [0..3, 0..3])
            GridManagerHarness harness = CreateHarness(
                worldWidth: 6,
                worldHeight: 6,
                worldCellSize: 2f,
                chunkWidth: 3,
                chunkHeight: 3,
                chunkCellSize: 2f,
                callOnEnable: false);
            harness.PlayerObject.transform.position = new Vector3(playerX, 0f, playerZ);

            // Act
            harness.GridManager.StepPlayerChunkFlowField();

            // Assert
            AssertChunkMatchesWorldSlice(harness.GridManager.WorldGrid, harness.GridManager.GridPlayerChunk, expectedStartX, expectedStartY);
        }

        [Test]
        public void StepPlayerChunkFlowField_ChunkLargerThanWorldGrid_ClampsToOriginAndNullsOutOfRangeSlots()
        {
            // Arrange — 2x2 world grid with 4x3 chunk
            GridManagerHarness harness = CreateHarness(
                worldWidth: 2,
                worldHeight: 2,
                worldCellSize: 2f,
                chunkWidth: 4,
                chunkHeight: 3,
                chunkCellSize: 2f,
                callOnEnable: false);
            harness.PlayerObject.transform.position = new Vector3(3f, 0f, 3f);

            // Act
            harness.GridManager.StepPlayerChunkFlowField();

            // Assert
            Grid world = harness.GridManager.WorldGrid;
            Grid chunk = harness.GridManager.GridPlayerChunk;
            Assert.AreSame(world.Cells[0, 0], chunk.Cells[0, 0]);
            Assert.AreSame(world.Cells[1, 1], chunk.Cells[1, 1]);
            Assert.IsNull(chunk.Cells[2, 0]);
            Assert.IsNull(chunk.Cells[0, 2]);
            Assert.IsNull(chunk.Cells[3, 2]);
        }

        [Test]
        public void StepPlayerChunkFlowField_ChunkShift_ClearsDepartingCellsUpdatesRetainedAndResetsBestCost()
        {
            // Arrange — 6x6 world grid with 2x2 chunk
            CreateBoxCollider("WorldGround", new Vector3(6f, -0.5f, 6f), new Vector3(16f, 1f, 16f), "Ground");
            GridManagerHarness harness = CreateHarness(
                worldWidth: 6,
                worldHeight: 6,
                worldCellSize: 2f,
                chunkWidth: 2,
                chunkHeight: 2,
                chunkCellSize: 2f,
                callOnEnable: false);

            // Step 1: Player at (0,0) center (1, 0, 1); halfWidth=1 => minGrid=(0,0) -> chunk covers [0..1, 0..1]
            harness.PlayerObject.transform.position = harness.GridManager.WorldGrid.Cells[0, 0].WorldPos;
            harness.GridManager.StepPlayerChunkFlowField();

            Cell departedCell = harness.GridManager.WorldGrid.Cells[0, 0];
            Cell retainedCell = harness.GridManager.WorldGrid.Cells[1, 0];
            Assert.AreEqual(new Vector2Int(0, 0), departedCell.ChunkGridPos);
            Assert.AreEqual((ushort)0, departedCell.BestCost);
            Assert.AreEqual(new Vector2Int(1, 0), retainedCell.ChunkGridPos);

            // Step 2: Move player to (2,0) center (5, 0, 1); halfWidth=1 => minGrid=(1,0) -> chunk covers [1..2, 0..1]
            // (0,0) departs while (1,0) shifts from chunk index (1,0) to (0,0).
            harness.PlayerObject.transform.position = harness.GridManager.WorldGrid.Cells[2, 0].WorldPos;
            harness.GridManager.StepPlayerChunkFlowField();

            // Assert
            Assert.AreEqual(GridConstants.INVALID_CHUNK_GRID_POS, departedCell.ChunkGridPos);
            Assert.AreSame(GridDirection.None, departedCell.BestDirection);
            Assert.AreEqual(ushort.MaxValue, departedCell.BestCost);

            Assert.AreSame(retainedCell, harness.GridManager.GridPlayerChunk.Cells[0, 0]);
            Assert.AreEqual(new Vector2Int(0, 0), retainedCell.ChunkGridPos);
        }

        [Test]
        public void StepPlayerChunkFlowField_VelocityPrediction_AppliesAbovePointOneAndClampsToMaxOffset()
        {
            // Arrange — 10x10 world and chunk with cellSize 2; predictionTime = 0.5, maxOffset = 4.
            // Player at (5, 0, 5) [cell (2,2)].
            CreateBoxCollider("WorldGround", new Vector3(10f, -0.5f, 10f), new Vector3(24f, 1f, 24f), "Ground");
            GridManagerHarness harness = CreateHarness(
                worldWidth: 10,
                worldHeight: 10,
                worldCellSize: 2f,
                chunkWidth: 10,
                chunkHeight: 10,
                chunkCellSize: 2f,
                predictionTime: 0.5f,
                maxOffset: 4f,
                callOnEnable: false);

            harness.PlayerObject.transform.position = new Vector3(5f, 0f, 5f);

            // Case 1: Speed == 0.1f (at threshold, not > 0.1) -> zero offset -> cell (2,2)
            harness.CarController.Velocity = new Vector3(0.1f, 0f, 0f);
            harness.GridManager.StepPlayerChunkFlowField();
            Assert.AreSame(harness.GridManager.WorldGrid.Cells[2, 2], harness.GridManager.DestinationCell);

            // Case 2: Speed = 6f -> offset = 6 * 0.5 = 3f -> target (8, 0, 5) -> cell (4,2)
            harness.CarController.Velocity = new Vector3(6f, 0f, 0f);
            harness.GridManager.StepPlayerChunkFlowField();
            Assert.AreSame(harness.GridManager.WorldGrid.Cells[4, 2], harness.GridManager.DestinationCell);

            // Case 3: Speed = 100f -> clamped to maxOffset 4f -> target (9, 0, 5) -> cell (4,2), not (9,2)
            harness.CarController.Velocity = new Vector3(100f, 0f, 0f);
            harness.GridManager.StepPlayerChunkFlowField();
            Assert.AreSame(harness.GridManager.WorldGrid.Cells[4, 2], harness.GridManager.DestinationCell);
        }

        [Test]
        public void StepPlayerChunkFlowField_PredictedDestinationOutsideBoundsOutsideChunkOrImpassable_FallsBackToNearestWalkableChunkCell()
        {
            // Arrange — 8x8 world grid (cellSize 2 => [0..16] x [0..16]), 3x3 chunk, predictionTime = 1, maxOffset = 4.
            CreateBoxCollider("WorldGround", new Vector3(8f, -0.5f, 8f), new Vector3(20f, 1f, 20f), "Ground");
            GridManagerHarness harness = CreateHarness(
                worldWidth: 8,
                worldHeight: 8,
                worldCellSize: 2f,
                chunkWidth: 3,
                chunkHeight: 3,
                chunkCellSize: 2f,
                predictionTime: 1f,
                maxOffset: 4f,
                callOnEnable: false);

            // Sub-case A: Predicted position inside world bounds (7, 0, 3) = world (3,1) is OUTSIDE chunk [0..2, 0..2]
            harness.PlayerObject.transform.position = new Vector3(3f, 0f, 3f);
            harness.CarController.Velocity = new Vector3(4f, 0f, 0f);
            harness.GridManager.StepPlayerChunkFlowField();
            Assert.AreSame(harness.GridManager.WorldGrid.Cells[2, 1], harness.GridManager.DestinationCell);

            // Sub-case B: Predicted position (19, 0, 5) is OUTSIDE world bounds -> falls back to nearest chunk cell (7, 2)
            harness.PlayerObject.transform.position = new Vector3(15f, 0f, 5f);
            harness.CarController.Velocity = new Vector3(4f, 0f, 0f);
            harness.GridManager.StepPlayerChunkFlowField();
            Assert.AreSame(harness.GridManager.WorldGrid.Cells[7, 2], harness.GridManager.DestinationCell);

            // Sub-case C: Predicted cell (1,1) inside chunk [0..2, 0..2] is covered by Impassable box ->
            // falls back to nearest walkable chunk cell (2,1) (or (0,1) tied; column-major loop picks (0,1) at distance 2)
            CreateBoxCollider("BlockCell11", harness.GridManager.WorldGrid.Cells[1, 1].WorldPos + new Vector3(0f, 0.5f, 0f), new Vector3(1.5f, 1f, 1.5f), "Impassable");
            harness.PlayerObject.transform.position = harness.GridManager.WorldGrid.Cells[1, 1].WorldPos;
            harness.CarController.Velocity = Vector3.zero;
            harness.GridManager.StepPlayerChunkFlowField();
            Assert.AreSame(harness.GridManager.WorldGrid.Cells[0, 1], harness.GridManager.DestinationCell);
        }

        [Test]
        public void StepPlayerChunkFlowField_AllChunkCellsImpassable_SetsDestinationCellNullAndLeavesDirectionsEmpty()
        {
            // Arrange — Impassable collider covers entire 3x3 grid even if any floor exists beneath
            CreateBoxCollider("FullImpassableBlock", new Vector3(3f, 0.5f, 3f), new Vector3(10f, 2f, 10f), "Impassable");
            GridManagerHarness harness = CreateHarness(
                worldWidth: 3,
                worldHeight: 3,
                worldCellSize: 2f,
                chunkWidth: 3,
                chunkHeight: 3,
                chunkCellSize: 2f,
                callOnEnable: false);
            harness.PlayerObject.transform.position = new Vector3(3f, 0f, 3f);

            // Act
            harness.GridManager.StepPlayerChunkFlowField();

            // Assert
            Assert.IsNull(harness.GridManager.DestinationCell);
            AssertAllCellsBlockedAndEmpty(harness.GridManager.GridPlayerChunk);
        }

        private GridManagerHarness CreateHarness(
            int worldWidth,
            int worldHeight,
            float worldCellSize,
            int chunkWidth,
            int chunkHeight,
            float chunkCellSize,
            float predictionTime = 0.25f,
            float maxOffset = 6f,
            FlowFieldDebugConfiguration debugConfig = null,
            bool callOnEnable = true)
        {
            GameObject playerGo = new GameObject("TestPlayerCar");
            _createdObjects.Add(playerGo);
            FakeCarController carController = new FakeCarController();
            FakePlayerManager playerManager = new FakePlayerManager(playerGo, carController);

            GameObject managerGo = new GameObject("TestGridManager");
            managerGo.SetActive(false);
            _createdObjects.Add(managerGo);

            GridManager gridManager = managerGo.AddComponent<GridManager>();
            _createdGridManagers.Add(gridManager);

            SetPrivateField(typeof(GridManager), gridManager, "_worldGridConfiguration", new GridConfiguration(worldWidth, worldHeight, worldCellSize));
            SetPrivateField(typeof(GridManager), gridManager, "_playerGridConfiguration", new GridConfiguration(chunkWidth, chunkHeight, chunkCellSize));
            SetPrivateField(typeof(GridManager), gridManager, "_flowFieldTargetPredictionTime", predictionTime);
            SetPrivateField(typeof(GridManager), gridManager, "_maxFlowFieldTargetOffset", maxOffset);
            SetPrivateField(typeof(GridManager), gridManager, "_flowFieldDebugConfiguration", debugConfig);

            ContainerBuilder builder = new ContainerBuilder().SetName("GridManagerTestContainer");
            builder.AddSingleton(playerManager, typeof(IPlayerManager));
            builder.AddSingleton(gridManager, typeof(IGridManager));
            Container container = builder.Build();
            _createdContainers.Add(container);

            GameObjectInjector.InjectRecursive(managerGo, container);
            InvokeLifecycleMethod(gridManager, "Awake");
            if (callOnEnable)
            {
                InvokeLifecycleMethod(gridManager, "OnEnable");
            }

            return new GridManagerHarness(gridManager, playerGo, carController, container);
        }

        private GameObject CreateBoxCollider(string name, Vector3 center, Vector3 size, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            Assert.GreaterOrEqual(layer, 0, $"Missing layer '{layerName}'");

            GameObject go = new GameObject(name);
            go.layer = layer;
            go.transform.position = center;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
            _createdObjects.Add(go);
            Physics.SyncTransforms();
            return go;
        }

        private static void AssertChunkMatchesWorldSlice(Grid world, Grid chunk, int startX, int startY)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                for (int y = 0; y < chunk.Height; y++)
                {
                    Cell expected = world.Cells[startX + x, startY + y];
                    Cell actual = chunk.Cells[x, y];
                    Assert.AreSame(expected, actual);
                    Assert.AreEqual(new Vector2Int(x, y), actual.ChunkGridPos);
                }
            }
        }

        private static void AssertAllCellsBlockedAndEmpty(Grid grid)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Cell cell = grid.Cells[x, y];
                    Assert.AreEqual((byte)FlowFieldConstants.IMPASSABLE_COST, cell.Cost);
                    Assert.AreEqual(ushort.MaxValue, cell.BestCost);
                    Assert.AreSame(GridDirection.None, cell.BestDirection);
                }
            }
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        private static void InvokeLifecycleMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method != null)
            {
                Action callback = (Action)Delegate.CreateDelegate(typeof(Action), target, method);
                callback();
            }
        }

        private readonly struct GridManagerHarness
        {
            public readonly GridManager GridManager;
            public readonly GameObject PlayerObject;
            public readonly FakeCarController CarController;
            public readonly Container Container;

            public GridManagerHarness(
                GridManager gridManager,
                GameObject playerObject,
                FakeCarController carController,
                Container container)
            {
                GridManager = gridManager;
                PlayerObject = playerObject;
                CarController = carController;
                Container = container;
            }
        }

        private sealed class FakeCarController : ICarController
        {
            public Vector3 Velocity { get; set; }

            public float MaxForwardSpeed { get { return 16f; } }
            public float MaxOverallSpeed { get { return 24f; } }
            public bool IsDrifting { get { return false; } }
            public int DriftDirection { get { return 0; } }
            public float DriftYawAngle { get { return 0f; } }
            public bool IsGrounded { get { return true; } }
            public float CurrentSteerInput { get { return 0f; } }

            public event EventHandler OnBrakePress
            {
                add { }
                remove { }
            }

            public event EventHandler OnBrakeRelease
            {
                add { }
                remove { }
            }

            public event EventHandler OnDriftStop
            {
                add { }
                remove { }
            }

            public float GetMovementSpeed()
            {
                return Velocity.magnitude;
            }

            public Vector3 GetMovementVelocity()
            {
                return Velocity;
            }

            public bool TryGetTireGroundContact(Vector3 contactPosition, out RaycastHit groundHit)
            {
                groundHit = default;
                return false;
            }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            public FakePlayerManager(GameObject gameObject, ICarController carController)
            {
                GameObject = gameObject;
                CarController = carController;
            }

            public IHealth Health { get { return null; } }
            public GameObject GameObject { get; private set; }
            public IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public ICarController CarController { get; private set; }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get { return null; } }
            public ISkillsRegistry SkillsRegistry { get { return null; } }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }
        }
    }
}
