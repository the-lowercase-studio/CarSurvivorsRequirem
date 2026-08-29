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
    [RequireComponent(typeof(Health))]
    public class GolemBoss : MonoBehaviour, IGolemBoss, IDamageable, IKnockable
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

        public IHealth Health { get; private set; }
        public GolemBossConfigSO Config => _config;
        public IGolemMovementController Movement => _movementController;
        public IGolemArmSocketController Arms => _armSocketController;
        public IGolemLinearAttackHitbox LinearAttackHitbox => _linearAttackHitbox;
        public IGolemAnimator Animator => _animator;
        public IAudioClipPlayer AudioClipPlayer => _audioClipPlayer;
        public Grid WorldGrid => _gridManager?.WorldGrid;
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
            _materialPropertyBlock = new MaterialPropertyBlock();

            if (_animator == null)
            {
                _animator = GetComponentInChildren<GolemAnimator>();
            }

            if (_stompLegTrigger == null)
            {
                _stompLegTrigger = GetComponentInChildren<GolemStompTrigger>();
            }

            if (_linearAttackHitbox == null)
            {
                _linearAttackHitbox = GetComponentInChildren<GolemLinearAttackHitbox>();
            }

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
            if (Health != null)
            {
                Health.OnHealthChanged -= Health_OnHealthChanged;
                Health.OnNoHealth -= Health_OnNoHealth;
            }

            _linearAttackHitbox?.Deactivate();
            DismissAllTelegraphs();
        }

        private void OnDestroy()
        {
            _linearAttackHitbox?.Deactivate();
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

        public CircularTelegraphIndicator ShowCircularTelegraph(Vector3 position, float radius, float duration, Action onImpact = null, bool autoContractOnFillComplete = false)
        {
            if (_circularTelegraph == null)
            {
                return null;
            }

            CircularTelegraphIndicator indicator = Instantiate(_circularTelegraph);
            _activeTelegraphs.Add(indicator);
            indicator.Show(position, radius, duration, WorldGrid, onImpact, autoContractOnFillComplete);
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

            if (_stompLegTrigger != null)
            {
                _stompLegTrigger.ApplyStompDamage(_config.StompDamage);
                return;
            }

            Vector3 point1 = transform.position;
            Vector3 point2 = transform.position + Vector3.up * 3.5f;
            Collider[] hitColliders = Physics.OverlapCapsule(point1, point2, _config.StompRadius, EntityLayers.Player, QueryTriggerInteraction.Collide);
            foreach (Collider hit in hitColliders)
            {
                if (hit != null)
                {
                    EntityManipulationHelper.Damage(hit, _config.StompDamage);
                }
            }
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
