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
        private GolemStompState _stompState;

        private int _totalCycles;
        private int _totalArmsCount;
        private int _armsFinishedCount;
        private bool _isLaunchingArms;
        private float _launchTimer;

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
            _stompState = stompState;
        }

        public void Enter()
        {
            _boss.Movement.CanMove = false;
            _boss.Movement.Stop();
            _boss.Animator?.SetMoving(false, 0f);

            _totalCycles = GetTotalCyclesForCurrentPhase();
            _armsFinishedCount = 0;
            _isLaunchingArms = true;
            _launchTimer = GolemBossConstants.SKY_BARRAGE_LAUNCH_DURATION;

            _boss.Animator?.PlaySkyBarrage();
            _boss.AudioClipPlayer?.PlayOneShot(GolemBossConstants.ROAR_SFX_KEY);

            _totalArmsCount = 0;
            if (_boss.Arms.LeftArm != null) _totalArmsCount++;
            if (_boss.Arms.RightArm != null) _totalArmsCount++;

            if (_totalArmsCount == 0)
            {
                FinishAttack();
                return;
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

        public void Update()
        {
            _stateMachine.TickCooldowns(Time.deltaTime);

            if (_isLaunchingArms)
            {
                _launchTimer -= Time.deltaTime;
                if (_launchTimer <= 0f)
                {
                    _isLaunchingArms = false;
                    _boss.Movement.CanMove = true;
                }
                return;
            }

            // Stomp check while arms are in the sky
            if (_boss.DistanceToPlayer <= _boss.Config.StompRadius && _stateMachine.StompCooldownTimer <= 0f)
            {
                if (_stompState != null)
                {
                    _stompState.SetReturnState(this);
                    _stateMachine.ChangeState(_stompState);
                    return;
                }

                _boss.TriggerStompDamage();
                _stateMachine.StompCooldownTimer = _boss.Config.StompCooldown * _boss.CurrentCooldownMultiplier;
            }
        }

        public void FixedUpdate()
        {
            if (!_isLaunchingArms && _boss.Movement.CanMove && (_boss.Animator == null || _boss.Animator.IsMovingAnimationPlaying))
            {
                // Body continues to pursue player while arms barrage
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
            KillAllSequences();
            _boss.DismissAllTelegraphs();
            _boss.Movement.Stop();
            _boss.Animator?.SetMoving(false, 0f);
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

            CircularTelegraphIndicator telegraph = _boss.ShowCircularTelegraph(targetOffsetPos, radius, warningDuration, null);
            Vector3 targetLandingPos = telegraph != null ? telegraph.SnappedPosition : targetOffsetPos;

            Sequence dropSeq = DOTween.Sequence();
            SetArmSequence(isLeftArm, dropSeq);

            dropSeq.AppendInterval(warningDuration * WARNING_FRACTION_BEFORE_DROP);
            dropSeq.AppendCallback(() =>
            {
                arm.DropFromSky(targetLandingPos, fallSpeed, damage, radius, () =>
                {
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
