using System;
using DG.Tweening;
using Assets.Scripts.Enemies.Bosses.Golem.Constants;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.StateMachine.States
{
    public class GolemLinearFistState : IGolemState
    {
        private readonly IGolemBoss _boss;
        private readonly GolemStateMachine _stateMachine;
        private GolemPursuitState _pursuitState;
        private Sequence _chargeSequence;
        private int _armsCompleted;

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
            _boss.Animator?.SetMoving(false, 0f);
            _boss.Animator?.PlayLinearFist();
            _armsCompleted = 0;

            Vector3 bossPos = _boss.Transform.position;
            Vector3 targetDir = _boss.DirectionToPlayer;
            targetDir.y = 0f;
            if (targetDir.sqrMagnitude < 0.01f)
            {
                targetDir = _boss.Transform.forward;
            }
            targetDir.Normalize();

            _boss.Transform.rotation = Quaternion.LookRotation(targetDir, Vector3.up);

            float warningDuration = _boss.Config.LinearFistWarningDuration;
            float maxDistance = _boss.Config.LinearFistMaxDistance;
            float width = _boss.Config.LinearFistWidth;

            if (_boss.RectangularTelegraph != null)
            {
                _boss.RectangularTelegraph.Show(bossPos, targetDir, maxDistance, width, warningDuration, () =>
                {
                    FireFists(targetDir);
                });
            }
            else
            {
                _chargeSequence = DOTween.Sequence();
                _chargeSequence.AppendInterval(warningDuration);
                _chargeSequence.OnComplete(() =>
                {
                    FireFists(targetDir);
                });
            }
        }

        public void Update()
        {
            _stateMachine.TickCooldowns(Time.deltaTime);
        }

        public void FixedUpdate()
        {
        }

        public void Exit()
        {
            if (_chargeSequence != null && _chargeSequence.IsActive())
            {
                _chargeSequence.Kill();
            }
            _chargeSequence = null;

            if (_boss.RectangularTelegraph != null)
            {
                _boss.RectangularTelegraph.Dismiss();
            }
        }

        private void FireFists(Vector3 targetDirection)
        {
            _boss.AudioClipPlayer?.PlayOneShot(GolemBossConstants.ROCKET_SFX_KEY);

            float speed = _boss.Config.LinearFistSpeed * _boss.CurrentArmSpeedMultiplier;
            float damage = _boss.Config.LinearFistDamage;
            float maxDistance = _boss.Config.LinearFistMaxDistance;

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
                _boss.Arms.LeftArm.FireLinear(targetDirection, maxDistance, speed, damage, OnArmReturned);
            }

            if (_boss.Arms.RightArm != null)
            {
                _boss.Arms.RightArm.FireLinear(targetDirection, maxDistance, speed, damage, OnArmReturned);
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
            _stateMachine.LinearFistCooldownTimer = _boss.Config.LinearFistCooldown * _boss.CurrentCooldownMultiplier;
            _stateMachine.ChangeState(_pursuitState);
        }
    }
}
