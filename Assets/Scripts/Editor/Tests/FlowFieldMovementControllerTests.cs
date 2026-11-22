using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.FlowFieldSystem;
using Assets.Scripts.Navigation.GridSystem;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class FlowFieldMovementControllerTests
    {
        private const float TOLERANCE = 0.0001f;
        private const float ISOLATED_Y = 500f;

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
            Physics.SyncTransforms();
        }

        [Test]
        public void GetFlowDirection_And_CalculateDesiredMovementDirection_WhenGridOrDestinationMissingOrOutOfBounds_ReturnZero()
        {
            // Arrange
            FlowFieldMovementController nullGridController = CreateController(new FakeGridManager(null, null, null), new Vector3(1f, ISOLATED_Y, 1f));

            Grid grid = new Grid(new GridConfiguration(3, 3, 2f));
            FlowFieldMovementController nullDestController = CreateController(new FakeGridManager(grid, grid, null), new Vector3(1f, ISOLATED_Y, 1f));

            FakeGridManager validGridManager = new FakeGridManager(grid, grid, grid.Cells[2, 2]);
            FlowFieldMovementController outNegController = CreateController(validGridManager, new Vector3(-0.01f, ISOLATED_Y, 1f));
            FlowFieldMovementController outUpperController = CreateController(validGridManager, new Vector3(6f, ISOLATED_Y, 1f));

            // Act & Assert
            AssertVector3Equal(Vector3.zero, nullGridController.GetFlowDirection());
            AssertVector3Equal(Vector3.zero, nullGridController.CalculateDesiredMovementDirection());
            AssertVector3Equal(Vector3.zero, nullDestController.GetFlowDirection());
            AssertVector3Equal(Vector3.zero, outNegController.GetFlowDirection());
            AssertVector3Equal(Vector3.zero, outUpperController.GetFlowDirection());
        }

        [Test]
        public void GetFlowDirection_OnDestinationCell_RespectsArrivalRadiusSqrAndIgnoresYDistance()
        {
            // Arrange — Destination cell (0,0) center is at (1f, 0f, 1f); arrival radius is 0.35f (sqr = 0.1225).
            Grid grid = new Grid(new GridConfiguration(2, 2, 2f));
            FakeGridManager gridManager = new FakeGridManager(grid, grid, grid.Cells[0, 0]);
            FlowFieldMovementController controller = CreateController(gridManager, new Vector3(1.34f, ISOLATED_Y, 1f));

            // Act & Assert — Inside threshold (0.34 and 0.349, avoiding 1.35f where float subtraction rounds above 0.35) -> zero
            AssertVector3Equal(Vector3.zero, controller.GetFlowDirection());

            controller.transform.position = new Vector3(1.349f, ISOLATED_Y, 1f);
            AssertVector3Equal(Vector3.zero, controller.GetFlowDirection());

            // Act & Assert — Strictly beyond threshold (0.36) -> horizontal normalized vector toward (1, 0, 1)
            controller.transform.position = new Vector3(1.36f, ISOLATED_Y, 1f);
            AssertVector3Equal(Vector3.left, controller.GetFlowDirection());
        }

        [Test]
        public void GetFlowDirection_ValidCellWithBestDirection_ReturnsNormalizedHorizontalVector()
        {
            // Arrange
            Grid grid = new Grid(new GridConfiguration(3, 3, 2f));
            grid.Cells[0, 0].BestDirection = GridDirection.NorthEast;
            FakeGridManager gridManager = new FakeGridManager(grid, grid, grid.Cells[2, 2]);
            FlowFieldMovementController controller = CreateController(gridManager, new Vector3(1f, ISOLATED_Y, 1f));

            // Act
            Vector3 direction = controller.GetFlowDirection();

            // Assert — Normalized (1, 0, 1) = (0.70710678, 0, 0.70710678)
            float invSqrt2 = 1f / Mathf.Sqrt(2f);
            AssertVector3Equal(new Vector3(invSqrt2, 0f, invSqrt2), direction);
        }

        [Test]
        public void GetFlowDirection_WhenBestDirectionNoneOrOnImpassableCell_FallsBackToCornerSafeLowestBestCostNeighbor()
        {
            // Arrange — 3x3 grid, entity at (0,0) with BestDirection = None.
            // (1,0) is impassable (even though BestCost=0); (1,1) has BestCost=1 but diagonal is blocked by (1,0);
            // (0,1) is walkable with BestCost=3 -> must choose North toward (0,1).WorldPos (1, 0, 3).
            Grid grid = new Grid(new GridConfiguration(3, 3, 2f));
            grid.Cells[0, 0].BestDirection = GridDirection.None;
            grid.Cells[1, 0].Cost = FlowFieldConstants.IMPASSABLE_COST;
            grid.Cells[1, 0].BestCost = 0;
            grid.Cells[1, 1].BestCost = 1;
            grid.Cells[0, 1].BestCost = 3;
            FakeGridManager gridManager = new FakeGridManager(grid, grid, grid.Cells[2, 2]);
            FlowFieldMovementController controller = CreateController(gridManager, new Vector3(1f, ISOLATED_Y, 1f));

            // Act & Assert — Fallback with BestDirection.None
            AssertVector3Equal(Vector3.forward, controller.GetFlowDirection());

            // Act & Assert — Also falls back when current cell itself is impassable despite stale BestDirection
            grid.Cells[0, 0].Cost = FlowFieldConstants.IMPASSABLE_COST;
            grid.Cells[0, 0].BestDirection = GridDirection.East;
            AssertVector3Equal(Vector3.forward, controller.GetFlowDirection());
        }

        [Test]
        public void GetFlowDirection_OutsideIntegratedChunkWithNoBestCosts_SelectsTraversableNeighborNearestDestination()
        {
            // Arrange — 4x4 grid, all BestCost = ushort.MaxValue, BestDirection = None.
            // Entity at (0,0) center (1, 500, 1), DestinationCell = (3, 1) center (7, 0, 3).
            // (1,1) center (3, 0, 3) is closer to (7, 0, 3) [dist^2=16] than (1,0) [dist^2=20] or (0,1) [dist^2=36].
            Grid grid = new Grid(new GridConfiguration(4, 4, 2f));
            FakeGridManager gridManager = new FakeGridManager(grid, grid, grid.Cells[3, 1]);
            FlowFieldMovementController controller = CreateController(gridManager, new Vector3(1f, ISOLATED_Y, 1f));

            // Act
            Vector3 direction = controller.GetFlowDirection();

            // Assert — Direction from (1, *, 1) toward (3, 0, 3) on XZ is normalized (2, 0, 2)
            float invSqrt2 = 1f / Mathf.Sqrt(2f);
            AssertVector3Equal(new Vector3(invSqrt2, 0f, invSqrt2), direction);
        }

        [Test]
        public void GetFlowDirection_SurroundedByImpassableCells_ReturnsZero()
        {
            // Arrange — 3x3 grid, entity at (1,1), all 8 neighbors impassable
            Grid grid = new Grid(new GridConfiguration(3, 3, 2f));
            MarkSurroundingCellsImpassable(grid, centerX: 1, centerY: 1);
            FakeGridManager gridManager = new FakeGridManager(grid, grid, grid.Cells[2, 2]);
            FlowFieldMovementController controller = CreateController(gridManager, new Vector3(3f, ISOLATED_Y, 3f));

            // Act
            Vector3 direction = controller.GetFlowDirection();

            // Assert
            AssertVector3Equal(Vector3.zero, direction);
        }

        [Test]
        public void MoveOnFlowFieldGrid_MovesPositionBySpeedTimesFixedDeltaTimeAndReturnsDisplacement()
        {
            // Arrange
            Grid grid = new Grid(new GridConfiguration(3, 3, 2f));
            grid.Cells[0, 0].BestDirection = GridDirection.East;
            FakeGridManager gridManager = new FakeGridManager(grid, grid, grid.Cells[2, 2]);
            Vector3 startPos = new Vector3(1f, ISOLATED_Y, 1f);
            FlowFieldMovementController controller = CreateController(gridManager, startPos);
            float expectedDeltaX = 10f * Time.fixedDeltaTime;

            // Act
            Vector3 displacement = controller.MoveOnFlowFieldGrid(10f);

            // Assert
            AssertVector3Equal(new Vector3(expectedDeltaX, 0f, 0f), displacement);
            AssertVector3Equal(startPos + new Vector3(expectedDeltaX, 0f, 0f), controller.transform.position);
        }

        [Test]
        public void StepSeparation_AppliesInverseDistanceWeightingAcrossEnemyAndBossExcludesSelfDistantAndNonEnemy()
        {
            // Arrange — Mover at (3, 500, 3) on destination cell (so gridDir == Vector3.zero).
            // Self collider at mover position (excluded), Ground collider nearby (ignored), distant Enemy at +1.3 (beyond radius 1.2, ignored),
            // Enemy neighbor at (+0.5, 0, 0) [dist 0.5 => weight 2 along -X], Boss neighbor at (0, 0, -1.0) [dist 1.0 => weight 1 along +Z].
            // Combined normalized separation * strength(0.5) * dampen(0.1) = (-2/sqrt(5), 0, 1/sqrt(5)) * 0.05.
            Grid grid = new Grid(new GridConfiguration(3, 3, 2f));
            FakeGridManager gridManager = new FakeGridManager(grid, grid, grid.Cells[1, 1]);
            Vector3 moverPos = new Vector3(3f, ISOLATED_Y, 3f);
            FlowFieldMovementController controller = CreateController(gridManager, moverPos, addSelfEnemyCollider: true);

            CreateCollider("NonEnemyGround", moverPos + new Vector3(-0.3f, 0f, 0f), "Ground", radius: 0.2f);
            CreateCollider("TooFarEnemy", moverPos + new Vector3(0f, 0f, 1.35f), "Enemy", radius: 0.1f);
            CreateCollider("CloseEnemy", moverPos + new Vector3(0.5f, 0f, 0f), "Enemy", radius: 0.1f);
            CreateCollider("FartherBoss", moverPos + new Vector3(0f, 0f, -1.0f), "Boss", radius: 0.1f);

            // Act
            controller.StepSeparation();
            Vector3 desired = controller.CalculateDesiredMovementDirection();

            // Assert
            float invSqrt5 = 1f / Mathf.Sqrt(5f);
            Vector3 expected = new Vector3(-2f * invSqrt5, 0f, 1f * invSqrt5) * (0.5f * 0.1f);
            AssertVector3Equal(expected, desired);
        }

        [Test]
        public void StepSeparation_EnemyAtExactSameXZPosition_AvoidsNaNAndProducesZeroSeparation()
        {
            // Arrange
            Grid grid = new Grid(new GridConfiguration(3, 3, 2f));
            FakeGridManager gridManager = new FakeGridManager(grid, grid, grid.Cells[1, 1]);
            Vector3 moverPos = new Vector3(3f, ISOLATED_Y, 3f);
            FlowFieldMovementController controller = CreateController(gridManager, moverPos, addSelfEnemyCollider: true);
            CreateCollider("CoincidentEnemy", moverPos + new Vector3(0f, 0.2f, 0f), "Enemy", radius: 0.1f);

            // Act
            controller.StepSeparation();
            Vector3 desired = controller.CalculateDesiredMovementDirection();

            // Assert
            Assert.IsFalse(float.IsNaN(desired.x));
            Assert.IsFalse(float.IsNaN(desired.y));
            Assert.IsFalse(float.IsNaN(desired.z));
            AssertVector3Equal(Vector3.zero, desired);
        }

        [Test]
        public void CalculateDesiredMovementDirection_BlendsFlowWithSeparationAndResetAfterRelocationClearsSeparation()
        {
            // Arrange — Cell (0,0) flows North (0,0,1); Enemy neighbor at +0.5X pushes (-0.5, 0, 0).
            // Blended = (0,0,1) + (-0.5,0,0) = (-0.5, 0, 1) normalized = (-1/sqrt(5), 0, 2/sqrt(5)).
            Grid grid = new Grid(new GridConfiguration(3, 3, 2f));
            grid.Cells[0, 0].BestDirection = GridDirection.North;
            FakeGridManager gridManager = new FakeGridManager(grid, grid, grid.Cells[2, 2]);
            Vector3 moverPos = new Vector3(1f, ISOLATED_Y, 1f);
            FlowFieldMovementController controller = CreateController(gridManager, moverPos, addSelfEnemyCollider: true);
            CreateCollider("AdjacentEnemy", moverPos + new Vector3(0.5f, 0f, 0f), "Enemy", radius: 0.1f);

            // Act 1 — Separation blended with flow
            controller.StepSeparation();
            Vector3 blended = controller.CalculateDesiredMovementDirection();

            float invSqrt5 = 1f / Mathf.Sqrt(5f);
            AssertVector3Equal(new Vector3(-1f * invSqrt5, 0f, 2f * invSqrt5), blended);

            // Act 2 — ResetAfterRelocation zeroes separation immediately
            controller.ResetAfterRelocation();
            Vector3 afterReset = controller.CalculateDesiredMovementDirection();

            // Assert
            AssertVector3Equal(Vector3.forward, afterReset);
        }

        [Test]
        public void IsValidVoluntaryStep_EnforcesWorldBoundsMaxTraversalStepsImpassableAndDiagonalCorners()
        {
            // Arrange — 20x4 grid with cellSize 1 => [0, 20) x [0, 4).
            // Max distance for MAX_LOCAL_TRAVERSAL_STEPS (64) at 0.25 * cellSize is 16.0f.
            Grid grid = new Grid(new GridConfiguration(20, 4, 1f));
            grid.Cells[1, 0].Cost = FlowFieldConstants.IMPASSABLE_COST;
            FlowFieldMovementController controller = CreateController(
                new FakeGridManager(grid, grid, grid.Cells[19, 3]),
                new Vector3(0.5f, ISOLATED_Y, 2.5f));
            FlowFieldMovementController nullGridController = CreateController(
                new FakeGridManager(null, null, null),
                new Vector3(0.5f, ISOLATED_Y, 2.5f));

            // Act & Assert — Null grid and world bounds
            Assert.IsFalse(nullGridController.IsValidVoluntaryStep(new Vector3(0.5f, 0f, 2.5f), new Vector3(1.5f, 0f, 2.5f)));
            Assert.IsFalse(controller.IsValidVoluntaryStep(new Vector3(-0.1f, 0f, 2.5f), new Vector3(0.5f, 0f, 2.5f)));
            Assert.IsFalse(controller.IsValidVoluntaryStep(new Vector3(0.5f, 0f, 2.5f), new Vector3(20f, 0f, 2.5f)));

            // Act & Assert — Max traversal step count boundary: 16.0f (64 steps) passes, 16.01f (65 steps) fails
            Assert.IsTrue(controller.IsValidVoluntaryStep(new Vector3(0.5f, 0f, 2.5f), new Vector3(16.5f, 0f, 2.5f)));
            Assert.IsFalse(controller.IsValidVoluntaryStep(new Vector3(0.5f, 0f, 2.5f), new Vector3(16.51f, 0f, 2.5f)));

            // Act & Assert — Entering impassable cell or cutting impassable corner fails; valid multi-cell diagonal passes
            Assert.IsFalse(controller.IsValidVoluntaryStep(new Vector3(0.5f, 0f, 0.5f), new Vector3(1.5f, 0f, 0.5f)));
            Assert.IsFalse(controller.IsValidVoluntaryStep(new Vector3(0.5f, 0f, 0.5f), new Vector3(1.5f, 0f, 1.5f)));
            Assert.IsTrue(controller.IsValidVoluntaryStep(new Vector3(2.5f, 0f, 0.5f), new Vector3(4.5f, 0f, 2.5f)));
        }

        private FlowFieldMovementController CreateController(
            IGridManager gridManager,
            Vector3 position,
            bool addSelfEnemyCollider = false)
        {
            GameObject go = new GameObject("TestFlowFieldMover");
            go.SetActive(false);
            go.transform.position = position;
            _createdObjects.Add(go);

            if (addSelfEnemyCollider)
            {
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                Assert.GreaterOrEqual(enemyLayer, 0);
                go.layer = enemyLayer;
                SphereCollider selfCollider = go.AddComponent<SphereCollider>();
                selfCollider.radius = 0.25f;
            }

            FlowFieldMovementController controller = go.AddComponent<FlowFieldMovementController>();
            Container container = BuildContainer(gridManager);
            GameObjectInjector.InjectRecursive(go, container);
            go.SetActive(true);
            InvokeLifecycleMethod(controller, "Awake");
            Physics.SyncTransforms();
            return controller;
        }

        private GameObject CreateCollider(string name, Vector3 position, string layerName, float radius)
        {
            int layer = LayerMask.NameToLayer(layerName);
            Assert.GreaterOrEqual(layer, 0, $"Missing layer '{layerName}'");

            GameObject go = new GameObject(name);
            go.layer = layer;
            go.transform.position = position;
            SphereCollider collider = go.AddComponent<SphereCollider>();
            collider.radius = radius;
            _createdObjects.Add(go);
            Physics.SyncTransforms();
            return go;
        }

        private Container BuildContainer(IGridManager gridManager)
        {
            ContainerBuilder builder = new ContainerBuilder().SetName("FlowFieldMoverTestContainer");
            builder.AddSingleton(gridManager, typeof(IGridManager));
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private static void MarkSurroundingCellsImpassable(Grid grid, int centerX, int centerY)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    if (x != centerX || y != centerY)
                    {
                        grid.Cells[x, y].Cost = FlowFieldConstants.IMPASSABLE_COST;
                    }
                }
            }
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

        private static void AssertVector3Equal(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, TOLERANCE, $"X mismatch: expected {expected}, got {actual}");
            Assert.AreEqual(expected.y, actual.y, TOLERANCE, $"Y mismatch: expected {expected}, got {actual}");
            Assert.AreEqual(expected.z, actual.z, TOLERANCE, $"Z mismatch: expected {expected}, got {actual}");
        }

        private sealed class FakeGridManager : IGridManager
        {
            public FakeGridManager(Grid worldGrid, Grid playerChunk, Cell destinationCell)
            {
                WorldGrid = worldGrid;
                GridPlayerChunk = playerChunk;
                DestinationCell = destinationCell;
            }

            public Cell DestinationCell { get; private set; }
            public Grid GridPlayerChunk { get; private set; }
            public Grid WorldGrid { get; private set; }
        }
    }
}
