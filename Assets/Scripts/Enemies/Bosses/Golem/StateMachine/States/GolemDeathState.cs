using Assets.Scripts.Enemies.Bosses.Golem.Constants;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.StateMachine.States
{
    public class GolemDeathState : IGolemState
    {
        private readonly IGolemBoss _boss;

        public GolemDeathState(IGolemBoss boss)
        {
            _boss = boss;
        }

        public void Enter()
        {
            _boss.Movement.CanMove = false;
            _boss.Movement.Stop();
            _boss.Animator?.SetMoving(false, 0f);

            _boss.DismissAllTelegraphs();
            _boss.Arms?.ResetAllArms();

            _boss.AudioClipPlayer?.PlayOneShot(GolemBossConstants.DEATH_SFX_KEY);
        }

        public void Update()
        {
        }

        public void FixedUpdate()
        {
        }

        public void Exit()
        {
        }
    }
}
