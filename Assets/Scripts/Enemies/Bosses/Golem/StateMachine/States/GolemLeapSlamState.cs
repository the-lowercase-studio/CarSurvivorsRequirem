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
        private Sequence _phaseSequence;
        private CircularTelegraphIndicator _activeTelegraph;

        private Vector3 _startPosition;
        private Vector3 _snappedTarget;
        private float _slamRadius;
        private float _slamDamage;
        private bool _hasLaunchedAirborne;
        private bool _hasLanded;

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

            _hasLaunchedAirborne = false;
            _hasLanded = false;

            _startPosition = _boss.Transform.position;
            Vector3 targetLandingPos = _boss.PlayerPosition;
            float warningDuration = _boss.Config.LeapWarningDuration;
            _slamRadius = _boss.Config.SlamRadius;
            _slamDamage = _boss.Config.SlamDamage;

            _activeTelegraph = _boss.ShowCircularTelegraph(targetLandingPos, _slamRadius, warningDuration, null);
            _snappedTarget = _activeTelegraph != null ? _activeTelegraph.SnappedPosition : targetLandingPos;
            _snappedTarget.y = _startPosition.y;

            Vector3 jumpDir = _snappedTarget - _startPosition;
            jumpDir.y = 0f;
            if (jumpDir.sqrMagnitude > 0.001f)
            {
                _boss.Transform.rotation = Quaternion.LookRotation(jumpDir.normalized, Vector3.up);
            }

            if (_boss.Animator != null)
            {
                _boss.Animator.OnLeapTakeoffComplete += HandleTakeoffComplete;
                _boss.Animator.OnLeapLandComplete += HandleLandingComplete;
                _boss.Animator.PlayLeapTakeoff();
            }

            float takeoffDelay = _boss.Config.LeapTakeoffDuration;
            _phaseSequence = DOTween.Sequence();
            _phaseSequence.AppendInterval(takeoffDelay);
            _phaseSequence.OnComplete(() =>
            {
                LaunchAirborne();
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
            KillAllSequences();

            if (_boss.Animator != null)
            {
                _boss.Animator.OnLeapTakeoffComplete -= HandleTakeoffComplete;
                _boss.Animator.OnLeapLandComplete -= HandleLandingComplete;
            }

            if (_activeTelegraph != null)
            {
                _activeTelegraph.Dismiss();
                _activeTelegraph = null;
            }

            _boss.Movement.SetKinematic(false);
        }

        private void HandleTakeoffComplete()
        {
            LaunchAirborne();
        }

        private void LaunchAirborne()
        {
            if (_hasLaunchedAirborne)
            {
                return;
            }
            _hasLaunchedAirborne = true;

            if (_phaseSequence != null && _phaseSequence.IsActive())
            {
                _phaseSequence.Kill();
                _phaseSequence = null;
            }

            float airTime = _boss.Config.LeapAirTime;
            float halfAirTime = airTime * 0.5f;
            float maxHeight = _boss.Config.LeapMaxHeight;
            Vector3 apexPos = (_startPosition + _snappedTarget) * 0.5f + Vector3.up * maxHeight;

            _leapSequence = DOTween.Sequence();
            _leapSequence.Append(_boss.Transform.DOMove(apexPos, halfAirTime).SetEase(Ease.OutQuad));
            _leapSequence.Append(_boss.Transform.DOMove(_snappedTarget, halfAirTime).SetEase(Ease.InQuad));
            _leapSequence.OnComplete(() =>
            {
                OnGroundCollision();
            });
        }

        private void OnGroundCollision()
        {
            if (_hasLanded)
            {
                return;
            }
            _hasLanded = true;

            _boss.Movement.SetPosition(_snappedTarget);
            _boss.Movement.SetKinematic(false);
            _boss.AudioClipPlayer?.PlayOneShot(GolemBossConstants.SLAM_SFX_KEY);

            _boss.Animator?.PlayLeapLand();

            // Perform circular area of effect damage check on ground impact
            ApplyAreaImpactDamage(_snappedTarget, _slamRadius, _slamDamage);

            float landingRecovery = _boss.Config.LeapLandingDuration;
            _phaseSequence = DOTween.Sequence();
            _phaseSequence.AppendInterval(landingRecovery);
            _phaseSequence.OnComplete(() =>
            {
                FinishLeapState();
            });
        }

        private void HandleLandingComplete()
        {
            FinishLeapState();
        }

        private void ApplyAreaImpactDamage(Vector3 center, float radius, float damage)
        {
            Vector3 point1 = center;
            Vector3 point2 = center + Vector3.up * 4.0f;
            Collider[] hitColliders = Physics.OverlapCapsule(point1, point2, radius, EntityLayers.Player, QueryTriggerInteraction.Collide);
            foreach (Collider hit in hitColliders)
            {
                if (hit == null)
                {
                    continue;
                }

                EntityManipulationHelper.Damage(hit, damage);
            }
        }

        private void FinishLeapState()
        {
            _stateMachine.LeapCooldownTimer = _boss.Config.LeapCooldown * _boss.CurrentCooldownMultiplier;
            _stateMachine.ChangeState(_pursuitState);
        }

        private void KillAllSequences()
        {
            if (_leapSequence != null && _leapSequence.IsActive())
            {
                _leapSequence.Kill();
            }
            _leapSequence = null;

            if (_phaseSequence != null && _phaseSequence.IsActive())
            {
                _phaseSequence.Kill();
            }
            _phaseSequence = null;
        }
    }
}
