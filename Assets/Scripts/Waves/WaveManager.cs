using Assets.Scripts.Enemies.Base;
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
        [Inject] private readonly IOnRandomGridPosSpawner<EnemiesSpawner> _enemiesSpawner;

        [SerializeField] private WaveConfig _config;

        private float _currentSpawnWaveDelay;
        private ushort _maxEnemiesInWave;
        private ushort _wave = 1;

        public bool IsFrozen { get; set; }

        private void Start()
        {
            float firstWaveDelay = _config != null ? _config.FirstWaveDelay : 1f;
            _maxEnemiesInWave = _config != null ? _config.InitialMaxEnemiesInWave : (ushort)4;
            _currentSpawnWaveDelay = firstWaveDelay;
        }

        private void Update()
        {
            if (IsFrozen) return;

            WavesProcess();
        }

        private void WavesProcess()
        {
            if ((_wave == 1 || _enemiesSpawner.CurrentlySpawnedObjectsCount > 0) && _currentSpawnWaveDelay > 0)
            {
                _currentSpawnWaveDelay -= Time.deltaTime;
            }
            else
            {
                SpawnWave();
                float startWaveDelay = _config != null ? _config.StartSpawnWaveDelay : 8f;
                _currentSpawnWaveDelay = startWaveDelay;
            }
        }

        private void SpawnWave()
        {
            _enemiesSpawner.SpawnAtRandomGridPos(_maxEnemiesInWave);
            float multiplier = _config != null ? _config.MaxEnemiesInWaveMultiplier : 1.2f;
            float maxEnemiesInWave = _maxEnemiesInWave * multiplier;
            if (maxEnemiesInWave < ushort.MaxValue)
            {
                _maxEnemiesInWave = (ushort)maxEnemiesInWave;
            }
            else
            {
                _maxEnemiesInWave = ushort.MaxValue;
            }
            _wave++;
        }
    }
}
