namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.StateMachine.States
{
    public class MortarDefeatedState : IMortarTowerState
    {
        private readonly MortarTowerBoss _boss;

        public MortarDefeatedState(MortarTowerBoss boss)
        {
            _boss = boss;
        }

        public void Enter()
        {
            _boss.DismissAllTelegraphs();

            if (_boss.BoulderHitbox != null && _boss.BoulderHitbox.IsRolling)
            {
                _boss.BoulderHitbox.Stop();
            }

            if (_boss.ShellPool != null)
            {
                _boss.ShellPool.ReturnAll();
            }
        }

        public void Exit()
        {
        }

        public void Update()
        {
        }

        public void FixedUpdate()
        {
        }
    }
}
