using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.Spawners.GridSpace;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Waves
{
    public interface IWaveFreezer
    {
        bool IsFrozen { get; set; }
    }

    public class WaveManager : MonoBehaviour, IWaveFreezer
    {
        [Inject] private readonly IOnRandomGridPosSpawner<EnemiesSpawner> _enemiesSpawner = null;
        [Inject] private readonly ITimeFreezeState _timeFreezeState = null;
        [Inject] private readonly IRunSessionService _runSessionService = null;

        [SerializeField] private WaveConfig _config;

        private float _currentSpawnWaveDelay;
        private float _effectiveWaveDelay;
        private float _effectiveGrowthMultiplier;
        private ushort _maxEnemiesInWave;
        private ushort _maxEnemiesInWaveCap = ushort.MaxValue;
        private ushort _wave = 1;

        public bool IsFrozen { get; set; }

        private void Start()
        {
            float firstWaveDelay = _config != null ? _config.FirstWaveDelay : 1f;
            float baseWaveDelay = _config != null ? _config.StartSpawnWaveDelay : 8f;
            ushort baseInitialMax = _config != null ? _config.InitialMaxEnemiesInWave : (ushort)4;
            float baseGrowthMultiplier = _config != null ? _config.MaxEnemiesInWaveMultiplier : 1.2f;

            TierEnemySpawningConfig tierSpawning = _runSessionService?.ActiveTierConfig?.EnemySpawning;
            if (tierSpawning != null)
            {
                _effectiveWaveDelay = Mathf.Max(0.1f, baseWaveDelay * tierSpawning.WaveDelayMultiplier);
                _effectiveGrowthMultiplier = Mathf.Max(0.1f, baseGrowthMultiplier * tierSpawning.WaveGrowthMultiplier);
                _maxEnemiesInWaveCap = (ushort)Mathf.Max(1, tierSpawning.MaxEnemiesInWaveCap);
                int scaledInitial = Mathf.RoundToInt(baseInitialMax * tierSpawning.WaveInitialCountMultiplier);
                _maxEnemiesInWave = (ushort)Mathf.Clamp(scaledInitial, 1, _maxEnemiesInWaveCap);
            }
            else
            {
                _effectiveWaveDelay = Mathf.Max(0.1f, baseWaveDelay);
                _effectiveGrowthMultiplier = Mathf.Max(0.1f, baseGrowthMultiplier);
                _maxEnemiesInWaveCap = ushort.MaxValue;
                _maxEnemiesInWave = (ushort)Mathf.Clamp(baseInitialMax, 1, _maxEnemiesInWaveCap);
            }

            _currentSpawnWaveDelay = firstWaveDelay;
        }

        private void Update()
        {
            AdvanceWaves(Time.deltaTime);
        }

        public void AdvanceWaves(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }

            if (IsFrozen || (_timeFreezeState != null && _timeFreezeState.IsFrozen))
            {
                return;
            }

            WavesProcess(deltaTime);
        }

        private void WavesProcess(float deltaTime)
        {
            if ((_wave == 1 || _enemiesSpawner.CurrentlySpawnedObjectsCount > 0) && _currentSpawnWaveDelay > 0)
            {
                _currentSpawnWaveDelay -= deltaTime;
            }
            else
            {
                SpawnWave();
                _currentSpawnWaveDelay = _effectiveWaveDelay;
            }
        }

        private void SpawnWave()
        {
            _enemiesSpawner.SpawnAtRandomGridPos(_maxEnemiesInWave);
            float maxEnemiesInWave = _maxEnemiesInWave * _effectiveGrowthMultiplier;
            if (maxEnemiesInWave < _maxEnemiesInWaveCap)
            {
                _maxEnemiesInWave = (ushort)Mathf.Max(1, Mathf.RoundToInt(maxEnemiesInWave));
            }
            else
            {
                _maxEnemiesInWave = _maxEnemiesInWaveCap;
            }
            _wave++;
        }
    }
}
