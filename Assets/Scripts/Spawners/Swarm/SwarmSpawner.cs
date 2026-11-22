using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.UI.HUD;
using Assets.Scripts.Waves;
using Reflex.Attributes;
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
        private SwarmPhase _phase;
        private EnemySpawnInfo _activeEnemyInfo;
        private int _activeTotalToSpawn;
        private float _warningTimer;
        private int _lastLoggedSecond;
        private int _ticksTotal;
        private int _currentTickIndex;
        private int _spawnedInSwarm;
        private float _tickWaitTimer;
        private bool _isSuppressed;

        private enum SwarmPhase
        {
            Idle,
            Warning,
            Spawning
        }

        public bool IsSuppressed
        {
            get
            {
                return _isSuppressed;
            }
            set
            {
                _isSuppressed = value;
                if (_isSuppressed && _phase != SwarmPhase.Idle)
                {
                    CancelActiveSwarm();
                }
            }
        }

        private void Start()
        {
            ApplyTierScaling();
            _nextSwarmTime = RollNextSwarmInterval();
            ResetActiveSwarmState();
        }

        private void OnDisable()
        {
            if (_phase != SwarmPhase.Idle)
            {
                CancelActiveSwarm();
            }
        }

        private void Update()
        {
            AdvanceSwarm(Time.deltaTime);
        }

        public void AdvanceSwarm(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }

            if (_phase == SwarmPhase.Idle)
            {
                StepCooldownPhase(deltaTime);
                return;
            }

            if (_isSuppressed)
            {
                CancelActiveSwarm();
                return;
            }

            if (_timeFreezeState != null && _timeFreezeState.IsFrozen)
            {
                return;
            }

            if (_phase == SwarmPhase.Warning)
            {
                StepWarningPhase(deltaTime);
                return;
            }

            StepSpawningPhase(deltaTime);
        }

        private void StepCooldownPhase(float deltaTime)
        {
            if (_isSuppressed || (_timeFreezeState != null && _timeFreezeState.IsFrozen))
            {
                return;
            }

            _nextSwarmTime -= deltaTime;

            if (_nextSwarmTime <= 0f)
            {
                StartSwarm();
            }
        }

        private void StartSwarm()
        {
            int configCount = _swarmEnemySpawner.EnemyConfigs.Count;
            if (configCount == 0)
            {
                _nextSwarmTime = RollNextSwarmInterval();
                return;
            }

            int clampedIndex = Mathf.Min(_currentSwarmIndex, configCount - 1);
            EnemySpawnInfo selectedConfig = _swarmEnemySpawner.EnemyConfigs[clampedIndex];

            int swarmSize = Random.Range(_effectiveMinSwarmSize, _effectiveMaxSwarmSize + 1);
            swarmSize = Mathf.Min(swarmSize, selectedConfig.MaxAmount);

            _waveFreezer.IsFrozen = true;
            BeginWarningPhase(selectedConfig, swarmSize);
        }

        private void BeginWarningPhase(EnemySpawnInfo enemyInfo, int totalToSpawn)
        {
            _activeEnemyInfo = enemyInfo;
            _activeTotalToSpawn = totalToSpawn;
            _warningTimer = _swarmWarningDuration;
            _lastLoggedSecond = Mathf.CeilToInt(_warningTimer);

            _swarmNotificationPresenter.ShowIncoming(_lastLoggedSecond);

            if (_warningTimer > 0f)
            {
                _phase = SwarmPhase.Warning;
                return;
            }

            BeginSpawningPhase();
        }

        private void StepWarningPhase(float deltaTime)
        {
            _warningTimer -= deltaTime;
            int currentSecond = Mathf.CeilToInt(_warningTimer);
            if (currentSecond != _lastLoggedSecond && currentSecond > 0)
            {
                _lastLoggedSecond = currentSecond;
                _swarmNotificationPresenter.ShowIncoming(_lastLoggedSecond);
            }

            if (_warningTimer <= 0f)
            {
                BeginSpawningPhase();
            }
        }

        private void BeginSpawningPhase()
        {
            _swarmNotificationPresenter.ShowOngoing();

            _ticksTotal = Mathf.Max(1, Mathf.RoundToInt(_swarmDuration / _spawnTickInterval));
            _spawnedInSwarm = 0;
            _currentTickIndex = 0;
            _phase = SwarmPhase.Spawning;

            ExecutePendingTicks();
        }

        private void StepSpawningPhase(float deltaTime)
        {
            _tickWaitTimer -= deltaTime;
            if (_tickWaitTimer <= 0f)
            {
                ExecutePendingTicks();
            }
        }

        private void ExecutePendingTicks()
        {
            while (_currentTickIndex < _ticksTotal)
            {
                int targetCumulative = Mathf.RoundToInt((float)(_currentTickIndex + 1) / _ticksTotal * _activeTotalToSpawn);
                int toSpawnThisTick = targetCumulative - _spawnedInSwarm;

                if (toSpawnThisTick > 0)
                {
                    _swarmEnemySpawner.SpawnSpecificEnemy(_activeEnemyInfo, toSpawnThisTick);
                    _spawnedInSwarm += toSpawnThisTick;
                }

                _currentTickIndex++;

                if (_currentTickIndex >= _ticksTotal)
                {
                    EndSwarm();
                    return;
                }

                if (_spawnTickInterval > 0f)
                {
                    _tickWaitTimer = _spawnTickInterval;
                    return;
                }
            }
        }

        private void EndSwarm()
        {
            _swarmNotificationPresenter.Hide();
            _waveFreezer.IsFrozen = false;
            ResetActiveSwarmState();

            _currentSwarmIndex++;
            _nextSwarmTime = RollNextSwarmInterval();
        }

        private void CancelActiveSwarm()
        {
            if (_swarmNotificationPresenter != null)
            {
                _swarmNotificationPresenter.Hide();
            }

            if (_waveFreezer != null)
            {
                _waveFreezer.IsFrozen = false;
            }

            ResetActiveSwarmState();
            _nextSwarmTime = RollNextSwarmInterval();
        }

        private float RollNextSwarmInterval()
        {
            if (_effectiveMaxSwarmInterval <= _effectiveMinSwarmInterval)
            {
                return _effectiveMinSwarmInterval;
            }

            return Mathf.Clamp(
                Random.Range(_effectiveMinSwarmInterval, _effectiveMaxSwarmInterval),
                _effectiveMinSwarmInterval,
                _effectiveMaxSwarmInterval);
        }

        private void ResetActiveSwarmState()
        {
            _phase = SwarmPhase.Idle;
            _activeEnemyInfo = null;
            _activeTotalToSpawn = 0;
            _warningTimer = 0f;
            _lastLoggedSecond = 0;
            _ticksTotal = 0;
            _currentTickIndex = 0;
            _spawnedInSwarm = 0;
            _tickWaitTimer = 0f;
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
