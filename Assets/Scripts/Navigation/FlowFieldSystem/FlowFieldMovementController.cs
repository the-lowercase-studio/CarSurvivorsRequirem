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
        [SerializeField] private float separationRadius = 1.2f;
        [SerializeField] private float separationStrength = 0.5f;
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

            Collider[] hits = Physics.OverlapSphere(transform.position, separationRadius, EntityLayers.Enemy);
            foreach (var hit in hits)
            {
                if (hit == _selfCollider) continue;

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
                separation = separation.normalized * separationStrength;
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
