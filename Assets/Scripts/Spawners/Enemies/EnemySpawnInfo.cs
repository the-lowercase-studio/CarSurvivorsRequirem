using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Spawners;
using System;
using UnityEngine;

namespace Assets.Scripts.Spawners.Enemies
{
    [Serializable]
    public class EnemySpawnInfo
    {
        public Enemy EnemyPrefab;
        public ushort MaxAmount;
        public SpawnChanceInfo SpawnChanceInfo;

        [Tooltip("Optional EnemyConfigSO override applied to pooled instances for this entry. Uses the prefab's authored Config when unassigned.")]
        [SerializeField] private EnemyConfigSO _configOverride;

        public EnemyConfigSO ConfigOverride => _configOverride;

        public EnemySpawnInfo()
        {
        }

        public EnemySpawnInfo(
            Enemy enemyPrefab,
            ushort maxAmount,
            SpawnChanceInfo spawnChanceInfo,
            EnemyConfigSO configOverride = null)
        {
            EnemyPrefab = enemyPrefab;
            MaxAmount = maxAmount;
            SpawnChanceInfo = spawnChanceInfo;
            _configOverride = configOverride;
        }

        public EnemySpawnInfo CreateRuntimeCopy()
        {
            SpawnChanceInfo clonedChance = SpawnChanceInfo != null
                ? new SpawnChanceInfo
                {
                    SpawnChance = SpawnChanceInfo.SpawnChance,
                    TresholdToStartAddingSpawnChanceToOtherInfos = SpawnChanceInfo.TresholdToStartAddingSpawnChanceToOtherInfos,
                    SpawnChanceWillNotChange = SpawnChanceInfo.SpawnChanceWillNotChange,
                    HasEverReachedThreshold = false
                }
                : new SpawnChanceInfo();

            return new EnemySpawnInfo(EnemyPrefab, MaxAmount, clonedChance, _configOverride);
        }
    }
}
