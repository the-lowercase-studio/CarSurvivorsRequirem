using System;
using DG.Tweening;
using Assets.Scripts.Enemies.Bosses.Golem.Constants;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.StateMachine.States
{
    public class GolemSkyBarrageState : IGolemState
    {
        private readonly IGolemBoss _boss;
        private readonly GolemStateMachine _stateMachine;
        private GolemPursuitState _pursuitState;

        private int _totalCycles;
        private int _currentCycle;
        private int _armsSlammedInCurrentCycle;
        private Sequence _barrageSequence;

        public GolemSkyBarrageState(IGolemBoss boss, GolemStateMachine stateMachine)
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
            _boss.Movement.CanMove = true;
            _currentCycle = 0;
            _totalCycles = GetTotalCyclesForCurrentPhase();
            _boss.Animator?.PlaySkyBarrage();

            LaunchArmsIntoSky();
        }

        public void Update()
        {
            _stateMachine.TickCooldowns(Time.deltaTime);

            // Stomp check while arms are in the sky
            if (_boss.DistanceToPlayer <= _boss.Config.StompRadius && _stateMachine.StompCooldownTimer <= 0f)
            {
                _boss.TriggerStompDamage();
                _stateMachine.StompCooldownTimer = _boss.Config.StompCooldown * _boss.CurrentCooldownMultiplier;
            }
        }

        public void FixedUpdate()
        {
            // Body continues to pursue player while arms barrage
            float speed = _boss.Config.MoveSpeed * _boss.CurrentSpeedMultiplier;
            _boss.Movement.MoveTowards(_boss.PlayerPosition, speed, _boss.Config.RotationSpeed);
            _boss.Animator?.SetMoving(true, speed);
        }

        public void Exit()
        {
            KillActiveSequence();
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

        private void LaunchArmsIntoSky()
        {
            _boss.AudioClipPlayer?.PlayOneShot(GolemBossConstants.ROAR_SFX_KEY);

            float airTime = _boss.Config.SkyArmLaunchAirTime;
            float launchHeight = 40f;
            int armsLaunched = 0;

            Action onLaunchComplete = () =>
            {
                armsLaunched++;
                if (armsLaunched >= 2)
                {
                    ExecuteDropCycle();
                }
            };

            if (_boss.Arms.LeftArm != null)
            {
                _boss.Arms.LeftArm.LaunchToSky(launchHeight, airTime, onLaunchComplete);
            }
            else
            {
                armsLaunched++;
            }

            if (_boss.Arms.RightArm != null)
            {
                _boss.Arms.RightArm.LaunchToSky(launchHeight, airTime, onLaunchComplete);
            }
            else
            {
                armsLaunched++;
            }
        }

        private void ExecuteDropCycle()
        {
            _armsSlammedInCurrentCycle = 0;
            _currentCycle++;

            float jitterDelay = UnityEngine.Random.Range(_boss.Config.SkyArmJitterMinDelay, _boss.Config.SkyArmJitterMaxDelay);

            // Arm 1 drop
            DropSingleArm(_boss.Arms.LeftArm, 0f);

            // Arm 2 drop with jitter delay
            DropSingleArm(_boss.Arms.RightArm, jitterDelay);
        }

        private void DropSingleArm(Arms.GolemArmProjectile arm, float delay)
        {
            if (arm == null)
            {
                OnArmSlamComplete();
                return;
            }

            Sequence dropSeq = DOTween.Sequence();
            dropSeq.AppendInterval(delay);
            dropSeq.AppendCallback(() =>
            {
                Vector3 playerPos = _boss.PlayerPosition;
                float radius = _boss.Config.SkyArmImpactRadius;
                float warningDuration = _boss.Config.SkyArmWarningDuration;
                float damage = _boss.Config.SkyArmDamage;
                float fallSpeed = _boss.Config.SkyArmFallSpeed * _boss.CurrentArmSpeedMultiplier;

                Vector3 targetPos = playerPos;
                if (_boss.CircularTelegraph != null)
                {
                    targetPos = _boss.CircularTelegraph.Show(playerPos, radius, warningDuration, _boss.WorldGrid);
                }

                Sequence armFallSeq = DOTween.Sequence();
                armFallSeq.AppendInterval(warningDuration * 0.4f);
                armFallSeq.AppendCallback(() =>
                {
                    arm.DropFromSky(targetPos, fallSpeed, damage, radius, OnArmSlamComplete);
                });
            });
        }

        private void OnArmSlamComplete()
        {
            _boss.AudioClipPlayer?.PlayOneShot(GolemBossConstants.SLAM_SFX_KEY);
            _armsSlammedInCurrentCycle++;

            if (_armsSlammedInCurrentCycle >= 2)
            {
                if (_currentCycle < _totalCycles)
                {
                    // Launch back up for next cycle
                    LaunchArmsIntoSky();
                }
                else
                {
                    // Return and dock
                    ReturnArmsAndFinish();
                }
            }
        }

        private void ReturnArmsAndFinish()
        {
            int armsDocked = 0;
            Action onDocked = () =>
            {
                armsDocked++;
                if (armsDocked >= 2)
                {
                    _stateMachine.SkyBarrageCooldownTimer = _boss.Config.SkyBarrageCooldown * _boss.CurrentCooldownMultiplier;
                    _stateMachine.ChangeState(_pursuitState);
                }
            };

            if (_boss.Arms.LeftArm != null)
            {
                _boss.Arms.LeftArm.ReturnAndDock(0.8f, onDocked);
            }
            else
            {
                armsDocked++;
            }

            if (_boss.Arms.RightArm != null)
            {
                _boss.Arms.RightArm.ReturnAndDock(0.8f, onDocked);
            }
            else
            {
                armsDocked++;
            }
        }

        private void KillActiveSequence()
        {
            if (_barrageSequence != null && _barrageSequence.IsActive())
            {
                _barrageSequence.Kill();
            }
            _barrageSequence = null;
        }
    }
}
