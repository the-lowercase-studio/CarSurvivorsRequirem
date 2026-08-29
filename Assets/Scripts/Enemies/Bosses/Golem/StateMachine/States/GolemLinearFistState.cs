using System;
using DG.Tweening;
using Assets.Scripts.Enemies.Bosses.Golem.Constants;
using Assets.Scripts.Indicators;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.StateMachine.States
{
    public class GolemLinearFistState : IGolemState
    {
        private readonly IGolemBoss _boss;
        private readonly GolemStateMachine _stateMachine;
        private GolemPursuitState _pursuitState;
        private Sequence _chargeSequence;
        private RectangularTelegraphIndicator _activeTelegraph;
        private Vector3 _attackDirection;
        private int _armsCompleted;
        private bool _hasFiredFists;

        public GolemLinearFistState(IGolemBoss boss, GolemStateMachine stateMachine)
        {
            _boss = boss;
            _stateMachine = stateMachine;
        }

        public void SetPursuitState(GolemPursuitState pursuitState)
        {
            _pursuitState = pursuitState;
        }

        public void Enter()
        {
            _boss.Movement.CanMove = false;
            _boss.Movement.Stop();
            _boss.Movement.SetKinematic(true);
            _boss.Animator?.SetMoving(false, 0f);
            _armsCompleted = 0;
            _hasFiredFists = false;

            // 1. Always rotate boss towards the forward attack target direction first
            Vector3 bossPos = _boss.Transform.position;
            Vector3 targetDir = _boss.DirectionToPlayer;
            targetDir.y = 0f;
            if (targetDir.sqrMagnitude < 0.01f)
            {
                targetDir = _boss.Transform.forward;
            }
            _attackDirection = targetDir.normalized;
            _boss.Transform.rotation = Quaternion.LookRotation(_attackDirection, Vector3.up);

            // 2. Display telegraph indicator
            float warningDuration = _boss.Config.LinearFistWarningDuration;
            float maxDistance = _boss.Config.LinearFistMaxDistance;
            float width = _boss.Config.LinearFistWidth;

            _activeTelegraph = _boss.ShowRectangularTelegraph(bossPos, _attackDirection, maxDistance, width, warningDuration, null, autoContractOnFillComplete: false);

            // 3. Start forward attack animation and listen for release trigger
            if (_boss.Animator != null)
            {
                _boss.Animator.OnLinearFistRelease += HandleLinearFistRelease;
                _boss.Animator.PlayLinearFist();
            }

            // 4. Fallback timer if animation event is not authored on clip
            float releaseDelay = _boss.Config.LinearFistReleaseDelay;
            _chargeSequence = DOTween.Sequence();
            _chargeSequence.AppendInterval(releaseDelay);
            _chargeSequence.OnComplete(() =>
            {
                TriggerFistRelease();
            });
        }

        public void Update()
        {
            _stateMachine.TickCooldowns(Time.deltaTime);
        }

        public void FixedUpdate()
        {
            _boss.Movement.Stop();
        }

        public void Exit()
        {
            if (_boss.Animator != null)
            {
                _boss.Animator.OnLinearFistRelease -= HandleLinearFistRelease;
            }

            if (_chargeSequence != null && _chargeSequence.IsActive())
            {
                _chargeSequence.Kill();
            }
            _chargeSequence = null;

            if (_boss.LinearAttackHitbox != null)
            {
                _boss.LinearAttackHitbox.Deactivate();
            }

            if (_activeTelegraph != null)
            {
                _activeTelegraph.Dismiss();
                _activeTelegraph = null;
            }

            _boss.Movement.SetKinematic(false);
            _boss.Movement.CanMove = true;
        }

        private void HandleLinearFistRelease()
        {
            TriggerFistRelease();
        }

        private void TriggerFistRelease()
        {
            if (_hasFiredFists)
            {
                return;
            }
            _hasFiredFists = true;

            if (_chargeSequence != null && _chargeSequence.IsActive())
            {
                _chargeSequence.Kill();
                _chargeSequence = null;
            }

            FireFists(_attackDirection);
        }

        private void FireFists(Vector3 targetDirection)
        {
            _boss.AudioClipPlayer?.PlayOneShot(GolemBossConstants.ROCKET_SFX_KEY);

            float speed = _boss.Config.LinearFistSpeed * _boss.CurrentArmSpeedMultiplier;
            float damage = _boss.Config.LinearFistDamage;
            float maxDistance = _boss.Config.LinearFistMaxDistance;
            float width = _boss.Config.LinearFistWidth;
            float height = _boss.Config.LinearFistHitboxHeight;
            float depth = _boss.Config.LinearFistHitboxDepth;
            float verticalOffset = _boss.Config.LinearFistHitboxVerticalOffset;

            if (_boss.LinearAttackHitbox != null)
            {
                _boss.LinearAttackHitbox.Activate(
                    _boss.Transform.position,
                    targetDirection,
                    width,
                    height,
                    depth,
                    verticalOffset,
                    maxDistance,
                    speed,
                    damage
                );
            }

            int totalArms = 0;
            if (_boss.Arms.LeftArm != null) totalArms++;
            if (_boss.Arms.RightArm != null) totalArms++;

            if (totalArms == 0)
            {
                FinishAttack();
                return;
            }

            if (_boss.Arms.LeftArm != null)
            {
                _boss.Arms.LeftArm.FireLinear(targetDirection, maxDistance, speed, OnArmReturned);
            }

            if (_boss.Arms.RightArm != null)
            {
                _boss.Arms.RightArm.FireLinear(targetDirection, maxDistance, speed, OnArmReturned);
            }
        }

        private void OnArmReturned()
        {
            _armsCompleted++;

            int totalArms = 0;
            if (_boss.Arms.LeftArm != null) totalArms++;
            if (_boss.Arms.RightArm != null) totalArms++;

            if (_armsCompleted >= totalArms)
            {
                FinishAttack();
            }
        }

        private void FinishAttack()
        {
            if (_activeTelegraph != null)
            {
                _activeTelegraph.ContractAndDismiss();
                _activeTelegraph = null;
            }

            _stateMachine.LinearFistCooldownTimer = _boss.Config.LinearFistCooldown * _boss.CurrentCooldownMultiplier;
            _stateMachine.ChangeState(_pursuitState);
        }
    }
}
