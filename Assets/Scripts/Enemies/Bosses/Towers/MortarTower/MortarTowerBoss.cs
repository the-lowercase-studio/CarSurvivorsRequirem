using Assets.ScriptableObjects.Enemies.Bosses;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Combat;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Constants;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Projectiles;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.StateMachine;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.StateMachine.States;
using Assets.Scripts.Navigation.GridSystem;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower
{
    public interface IMortarTowerBoss : ITowerBoss
    {
        MortarTowerConfigSO Config { get; }
    }

    public class MortarTowerBoss : TowerBossBase, IMortarTowerBoss
    {
        [Header("Configuration")]
        [SerializeField] private MortarTowerConfigSO _config;

        [Header("Subsystems & Projectiles")]
        [SerializeField] private MortarShellPool _shellPool;
        [SerializeField] private MortarBoulderHitbox _boulderHitbox;

        private MortarTowerStateMachine _stateMachine;
        private MortarIdleState _idleState;
        private MortarClusterBurstState _clusterBurstState;
        private MortarDiagonalBounceState _diagonalBounceState;
        private MortarRollingBoulderState _rollingBoulderState;
        private MortarCooldownState _cooldownState;
        private MortarDefeatedState _defeatedState;

        public override TowerBossConfigSO BaseConfig
        {
            get { return _config; }
        }

        public MortarTowerConfigSO Config
        {
            get { return _config; }
        }

        public IMortarShellPool ShellPool
        {
            get { return _shellPool; }
        }

        public IMortarBoulderHitbox BoulderHitbox
        {
            get { return _boulderHitbox; }
        }

        public MortarIdleState IdleState
        {
            get { return _idleState; }
        }

        public MortarClusterBurstState ClusterBurstState
        {
            get { return _clusterBurstState; }
        }

        public MortarDiagonalBounceState DiagonalBounceState
        {
            get { return _diagonalBounceState; }
        }

        public MortarRollingBoulderState RollingBoulderState
        {
            get { return _rollingBoulderState; }
        }

        public MortarCooldownState CooldownState
        {
            get { return _cooldownState; }
        }

        public MortarDefeatedState DefeatedState
        {
            get { return _defeatedState; }
        }

        protected override string EnrageSfxKey
        {
            get { return MortarTowerConstants.SFX_ENRAGE_ROAR; }
        }

        protected override bool ShouldUpdateTurretAim
        {
            get
            {
                return _stateMachine != null
                    && _stateMachine.CurrentState != null
                    && _stateMachine.CurrentState != _idleState
                    && _stateMachine.CurrentState != _defeatedState;
            }
        }

        protected override bool IsIdleStateActive
        {
            get { return _stateMachine != null && _stateMachine.CurrentState == _idleState; }
        }

        public override void StartEncounter()
        {
            _cooldownState.ResetAttackCounter();
            _stateMachine.ChangeState(_clusterBurstState);
        }

        public void ChangeState(IMortarTowerState newState)
        {
            _stateMachine.ChangeState(newState);
        }

        public void PlayFireRecoil()
        {
            base.PlayFireRecoil(MortarTowerConstants.SFX_MORTAR_FIRE);
        }

        protected override void InitializeTower()
        {
            if (_boulderHitbox != null && !_boulderHitbox.gameObject.scene.IsValid())
            {
                _boulderHitbox = Instantiate(_boulderHitbox, transform);
                _boulderHitbox.gameObject.SetActive(false);
            }

            _stateMachine = new MortarTowerStateMachine();
            _idleState = new MortarIdleState(this);
            _clusterBurstState = new MortarClusterBurstState(this);
            _diagonalBounceState = new MortarDiagonalBounceState(this);
            _rollingBoulderState = new MortarRollingBoulderState(this);
            _cooldownState = new MortarCooldownState(this);
            _defeatedState = new MortarDefeatedState(this);
        }

        protected override void InitializeStateMachineForEnable()
        {
            _stateMachine.Initialize(_idleState);
        }

        protected override void UpdateTowerStateMachine()
        {
            _stateMachine.Update();
        }

        protected override void FixedUpdateTowerStateMachine()
        {
            _stateMachine.FixedUpdate();
        }

        protected override void OnBossReset()
        {
            if (_boulderHitbox != null && _boulderHitbox.IsRolling)
            {
                _boulderHitbox.Stop();
            }

            if (_shellPool != null)
            {
                _shellPool.ReturnAll();
            }

            _cooldownState.ResetAttackCounter();
            _stateMachine.ChangeState(_idleState);
        }

        protected override void OnBossDisabled()
        {
            if (_boulderHitbox != null && _boulderHitbox.IsRolling)
            {
                _boulderHitbox.Stop();
            }
        }

        protected override void OnBossDefeatedCleanup()
        {
            _stateMachine.ChangeState(_defeatedState);
        }

        protected override void MarkFootprintImpassable()
        {
            Grid grid = WorldGrid;
            if (grid == null || grid.Cells == null)
            {
                return;
            }

            Cell centerCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(grid, transform.position);
            if (centerCell == null)
            {
                return;
            }

            int cx = centerCell.WorldGridPos.x;
            int cy = centerCell.WorldGridPos.y;
            for (int dx = -MortarTowerConstants.FOOTPRINT_RADIUS; dx <= MortarTowerConstants.FOOTPRINT_RADIUS; dx++)
            {
                for (int dy = -MortarTowerConstants.FOOTPRINT_RADIUS; dy <= MortarTowerConstants.FOOTPRINT_RADIUS; dy++)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;

                    if (nx >= 0 && nx < grid.Width && ny >= 0 && ny < grid.Height)
                    {
                        Cell c = grid.Cells[nx, ny];
                        if (c != null)
                        {
                            c.Cost = byte.MaxValue;
                        }
                    }
                }
            }
        }
    }
}
