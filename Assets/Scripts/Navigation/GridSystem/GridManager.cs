using UnityEngine;
using Assets.Scripts.Player;
using Assets.Scripts.Navigation.FlowFieldSystem;
using Assets.Scripts.Navigation.Constants;
using Reflex.Attributes;

namespace Assets.Scripts.Navigation.GridSystem
{
    public interface IGridManager
    {
        Cell DestinationCell { get; }
        Grid GridPlayerChunk { get; }
        Grid WorldGrid { get; }
    }

    public class GridManager : MonoBehaviour, IGridManager
    {
        [Inject] private readonly IPlayerManager _playerManager = null;

        [SerializeField] private GridConfiguration _worldGridConfiguration;
        [SerializeField] private float _delayBetweenWorldGridUpdate = 0.2f;
        [SerializeField] private GridConfiguration _playerGridConfiguration;
        [SerializeField] private float _delayBetweenPlayerChunkGridUpdate = 0.32f;

        [Header("Target Prediction")]
        [SerializeField] private float _flowFieldTargetPredictionTime = 0.25f;
        [SerializeField] private float _maxFlowFieldTargetOffset = 6f;

#if DEBUG
        [SerializeField] private bool _debugGrid;
        [SerializeField] private bool _debugFlowField;

        [SerializeField][ColorUsage(false)] private Color _worldCellBorderColor = Color.blue;
        [SerializeField][ColorUsage(false)] private Color _playerChunkCellBorderColor = Color.green;
        [SerializeField][ColorUsage(false)] private Color _blockedCellBorderDrawColor = Color.red;

        [SerializeField] private FlowFieldDebugConfiguration _flowFieldDebugConfiguration;

#endif

        private FlowField _flowField;
        private Cell[,] _playerChunkCells;

        public Grid WorldGrid { get; private set; }
        public Grid GridPlayerChunk { get; private set; }
        public Cell DestinationCell { get; private set; }

        private void Awake()
        {
            WorldGrid = new Grid(_worldGridConfiguration);
            _playerChunkCells = new Cell[_playerGridConfiguration.Width, _playerGridConfiguration.Height];
            GridPlayerChunk = new Grid(_playerGridConfiguration, _playerChunkCells);
            _flowField = new FlowField();

#if DEBUG
            _flowFieldDebugConfiguration.Grid = WorldGrid;
#endif
        }

        private void OnEnable()
        {
            if (WorldGrid != null && WorldGrid.Cells != null && WorldGrid.Width > 0 && WorldGrid.Height > 0)
            {
                Cell initialCenterCell = WorldGrid.Cells[WorldGrid.Width / 2, WorldGrid.Height / 2];
                UpdateFlowField(WorldGrid, initialCenterCell.WorldPos);
            }
            else
            {
                Debug.LogWarning("[GridManager] WorldGrid configuration is uninitialized or invalid.", this);
            }

            InvokeRepeating(nameof(UpdateFlowFieldWithNewPlayerChunkGrid), 0, _delayBetweenPlayerChunkGridUpdate);
        }

#if DEBUG
        private void Start()
        {
            if (_debugGrid)
            {
                InvokeRepeating(nameof(DebugWorldGrid), 0, _delayBetweenWorldGridUpdate + GridConstants.DRAW_TIME_OFFSET);
                InvokeRepeating(nameof(DebugPlayerChunkGrid), 0, _delayBetweenPlayerChunkGridUpdate + GridConstants.DRAW_TIME_OFFSET);
            }
        }
#endif

        private void UpdateFlowFieldWithNewPlayerChunkGrid()
        {
            UpdatePlayerChunkBasedOnPlayerPositionInWorldGrid();

            Vector3 playerPosition = _playerManager.GameObject.transform.position;
            Vector3 velocity = _playerManager.CarController.GetMovementVelocity();
            float speed = velocity.magnitude;

            Vector3 destination = playerPosition;
            if (speed > 0.1f)
            {
                float offsetDistance = Mathf.Clamp(speed * _flowFieldTargetPredictionTime, 0f, _maxFlowFieldTargetOffset);
                destination += velocity.normalized * offsetDistance;
            }

            UpdateFlowField(GridPlayerChunk, destination);
        }

