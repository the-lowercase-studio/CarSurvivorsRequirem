using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.LayerMasks;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Navigation.FlowFieldSystem
{
    public interface IFlowFieldMovementController
    {
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

        public Vector3 MoveOnFlowFieldGrid(float movementSpeed)
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

            Vector3 movement = movementSpeed * Time.deltaTime * combinedDir;
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

            if (currentCell != null && currentCell.BestDirection != null && currentCell.BestDirection != GridDirection.None)
            {
                Vector2Int gridDirection = currentCell.BestDirection.Vector;
                if (gridDirection != Vector2Int.zero)
                {
                    return new Vector3(gridDirection.x, 0, gridDirection.y).normalized;
                }
            }

            // Fallback: When inside destination cell, outside chunk, or on unintegrated cell, direct toward destination
            if (_gridManager.DestinationCell != null)
            {
                Vector3 toDestination = _gridManager.DestinationCell.WorldPos - transform.position;
                toDestination.y = 0f;

                if (toDestination.sqrMagnitude > 0.0001f)
                {
                    return toDestination.normalized;
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
