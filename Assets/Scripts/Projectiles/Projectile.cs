using Assets.ScriptableObjects;
using Assets.Scripts.Extensions;
using Assets.Scripts.Initializers;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Pooling;
using Assets.Scripts.StatusEffects;
using System;
using UnityEngine;

namespace Assets.Scripts.Projectiles
{
    public class Projectile : MonoBehaviour, IInitializableWithScriptableConfig<ProjectileConfigSO>, IPoolable
    {
        [SerializeField] private ProjectileConfigSO _config;
        [SerializeField] private SphereCollider _sphereCollider;

        private int _piercedCounter;
        private bool _isInitialized;
        private bool _isAlive = true;
        private float _distanceTraveled;
        private Vector3 _movementDir;
        private Vector3 _startScale;
        private bool _isShrinking;
        private float _shrinkElapsed;
        private Vector3 _initialShrinkScale;

        public event EventHandler OnLifeEnd;
        public event EventHandler OnCanBeReleased;

        private void Start()
        {
            _startScale = transform.localScale;
        }

        private void FixedUpdate()
        {
            if (_isShrinking)
            {
                _shrinkElapsed += Time.fixedDeltaTime;
                float duration = Mathf.Max(0.0001f, _config != null ? _config.DisapearingDuration : 0.1f);
                float t = Mathf.Clamp01(_shrinkElapsed / duration);
                transform.localScale = Vector3.Lerp(_initialShrinkScale, Vector3.zero, t);

                if (t >= 1f)
                {
                    _isShrinking = false;
                    transform.localScale = Vector3.zero;
                    OnLifeEnd?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            if (!_isAlive || !_isInitialized)
            {
                return;
            }

            MoveProjectileInDirection(_movementDir);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isAlive || !_isInitialized)
            {
                return;
            }

            HandleCollisions();
        }

        public void OnGet()
        {
            _distanceTraveled = 0f;
            _isAlive = true;
            _isShrinking = false;
            _shrinkElapsed = 0f;
        }

        public void OnRelease()
        {
            _isShrinking = false;
            _shrinkElapsed = 0f;
            _isInitialized = false;

            transform.localScale = _startScale;
        }

        public void ReturnToPool()
        {
            OnRelease();

            OnCanBeReleased?.Invoke(this, EventArgs.Empty);
        }

        public void Initialize(ProjectileConfigSO config)
        {
            _config = config;

            _piercedCounter = _config.MaxPiercing;

            transform.localScale = new Vector3(_config.Size, _config.Size, transform.localScale.y);

            _isInitialized = true;
        }

        public bool IsInitialized()
        {
            return _isInitialized;
        }

        public bool SetMovementDirection(Vector3 direction)
        {
            if (direction.Equals(Vector3.zero))
            {
                return false;
            }

            _movementDir = direction.normalized;

            return true;
        }

        private void MoveProjectileInDirection(Vector3 direction)
        {
            float moveStep = _config.Speed * Time.deltaTime;
            transform.position += _movementDir * moveStep;
            _distanceTraveled += moveStep;

            if (_distanceTraveled >= _config.Range)
            {
                _isAlive = false;
                PlayEndLifeAnimation();
            }
        }

        private void HandleCollisions()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position + _sphereCollider.center, _sphereCollider.radius,
                                         EntityLayers.Enemies | TerrainLayers.Impassable);
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                {
                    continue;
                }

                EntityManipulationHelper.Damage(collider, _config.Damage);

                if (_piercedCounter > 0)
                {
                    _piercedCounter--;
                }
                else
                {
                    _isAlive = false;
                    OnLifeEnd?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void PlayEndLifeAnimation()
        {
            _isShrinking = true;
            _shrinkElapsed = 0f;
            _initialShrinkScale = transform.localScale;
        }
    }
}

