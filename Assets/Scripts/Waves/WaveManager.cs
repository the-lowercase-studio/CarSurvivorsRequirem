using Assets.Scripts.Enemies;
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

        [SerializeField] private float _startSpawnWaveDelay = 8f;
        private float _currentSpawnWaveDelay;
        private float _firstWaveDelay = 1f;
        private ushort _maxEnemiesInWave = 4;
        private float _maxEnemiesInWaveMultiplier = 1.2f;
        private ushort _wave = 1;

        public bool IsFrozen { get; set; }

        private void Start()
        {
            _currentSpawnWaveDelay = _firstWaveDelay;
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
                _currentSpawnWaveDelay = _startSpawnWaveDelay;
            }
        }

        private void SpawnWave()
        {
            _enemiesSpawner.SpawnAtRandomGridPos(_maxEnemiesInWave);
            float maxEnemiesInWave = _maxEnemiesInWave * _maxEnemiesInWaveMultiplier;
            if (maxEnemiesInWave < ushort.MaxValue)
            {
                _maxEnemiesInWave = (ushort)(_maxEnemiesInWave * _maxEnemiesInWaveMultiplier);
            }
            else
            {
                _maxEnemiesInWave = ushort.MaxValue;
            }
            _wave++;
        }
    }
}
