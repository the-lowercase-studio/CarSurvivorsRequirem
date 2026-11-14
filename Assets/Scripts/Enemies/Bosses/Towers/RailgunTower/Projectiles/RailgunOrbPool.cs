using System;
using System.Collections.Generic;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Constants;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.Scripts.Enemies.Bosses.Towers.RailgunTower.Projectiles
{
    public interface IRailgunOrbPool
    {
        RailgunOrbProjectile Get();
        void ReturnAll();
    }

    public class RailgunOrbPool : MonoBehaviour, IRailgunOrbPool
    {
        [Tooltip("Prefab instantiated for railgun explosive orbs.")]
        [SerializeField] private RailgunOrbProjectile _orbPrefab;
        [Tooltip("Initial capacity of the orb object pool.")]
        [SerializeField] private int _defaultCapacity = RailgunTowerConstants.ORB_POOL_CAPACITY;
        [Tooltip("Maximum allowed size of the orb object pool.")]
        [SerializeField] private int _maxSize = RailgunTowerConstants.ORB_POOL_MAX_SIZE;

        private readonly List<RailgunOrbProjectile> _activeOrbs = new List<RailgunOrbProjectile>();
        private IObjectPool<RailgunOrbProjectile> _pool;

        private void Awake()
        {
            if (_orbPrefab == null)
            {
                throw new InvalidOperationException($"[{nameof(RailgunOrbPool)}] _orbPrefab is unassigned.");
            }

            _pool = new ObjectPool<RailgunOrbProjectile>(
                createFunc: CreateOrb,
                actionOnGet: OnGetOrb,
                actionOnRelease: OnReleaseOrb,
                actionOnDestroy: OnDestroyOrb,
                collectionCheck: false,
                defaultCapacity: _defaultCapacity,
                maxSize: _maxSize
            );
        }

        private void OnDestroy()
        {
            ReturnAll();
        }

        public RailgunOrbProjectile Get()
        {
            RailgunOrbProjectile orb = _pool.Get();
            _activeOrbs.Add(orb);
            return orb;
        }

        public void ReturnAll()
        {
            while (_activeOrbs.Count > 0)
            {
                int lastIndex = _activeOrbs.Count - 1;
                RailgunOrbProjectile orb = _activeOrbs[lastIndex];
                if (orb != null && orb.gameObject != null && orb.gameObject.activeSelf)
                {
                    orb.ReturnToPool();
                }
                else
                {
                    _activeOrbs.RemoveAt(lastIndex);
                }
            }
        }

        private RailgunOrbProjectile CreateOrb()
        {
            RailgunOrbProjectile instance = Instantiate(_orbPrefab, transform);
            instance.SetPool(_pool);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private void OnGetOrb(RailgunOrbProjectile orb)
        {
        }

        private void OnReleaseOrb(RailgunOrbProjectile orb)
        {
            if (orb != null && orb.gameObject != null)
            {
                orb.gameObject.SetActive(false);
                _activeOrbs.Remove(orb);
            }
        }

        private void OnDestroyOrb(RailgunOrbProjectile orb)
        {
            if (orb != null && orb.gameObject != null)
            {
                Destroy(orb.gameObject);
            }
        }
    }
}
