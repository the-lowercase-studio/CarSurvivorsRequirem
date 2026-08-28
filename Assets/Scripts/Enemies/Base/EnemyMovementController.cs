using Assets.Scripts.Enemies.Constants;
using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.FlowFieldSystem;
using Assets.Scripts.LayerMasks;
using DG.Tweening;
using UnityEngine;

namespace Assets.Scripts.Enemies.Base
{
    [RequireComponent(typeof(Enemy), typeof(FlowFieldMovementController))]
    public class EnemyMovementController : MonoBehaviour, IMovementController
    {
        private readonly Vector3 _obstacleCheckOffset = new(0, 0.5f, 0);

        private Enemy _enemy;
        private IFlowFieldMovementController _flowFieldMovementController;

        private float _lastGroundedY;
        private float _verticalVelocity;
        private bool _isStunnable = false;

        private bool _isMovingToPositionUnrelatedToGrid;
        private Vector3 _currentMovementPositionUnrelatedToGrid;
        private Vector3 _lastPos;
        private Vector3 _currentVelocity;

        private Tween _movementUnrelatedToSpeedTween;

        private float _movementDelayAfterAttack = 0.2f;
        private float _currentMovementDelayAfterAttack;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _flowFieldMovementController = GetComponent<IFlowFieldMovementController>();
        }

        private void OnEnable()
        {
            _enemy.EnemyAnimator.OnAttackAnimationEnd += EnemyAnimator_OnAttackAnimationEnd;

            _lastGroundedY = transform.position.y;
            _verticalVelocity = 0f;
            _currentVelocity = Vector3.zero;

            _currentMovementDelayAfterAttack = 0;
        }

        private void OnDisable()
        {
            _enemy.EnemyAnimator.OnAttackAnimationEnd -= EnemyAnimator_OnAttackAnimationEnd;

            if (_movementUnrelatedToSpeedTween != null)
            {
                _movementUnrelatedToSpeedTween.Kill();
                _movementUnrelatedToSpeedTween = null;
            }

            _isMovingToPositionUnrelatedToGrid = false;
            _verticalVelocity = 0f;
            _currentVelocity = Vector3.zero;
        }

        private void OnDestroy()
        {
            if (_movementUnrelatedToSpeedTween != null)
            {
                _movementUnrelatedToSpeedTween.Kill();
                _movementUnrelatedToSpeedTween = null;
            }
        }

        private void FixedUpdate()
        {
            MovementHandler();
        }

        private void OnDrawGizmos()
        {
            Vector3 origin = transform.position + (Vector3.up * EnemyMovementConstants.GROUND_CHECK_ORIGIN_Y);
            Vector3 endPos = origin + (Vector3.down * EnemyMovementConstants.GROUND_CHECK_DISTANCE);
            if (IsOnGround())
            {
                Debug.DrawLine(origin, endPos, Color.green);
            }
            else
            {
                Debug.DrawLine(origin, endPos, Color.red);
            }
        }

        public float GetCurrentMovementSpeed()
        {
            if (_isMovingToPositionUnrelatedToGrid)
            {
                float distance = Vector3.Distance(transform.position, _lastPos);
                if (distance < 0.001f)
                {
                    return 0f;
                }

                return distance / Time.fixedDeltaTime;
            }

            if (_currentVelocity.magnitude < FlowFieldConstants.MIN_MOVEMENT_SPEED_THRESHOLD)
            {
                return 0f;
            }

            return _currentVelocity.magnitude;
        }

        public Tween MoveToPositionInTimeIgnoringSpeed(Vector3 pos, float time)
        {
            if (_movementUnrelatedToSpeedTween != null)
            {
                _movementUnrelatedToSpeedTween.Kill();
            }

            _currentVelocity = Vector3.zero;

            Vector3 startPos = transform.position;
            Vector3 direction = pos - startPos;
            float distance = direction.magnitude;
            float adjustedTime = time;

            if (distance > 0.001f)
            {
                direction.Normalize();
                Vector3 origin = startPos + _obstacleCheckOffset;

                if (Physics.SphereCast(origin, EnemyMovementConstants.OBSTACLE_CHECK_RADIUS, direction, out RaycastHit hitInfo, distance, TerrainLayers.Impassable))
                {
                    float safeDistance = Mathf.Max(0f, hitInfo.distance - EnemyMovementConstants.OBSTACLE_SAFETY_BUFFER);
                    adjustedTime = time * (safeDistance / distance);
                    pos = startPos + (direction * safeDistance);
                }
            }

            _isMovingToPositionUnrelatedToGrid = true;

            return _movementUnrelatedToSpeedTween = transform
                .DOMove(pos, adjustedTime)
                .SetEase(Ease.OutSine)
                .OnComplete(() =>
                {
                    _isMovingToPositionUnrelatedToGrid = false;
                    _currentVelocity = Vector3.zero;
                    _movementUnrelatedToSpeedTween = null;
                });
        }

        public void ResetVerticalVelocity()
        {
            _verticalVelocity = 0f;
        }

