using System.Collections.Generic;
using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.GridSystem;
using NUnit.Framework;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class GridPrimitivesAndQueriesTests
    {
        private const float TOLERANCE = 0.0001f;
        private static readonly Vector3 TEST_ORIGIN = new Vector3(36000f, 500f, 36000f);

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
        public void Cell_Constructor_InitializesDefaultCostsAndCoordinates()
        {
            // Arrange
            Vector3 worldPos = new Vector3(3.5f, 0f, 7.5f);
            Vector2Int worldGridPos = new Vector2Int(1, 3);
            Vector2Int chunkGridPos = new Vector2Int(0, 2);

            // Act
            Cell cell = new Cell(worldPos, worldGridPos, chunkGridPos);

            // Assert
            Assert.AreEqual(worldPos.x, cell.WorldPos.x, TOLERANCE);
            Assert.AreEqual(worldPos.y, cell.WorldPos.y, TOLERANCE);
            Assert.AreEqual(worldPos.z, cell.WorldPos.z, TOLERANCE);
            Assert.AreEqual(worldGridPos, cell.WorldGridPos);
            Assert.AreEqual(chunkGridPos, cell.ChunkGridPos);
            Assert.AreEqual(GridConstants.DEFAULT_FIELD_COST, cell.Cost);
            Assert.AreEqual(ushort.MaxValue, cell.BestCost);
            Assert.AreSame(GridDirection.None, cell.BestDirection);
        }

        [Test]
        public void Cell_IncreaseCost_IgnoresNonPositiveAddsWithinRangeAndClampsAtByteMax()
        {
            // Arrange
            Cell cell = new Cell(Vector3.zero, Vector2Int.zero, Vector2Int.zero);

            // Act & Assert — Non-positive amounts are ignored
            cell.IncreaseCost(0);
            Assert.AreEqual((byte)1, cell.Cost);

            cell.IncreaseCost(-10);
            Assert.AreEqual((byte)1, cell.Cost);

            // Act & Assert — Additive up to 254, then clamped at 255
            cell.IncreaseCost(3);
            Assert.AreEqual((byte)4, cell.Cost);

            cell.IncreaseCost(250);
            Assert.AreEqual((byte)254, cell.Cost);

            cell.IncreaseCost(1);
            Assert.AreEqual(byte.MaxValue, cell.Cost);

            cell.IncreaseCost(50);
            Assert.AreEqual(byte.MaxValue, cell.Cost);
        }

        [Test]
        public void Cell_ResetCosts_RestoresCostDefaultsWhilePreservingGridCoordinates()
        {
            // Arrange
            Vector3 worldPos = new Vector3(12f, 0f, 8f);
            Vector2Int worldGridPos = new Vector2Int(6, 4);
            Vector2Int chunkGridPos = new Vector2Int(2, 1);
            Cell cell = new Cell(worldPos, worldGridPos, chunkGridPos);
            cell.IncreaseCost(200);
            cell.BestCost = 14;
            cell.BestDirection = GridDirection.NorthEast;

            // Act
            cell.ResetCosts();

            // Assert
            Assert.AreEqual(GridConstants.DEFAULT_FIELD_COST, cell.Cost);
            Assert.AreEqual(ushort.MaxValue, cell.BestCost);
            Assert.AreSame(GridDirection.None, cell.BestDirection);
            Assert.AreEqual(worldPos, cell.WorldPos);
            Assert.AreEqual(worldGridPos, cell.WorldGridPos);
            Assert.AreEqual(chunkGridPos, cell.ChunkGridPos);
        }

        [Test]
        public void Grid_ConfigurationConstructor_CreatesCenteredCellsForNonSquareNonUnitGrid()
        {
            // Arrange
            GridConfiguration configuration = new GridConfiguration(width: 3, height: 2, cellSize: 2.5f);

            // Act
            Grid grid = new Grid(configuration);

            // Assert
            Assert.AreEqual(3, grid.Width);
            Assert.AreEqual(2, grid.Height);
            Assert.AreEqual(2.5f, grid.CellSize, TOLERANCE);
            Assert.AreEqual(3, grid.Cells.GetLength(0));
            Assert.AreEqual(2, grid.Cells.GetLength(1));

            Cell originCell = grid.Cells[0, 0];
            Assert.AreEqual(new Vector3(1.25f, 0f, 1.25f), originCell.WorldPos);
            Assert.AreEqual(new Vector2Int(0, 0), originCell.WorldGridPos);
            Assert.AreEqual(new Vector2Int(0, 0), originCell.ChunkGridPos);

            Cell farCornerCell = grid.Cells[2, 1];
            Assert.AreEqual(new Vector3(6.25f, 0f, 3.75f), farCornerCell.WorldPos);
            Assert.AreEqual(new Vector2Int(2, 1), farCornerCell.WorldGridPos);
            Assert.AreEqual(new Vector2Int(2, 1), farCornerCell.ChunkGridPos);
        }

        [Test]
        public void Grid_ExistingCellsConstructor_SharesProvidedArrayReferenceWithoutCopying()
        {
            // Arrange
            Cell[,] sharedCells = new Cell[2, 2];
            Cell cell00 = new Cell(new Vector3(1f, 0f, 1f), new Vector2Int(4, 5), new Vector2Int(0, 0));
            sharedCells[0, 0] = cell00;
            GridConfiguration configuration = new GridConfiguration(width: 2, height: 2, cellSize: 2f);

            // Act
            Grid chunkGrid = new Grid(configuration, sharedCells);

            // Assert
            Assert.AreSame(sharedCells, chunkGrid.Cells);
            Assert.AreSame(cell00, chunkGrid.Cells[0, 0]);
        }

        [Test]
        public void GridDirection_StaticCollectionsAndImplicitConversion_ContainExpectedDirections()
        {
            // Arrange
            Vector2Int northVector = GridDirection.North;
            Vector2Int southWestVector = GridDirection.SouthWest;

            // Assert
            Assert.AreEqual(new Vector2Int(0, 1), northVector);
            Assert.AreEqual(new Vector2Int(-1, -1), southWestVector);
            Assert.AreEqual(4, GridDirection.CardinalDirections.Count);
            Assert.AreEqual(8, GridDirection.CardinalAndIntercardinalDirections.Count);
            Assert.AreEqual(9, GridDirection.AllDirections.Count);
            Assert.AreSame(GridDirection.None, GridDirection.AllDirections[0]);
            CollectionAssert.AreEqual(
                new[] { GridDirection.North, GridDirection.East, GridDirection.South, GridDirection.West },
                GridDirection.CardinalDirections);
        }

        [TestCase(0, 1, nameof(GridDirection.North), TestName = "GetDirectionFromV2I_NorthVector_ReturnsNorth")]
        [TestCase(1, 1, nameof(GridDirection.NorthEast), TestName = "GetDirectionFromV2I_NorthEastVector_ReturnsNorthEast")]
        [TestCase(1, 0, nameof(GridDirection.East), TestName = "GetDirectionFromV2I_EastVector_ReturnsEast")]
        [TestCase(1, -1, nameof(GridDirection.SouthEast), TestName = "GetDirectionFromV2I_SouthEastVector_ReturnsSouthEast")]
        [TestCase(0, -1, nameof(GridDirection.South), TestName = "GetDirectionFromV2I_SouthVector_ReturnsSouth")]
        [TestCase(-1, -1, nameof(GridDirection.SouthWest), TestName = "GetDirectionFromV2I_SouthWestVector_ReturnsSouthWest")]
        [TestCase(-1, 0, nameof(GridDirection.West), TestName = "GetDirectionFromV2I_WestVector_ReturnsWest")]
        [TestCase(-1, 1, nameof(GridDirection.NorthWest), TestName = "GetDirectionFromV2I_NorthWestVector_ReturnsNorthWest")]
        [TestCase(0, 0, nameof(GridDirection.None), TestName = "GetDirectionFromV2I_ZeroVector_ReturnsNone")]
        [TestCase(2, 0, nameof(GridDirection.None), TestName = "GetDirectionFromV2I_NonUnitCardinalVector_ReturnsNone")]
        [TestCase(1, 2, nameof(GridDirection.None), TestName = "GetDirectionFromV2I_NonUnitDiagonalVector_ReturnsNone")]
        public void GridDirection_GetDirectionFromV2I_MapsExpectedDirection(int x, int y, string expectedName)
        {
            // Arrange
            GridDirection expected = ResolveDirectionByName(expectedName);

            // Act
            GridDirection actual = GridDirection.GetDirectionFromV2I(new Vector2Int(x, y));

            // Assert
            Assert.AreSame(expected, actual);
        }

        [TestCase((byte)1, true, TestName = "CellStatusDescriber_DefaultCostOne_IsWalkable")]
        [TestCase((byte)254, true, TestName = "CellStatusDescriber_MaxWalkableCost254_IsWalkable")]
        [TestCase((byte)255, false, TestName = "CellStatusDescriber_ImpassableCost255_IsNotWalkable")]
        public void CellStatusDescriber_IsWalkable_EvaluatesCostThreshold(byte cost, bool expectedWalkable)
        {
            // Arrange
            Cell cell = new Cell(Vector3.zero, Vector2Int.zero, Vector2Int.zero)
            {
                Cost = cost
            };

            // Act
            bool actual = CellStatusDescriber.IsWalkable(cell);

            // Assert
            Assert.AreEqual(expectedWalkable, actual);
        }

        [TestCase(0f, 0f, 0f, 0, 0, TestName = "WorldPosToCellConverter_ExactLowerOrigin_MapsToZeroZero")]
        [TestCase(1.99f, 45f, 1.99f, 0, 0, TestName = "WorldPosToCellConverter_BeforeCellBorderWithNonZeroY_MapsToZeroZero")]
        [TestCase(2f, -15f, 2f, 1, 1, TestName = "WorldPosToCellConverter_ExactCellBorder_MapsToNextCell")]
        [TestCase(7.99f, 0f, 5.99f, 3, 2, TestName = "WorldPosToCellConverter_JustInsideMaxExtent_MapsToLastCell")]
        [TestCase(8f, 0f, 6f, 3, 2, TestName = "WorldPosToCellConverter_ExactUpperExtent_ClampsToLastCell")]
        [TestCase(-10f, 0f, -5f, 0, 0, TestName = "WorldPosToCellConverter_NegativeOutOfBounds_ClampsToZeroZero")]
        [TestCase(40f, 0f, 30f, 3, 2, TestName = "WorldPosToCellConverter_PositiveOutOfBounds_ClampsToLastCell")]
        public void WorldPosToCellConverter_GetCellFromGridByWorldPos_MapsAndClampsDeterministically(
            float x,
            float y,
            float z,
            int expectedGridX,
            int expectedGridY)
        {
            // Arrange — 4x3 grid with cellSize 2 spans [0..8] x [0..6]
            Grid grid = new Grid(new GridConfiguration(width: 4, height: 3, cellSize: 2f));

            // Act
            Cell actual = WorldPosToCellConverter.GetCellFromGridByWorldPos(grid, new Vector3(x, y, z));

            // Assert
            Assert.AreSame(grid.Cells[expectedGridX, expectedGridY], actual);
        }

        [TestCase(0f, 0f, 5f, true, TestName = "CellCameraVisibilityChecker_CenterInFrustum_ReturnsTrue")]
        [TestCase(-5f, -5f, 5f, true, TestName = "CellCameraVisibilityChecker_ExactBottomLeftViewportBorder_ReturnsTrue")]
        [TestCase(5f, 5f, 5f, true, TestName = "CellCameraVisibilityChecker_ExactTopRightViewportBorder_ReturnsTrue")]
        [TestCase(-5.01f, 0f, 5f, false, TestName = "CellCameraVisibilityChecker_OutsideLeftBorder_ReturnsFalse")]
        [TestCase(5.01f, 0f, 5f, false, TestName = "CellCameraVisibilityChecker_OutsideRightBorder_ReturnsFalse")]
        [TestCase(0f, -5.01f, 5f, false, TestName = "CellCameraVisibilityChecker_OutsideBottomBorder_ReturnsFalse")]
        [TestCase(0f, 5.01f, 5f, false, TestName = "CellCameraVisibilityChecker_OutsideTopBorder_ReturnsFalse")]
        [TestCase(0f, 0f, 0f, false, TestName = "CellCameraVisibilityChecker_ExactZeroDepth_ReturnsFalse")]
        [TestCase(0f, 0f, -0.5f, false, TestName = "CellCameraVisibilityChecker_BehindCamera_ReturnsFalse")]
        public void CellCameraVisibilityChecker_IsCellVisibleFromCamera_EvaluatesViewportAndDepthBounds(
            float offsetX,
            float offsetY,
            float offsetZ,
            bool expectedVisible)
        {
            // Arrange — Orthographic camera at TEST_ORIGIN facing +Z with [-5, +5] X/Y frustum
            Camera camera = CreateOrthographicTestCamera(TEST_ORIGIN, orthoSize: 5f);
            Vector3 cellPosition = TEST_ORIGIN + new Vector3(offsetX, offsetY, offsetZ);

            // Act
            bool visible = CellCameraVisibilityChecker.IsCellVisibleFromCamera(cellPosition, camera);

            // Assert
            Assert.AreEqual(expectedVisible, visible);
        }

        [TestCase("Ground", 0f, TestName = "GroundSupportQuery_GroundFloorAtOriginHeight_ReturnsSupportedWithClearance")]
        [TestCase("RoughTerrain", 0.75f, TestName = "GroundSupportQuery_ElevatedRoughTerrain_ReturnsSupportedWithClearance")]
        [TestCase("Ground", -1.8f, TestName = "GroundSupportQuery_LowerGroundWithinProbeReach_ReturnsSupportedWithClearance")]
        public void GroundSupportQuery_TryGetSupportedPosition_ValidWalkableSurface_ReturnsTrueAndExactHitPosition(
            string layerName,
            float surfaceOffsetY)
        {
            // Arrange
            Vector3 candidate = TEST_ORIGIN;
            GameObject floor = CreateBoxCollider(
                "SupportedFloor",
                TEST_ORIGIN + new Vector3(0f, surfaceOffsetY - 0.5f, 0f),
                new Vector3(4f, 1f, 4f),
                layerName);
            Collider expectedCollider = floor.GetComponent<Collider>();

            // Act
            bool supported = GroundSupportQuery.TryGetSupportedPosition(candidate, out Vector3 position, out Collider supportCollider);

            // Assert
            Assert.IsTrue(supported);
            Assert.AreEqual(candidate.x, position.x, TOLERANCE);
            Assert.AreEqual(TEST_ORIGIN.y + surfaceOffsetY + GridConstants.ROOT_GROUND_CLEARANCE, position.y, TOLERANCE);
            Assert.AreEqual(candidate.z, position.z, TOLERANCE);
            Assert.AreSame(expectedCollider, supportCollider);
        }

        [Test]
        public void GroundSupportQuery_TryGetSupportedPosition_MissingOrOutOfReachOrNonWalkableFloor_ReturnsFalse()
        {
            // Arrange — Floor 1 sits below probe reach (1.5 - 3.5 = -2.0 -> top at -2.1 is out of reach);
            // Floor 2 at second probe is Impassable; Floor 3 at third probe is Default.
            Vector3 voidCandidate = TEST_ORIGIN;
            Vector3 farFloorCandidate = TEST_ORIGIN + new Vector3(10f, 0f, 0f);
            Vector3 impassableCandidate = TEST_ORIGIN + new Vector3(20f, 0f, 0f);
            Vector3 defaultLayerCandidate = TEST_ORIGIN + new Vector3(30f, 0f, 0f);

            CreateBoxCollider("OutOfReachFloor", farFloorCandidate + new Vector3(0f, -2.6f, 0f), new Vector3(2f, 1f, 2f), "Ground");
            CreateBoxCollider("ImpassableOnlyFloor", impassableCandidate + new Vector3(0f, -0.5f, 0f), new Vector3(2f, 1f, 2f), "Impassable");
            CreateBoxCollider("DefaultOnlyFloor", defaultLayerCandidate + new Vector3(0f, -0.5f, 0f), new Vector3(2f, 1f, 2f), "Default");

            // Act & Assert
            Assert.IsFalse(GroundSupportQuery.TryGetSupportedPosition(voidCandidate, out Vector3 voidPos, out Collider voidCollider));
            Assert.AreEqual(voidCandidate, voidPos);
            Assert.IsNull(voidCollider);

            Assert.IsFalse(GroundSupportQuery.TryGetSupportedPosition(farFloorCandidate, out _, out _));
            Assert.IsFalse(GroundSupportQuery.TryGetSupportedPosition(impassableCandidate, out _, out _));
            Assert.IsFalse(GroundSupportQuery.TryGetSupportedPosition(defaultLayerCandidate, out _, out _));
        }

        [TestCase("Ground", TestName = "GroundSupportQuery_EmbeddedOriginInGround_ReturnsFalse")]
        [TestCase("RoughTerrain", TestName = "GroundSupportQuery_EmbeddedOriginInRoughTerrain_ReturnsFalse")]
        [TestCase("Impassable", TestName = "GroundSupportQuery_EmbeddedOriginInImpassable_ReturnsFalse")]
        public void GroundSupportQuery_TryGetSupportedPosition_EmbeddedProbeOrigin_RejectsEvenWithValidFloorBelow(
            string embeddedLayerName)
        {
            // Arrange — Valid Ground floor at TEST_ORIGIN.y, plus solid collider enclosing probe origin at y + 1.5
            CreateBoxCollider("ValidFloor", TEST_ORIGIN + new Vector3(0f, -0.5f, 0f), new Vector3(4f, 1f, 4f), "Ground");
            CreateBoxCollider(
                "EmbeddedOriginBlock",
                TEST_ORIGIN + new Vector3(0f, GridConstants.GROUND_PROBE_ORIGIN_HEIGHT, 0f),
                new Vector3(1f, 1f, 1f),
                embeddedLayerName);

            // Act
            bool supported = GroundSupportQuery.TryGetSupportedPosition(TEST_ORIGIN, out Vector3 position, out Collider supportCollider);

            // Assert
            Assert.IsFalse(supported);
            Assert.AreEqual(TEST_ORIGIN, position);
            Assert.IsNull(supportCollider);
        }

        [Test]
        public void GroundSupportQuery_TryGetSupportedPosition_IgnoresTriggersOnFloorAndAtProbeOrigin()
        {
            // Arrange — Trigger floor alone is rejected; trigger at probe origin over solid floor does not reject
            Vector3 triggerFloorCandidate = TEST_ORIGIN;
            Vector3 triggerOverheadCandidate = TEST_ORIGIN + new Vector3(10f, 0f, 0f);

            CreateBoxCollider("TriggerGround", triggerFloorCandidate + new Vector3(0f, -0.5f, 0f), new Vector3(3f, 1f, 3f), "Ground", isTrigger: true);
            GameObject solidFloor = CreateBoxCollider("SolidGround", triggerOverheadCandidate + new Vector3(0f, -0.5f, 0f), new Vector3(3f, 1f, 3f), "Ground", isTrigger: false);
            CreateBoxCollider(
                "TriggerAtOrigin",
                triggerOverheadCandidate + new Vector3(0f, GridConstants.GROUND_PROBE_ORIGIN_HEIGHT, 0f),
                new Vector3(1f, 1f, 1f),
                "Impassable",
                isTrigger: true);

            // Act & Assert
            Assert.IsFalse(GroundSupportQuery.TryGetSupportedPosition(triggerFloorCandidate, out _, out _));

            bool supportedWithTriggerAtOrigin = GroundSupportQuery.TryGetSupportedPosition(
                triggerOverheadCandidate,
                out Vector3 supportedPos,
                out Collider supportCollider);

            Assert.IsTrue(supportedWithTriggerAtOrigin);
            Assert.AreEqual(TEST_ORIGIN.y + GridConstants.ROOT_GROUND_CLEARANCE, supportedPos.y, TOLERANCE);
            Assert.AreSame(solidFloor.GetComponent<Collider>(), supportCollider);
        }

        [Test]
        public void GroundSupportQuery_TryGetSupportedPosition_NearlyVerticalSurfaceNormalBelowThreshold_ReturnsFalse()
        {
            // Arrange — Box (4 x 0.1 x 2) rotated +89.5 deg around X at offset (0, -0.5, -0.055):
            // -Z end cap stays strictly at z < -0.013, while tilted +Y face crosses z = 0 around y = -1.07
            // with normal.y = cos(89.5 deg) ~= 0.00873 <= 0.01 and box max y < 0.51 (below probe origin y + 1.5).
            GameObject steepSlope = CreateBoxCollider(
                "SteepWalkableSlope",
                TEST_ORIGIN + new Vector3(0f, -0.5f, -0.055f),
                new Vector3(4f, 0.1f, 2f),
                "Ground");
            steepSlope.transform.rotation = Quaternion.Euler(89.5f, 0f, 0f);
            Physics.SyncTransforms();

            // Act
            bool supported = GroundSupportQuery.TryGetSupportedPosition(TEST_ORIGIN, out _, out _);

            // Assert
            Assert.IsFalse(supported);
        }

        [TestCase(0f, 100f, 0f, true, TestName = "IsWithinWorldBounds_ExactLowerInclusiveZero_ReturnsTrue")]
        [TestCase(5.999f, -20f, 3.999f, true, TestName = "IsWithinWorldBounds_JustBelowUpperBounds_ReturnsTrue")]
        [TestCase(6f, 0f, 2f, false, TestName = "IsWithinWorldBounds_ExactUpperExclusiveX_ReturnsFalse")]
        [TestCase(2f, 0f, 4f, false, TestName = "IsWithinWorldBounds_ExactUpperExclusiveZ_ReturnsFalse")]
        [TestCase(-0.001f, 0f, 1f, false, TestName = "IsWithinWorldBounds_NegativeX_ReturnsFalse")]
        [TestCase(1f, 0f, -0.001f, false, TestName = "IsWithinWorldBounds_NegativeZ_ReturnsFalse")]
        public void GroundSupportQuery_IsWithinWorldBounds_EvaluatesHalfOpenXZExtents(
            float x,
            float y,
            float z,
            bool expectedWithin)
        {
            // Arrange — 3x2 grid with cellSize 2 covers [0, 6) x [0, 4)
            Grid grid = new Grid(new GridConfiguration(width: 3, height: 2, cellSize: 2f));

            // Act & Assert
            Assert.AreEqual(expectedWithin, GroundSupportQuery.IsWithinWorldBounds(grid, new Vector3(x, y, z)));
            Assert.IsFalse(GroundSupportQuery.IsWithinWorldBounds(null, Vector3.zero));
        }

        private Camera CreateOrthographicTestCamera(Vector3 position, float orthoSize)
        {
            GameObject cameraGo = new GameObject("OrthoVisibilityTestCamera");
            cameraGo.transform.position = position;
            cameraGo.transform.rotation = Quaternion.identity;
            _createdObjects.Add(cameraGo);

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthoSize;
            camera.aspect = 1f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 20f;
            return camera;
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

        private static GridDirection ResolveDirectionByName(string name)
        {
            if (name == nameof(GridDirection.North)) { return GridDirection.North; }
            if (name == nameof(GridDirection.NorthEast)) { return GridDirection.NorthEast; }
            if (name == nameof(GridDirection.East)) { return GridDirection.East; }
            if (name == nameof(GridDirection.SouthEast)) { return GridDirection.SouthEast; }
            if (name == nameof(GridDirection.South)) { return GridDirection.South; }
            if (name == nameof(GridDirection.SouthWest)) { return GridDirection.SouthWest; }
            if (name == nameof(GridDirection.West)) { return GridDirection.West; }
            if (name == nameof(GridDirection.NorthWest)) { return GridDirection.NorthWest; }
            return GridDirection.None;
        }
    }
}
