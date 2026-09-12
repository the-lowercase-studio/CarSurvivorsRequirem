using System;
using System.Collections.Generic;
using DG.Tweening;
using Assets.ScriptableObjects.Enemies.Bosses;
using Assets.Scripts.Audio;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies.Bosses.Towers.Arena;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Combat;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Constants;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Projectiles;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.StateMachine;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.StateMachine.States;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Shapes;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower
{
    public interface IMortarTowerBoss : IDamageable, IKnockable
    {
        IHealth Health { get; }
        MortarTowerConfigSO Config { get; }
        Transform Transform { get; }
        Transform TurretHousing { get; }
        bool IsEnraged { get; }
        event Action<IMortarTowerBoss> OnBossDefeated;
        void StartEncounter();
        void ResetEncounter();
        void SnapTurretAim(Vector3 worldTarget);
        void UpdateTurretAim(float deltaTime, float turnSpeed = 5f);
    }

    [RequireComponent(typeof(Health))]
    public class MortarTowerBoss : MonoBehaviour, IMortarTowerBoss
    {
        [Inject] private IPlayerManager _playerManager = null;
        [Inject] private readonly IGridManager _gridManager = null;
        [Inject] private readonly Reflex.Core.Container _container = null;
        [Inject] private readonly IInWorldSpaceSpawner<ExpParticleSpawner, float> _expParticleSpawner = null;

        [Header("Configuration")]
        [SerializeField] private MortarTowerConfigSO _config;

        [Header("Subsystems & Projectiles")]
        [SerializeField] private MortarShellPool _shellPool;
        [SerializeField] private MortarBoulderHitbox _boulderHitbox;
        [SerializeField] private CircularTelegraphIndicator _circularTelegraphPrefab;
        [SerializeField] private RectangularTelegraphIndicator _rectangularTelegraphPrefab;
        [SerializeField] private Transform _muzzleTransform;
        [SerializeField] private Transform _turretHousing;
        [SerializeField] private Transform _barrelTransform;
        [SerializeField] private AudioClipPlayer _audioClipPlayer;

        [Header("Visual & Feedback")]
        [SerializeField] private VFXPlayer _hitVfxPlayer;
        [SerializeField] private VFXPlayer _enrageVfxPlayer;
        [SerializeField] private VFXPlayer _fireVfxPlayer;
        [SerializeField] private VFXPlayer _deathVfxPlayer;
        [SerializeField] private Renderer[] _renderersForEnrage;

        private readonly List<ITelegraphIndicator> _activeTelegraphs = new List<ITelegraphIndicator>();
        private IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> _damageNumbersSpawner;
        private EncounterArenaController _arenaController;
        private Vector3 _initialHousingLocalPosition;
        private MortarTowerStateMachine _stateMachine;
        private MortarIdleState _idleState;
        private MortarClusterBurstState _clusterBurstState;
        private MortarDiagonalBounceState _diagonalBounceState;
        private MortarRollingBoulderState _rollingBoulderState;
        private MortarCooldownState _cooldownState;
        private MortarDefeatedState _defeatedState;
        private MaterialPropertyBlock _materialPropertyBlock;
        private Tween _barrelRecoilTween;
        private Sequence _housingRecoilSequence;
        private bool _isEnraged;

        public event Action<IMortarTowerBoss> OnBossDefeated;

        public IHealth Health { get; private set; }

        public MortarTowerConfigSO Config
        {
            get
            {
                return _config;
            }
        }

        public Transform Transform
        {
            get
            {
                return transform;
            }
        }

        public Transform TurretHousing
        {
            get
            {
                return _turretHousing;
            }
        }

        public bool IsEnraged
        {
            get
            {
                return _isEnraged;
            }
        }

        public Grid WorldGrid
        {
            get
            {
                return _gridManager?.WorldGrid;
            }
        }

        public Vector3 MuzzlePosition
        {
            get
            {
                return _muzzleTransform != null ? _muzzleTransform.position : transform.position + Vector3.up * 4f;
            }
        }

        public IMortarShellPool ShellPool
        {
            get
            {
                return _shellPool;
            }
        }

        public IMortarBoulderHitbox BoulderHitbox
        {
            get
            {
                return _boulderHitbox;
            }
        }

        public MortarIdleState IdleState
        {
            get
            {
                return _idleState;
            }
        }

        public MortarClusterBurstState ClusterBurstState
        {
            get
            {
                return _clusterBurstState;
            }
        }

        public MortarDiagonalBounceState DiagonalBounceState
        {
            get
            {
                return _diagonalBounceState;
            }
        }

        public MortarRollingBoulderState RollingBoulderState
        {
            get
            {
                return _rollingBoulderState;
            }
        }

        public MortarCooldownState CooldownState
        {
            get
            {
                return _cooldownState;
            }
        }

        public MortarDefeatedState DefeatedState
        {
            get
            {
                return _defeatedState;
            }
        }

        public Vector3 PlayerPosition
        {
            get
            {
                if (_playerManager != null && _playerManager.GameObject != null)
                {
                    return _playerManager.GameObject.transform.position;
                }
                return transform.position;
            }
        }

        private void Awake()
        {
            Health = GetComponent<IHealth>();
            _arenaController = GetComponent<EncounterArenaController>();
            _materialPropertyBlock = new MaterialPropertyBlock();

            _initialHousingLocalPosition = _turretHousing != null ? _turretHousing.localPosition : Vector3.zero;

            if (_boulderHitbox != null && !_boulderHitbox.gameObject.scene.IsValid())
            {
                _boulderHitbox = Instantiate(_boulderHitbox, transform);
                _boulderHitbox.gameObject.SetActive(false);
            }

            ResolveOptionalDependencies();
            InitializeStateMachine();
        }

        private void ResolveOptionalDependencies()
        {
            if (_container != null)
            {
                if (_damageNumbersSpawner == null && _container.HasBinding<IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>>())
                {
                    _damageNumbersSpawner = _container.Resolve<IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>>();
                }

                if (_playerManager == null && _container.HasBinding<IPlayerManager>())
                {
                    _playerManager = _container.Resolve<IPlayerManager>();
                }
            }

            if (_playerManager == null)
            {
                _playerManager = FindAnyObjectByType<PlayerManager>();
            }
        }

        private void Start()
        {
            MarkFootprintImpassable();
        }

        private void OnEnable()
        {
            if (Health != null && _config != null)
            {
                Health.MaxHealth = _config.MaxHealth;
                float needed = Health.MaxHealth - Health.CurrentHealth;
                if (needed > 0f)
                {
                    Health.IncreaseHealth(needed);
                }
                Health.OnHealthChanged += Health_OnHealthChanged;
                Health.OnNoHealth += Health_OnNoHealth;
            }

            _isEnraged = false;
            _stateMachine.Initialize(_idleState);
        }

        private void OnDisable()
        {
            if (Health != null)
            {
                Health.OnHealthChanged -= Health_OnHealthChanged;
                Health.OnNoHealth -= Health_OnNoHealth;
            }

            KillRecoilTweens();
            DismissAllTelegraphs();

            if (_boulderHitbox != null && _boulderHitbox.IsRolling)
            {
                _boulderHitbox.Stop();
            }
        }

        private void OnDestroy()
        {
            KillRecoilTweens();
            DismissAllTelegraphs();
        }

        private void Update()
        {
            _stateMachine.Update();

            if (_stateMachine.CurrentState != null && _stateMachine.CurrentState != _idleState && _stateMachine.CurrentState != _defeatedState)
            {
                UpdateTurretAim(Time.deltaTime);
            }
            else if (_stateMachine.CurrentState == _idleState && _arenaController == null)
            {
                if (_playerManager != null && _playerManager.GameObject != null)
                {
                    Vector3 diff = _playerManager.GameObject.transform.position - transform.position;
                    diff.y = 0f;
                    if (diff.sqrMagnitude <= MortarTowerConstants.DEFAULT_ARENA_RADIUS * MortarTowerConstants.DEFAULT_ARENA_RADIUS)
                    {
                        StartEncounter();
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            _stateMachine.FixedUpdate();
        }

        public void StartEncounter()
        {
            _cooldownState.ResetAttackCounter();
            _stateMachine.ChangeState(_clusterBurstState);
        }

        public void ResetEncounter()
        {
            if (Health != null && _config != null)
            {
                float needed = Health.MaxHealth - Health.CurrentHealth;
                if (needed > 0f)
                {
                    Health.IncreaseHealth(needed);
                }
            }

            _isEnraged = false;
            ResetMaterials();
            DismissAllTelegraphs();

            if (_turretHousing != null)
            {
                _turretHousing.localPosition = _initialHousingLocalPosition;
            }

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

        public void ChangeState(IMortarTowerState newState)
        {
            _stateMachine.ChangeState(newState);
        }

        public void TakeDamage(float damage)
        {
            if (Health == null || !Health.IsAlive())
            {
                return;
            }

            if (_stateMachine.CurrentState == _idleState)
            {
                if (_arenaController != null)
                {
                    _arenaController.NotifyDirectCombatEngaged();
                }
                else
                {
                    StartEncounter();
                }
            }

            Vector3 spawnPos = transform.position + Vector3.up * 2f;

            if (_damageNumbersSpawner != null)
            {
                _damageNumbersSpawner.Spawn(
                    spawnPos,
                    new DamageNubmersSpawnerConfig(damage, ShapeModes.Hemisphere)
                );
            }

            Health.DecreaseHealth(damage);

            if (Health.IsAlive() && _hitVfxPlayer != null)
            {
                _hitVfxPlayer.Play(new VFXPlayConfig());
            }
        }

        public void TakeFullHpDamage()
        {
            if (Health != null)
            {
                TakeDamage(Health.MaxHealth);
            }
        }

        public void ApplyKnockBack(Vector3 direction, float power, float timeToArriveAtLocation)
        {
            // The Mortar Tower is an immovable stationary fortification; immune to knockback
        }

        public CircularTelegraphIndicator ShowCircularTelegraph(Vector3 position, float radius, float duration, Action onImpact = null, bool autoContractOnFillComplete = true)
        {
            if (_circularTelegraphPrefab == null)
            {
                return null;
            }

            PruneInactiveTelegraphs();

            CircularTelegraphIndicator indicator = Instantiate(_circularTelegraphPrefab);
            _activeTelegraphs.Add(indicator);
            indicator.Show(position, radius, duration, WorldGrid, onImpact, autoContractOnFillComplete);
            return indicator;
        }

        public RectangularTelegraphIndicator ShowRectangularTelegraph(Vector3 origin, Vector3 direction, float length, float width, float duration, Action onImpact = null, bool autoContractOnFillComplete = true)
        {
            if (_rectangularTelegraphPrefab == null)
            {
                return null;
            }

            PruneInactiveTelegraphs();

            RectangularTelegraphIndicator indicator = Instantiate(_rectangularTelegraphPrefab);
            _activeTelegraphs.Add(indicator);
            indicator.Show(origin, direction, length, width, duration, onImpact, autoContractOnFillComplete);
            return indicator;
        }

        public void DismissAllTelegraphs()
        {
            for (int i = _activeTelegraphs.Count - 1; i >= 0; i--)
            {
                ITelegraphIndicator indicator = _activeTelegraphs[i];
                if (indicator != null && indicator is UnityEngine.Object unityObj && unityObj != null)
                {
                    indicator.Dismiss();
                }
            }
            _activeTelegraphs.Clear();
        }

        private void PruneInactiveTelegraphs()
        {
            for (int i = _activeTelegraphs.Count - 1; i >= 0; i--)
            {
                ITelegraphIndicator ind = _activeTelegraphs[i];
                if (ind == null || (ind is UnityEngine.Object uObj && uObj == null))
                {
                    _activeTelegraphs.RemoveAt(i);
                }
            }
        }

        public void PlayFireRecoil()
        {
            if (_barrelTransform != null)
            {
                _barrelRecoilTween?.Kill(true);
                _barrelRecoilTween = _barrelTransform.DOPunchRotation(new Vector3(-15f, 0f, 0f), 0.35f, 4, 0.5f);
            }

            if (_turretHousing != null)
            {
                _housingRecoilSequence?.Kill(true);
                _housingRecoilSequence = DOTween.Sequence();
                Vector3 recoilOffset = _initialHousingLocalPosition - _turretHousing.forward * 0.35f;
                _housingRecoilSequence.Append(_turretHousing.DOLocalMove(recoilOffset, 0.1f).SetEase(Ease.OutQuad));
                _housingRecoilSequence.Append(_turretHousing.DOLocalMove(_initialHousingLocalPosition, 0.4f).SetEase(Ease.InOutSine));
            }

            if (_fireVfxPlayer != null)
            {
                _fireVfxPlayer.Play(new VFXPlayConfig());
            }

            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.PlayOneShot(MortarTowerConstants.SFX_MORTAR_FIRE);
            }
        }

        private void InitializeStateMachine()
        {
            _stateMachine = new MortarTowerStateMachine();
            _idleState = new MortarIdleState(this);
            _clusterBurstState = new MortarClusterBurstState(this);
            _diagonalBounceState = new MortarDiagonalBounceState(this);
            _rollingBoulderState = new MortarRollingBoulderState(this);
            _cooldownState = new MortarCooldownState(this);
            _defeatedState = new MortarDefeatedState(this);
        }

        private void MarkFootprintImpassable()
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
            const int FOOTPRINT_RADIUS = 2;

            for (int dx = -FOOTPRINT_RADIUS; dx <= FOOTPRINT_RADIUS; dx++)
            {
                for (int dy = -FOOTPRINT_RADIUS; dy <= FOOTPRINT_RADIUS; dy++)
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

        private void Health_OnHealthChanged(object sender, EventArgs e)
        {
            if (Health == null || !Health.IsAlive() || _config == null)
            {
                return;
            }

            float healthPercentage = Health.CurrentHealth / Health.MaxHealth;
            if (healthPercentage <= _config.EnrageHealthPercent && !_isEnraged)
            {
                TriggerEnrage();
            }
        }

        private void TriggerEnrage()
        {
            _isEnraged = true;

            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.PlayOneShot(MortarTowerConstants.SFX_ENRAGE_ROAR);
            }

            if (_enrageVfxPlayer != null)
            {
                _enrageVfxPlayer.Play(new VFXPlayConfig());
            }

            ApplyEnrageMaterials();
        }

        private void ApplyEnrageMaterials()
        {
            if (_renderersForEnrage == null || _config == null)
            {
                return;
            }

            for (int i = 0; i < _renderersForEnrage.Length; i++)
            {
                Renderer rend = _renderersForEnrage[i];
                if (rend == null) continue;

                rend.GetPropertyBlock(_materialPropertyBlock);
                _materialPropertyBlock.SetColor(MortarTowerConstants.BASE_COLOR_PROPERTY, _config.EnrageColor);
                _materialPropertyBlock.SetColor(MortarTowerConstants.EMISSION_COLOR_PROPERTY, _config.EnrageEmissionColor);
                rend.SetPropertyBlock(_materialPropertyBlock);
            }
        }

        private void ResetMaterials()
        {
            if (_renderersForEnrage == null)
            {
                return;
            }

            for (int i = 0; i < _renderersForEnrage.Length; i++)
            {
                Renderer rend = _renderersForEnrage[i];
                if (rend == null) continue;

                rend.SetPropertyBlock(null);
            }
        }

        private void Health_OnNoHealth(object sender, EventArgs e)
        {
            _stateMachine.ChangeState(_defeatedState);

            if (_deathVfxPlayer != null)
            {
                _deathVfxPlayer.transform.SetParent(null);
                _deathVfxPlayer.Play(new VFXPlayConfig(destroyOnEnd: true));
            }

            if (_expParticleSpawner != null && _config != null)
            {
                _expParticleSpawner.Spawn(transform.position, _config.ExpReward);
            }

            OnBossDefeated?.Invoke(this);
            gameObject.SetActive(false);
        }

        private void KillRecoilTweens()
        {
            if (_barrelRecoilTween != null && _barrelRecoilTween.IsActive())
            {
                _barrelRecoilTween.Kill();
            }
            _barrelRecoilTween = null;

            if (_housingRecoilSequence != null && _housingRecoilSequence.IsActive())
            {
                _housingRecoilSequence.Kill();
            }
            _housingRecoilSequence = null;

            if (_turretHousing != null)
            {
                _turretHousing.localPosition = _initialHousingLocalPosition;
            }
        }

        public void UpdateTurretAim(float deltaTime, float turnSpeed = 5f)
        {
            if (_turretHousing == null)
            {
                return;
            }

            Vector3 targetDir = PlayerPosition - _turretHousing.position;
            targetDir.y = 0f;

            if (targetDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);
                _turretHousing.rotation = Quaternion.Slerp(_turretHousing.rotation, targetRot, turnSpeed * deltaTime);
            }
        }

        public void SnapTurretAim(Vector3 worldTarget)
        {
            if (_turretHousing == null)
            {
                return;
            }

            Vector3 targetDir = worldTarget - _turretHousing.position;
            targetDir.y = 0f;

            if (targetDir.sqrMagnitude > 0.001f)
            {
                _turretHousing.rotation = Quaternion.LookRotation(targetDir, Vector3.up);
            }
        }
    }
}
