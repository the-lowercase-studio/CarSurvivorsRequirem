using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.LayerMasks;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Navigation.FlowFieldSystem
{
    public interface IFlowFieldMovementController
    {
        Vector3 CalculateDesiredMovementDirection();
        Vector3 GetFlowDirection();
        bool IsValidVoluntaryStep(Vector3 from, Vector3 to);
        void ResetAfterRelocation();
        Vector3 MoveOnFlowFieldGrid(float movementSpeed);
    }

    public class FlowFieldMovementController : MonoBehaviour, IFlowFieldMovementController
    {
        [Inject] private readonly IGridManager _gridManager = null;

        [Header("Separating moving entities")]
        [SerializeField] private float _separationRadius = 1.2f;
        [SerializeField] private float _separationStrength = 0.5f;

        private readonly Collider[] _separationColliderBuffer = new Collider[FlowFieldConstants.SEPARATION_COLLIDER_BUFFER_SIZE];
        private Vector3 _separationVector;

        private Collider _selfCollider;

        private void Awake()
        {
            _selfCollider = GetComponent<Collider>();
        }

        private void FixedUpdate()
        {
            PreventEntitiesFromStackingOnEachOther();
        }

        public Vector3 CalculateDesiredMovementDirection()
        {
            Vector3 gridDir = GetMoveDirectionBasedOnCurrentCell();
            Vector3 combinedDir;

            if (gridDir != Vector3.zero)
            {
                combinedDir = (gridDir + _separationVector).normalized;
            }
            else if (_separationVector != Vector3.zero)
            {
                // When entity is directly on target or no direction exists, dampen separation to avoid jitter
                combinedDir = _separationVector * 0.1f;
            }
            else
            {
                combinedDir = Vector3.zero;
            }

            return combinedDir;
        }

        public Vector3 MoveOnFlowFieldGrid(float movementSpeed)
        {
            Vector3 combinedDir = CalculateDesiredMovementDirection();
            Vector3 movement = movementSpeed * Time.fixedDeltaTime * combinedDir;
            transform.position += movement;

            return movement;
        }

        public void ResetAfterRelocation()
        {
            _separationVector = Vector3.zero;
        }

        public Vector3 GetFlowDirection()
        {
            return GetMoveDirectionBasedOnCurrentCell();
        }

        public bool IsValidVoluntaryStep(Vector3 from, Vector3 to)
        {
            GridSystem.Grid grid = _gridManager.WorldGrid;
            if (!GroundSupportQuery.IsWithinWorldBounds(grid, from)
                || !GroundSupportQuery.IsWithinWorldBounds(grid, to))
            {
                return false;
            }
            Cell previous = WorldPosToCellConverter.GetCellFromGridByWorldPos(grid, from);
            Vector3 displacement = to - from;
            displacement.y = 0f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / (grid.CellSize * 0.25f)));
            if (steps > FlowFieldConstants.MAX_LOCAL_TRAVERSAL_STEPS)
            {
                return false;
            }
            for (int step = 1; step <= steps; step++)
            {
                Cell next = WorldPosToCellConverter.GetCellFromGridByWorldPos(grid,
                    Vector3.Lerp(from, to, (float)step / steps));
                if (!FlowField.CanTraverse(grid, previous, next))
                {
                    return false;
                }
                previous = next;
            }
            return true;
        }

        private Vector3 GetMoveDirectionBasedOnCurrentCell()
        {
            if (_gridManager.WorldGrid == null || _gridManager.DestinationCell == null
                || !GroundSupportQuery.IsWithinWorldBounds(_gridManager.WorldGrid, transform.position))
            {
                return Vector3.zero;
            }

            Cell currentCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(
                _gridManager.WorldGrid, transform.position
            );

            if (currentCell != null && currentCell == _gridManager.DestinationCell)
            {
                Vector3 toDestination = _gridManager.DestinationCell.WorldPos - transform.position;
                toDestination.y = 0f;
                if (toDestination.sqrMagnitude > FlowFieldConstants.DESTINATION_ARRIVAL_DISTANCE_SQR)
                {
                    return toDestination.normalized;
                }

                return Vector3.zero;
            }

            if (currentCell != null && currentCell.Cost < FlowFieldConstants.IMPASSABLE_COST
                && currentCell.BestDirection != null && currentCell.BestDirection != GridDirection.None)
            {
                Vector2Int gridDirection = currentCell.BestDirection.Vector;
                if (gridDirection != Vector2Int.zero)
                {
                    return new Vector3(gridDirection.x, 0, gridDirection.y).normalized;
                }
            }

            Vector3 borderNeighborDirection = TryGetBorderNeighborDirection(currentCell);
            if (borderNeighborDirection != Vector3.zero)
            {
                return borderNeighborDirection;
            }

            // Outside the integrated chunk, take only one traversable local step toward the target.
            Cell nearest = null;
            float bestDistance = float.MaxValue;
            if (currentCell != null)
            {
                for (int i = 0; i < GridDirection.CardinalAndIntercardinalDirections.Count; i++)
                {
                    Vector2Int index = currentCell.WorldGridPos + GridDirection.CardinalAndIntercardinalDirections[i].Vector;
                    if (index.x < 0 || index.y < 0 || index.x >= _gridManager.WorldGrid.Width
                        || index.y >= _gridManager.WorldGrid.Height)
                    {
                        continue;
                    }
                    Cell candidate = _gridManager.WorldGrid.Cells[index.x, index.y];
                    if (!FlowField.CanTraverse(_gridManager.WorldGrid, currentCell, candidate))
                    {
                        continue;
                    }
                    Vector3 offset = candidate.WorldPos - _gridManager.DestinationCell.WorldPos;
                    offset.y = 0f;
                    if (offset.sqrMagnitude < bestDistance)
                    {
                        bestDistance = offset.sqrMagnitude;
                        nearest = candidate;
                    }
                }
            }
            if (nearest != null)
            {
                Vector3 direction = nearest.WorldPos - transform.position;
                direction.y = 0f;
                return direction.normalized;
            }
            return Vector3.zero;
        }

        private Vector3 TryGetBorderNeighborDirection(Cell currentCell)
        {
            if (currentCell == null || _gridManager.WorldGrid == null || _gridManager.WorldGrid.Cells == null)
            {
                return Vector3.zero;
            }

            Vector2Int currentGridPos = currentCell.WorldGridPos;
            Cell bestNeighbor = null;
            ushort bestCost = ushort.MaxValue;

            for (int i = 0; i < GridDirection.CardinalAndIntercardinalDirections.Count; i++)
            {
                Vector2Int neighborPos = currentGridPos + GridDirection.CardinalAndIntercardinalDirections[i].Vector;
                if (neighborPos.x >= 0 && neighborPos.x < _gridManager.WorldGrid.Width &&
                    neighborPos.y >= 0 && neighborPos.y < _gridManager.WorldGrid.Height)
                {
                    Cell neighbor = _gridManager.WorldGrid.Cells[neighborPos.x, neighborPos.y];
                    if (neighbor != null
                        && FlowField.CanTraverse(_gridManager.WorldGrid, currentCell, neighbor)
                        && neighbor.BestCost < bestCost)
                    {
                        bestCost = neighbor.BestCost;
                        bestNeighbor = neighbor;
                    }
                }
            }

            if (bestNeighbor != null)
            {
                Vector3 direction = bestNeighbor.WorldPos - transform.position;
                direction.y = 0f;
                return direction.normalized;
            }

            return Vector3.zero;
        }

        private void PreventEntitiesFromStackingOnEachOther()
        {
            Vector3 separation = Vector3.zero;
            int neighborCount = 0;

            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                _separationRadius,
                _separationColliderBuffer,
                EntityLayers.Enemies);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hit = _separationColliderBuffer[hitIndex];
                if (hit == _selfCollider)
                {
                    continue;
                }

                Vector3 away = transform.position - hit.transform.position;
                away.y = 0f;

                float distance = away.magnitude;
                if (distance > 0)
                {
                    separation += away.normalized / distance;
                    neighborCount++;
                }
            }

            if (neighborCount > 0)
            {
                separation /= neighborCount;
                separation = separation.normalized * _separationStrength;
            }
            else
            {
                separation = Vector3.zero;
            }

            separation.y = 0f;

            _separationVector = separation;
        }
    }
}
