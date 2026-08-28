using UnityEngine;
using Assets.Scripts.Player;
using Assets.Scripts.Navigation.FlowFieldSystem;
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
        [Inject] private readonly IPlayerManager _playerManager;

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

        private const float PLAYER_CHUNK_DRAW_Y_OFFSET = 0.2f;
        private const float DRAW_TIME_OFFSET = 0.02f;
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
                UpdateFlowField(WorldGrid, initialCenterCell);
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
                InvokeRepeating(nameof(DebugWorldGrid), 0, _delayBetweenWorldGridUpdate + DRAW_TIME_OFFSET);
                InvokeRepeating(nameof(DebugPlayerChunkGrid), 0, _delayBetweenPlayerChunkGridUpdate + DRAW_TIME_OFFSET);
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

            Cell destinationCell = GetClampedChunkDestinationCell(destination, playerPosition);
            UpdateFlowField(GridPlayerChunk, destinationCell);
        }

        private void UpdatePlayerChunkBasedOnPlayerPositionInWorldGrid()
        {
            int chunkWidth = _playerGridConfiguration.Width;
            int chunkHeight = _playerGridConfiguration.Height;
            ClearPlayerChunkCells();

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

            for (int chunkX = 0; chunkX < chunkWidth; chunkX++)
            {
                int worldX = minGridX + chunkX;
                if (worldX < 0 || worldX >= WorldGrid.Width)
                {
                    continue;
                }

                for (int chunkY = 0; chunkY < chunkHeight; chunkY++)
                {
                    int worldY = minGridY + chunkY;
                    if (worldY < 0 || worldY >= WorldGrid.Height)
                    {
                        continue;
                    }

                    Cell cell = WorldGrid.Cells[worldX, worldY];
                    if (cell != null)
                    {
                        _playerChunkCells[chunkX, chunkY] = cell;
                        cell.ChunkGridPos = new Vector2Int(chunkX, chunkY);
                    }
                }
            }
        }

        private void ClearPlayerChunkCells()
        {
            int width = _playerChunkCells.GetLength(0);
            int height = _playerChunkCells.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Cell cell = _playerChunkCells[x, y];
                    if (cell != null)
                    {
                        cell.ChunkGridPos = Assets.Scripts.Navigation.Constants.GridConstants.INVALID_CHUNK_GRID_POS;
                        cell.BestDirection = GridDirection.None;
                        _playerChunkCells[x, y] = null;
                    }
                }
            }
        }

        private Cell GetClampedChunkDestinationCell(Vector3 destination, Vector3 fallbackPosition)
        {
            Cell predictedWorldCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(WorldGrid, destination);
            if (IsCellInChunk(predictedWorldCell))
            {
                return predictedWorldCell;
            }

            // If predicted destination falls outside chunk, clamp world coords to chunk boundary
            Cell chunkOrigin = GridPlayerChunk.Cells[0, 0];
            if (chunkOrigin != null && predictedWorldCell != null)
            {
                int minChunkWorldX = chunkOrigin.WorldGridPos.x;
                int minChunkWorldY = chunkOrigin.WorldGridPos.y;

                int clampedChunkX = Mathf.Clamp(predictedWorldCell.WorldGridPos.x - minChunkWorldX, 0, GridPlayerChunk.Width - 1);
                int clampedChunkY = Mathf.Clamp(predictedWorldCell.WorldGridPos.y - minChunkWorldY, 0, GridPlayerChunk.Height - 1);

                Cell clampedCell = GridPlayerChunk.Cells[clampedChunkX, clampedChunkY];
                if (clampedCell != null)
                {
                    return clampedCell;
                }
            }

            // Fallback to player's current cell
            Cell playerWorldCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(WorldGrid, fallbackPosition);
            if (IsCellInChunk(playerWorldCell))
            {
                return playerWorldCell;
            }

            // Final fallback: center cell of the player chunk
            return GridPlayerChunk.Cells[GridPlayerChunk.Width / 2, GridPlayerChunk.Height / 2];
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

        private void UpdateFlowField(Grid gridPerformingUpdate, Cell destinationCell)
        {
            if (destinationCell == null)
            {
                return;
            }

            DestinationCell = destinationCell;
            _flowField.CreateCostField(gridPerformingUpdate);
            _flowField.CreateIntegrationField(gridPerformingUpdate, DestinationCell);
            _flowField.CreateFlowField(gridPerformingUpdate);
        }

#if DEBUG
        private void DebugPlayerChunkGrid()
        {
            GridDebug.DisplayGrid(
                GridPlayerChunk,
                _playerChunkCellBorderColor,
                _blockedCellBorderDrawColor,
                PLAYER_CHUNK_DRAW_Y_OFFSET,
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
