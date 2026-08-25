using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.StateMachine.States
{
    public class GolemPursuitState : IGolemState
    {
        private readonly IGolemBoss _boss;
        private readonly GolemStateMachine _stateMachine;
        private readonly GolemLeapSlamState _leapSlamState;
        private readonly GolemLinearFistState _linearFistState;
        private readonly GolemSkyBarrageState _skyBarrageState;
        private readonly GolemStompState _stompState;

        public GolemPursuitState(
            IGolemBoss boss,
            GolemStateMachine stateMachine,
            GolemLeapSlamState leapSlamState,
            GolemLinearFistState linearFistState,
            GolemSkyBarrageState skyBarrageState,
            GolemStompState stompState)
        {
            _boss = boss;
            _stateMachine = stateMachine;
            _leapSlamState = leapSlamState;
            _linearFistState = linearFistState;
            _skyBarrageState = skyBarrageState;
            _stompState = stompState;
        }

        public void Enter()
        {
            _boss.Movement.CanMove = true;
            _boss.Animator?.SetMoving(true, _boss.Config.MoveSpeed * _boss.CurrentSpeedMultiplier);
        }

        public void Update()
        {
            _stateMachine.TickCooldowns(Time.deltaTime);

            // Attack Lock: Do not initiate any new attack while any non-moving/attack animation is active
            if (_boss.Animator != null && !_boss.Animator.IsMovingAnimationPlaying)
            {
                return;
            }

            // Attack 2: Melee Stomp Check (works anytime player is close and arms are docked or moving)
            if (_boss.DistanceToPlayer <= _boss.Config.StompRadius && _stateMachine.StompCooldownTimer <= 0f)
            {
                if (_stompState != null)
                {
                    _stompState.SetReturnState(this);
                    _stateMachine.ChangeState(_stompState);
                    return;
                }

                _boss.Animator?.PlayStomp();
                _boss.TriggerStompDamage();
                _stateMachine.StompCooldownTimer = _boss.Config.StompCooldown * _boss.CurrentCooldownMultiplier;
            }

            // Anti-kiting Leap Trigger: If player is far away, immediately prioritize leap slam
            if (_boss.DistanceToPlayer >= _boss.Config.LeapTriggerMaxDistance && _stateMachine.LeapCooldownTimer <= 0f)
            {
                _stateMachine.ChangeState(_leapSlamState);
                return;
            }

            // Rotational attack selection when arms are available
            if (_boss.Arms.AreBothArmsDocked)
            {
                if (_stateMachine.SkyBarrageCooldownTimer <= 0f)
                {
                    _stateMachine.ChangeState(_skyBarrageState);
                    return;
                }

                if (_stateMachine.LinearFistCooldownTimer <= 0f && _boss.DistanceToPlayer <= _boss.Config.LinearFistMaxDistance * 1.3f)
                {
                    _stateMachine.ChangeState(_linearFistState);
                    return;
                }

                if (_stateMachine.LeapCooldownTimer <= 0f)
                {
                    _stateMachine.ChangeState(_leapSlamState);
                    return;
                }
            }
        }

        public void FixedUpdate()
        {
            if (!_boss.Movement.CanMove || (_boss.Animator != null && !_boss.Animator.IsMovingAnimationPlaying))
            {
                _boss.Movement.Stop();
                _boss.Animator?.SetMoving(false, 0f);
                return;
            }

            float speed = _boss.Config.MoveSpeed * _boss.CurrentSpeedMultiplier;
            _boss.Movement.MoveTowards(_boss.PlayerPosition, speed, _boss.Config.RotationSpeed);
            _boss.Animator?.SetMoving(true, speed);
        }

        public void Exit()
        {
            _boss.Movement.Stop();
            _boss.Animator?.SetMoving(false, 0f);
        }
    }
}
