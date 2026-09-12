using System.Collections.Generic;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Constants;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.Scripts.Enemies.Bosses.Towers.MortarTower.Projectiles
{
    public interface IMortarShellPool
    {
        MortarShellProjectile Get();
        void ReturnAll();
    }

    public class MortarShellPool : MonoBehaviour, IMortarShellPool
    {
        [Tooltip("Prefab instantiated for mortar shells.")]
        [SerializeField] private MortarShellProjectile _shellPrefab;
        [Tooltip("Initial capacity of the shell object pool.")]
        [SerializeField] private int _defaultCapacity = MortarTowerConstants.PROJECTILE_POOL_CAPACITY;
        [Tooltip("Maximum allowed size of the shell object pool.")]
        [SerializeField] private int _maxSize = MortarTowerConstants.PROJECTILE_POOL_MAX_SIZE;

        private readonly List<MortarShellProjectile> _activeShells = new List<MortarShellProjectile>();
        private IObjectPool<MortarShellProjectile> _pool;

        private void Awake()
        {
            _pool = new ObjectPool<MortarShellProjectile>(
                createFunc: CreateShell,
                actionOnGet: OnGetShell,
                actionOnRelease: OnReleaseShell,
                actionOnDestroy: OnDestroyShell,
                collectionCheck: false,
                defaultCapacity: _defaultCapacity,
                maxSize: _maxSize
            );
        }

        private void OnDestroy()
        {
            ReturnAll();
        }

        public MortarShellProjectile Get()
        {
            MortarShellProjectile shell = _pool.Get();
            _activeShells.Add(shell);
            return shell;
        }

        public void ReturnAll()
        {
            while (_activeShells.Count > 0)
            {
                int lastIndex = _activeShells.Count - 1;
                MortarShellProjectile shell = _activeShells[lastIndex];
                if (shell != null && shell.gameObject != null && shell.gameObject.activeSelf)
                {
                    shell.ReturnToPool();
                }
                else
                {
                    _activeShells.RemoveAt(lastIndex);
                }
            }
        }

        private MortarShellProjectile CreateShell()
        {
            MortarShellProjectile instance = Instantiate(_shellPrefab, transform);
            instance.SetPool(_pool);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private void OnGetShell(MortarShellProjectile shell)
        {
        }

        private void OnReleaseShell(MortarShellProjectile shell)
        {
            if (shell != null && shell.gameObject != null)
            {
                shell.gameObject.SetActive(false);
                _activeShells.Remove(shell);
            }
        }

        private void OnDestroyShell(MortarShellProjectile shell)
        {
            if (shell != null && shell.gameObject != null)
            {
                Destroy(shell.gameObject);
            }
        }
    }
}
