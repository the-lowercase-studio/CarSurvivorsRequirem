namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.StateMachine
{
    public interface IMortarTowerState
    {
        void Enter();
        void Exit();
        void Update();
        void FixedUpdate();
    }
}
