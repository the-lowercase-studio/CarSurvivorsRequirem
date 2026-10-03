using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.Scripts.VFX
{
    public interface IExplosionVfxPool
    {
        void Play(Vector3 position, float radius);
    }

    public class ExplosionVfxPool : MonoBehaviour, IExplosionVfxPool
    {
        [SerializeField] private PooledExplosionEffect _effectPrefab;
        [SerializeField, Min(1), Tooltip("Instances reserved before combat; exhausted reserves skip optional effects.")]
        private int _prewarmCount = 32;
        [SerializeField, Min(1), Tooltip("Upper bound on the reserved pool and simultaneous active effects.")]
        private int _capacityLimit = 32;
        [SerializeField, Min(0.1f), Tooltip("Maximum scaled seconds for the complete effect, including particle tails.")]
        private float _watchdogTimeout = 8f;

        private List<PooledExplosionEffect> _activeEffects;
        private ObjectPool<PooledExplosionEffect> _pool;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void Update()
        {
            if (_pool == null)
            {
                return;
            }
            for (int index = _activeEffects.Count - 1; index >= 0; index--)
            {
                PooledExplosionEffect effect = _activeEffects[index];
                if (effect.HasFinished(_watchdogTimeout))
                {
                    _activeEffects.RemoveAt(index);
                    _pool.Release(effect);
                }
            }
        }

        private void OnDisable()
        {
            DisposePool();
        }

        private void OnDestroy()
        {
            DisposePool();
        }

        public void Play(Vector3 position, float radius)
        {
            if (!isActiveAndEnabled || _pool == null || !(radius > 0f) || float.IsInfinity(radius)
                || _activeEffects.Count >= _capacityLimit || _pool.CountInactive == 0)
            {
                return;
            }
            // ObjectPool's maxSize limits inactive storage, so never allow a lazy creation in combat.
            PooledExplosionEffect effect = _pool.Get();
            effect.PreparePlayback(position, radius);
            _activeEffects.Add(effect);
            effect.Play();
        }

        private void Initialize()
        {
            if (_pool != null)
            {
                return;
            }
            if (_effectPrefab == null || _prewarmCount < 1 || _capacityLimit < _prewarmCount
                || !(_watchdogTimeout > 0f) || float.IsInfinity(_watchdogTimeout)
                || (transform.lossyScale - Vector3.one).sqrMagnitude > 0.000001f)
            {
                Debug.LogError("Explosion VFX pool requires an authored effect, valid prewarm/capacity/timeout, and unit world scale.", this);
                return;
            }
            if (!_effectPrefab.ValidateConfiguration())
            {
                return;
            }

            _activeEffects = new List<PooledExplosionEffect>(_capacityLimit);
            _pool = new ObjectPool<PooledExplosionEffect>(CreateEffect, null, ReleaseEffect,
                DestroyEffect, false, _capacityLimit, _capacityLimit);
            for (int index = 0; index < _prewarmCount; index++)
            {
                _activeEffects.Add(_pool.Get());
            }
            foreach (PooledExplosionEffect effect in _activeEffects)
            {
                _pool.Release(effect);
            }
            _activeEffects.Clear();
        }

        private PooledExplosionEffect CreateEffect()
        {
            PooledExplosionEffect effect = Instantiate(_effectPrefab, transform);
            // Warm the nested VFXPlayer's Awake cache before the first combat playback.
            effect.gameObject.SetActive(true);
            effect.gameObject.SetActive(false);
            return effect;
        }

        private void ReleaseEffect(PooledExplosionEffect effect)
        {
            effect.StopPlayback();
            effect.gameObject.SetActive(false);
        }

        private void DestroyEffect(PooledExplosionEffect effect)
        {
            effect.StopPlayback();
            Destroy(effect.gameObject);
        }

        private void DisposePool()
        {
            if (_pool == null)
            {
                return;
            }
            foreach (PooledExplosionEffect effect in _activeEffects)
            {
                _pool.Release(effect);
            }
            _activeEffects.Clear();
            _pool.Dispose();
            _pool = null;
        }
    }
}
