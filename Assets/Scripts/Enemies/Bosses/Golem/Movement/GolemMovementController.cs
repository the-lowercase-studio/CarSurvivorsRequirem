using Assets.Scripts.LayerMasks;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.Movement
{
    public interface IGolemMovementController
    {
        bool CanMove { get; set; }
        void MoveTowards(Vector3 targetPosition, float moveSpeed, float rotationSpeed);
        void Stop();
        void SetPosition(Vector3 position);
        void SetKinematic(bool isKinematic);
    }

    [RequireComponent(typeof(Rigidbody))]
    public class GolemMovementController : MonoBehaviour, IGolemMovementController
    {
        [SerializeField] private float _obstacleCheckRadius = 1.2f;
        [SerializeField] private float _obstacleRayDistance = 1.5f;

        private Rigidbody _rigidbody;
        private Vector3 _desiredVelocity;
        private Quaternion _desiredRotation;
        private bool _hasMovementTarget;

        public bool CanMove { get; set; } = true;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.freezeRotation = true;
            _desiredRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            if (_rigidbody.isKinematic)
            {
                return;
            }

            if (!CanMove || !_hasMovementTarget)
            {
                _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);
                return;
            }

            _rigidbody.linearVelocity = new Vector3(_desiredVelocity.x, _rigidbody.linearVelocity.y, _desiredVelocity.z);
            _rigidbody.MoveRotation(Quaternion.RotateTowards(_rigidbody.rotation, _desiredRotation, 360f * Time.fixedDeltaTime));
        }

        public void MoveTowards(Vector3 targetPosition, float moveSpeed, float rotationSpeed)
        {
            if (!CanMove)
            {
                _hasMovementTarget = false;
                return;
            }

            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f)
            {
                _desiredVelocity = Vector3.zero;
                _hasMovementTarget = false;
                return;
            }

            _hasMovementTarget = true;
            Vector3 normalizedDir = direction.normalized;

            Vector3 slideDirection = CalculateSlideDirection(normalizedDir);
            _desiredVelocity = slideDirection * moveSpeed;

            if (slideDirection.sqrMagnitude > 0.01f)
            {
                _desiredRotation = Quaternion.LookRotation(slideDirection, Vector3.up);
            }
        }

        public void Stop()
        {
            _hasMovementTarget = false;
            _desiredVelocity = Vector3.zero;
            if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);
            }
        }

        public void SetPosition(Vector3 position)
        {
            transform.position = position;
            if (_rigidbody != null)
            {
                _rigidbody.position = position;
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
            }
        }

        public void SetKinematic(bool isKinematic)
        {
            if (_rigidbody != null)
            {
                if (isKinematic && !_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }

                _rigidbody.isKinematic = isKinematic;
            }
        }

        private Vector3 CalculateSlideDirection(Vector3 desiredDirection)
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;

            if (Physics.SphereCast(origin, _obstacleCheckRadius, desiredDirection, out RaycastHit hit, _obstacleRayDistance, TerrainLayers.Impassable))
            {
                Vector3 normal = hit.normal;
                normal.y = 0f;
                normal.Normalize();

                Vector3 projected = Vector3.ProjectOnPlane(desiredDirection, normal).normalized;
                if (projected.sqrMagnitude > 0.01f)
                {
                    return projected;
                }
            }

            return desiredDirection;
        }
    }
}
