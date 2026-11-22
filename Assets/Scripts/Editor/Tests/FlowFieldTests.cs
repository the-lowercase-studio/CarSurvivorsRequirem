using System.Collections.Generic;
using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.FlowFieldSystem;
using Assets.Scripts.Navigation.GridSystem;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class FlowFieldTests
    {
        private static readonly Vector3 TEST_ORIGIN = new Vector3(38000f, 500f, 38000f);

        private readonly List<UnityEngine.Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
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
        public void CreateCostField_NullGridOrNullCells_DoesNotThrow()
        {
            // Arrange
            FlowField flowField = new FlowField();
            Grid nullCellsGrid = new Grid(new GridConfiguration(2, 2, 1f), existingCells: null);

            // Act & Assert
            Assert.DoesNotThrow(delegate { flowField.CreateCostField(null); });
            Assert.DoesNotThrow(delegate { flowField.CreateCostField(nullCellsGrid); });
            Assert.IsNull(nullCellsGrid.Cells);
        }

        [Test]
        public void CreateCostField_ResetsPriorCostsAndSkipsNullCells()
        {
            // Arrange
            FlowField flowField = new FlowField();
            Grid grid = CreateOffsetGrid(2, 1, 2f);
            Cell cell00 = grid.Cells[0, 0];
            cell00.IncreaseCost(120);
            cell00.BestCost = 42;
            cell00.BestDirection = GridDirection.North;
            grid.Cells[1, 0] = null;

            CreateBoxCollider("Floor", cell00.WorldPos + new Vector3(0f, -0.5f, 0f), new Vector3(3f, 1f, 3f), "Ground");

            // Act
            flowField.CreateCostField(grid);

            // Assert
            Assert.AreEqual((byte)FlowFieldConstants.DEFAULT_FIELD_COST, cell00.Cost);
            Assert.AreEqual(ushort.MaxValue, cell00.BestCost);
            Assert.AreSame(GridDirection.None, cell00.BestDirection);
            Assert.IsNull(grid.Cells[1, 0]);
        }

        [Test]
        public void CreateCostField_EvaluatesGroundRoughImpassableAndMissingSupportPriority()
        {
            // Arrange — 3x2 grid at TEST_ORIGIN, cellSize 2:
            // (0,0): Ground only -> 1
            // (1,0): RoughTerrain floor only -> 1 + 3 = 4
            // (2,0): Ground floor + RoughTerrain overlap -> 1 + 3 = 4
            // (0,1): Ground floor + Impassable overlap -> 255
            // (1,1): RoughTerrain floor + Impassable overlap -> 255
            // (2,1): Void (no floor) -> 255
            FlowField flowField = new FlowField();
            Grid grid = CreateOffsetGrid(3, 2, 2f);

            CreateBoxCollider("Ground00", grid.Cells[0, 0].WorldPos + new Vector3(0f, -0.5f, 0f), new Vector3(1.8f, 1f, 1.8f), "Ground");
            CreateBoxCollider("RoughFloor10", grid.Cells[1, 0].WorldPos + new Vector3(0f, -0.5f, 0f), new Vector3(1.8f, 1f, 1.8f), "RoughTerrain");
            CreateBoxCollider("Ground20", grid.Cells[2, 0].WorldPos + new Vector3(0f, -0.5f, 0f), new Vector3(1.8f, 1f, 1.8f), "Ground");
            CreateBoxCollider("RoughOverlap20", grid.Cells[2, 0].WorldPos + new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.2f, 0.5f), "RoughTerrain");

            CreateBoxCollider("Ground01", grid.Cells[0, 1].WorldPos + new Vector3(0f, -0.5f, 0f), new Vector3(1.8f, 1f, 1.8f), "Ground");
            CreateBoxCollider("Impassable01", grid.Cells[0, 1].WorldPos + new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f), "Impassable");

            CreateBoxCollider("RoughFloor11", grid.Cells[1, 1].WorldPos + new Vector3(0f, -0.5f, 0f), new Vector3(1.8f, 1f, 1.8f), "RoughTerrain");
            CreateBoxCollider("Impassable11", grid.Cells[1, 1].WorldPos + new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f), "Impassable");

            // Act
            flowField.CreateCostField(grid);

            // Assert
            Assert.AreEqual((byte)1, grid.Cells[0, 0].Cost);
            Assert.AreEqual((byte)4, grid.Cells[1, 0].Cost);
            Assert.AreEqual((byte)4, grid.Cells[2, 0].Cost);
            Assert.AreEqual((byte)FlowFieldConstants.IMPASSABLE_COST, grid.Cells[0, 1].Cost);
            Assert.AreEqual((byte)FlowFieldConstants.IMPASSABLE_COST, grid.Cells[1, 1].Cost);
            Assert.AreEqual((byte)FlowFieldConstants.IMPASSABLE_COST, grid.Cells[2, 1].Cost);
        }

        [Test]
        public void CreateCostField_IgnoresTriggerCollidersOnRoughAndImpassableLayers()
        {
            // Arrange
            FlowField flowField = new FlowField();
            Grid grid = CreateOffsetGrid(1, 1, 2f);
            Vector3 center = grid.Cells[0, 0].WorldPos;
            CreateBoxCollider("SolidGround", center + new Vector3(0f, -0.5f, 0f), new Vector3(1.8f, 1f, 1.8f), "Ground");
            CreateBoxCollider("TriggerImpassable", center + new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f), "Impassable", isTrigger: true);
            CreateBoxCollider("TriggerRough", center + new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f), "RoughTerrain", isTrigger: true);

            // Act
            flowField.CreateCostField(grid);

            // Assert
            Assert.AreEqual((byte)FlowFieldConstants.DEFAULT_FIELD_COST, grid.Cells[0, 0].Cost);
        }

        [Test]
        public void CreateCostField_EmbeddedOriginAndSteepSlope_BlockCell()
        {
            // Arrange
            FlowField flowField = new FlowField();
            Grid grid = CreateOffsetGrid(2, 1, 2f);

            // Cell (0,0): Ground floor plus Ground ceiling embedding probe origin at y + 1.5
            CreateBoxCollider("Floor00", grid.Cells[0, 0].WorldPos + new Vector3(0f, -0.5f, 0f), new Vector3(1.8f, 1f, 1.8f), "Ground");
            CreateBoxCollider("Ceiling00", grid.Cells[0, 0].WorldPos + new Vector3(0f, GridConstants.GROUND_PROBE_ORIGIN_HEIGHT, 0f), new Vector3(1f, 0.5f, 1f), "Ground");

            // Cell (1,0): Steep 89.5 deg tilted Ground slab (normal.y <= 0.01, kept clear of probe origin y + 1.5)
            GameObject steep = CreateBoxCollider(
                "Steep10",
                grid.Cells[1, 0].WorldPos + new Vector3(0f, -0.5f, -0.055f),
                new Vector3(1.8f, 0.1f, 2f),
                "Ground");
            steep.transform.rotation = Quaternion.Euler(89.5f, 0f, 0f);
            Physics.SyncTransforms();

            // Act
            flowField.CreateCostField(grid);

            // Assert
            Assert.AreEqual((byte)FlowFieldConstants.IMPASSABLE_COST, grid.Cells[0, 0].Cost);
            Assert.AreEqual((byte)FlowFieldConstants.IMPASSABLE_COST, grid.Cells[1, 0].Cost);
        }

        [Test]
        public void CreateCostField_TerrainBufferSaturationAtSixteenColliders_ConservativelyBlocksCell()
        {
            // Arrange — Cell (0,0) has 15 Ground colliders (1 floor + 14 small boxes < 16);
            // Cell (1,0) has 16 Ground colliders (1 floor + 15 small boxes == TERRAIN_COLLIDER_BUFFER_SIZE).
            FlowField flowField = new FlowField();
            Grid grid = CreateOffsetGrid(2, 1, 4f);
            CreateGroundClusterInCell(grid.Cells[0, 0].WorldPos, extraColliderCount: 14);
            CreateGroundClusterInCell(grid.Cells[1, 0].WorldPos, extraColliderCount: 15);

            // Act
            flowField.CreateCostField(grid);

            // Assert
            Assert.AreEqual((byte)FlowFieldConstants.DEFAULT_FIELD_COST, grid.Cells[0, 0].Cost);
            Assert.AreEqual((byte)FlowFieldConstants.IMPASSABLE_COST, grid.Cells[1, 0].Cost);
        }

        [Test]
        public void CreateIntegrationField_InvalidOrForeignOrImpassableDestination_LeavesGridUntouched()
        {
            // Arrange
            FlowField flowField = new FlowField();
            Grid grid = CreateOffsetGrid(2, 2, 2f);
            Grid otherGrid = CreateOffsetGrid(2, 2, 2f);
            grid.Cells[0, 0].Cost = FlowFieldConstants.IMPASSABLE_COST;

            // Act
            flowField.CreateIntegrationField(null, grid.Cells[1, 1]);
            flowField.CreateIntegrationField(new Grid(new GridConfiguration(2, 2, 2f), existingCells: null), grid.Cells[1, 1]);
            flowField.CreateIntegrationField(grid, null);
            flowField.CreateIntegrationField(grid, otherGrid.Cells[0, 0]);
            flowField.CreateIntegrationField(grid, grid.Cells[0, 0]);

            // Assert
            Assert.AreEqual(ushort.MaxValue, grid.Cells[0, 0].BestCost);
            Assert.AreEqual(ushort.MaxValue, grid.Cells[1, 0].BestCost);
            Assert.AreEqual(ushort.MaxValue, grid.Cells[0, 1].BestCost);
            Assert.AreEqual(ushort.MaxValue, grid.Cells[1, 1].BestCost);
        }

        [Test]
        public void CreateIntegrationField_UniformGrid_PropagatesCardinalManhattanCostsAndPreservesDestinationCost()
        {
            // Arrange
            FlowField flowField = new FlowField();
            Grid grid = CreateOffsetGrid(3, 3, 2f);
            Cell destination = grid.Cells[0, 0];
            destination.Cost = 4;

            // Act
            flowField.CreateIntegrationField(grid, destination);

            // Assert
            Assert.AreEqual((byte)4, destination.Cost);
            Assert.AreEqual((ushort)0, destination.BestCost);
            Assert.AreEqual((ushort)1, grid.Cells[1, 0].BestCost);
            Assert.AreEqual((ushort)1, grid.Cells[0, 1].BestCost);
            Assert.AreEqual((ushort)2, grid.Cells[1, 1].BestCost);
            Assert.AreEqual((ushort)4, grid.Cells[2, 2].BestCost);
        }

        [Test]
        public void CreateIntegrationField_WallDetourAndUnreachableIsland_ComputesDetourAndPreservesMaxOnIsland()
        {
            // Arrange — 4x4 uniform grid, destination (0,0), wall at (1,0) and (1,1),
            // and isolated corner (3,3) walled off by (2,3)=255 and (3,2)=255.
            FlowField flowField = new FlowField();
            Grid grid = CreateOffsetGrid(4, 4, 2f);
            grid.Cells[1, 0].Cost = FlowFieldConstants.IMPASSABLE_COST;
            grid.Cells[1, 1].Cost = FlowFieldConstants.IMPASSABLE_COST;
            grid.Cells[2, 3].Cost = FlowFieldConstants.IMPASSABLE_COST;
            grid.Cells[3, 2].Cost = FlowFieldConstants.IMPASSABLE_COST;

            // Act
            flowField.CreateIntegrationField(grid, grid.Cells[0, 0]);

            // Assert — Path to (2,0): (0,0)->(0,1)->(0,2)->(1,2)->(2,2)->(2,1)->(2,0) = 6
            Assert.AreEqual((ushort)3, grid.Cells[1, 2].BestCost);
            Assert.AreEqual((ushort)6, grid.Cells[2, 0].BestCost);
            Assert.AreEqual(ushort.MaxValue, grid.Cells[1, 0].BestCost);
            Assert.AreEqual(ushort.MaxValue, grid.Cells[3, 3].BestCost);
        }

        [Test]
        public void CreateIntegrationField_RoughTerrainShortcut_RelaxesToCheaperNormalDetour()
        {
            // Arrange — 3x3 grid, destination (0,0):
            // Direct route through rough (1,0) [Cost=4] -> (2,0) [Cost=1] costs 5.
            // Detour via (0,1)->(1,1)->(2,1)->(2,0) costs 4, so SPFA re-relaxes (2,0) to 4.
            FlowField flowField = new FlowField();
            Grid grid = CreateOffsetGrid(3, 3, 2f);
            grid.Cells[1, 0].Cost = 4;

            // Act
            flowField.CreateIntegrationField(grid, grid.Cells[0, 0]);

            // Assert
            Assert.AreEqual((ushort)4, grid.Cells[1, 0].BestCost);
            Assert.AreEqual((ushort)4, grid.Cells[2, 0].BestCost);
        }

        [Test]
        public void CreateIntegrationField_And_CreateFlowField_OnOffsetPlayerChunk_MapUsingChunkCoordinates()
        {
            // Arrange — Extract 3x3 chunk starting at world (2, 2) from a 6x6 world grid
            FlowField flowField = new FlowField();
            Grid worldGrid = CreateOffsetGrid(6, 6, 2f);
            Grid chunk = ExtractChunk(worldGrid, startX: 2, startY: 2, width: 3, height: 3);
            Cell destination = chunk.Cells[0, 0];

            // Act
            flowField.CreateIntegrationField(chunk, destination);
            flowField.CreateFlowField(chunk);

            // Assert
            Assert.AreEqual((ushort)0, chunk.Cells[0, 0].BestCost);
            Assert.AreEqual((ushort)4, chunk.Cells[2, 2].BestCost);
            Assert.AreSame(GridDirection.None, chunk.Cells[0, 0].BestDirection);
            Assert.AreSame(GridDirection.SouthWest, chunk.Cells[2, 2].BestDirection);
            Assert.AreSame(GridDirection.West, chunk.Cells[1, 0].BestDirection);
            Assert.AreEqual(ushort.MaxValue, worldGrid.Cells[0, 0].BestCost);
        }

        [Test]
        public void CreateIntegrationField_CalledTwiceWithNewDestinationWithoutCostReset_RecomputesBestCostsCleanly()
        {
            // Arrange
            FlowField flowField = new FlowField();
            Grid grid = CreateOffsetGrid(3, 3, 2f);

            // Act — First destination at (0,0), then second destination at (2,2) without CreateCostField in between
            flowField.CreateIntegrationField(grid, grid.Cells[0, 0]);
            flowField.CreateIntegrationField(grid, grid.Cells[2, 2]);

            // Assert — Prior destination (0,0) must now be distance 4, not stale 0
            Assert.AreEqual((ushort)0, grid.Cells[2, 2].BestCost);
            Assert.AreEqual((ushort)1, grid.Cells[2, 1].BestCost);
            Assert.AreEqual((ushort)4, grid.Cells[0, 0].BestCost);
        }

        [Test]
        public void CreateFlowField_NullOrEmptyCells_DoesNotThrow()
        {
            // Arrange
            FlowField flowField = new FlowField();
            Grid nullCellsGrid = new Grid(new GridConfiguration(2, 2, 1f), existingCells: null);

            // Act & Assert
            Assert.DoesNotThrow(delegate { flowField.CreateFlowField(null); });
            Assert.DoesNotThrow(delegate { flowField.CreateFlowField(nullCellsGrid); });
            Assert.IsNull(nullCellsGrid.Cells);
        }

        [Test]
        public void CreateFlowField_PreventsDiagonalCornerCuttingAndClearsBlockedOrUnreachableDirections()
        {
            // Arrange — 3x3 grid, destination (0,0), impassable wall at (1,0) and isolated cell (2,2)
            // blocked off by (2,1)=255 and (1,2)=255.
            // At (1,1): (0,0) has lower BestCost=0, but (1,0) is impassable so SouthWest is forbidden -> West.
            FlowField flowField = new FlowField();
            Grid grid = CreateOffsetGrid(3, 3, 2f);
            grid.Cells[1, 0].Cost = FlowFieldConstants.IMPASSABLE_COST;
            grid.Cells[1, 0].BestDirection = GridDirection.East;
            grid.Cells[2, 1].Cost = FlowFieldConstants.IMPASSABLE_COST;
            grid.Cells[1, 2].Cost = FlowFieldConstants.IMPASSABLE_COST;
            grid.Cells[2, 2].BestDirection = GridDirection.North;

            flowField.CreateIntegrationField(grid, grid.Cells[0, 0]);

            // Act
            flowField.CreateFlowField(grid);

            // Assert
            Assert.AreSame(GridDirection.None, grid.Cells[0, 0].BestDirection);
            Assert.AreSame(GridDirection.South, grid.Cells[0, 1].BestDirection);
            Assert.AreSame(GridDirection.West, grid.Cells[1, 1].BestDirection);
            Assert.AreSame(GridDirection.None, grid.Cells[1, 0].BestDirection);
            Assert.AreSame(GridDirection.None, grid.Cells[2, 2].BestDirection);
        }

        [Test]
        public void CanTraverse_EvaluatesBoundsForeignCellsImpassableTargetsAndDiagonalCorners()
        {
            // Arrange — 3x3 grid with impassable (1,0) and null cell at (0,2)
            Grid grid = CreateOffsetGrid(3, 3, 2f);
            Grid otherGrid = CreateOffsetGrid(3, 3, 2f);
            grid.Cells[1, 0].Cost = FlowFieldConstants.IMPASSABLE_COST;
            grid.Cells[0, 2] = null;

            // Act & Assert — Guards and foreign cells
            Assert.IsFalse(FlowField.CanTraverse(null, grid.Cells[0, 0], grid.Cells[0, 1]));
            Assert.IsFalse(FlowField.CanTraverse(grid, null, grid.Cells[0, 1]));
            Assert.IsFalse(FlowField.CanTraverse(grid, grid.Cells[0, 0], null));
            Assert.IsFalse(FlowField.CanTraverse(grid, otherGrid.Cells[0, 0], grid.Cells[0, 1]));
            Assert.IsFalse(FlowField.CanTraverse(grid, grid.Cells[0, 0], otherGrid.Cells[0, 1]));

            // Act & Assert — Distance and impassable destination vs impassable origin
            Assert.IsFalse(FlowField.CanTraverse(grid, grid.Cells[0, 0], grid.Cells[2, 0]));
            Assert.IsFalse(FlowField.CanTraverse(grid, grid.Cells[0, 0], grid.Cells[1, 0]));
            Assert.IsFalse(FlowField.CanTraverse(grid, grid.Cells[1, 0], grid.Cells[1, 0]));
            Assert.IsTrue(FlowField.CanTraverse(grid, grid.Cells[0, 0], grid.Cells[0, 0]));
            Assert.IsTrue(FlowField.CanTraverse(grid, grid.Cells[1, 0], grid.Cells[2, 0]));

            // Act & Assert — Diagonal corner checks (blocked by impassable (1,0), blocked by null (0,2), open at (1,1)->(2,2))
            Assert.IsFalse(FlowField.CanTraverse(grid, grid.Cells[0, 0], grid.Cells[1, 1]));
            Assert.IsFalse(FlowField.CanTraverse(grid, grid.Cells[1, 1], grid.Cells[0, 0]));
            Assert.IsFalse(FlowField.CanTraverse(grid, grid.Cells[1, 2], grid.Cells[0, 1]));
            Assert.IsTrue(FlowField.CanTraverse(grid, grid.Cells[1, 1], grid.Cells[2, 2]));
        }

        [Test]
        public void FlowFieldDebug_DisplayFlowFieldDebugTextOnGrid_CreatesAndReusesTextAcrossAllModes()
        {
            // Arrange
            Grid grid = CreateOffsetGrid(2, 1, 2f);
            grid.Cells[0, 0].Cost = 1;
            grid.Cells[0, 0].BestCost = 0;
            grid.Cells[0, 0].BestDirection = GridDirection.None;
            grid.Cells[1, 0].Cost = 4;
            grid.Cells[1, 0].BestCost = 3;
            grid.Cells[1, 0].BestDirection = GridDirection.West;

            GameObject holder = new GameObject("FlowFieldDebugHolder");
            _createdObjects.Add(holder);

            FlowFieldDebugConfiguration costConfig = new FlowFieldDebugConfiguration(
                grid,
                holder.transform,
                fontSize: 2f,
                textYOffset: 0.1f,
                displayMode: FlowFieldDebug.DisplayMode.CostField);

            FlowFieldDebugConfiguration integrationConfig = new FlowFieldDebugConfiguration(
                grid,
                holder.transform,
                fontSize: 2f,
                textYOffset: 0.1f,
                displayMode: FlowFieldDebug.DisplayMode.IntegrationField);

            FlowFieldDebugConfiguration directionConfig = new FlowFieldDebugConfiguration(
                grid,
                holder.transform,
                fontSize: 2f,
                textYOffset: 0.1f,
                displayMode: FlowFieldDebug.DisplayMode.FlowField);

            // Act & Assert — First pass creates 2 TMP labels in CostField mode
            FlowFieldDebug.DisplayFlowFieldDebugTextOnGrid(costConfig);
            Assert.AreEqual(2, holder.transform.childCount);
            Assert.AreEqual("1", holder.transform.GetChild(0).GetComponent<TextMeshPro>().text);
            Assert.AreEqual("4", holder.transform.GetChild(1).GetComponent<TextMeshPro>().text);

            // Act & Assert — Second pass reuses existing children for IntegrationField mode
            FlowFieldDebug.DisplayFlowFieldDebugTextOnGrid(integrationConfig);
            Assert.AreEqual(2, holder.transform.childCount);
            Assert.AreEqual("0", holder.transform.GetChild(0).GetComponent<TextMeshPro>().text);
            Assert.AreEqual("3", holder.transform.GetChild(1).GetComponent<TextMeshPro>().text);

            // Act & Assert — Third pass reuses existing children for FlowField mode
            FlowFieldDebug.DisplayFlowFieldDebugTextOnGrid(directionConfig);
            Assert.AreEqual(2, holder.transform.childCount);
            Assert.AreEqual(Vector2Int.zero.ToString(), holder.transform.GetChild(0).GetComponent<TextMeshPro>().text);
            Assert.AreEqual(Vector2Int.left.ToString(), holder.transform.GetChild(1).GetComponent<TextMeshPro>().text);
        }

        private Grid CreateOffsetGrid(int width, int height, float cellSize)
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

        private static Grid ExtractChunk(Grid worldGrid, int startX, int startY, int width, int height)
        {
            Cell[,] chunkCells = new Cell[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Cell cell = worldGrid.Cells[startX + x, startY + y];
                    cell.ChunkGridPos = new Vector2Int(x, y);
                    chunkCells[x, y] = cell;
                }
            }

            return new Grid(new GridConfiguration(width, height, worldGrid.CellSize), chunkCells);
        }

        private void CreateGroundClusterInCell(Vector3 cellCenter, int extraColliderCount)
        {
            CreateBoxCollider("ClusterFloor", cellCenter + new Vector3(0f, -0.5f, 0f), new Vector3(3f, 1f, 3f), "Ground");
            for (int i = 0; i < extraColliderCount; i++)
            {
                CreateBoxCollider(
                    $"ClusterProp_{i}",
                    cellCenter + new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0.2f, 0.2f, 0.2f),
                    "Ground");
            }
        }

        private GameObject CreateBoxCollider(
            string name,
            Vector3 center,
            Vector3 size,
            string layerName,
            bool isTrigger = false)
        {
            int layer = LayerMask.NameToLayer(layerName);
            Assert.GreaterOrEqual(layer, 0, $"Missing layer '{layerName}'");

            GameObject go = new GameObject(name);
            go.layer = layer;
            go.transform.position = center;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = isTrigger;
            _createdObjects.Add(go);
            Physics.SyncTransforms();
            return go;
        }
    }
}
