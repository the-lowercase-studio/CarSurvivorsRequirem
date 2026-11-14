using System;
using System.Collections.Generic;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Constants;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Combat
{
    public interface IRailgunWavePool
    {
        RailgunLinearWaveHitbox Get();
        void ReturnAll();
    }

    public class RailgunWavePool : MonoBehaviour, IRailgunWavePool
    {
        [Tooltip("Prefab instantiated for bidirectional linear wave hitbox pairs.")]
        [SerializeField] private RailgunLinearWaveHitbox _wavePrefab;
        [Tooltip("Initial capacity of the wave object pool.")]
        [SerializeField] private int _defaultCapacity = RailgunTowerConstants.WAVE_POOL_CAPACITY;
        [Tooltip("Maximum allowed size of the wave object pool.")]
        [SerializeField] private int _maxSize = RailgunTowerConstants.WAVE_POOL_MAX_SIZE;

        private readonly List<RailgunLinearWaveHitbox> _activeWaves = new List<RailgunLinearWaveHitbox>();
        private IObjectPool<RailgunLinearWaveHitbox> _pool;

        private void Awake()
        {
            if (_wavePrefab == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunWavePool)}] _wavePrefab is unassigned.");
            }

            _pool = new ObjectPool<RailgunLinearWaveHitbox>(
                createFunc: CreateWave,
                actionOnGet: OnGetWave,
                actionOnRelease: OnReleaseWave,
                actionOnDestroy: OnDestroyWave,
                collectionCheck: false,
                defaultCapacity: _defaultCapacity,
                maxSize: _maxSize
            );
        }

        private void OnDestroy()
        {
            ReturnAll();
        }

        public RailgunLinearWaveHitbox Get()
        {
            RailgunLinearWaveHitbox wave = _pool.Get();
            _activeWaves.Add(wave);
            return wave;
        }

        public void ReturnAll()
        {
            while (_activeWaves.Count > 0)
            {
                int lastIndex = _activeWaves.Count - 1;
                RailgunLinearWaveHitbox wave = _activeWaves[lastIndex];
                if (wave != null && wave.gameObject != null && wave.gameObject.activeSelf)
                {
                    wave.ReturnToPool();
                }
                else
                {
                    _activeWaves.RemoveAt(lastIndex);
                }
            }
        }

        private RailgunLinearWaveHitbox CreateWave()
        {
            RailgunLinearWaveHitbox instance = Instantiate(_wavePrefab, transform);
            instance.SetPool(_pool);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private void OnGetWave(RailgunLinearWaveHitbox wave)
        {
        }

        private void OnReleaseWave(RailgunLinearWaveHitbox wave)
        {
            if (wave != null && wave.gameObject != null)
            {
                wave.gameObject.SetActive(false);
                _activeWaves.Remove(wave);
            }
        }

        private void OnDestroyWave(RailgunLinearWaveHitbox wave)
        {
            if (wave != null && wave.gameObject != null)
            {
                Destroy(wave.gameObject);
            }
        }
    }
}
