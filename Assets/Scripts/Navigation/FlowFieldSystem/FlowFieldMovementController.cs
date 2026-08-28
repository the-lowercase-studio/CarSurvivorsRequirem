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
        Vector3 MoveOnFlowFieldGrid(float movementSpeed);
    }

    public class FlowFieldMovementController : MonoBehaviour, IFlowFieldMovementController
    {
        [Inject] private readonly IGridManager _gridManager;

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

        private Vector3 GetMoveDirectionBasedOnCurrentCell()
        {
            if (_gridManager == null || _gridManager.WorldGrid == null)
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

            if (currentCell != null && currentCell.BestDirection != null && currentCell.BestDirection != GridDirection.None)
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

            // Fallback: When far outside chunk or on unintegrated cell, direct toward destination if beyond arrival distance
            if (_gridManager.DestinationCell != null)
            {
                Vector3 toDestination = _gridManager.DestinationCell.WorldPos - transform.position;
                toDestination.y = 0f;
                float sqrDist = toDestination.sqrMagnitude;

                if (sqrDist > FlowFieldConstants.DESTINATION_ARRIVAL_DISTANCE_SQR)
                {
                    return toDestination.normalized;
                }

                return Vector3.zero;
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
                        && neighbor.Cost < FlowFieldConstants.IMPASSABLE_COST
                        && neighbor.BestDirection != null
                        && neighbor.BestDirection != GridDirection.None
                        && neighbor.BestCost < bestCost)
                    {
                        bestCost = neighbor.BestCost;
                        bestNeighbor = neighbor;
                    }
                }
            }

            if (bestNeighbor != null && bestNeighbor.BestDirection != null && bestNeighbor.BestDirection != GridDirection.None)
            {
                Vector2Int dir = bestNeighbor.BestDirection.Vector;
                if (dir != Vector2Int.zero)
                {
                    return new Vector3(dir.x, 0, dir.y).normalized;
                }
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
