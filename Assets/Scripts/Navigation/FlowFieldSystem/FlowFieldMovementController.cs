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
            Vector3 moveDir = (gridDir + _separationVector).normalized;
            Vector3 movement = movementSpeed * Time.deltaTime * moveDir;
            transform.position += movement;

            return movement;
        }

        private Vector3 GetMoveDirectionBasedOnCurrentCell()
        {
            Cell currentCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(
                _gridManager.WorldGrid, transform.position
            );

            if (currentCell != null && currentCell.BestDirection != null)
            {
                Vector2Int gridDirection = currentCell.BestDirection.Vector;
                return new Vector3(gridDirection.x, 0, gridDirection.y);
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
