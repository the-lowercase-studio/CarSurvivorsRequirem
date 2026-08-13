using UnityEngine;

namespace Assets.Scripts.Waves
{
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "Scriptable Objects/Waves/WaveConfig")]
    public class WaveConfig : ScriptableObject
    {
        [SerializeField] private float _startSpawnWaveDelay = 8f;
        [SerializeField] private float _firstWaveDelay = 1f;
        [SerializeField] private ushort _initialMaxEnemiesInWave = 4;
        [SerializeField] private float _maxEnemiesInWaveMultiplier = 1.2f;

        public float StartSpawnWaveDelay => _startSpawnWaveDelay;
        public float FirstWaveDelay => _firstWaveDelay;
        public ushort InitialMaxEnemiesInWave => _initialMaxEnemiesInWave;
        public float MaxEnemiesInWaveMultiplier => _maxEnemiesInWaveMultiplier;
    }
}
