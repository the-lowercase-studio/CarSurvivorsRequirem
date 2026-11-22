using Assets.Scripts.Audio;
using Assets.Scripts.Collisions;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.Pooling;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Shapes;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using System;
using UnityEngine;

namespace Assets.Scripts.Enemies.Base
{
    public class Enemy : MonoBehaviour, IHealthy, IDamageable, IKnockable, IPoolable
    {
        [Inject] private readonly IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> _damageNumbersSpawner = null;
        [Inject] private readonly ITimeFreezeState _timeFreezeState = null;

        [field: SerializeField] public EnemyConfigSO Config { get; private set; }
        [SerializeField] private VFXPlayer _bloodVfxPlayer;
        [SerializeField] private GameObject _visual;
        [Tooltip("Optional component toggling the tier shiny shader effect on enemy body renderers.")]
        [SerializeField] private EnemyTierShineController _tierShineController;

        private INeedToCompleteBeforeDisable _enemyDeathSequence;
        private EnemyConfigSO _baseConfig;
        private Collider _collider;
        private uint _materializationToken;
        private float _materializeElapsed;
        private float _materializeDuration;

        public IHealth Health { get; private set; }
        public ICollisionsController CollisionsController { get; private set; }
        public IMovementController MovementController { get; private set; }
        public IAudioClipPlayer AudioClipPlayer { get; private set; }
        public EnemyAnimator EnemyAnimator { get; private set; }

        public bool IsFrozen => _timeFreezeState != null && _timeFreezeState.IsFrozen;
        public float UnfrozenTime => _timeFreezeState != null ? _timeFreezeState.UnfrozenTime : Time.time;
        public bool IsMaterializing { get; private set; }
        public uint MaterializationToken => _materializationToken;

        public event Action<bool> OnFrozenChanged;
        public event EventHandler OnCanBeReleased;

        private void Awake()
        {
            Health = GetComponent<IHealth>();
            CollisionsController = GetComponent<ICollisionsController>();
            MovementController = GetComponent<IMovementController>();
            AudioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
            _enemyDeathSequence = GetComponent<INeedToCompleteBeforeDisable>();
            EnemyAnimator = GetComponentInChildren<EnemyAnimator>();
            _collider = GetComponent<Collider>();

            _baseConfig ??= Config;
            if (Config != null)
            {
                Health.MaxHealth = Config.MaxHealth;
                ApplyTierShine(Config.IsShiny);
            }
        }

        private void OnEnable()
        {
            if (_timeFreezeState != null)
            {
                _timeFreezeState.OnFrozenChanged += HandleFrozenChanged;
            }
        }

        private void Update()
        {
            if (!IsMaterializing)
            {
                return;
            }

            _materializeElapsed += Time.deltaTime;
            float normalizedProgress = _materializeDuration > 0f
                ? Mathf.Clamp01(_materializeElapsed / _materializeDuration)
                : 1f;

            if (_tierShineController != null)
            {
                _tierShineController.SetMaterializeProgress(normalizedProgress);
            }
        }

        private void OnDisable()
        {
            if (_timeFreezeState != null)
            {
                _timeFreezeState.OnFrozenChanged -= HandleFrozenChanged;
            }

            ResetMaterializationState();
        }

        public void OnGet()
        {
            ResetMaterializationState();
            _enemyDeathSequence.OnCompleted += EnemyDeathSequence_OnCompleted;

            _visual.SetActive(true);

            Health.MaxHealth = Config.MaxHealth;

            EnemyAnimator.ResetAttackAnimationState();
        }

        public void BeginMaterialization(float duration)
        {
            if (_collider == null)
            {
                throw new InvalidOperationException($"{nameof(Enemy)} on '{name}' requires a {nameof(Collider)} to begin swarm materialization.");
            }

            _materializationToken++;
            IsMaterializing = true;
            _materializeElapsed = 0f;
            _materializeDuration = Mathf.Max(0f, duration);
            _collider.enabled = false;

            if (EnemyAnimator != null)
            {
                EnemyAnimator.ResetAttackAnimationState();
            }

            if (_tierShineController != null)
            {
                _tierShineController.BeginMaterialization();
            }
        }

