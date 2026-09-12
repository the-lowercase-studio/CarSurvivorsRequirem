namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.StateMachine
{
    public class MortarTowerStateMachine
    {
        private IMortarTowerState _currentState;

        public IMortarTowerState CurrentState
        {
            get
            {
                return _currentState;
            }
        }

        public void Initialize(IMortarTowerState startingState)
        {
            _currentState = startingState;
            if (_currentState != null)
            {
                _currentState.Enter();
            }
        }

        public void ChangeState(IMortarTowerState newState)
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
