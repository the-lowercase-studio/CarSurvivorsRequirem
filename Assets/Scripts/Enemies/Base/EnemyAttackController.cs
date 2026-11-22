using Assets.Scripts.Collisions;
using Assets.Scripts.Enemies.Constants;
using Assets.Scripts.Extensions;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using System;
using UnityEngine;

namespace Assets.Scripts.Enemies.Base
{
    [RequireComponent(typeof(Enemy))]
    public class EnemyAttackController : MonoBehaviour
    {
        [SerializeField, Range(0, 360)] private float _attackArcAngle = 60f;
        [SerializeField] private float _attackRange = 1f;

        private Enemy _enemy;
        private IAttackAnimationPlayer _attackAnimationPlayer;
        private Collider _currentAttackedTarget;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _attackAnimationPlayer = _enemy.EnemyAnimator;
        }

        private void OnEnable()
        {
            _enemy.CollisionsController.OnCollisionWithPlayer += EnemyCollisions_OnCollisionWithPlayer;
            _attackAnimationPlayer.OnAttackHitFrame += AttackAnimationPlayer_OnAttackHitFrame;
        }

        private void OnDisable()
        {
            _enemy.CollisionsController.OnCollisionWithPlayer -= EnemyCollisions_OnCollisionWithPlayer;
            _attackAnimationPlayer.OnAttackHitFrame -= AttackAnimationPlayer_OnAttackHitFrame;
            _currentAttackedTarget = null;
        }

        private void EnemyCollisions_OnCollisionWithPlayer(object sender, CollisionEventArgs e)
        {
            if (_enemy.IsFrozen || _enemy.IsMaterializing || _attackAnimationPlayer.IsPlayingAttackAnimation || e.Collider == null)
            {
                return;
            }

            if (CanAttackTarget(e.Collider))
            {
                _currentAttackedTarget = e.Collider;
                _enemy.EnemyAnimator.PlayAttackAnimation();
            }
        }

        private void AttackAnimationPlayer_OnAttackHitFrame(object sender, EventArgs e)
        {
            DamageCurrentlyAttackedTarget();
        }

        private void DamageCurrentlyAttackedTarget()
        {
            if (!_enemy.IsMaterializing
                && CanAttackTarget(_currentAttackedTarget)
                && _currentAttackedTarget.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(_enemy.Config.Damage);
            }

            _currentAttackedTarget = null;
        }

        private bool CanAttackTarget(Collider target)
        {
            if (target == null)
            {
                return false;
            }

            Collider attackedTarget = GetAttackedColliderIfInRange(target);

            if (attackedTarget == target)
            {
                Vector3 toTarget = target.ClosestPoint(transform.position) - transform.position;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude < 0.001f)
                {
                    return true;
                }

                float angleToTarget = Vector3.Angle(transform.forward, toTarget);
                if (angleToTarget <= _attackArcAngle * 0.5f)
                {
                    float distanceToTarget = toTarget.magnitude;

                    Ray ray = new Ray(transform.position, toTarget.normalized);
                    if (Physics.Raycast(ray, out RaycastHit rayHit, distanceToTarget, TerrainLayers.All)
                        && rayHit.collider != target)
                    {
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        private Collider GetAttackedColliderIfInRange(Collider target)
        {
            Collider[] colliders = Physics.OverlapSphere(
                transform.position,
                _attackRange,
                1 << target.gameObject.layer
            );

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == target)
                {
                    return target;
                }
            }

            return null;
        }

        private void OnDrawGizmos()
        {
            new Debug().DrawArc(
                transform.position,
                transform.forward,
                _attackArcAngle,
                _attackRange,
                EnemyCombatConstants.ARC_DEBUG_SEGMENTS,
                Color.red);
        }
    }
}
