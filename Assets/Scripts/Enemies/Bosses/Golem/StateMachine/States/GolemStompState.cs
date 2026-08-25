using Assets.Scripts.Enemies.Bosses.Golem.Constants;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.StateMachine.States
{
    public class GolemStompState : IGolemState
    {
        private readonly IGolemBoss _boss;
        private readonly GolemStateMachine _stateMachine;

        private IGolemState _returnState;
        private float _impactTimer;
        private float _durationTimer;
        private bool _hasDealtDamage;

        public GolemStompState(IGolemBoss boss, GolemStateMachine stateMachine)
        {
            _boss = boss;
            _stateMachine = stateMachine;
        }

        public void SetReturnState(IGolemState returnState)
        {
            _returnState = returnState;
        }

        public void Enter()
        {
            _boss.Movement.CanMove = false;
            _boss.Movement.Stop();
            _boss.Movement.SetKinematic(true);
            _boss.Animator?.SetMoving(false, 0f);
            _boss.Animator?.PlayStomp();

            _hasDealtDamage = false;
            _impactTimer = GolemBossConstants.STOMP_IMPACT_DELAY;
            _durationTimer = GolemBossConstants.STOMP_TOTAL_DURATION;

            if (_boss.Animator != null)
            {
                _boss.Animator.OnStompImpact += HandleStompImpact;
            }
        }

        public void Update()
        {
            _stateMachine.TickCooldowns(Time.deltaTime);

            _impactTimer -= Time.deltaTime;
            if (_impactTimer <= 0f && !_hasDealtDamage)
            {
                ApplyStomp();
            }

            _durationTimer -= Time.deltaTime;
            if (_durationTimer <= 0f)
            {
                _stateMachine.StompCooldownTimer = _boss.Config.StompCooldown * _boss.CurrentCooldownMultiplier;
                _stateMachine.ChangeState(_returnState);
            }
        }

        public void FixedUpdate()
        {
            _boss.Movement.Stop();
        }

        public void Exit()
        {
            if (_boss.Animator != null)
            {
                _boss.Animator.OnStompImpact -= HandleStompImpact;
            }

            _hasDealtDamage = false;
            _returnState = null;
            _boss.Movement.SetKinematic(false);
            _boss.Movement.CanMove = true;
        }

        private void HandleStompImpact()
        {
            ApplyStomp();
        }

        private void ApplyStomp()
        {
            if (_hasDealtDamage)
            {
                return;
            }

            _hasDealtDamage = true;
            _boss.TriggerStompDamage();
        }
    }
}
