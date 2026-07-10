using Assets.Scripts.Common.Types;
using Assets.Scripts.Enemies;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Pooling;
using Assets.Scripts.Spawners.GridSpace;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.Scripts.Spawners.Enemies
{
    public interface IEnemySpawnDifficultyController
    {
        void IncreaseSpawnChanceRedistributionFactor(float amount);
    }

    public interface ISwarmEnemySpawner
    {
        IReadOnlyList<EnemySpawnInfo> EnemyConfigs { get; }
        void SpawnSpecificEnemy(EnemySpawnInfo enemyInfo, int count = 1);
    }

    public class EnemiesSpawner : MonoBehaviour,
        IOnRandomGridPosSpawner<EnemiesSpawner>, IObjectReleaseNotifier, ISwarmEnemySpawner, IEnemySpawnDifficultyController
    {
        [Inject] private readonly IGridManager _gridManager;
        [Inject] private readonly Camera _mainCamera = null;

        [Header("SpawnExpParticle Chance Settings")]
        [SerializeField] private FloatValueRange _spawnChanceDecreaseFactor;

        [Header("Enemies Pool settings")]
        [SerializeField] private Transform _enemiesParent;
        [SerializeField] private List<EnemySpawnInfo> _poolEnemiesInfo;
        [SerializeField] private VFXPlayer _swarmSpawnVfxPrefab;

        [Header("Spawn Distance & Occupancy Settings")]
        [SerializeField] private int _outerSpawnBufferCells = 8;
        [SerializeField] private int _maxEnemiesPerCell = 2;

        private EnemiesSpawnChanceRedistributionSystem _enemiesSpawnChanceRedistributionSystem = new();
        [SerializeField] private float _currentRedistributionFactorBonus = 0f;
        private Dictionary<EnemySpawnInfo, ObjectPool<Enemy>> _enemyPools = new();

        public event EventHandler OnSpawnedEntityReleased;

        public uint CurrentlySpawnedObjectsCount { get; private set; }
        public IReadOnlyList<EnemySpawnInfo> EnemyConfigs => _poolEnemiesInfo;

        private void Awake()
        {
            PoolEnemies();
        }

        private void Start()
        {
            EnemiesSpawnChanceRedistributionSystem.Configuration config = new()
            {
                EnemiesInfo = _poolEnemiesInfo,
                SpawnChanceDecreaseFactor = _spawnChanceDecreaseFactor
            };

            _enemiesSpawnChanceRedistributionSystem.Initialize(config);
            _currentRedistributionFactorBonus = _enemiesSpawnChanceRedistributionSystem.RedistributionFactorBonus;
            PreWarmPools();
        }

        private void PoolEnemies()
        {
            foreach (EnemySpawnInfo poolEnemyInfo in _poolEnemiesInfo)
            {
                ObjectPool<Enemy> currentEnemyPool = new(createFunc: () => Instantiate(poolEnemyInfo.EnemyPrefab, _enemiesParent),
                                                         actionOnGet: OnEnemyGet,
                                                         actionOnRelease: OnEnemyRelease,
                                                         actionOnDestroy: enemy => Destroy(enemy.gameObject),
                                                         defaultCapacity: poolEnemyInfo.MaxAmount,
                                                         maxSize: poolEnemyInfo.MaxAmount);

                _enemyPools.Add(poolEnemyInfo, currentEnemyPool);
            }
        }

        private void PreWarmPools()
        {
            foreach (var kvp in _enemyPools)
            {
                EnemySpawnInfo info = kvp.Key;
                ObjectPool<Enemy> pool = kvp.Value;

                List<Enemy> temp = new List<Enemy>(info.MaxAmount);
                for (int i = 0; i < info.MaxAmount; i++)
                {
                    temp.Add(pool.Get());
                }
                foreach (Enemy enemy in temp)
                {
                    pool.Release(enemy);
                }
            }
        }

        private void OnEnemyGet(Enemy enemy)
        {
            enemy.OnGet();

            enemy.OnCanBeReleased += Enemy_OnRelease;

            enemy.gameObject.SetActive(true);

            CurrentlySpawnedObjectsCount++;
        }

        private void OnEnemyRelease(Enemy enemy)
        {
            enemy.OnRelease();

            enemy.OnCanBeReleased -= Enemy_OnRelease;

            enemy.gameObject.SetActive(false);

            OnSpawnedEntityReleased?.Invoke(enemy, EventArgs.Empty);

            CurrentlySpawnedObjectsCount--;
        }

        private void Enemy_OnRelease(object sender, EventArgs e)
        {
            if (sender is Enemy enemy)
            {
                OnEnemyRelease(enemy);
            }
        }

        public void SpawnAtRandomGridPos(int count = 1)
        {
            IEnumerable<Cell> cells = GridCellsNotVisibleByMainCamera.GetRandomWalkableCellsOutsidePlayerChunk(
                _gridManager.WorldGrid,
                _gridManager.GridPlayerChunk,
                _mainCamera,
                count,
                _outerSpawnBufferCells,
                _maxEnemiesPerCell
            );
            using (var enumerator = cells.GetEnumerator())
            {
                for (int i = 0; i < count; i++)
                {
                    if (!enumerator.MoveNext()) break;

                    EnemySpawnInfo currentEnemyToSpawnInfo = RandomEnemyInfoBasedOnSpawnChance();
                    if (currentEnemyToSpawnInfo != null)
                    {
                        Enemy enemy = _enemyPools[currentEnemyToSpawnInfo].Get();
                        enemy.transform.position = enumerator.Current.WorldPos;
                    }
                }
            }

            _enemiesSpawnChanceRedistributionSystem.RedistributeSpawnChance();
        }

        public void SpawnSpecificEnemy(EnemySpawnInfo enemyInfo, int count = 1)
        {
            if (!_enemyPools.TryGetValue(enemyInfo, out ObjectPool<Enemy> pool)) return;

            IEnumerable<Cell> cells = GridCellsNotVisibleByMainCamera.GetRandomWalkableCells(
                _gridManager.GridPlayerChunk,
                _mainCamera,
                count,
                _maxEnemiesPerCell
            );
            using (var enumerator = cells.GetEnumerator())
            {
                for (int i = 0; i < count; i++)
                {
                    if (!enumerator.MoveNext()) break;

                    Vector3 spawnPos = enumerator.Current.WorldPos;

                    if (_swarmSpawnVfxPrefab != null)
                    {
                        VFXPlayer vfxInstance = Instantiate(_swarmSpawnVfxPrefab, spawnPos, Quaternion.identity);

                        vfxInstance.Play(new VFXPlayConfig(scale: 1f, destroyOnEnd: true));

                        vfxInstance.OnVFXFinished += (sender, e) =>
                        {
                            if (this == null) return;
                            if (_enemyPools.TryGetValue(enemyInfo, out ObjectPool<Enemy> currentPool))
                            {
                                Enemy enemy = currentPool.Get();
                                enemy.transform.position = spawnPos;
                            }
                        };
                    }
                    else
                    {
                        Enemy enemy = pool.Get();
                        enemy.transform.position = spawnPos;
                    }
                }
            }
        }

        public void IncreaseSpawnChanceRedistributionFactor(float amount)
        {
            _enemiesSpawnChanceRedistributionSystem.IncreaseSpawnChanceRedistributionFactor(amount);
            _currentRedistributionFactorBonus = _enemiesSpawnChanceRedistributionSystem.RedistributionFactorBonus;
        }

        public void SpawnRandomEnemiesBasedOnSpawnChance(int count)
        {
            SpawnAtRandomGridPos(count);
        }

        private EnemySpawnInfo RandomEnemyInfoBasedOnSpawnChance()
        {
            float totalChance = 0;
            foreach (EnemySpawnInfo enemySpawnInfo in _poolEnemiesInfo)
            {
                totalChance += enemySpawnInfo.SpawnChanceInfo.SpawnChance;
            }

            float randomPoint = UnityEngine.Random.value * totalChance;

            float currentSum = 0;
            foreach (EnemySpawnInfo enemySpawnInfo in _poolEnemiesInfo)
            {
                currentSum += enemySpawnInfo.SpawnChanceInfo.SpawnChance;
                if (currentSum >= randomPoint)
                {
                    return enemySpawnInfo;
                }
            }

            return null;
        }
    }
}
