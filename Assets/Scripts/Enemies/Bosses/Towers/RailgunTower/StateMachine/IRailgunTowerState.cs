namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.StateMachine
{
    public interface IRailgunTowerState
    {
        void Enter();
        void Exit();
        void Update();
        void FixedUpdate();
    }
}
