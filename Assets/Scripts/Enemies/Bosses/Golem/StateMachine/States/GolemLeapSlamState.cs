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
            _boss.Movement.SetKinematic(true);
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
            snappedTarget.y = startPos.y;

            Vector3 jumpDir = snappedTarget - startPos;
            jumpDir.y = 0f;
            if (jumpDir.sqrMagnitude > 0.001f)
            {
                _boss.Transform.rotation = Quaternion.LookRotation(jumpDir.normalized, Vector3.up);
            }

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
            _boss.Movement.Stop();
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

            _boss.Movement.SetKinematic(false);
        }

        private void OnSlamImpact(Vector3 impactPosition, float radius, float damage)
        {
            _boss.Movement.SetPosition(impactPosition);
            _boss.Movement.SetKinematic(false);
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
