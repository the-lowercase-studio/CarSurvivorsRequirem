using System;
using DG.Tweening;
using Assets.Scripts.Enemies.Bosses.Golem.Constants;
using Assets.Scripts.Indicators;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.StateMachine.States
{
    public class GolemLeapSlamState : IGolemState
    {
        private readonly IGolemBoss _boss;
        private readonly GolemStateMachine _stateMachine;
        private GolemPursuitState _pursuitState;
        private Sequence _leapSequence;
        private CircularTelegraphIndicator _activeTelegraph;

        public GolemLeapSlamState(IGolemBoss boss, GolemStateMachine stateMachine)
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
            _boss.Animator?.PlayLeapSlam();

            Vector3 startPos = _boss.Transform.position;
            Vector3 targetLandingPos = _boss.PlayerPosition;
            float airTime = _boss.Config.LeapAirTime;
            float halfAirTime = airTime * 0.5f;
            float maxHeight = _boss.Config.LeapMaxHeight;
            float warningDuration = _boss.Config.LeapWarningDuration;
            float slamRadius = _boss.Config.SlamRadius;
            float slamDamage = _boss.Config.SlamDamage;

            _activeTelegraph = _boss.ShowCircularTelegraph(targetLandingPos, slamRadius, warningDuration, null);
            Vector3 snappedTarget = _activeTelegraph != null ? _activeTelegraph.SnappedPosition : targetLandingPos;

            Vector3 apexPos = (startPos + snappedTarget) * 0.5f + Vector3.up * maxHeight;

            _leapSequence = DOTween.Sequence();
            _leapSequence.Append(_boss.Transform.DOMove(apexPos, halfAirTime).SetEase(Ease.OutQuad));
            _leapSequence.Append(_boss.Transform.DOMove(snappedTarget, halfAirTime).SetEase(Ease.InQuad));
            _leapSequence.OnComplete(() =>
            {
                OnSlamImpact(snappedTarget, slamRadius, slamDamage);
            });
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
            if (_leapSequence != null && _leapSequence.IsActive())
            {
                _leapSequence.Kill();
            }
            _leapSequence = null;

            if (_activeTelegraph != null)
            {
                _activeTelegraph.Dismiss();
                _activeTelegraph = null;
            }
        }

        private void OnSlamImpact(Vector3 impactPosition, float radius, float damage)
        {
            _boss.AudioClipPlayer?.PlayOneShot(GolemBossConstants.SLAM_SFX_KEY);

            Collider[] hitColliders = Physics.OverlapSphere(impactPosition, radius, EntityLayers.Player);
            foreach (Collider hit in hitColliders)
            {
                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(damage);
                }
            }

            _stateMachine.LeapCooldownTimer = _boss.Config.LeapCooldown * _boss.CurrentCooldownMultiplier;
            _stateMachine.ChangeState(_pursuitState);
        }
    }
}
