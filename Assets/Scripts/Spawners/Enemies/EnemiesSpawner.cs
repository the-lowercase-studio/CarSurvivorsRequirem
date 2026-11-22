using Assets.Scripts.Common.Types;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Pooling;
using Assets.Scripts.ReflexDI;
using Assets.Scripts.Spawners.GridSpace;
using Assets.Scripts.VFX;
using Reflex.Attributes;
using Reflex.Core;
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
        bool TrySpawnEnemyAt(EnemySpawnInfo enemyInfo, Vector3 position, out Enemy enemy);
    }

    public interface IActiveEnemiesProvider
    {
        int CopyActiveEnemies(List<Enemy> buffer);
    }

    public class EnemiesSpawner : MonoBehaviour,
        IOnRandomGridPosSpawner<EnemiesSpawner>, ISwarmEnemySpawner, IEnemySpawnDifficultyController, IActiveEnemiesProvider
    {
        [Inject] private readonly IGridManager _gridManager = null;
        [Inject] private readonly Camera _mainCamera = null;
        [Inject] private readonly IRunSessionService _runSessionService = null;
        [Inject] private readonly Container _container = null;

        [Header("SpawnExpParticle Chance Settings")]
        [SerializeField] private FloatValueRange _spawnChanceDecreaseFactor;

        [Header("Enemies Pool settings")]
        [SerializeField] private Transform _enemiesParent;
        [SerializeField] private List<EnemySpawnInfo> _poolEnemiesInfo;
        [SerializeField] private VFXPlayer _swarmSpawnVfxPrefab;

        [Header("Spawn Distance & Occupancy Settings")]
        [SerializeField] private int _outerSpawnBufferCells = 8;
        [SerializeField] private int _maxEnemiesPerCell = 2;
        [SerializeField] private float _currentRedistributionFactorBonus = 0f;

        private EnemiesSpawnChanceRedistributionSystem _enemiesSpawnChanceRedistributionSystem = new();
        private readonly List<EnemySpawnInfo> _runtimeEnemiesInfo = new();
        private FloatValueRange _runtimeSpawnChanceDecreaseFactor;
        private Dictionary<EnemySpawnInfo, ObjectPool<Enemy>> _enemyPools = new();
        private Dictionary<Enemy, ObjectPool<Enemy>> _instancePoolMap = new();
        private readonly HashSet<Enemy> _activeEnemies = new();

        private readonly Dictionary<VFXPlayer, PendingSpawn> _pendingSpawns = new();
        private Transform _injectionStagingParent;
        private bool _isPreWarming;

        private sealed class PendingSpawn
        {
            public Enemy Enemy { get; }
            public uint SpawnToken { get; }
            public Cell Cell { get; }
            public Vector3 Position { get; }

            public PendingSpawn(Enemy enemy, uint spawnToken, Cell cell, Vector3 position)
            {
                Enemy = enemy;
                SpawnToken = spawnToken;
                Cell = cell;
                Position = position;
            }
        }

        private void OnDisable()
        {
            foreach (var pending in _pendingSpawns)
            {
                if (pending.Key != null)
                {
                    pending.Key.OnVFXFinished -= HandleSwarmVfxFinished;
                }

                PendingSpawn pendingSpawn = pending.Value;
                if (pendingSpawn != null
                    && pendingSpawn.Enemy != null
                    && pendingSpawn.Enemy.isActiveAndEnabled
                    && pendingSpawn.Enemy.IsMaterializing
                    && pendingSpawn.Enemy.MaterializationToken == pendingSpawn.SpawnToken)
                {
                    pendingSpawn.Enemy.ReturnToPool();
                }
            }

            _pendingSpawns.Clear();
        }

        private void OnDestroy()
        {
            if (_injectionStagingParent != null)
            {
                Destroy(_injectionStagingParent.gameObject);
                _injectionStagingParent = null;
            }
        }

        public event EventHandler OnSpawnedEntityReleased;

        public uint CurrentlySpawnedObjectsCount { get; private set; }
        public IReadOnlyList<EnemySpawnInfo> EnemyConfigs => _runtimeEnemiesInfo;

        private void Awake()
        {
            _injectionStagingParent = ReflexPrefabInstantiator.CreateInactiveStagingParent(nameof(EnemiesSpawner));
            InitializeRuntimeEnemyConfigs();
            PoolEnemies();
        }

        private void Start()
        {
            EnemiesSpawnChanceRedistributionSystem.Configuration config = new()
            {
                EnemiesInfo = _runtimeEnemiesInfo,
                SpawnChanceDecreaseFactor = _runtimeSpawnChanceDecreaseFactor
            };

            _enemiesSpawnChanceRedistributionSystem.Initialize(config);
            _currentRedistributionFactorBonus = _enemiesSpawnChanceRedistributionSystem.RedistributionFactorBonus;
            PreWarmPools();
        }

        private void InitializeRuntimeEnemyConfigs()
        {
            _runtimeEnemiesInfo.Clear();

            IReadOnlyList<EnemySpawnInfo> sourceInfos = _poolEnemiesInfo;
            FloatValueRange sourceDecreaseFactor = _spawnChanceDecreaseFactor;

            TierEnemySpawningConfig tierSpawning = _runSessionService?.ActiveTierConfig?.EnemySpawning;
            if (tierSpawning != null && tierSpawning.EnemySpawnInfos != null && tierSpawning.EnemySpawnInfos.Count > 0)
            {
                sourceInfos = tierSpawning.EnemySpawnInfos;
                if (tierSpawning.SpawnChanceDecreaseFactor != null)
                {
                    sourceDecreaseFactor = tierSpawning.SpawnChanceDecreaseFactor;
                }
            }

            _runtimeSpawnChanceDecreaseFactor = sourceDecreaseFactor != null
                ? new FloatValueRange(sourceDecreaseFactor.Min, sourceDecreaseFactor.Max)
                : new FloatValueRange(1.5f, 2.5f);

            if (sourceInfos == null)
            {
                return;
            }

            for (int i = 0; i < sourceInfos.Count; i++)
            {
                EnemySpawnInfo source = sourceInfos[i];
                if (source == null || source.EnemyPrefab == null)
                {
                    continue;
                }

                _runtimeEnemiesInfo.Add(source.CreateRuntimeCopy());
            }
        }

        private void PoolEnemies()
        {
            foreach (EnemySpawnInfo poolEnemyInfo in _runtimeEnemiesInfo)
            {
                EnemySpawnInfo localInfo = poolEnemyInfo;
                Action<Enemy> beforeActivate = localInfo.ConfigOverride != null
                    ? enemy => enemy.SetBaseConfig(localInfo.ConfigOverride)
                    : null;

                ObjectPool<Enemy> currentEnemyPool = new(
                    createFunc: () => ReflexPrefabInstantiator.InstantiateInjectedObject(
                        localInfo.EnemyPrefab,
                        _enemiesParent,
                        _container,
                        _injectionStagingParent,
                        beforeActivate),
                    actionOnGet: OnEnemyGet,
                    actionOnRelease: OnEnemyRelease,
                    actionOnDestroy: enemy => Destroy(enemy.gameObject),
                    defaultCapacity: localInfo.MaxAmount,
                    maxSize: localInfo.MaxAmount);

                _enemyPools.Add(localInfo, currentEnemyPool);
            }
        }

        private void PreWarmPools()
        {
            _isPreWarming = true;
            try
            {
                foreach (var kvp in _enemyPools)
                {
                    EnemySpawnInfo info = kvp.Key;
                    ObjectPool<Enemy> pool = kvp.Value;

                    List<Enemy> temp = new List<Enemy>(info.MaxAmount);
                    for (int i = 0; i < info.MaxAmount; i++)
                    {
                        Enemy enemy = pool.Get();
                        _instancePoolMap[enemy] = pool;
                        temp.Add(enemy);
                    }
                    foreach (Enemy enemy in temp)
                    {
                        pool.Release(enemy);
                    }
                }
            }
            finally
            {
                _isPreWarming = false;
            }
        }

        private void OnEnemyGet(Enemy enemy)
        {
            _activeEnemies.Add(enemy);
            enemy.OnGet();

            enemy.OnCanBeReleased += Enemy_OnRelease;

            enemy.gameObject.SetActive(true);

            CurrentlySpawnedObjectsCount++;
        }

        private void OnEnemyRelease(Enemy enemy)
        {
            _activeEnemies.Remove(enemy);
            enemy.OnRelease();

            enemy.OnCanBeReleased -= Enemy_OnRelease;

            enemy.gameObject.SetActive(false);

            _instancePoolMap.Remove(enemy);

            if (!_isPreWarming)
            {
                OnSpawnedEntityReleased?.Invoke(enemy, EventArgs.Empty);
            }

            CurrentlySpawnedObjectsCount--;
        }

        private void Enemy_OnRelease(object sender, EventArgs e)
        {
            if (sender is Enemy enemy)
            {
                if (_instancePoolMap.TryGetValue(enemy, out ObjectPool<Enemy> pool))
                {
                    pool.Release(enemy);
                }
                else
                {
                    OnEnemyRelease(enemy);
                }
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
                _maxEnemiesPerCell,
                includeRemainingCandidates: true
            );
            using (var enumerator = cells.GetEnumerator())
            {
                for (int i = 0; i < count; i++)
                {
                    if (!enumerator.MoveNext())
                    {
                        break;
                    }
                    if (!GridCellsNotVisibleByMainCamera.TryGetPlacement(enumerator.Current,
                        _gridManager.WorldGrid, _mainCamera, _maxEnemiesPerCell, out Vector3 supportedPosition))
                    {
                        i--;
                        continue;
                    }

                    EnemySpawnInfo currentEnemyToSpawnInfo = RandomEnemyInfoBasedOnSpawnChance();
                    if (currentEnemyToSpawnInfo != null && _enemyPools.TryGetValue(currentEnemyToSpawnInfo, out var pool))
                    {
                        Enemy enemy = pool.Get();
                        _instancePoolMap[enemy] = pool;
                        enemy.transform.position = supportedPosition;
                        enemy.MovementController.ResetAfterRelocation();
                    }
                }
            }

            _enemiesSpawnChanceRedistributionSystem.RedistributeSpawnChance();
        }

        public void SpawnSpecificEnemy(EnemySpawnInfo enemyInfo, int count = 1)
        {
            if (enemyInfo == null || !_enemyPools.TryGetValue(enemyInfo, out ObjectPool<Enemy> pool))
            {
                return;
            }

            IEnumerable<Cell> cells = GridCellsNotVisibleByMainCamera.GetRandomWalkableCells(
                _gridManager.GridPlayerChunk,
                _mainCamera,
                count,
                _maxEnemiesPerCell,
                includeRemainingCandidates: true,
                requireHiddenFromCamera: false
            );
            using (var enumerator = cells.GetEnumerator())
            {
                for (int i = 0; i < count; i++)
                {
                    if (!enumerator.MoveNext())
                    {
                        break;
                    }
                    Cell candidate = enumerator.Current;
                    if (!GridCellsNotVisibleByMainCamera.TryGetPlacement(candidate, _gridManager.GridPlayerChunk,
                        _mainCamera, _maxEnemiesPerCell, requireHiddenFromCamera: false, out Vector3 spawnPos))
                    {
                        i--;
                        continue;
                    }

                    if (_swarmSpawnVfxPrefab != null)
                    {
                        VFXPlayer vfx = Instantiate(_swarmSpawnVfxPrefab, spawnPos, Quaternion.identity);
                        Quaternion spawnRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                        Enemy enemy = CheckoutEnemyAt(pool, spawnPos, spawnRotation, applyCustomRotation: true);
                        enemy.BeginMaterialization(0f);
                        _pendingSpawns.Add(
                            vfx,
                            new PendingSpawn(enemy, enemy.MaterializationToken, candidate, spawnPos));
                        vfx.OnVFXFinished += HandleSwarmVfxFinished;
                        vfx.Play(new VFXPlayConfig(scale: 1f, destroyOnEnd: true));
                        if (enemy.IsMaterializing && _pendingSpawns.ContainsKey(vfx))
                        {
                            enemy.SetMaterializationDuration(vfx.LastScheduledDuration);
                        }
                    }
                    else
                    {
                        SpawnAtSupportedPosition(pool, spawnPos);
                    }
                }
            }
        }

        public bool TrySpawnEnemyAt(EnemySpawnInfo enemyInfo, Vector3 position, out Enemy enemy)
        {
            enemy = null;
            if (enemyInfo == null || !_enemyPools.TryGetValue(enemyInfo, out ObjectPool<Enemy> pool))
            {
                return false;
            }

            enemy = CheckoutEnemyAt(pool, position, Quaternion.identity, applyCustomRotation: false);
            return true;
        }

        private void HandleSwarmVfxFinished(object sender, EventArgs args)
        {
            if (sender is not VFXPlayer vfx || !_pendingSpawns.TryGetValue(vfx, out PendingSpawn pending))
            {
                return;
            }

            vfx.OnVFXFinished -= HandleSwarmVfxFinished;
            _pendingSpawns.Remove(vfx);

            Enemy enemy = pending.Enemy;
            if (enemy == null
                || !enemy.isActiveAndEnabled
                || !enemy.IsMaterializing
                || enemy.MaterializationToken != pending.SpawnToken)
            {
                return;
            }

            // Spawn into committed circle indicators even if the camera view or player chunk moved during the VFX.
            if (!isActiveAndEnabled
                || pending.Cell == null
                || !CellStatusDescriber.IsWalkable(pending.Cell)
                || !GroundSupportQuery.TryGetSupportedPosition(pending.Cell.WorldPos, out Vector3 currentPosition)
                || (currentPosition - pending.Position).sqrMagnitude > 0.01f)
            {
                enemy.ReturnToPool();
                return;
            }

            enemy.CompleteMaterialization();
        }

        private void SpawnAtSupportedPosition(ObjectPool<Enemy> pool, Vector3 position)
        {
            CheckoutEnemyAt(pool, position, Quaternion.identity, applyCustomRotation: false);
        }

        private Enemy CheckoutEnemyAt(ObjectPool<Enemy> pool, Vector3 position, Quaternion rotation, bool applyCustomRotation)
        {
            Enemy enemy = pool.Get();
            _instancePoolMap[enemy] = pool;
            if (applyCustomRotation)
            {
                enemy.transform.SetPositionAndRotation(position, rotation);
            }
            else
            {
                enemy.transform.position = position;
            }

            enemy.MovementController.ResetAfterRelocation();
            return enemy;
        }

        public void IncreaseSpawnChanceRedistributionFactor(float amount)
        {
            _enemiesSpawnChanceRedistributionSystem.IncreaseSpawnChanceRedistributionFactor(amount);
            _currentRedistributionFactorBonus = _enemiesSpawnChanceRedistributionSystem.RedistributionFactorBonus;
        }

        public int CopyActiveEnemies(List<Enemy> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            foreach (Enemy enemy in _activeEnemies)
            {
                buffer.Add(enemy);
            }

            return buffer.Count;
        }

        private EnemySpawnInfo RandomEnemyInfoBasedOnSpawnChance()
        {
            float totalChance = 0f;
            foreach (EnemySpawnInfo enemySpawnInfo in _runtimeEnemiesInfo)
            {
                float chance = enemySpawnInfo.SpawnChanceInfo.SpawnChance;
                if (chance > 0f)
                {
                    totalChance += chance;
                }
            }

            if (totalChance <= 0f)
            {
                return null;
            }

            float randomPoint = UnityEngine.Random.value * totalChance;

            float currentSum = 0f;
            foreach (EnemySpawnInfo enemySpawnInfo in _runtimeEnemiesInfo)
            {
                float chance = enemySpawnInfo.SpawnChanceInfo.SpawnChance;
                if (chance <= 0f)
                {
                    continue;
                }

                currentSum += chance;
                if (currentSum >= randomPoint)
                {
                    return enemySpawnInfo;
                }
            }

            return null;
        }
    }
}
