namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.StateMachine
{
    public class RailgunTowerStateMachine
    {
        private IRailgunTowerState _currentState;

        public IRailgunTowerState CurrentState
        {
            get { return _currentState; }
        }

        public void Initialize(IRailgunTowerState startingState)
        {
            _currentState = startingState;
            if (_currentState != null)
            {
                _currentState.Enter();
            }
        }

        public void ChangeState(IRailgunTowerState newState)
        {
            if (_currentState != null)
            {
                _currentState.Exit();
            }

            _currentState = newState;

            if (_currentState != null)
            {
                _currentState.Enter();
            }
        }

        public void Update()
        {
            if (_currentState != null)
            {
                _currentState.Update();
            }
        }

        public void FixedUpdate()
        {
            if (_currentState != null)
            {
                _currentState.FixedUpdate();
            }
        }
    }
}
