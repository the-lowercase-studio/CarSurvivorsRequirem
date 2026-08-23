namespace Assets.Scripts.Enemies.Bosses.Golem.StateMachine
{
    public interface IGolemState
    {
        void Enter();
        void Update();
        void FixedUpdate();
        void Exit();
    }
}
