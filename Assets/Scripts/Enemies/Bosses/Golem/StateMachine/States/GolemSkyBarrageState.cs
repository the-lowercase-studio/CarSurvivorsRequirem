using System;
using DG.Tweening;
using Assets.Scripts.Enemies.Bosses.Golem.Arms;
using Assets.Scripts.Enemies.Bosses.Golem.Constants;
using Assets.Scripts.Indicators;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.StateMachine.States
{
    public class GolemSkyBarrageState : IGolemState
    {
        private const float SKY_LAUNCH_HEIGHT = 40f;
        private const float RETURN_DOCK_DURATION = 0.8f;
        private const float WARNING_FRACTION_BEFORE_DROP = 0.4f;

        private readonly IGolemBoss _boss;
        private readonly GolemStateMachine _stateMachine;
        private GolemPursuitState _pursuitState;

        private int _totalCycles;
        private int _totalArmsCount;
        private int _armsFinishedCount;
        private bool _hasTriggeredRelease;
        private bool _isLaunchingArms;
        private bool _isStomping;
        private float _stompImpactTimer;
        private float _stompDurationTimer;
        private bool _hasDealtStompDamage;

        private Sequence _releaseSequence;
        private Sequence _leftArmSequence;
        private Sequence _rightArmSequence;

        public GolemSkyBarrageState(IGolemBoss boss, GolemStateMachine stateMachine)
        {
            _boss = boss;
            _stateMachine = stateMachine;
        }

        public void SetPursuitState(GolemPursuitState pursuitState)
        {
            _pursuitState = pursuitState;
        }

        public void SetStompState(GolemStompState stompState)
        {
            // Maintained for interface/instantiation compatibility
        }

        public void Enter()
        {
            _isLaunchingArms = true;
            _isStomping = false;
            _hasDealtStompDamage = false;

            _boss.Movement.CanMove = false;
            _boss.Movement.Stop();
            _boss.Movement.SetKinematic(true);
            _boss.Animator?.SetMoving(false, 0f);

            _totalCycles = GetTotalCyclesForCurrentPhase();
            _armsFinishedCount = 0;
            _hasTriggeredRelease = false;

            _totalArmsCount = 0;
            if (_boss.Arms.LeftArm != null) _totalArmsCount++;
            if (_boss.Arms.RightArm != null) _totalArmsCount++;

            if (_totalArmsCount == 0)
            {
                FinishAttack();
                return;
            }

            if (_boss.Animator != null)
            {
                _boss.Animator.OnSkyBarrageRelease += HandleSkyBarrageRelease;
                _boss.Animator.PlaySkyBarrage();
            }

            _boss.AudioClipPlayer?.PlayOneShot(GolemBossConstants.ROAR_SFX_KEY);

            float releaseDelay = _boss.Config.SkyBarrageReleaseDelay;
            _releaseSequence = DOTween.Sequence();
            _releaseSequence.AppendInterval(releaseDelay);
            _releaseSequence.OnComplete(() =>
            {
                TriggerArmLaunch();
            });
        }

        public void Update()
        {
            _stateMachine.TickCooldowns(Time.deltaTime);

            if (_isStomping)
            {
                _stompImpactTimer -= Time.deltaTime;
                if (_stompImpactTimer <= 0f && !_hasDealtStompDamage)
                {
                    _hasDealtStompDamage = true;
                    _boss.TriggerStompDamage();
                }

                _stompDurationTimer -= Time.deltaTime;
                if (_stompDurationTimer <= 0f)
                {
                    _isStomping = false;
                    _stateMachine.StompCooldownTimer = _boss.Config.StompCooldown * _boss.CurrentCooldownMultiplier;
                    if (!_isLaunchingArms)
                    {
                        _boss.Movement.SetKinematic(false);
                        _boss.Movement.CanMove = true;
                    }
                }
                return;
            }

            if (!_isLaunchingArms)
            {
                if (_boss.DistanceToPlayer <= _boss.Config.StompRadius && _stateMachine.StompCooldownTimer <= 0f)
                {
                    _isStomping = true;
                    _hasDealtStompDamage = false;
                    _stompImpactTimer = GolemBossConstants.STOMP_IMPACT_DELAY;
                    _stompDurationTimer = GolemBossConstants.STOMP_TOTAL_DURATION;

                    _boss.Movement.CanMove = false;
                    _boss.Movement.Stop();
                    _boss.Movement.SetKinematic(true);
                    _boss.Animator?.SetMoving(false, 0f);
                    _boss.Animator?.PlayStomp();
                }
            }
        }

        public void FixedUpdate()
        {
            if (!_isLaunchingArms && !_isStomping && _boss.Movement.CanMove)
            {
                float speed = _boss.Config.MoveSpeed * _boss.CurrentSpeedMultiplier;
                _boss.Movement.MoveTowards(_boss.PlayerPosition, speed, _boss.Config.RotationSpeed);
                _boss.Animator?.SetMoving(true, speed);
            }
            else
            {
                _boss.Movement.Stop();
                _boss.Animator?.SetMoving(false, 0f);
            }
        }

        public void Exit()
        {
            if (_boss.Animator != null)
            {
                _boss.Animator.OnSkyBarrageRelease -= HandleSkyBarrageRelease;
            }

            KillAllSequences();
            _boss.DismissAllTelegraphs();
            _isLaunchingArms = false;
            _isStomping = false;
            _boss.Movement.Stop();
            _boss.Movement.SetKinematic(false);
            _boss.Movement.CanMove = true;
            _boss.Animator?.SetMoving(false, 0f);
        }

        private void HandleSkyBarrageRelease()
        {
            TriggerArmLaunch();
        }

        private void TriggerArmLaunch()
        {
            if (_hasTriggeredRelease)
            {
                return;
            }
            _hasTriggeredRelease = true;

            if (_releaseSequence != null && _releaseSequence.IsActive())
            {
                _releaseSequence.Kill();
                _releaseSequence = null;
            }

            _isLaunchingArms = false;
            if (!_isStomping)
            {
                _boss.Movement.SetKinematic(false);
                _boss.Movement.CanMove = true;
            }

            if (_boss.Arms.LeftArm != null)
            {
                StartArmLifecycle(_boss.Arms.LeftArm, initialDelay: 0f, isLeftArm: true);
            }

            if (_boss.Arms.RightArm != null)
            {
                float stagger = _boss.Config.SkyArmInitialStaggerDelay;
                StartArmLifecycle(_boss.Arms.RightArm, initialDelay: stagger, isLeftArm: false);
            }
        }

        private int GetTotalCyclesForCurrentPhase()
        {
            switch (_boss.CurrentPhase)
            {
                case 1:
                    return _boss.Config.SkyBarrageCyclesPhase1;
                case 2:
                    return _boss.Config.SkyBarrageCyclesPhase2;
                case 3:
                default:
                    return _boss.Config.SkyBarrageCyclesPhase3;
            }
        }

        private void StartArmLifecycle(GolemArmProjectile arm, float initialDelay, bool isLeftArm)
        {
            Sequence armSeq = DOTween.Sequence();
            SetArmSequence(isLeftArm, armSeq);

            if (initialDelay > 0f)
            {
                armSeq.AppendInterval(initialDelay);
            }

            float airTime = _boss.Config.SkyArmLaunchAirTime / Mathf.Max(_boss.CurrentArmSpeedMultiplier, 0.1f);

            armSeq.AppendCallback(() =>
            {
                arm.LaunchToSky(SKY_LAUNCH_HEIGHT, airTime, () =>
                {
                    ExecuteArmCycle(arm, currentCycle: 1, isLeftArm);
                });
            });
        }

        private void ExecuteArmCycle(GolemArmProjectile arm, int currentCycle, bool isLeftArm)
        {
            if (arm == null)
            {
                OnArmFinished();
                return;
            }

            Vector3 targetOffsetPos = GetRandomOffsetTargetPosition();
            float radius = _boss.Config.SkyArmImpactRadius;
            float warningDuration = _boss.Config.SkyArmWarningDuration;
            float damage = _boss.Config.SkyArmDamage;
            float fallSpeed = _boss.Config.SkyArmFallSpeed * _boss.CurrentArmSpeedMultiplier;

            CircularTelegraphIndicator telegraph = _boss.ShowCircularTelegraph(targetOffsetPos, radius, warningDuration, null, autoContractOnFillComplete: false);
            Vector3 targetLandingPos = telegraph != null ? telegraph.SnappedPosition : targetOffsetPos;

            Sequence dropSeq = DOTween.Sequence();
            SetArmSequence(isLeftArm, dropSeq);

            dropSeq.AppendInterval(warningDuration * WARNING_FRACTION_BEFORE_DROP);
            dropSeq.AppendCallback(() =>
            {
                arm.DropFromSky(targetLandingPos, fallSpeed, damage, radius, () =>
                {
                    telegraph?.ContractAndDismiss();
                    OnArmImpact(arm, currentCycle, isLeftArm);
                });
            });
        }

        private void OnArmImpact(GolemArmProjectile arm, int currentCycle, bool isLeftArm)
        {
            _boss.AudioClipPlayer?.PlayOneShot(GolemBossConstants.SLAM_SFX_KEY);

            if (currentCycle < _totalCycles)
            {
                // Prepare for next cycle independently
                Sequence resetSeq = DOTween.Sequence();
                SetArmSequence(isLeftArm, resetSeq);

                float resetDelay = _boss.Config.SkyArmCycleResetDelay;
                float airTime = (_boss.Config.SkyArmLaunchAirTime * 0.75f) / Mathf.Max(_boss.CurrentArmSpeedMultiplier, 0.1f);

                resetSeq.AppendInterval(resetDelay);
                resetSeq.AppendCallback(() =>
                {
                    arm.LaunchToSky(SKY_LAUNCH_HEIGHT, airTime, () =>
                    {
                        ExecuteArmCycle(arm, currentCycle + 1, isLeftArm);
                    });
                });
            }
            else
            {
                // Completed all cycles, return to socket and dock
                arm.ReturnAndDock(RETURN_DOCK_DURATION, () =>
                {
                    OnArmFinished();
                });
            }
        }

        private void OnArmFinished()
        {
            _armsFinishedCount++;
            if (_armsFinishedCount >= _totalArmsCount)
            {
                FinishAttack();
            }
        }

        private void FinishAttack()
        {
            _stateMachine.SkyBarrageCooldownTimer = _boss.Config.SkyBarrageCooldown * _boss.CurrentCooldownMultiplier;
            _stateMachine.ChangeState(_pursuitState);
        }

        private Vector3 GetRandomOffsetTargetPosition()
        {
            Vector3 playerPos = _boss.PlayerPosition;
            float minRadius = _boss.Config.SkyArmTargetOffsetMinRadius;
            float maxRadius = _boss.Config.SkyArmTargetOffsetMaxRadius;

            float randomAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float randomDistance = UnityEngine.Random.Range(minRadius, maxRadius);

            Vector3 offset = new Vector3(Mathf.Cos(randomAngle) * randomDistance, 0f, Mathf.Sin(randomAngle) * randomDistance);
            return playerPos + offset;
        }

        private void SetArmSequence(bool isLeftArm, Sequence seq)
        {
            if (isLeftArm)
            {
                if (_leftArmSequence != null && _leftArmSequence.IsActive())
                {
                    _leftArmSequence.Kill();
                }
                _leftArmSequence = seq;
            }
            else
            {
                if (_rightArmSequence != null && _rightArmSequence.IsActive())
                {
                    _rightArmSequence.Kill();
                }
                _rightArmSequence = seq;
            }
        }

        private void KillAllSequences()
        {
            if (_leftArmSequence != null && _leftArmSequence.IsActive())
            {
                _leftArmSequence.Kill();
            }
            _leftArmSequence = null;

            if (_rightArmSequence != null && _rightArmSequence.IsActive())
            {
                _rightArmSequence.Kill();
            }
            _rightArmSequence = null;
        }
    }
}
