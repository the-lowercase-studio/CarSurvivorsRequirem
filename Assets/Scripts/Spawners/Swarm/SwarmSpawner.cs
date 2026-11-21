using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.UI.HUD;
using Assets.Scripts.Waves;
using Reflex.Attributes;
using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Spawners.Swarm
{
    public interface ISwarmFreezer
    {
        bool IsSuppressed { get; set; }
    }

    public class SwarmSpawner : MonoBehaviour, ISwarmFreezer
    {
        [Inject] private readonly ISwarmEnemySpawner _swarmEnemySpawner = null;
        [Inject] private readonly IWaveFreezer _waveFreezer = null;
        [Inject] private readonly ISwarmNotificationPresenter _swarmNotificationPresenter = null;
        [Inject] private readonly ITimeFreezeState _timeFreezeState = null;
        [Inject] private readonly IRunSessionService _runSessionService = null;

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

        private float _effectiveMinSwarmInterval;
        private float _effectiveMaxSwarmInterval;
        private int _effectiveMinSwarmSize;
        private int _effectiveMaxSwarmSize;
        private float _nextSwarmTime;
        private int _currentSwarmIndex;
        private bool _isSwarmActive;
        private Coroutine _swarmCoroutine;

        public bool IsSuppressed { get; set; }

        private void Start()
        {
            ApplyTierScaling();
            _nextSwarmTime = Random.Range(_effectiveMinSwarmInterval, _effectiveMaxSwarmInterval);
        }

        private void OnDisable()
        {
            if (_isSwarmActive)
            {
                if (_waveFreezer != null)
                {
                    _waveFreezer.IsFrozen = false;
                }
                _isSwarmActive = false;
            }
            if (_swarmCoroutine != null)
            {
                StopCoroutine(_swarmCoroutine);
                _swarmCoroutine = null;
            }
        }

        private void Update()
        {
            if (_isSwarmActive || IsSuppressed || (_timeFreezeState != null && _timeFreezeState.IsFrozen))
            {
                return;
            }

            _nextSwarmTime -= Time.deltaTime;

            if (_nextSwarmTime <= 0f)
            {
                StartSwarm();
            }
        }

        private void StartSwarm()
        {
            int configCount = _swarmEnemySpawner.EnemyConfigs.Count;
            if (configCount == 0) return;

            _isSwarmActive = true;

            int clampedIndex = Mathf.Min(_currentSwarmIndex, configCount - 1);
            EnemySpawnInfo selectedConfig = _swarmEnemySpawner.EnemyConfigs[clampedIndex];

            int swarmSize = Random.Range(_effectiveMinSwarmSize, _effectiveMaxSwarmSize + 1);
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
                if (_timeFreezeState != null && _timeFreezeState.IsFrozen)
                {
                    continue;
                }

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
                    float waitTimer = _spawnTickInterval;
                    while (waitTimer > 0f)
                    {
                        yield return null;
                        if (_timeFreezeState != null && _timeFreezeState.IsFrozen)
                        {
                            continue;
                        }

                        waitTimer -= Time.deltaTime;
                    }
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
            _nextSwarmTime = Random.Range(_effectiveMinSwarmInterval, _effectiveMaxSwarmInterval);
        }

        private void ApplyTierScaling()
        {
            float intervalMultiplier = 1f;
            float sizeMultiplier = 1f;

            TierEnemySpawningConfig tierSpawning = _runSessionService?.ActiveTierConfig?.EnemySpawning;
            if (tierSpawning != null)
            {
                intervalMultiplier = Mathf.Max(0.1f, tierSpawning.SwarmIntervalMultiplier);
                sizeMultiplier = Mathf.Max(0.1f, tierSpawning.SwarmSizeMultiplier);
            }

            _effectiveMinSwarmInterval = Mathf.Max(5f, _minSwarmInterval * intervalMultiplier);
            _effectiveMaxSwarmInterval = Mathf.Max(_effectiveMinSwarmInterval, _maxSwarmInterval * intervalMultiplier);
            _effectiveMinSwarmSize = Mathf.Max(1, Mathf.RoundToInt(_minSwarmSize * sizeMultiplier));
            _effectiveMaxSwarmSize = Mathf.Max(_effectiveMinSwarmSize, Mathf.RoundToInt(_maxSwarmSize * sizeMultiplier));
        }
    }
}
