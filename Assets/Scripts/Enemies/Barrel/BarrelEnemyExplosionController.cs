using System;
using Assets.Scripts.Collisions;
using Assets.Scripts.Enemies.Barrel.Constants;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Enemies.Constants;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Player;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Enemies.Barrel
{
    [RequireComponent(typeof(Enemy), typeof(Health), typeof(EnemyMovementController))]
    [RequireComponent(typeof(EnemyCollisionsController))]
    public class BarrelEnemyExplosionController : MonoBehaviour
    {
        private enum ExplosionState { Inactive, Approaching, Priming, Canceling, Detonating, Dying }

        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly IExplosionVfxPool _explosionVfxPool = null;

        [SerializeField] private BarrelExplosionConfigSO _config;
        [SerializeField] private EnemyAnimator _enemyAnimator;
        [SerializeField] private CircularTelegraphIndicator _warning;
        [SerializeField] private BarrelEnemyDeathHandler _deathHandler;
        [SerializeField] private BarrelEnemyAttackFeedback _attackFeedback;

        private Enemy _enemy;
        private Health _health;
        private EnemyMovementController _movement;
        private EnemyCollisionsController _collisions;
        private BoxCollider _playerBody;
        private IDamageable _playerDamageable;
        private ExplosionState _state;
        private int _lifetime;
        private float _radius;
        private float _damage;
        private float _primingStartedAt;

        public bool HasDetonated { get; private set; }

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _health = GetComponent<Health>();
            _movement = GetComponent<EnemyMovementController>();
            _collisions = GetComponent<EnemyCollisionsController>();
            _config.ValidateConfiguration();
            if (_enemyAnimator == null || _warning == null || _deathHandler == null)
            {
                throw new InvalidOperationException("Barrel requires authored Animator, warning, and death handler references.");
            }
            if (_attackFeedback == null)
            {
                Debug.LogError("Barrel attack feedback is not assigned on the prefab.", this);
            }
        }

        private void OnEnable()
        {
            _lifetime++;
            _state = ExplosionState.Approaching;
            HasDetonated = false;
            ResetFeedback();
            _warning.HideReusable();
            _enemyAnimator.RestoreInitialState(BarrelEnemyConstants.INITIAL_ANIMATOR_STATE);
            _collisions.OnCollisionWithPlayer += OnPlayerContact;
            _enemyAnimator.OnAttackAnimationStart += OnAttackStarted;
            _enemyAnimator.OnAttackHitFrame += OnAttackHitFrame;
            _enemyAnimator.OnAttackAnimationEnd += OnAttackEnded;
            _health.OnNoHealth += OnNoHealth;
        }

        private void Start()
        {
            _playerBody = _playerManager.GameObject.GetComponent<BoxCollider>();
            _playerDamageable = _playerManager.GameObject.GetComponent<IDamageable>();
            if (_playerBody == null || _playerDamageable == null || EntityLayers.Player.value == 0)
            {
                throw new InvalidOperationException("Barrel requires a player root body collider, IDamageable, and Player layer.");
            }
        }

        private void Update()
        {
            if (_state == ExplosionState.Priming
                && Time.time - _primingStartedAt >= EnemyAnimationConstants.MAX_ATTACK_ANIMATION_DURATION)
            {
                Debug.LogError("Barrel Attack failed to deliver its hit event; canceling priming.", this);
                CancelPriming();
            }
        }

        private void OnDisable()
        {
            _lifetime++;
            _state = ExplosionState.Inactive;
            _collisions.OnCollisionWithPlayer -= OnPlayerContact;
            _enemyAnimator.OnAttackAnimationStart -= OnAttackStarted;
            _enemyAnimator.OnAttackHitFrame -= OnAttackHitFrame;
            _enemyAnimator.OnAttackAnimationEnd -= OnAttackEnded;
            _health.OnNoHealth -= OnNoHealth;
            ResetFeedback();
            _warning.HideReusable();
            _enemyAnimator.ResetAttackAnimationState();
            _movement.SetPursuitSuppressed(false);
            _radius = 0f;
            _damage = 0f;
            _primingStartedAt = 0f;
            HasDetonated = false;
        }

        private void OnDestroy()
        {
            // A forced disable can leave the inactive warning detached until reuse.
            if (_warning != null && _warning.transform.parent == null)
            {
                Destroy(_warning.gameObject);
            }
        }

        public void CancelForDeath()
        {
            ResetFeedback();
            if (_state != ExplosionState.Inactive)
            {
                _state = HasDetonated ? ExplosionState.Detonating : ExplosionState.Dying;
                _movement.StopMovement();
            }
        }

        private bool IsPlayerEligible(float radius)
        {
            if (!_playerManager.GameObject.activeInHierarchy || !_playerManager.Health.IsAlive()
                || !_playerBody.enabled)
            {
                return false;
            }

            Bounds bodyBounds = _playerBody.bounds;
            Vector3 bottom = transform.position;
            bottom.y = bodyBounds.min.y;
            Vector3 top = bottom;
            top.y = bodyBounds.max.y;
            // Include touching contacts despite world-coordinate floating-point rounding.
            return Physics.CheckCapsule(bottom, top, radius + BarrelEnemyConstants.DISK_CONTACT_TOLERANCE,
                EntityLayers.Player, QueryTriggerInteraction.Collide);
        }

        private void CancelPriming()
        {
            if (_state != ExplosionState.Priming || !_health.IsAlive())
            {
                return;
            }

            _state = ExplosionState.Canceling;
            ResetFeedback();
            int lifetime = _lifetime;
            _enemyAnimator.RestoreInitialState(BarrelEnemyConstants.INITIAL_ANIMATOR_STATE);
            _warning.ContractAndHideReusable(() =>
            {
                if (lifetime == _lifetime && _state == ExplosionState.Canceling && _health.IsAlive())
                {
                    _state = ExplosionState.Approaching;
                    _movement.SetPursuitSuppressed(false);
                }
            });
        }

        private void OnPlayerContact(object sender, CollisionEventArgs e)
        {
            if (_state != ExplosionState.Approaching || !_health.IsAlive()
                || e.Collider != _playerBody || !IsPlayerEligible(_config.ActivationRange))
            {
                return;
            }

            _config.ValidateConfiguration();
            _state = ExplosionState.Priming;
            _radius = _config.ExplosionRadius;
            _damage = _enemy.Config.Damage;
            _primingStartedAt = Time.time;
            _movement.SetPursuitSuppressed(true);
            _warning.ShowAttachedWarning(transform, _radius);
            _enemyAnimator.PlayAttackAnimation();
        }

        private void OnAttackHitFrame(object sender, EventArgs e)
        {
            if (_state != ExplosionState.Priming || !_health.IsAlive() || HasDetonated)
            {
                return;
            }

            _state = ExplosionState.Detonating;
            HasDetonated = true;
            int lifetime = _lifetime;
            if (_attackFeedback != null)
            {
                _attackFeedback.CompleteFeedback();
            }
            ResetFeedback();
            Vector3 explosionPosition = transform.position;
            float explosionRadius = _radius;
            _explosionVfxPool.Play(explosionPosition, explosionRadius);
            _warning.SynchronizeAttachedPosition();
            Physics.SyncTransforms();
            if (IsPlayerEligible(_radius))
            {
                _playerDamageable.TakeDamage(_damage);
            }

            // Damage subscribers may unload or forcibly release the current lifetime.
            if (lifetime != _lifetime || !isActiveAndEnabled)
            {
                return;
            }
            _warning.HideReusable();
            _deathHandler.HideBody();
            _enemy.TakeFullHpDamage();
            if (lifetime == _lifetime && isActiveAndEnabled)
            {
                _deathHandler.CompleteDetonationAfterDispatch();
            }
        }

        private void OnAttackEnded(object sender, EventArgs e)
        {
            CancelPriming();
        }

        private void ResetFeedback()
        {
            if (_attackFeedback != null)
            {
                _attackFeedback.ResetFeedback();
            }
        }

        private void OnAttackStarted(object sender, EventArgs e)
        {
            if (_state == ExplosionState.Priming && _health.IsAlive() && _attackFeedback != null)
            {
                _attackFeedback.BeginFeedback();
            }
        }

        private void OnNoHealth(object sender, EventArgs e)
        {
            CancelForDeath();
        }
    }
}