        private void UpdatePlayerChunkBasedOnPlayerPositionInWorldGrid()
        {
            int chunkWidth = _playerGridConfiguration.Width;
            int chunkHeight = _playerGridConfiguration.Height;

            Cell cellClosestToPlayer = WorldPosToCellConverter.GetCellFromGridByWorldPos(
                WorldGrid,
                _playerManager.GameObject.transform.position
            );

            if (cellClosestToPlayer == null)
            {
                return;
            }

            int halfWidth = chunkWidth >> 1;
            int halfHeight = chunkHeight >> 1;

            int minGridX = Mathf.Clamp(cellClosestToPlayer.WorldGridPos.x - halfWidth, 0, Mathf.Max(0, WorldGrid.Width - chunkWidth));
            int minGridY = Mathf.Clamp(cellClosestToPlayer.WorldGridPos.y - halfHeight, 0, Mathf.Max(0, WorldGrid.Height - chunkHeight));

            ClearDepartingChunkCells(minGridX, minGridY, chunkWidth, chunkHeight);

            for (int chunkX = 0; chunkX < chunkWidth; chunkX++)
            {
                int worldX = minGridX + chunkX;
                bool isWorldXValid = worldX >= 0 && worldX < WorldGrid.Width;

                for (int chunkY = 0; chunkY < chunkHeight; chunkY++)
                {
                    int worldY = minGridY + chunkY;
                    bool isWorldYValid = worldY >= 0 && worldY < WorldGrid.Height;

                    if (isWorldXValid && isWorldYValid)
                    {
                        Cell cell = WorldGrid.Cells[worldX, worldY];
                        _playerChunkCells[chunkX, chunkY] = cell;
                        if (cell != null)
                        {
                            cell.ChunkGridPos = new Vector2Int(chunkX, chunkY);
                        }
                    }
                    else
                    {
                        _playerChunkCells[chunkX, chunkY] = null;
                    }
                }
            }
        }

        private void ClearDepartingChunkCells(int newMinGridX, int newMinGridY, int chunkWidth, int chunkHeight)
        {
            int width = _playerChunkCells.GetLength(0);
            int height = _playerChunkCells.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Cell oldCell = _playerChunkCells[x, y];
                    if (oldCell != null)
                    {
                        int newChunkX = oldCell.WorldGridPos.x - newMinGridX;
                        int newChunkY = oldCell.WorldGridPos.y - newMinGridY;

                        if (newChunkX < 0 || newChunkX >= chunkWidth || newChunkY < 0 || newChunkY >= chunkHeight)
                        {
                            oldCell.ChunkGridPos = Assets.Scripts.Navigation.Constants.GridConstants.INVALID_CHUNK_GRID_POS;
                            oldCell.BestDirection = GridDirection.None;
                            _playerChunkCells[x, y] = null;
                        }
                    }
                }
            }
        }

        private Cell GetSupportedDestination(Grid grid, Vector3 destination)
        {
            if (GroundSupportQuery.IsWithinWorldBounds(WorldGrid, destination))
            {
                Cell predicted = WorldPosToCellConverter.GetCellFromGridByWorldPos(WorldGrid, destination);
                if ((grid == WorldGrid || IsCellInChunk(predicted))
                    && predicted.Cost < FlowFieldConstants.IMPASSABLE_COST)
                {
                    return predicted;
                }
            }

            Cell nearest = null;
            float bestDistance = float.MaxValue;
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Cell cell = grid.Cells[x, y];
                    if (cell == null || cell.Cost >= FlowFieldConstants.IMPASSABLE_COST)
                    {
                        continue;
                    }
                    float dx = cell.WorldPos.x - destination.x;
                    float dz = cell.WorldPos.z - destination.z;
                    float distance = dx * dx + dz * dz;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        nearest = cell;
                    }
                }
            }
            return nearest;
        }

        private bool IsCellInChunk(Cell cell)
        {
            if (cell == null || GridPlayerChunk == null || GridPlayerChunk.Cells == null)
            {
                return false;
            }

            Vector2Int chunkPos = cell.ChunkGridPos;
            return chunkPos.x >= 0
                && chunkPos.x < GridPlayerChunk.Width
                && chunkPos.y >= 0
                && chunkPos.y < GridPlayerChunk.Height
                && GridPlayerChunk.Cells[chunkPos.x, chunkPos.y] == cell;
        }

        private void UpdateFlowField(Grid gridPerformingUpdate, Vector3 destination)
        {
            _flowField.CreateCostField(gridPerformingUpdate);
            DestinationCell = GetSupportedDestination(gridPerformingUpdate, destination);
            _flowField.CreateIntegrationField(gridPerformingUpdate, DestinationCell);
            _flowField.CreateFlowField(gridPerformingUpdate);
        }

        private void OnDisable()
        {
            CancelInvoke();
        }

#if DEBUG
        private void DebugPlayerChunkGrid()
        {
            GridDebug.DisplayGrid(
                GridPlayerChunk,
                _playerChunkCellBorderColor,
                _blockedCellBorderDrawColor,
                GridConstants.PLAYER_CHUNK_DRAW_Y_OFFSET,
                _delayBetweenPlayerChunkGridUpdate);
        }

        private void DebugWorldGrid()
        {
            if (_debugGrid)
            {
                GridDebug.DisplayGrid(
                    WorldGrid,
                    _worldCellBorderColor,
                    _blockedCellBorderDrawColor,
                    0,
                    _delayBetweenWorldGridUpdate);
            }

            if (_debugFlowField)
            {
                FlowFieldDebug.DisplayFlowFieldDebugTextOnGrid(_flowFieldDebugConfiguration);
            }
        }
#endif
    }
}
