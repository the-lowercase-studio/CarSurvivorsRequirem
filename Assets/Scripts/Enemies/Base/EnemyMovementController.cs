using Assets.Scripts.Enemies.Constants;
using Assets.Scripts.Navigation.FlowFieldSystem;
using Assets.Scripts.LayerMasks;
using DG.Tweening;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Assets.Scripts.Enemies.Base
{
    [RequireComponent(typeof(Enemy), typeof(FlowFieldMovementController))]
    public class EnemyMovementController : MonoBehaviour, IMovementController
    {
        private readonly Vector3 _groundCheckOffset = new(0, 0.1f, 0);
        private readonly Vector3 _obstacleCheckOffset = new(0, 0.5f, 0);

        private Enemy _enemy;
        private IFlowFieldMovementController _flowFieldMovementController;

        private float _verticalPosOffset;
        private bool _isStunnable = false;

        private bool _isMovingToPositionUnrelatedToGrid;
        private Vector3 _currentMovementPositionUnrelatedToGrid;
        private Vector3 _lastPos;

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

            _verticalPosOffset = transform.position.y;

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

            transform.position = new Vector3(0, _verticalPosOffset, 0);
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
            Vector3 endPos = transform.position + _groundCheckOffset - Vector3.up * EnemyMovementConstants.GROUND_CHECK_DISTANCE;
            if (IsOnGround())
            {
                Debug.DrawLine(transform.position, endPos, Color.green);
            }
            else
            {
                Debug.DrawLine(transform.position, endPos, Color.red);
            }
        }

        public float GetCurrentMovementSpeed()
        {
            return Vector3.Distance(transform.position, _lastPos) / Time.deltaTime;
        }

        public Tween MoveToPositionInTimeIgnoringSpeed(Vector3 pos, float time)
        {
            if (_movementUnrelatedToSpeedTween != null)
            {
                _movementUnrelatedToSpeedTween.Kill();
            }

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
                    _movementUnrelatedToSpeedTween = null;
                });
        }

        public bool IsOnGround()
        {
            RaycastHit hitInfo;

            Physics.Raycast(
                transform.position + _groundCheckOffset,
                -Vector3.up,
                out hitInfo,
                EnemyMovementConstants.GROUND_CHECK_DISTANCE,
                TerrainLayers.Ground);

            return hitInfo.collider is not null;
        }

        private void MovementHandler()
        {
            if (!IsOnGround())
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

                Vector3? movement;
                if (_isMovingToPositionUnrelatedToGrid)
                {
                    movement = MoveToPosition(_currentMovementPositionUnrelatedToGrid);
                }
                else
                {
                    movement = _flowFieldMovementController.MoveOnFlowFieldGrid(_enemy.Config.MovementSpeed);
                }

                if (movement.HasValue)
                {
                    RotateTowardsMovementDirection(movement.Value);
                }
            }
            else if (_currentMovementDelayAfterAttack > 0)
            {
                _currentMovementDelayAfterAttack -= Time.deltaTime;
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
            Vector3 movement = Vector3.Lerp(transform.position, pos, _enemy.Config.MovementSpeed * Time.deltaTime);
            transform.position = movement;
            return movement;
        }

        private void RotateTowardsMovementDirection(Vector3 movement)
        {
            if (GetCurrentMovementSpeed() > 0)
            {
                Quaternion targetRotation = Quaternion.LookRotation(movement.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    _enemy.Config.RotationSpeed * Time.deltaTime
                );
            }
        }
    }
}