        public void SetMaterializationDuration(float duration)
        {
            if (!IsMaterializing)
            {
                return;
            }

            _materializeDuration = Mathf.Max(0f, duration);
        }

        public void CompleteMaterialization()
        {
            if (!IsMaterializing)
            {
                return;
            }

            IsMaterializing = false;
            _materializeElapsed = 0f;
            _materializeDuration = 0f;

            if (_collider != null)
            {
                _collider.enabled = true;
            }

            if (_tierShineController != null)
            {
                _tierShineController.EndMaterialization();
            }

            if (MovementController != null)
            {
                MovementController.ResetAfterRelocation();
            }
        }

        private void ResetMaterializationState()
        {
            _materializationToken++;
            IsMaterializing = false;
            _materializeElapsed = 0f;
            _materializeDuration = 0f;

            if (_tierShineController != null)
            {
                _tierShineController.EndMaterialization();
            }
        }

        public void ReturnToPool()
        {
            OnRelease();

            OnCanBeReleased?.Invoke(this, EventArgs.Empty);
        }

        public void SetBaseConfig(EnemyConfigSO baseConfig)
        {
            if (baseConfig == null)
            {
                return;
            }

            _baseConfig = baseConfig;
            ApplyRuntimeConfig(baseConfig);
        }

        public void ApplyRuntimeConfig(EnemyConfigSO configOverride)
        {
            if (configOverride == null)
            {
                return;
            }

            _baseConfig ??= Config;
            Config = configOverride;

            IHealth health = Health != null ? Health : GetComponent<IHealth>();
            health.MaxHealth = Config.MaxHealth;

            ApplyTierShine(Config.IsShiny);
        }

        public void ApplyTierShine(bool isEnabled)
        {
            if (_tierShineController != null)
            {
                _tierShineController.SetShineEnabled(isEnabled);
            }
        }

        public void OnRelease()
        {
            ResetMaterializationState();
            _enemyDeathSequence.OnCompleted -= EnemyDeathSequence_OnCompleted;

            EnemyAnimator.ResetAttackAnimationState();

            if (_baseConfig != null && !ReferenceEquals(Config, _baseConfig))
            {
                ApplyRuntimeConfig(_baseConfig);
            }
        }

        public void ApplyKnockBack(Vector3 direction, float power, float timeToArriveAtLocation)
        {
            if (IsFrozen || IsMaterializing)
            {
                return;
            }

            MovementController.MoveToPositionInTimeIgnoringSpeed(transform.position + (direction * power), timeToArriveAtLocation);
        }

        public void TakeFullHpDamage()
        {
            if (IsMaterializing)
            {
                return;
            }

            Health.DecreaseHealth(Health.MaxHealth);
        }

        public void TakeDamage(float damage)
        {
            if (IsMaterializing || !Health.IsAlive())
            {
                return;
            }

            Vector3 spawnPos = _bloodVfxPlayer != null ? _bloodVfxPlayer.transform.position : transform.position + Vector3.up;

            _damageNumbersSpawner.Spawn(
                spawnPos,
                new DamageNubmersSpawnerConfig(
                    damage,
                    ShapeModes.Hemisphere
                )
            );

            Health.DecreaseHealth(damage);

            if (Health.IsAlive() && _bloodVfxPlayer != null)
            {
                _bloodVfxPlayer.Play(new VFXPlayConfig());
            }
        }

        private void HandleFrozenChanged(bool isFrozen)
        {
            OnFrozenChanged?.Invoke(isFrozen);
        }

        private void EnemyDeathSequence_OnCompleted(object sender, EventArgs e)
        {
            OnCanBeReleased?.Invoke(this, EventArgs.Empty);
        }
    }
}
