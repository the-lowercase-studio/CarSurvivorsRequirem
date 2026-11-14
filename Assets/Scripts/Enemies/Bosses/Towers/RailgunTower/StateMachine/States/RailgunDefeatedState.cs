namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.StateMachine.States
{
    public class RailgunDefeatedState : IRailgunTowerState
    {
        private readonly IRailgunTowerBoss _boss;

        public RailgunDefeatedState(IRailgunTowerBoss boss)
        {
            _boss = boss;
        }

        public void Enter()
        {
            _boss.StopVisualsAndIndicators();

            if (_boss.OrbPool != null)
            {
                _boss.OrbPool.ReturnAll();
            }

            if (_boss.WavePool != null)
            {
                _boss.WavePool.ReturnAll();
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
