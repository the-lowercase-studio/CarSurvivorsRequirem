using Assets.ScriptableObjects;
using Assets.Scripts.Extensions;
using Assets.Scripts.Initializers;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Pooling;
using Assets.Scripts.StatusEffects;
using DG.Tweening;
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
        private Tween _shrinkTween;

        public event EventHandler OnLifeEnd;
        public event EventHandler OnCanBeReleased;

        private void Start()
        {
            _startScale = transform.localScale;
        }

        private void FixedUpdate()
        {
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

        private void OnDestroy()
        {
            _shrinkTween?.Kill();
            _shrinkTween = null;
        }

        public void OnGet()
        {
            _distanceTraveled = 0f;
            _isAlive = true;
        }

        public void OnRelease()
        {
            _shrinkTween?.Kill();
            _shrinkTween = null;

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
                                         EntityLayers.Enemy | TerrainLayers.Impassable);
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
            _shrinkTween?.Kill();
            _shrinkTween = transform.DOScale(Vector3.zero, _config.DisapearingDuration)
                .SetEase(Ease.Flash)
                .OnComplete(HandleShrinkComplete);
        }

        private void HandleShrinkComplete()
        {
            _shrinkTween = null;
            OnLifeEnd?.Invoke(this, EventArgs.Empty);
        }
    }
}

