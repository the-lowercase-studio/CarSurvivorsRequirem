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
        private Vector3 _impactSurface;
        private float _slamRadius;
        private float _slamDamage;
        private bool _hasLaunchedAirborne;
        private bool _hasLanded;
        private bool _hasCompleted;
        private bool _isRecovery;
        private int _operationGeneration;
        private int _entryGeneration;

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
            _entryGeneration++;
            _operationGeneration = _boss.OperationGeneration;
            _isRecovery = _boss.IsRecovering;
            _hasLaunchedAirborne = false;
            _hasLanded = false;
            _hasCompleted = false;
            _slamRadius = _boss.Config.SlamRadius;
            _slamDamage = _boss.Config.SlamDamage;
            _boss.Movement.CanMove = false;
            _boss.Movement.Stop();
            _boss.Movement.SetKinematic(true);
            _boss.Animator.SetMoving(false, 0f);
            _boss.Animator.OnLeapLandComplete += HandleLandingComplete;

            if (_isRecovery)
            {
                _boss.Animator.HoldLeapAirbornePose();
                return;
            }

            _startPosition = _boss.Transform.position;
            Vector3 target = _boss.PlayerPosition;
            _activeTelegraph = _boss.ShowCircularTelegraph(target, _slamRadius, GetWarningDuration());
            _snappedTarget = _activeTelegraph != null ? _activeTelegraph.SnappedPosition : target;
            _snappedTarget.y = _startPosition.y;
            _impactSurface = _snappedTarget;
            RotateTowardsLanding();
            _boss.Animator.OnLeapTakeoffComplete += HandleTakeoffComplete;
            _boss.Animator.PlayLeapTakeoff();
            int entry = _entryGeneration;
            _phaseSequence = DOTween.Sequence();
            _phaseSequence.AppendInterval(_boss.Config.LeapTakeoffDuration);
            _phaseSequence.OnComplete(() =>
            {
                if (IsCurrent(entry))
                {
                    LaunchAirborne();
                }
            });
        }

        public void Update()
        {
            _stateMachine.TickCooldowns(Time.deltaTime);
            if (_isRecovery && !_hasLaunchedAirborne && IsCurrent(_entryGeneration)
                && _boss.TryFindRecoveryLanding(out Vector3 root, out Vector3 surface))
            {
                _snappedTarget = root;
                _impactSurface = surface;
                StartRecoveryDescent();
            }
        }

        public void FixedUpdate()
        {
            _boss.Movement.Stop();
        }

        public void Exit()
        {
            _entryGeneration++;
            KillAllSequences();
            _boss.Animator.OnLeapTakeoffComplete -= HandleTakeoffComplete;
            _boss.Animator.OnLeapLandComplete -= HandleLandingComplete;
            _boss.Animator.RestorePlayback();
            if (_activeTelegraph != null)
            {
                _activeTelegraph.Dismiss();
                _activeTelegraph = null;
            }
            _boss.Movement.SetKinematic(false);
        }

        private float GetWarningDuration()
        {
            return _boss.Config.LeapTakeoffDuration + _boss.Config.LeapAirTime * 0.5f;
        }

        private bool IsCurrent(int entry)
        {
            return entry == _entryGeneration && _operationGeneration == _boss.OperationGeneration
                && _stateMachine.CurrentState == this && _boss.IsOperational && !_hasCompleted;
        }

        private void RotateTowardsLanding()
        {
            Vector3 direction = _snappedTarget - _boss.Transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                _boss.Transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        private void HandleTakeoffComplete()
        {
            if (!_isRecovery && IsCurrent(_entryGeneration))
            {
                LaunchAirborne();
            }
        }

        private void LaunchAirborne()
        {
            if (_hasLaunchedAirborne || !IsCurrent(_entryGeneration))
            {
                return;
            }
            _hasLaunchedAirborne = true;
            KillPhaseSequence();
            float halfAirTime = _boss.Config.LeapAirTime * 0.5f;
            Vector3 apex = (_startPosition + _snappedTarget) * 0.5f + Vector3.up * _boss.Config.LeapMaxHeight;
            int entry = _entryGeneration;
            _leapSequence = DOTween.Sequence();
            _leapSequence.Append(_boss.Transform.DOMove(apex, halfAirTime).SetEase(Ease.OutQuad));
            _leapSequence.Append(_boss.Transform.DOMove(_snappedTarget, halfAirTime).SetEase(Ease.InQuad));
            _leapSequence.OnComplete(() =>
            {
                if (IsCurrent(entry))
                {
                    OnGroundCollision();
                }
            });
        }

        private void StartRecoveryDescent()
        {
            _hasLaunchedAirborne = true;
            RotateTowardsLanding();
            _boss.Movement.SetPosition(_snappedTarget + Vector3.up * _boss.Config.LeapMaxHeight);
            _activeTelegraph = _boss.ShowCircularTelegraph(_impactSurface, _slamRadius,
                GetWarningDuration(), exactPosition: true);
            int entry = _entryGeneration;
            _leapSequence = DOTween.Sequence();
            _leapSequence.Append(_boss.Transform.DOMove(_snappedTarget, GetWarningDuration()).SetEase(Ease.InQuad));
            _leapSequence.OnComplete(() =>
            {
                if (IsCurrent(entry))
                {
                    OnGroundCollision();
                }
            });
        }

        private void OnGroundCollision()
        {
            if (_hasLanded || !IsCurrent(_entryGeneration))
            {
                return;
            }
            if (_isRecovery && !_boss.ValidateRecoveryLanding(_snappedTarget, out _impactSurface))
            {
                _boss.RestartRecoverySearch();
                _stateMachine.ChangeState(this, restart: true);
                return;
            }
            _hasLanded = true;
            _boss.Movement.SetPosition(_snappedTarget);
            _boss.Movement.SetKinematic(false);
            _boss.AudioClipPlayer?.PlayOneShot(GolemBossConstants.SLAM_SFX_KEY);
            _boss.Animator.PlayLeapLand();
            ApplyAreaImpactDamage(_impactSurface, _slamRadius, _slamDamage);
            if (_activeTelegraph != null)
            {
                _activeTelegraph.ContractAndDismiss();
                _activeTelegraph = null;
            }
            int entry = _entryGeneration;
            _phaseSequence = DOTween.Sequence();
            _phaseSequence.AppendInterval(_boss.Config.LeapLandingDuration);
            _phaseSequence.OnComplete(() =>
            {
                if (IsCurrent(entry))
                {
                    FinishLeapState();
                }
            });
        }

        private void HandleLandingComplete()
        {
            if (_hasLanded && IsCurrent(_entryGeneration))
            {
                FinishLeapState();
            }
        }

        private void ApplyAreaImpactDamage(Vector3 center, float radius, float damage)
        {
            Collider[] hits = Physics.OverlapCapsule(center, center + Vector3.up * 4f,
                radius, EntityLayers.Player, QueryTriggerInteraction.Collide);
            foreach (Collider hit in hits)
            {
                if (hit != null)
                {
                    EntityManipulationHelper.Damage(hit, damage);
                }
            }
        }

        private void FinishLeapState()
        {
            if (!_hasLanded || !IsCurrent(_entryGeneration))
            {
                return;
            }
            _hasCompleted = true;
            _stateMachine.LeapCooldownTimer = _boss.Config.LeapCooldown * _boss.CurrentCooldownMultiplier;
            if (_isRecovery)
            {
                _boss.CompleteRecovery();
            }
            _stateMachine.ChangeState(_pursuitState);
        }

        private void KillPhaseSequence()
        {
            if (_phaseSequence != null && _phaseSequence.IsActive())
            {
                _phaseSequence.Kill(false);
            }
            _phaseSequence = null;
        }

        private void KillAllSequences()
        {
            if (_leapSequence != null && _leapSequence.IsActive())
            {
                _leapSequence.Kill(false);
            }
            _leapSequence = null;
            KillPhaseSequence();
        }
    }
}
