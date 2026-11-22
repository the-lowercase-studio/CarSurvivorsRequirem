using System.Collections.Generic;
using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.GridSystem;
using NUnit.Framework;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class GridCellsNotVisibleByMainCameraTests
    {
        private const float TOLERANCE = 0.0001f;
        private const int DEFAULT_RANDOM_SEED = 424242;
        private static readonly Vector3 TEST_ORIGIN = new Vector3(12000f, 500f, 12000f);

        private readonly List<UnityEngine.Object> _createdObjects = new();
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
        public void ContainsCell_MatchesChunkOrWorldCoordsAndRejectsForeignOrOutOfBoundsCells()
        {
            // Arrange
            Grid grid = CreateWorldGrid(3, 3, 2f);
            Cell memberViaChunk = grid.Cells[1, 2];

            Cell memberViaWorldOnly = grid.Cells[2, 1];
            memberViaWorldOnly.ChunkGridPos = GridConstants.INVALID_CHUNK_GRID_POS;

            Cell foreignSameCoords = new Cell(memberViaChunk.WorldPos, new Vector2Int(1, 2), new Vector2Int(1, 2));
            Cell outOfBounds = new Cell(Vector3.zero, new Vector2Int(5, 5), new Vector2Int(-1, 5));

            // Assert
            Assert.IsTrue(GridCellsNotVisibleByMainCamera.ContainsCell(grid, memberViaChunk));
            Assert.IsTrue(GridCellsNotVisibleByMainCamera.ContainsCell(grid, memberViaWorldOnly));
            Assert.IsFalse(GridCellsNotVisibleByMainCamera.ContainsCell(grid, foreignSameCoords));
            Assert.IsFalse(GridCellsNotVisibleByMainCamera.ContainsCell(grid, outOfBounds));
        }

        [Test]
        public void TryGetPlacement_ValidHiddenSupportedCell_ReturnsTrueWithGroundClearanceOffset()
        {
            // Arrange
            Grid grid = CreateWorldGrid(2, 2, 2f);
            CreateFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 2f), new Vector3(10f, 1f, 10f));
            Camera camera = CreateAllHiddenCamera();
            Cell targetCell = grid.Cells[0, 0];

            // Act
            bool placed = GridCellsNotVisibleByMainCamera.TryGetPlacement(
                targetCell,
                grid,
                camera,
                maxEnemiesPerCell: 2,
                out Vector3 position);

            // Assert
            Assert.IsTrue(placed);
            Assert.AreEqual(targetCell.WorldPos.x, position.x, TOLERANCE);
            Assert.AreEqual(TEST_ORIGIN.y + GridConstants.ROOT_GROUND_CLEARANCE, position.y, TOLERANCE);
            Assert.AreEqual(targetCell.WorldPos.z, position.z, TOLERANCE);
        }

        [Test]
        public void TryGetPlacement_NullOrUnwalkableOrForeignCell_ReturnsFalse()
        {
            // Arrange
            Grid grid = CreateWorldGrid(2, 2, 2f);
            CreateFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 2f), new Vector3(10f, 1f, 10f));
            Camera camera = CreateAllHiddenCamera();

            Cell unwalkable = grid.Cells[0, 0];
            unwalkable.Cost = byte.MaxValue;
            Cell foreign = new Cell(grid.Cells[1, 1].WorldPos, new Vector2Int(1, 1), new Vector2Int(1, 1));

            // Act & Assert
            Assert.IsFalse(GridCellsNotVisibleByMainCamera.TryGetPlacement(null, grid, camera, 2, out _));
            Assert.IsFalse(GridCellsNotVisibleByMainCamera.TryGetPlacement(unwalkable, grid, camera, 2, out _));
            Assert.IsFalse(GridCellsNotVisibleByMainCamera.TryGetPlacement(foreign, grid, camera, 2, out _));
        }

        [Test]
        public void TryGetPlacement_VisibleCell_RespectsRequireHiddenFromCameraFlag()
        {
            // Arrange
            Grid grid = CreateWorldGrid(2, 2, 2f);
            CreateFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 2f), new Vector3(10f, 1f, 10f));
            Cell targetCell = grid.Cells[1, 1];
            Camera camera = CreateCameraLookingAt(targetCell.WorldPos);

            // Act
            bool rejectedWhenHiddenRequired = GridCellsNotVisibleByMainCamera.TryGetPlacement(
                targetCell,
                grid,
                camera,
                maxEnemiesPerCell: 2,
                requireHiddenFromCamera: true,
                out _);

            bool acceptedWhenHiddenNotRequired = GridCellsNotVisibleByMainCamera.TryGetPlacement(
                targetCell,
                grid,
                camera,
                maxEnemiesPerCell: 2,
                requireHiddenFromCamera: false,
                out Vector3 position);

            // Assert
            Assert.IsFalse(rejectedWhenHiddenRequired);
            Assert.IsTrue(acceptedWhenHiddenNotRequired);
            Assert.AreEqual(TEST_ORIGIN.y + GridConstants.ROOT_GROUND_CLEARANCE, position.y, TOLERANCE);
        }

        [Test]
        public void TryGetPlacement_WithoutGroundSupportOrEmbeddedOrigin_ReturnsFalse()
        {
            // Arrange
            Grid grid = CreateWorldGrid(2, 2, 2f);
            CreateFloorCollider(TEST_ORIGIN + new Vector3(1f, -0.5f, 1f), new Vector3(1.5f, 1f, 1.5f));
            // Embedded impassable block overlapping ground probe origin at cell (0,0)
            CreateBoxObstacle(
                grid.Cells[0, 0].WorldPos + Vector3.up * GridConstants.GROUND_PROBE_ORIGIN_HEIGHT,
                Vector3.one,
                "Impassable");
            Camera camera = CreateAllHiddenCamera();

            // Act & Assert — Cell (0,0) probe origin is embedded; Cell (1,1) has no floor beneath it
            Assert.IsFalse(GridCellsNotVisibleByMainCamera.TryGetPlacement(grid.Cells[0, 0], grid, camera, 2, out _));
            Assert.IsFalse(GridCellsNotVisibleByMainCamera.TryGetPlacement(grid.Cells[1, 1], grid, camera, 2, out _));
        }

        [Test]
        public void TryGetPlacement_MaxEnemiesPerCellOccupancy_RejectsAtLimitAndIgnoresWhenNonPositive()
        {
            // Arrange
            Grid grid = CreateWorldGrid(2, 2, 2f);
            CreateFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 2f), new Vector3(10f, 1f, 10f));
            Camera camera = CreateAllHiddenCamera();
            Cell targetCell = grid.Cells[0, 0];

            CreateEnemyCollider(targetCell.WorldPos + new Vector3(-0.2f, 0.5f, 0f));
            CreateEnemyCollider(targetCell.WorldPos + new Vector3(0.2f, 0.5f, 0f));

            // Act & Assert — 2 existing enemies: limit 2 rejects, limit 3 accepts, <= 0 ignores occupancy
            Assert.IsFalse(GridCellsNotVisibleByMainCamera.TryGetPlacement(targetCell, grid, camera, 2, out _));
            Assert.IsTrue(GridCellsNotVisibleByMainCamera.TryGetPlacement(targetCell, grid, camera, 3, out _));
            Assert.IsTrue(GridCellsNotVisibleByMainCamera.TryGetPlacement(targetCell, grid, camera, 0, out _));
            Assert.IsTrue(GridCellsNotVisibleByMainCamera.TryGetPlacement(targetCell, grid, camera, -1, out _));
        }

        [Test]
        public void FillWalkableCells_ClearsBufferAndEmitsRemainingOccupancySlots()
        {
            // Arrange — Cell (0,0) has 0 enemies (2 slots), Cell (1,0) has 1 enemy (1 slot), Cell (0,1) unwalkable
            Grid grid = CreateWorldGrid(2, 2, 2f);
            grid.Cells[0, 1].Cost = byte.MaxValue;
            CreateFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 1f), new Vector3(6f, 1f, 2.5f));
            Camera camera = CreateAllHiddenCamera();

            CreateEnemyCollider(grid.Cells[1, 0].WorldPos + new Vector3(0f, 0.5f, 0f));

            List<Cell> results = new List<Cell>
            {
                new Cell(Vector3.zero, Vector2Int.zero, Vector2Int.zero)
            };

            // Act — With maxEnemiesPerCell = 2: 2 slots for (0,0) + 1 slot for (1,0) = 3
            GridCellsNotVisibleByMainCamera.FillWalkableCells(grid, camera, results, maxEnemiesPerCell: 2);
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(2, CountOccurrences(results, grid.Cells[0, 0]));
            Assert.AreEqual(1, CountOccurrences(results, grid.Cells[1, 0]));

            // Act — With maxEnemiesPerCell = -1: 1 slot per valid cell = 2
            GridCellsNotVisibleByMainCamera.FillWalkableCells(grid, camera, results, maxEnemiesPerCell: -1);
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(1, CountOccurrences(results, grid.Cells[0, 0]));
            Assert.AreEqual(1, CountOccurrences(results, grid.Cells[1, 0]));
        }

        [Test]
        public void GetRandomWalkableCells_RespectsIncludeRemainingCandidatesFlag()
        {
            // Arrange — 3 valid slots (maxEnemiesPerCell = 3 on single valid cell (0,0))
            Grid grid = CreateWorldGrid(2, 2, 2f);
            CreateFloorCollider(TEST_ORIGIN + new Vector3(1f, -0.5f, 1f), new Vector3(1.5f, 1f, 1.5f));
            Camera camera = CreateAllHiddenCamera();

            // Act
            List<Cell> capped = CollectCells(GridCellsNotVisibleByMainCamera.GetRandomWalkableCells(
                grid,
                camera,
                count: 1,
                maxEnemiesPerCell: 3,
                includeRemainingCandidates: false));

            List<Cell> all = CollectCells(GridCellsNotVisibleByMainCamera.GetRandomWalkableCells(
                grid,
                camera,
                count: 1,
                maxEnemiesPerCell: 3,
                includeRemainingCandidates: true));

            // Assert
            Assert.AreEqual(1, capped.Count);
            Assert.AreEqual(3, all.Count);
        }

        [Test]
        public void FillWalkableCellsOutsidePlayerChunk_ExcludesChunkAndRespectsBufferBounds()
        {
            // Arrange — 7x7 world grid with 3x3 chunk centered at (3,3) => [2..4, 2..4]
            Grid worldGrid = CreateWorldGrid(7, 7, 2f);
            Grid playerChunk = ExtractPlayerChunk(worldGrid, startX: 2, startY: 2, width: 3, height: 3);
            CreateFloorCollider(TEST_ORIGIN + new Vector3(7f, -0.5f, 7f), new Vector3(20f, 1f, 20f));
            Camera camera = CreateAllHiddenCamera();
            List<Cell> results = new List<Cell>();

            // Act & Assert — buffer=1 covers [1..5, 1..5] minus [2..4, 2..4] = 25 - 9 = 16 cells
            GridCellsNotVisibleByMainCamera.FillWalkableCellsOutsidePlayerChunk(
                worldGrid,
                playerChunk,
                camera,
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: -1,
                results);

            Assert.AreEqual(16, results.Count);
            AssertAllCellsOutsideBoundsAndWithinChebyshevDistance(results, 2, 4, 2, 4, maxDistanceFromCenter: 2, center: new Vector2Int(3, 3));

            // Act & Assert — buffer=2 covers [0..6, 0..6] minus [2..4, 2..4] = 49 - 9 = 40 cells
            GridCellsNotVisibleByMainCamera.FillWalkableCellsOutsidePlayerChunk(
                worldGrid,
                playerChunk,
                camera,
                outerSpawnBufferCells: 2,
                maxEnemiesPerCell: -1,
                results);

            Assert.AreEqual(40, results.Count);
            AssertAllCellsOutsideBoundsAndWithinChebyshevDistance(results, 2, 4, 2, 4, maxDistanceFromCenter: 3, center: new Vector2Int(3, 3));
        }

        [Test]
        public void GetRandomWalkableCellsOutsidePlayerChunk_ExpandsBufferIterativelyToMeetCount()
        {
            // Arrange — 11x11 world grid, 3x3 chunk at [4..6, 4..6] (center 5,5).
            // All outer cells unwalkable except 4 cells at Chebyshev distance 5: (0,5), (10,5), (5,0), (5,10).
            Grid worldGrid = CreateWorldGrid(11, 11, 2f);
            MarkAllCellsUnwalkable(worldGrid);
            Grid playerChunk = ExtractPlayerChunk(worldGrid, startX: 4, startY: 4, width: 3, height: 3);

            worldGrid.Cells[0, 5].Cost = GridConstants.DEFAULT_FIELD_COST;
            worldGrid.Cells[10, 5].Cost = GridConstants.DEFAULT_FIELD_COST;
            worldGrid.Cells[5, 0].Cost = GridConstants.DEFAULT_FIELD_COST;
            worldGrid.Cells[5, 10].Cost = GridConstants.DEFAULT_FIELD_COST;

            CreateFloorCollider(TEST_ORIGIN + new Vector3(11f, -0.5f, 11f), new Vector3(30f, 1f, 30f));
            Camera camera = CreateAllHiddenCamera();

            // Act — Starting buffer=1 finds 0 cells, expands +4 to buffer=5 (range 6 -> [0..10]), finds 4 >= 3
            List<Cell> requestedThree = CollectCells(GridCellsNotVisibleByMainCamera.GetRandomWalkableCellsOutsidePlayerChunk(
                worldGrid,
                playerChunk,
                camera,
                count: 3,
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: -1,
                includeRemainingCandidates: false));

            List<Cell> allFour = CollectCells(GridCellsNotVisibleByMainCamera.GetRandomWalkableCellsOutsidePlayerChunk(
                worldGrid,
                playerChunk,
                camera,
                count: 3,
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: -1,
                includeRemainingCandidates: true));

            // Assert
            Assert.AreEqual(3, requestedThree.Count);
            Assert.AreEqual(4, allFour.Count);
        }

        [Test]
        public void FillWalkableCellsOutsidePlayerChunk_NullChunkCenterOrZeroCandidates_ReturnsEmptyCleanly()
        {
            // Arrange
            Grid worldGrid = CreateWorldGrid(5, 5, 2f);
            Grid playerChunk = ExtractPlayerChunk(worldGrid, startX: 1, startY: 1, width: 3, height: 3);
            Camera camera = CreateAllHiddenCamera();
            List<Cell> results = new List<Cell>();

            // Case 1: Null center cell
            playerChunk.Cells[1, 1] = null;
            GridCellsNotVisibleByMainCamera.FillWalkableCellsOutsidePlayerChunk(worldGrid, playerChunk, camera, 2, -1, results);
            Assert.AreEqual(0, results.Count);

            // Case 2: Valid center cell, no ground floor anywhere outside -> GetRandomWalkableCellsOutsidePlayerChunk terminates cleanly
            playerChunk.Cells[1, 1] = worldGrid.Cells[2, 2];
            List<Cell> emptyResult = CollectCells(GridCellsNotVisibleByMainCamera.GetRandomWalkableCellsOutsidePlayerChunk(
                worldGrid,
                playerChunk,
                camera,
                count: 5,
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: -1));

            Assert.AreEqual(0, emptyResult.Count);
        }

        [Test]
        public void TryGetPlacement_BossLayerColliderCountsTowardOccupancyLimit()
        {
            // Arrange
            Grid grid = CreateWorldGrid(2, 2, 2f);
            CreateFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 2f), new Vector3(10f, 1f, 10f));
            Camera camera = CreateAllHiddenCamera();
            Cell targetCell = grid.Cells[0, 0];
            CreateBoxObstacle(targetCell.WorldPos + new Vector3(0f, 0.5f, 0f), new Vector3(0.4f, 1f, 0.4f), "Boss");

            // Act
            bool blockedAtLimitOne = GridCellsNotVisibleByMainCamera.TryGetPlacement(targetCell, grid, camera, 1, out _);
            bool allowedAtLimitTwo = GridCellsNotVisibleByMainCamera.TryGetPlacement(targetCell, grid, camera, 2, out _);

            // Assert
            Assert.IsFalse(blockedAtLimitOne);
            Assert.IsTrue(allowedAtLimitTwo);
        }

        [Test]
        public void FillWalkableCells_WhenRequireHiddenFromCameraFalse_IncludesCameraVisibleCells()
        {
            // Arrange — Orthographic camera sees only (1, 1) (at top-right), while (0, 0) stays outside frustum
            Grid grid = CreateWorldGrid(2, 2, 2f);
            grid.Cells[1, 0].Cost = byte.MaxValue;
            grid.Cells[0, 1].Cost = byte.MaxValue;
            CreateFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 2f), new Vector3(10f, 1f, 10f));
            Camera camera = CreateCameraLookingAt(grid.Cells[1, 1].WorldPos);
            camera.orthographic = true;
            camera.orthographicSize = 0.8f;
            camera.aspect = 1f;

            List<Cell> hiddenOnlyResults = new List<Cell>();
            List<Cell> allWalkableResults = new List<Cell>();

            // Act
            GridCellsNotVisibleByMainCamera.FillWalkableCells(
                grid,
                camera,
                hiddenOnlyResults,
                maxEnemiesPerCell: -1,
                requireHiddenFromCamera: true);

            GridCellsNotVisibleByMainCamera.FillWalkableCells(
                grid,
                camera,
                allWalkableResults,
                maxEnemiesPerCell: -1,
                requireHiddenFromCamera: false);

            // Assert
            Assert.AreEqual(1, hiddenOnlyResults.Count);
            Assert.AreSame(grid.Cells[0, 0], hiddenOnlyResults[0]);
            Assert.AreEqual(2, allWalkableResults.Count);
            CollectionAssert.Contains(allWalkableResults, grid.Cells[0, 0]);
            CollectionAssert.Contains(allWalkableResults, grid.Cells[1, 1]);
        }

        [Test]
        public void GetRandomWalkableCellsAndOutsideChunk_WhenCountIsZero_ReturnEmptyWithoutRemainingFlag()
        {
            // Arrange
            Grid worldGrid = CreateWorldGrid(5, 5, 2f);
            Grid playerChunk = ExtractPlayerChunk(worldGrid, startX: 1, startY: 1, width: 3, height: 3);
            CreateFloorCollider(TEST_ORIGIN + new Vector3(5f, -0.5f, 5f), new Vector3(16f, 1f, 16f));
            Camera camera = CreateAllHiddenCamera();

            // Act
            List<Cell> gridCells = CollectCells(GridCellsNotVisibleByMainCamera.GetRandomWalkableCells(
                worldGrid,
                camera,
                count: 0,
                maxEnemiesPerCell: -1,
                includeRemainingCandidates: false));

            List<Cell> outsideCells = CollectCells(GridCellsNotVisibleByMainCamera.GetRandomWalkableCellsOutsidePlayerChunk(
                worldGrid,
                playerChunk,
                camera,
                count: 0,
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: -1,
                includeRemainingCandidates: false));

            // Assert
            Assert.AreEqual(0, gridCells.Count);
            Assert.AreEqual(0, outsideCells.Count);
        }

        [Test]
        public void FillWalkableCellsOutsidePlayerChunk_EvenDimensionsAndOccupancyLimit_ExcludesExactChunkAndCapsSlots()
        {
            // Arrange — 6x6 world grid with 2x2 chunk at [1..2, 1..2]; chunk[1,1] = world (2,2), halfWidth = 1.
            // Exact chunk exclusion covers [1..2, 1..2] (4 cells).
            // With buffer = 1 -> [0..3, 0..3] (16 cells) minus 4 chunk cells = 12 candidate cells;
            // placing 2 enemies on (0,0) with maxEnemiesPerCell = 2 leaves 11*2 + 0 = 22 slots, including (3,3).
            Grid worldGrid = CreateWorldGrid(6, 6, 2f);
            Grid playerChunk = ExtractPlayerChunk(worldGrid, startX: 1, startY: 1, width: 2, height: 2);
            CreateFloorCollider(TEST_ORIGIN + new Vector3(6f, -0.5f, 6f), new Vector3(18f, 1f, 18f));
            Camera camera = CreateAllHiddenCamera();

            CreateEnemyCollider(worldGrid.Cells[0, 0].WorldPos + new Vector3(-0.2f, 0.5f, 0f));
            CreateEnemyCollider(worldGrid.Cells[0, 0].WorldPos + new Vector3(0.2f, 0.5f, 0f));

            List<Cell> results = new List<Cell>();

            // Act
            GridCellsNotVisibleByMainCamera.FillWalkableCellsOutsidePlayerChunk(
                worldGrid,
                playerChunk,
                camera,
                outerSpawnBufferCells: 1,
                maxEnemiesPerCell: 2,
                results);

            // Assert
            Assert.AreEqual(22, results.Count);
            Assert.AreEqual(0, CountOccurrences(results, worldGrid.Cells[0, 0]));
            Assert.AreEqual(0, CountOccurrences(results, worldGrid.Cells[1, 1]));
            Assert.AreEqual(0, CountOccurrences(results, worldGrid.Cells[2, 2]));
            Assert.AreEqual(2, CountOccurrences(results, worldGrid.Cells[3, 3]));
        }

        private static Grid CreateWorldGrid(int width, int height, float cellSize)
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

        private static Grid ExtractPlayerChunk(Grid worldGrid, int startX, int startY, int width, int height)
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

        private Camera CreateAllHiddenCamera()
        {
            GameObject cameraGo = new GameObject("AllHiddenTestCamera");
            cameraGo.transform.position = TEST_ORIGIN + new Vector3(0f, 50f, 0f);
            cameraGo.transform.rotation = Quaternion.LookRotation(Vector3.up);
            _createdObjects.Add(cameraGo);

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private Camera CreateCameraLookingAt(Vector3 targetPosition)
        {
            GameObject cameraGo = new GameObject("LookingAtTestCamera");
            cameraGo.transform.position = targetPosition + new Vector3(0f, 2f, -4f);
            cameraGo.transform.LookAt(targetPosition);
            _createdObjects.Add(cameraGo);

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private GameObject CreateFloorCollider(Vector3 center, Vector3 size)
        {
            return CreateBoxObstacle(center, size, "Ground");
        }

        private GameObject CreateEnemyCollider(Vector3 center)
        {
            return CreateBoxObstacle(center, new Vector3(0.4f, 1f, 0.4f), "Enemy");
        }

        private GameObject CreateBoxObstacle(Vector3 center, Vector3 size, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            Assert.GreaterOrEqual(layer, 0, $"Missing layer '{layerName}'");

            GameObject go = new GameObject($"TestCollider_{layerName}");
            go.layer = layer;
            go.transform.position = center;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
            _createdObjects.Add(go);
            Physics.SyncTransforms();
            return go;
        }

        private static List<Cell> CollectCells(IEnumerable<Cell> source)
        {
            List<Cell> list = new List<Cell>();
            foreach (Cell cell in source)
            {
                list.Add(cell);
            }

            return list;
        }

        private static int CountOccurrences(List<Cell> cells, Cell target)
        {
            int count = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == target)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertAllCellsOutsideBoundsAndWithinChebyshevDistance(
            List<Cell> cells,
            int chunkMinX,
            int chunkMaxX,
            int chunkMinY,
            int chunkMaxY,
            int maxDistanceFromCenter,
            Vector2Int center)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int pos = cells[i].WorldGridPos;
                bool insideChunk = pos.x >= chunkMinX && pos.x <= chunkMaxX && pos.y >= chunkMinY && pos.y <= chunkMaxY;
                Assert.IsFalse(insideChunk, $"Cell {pos} should have been excluded from GridPlayerChunk.");

                int distX = Mathf.Abs(pos.x - center.x);
                int distY = Mathf.Abs(pos.y - center.y);
                Assert.LessOrEqual(Mathf.Max(distX, distY), maxDistanceFromCenter);
            }
        }
    }
}
