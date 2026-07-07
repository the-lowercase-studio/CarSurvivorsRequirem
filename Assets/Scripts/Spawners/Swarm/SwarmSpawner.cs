using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.UI.HUD;
using Assets.Scripts.Waves;
using Reflex.Attributes;
using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Spawners.Swarm
{
    public class SwarmSpawner : MonoBehaviour
    {
        [Inject] private readonly ISwarmEnemySpawner _swarmEnemySpawner;
        [Inject] private readonly IWaveFreezer _waveFreezer;
        [Inject] private readonly ISwarmNotificationPresenter _swarmNotificationPresenter;

        [Header("Swarm Timing")]
        [SerializeField] private float _minSwarmInterval = 120f;
        [SerializeField] private float _maxSwarmInterval = 180f;

        [Header("Swarm Size")]
        [SerializeField] private int _minSwarmSize = 80;
        [SerializeField] private int _maxSwarmSize = 100;

        [Header("Swarm Warning & Spawning Configuration")]
        [SerializeField] private float _swarmWarningDuration = 10f;
        [SerializeField] private float _swarmDuration = 5f;
        [SerializeField] private float _spawnTickInterval = 1f;

        private float _nextSwarmTime;
        private int _currentSwarmIndex;
        private bool _isSwarmActive;
        private Coroutine _swarmCoroutine;

        private void Start()
        {
            _nextSwarmTime = Random.Range(_minSwarmInterval, _maxSwarmInterval);
        }

        private void Update()
        {
            if (_isSwarmActive) return;

            _nextSwarmTime -= Time.deltaTime;

            if (_nextSwarmTime <= 0f)
            {
                StartSwarm();
            }
        }

        private void StartSwarm()
        {
            _isSwarmActive = true;

            int configCount = _swarmEnemySpawner.EnemyConfigs.Count;
            if (configCount == 0) return;

            int clampedIndex = Mathf.Min(_currentSwarmIndex, configCount - 1);
            EnemySpawnInfo selectedConfig = _swarmEnemySpawner.EnemyConfigs[clampedIndex];

            int swarmSize = Random.Range(_minSwarmSize, _maxSwarmSize + 1);
            swarmSize = Mathf.Min(swarmSize, selectedConfig.MaxAmount);

            _waveFreezer.IsFrozen = true;

            _swarmCoroutine = StartCoroutine(SwarmCoroutine(selectedConfig, swarmSize));
        }

        private IEnumerator SwarmCoroutine(EnemySpawnInfo enemyInfo, int totalToSpawn)
        {
            // Phase 1: Warning Countdown
            float warningTimer = _swarmWarningDuration;
            int lastLoggedSecond = Mathf.CeilToInt(warningTimer);

            _swarmNotificationPresenter.ShowIncoming(lastLoggedSecond);

            while (warningTimer > 0f)
            {
                yield return null;
                warningTimer -= Time.deltaTime;
                int currentSecond = Mathf.CeilToInt(warningTimer);
                if (currentSecond != lastLoggedSecond && currentSecond > 0)
                {
                    lastLoggedSecond = currentSecond;
                    _swarmNotificationPresenter.ShowIncoming(lastLoggedSecond);
                }
            }

            // Phase 2: Ongoing Spawning
            _swarmNotificationPresenter.ShowOngoing();

            int ticks = Mathf.Max(1, Mathf.RoundToInt(_swarmDuration / _spawnTickInterval));
            int spawned = 0;

            for (int i = 0; i < ticks; i++)
            {
                int targetCumulative = Mathf.RoundToInt((float)(i + 1) / ticks * totalToSpawn);
                int toSpawnThisTick = targetCumulative - spawned;

                if (toSpawnThisTick > 0)
                {
                    _swarmEnemySpawner.SpawnSpecificEnemy(enemyInfo, toSpawnThisTick);
                    spawned += toSpawnThisTick;
                }

                if (i < ticks - 1)
                {
                    yield return new WaitForSeconds(_spawnTickInterval);
                }
            }

            EndSwarm();
        }

        private void EndSwarm()
        {
            _swarmNotificationPresenter.Hide();
            _waveFreezer.IsFrozen = false;
            _isSwarmActive = false;
            _swarmCoroutine = null;

            _currentSwarmIndex++;
            _nextSwarmTime = Random.Range(_minSwarmInterval, _maxSwarmInterval);
        }
    }
}
