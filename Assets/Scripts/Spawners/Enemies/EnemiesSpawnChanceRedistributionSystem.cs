using Assets.Scripts.Common.Types;
using Assets.Scripts.Initializers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Spawners.Enemies
{
    public class EnemiesSpawnChanceRedistributionSystem
        : IInitializable<EnemiesSpawnChanceRedistributionSystem.Configuration>
    {
        public class Configuration
        {
            public List<EnemySpawnInfo> EnemiesInfo;
            public FloatValueRange SpawnChanceDecreaseFactor;
        }

        private FloatValueRange _spawnChanceDecreaseFactor;
        private EnemySpawnInfo _currentEnemyInfoSpawnChanceSource;
        private List<EnemySpawnInfo> _spawnChanceSystemEnemiesInfo;
        public float RedistributionFactorBonus { get; private set; } = 0f;

        public void Initialize(Configuration config)
        {
            _spawnChanceDecreaseFactor = config.SpawnChanceDecreaseFactor;
            _spawnChanceSystemEnemiesInfo = config.EnemiesInfo
                .Where(info => !info.SpawnChanceInfo.SpawnChanceWillNotChange)
                .ToList();

            _currentEnemyInfoSpawnChanceSource = _spawnChanceSystemEnemiesInfo[0];
        }

        public bool IsInitialized()
        {
            return _spawnChanceSystemEnemiesInfo is not null;
        }

        public void IncreaseSpawnChanceRedistributionFactor(float amount)
        {
            RedistributionFactorBonus += amount;
        }

        public void RedistributeSpawnChance()
        {
            if (!IsInitialized() || _currentEnemyInfoSpawnChanceSource == null)
                return;

            UpdateThresholdFlags();

            int lastIndex = _spawnChanceSystemEnemiesInfo.Count - 1;

            while (_currentEnemyInfoSpawnChanceSource != null)
            {
                int currentIndex = _spawnChanceSystemEnemiesInfo.IndexOf(_currentEnemyInfoSpawnChanceSource);

                if (currentIndex == lastIndex)
                {
                    break;
                }

                float spawnChanceScalar = _spawnChanceDecreaseFactor.GetRandomValueInRange() + RedistributionFactorBonus;
                float currentSpawnChance = _currentEnemyInfoSpawnChanceSource.SpawnChanceInfo.SpawnChance;

                if (currentSpawnChance > spawnChanceScalar)
                {
                    _currentEnemyInfoSpawnChanceSource.SpawnChanceInfo.SpawnChance -= spawnChanceScalar;
                    DistributeSpawnChanceWithFlag(currentIndex, spawnChanceScalar);
                    break;
                }
                else
                {
                    float remainingToDistribute = currentSpawnChance;
                    _currentEnemyInfoSpawnChanceSource.SpawnChanceInfo.SpawnChance = 0;

                    int nextIndex = currentIndex + 1;

                    if (nextIndex == lastIndex)
                    {
                        _spawnChanceSystemEnemiesInfo[lastIndex].SpawnChanceInfo.SpawnChance += remainingToDistribute;
                        _currentEnemyInfoSpawnChanceSource = null;
                        break;
                    }

                    DistributeSpawnChanceWithFlag(currentIndex, remainingToDistribute);
                    _currentEnemyInfoSpawnChanceSource = _spawnChanceSystemEnemiesInfo[nextIndex];
                }
            }
        }

        private void UpdateThresholdFlags()
        {
            foreach (var info in _spawnChanceSystemEnemiesInfo)
            {
                var spawnInfo = info.SpawnChanceInfo;
                if (!spawnInfo.HasEverReachedThreshold)
                    spawnInfo.HasEverReachedThreshold = spawnInfo.HasReachedTresholdToStartAddingSpawnChanceToOtherInfos();
            }
        }

        private void DistributeSpawnChanceWithFlag(int currentIndex, float value)
        {
            int lastIndex = _spawnChanceSystemEnemiesInfo.Count - 1;
            List<EnemySpawnInfo> eligible = GetEligibleEntries(currentIndex, lastIndex);

            if (eligible.Count == 0)
            {
                _spawnChanceSystemEnemiesInfo[lastIndex].SpawnChanceInfo.SpawnChance += value;
                return;
            }

            DistributeGeometric(value, eligible);
        }

        private List<EnemySpawnInfo> GetEligibleEntries(int currentIndex, int lastIndex)
        {
            var eligible = new List<EnemySpawnInfo>();
            for (int i = currentIndex; i < lastIndex && _spawnChanceSystemEnemiesInfo[i].SpawnChanceInfo.HasEverReachedThreshold; i++)
            {
                eligible.Add(_spawnChanceSystemEnemiesInfo[i + 1]);
            }
            return eligible;
        }

        private void DistributeGeometric(float value, List<EnemySpawnInfo> eligible)
        {
            float remaining = value;
            for (int i = 0; i < eligible.Count; i++)
            {
                float portion;
                if (i < eligible.Count - 1)
                {
                    portion = value / (float)Mathf.Pow(2, i + 1);
                    eligible[i].SpawnChanceInfo.SpawnChance += portion;
                    remaining -= portion;
                }
                else
                {
                    eligible[i].SpawnChanceInfo.SpawnChance += remaining;
                }
            }
        }
    }
}
