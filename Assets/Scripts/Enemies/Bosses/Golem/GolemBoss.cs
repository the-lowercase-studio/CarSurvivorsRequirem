using System;
using System.Collections.Generic;
using DG.Tweening;
using Assets.Scripts.Audio;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies.Bosses.Golem.Animation;
using Assets.Scripts.Enemies.Bosses.Golem.Arms;
using Assets.Scripts.Enemies.Bosses.Golem.Combat;
using Assets.Scripts.Enemies.Bosses.Golem.Config;
using Assets.Scripts.Enemies.Bosses.Golem.Constants;
using Assets.Scripts.Enemies.Bosses.Golem.Movement;
using Assets.Scripts.Enemies.Bosses.Golem.StateMachine;
using Assets.Scripts.Enemies.Bosses.Golem.StateMachine.States;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Player;
using Assets.Scripts.Shapes;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Enemies.Bosses.Golem
{
    public interface IDeathVolumeRecoverable
    {
        void RequestDeathVolumeRecovery();
    }

    [RequireComponent(typeof(Health), typeof(CapsuleCollider))]
    public class GolemBoss : MonoBehaviour, IGolemBoss, IDamageable, IKnockable, IDeathVolumeRecoverable
    {
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly IGridManager _gridManager = null;
        [Inject] private readonly IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> _damageNumbersSpawner = null;
        [Inject] private readonly IInWorldSpaceSpawner<ExpParticleSpawner, float> _expParticleSpawner = null;

        [Header("Configuration")]
        [SerializeField] private GolemBossConfigSO _config;

        [Header("Subsystems")]
        [SerializeField] private GolemMovementController _movementController;
        [SerializeField] private GolemArmSocketController _armSocketController;
        [SerializeField] private GolemLinearAttackHitbox _linearAttackHitbox;
        [SerializeField] private GolemAnimator _animator;
        [SerializeField] private GolemStompTrigger _stompLegTrigger;
        [SerializeField] private CircularTelegraphIndicator _circularTelegraph;
        [SerializeField] private RectangularTelegraphIndicator _rectangularTelegraph;
        [SerializeField] private AudioClipPlayer _audioClipPlayer;

        [Header("Visual & Feedback")]
        [SerializeField] private VFXPlayer _bloodVfxPlayer;
        [SerializeField] private VFXPlayer _enrageVfxPlayer;
        [SerializeField] private VFXPlayer _deathVfxPlayer;
        [SerializeField] private Renderer[] _renderersForEnrage;

        private CapsuleCollider _rootCollider;
        private readonly Collider[] _recoveryClearanceBuffer = new Collider[GolemBossConstants.RECOVERY_CLEARANCE_BUFFER_SIZE];
        private Vector2Int _recoverySearchCenter;
        private int _recoverySearchRadius;
        private int _recoverySearchIndex;
        private float _recoveryRetryTimer;
        private bool _hasLoggedMissingLanding;
        private bool _hasSafeLanding;
        private Vector3 _lastSafeLanding;

        private GolemStateMachine _stateMachine;
        private GolemPursuitState _pursuitState;
        private GolemLeapSlamState _leapSlamState;
        private GolemLinearFistState _linearFistState;
        private GolemSkyBarrageState _skyBarrageState;
        private GolemStompState _stompState;
        private GolemDeathState _deathState;

        private MaterialPropertyBlock _materialPropertyBlock;
        private bool _isEnraged;
        private readonly List<ITelegraphIndicator> _activeTelegraphs = new List<ITelegraphIndicator>();

        public event Action<IGolemBoss> OnBossDefeated;

        public bool IsOperational => isActiveAndEnabled && Health.IsAlive();
        public bool IsRecovering { get; private set; }
        public int OperationGeneration { get; private set; }
        public IHealth Health { get; private set; }
        public GolemBossConfigSO Config => _config;
        public IGolemMovementController Movement => _movementController;
        public IGolemArmSocketController Arms => _armSocketController;
        public IGolemLinearAttackHitbox LinearAttackHitbox => _linearAttackHitbox;
        public IGolemAnimator Animator => _animator;
        public IAudioClipPlayer AudioClipPlayer => _audioClipPlayer;
        public Grid WorldGrid => _gridManager.WorldGrid;
        public Transform Transform => transform;

        public int CurrentPhase { get; private set; } = 1;
        public bool IsEnraged => _isEnraged;

        public float CurrentCooldownMultiplier
        {
            get
            {
                if (CurrentPhase == 3) return _config.Phase3CooldownMultiplier;
                if (CurrentPhase == 2) return _config.Phase2CooldownMultiplier;
                return 1f;
            }
        }

        public float CurrentSpeedMultiplier
        {
            get
            {
                if (CurrentPhase == 3) return _config.Phase3SpeedMultiplier;
                if (CurrentPhase == 2) return _config.Phase2SpeedMultiplier;
                return 1f;
            }
        }

        public float CurrentArmSpeedMultiplier
        {
            get
            {
                if (CurrentPhase == 3) return _config.Phase3ArmSpeedMultiplier;
                if (CurrentPhase == 2) return _config.Phase2ArmSpeedMultiplier;
                return 1f;
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

        public float DistanceToPlayer => Vector3.Distance(transform.position, PlayerPosition);

        public Vector3 DirectionToPlayer
        {
            get
            {
                Vector3 dir = PlayerPosition - transform.position;
                dir.y = 0f;
                return dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
            }
        }

        private void Awake()
        {
            Health = GetComponent<IHealth>();
            _rootCollider = GetComponent<CapsuleCollider>();
            if (_rootCollider.direction != 1)
            {
                throw new InvalidOperationException("Golem recovery requires a vertical root capsule.");
            }
            _materialPropertyBlock = new MaterialPropertyBlock();
            InitializeStateMachine();
        }

        private void OnEnable()
        {
            if (Health != null)
            {
                Health.MaxHealth = _config.MaxHealth;
                Health.OnHealthChanged += Health_OnHealthChanged;
                Health.OnNoHealth += Health_OnNoHealth;
            }

            IsRecovering = false;
            OperationGeneration++;
            CurrentPhase = 1;
            _isEnraged = false;
            _stateMachine.ResetCooldowns(
                GolemBossConstants.INITIAL_LEAP_SLAM_COOLDOWN,
                GolemBossConstants.INITIAL_STOMP_COOLDOWN,
                GolemBossConstants.INITIAL_LINEAR_FIST_COOLDOWN,
                GolemBossConstants.INITIAL_SKY_BARRAGE_COOLDOWN
            );
            _stateMachine.Initialize(_pursuitState);
        }

        private void OnDisable()
        {
            OperationGeneration++;
            IsRecovering = false;
            _stateMachine.Shutdown();
            _movementController.CanMove = false;
            _movementController.Stop();
            _animator.RestorePlayback();
            _armSocketController.ResetAllArms();
            if (Health != null)
            {
                Health.OnHealthChanged -= Health_OnHealthChanged;
                Health.OnNoHealth -= Health_OnNoHealth;
            }

            if (_linearAttackHitbox != null)
            {
                _linearAttackHitbox.Deactivate();
            }
            DismissAllTelegraphs();
        }

        private void OnDestroy()
        {
            DismissAllTelegraphs();
        }

        private void Update()
        {
            _stateMachine.Update();
        }

        private void FixedUpdate()
        {
            _stateMachine.FixedUpdate();
        }

        public CircularTelegraphIndicator ShowCircularTelegraph(Vector3 position, float radius, float duration, Action onImpact = null, bool autoContractOnFillComplete = false, bool exactPosition = false)
        {
            if (_circularTelegraph == null)
            {
                return null;
            }

            CircularTelegraphIndicator indicator = Instantiate(_circularTelegraph);
            _activeTelegraphs.Add(indicator);
            indicator.Show(position, radius, duration, WorldGrid, onImpact, autoContractOnFillComplete, exactPosition);
            return indicator;
        }

        public RectangularTelegraphIndicator ShowRectangularTelegraph(Vector3 origin, Vector3 direction, float length, float width, float duration, Action onImpact = null, bool autoContractOnFillComplete = false)
        {
            if (_rectangularTelegraph == null)
            {
                return null;
            }

            RectangularTelegraphIndicator indicator = Instantiate(_rectangularTelegraph);
            _activeTelegraphs.Add(indicator);
            indicator.Show(origin, direction, length, width, duration, onImpact, autoContractOnFillComplete);
            return indicator;
        }

        public void DismissAllTelegraphs()
        {
            for (int i = _activeTelegraphs.Count - 1; i >= 0; i--)
            {
                ITelegraphIndicator telegraph = _activeTelegraphs[i];
                if (telegraph != null && telegraph is UnityEngine.Object unityObj && unityObj != null)
                {
                    telegraph.Dismiss();
                }
            }
            _activeTelegraphs.Clear();
        }

        private void InitializeStateMachine()
        {
            _stateMachine = new GolemStateMachine();

            _leapSlamState = new GolemLeapSlamState(this, _stateMachine);
            _linearFistState = new GolemLinearFistState(this, _stateMachine);
            _skyBarrageState = new GolemSkyBarrageState(this, _stateMachine);
            _stompState = new GolemStompState(this, _stateMachine);
            _deathState = new GolemDeathState(this);

            _pursuitState = new GolemPursuitState(this, _stateMachine, _leapSlamState, _linearFistState, _skyBarrageState, _stompState);

            _leapSlamState.SetPursuitState(_pursuitState);
            _linearFistState.SetPursuitState(_pursuitState);
            _skyBarrageState.SetPursuitState(_pursuitState);
            _skyBarrageState.SetStompState(_stompState);
        }

        public void RequestDeathVolumeRecovery()
        {
            if (!IsOperational || IsRecovering)
            {
                return;
            }
            IsRecovering = true;
            OperationGeneration++;
            _hasLoggedMissingLanding = false;
            _stateMachine.Shutdown();
            _linearAttackHitbox.Deactivate();
            _armSocketController.ResetAllArms();
            DismissAllTelegraphs();
            _movementController.CanMove = false;
            _movementController.Stop();
            _movementController.SetKinematic(true);
            RestartRecoverySearch();
            _stateMachine.ChangeState(_leapSlamState, restart: true);
        }

        public void RestartRecoverySearch()
        {
            Cell center = WorldPosToCellConverter.GetCellFromGridByWorldPos(WorldGrid, PlayerPosition);
            _recoverySearchCenter = center.WorldGridPos;
            _recoverySearchRadius = 0;
            _recoverySearchIndex = 0;
            _recoveryRetryTimer = 0f;
            _movementController.SetKinematic(true);
            _movementController.SetPosition(center.WorldPos + Vector3.up * _config.LeapMaxHeight);
        }

        public bool TryFindRecoveryLanding(out Vector3 rootPosition, out Vector3 surfacePosition)
        {
            rootPosition = default;
            surfacePosition = default;
            if (!IsRecovering || !IsOperational || Time.deltaTime <= 0f)
            {
                return false;
            }
            if (_recoveryRetryTimer > 0f)
            {
                _recoveryRetryTimer -= Time.deltaTime;
                if (_recoveryRetryTimer > 0f)
                {
                    return false;
                }
                RestartRecoverySearch();
            }

            int maxRadius = Mathf.Max(WorldGrid.Width, WorldGrid.Height);
            for (int attempt = 0; attempt < GolemBossConstants.RECOVERY_CANDIDATES_PER_FRAME; attempt++)
            {
                if (_recoverySearchRadius >= maxRadius)
                {
                    if (_hasSafeLanding && TryGetRecoveryLanding(_lastSafeLanding, out rootPosition, out surfacePosition))
                    {
                        return true;
                    }
                    if (!_hasLoggedMissingLanding)
                    {
                        Debug.LogWarning("Golem recovery found no supported body landing; holding airborne and retrying.", this);
                        _hasLoggedMissingLanding = true;
                    }
                    _recoveryRetryTimer = GolemBossConstants.RECOVERY_RETRY_DELAY;
                    return false;
                }

                Vector2Int offset = GetRecoverySearchOffset(_recoverySearchRadius, _recoverySearchIndex);
                _recoverySearchIndex++;
                int ringCount = _recoverySearchRadius == 0 ? 1 : 8 * _recoverySearchRadius;
                if (_recoverySearchIndex >= ringCount)
                {
                    _recoverySearchRadius++;
                    _recoverySearchIndex = 0;
                }
                Vector2Int index = _recoverySearchCenter + offset;
                if (index.x < 0 || index.y < 0 || index.x >= WorldGrid.Width || index.y >= WorldGrid.Height)
                {
                    continue;
                }
                Cell candidate = WorldGrid.Cells[index.x, index.y];
                if (candidate != null && TryGetRecoveryLanding(candidate.WorldPos, out rootPosition, out surfacePosition))
                {
                    _lastSafeLanding = rootPosition;
                    _hasSafeLanding = true;
                    return true;
                }
            }
            return false;
        }

        public bool ValidateRecoveryLanding(Vector3 rootPosition, out Vector3 surfacePosition)
        {
            if (TryGetRecoveryLanding(rootPosition, out Vector3 currentRoot, out surfacePosition))
            {
                return (currentRoot - rootPosition).sqrMagnitude
                    <= GridConstants.ROOT_GROUND_CLEARANCE * GridConstants.ROOT_GROUND_CLEARANCE;
            }
            return false;
        }

        public void CompleteRecovery()
        {
            IsRecovering = false;
        }

        private static Vector2Int GetRecoverySearchOffset(int radius, int index)
        {
            if (radius == 0)
            {
                return Vector2Int.zero;
            }
            int sideLength = radius * 2;
            int side = index / sideLength;
            int step = index % sideLength;
            switch (side)
            {
                case 0: return new Vector2Int(-radius + step, -radius);
                case 1: return new Vector2Int(radius, -radius + step);
                case 2: return new Vector2Int(radius - step, radius);
                default: return new Vector2Int(-radius, radius - step);
            }
        }

        private bool TryGetRecoveryLanding(Vector3 candidate, out Vector3 rootPosition, out Vector3 surfacePosition)
        {
            rootPosition = default;
            surfacePosition = default;
            if (!GroundSupportQuery.IsWithinWorldBounds(WorldGrid, candidate))
            {
                return false;
            }
            // Grid height, never the fallen root height, defines the support probing band.
            Cell cell = WorldPosToCellConverter.GetCellFromGridByWorldPos(WorldGrid, candidate);
            candidate.y = cell.WorldPos.y;
            Vector3 scale = transform.lossyScale;
            float radius = _rootCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(_rootCollider.height * Mathf.Abs(scale.y), radius * 2f);
            Vector3 centerOffset = transform.TransformVector(_rootCollider.center);
            Vector3 supportCenter = candidate + new Vector3(centerOffset.x, 0f, centerOffset.z);
            if (!GroundSupportQuery.TryGetSupportedPosition(supportCenter, out Vector3 support))
            {
                return false;
            }
            float minHeight = support.y;
            float maxHeight = support.y;
            // Sample the interior at grid resolution as well as the circular footprint boundary.
            float spacing = WorldGrid.CellSize * 0.5f;
            int extent = Mathf.CeilToInt(radius / spacing);
            for (int x = -extent; x <= extent; x++)
            {
                for (int z = -extent; z <= extent; z++)
                {
                    Vector3 offset = new Vector3(x * spacing, 0f, z * spacing);
                    if (offset.sqrMagnitude <= radius * radius
                        && !SampleRecoverySupport(supportCenter + offset, ref minHeight, ref maxHeight))
                    {
                        return false;
                    }
                }
            }
            for (int sample = 0; sample < GolemBossConstants.RECOVERY_FOOTPRINT_RING_SAMPLES; sample++)
            {
                float angle = sample * Mathf.PI * 2f / GolemBossConstants.RECOVERY_FOOTPRINT_RING_SAMPLES;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!SampleRecoverySupport(supportCenter + offset, ref minHeight, ref maxHeight))
                {
                    return false;
                }
            }
            surfacePosition = new Vector3(supportCenter.x,
                maxHeight - GridConstants.ROOT_GROUND_CLEARANCE, supportCenter.z);
            rootPosition = candidate;
            rootPosition.y = maxHeight - centerOffset.y + height * 0.5f;
            Vector3 center = rootPosition + centerOffset;
            Vector3 bottom = center - Vector3.up * (height * 0.5f - radius);
            Vector3 top = center + Vector3.up * (height * 0.5f - radius + _config.LeapMaxHeight);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _recoveryClearanceBuffer,
                TerrainLayers.All, QueryTriggerInteraction.Ignore);
            if (count == _recoveryClearanceBuffer.Length)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = _recoveryClearanceBuffer[i];
                if (obstacle != _rootCollider && (obstacle.attachedRigidbody == null || obstacle.attachedRigidbody.isKinematic))
                {
                    return false;
                }
            }
            return true;
        }

        private bool SampleRecoverySupport(Vector3 sample, ref float minHeight, ref float maxHeight)
        {
            if (!GroundSupportQuery.IsWithinWorldBounds(WorldGrid, sample)
                || !GroundSupportQuery.TryGetSupportedPosition(sample, out Vector3 supported))
            {
                return false;
            }
            minHeight = Mathf.Min(minHeight, supported.y);
            maxHeight = Mathf.Max(maxHeight, supported.y);
            return maxHeight - minHeight <= GolemBossConstants.RECOVERY_SUPPORT_HEIGHT_TOLERANCE;
        }

        public void TakeDamage(float damage)
        {
            if (!Health.IsAlive())
            {
                return;
            }

            Vector3 spawnPos = _bloodVfxPlayer != null ? _bloodVfxPlayer.transform.position : transform.position + Vector3.up * 1.5f;

            if (_damageNumbersSpawner != null)
            {
                _damageNumbersSpawner.Spawn(
                    spawnPos,
                    new DamageNubmersSpawnerConfig(damage, ShapeModes.Hemisphere)
                );
            }

            Health.DecreaseHealth(damage);

            if (Health.IsAlive() && _bloodVfxPlayer != null)
            {
                _bloodVfxPlayer.Play(new VFXPlayConfig());
            }
        }

        public void TakeFullHpDamage()
        {
            TakeDamage(Health.MaxHealth);
        }

        public void ApplyKnockBack(Vector3 direction, float power, float timeToArriveAtLocation)
        {
            // Boss is immune to normal knockback to maintain heavy presence
        }

        public void TriggerStompDamage()
        {
            _audioClipPlayer?.PlayOneShot(GolemBossConstants.STOMP_SFX_KEY);
            _stompLegTrigger.ApplyStompDamage(_config.StompDamage);
        }

        private void Health_OnHealthChanged(object sender, EventArgs e)
        {
            if (!Health.IsAlive())
            {
                return;
            }

            float healthPercentage = Health.CurrentHealth / Health.MaxHealth;

            if (healthPercentage <= _config.Phase3HealthPercent && CurrentPhase < 3)
            {
                TriggerEnragePhase();
            }
            else if (healthPercentage <= _config.Phase2HealthPercent && CurrentPhase < 2)
            {
                CurrentPhase = 2;
            }
        }

        private void TriggerEnragePhase()
        {
            CurrentPhase = 3;
            _isEnraged = true;

            _audioClipPlayer?.PlayOneShot(GolemBossConstants.ROAR_SFX_KEY);

            if (_enrageVfxPlayer != null)
            {
                _enrageVfxPlayer.Play(new VFXPlayConfig());
            }

            ApplyEnrageMaterials();
        }

        private void ApplyEnrageMaterials()
        {
            if (_renderersForEnrage == null)
            {
                return;
            }

            foreach (Renderer rend in _renderersForEnrage)
            {
                if (rend == null) continue;

                rend.GetPropertyBlock(_materialPropertyBlock);
                _materialPropertyBlock.SetColor(GolemBossConstants.BASE_COLOR_PROPERTY, _config.EnrageColor);
                _materialPropertyBlock.SetColor(GolemBossConstants.EMISSION_COLOR_PROPERTY, _config.EnrageEmissionColor);
                rend.SetPropertyBlock(_materialPropertyBlock);
            }
        }

        private void Health_OnNoHealth(object sender, EventArgs e)
        {
            OperationGeneration++;
            IsRecovering = false;
            _animator.RestorePlayback();
            _stateMachine.ChangeState(_deathState);

            if (_deathVfxPlayer != null)
            {
                _deathVfxPlayer.Play(new VFXPlayConfig());
            }

            if (_expParticleSpawner != null)
            {
                _expParticleSpawner.Spawn(transform.position, _config.ExpForKill);
            }

            OnBossDefeated?.Invoke(this);
        }
    }
}
