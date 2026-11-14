using System;
using System.Collections;
using Assets.ScriptableObjects.Enemies.Bosses;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Combat;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Constants;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Projectiles;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.StateMachine;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.StateMachine.States;
using Assets.Scripts.Indicators;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player.Car;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower
{
    public interface IRailgunTowerBoss : ITowerBoss
    {
        RailgunTowerConfigSO Config { get; }
        float PlayerMaxForwardSpeed { get; }
        bool IsTimeFrozen { get; }
        Vector3 MuzzlePosition { get; }
        IRailgunOrbPool OrbPool { get; }
        IRailgunWavePool WavePool { get; }
        LineRenderer LaserSweepLine { get; }
        LineRenderer ChaseBeamLine { get; }
        CircularTelegraphIndicator ReusableChaseIndicator { get; }
        RectangularTelegraphIndicator ReusableSweepIndicator { get; }
        ArcTelegraphIndicator ReusableSweepArcIndicator { get; }
        RailgunArcLaserBeam ArcLaserBeam { get; }
        Transform ChaseAnchor { get; }
        RailgunIdleState IdleState { get; }
        RailgunCooldownState CooldownState { get; }
        RailgunLaserSweepState LaserSweepState { get; }
        RailgunChasingLaserState ChasingLaserState { get; }
        RailgunExplosiveCrossBurstState ExplosiveCrossBurstState { get; }
        RailgunDefeatedState DefeatedState { get; }
        void ChangeState(IRailgunTowerState newState);
        void PlayFireFeedback(string sfxKey, bool includeBarrelRecoil = true);
        void PlayBurstFireFeedback(string sfxKey, float basePitchDegrees, float duration);
        void PlayAudioOneShot(string sfxKey);
        void StopVisualsAndIndicators();
        CircularTelegraphIndicator ShowCircularTelegraph(Vector3 position, float radius, float duration, Action onImpact = null, bool autoContractOnFillComplete = true);
        Coroutine StartBossCoroutine(IEnumerator routine);
        void StopBossCoroutine(Coroutine routine);
    }

    public class RailgunTowerBoss : TowerBossBase, IRailgunTowerBoss
    {
        [Header("Configuration")]
        [Tooltip("Designer-authored Railgun Tower Boss configuration asset.")]
        [SerializeField] private RailgunTowerConfigSO _config;

        [Header("Railgun Subsystems & Colliders")]
        [Tooltip("Object pool for Attack 3 explosive energy orbs.")]
        [SerializeField] private RailgunOrbPool _orbPool;
        [Tooltip("Object pool for Attack 3 bidirectional energy wave pairs.")]
        [SerializeField] private RailgunWavePool _wavePool;
        [Tooltip("Two physical BoxColliders aligned to the left and right gantry legs (leaving the central bridge lane open).")]
        [SerializeField] private BoxCollider[] _legColliders;
        [Tooltip("LineRenderer displaying the elevated sweeping laser beam in Attack 1.")]
        [SerializeField] private LineRenderer _laserSweepLine;
        [Tooltip("LineRenderer displaying the elevated chasing lock-on laser beam in Attack 2.")]
        [SerializeField] private LineRenderer _chaseBeamLine;
        [Tooltip("Reusable circular indicator used for the chasing laser circle in Attack 2.")]
        [SerializeField] private CircularTelegraphIndicator _reusableChaseIndicator;
        [Tooltip("Reusable rectangular indicator used for the continuous sweeping lane in Attack 1.")]
        [SerializeField] private RectangularTelegraphIndicator _reusableSweepIndicator;
        [Tooltip("Reusable arc indicator used to telegraph the entire Attack 1 sweep sector before sweeping.")]
        [SerializeField] private ArcTelegraphIndicator _reusableSweepArcIndicator;
        [Tooltip("Procedural 3D arc laser fan and ground band used during the active Attack 1 radial sweep.")]
        [SerializeField] private RailgunArcLaserBeam _arcLaserBeam;

        private ICarController _playerCarController;
        private Transform _chaseAnchor;
        private RailgunTowerStateMachine _stateMachine;
        private RailgunIdleState _idleState;
        private RailgunCooldownState _cooldownState;
        private RailgunLaserSweepState _laserSweepState;
        private RailgunChasingLaserState _chasingLaserState;
        private RailgunExplosiveCrossBurstState _explosiveCrossBurstState;
        private RailgunDefeatedState _defeatedState;

        public override TowerBossConfigSO BaseConfig
        {
            get { return _config; }
        }

        public RailgunTowerConfigSO Config
        {
            get { return _config; }
        }

        public float PlayerMaxForwardSpeed
        {
            get
            {
                if (_playerCarController == null && PlayerManager != null && PlayerManager.GameObject != null)
                {
                    _playerCarController = PlayerManager.GameObject.GetComponent<ICarController>();
                }

                return _playerCarController != null
                    ? _playerCarController.MaxForwardSpeed
                    : RailgunTowerConstants.DEFAULT_PLAYER_MAX_FORWARD_SPEED;
            }
        }

        public bool IsTimeFrozen
        {
            get { return TimeFreezeState != null && TimeFreezeState.IsFrozen; }
        }

        public IRailgunOrbPool OrbPool
        {
            get { return _orbPool; }
        }

        public IRailgunWavePool WavePool
        {
            get { return _wavePool; }
        }

        public LineRenderer LaserSweepLine
        {
            get { return _laserSweepLine; }
        }

        public LineRenderer ChaseBeamLine
        {
            get { return _chaseBeamLine; }
        }

        public CircularTelegraphIndicator ReusableChaseIndicator
        {
            get { return _reusableChaseIndicator; }
        }

        public RectangularTelegraphIndicator ReusableSweepIndicator
        {
            get { return _reusableSweepIndicator; }
        }

        public ArcTelegraphIndicator ReusableSweepArcIndicator
        {
            get { return _reusableSweepArcIndicator; }
        }

        public RailgunArcLaserBeam ArcLaserBeam
        {
            get { return _arcLaserBeam; }
        }

        public Transform ChaseAnchor
        {
            get { return _chaseAnchor; }
        }

        public RailgunIdleState IdleState
        {
            get { return _idleState; }
        }

        public RailgunCooldownState CooldownState
        {
            get { return _cooldownState; }
        }

        public RailgunLaserSweepState LaserSweepState
        {
            get { return _laserSweepState; }
        }

        public RailgunChasingLaserState ChasingLaserState
        {
            get { return _chasingLaserState; }
        }

        public RailgunExplosiveCrossBurstState ExplosiveCrossBurstState
        {
            get { return _explosiveCrossBurstState; }
        }

        public RailgunDefeatedState DefeatedState
        {
            get { return _defeatedState; }
        }

        protected override float FallbackMuzzleHeight
        {
            get { return RailgunTowerConstants.FALLBACK_MUZZLE_HEIGHT; }
        }

        protected override float DamageNumberYOffset
        {
            get { return RailgunTowerConstants.DAMAGE_NUMBER_Y_OFFSET; }
        }

        protected override string EnrageSfxKey
        {
            get { return RailgunTowerConstants.SFX_ENRAGE_ROAR; }
        }

        protected override bool ShouldUpdateTurretAim
        {
            get { return _stateMachine != null && _stateMachine.CurrentState == _cooldownState; }
        }

        protected override bool IsIdleStateActive
        {
            get { return _stateMachine != null && _stateMachine.CurrentState == _idleState; }
        }

        public override void StartEncounter()
        {
            _cooldownState.ResetAttackCounter();
            _stateMachine.ChangeState(_laserSweepState);
        }

        public void ChangeState(IRailgunTowerState newState)
        {
            _stateMachine.ChangeState(newState);
        }

        public void PlayFireFeedback(string sfxKey, bool includeBarrelRecoil = true)
        {
            PlayFireRecoil(sfxKey, includeBarrelRecoil);
        }

        public void PlayBurstFireFeedback(string sfxKey, float basePitchDegrees, float duration)
        {
            PlayBurstFireRecoil(
                sfxKey,
                basePitchDegrees,
                RailgunTowerConstants.CROSS_BURST_BARREL_KICK_DEGREES,
                RailgunTowerConstants.CROSS_BURST_HOUSING_KICK_DISTANCE,
                duration
            );
        }

        public void StopVisualsAndIndicators()
        {
            if (_laserSweepLine != null)
            {
                _laserSweepLine.enabled = false;
            }

            if (_chaseBeamLine != null)
            {
                _chaseBeamLine.enabled = false;
            }

            if (_reusableSweepIndicator != null)
            {
                _reusableSweepIndicator.HidePersistent();
            }

            if (_reusableSweepArcIndicator != null)
            {
                _reusableSweepArcIndicator.HidePersistent();
            }

            if (_arcLaserBeam != null)
            {
                _arcLaserBeam.Hide();
            }

            if (_reusableChaseIndicator != null)
            {
                _reusableChaseIndicator.HideReusable();
            }

            ResetBarrelPitch(0f);
            DismissAllTelegraphs();
        }

        public Coroutine StartBossCoroutine(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        public void StopBossCoroutine(Coroutine routine)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        protected override void InitializeTower()
        {
            ValidateRequiredDependencies();

            GameObject chaseAnchorObject = new GameObject("RailgunChaseAnchor");
            chaseAnchorObject.transform.SetParent(transform, false);
            _chaseAnchor = chaseAnchorObject.transform;

            _laserSweepLine.enabled = false;
            _laserSweepLine.positionCount = 2;
            _laserSweepLine.useWorldSpace = true;

            _chaseBeamLine.enabled = false;
            _chaseBeamLine.positionCount = 2;
            _chaseBeamLine.useWorldSpace = true;

            _reusableChaseIndicator.SetFreezable(true);
            _reusableChaseIndicator.gameObject.SetActive(false);

            _reusableSweepIndicator.SetFreezable(true);
            _reusableSweepIndicator.HidePersistent();

            _reusableSweepArcIndicator.SetFreezable(true);
            _reusableSweepArcIndicator.HidePersistent();

            _arcLaserBeam.Hide();

            _stateMachine = new RailgunTowerStateMachine();
            _idleState = new RailgunIdleState(this);
            _cooldownState = new RailgunCooldownState(this);
            _laserSweepState = new RailgunLaserSweepState(this);
            _chasingLaserState = new RailgunChasingLaserState(this);
            _explosiveCrossBurstState = new RailgunExplosiveCrossBurstState(this);
            _defeatedState = new RailgunDefeatedState(this);
        }

        protected override void InitializeStateMachineForEnable()
        {
            StopVisualsAndIndicators();
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
            _cooldownState.ResetAttackCounter();
            _stateMachine.ChangeState(_idleState);

            StopVisualsAndIndicators();

            if (_orbPool != null)
            {
                _orbPool.ReturnAll();
            }

            if (_wavePool != null)
            {
                _wavePool.ReturnAll();
            }

            if (TurretHousing != null)
            {
                TurretHousing.localRotation = Quaternion.identity;
            }
        }

        protected override void OnBossDisabled()
        {
            if (_stateMachine != null && _idleState != null && _stateMachine.CurrentState != _idleState)
            {
                _stateMachine.ChangeState(_idleState);
            }

            StopVisualsAndIndicators();

            if (_orbPool != null)
            {
                _orbPool.ReturnAll();
            }

            if (_wavePool != null)
            {
                _wavePool.ReturnAll();
            }
        }

        protected override void OnBossDestroyed()
        {
            StopVisualsAndIndicators();
        }

        protected override void OnBossDefeatedCleanup()
        {
            _stateMachine.ChangeState(_defeatedState);
        }

        protected override void OnResolveAdditionalDependencies(Reflex.Core.Container container)
        {
            if (_playerCarController == null && PlayerManager != null && PlayerManager.GameObject != null)
            {
                _playerCarController = PlayerManager.GameObject.GetComponent<ICarController>();
            }
        }

        protected override void MarkFootprintImpassable()
        {
            Grid grid = WorldGrid;
            if (grid == null || grid.Cells == null || _legColliders == null)
            {
                return;
            }

            float cellSize = grid.CellSize;
            float halfCell = cellSize * 0.5f;

            for (int i = 0; i < _legColliders.Length; i++)
            {
                BoxCollider legCollider = _legColliders[i];
                if (legCollider == null)
                {
                    continue;
                }

                Bounds worldBounds = legCollider.bounds;
                Vector3 margin = new Vector3(
                    RailgunTowerConstants.FOOTPRINT_CELL_PADDING + halfCell,
                    0f,
                    RailgunTowerConstants.FOOTPRINT_CELL_PADDING + halfCell
                );

                Cell minCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(grid, worldBounds.min - margin);
                Cell maxCell = WorldPosToCellConverter.GetCellFromGridByWorldPos(grid, worldBounds.max + margin);
                if (minCell == null || maxCell == null)
                {
                    continue;
                }

                int minX = Mathf.Max(0, Mathf.Min(minCell.WorldGridPos.x, maxCell.WorldGridPos.x));
                int maxX = Mathf.Min(grid.Width - 1, Mathf.Max(minCell.WorldGridPos.x, maxCell.WorldGridPos.x));
                int minY = Mathf.Max(0, Mathf.Min(minCell.WorldGridPos.y, maxCell.WorldGridPos.y));
                int maxY = Mathf.Min(grid.Height - 1, Mathf.Max(minCell.WorldGridPos.y, maxCell.WorldGridPos.y));

                Transform legTransform = legCollider.transform;
                Vector3 localCenter = legCollider.center;
                Vector3 localHalf = legCollider.size * 0.5f;
                Vector3 lossyScale = legTransform.lossyScale;

                float padX = Mathf.Abs(lossyScale.x) > 0.0001f
                    ? RailgunTowerConstants.FOOTPRINT_CELL_PADDING / Mathf.Abs(lossyScale.x)
                    : RailgunTowerConstants.FOOTPRINT_CELL_PADDING;
                float padZ = Mathf.Abs(lossyScale.z) > 0.0001f
                    ? RailgunTowerConstants.FOOTPRINT_CELL_PADDING / Mathf.Abs(lossyScale.z)
                    : RailgunTowerConstants.FOOTPRINT_CELL_PADDING;

                float sampleY = legTransform.TransformPoint(localCenter).y;

                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        Cell cell = grid.Cells[x, y];
                        if (cell == null)
                        {
                            continue;
                        }

                        Vector3 sampleWorldPos = new Vector3(cell.WorldPos.x, sampleY, cell.WorldPos.z);
                        Vector3 localPoint = legTransform.InverseTransformPoint(sampleWorldPos) - localCenter;

                        if (Mathf.Abs(localPoint.x) <= localHalf.x + padX &&
                            Mathf.Abs(localPoint.z) <= localHalf.z + padZ)
                        {
                            cell.Cost = byte.MaxValue;
                        }
                    }
                }
            }
        }

        private void ValidateRequiredDependencies()
        {
            if (_config == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _config is unassigned.");
            }

            if (_legColliders == null || _legColliders.Length != 2 || _legColliders[0] == null || _legColliders[1] == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _legColliders must contain exactly two assigned leg BoxColliders.");
            }

            if (TurretHousing == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _turretHousing is unassigned.");
            }

            if (MuzzleTransform == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _muzzleTransform is unassigned.");
            }

            if (_orbPool == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _orbPool is unassigned.");
            }

            if (_wavePool == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _wavePool is unassigned.");
            }

            if (_laserSweepLine == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _laserSweepLine is unassigned.");
            }

            if (_chaseBeamLine == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _chaseBeamLine is unassigned.");
            }

            if (CircularTelegraphPrefab == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _circularTelegraphPrefab is unassigned.");
            }

            if (RectangularTelegraphPrefab == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _rectangularTelegraphPrefab is unassigned.");
            }

            if (_reusableChaseIndicator == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _reusableChaseIndicator is unassigned.");
            }

            if (_reusableSweepIndicator == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _reusableSweepIndicator is unassigned.");
            }

            if (_reusableSweepArcIndicator == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _reusableSweepArcIndicator is unassigned.");
            }

            if (_arcLaserBeam == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunTowerBoss)}] _arcLaserBeam is unassigned.");
            }
        }
    }
}