        public bool IsOnGround()
        {
            Vector3 origin = transform.position + (Vector3.up * EnemyMovementConstants.GROUND_CHECK_ORIGIN_Y);

            return Physics.Raycast(
                origin,
                Vector3.down,
                out _,
                EnemyMovementConstants.GROUND_CHECK_DISTANCE,
                TerrainLayers.Walkable);
        }

        private bool HandleVerticalPositionAndGrounding()
        {
            Vector3 origin = transform.position + (Vector3.up * EnemyMovementConstants.GROUND_CHECK_ORIGIN_Y);

            bool isGrounded = Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hitInfo,
                EnemyMovementConstants.GROUND_CHECK_DISTANCE,
                TerrainLayers.Walkable);

            if (isGrounded)
            {
                _verticalVelocity = 0f;
                _lastGroundedY = hitInfo.point.y;
                float targetY = hitInfo.point.y;
                float newY = Mathf.MoveTowards(transform.position.y, targetY, EnemyMovementConstants.GROUND_SNAP_LERP_SPEED * Time.fixedDeltaTime);

                Vector3 currentPosition = transform.position;
                currentPosition.y = newY;
                transform.position = currentPosition;

                return true;
            }

            _verticalVelocity += EnemyMovementConstants.FALL_GRAVITY * Time.fixedDeltaTime;
            transform.position += Vector3.down * (_verticalVelocity * Time.fixedDeltaTime);

            if (transform.position.y < EnemyMovementConstants.FALL_DEATH_Y_THRESHOLD)
            {
                _enemy.TakeFullHpDamage();
            }

            return false;
        }

        private void MovementHandler()
        {
            bool isGrounded = HandleVerticalPositionAndGrounding();

            if (!isGrounded && transform.position.y < _lastGroundedY - EnemyMovementConstants.FALL_SUPPRESSION_Y_OFFSET)
            {
                return;
            }

            bool isStunned = _isStunnable && _enemy.StunController.IsStunned;

            bool canMoveOnGrid = _movementUnrelatedToSpeedTween is null
                && !isStunned
                && !_enemy.EnemyAnimator.IsPlayingAttackAnimation
                && _currentMovementDelayAfterAttack <= 0;

            if (canMoveOnGrid)
            {
                _lastPos = transform.position;

                if (_isMovingToPositionUnrelatedToGrid)
                {
                    Vector3? movement = MoveToPosition(_currentMovementPositionUnrelatedToGrid);
                    if (movement.HasValue)
                    {
                        RotateTowardsMovementDirection(movement.Value - _lastPos);
                    }
                }
                else
                {
                    Vector3 desiredDirection = _flowFieldMovementController.CalculateDesiredMovementDirection();
                    Vector3 targetVelocity = desiredDirection * _enemy.Config.MovementSpeed;

                    float effectiveAcceleration = _enemy.Config.Acceleration > 0f
                        ? _enemy.Config.Acceleration
                        : _enemy.Config.MovementSpeed * EnemyMovementConstants.DEFAULT_ACCELERATION_SPEED_MULTIPLIER;

                    _currentVelocity = Vector3.MoveTowards(_currentVelocity, targetVelocity, effectiveAcceleration * Time.fixedDeltaTime);
                    transform.position += _currentVelocity * Time.fixedDeltaTime;

                    if (_currentVelocity.sqrMagnitude > EnemyMovementConstants.MIN_VELOCITY_FOR_ROTATION_SQR)
                    {
                        RotateTowardsMovementDirection(_currentVelocity);
                    }
                }
            }
            else
            {
                _currentVelocity = Vector3.zero;

                if (_currentMovementDelayAfterAttack > 0)
                {
                    _currentMovementDelayAfterAttack -= Time.fixedDeltaTime;
                }
            }
        }

        private void EnemyAnimator_OnAttackAnimationEnd(object sender, System.EventArgs e)
        {
            _currentMovementDelayAfterAttack = _movementDelayAfterAttack;
        }

        private Vector3? MoveToPosition(Vector3 pos)
        {
            bool isOnPosition = Vector3.Distance(transform.position, pos) <= EnemyMovementConstants.MOVING_TO_POSITION_ACCURACY;

            if (_isMovingToPositionUnrelatedToGrid && !isOnPosition)
            {
                return Move(pos);
            }
            else if (isOnPosition)
            {
                _isMovingToPositionUnrelatedToGrid = false;
                return null;
            }
            else
            {
                _currentMovementPositionUnrelatedToGrid = pos;
                _isMovingToPositionUnrelatedToGrid = true;
                return Move(pos);
            }
        }

        private Vector3 Move(Vector3 pos)
        {
            Vector3 movement = Vector3.Lerp(transform.position, pos, _enemy.Config.MovementSpeed * Time.fixedDeltaTime);
            transform.position = movement;
            return movement;
        }

        private void RotateTowardsMovementDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude > EnemyMovementConstants.MIN_VELOCITY_FOR_ROTATION_SQR)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    _enemy.Config.RotationSpeed * Time.fixedDeltaTime
                );
            }
        }
    }
}
