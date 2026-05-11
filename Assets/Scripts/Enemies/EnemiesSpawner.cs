using Assets.Scripts.Common.Types;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Pooling;
using Assets.Scripts.Spawners.GridSpace;
using Reflex.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

namespace Assets.Scripts.Enemies
{
    public class EnemiesSpawner : MonoBehaviour,
        IOnRandomGridPosSpawner<EnemiesSpawner>, IObjectReleaseNotifier
    {
        [Inject] private readonly IGridManager _gridManager;

        [Header("SpawnExpParticle Chance Settings")]
        [SerializeField] private FloatValueRange _spawnChanceDecreaseFactor;
        private EnemiesSpawnChanceRedistributionSystem _enemiesSpawnChanceRedistributionSystem = new();

        [Header("Enemies Pool settings")]
        [SerializeField] private Transform _enemiesParent;
        [SerializeField] private List<EnemySpawnInfo> _poolEnemiesInfo;
        private Dictionary<EnemySpawnInfo, ObjectPool<Enemy>> _enemyPools = new();

        public event EventHandler OnSpawnedEntityReleased;

        public uint CurrentlySpawnedObjectsCount { get; private set; }

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
        }

        private void PoolEnemies()
        {
            foreach (EnemySpawnInfo poolEnemyInfo in _poolEnemiesInfo)
            {
                ObjectPool<Enemy> currentEnemyPool = new(createFunc: () => Instantiate(poolEnemyInfo.EnemyPrefab, _enemiesParent),
                                                         actionOnGet: OnEnemyGet,
                                                         actionOnRelease: OnEnemyRelease,
                                                         defaultCapacity: poolEnemyInfo.MaxAmount,
                                                         maxSize: poolEnemyInfo.MaxAmount);

                _enemyPools.Add(poolEnemyInfo, currentEnemyPool);
            }
        }

        private void OnEnemyGet(Enemy enemy)
        {
            Cell cell = GridCellsNotVisibleByMainCamera
                .GetRandomWalkableCells(_gridManager.GridPlayerChunk, 1)
                .FirstOrDefault();

            if (cell == null)
            {
                Debug.LogWarning("No walkable cells found for enemy spawn.");
                return;
            }

            enemy.OnGet();

            enemy.OnCanBeReleased += Enemy_OnRelease;

            enemy.transform.position = cell.WorldPos;

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
            Enemy enemy = sender as Enemy;

            if (enemy is null)
            {
                return;
            }

            OnEnemyRelease(enemy);
        }

        public void SpawnAtRandomGridPos(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                EnemySpawnInfo currentEnemyToSpawnInfo = RandomEnemyInfoBasedOnSpawnChance();
                if (currentEnemyToSpawnInfo != null)
                {
                    _enemyPools[currentEnemyToSpawnInfo].Get();
                }
            }

            _enemiesSpawnChanceRedistributionSystem.RedistributeSpawnChance();
        }

        public void SpawnRandomEnemiesBasedOnSpawnChance(int count)
        {
            for (int i = 0; i < count; i++)
            {
                EnemySpawnInfo currentEnemyToSpawnInfo = RandomEnemyInfoBasedOnSpawnChance();
                if (currentEnemyToSpawnInfo != null)
                {
                    _enemyPools[currentEnemyToSpawnInfo].Get();
                }
            }

            _enemiesSpawnChanceRedistributionSystem.RedistributeSpawnChance();
        }

        private EnemySpawnInfo RandomEnemyInfoBasedOnSpawnChance()
        {
            float totalChance = _poolEnemiesInfo.Sum(info => info.SpawnChanceInfo.SpawnChance);
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
