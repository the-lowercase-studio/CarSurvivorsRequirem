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
        public Grid WorldGrid { get; private set; }

        [SerializeField] private GridConfiguration _playerGridConfiguration;
        [SerializeField] private float _delayBetweenPlayerChunkGridUpdate = 0.32f;
        public Grid GridPlayerChunk { get; private set; }

#if DEBUG
        [SerializeField] private bool _debugGrid;
        [SerializeField] private bool _debugFlowField;

        [SerializeField][ColorUsage(false)] private Color _worldCellBorderColor = Color.blue;
        [SerializeField][ColorUsage(false)] private Color _playerChunkCellBorderColor = Color.green;
        [SerializeField][ColorUsage(false)] private Color _blockedCellBorderDrawColor = Color.red;

        [SerializeField] private FlowFieldDebugConfiguration _flowFieldDebugConfiguration;

        private float _playerChunkDrawYOffset = 0.2f;
#endif
        private FlowField _flowField;

        public Cell DestinationCell { get; private set; }

        private void Awake()
        {
            WorldGrid = new Grid(_worldGridConfiguration);
            _flowField = new FlowField();

#if DEBUG
            _flowFieldDebugConfiguration.Grid = WorldGrid;
#endif
        }

        private void OnEnable()
        {
            UpdateFlowField(WorldGrid, WorldGrid.Cells[WorldGrid.Width / 2, WorldGrid.Height / 2].WorldPos);

            InvokeRepeating(nameof(UpdateFlowFieldWithNewPlayerChunkGrid), 0, _delayBetweenPlayerChunkGridUpdate);
        }

#if DEBUG

        private void Start()
        {
            if (_debugGrid)
            {
                float drawTimeOffset = 0.02f;
                InvokeRepeating(nameof(DebugWorldGrid), 0, _delayBetweenWorldGridUpdate + drawTimeOffset);
                InvokeRepeating(nameof(DebugPlayerChunkGrid), 0, _delayBetweenPlayerChunkGridUpdate + drawTimeOffset);
            }
        }

        private void DebugPlayerChunkGrid()
        {
            GridDebug.DisplayGrid(GridPlayerChunk,
                                  _playerChunkCellBorderColor,
                                  _blockedCellBorderDrawColor,
                                  _playerChunkDrawYOffset,
                                  _delayBetweenPlayerChunkGridUpdate);
        }

        private void DebugWorldGrid()
        {
            if (_debugGrid)
            {
                GridDebug.DisplayGrid(WorldGrid, _worldCellBorderColor, _blockedCellBorderDrawColor, 0, _delayBetweenWorldGridUpdate);
            }
            if (_debugFlowField)
            {
                FlowFieldDebug.DisplayFlowFieldDebugTextOnGrid(_flowFieldDebugConfiguration);
            }
        }

#endif

        private void UpdateFlowFieldWithNewPlayerChunkGrid()
        {
            GridPlayerChunk = CreatePlayerChunkBasedOnPlayerPositionInWorldGrid();
            UpdateFlowField(GridPlayerChunk, _playerManager.GameObject.transform.position);
        }

        private Grid CreatePlayerChunkBasedOnPlayerPositionInWorldGrid()
        {
            int chunkWidth = _playerGridConfiguration.Width;
            int chunkHeight = _playerGridConfiguration.Height;
            Cell[,] chunkCells = new Cell[chunkWidth, chunkHeight];

            Cell cellClosestToPlayer = WorldPosToCellConverter.GetCellFromGridByWorldPos(
                WorldGrid,
                _playerManager.GameObject.transform.position
            );

            int halfWidth = chunkWidth >> 1;
            int maxGridX = cellClosestToPlayer.WorldGridPos.x + halfWidth;
            int minGridX = cellClosestToPlayer.WorldGridPos.x - halfWidth;

            int halfHeight = chunkHeight >> 1;
            int maxGridY = cellClosestToPlayer.WorldGridPos.y + halfHeight;
            int minGridY = cellClosestToPlayer.WorldGridPos.y - halfHeight;

            int x = minGridX;
            int chunkX = 0;
            while (x <= maxGridX && x < WorldGrid.Cells.GetLength(0))
            {
                if (x >= 0 && chunkX < chunkCells.GetLength(0))
                {
                    int y = minGridY;
                    int chunkY = 0;
                    while (y <= maxGridY && y < WorldGrid.Cells.GetLength(1))
                    {
                        if (y >= 0 && chunkY < chunkCells.GetLength(1))
                        {
                            chunkCells[chunkX, chunkY] = WorldGrid.Cells[x, y];
                            chunkCells[chunkX, chunkY].ChunkGridPos = new Vector2Int(chunkX, chunkY);
                            chunkY++;
                        }
                        y++;
                    }

                    chunkX++;
                }
                x++;
            }

            return new Grid(_playerGridConfiguration, chunkCells);
        }

        private void UpdateFlowField(Grid gridPerformingUpdate, Vector3 destination)
        {
            _flowField.CreateCostField(gridPerformingUpdate);

            DestinationCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(
                WorldGrid,
                destination
            );

            _flowField.CreateIntegrationField(gridPerformingUpdate, DestinationCell);
            _flowField.CreateFlowField(gridPerformingUpdate);
        }
    }
}
