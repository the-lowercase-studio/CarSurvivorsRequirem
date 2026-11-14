using System.Collections.Generic;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Constants;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.StateMachine.States
{
    public class RailgunChasingLaserState : IRailgunTowerState
    {
        private enum Phase
        {
            LockOn,
            Chasing
        }

        private readonly IRailgunTowerBoss _boss;
        private readonly HashSet<IDamageable> _tickHitTargets = new HashSet<IDamageable>();
        private readonly Collider[] _overlapBuffer = new Collider[RailgunTowerConstants.OVERLAP_BUFFER_SIZE];

        private Phase _phase;
        private Vector3 _chaseCenter;
        private float _radius;
        private float _lockOnTimer;
        private float _chaseElapsed;
        private float _tickTimer;
        private bool _isTransitioningToCooldown;
        private bool _isHoldingOnPlayerCenter;

        public RailgunChasingLaserState(IRailgunTowerBoss boss)
        {
            _boss = boss;
        }

        public void Enter()
        {
            _tickHitTargets.Clear();
            _isTransitioningToCooldown = false;
            _isHoldingOnPlayerCenter = false;
            _phase = Phase.LockOn;
            _radius = _boss.IsEnraged ? _boss.Config.Attack2EnrageRadius : _boss.Config.Attack2NormalRadius;
            _lockOnTimer = _boss.Config.Attack2LockOnDelay;
            _chaseElapsed = 0f;
            _tickTimer = 0f;

            _chaseCenter = ClampToArena(GetPlayerXZ(), Mathf.Max(1f, _boss.Config.ArenaRadius - _radius * 0.5f));

            _boss.ChaseAnchor.position = _chaseCenter;
            _boss.ReusableChaseIndicator.ShowAttachedWarning(_boss.ChaseAnchor, _radius);
            _boss.SnapTurretAim(_chaseCenter);
            _boss.ChaseBeamLine.enabled = false;
            _boss.PlayAudioOneShot(RailgunTowerConstants.SFX_RAILGUN_CHARGE);
        }

        public void Exit()
        {
            if (_boss.ChaseBeamLine != null)
            {
                _boss.ChaseBeamLine.enabled = false;
            }

            if (!_isTransitioningToCooldown && _boss.ReusableChaseIndicator != null)
            {
                _boss.ReusableChaseIndicator.HideReusable();
            }

            _isTransitioningToCooldown = false;
            _isHoldingOnPlayerCenter = false;
            _tickHitTargets.Clear();
        }

        public void Update()
        {
            if (_phase == Phase.LockOn)
            {
                _lockOnTimer -= Time.deltaTime;
                _boss.SnapTurretAim(_chaseCenter);
                if (_lockOnTimer <= 0f)
                {
                    BeginChase();
                }
                return;
            }

            _chaseElapsed += Time.deltaTime;

            Vector3 playerXZ = GetPlayerXZ();
            if (_isHoldingOnPlayerCenter && !IsPlayerInsideChaseCircle())
            {
                _isHoldingOnPlayerCenter = false;
            }

            if (!_isHoldingOnPlayerCenter)
            {
                float speedRatio = _boss.IsEnraged ? _boss.Config.Attack2EnrageSpeedRatio : _boss.Config.Attack2NormalSpeedRatio;
                float chaseSpeed = _boss.PlayerMaxForwardSpeed * speedRatio;

                _chaseCenter = Vector3.MoveTowards(_chaseCenter, playerXZ, chaseSpeed * Time.deltaTime);
                _chaseCenter = ClampToArena(_chaseCenter, _boss.Config.ArenaRadius);

                if ((playerXZ - _chaseCenter).sqrMagnitude <= RailgunTowerConstants.CHASE_CENTER_HOLD_DISTANCE_SQR)
                {
                    _isHoldingOnPlayerCenter = true;
                }
            }

            _boss.ChaseAnchor.position = _chaseCenter;
            _boss.ReusableChaseIndicator.SynchronizeAttachedPosition();
            _boss.SnapTurretAim(_chaseCenter);
            UpdateChaseBeamLine();

            _tickTimer -= Time.deltaTime;
            if (_tickTimer <= 0f)
            {
                _tickTimer = _boss.Config.Attack2DamageTickInterval;
                ApplyTickDamage();
            }

            if (_chaseElapsed >= _boss.Config.Attack2ChaseDuration)
            {
                _isTransitioningToCooldown = true;
                _boss.ChaseBeamLine.enabled = false;
                _boss.ReusableChaseIndicator.ContractAndHideReusable(null);
                _boss.ChangeState(_boss.CooldownState);
            }
        }

        public void FixedUpdate()
        {
        }

        private void BeginChase()
        {
            _phase = Phase.Chasing;
            _chaseElapsed = 0f;
            _tickTimer = 0f;
            _isHoldingOnPlayerCenter = false;

            _boss.ChaseBeamLine.enabled = true;
            _boss.ChaseBeamLine.positionCount = 2;
            UpdateChaseBeamLine();
            _boss.PlayFireFeedback(RailgunTowerConstants.SFX_RAILGUN_CHASE_BEAM);
        }

        private void UpdateChaseBeamLine()
        {
            _boss.ChaseBeamLine.SetPosition(0, _boss.MuzzlePosition);
            _boss.ChaseBeamLine.SetPosition(1, _chaseCenter + Vector3.up * RailgunTowerConstants.BEAM_GROUND_Y_OFFSET);
        }

        private void ApplyTickDamage()
        {
            _tickHitTargets.Clear();

            int hitCount = Physics.OverlapSphereNonAlloc(
                _chaseCenter,
                _radius,
                _overlapBuffer,
                EntityLayers.Player,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                _overlapBuffer[i] = null;

                if (hit == null || !hit.enabled)
                {
                    continue;
                }

                if (hit.TryGetComponent(out IDamageable damageable) || (damageable = hit.GetComponentInParent<IDamageable>()) != null)
                {
                    if (_tickHitTargets.Add(damageable))
                    {
                        damageable.TakeDamage(_boss.Config.Attack2TickDamage);
                    }
                }
            }
        }

        private bool IsPlayerInsideChaseCircle()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                _chaseCenter,
                _radius,
                _overlapBuffer,
                EntityLayers.Player,
                QueryTriggerInteraction.Collide
            );

            bool isInside = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                _overlapBuffer[i] = null;

                if (hit != null && hit.enabled)
                {
                    isInside = true;
                }
            }

            return isInside;
        }

        private Vector3 GetPlayerXZ()
        {
            Vector3 playerPos = _boss.PlayerPosition;
            return new Vector3(playerPos.x, 0f, playerPos.z);
        }

        private Vector3 ClampToArena(Vector3 worldXZ, float maxRadius)
        {
            Vector3 towerPos = _boss.Transform.position;
            Vector3 towerXZ = new Vector3(towerPos.x, 0f, towerPos.z);
            Vector3 offset = worldXZ - towerXZ;
            offset.y = 0f;

            if (offset.sqrMagnitude > maxRadius * maxRadius)
            {
                offset = offset.normalized * maxRadius;
            }

            return towerXZ + offset;
        }
    }
}
