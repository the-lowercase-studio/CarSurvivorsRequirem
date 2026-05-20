using Assets.Scripts.Audio;
using Assets.Scripts.Collisions;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.Pooling;
using Assets.Scripts.Shapes;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using System;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    public class Enemy : MonoBehaviour, IHealthy, IDamageable, IKnockable, IStunnable, IPoolable
    {
        [Inject] private readonly IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig> _damageNumbersSpawner;

        [field: SerializeField] public EnemyConfigSO Config { get; private set; }
        [SerializeField] private VFXPlayer _bloodVfxPlayer;
        [SerializeField] private GameObject _visual;

        public IHealth Health { get; private set; }
        public IStunController StunController { get; private set; }
        public ICollisionsController CollisionsController { get; private set; }
        public IMovementController MovementController { get; private set; }
        public IAudioClipPlayer AudioClipPlayer { get; private set; }
        public EnemyAnimator EnemyAnimator { get; private set; }

        public event EventHandler OnCanBeReleased;

        private INeedToCompleteBeforeDisable _enemyDeathSequence;

        private void Awake()
        {
            Health = GetComponent<IHealth>();
            StunController = GetComponent<IStunController>();
            CollisionsController = GetComponent<ICollisionsController>();
            MovementController = GetComponent<IMovementController>();
            AudioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
            _enemyDeathSequence = GetComponent<INeedToCompleteBeforeDisable>();
            EnemyAnimator = GetComponentInChildren<EnemyAnimator>();
        }

        public void OnGet()
        {
            _enemyDeathSequence.OnCompleted += EnemyDeathSequence_OnCompleted;

            _visual.SetActive(true);

            Health.MaxHealth = Config.MaxHealth;
        }

        public void ReturnToPool()
        {
            OnRelease();

            OnCanBeReleased?.Invoke(this, EventArgs.Empty);
        }

        public void OnRelease()
        {
            _enemyDeathSequence.OnCompleted -= EnemyDeathSequence_OnCompleted;
        }

        public void ApplyKnockBack(Vector3 direction, float power, float timeToArriveAtLocation)
        {
            MovementController.MoveToPositionInTimeIgnoringSpeed(transform.position + (direction * power), timeToArriveAtLocation);
        }

        public void TakeFullHpDamage()
        {
            Health.DecreaseHealth(Health.MaxHealth);
        }

        public void TakeDamage(float damage)
        {
            _damageNumbersSpawner.Spawn(
                _bloodVfxPlayer.transform.position,
                new DamageNubmersSpawnerConfig(
                    damage,
                    ShapeModes.Hemisphere
                )
            );

            Health.DecreaseHealth(damage);

            if (Health.IsAlive())
            {
                _bloodVfxPlayer.Play(new VFXPlayConfig());
            }
        }

        public void ApplyStun(float duration)
        {
            StunController.PerformStun(duration);
        }

        private void EnemyDeathSequence_OnCompleted(object sender, EventArgs e)
        {
            OnCanBeReleased?.Invoke(this, EventArgs.Empty);
        }
    }
}
