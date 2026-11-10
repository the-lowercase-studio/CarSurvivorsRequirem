using System;
using System.Collections.Generic;
using Assets.Scripts.Audio;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Extensions;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.PowerUps.Config;
using Assets.Scripts.PowerUps.Constants;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.StatusEffects;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.PowerUps.FireTails
{
    public interface IFireTailsService
    {
        bool IsActive { get; }
        float RemainingSeconds { get; }
    }

    public class FireTailsService : MonoBehaviour, IFireTailsService
    {
        private struct FireHazardNode
        {
            public Vector3 Position;
            public float ExpireCombatTime;
        }

        [Inject] private readonly ICollectibleDropNotifier _collectibleDropNotifier = null;
        [Inject] private readonly IPlayerManager _playerManager = null;
        [Inject] private readonly ITimeFreezeState _timeFreezeState = null;

        [SerializeField, Tooltip("Configuration asset defining Fire Tails duration, hazard lifetime, DoT, slow, and query interval parameters.")]
        private FireTailsConfigSO _config;

        [SerializeField, Tooltip("Optional audio player for Fire Tails activation, ignition, and expiration SFX.")]
        private AudioClipPlayer _audioClipPlayer;

        private readonly PowerUpTimer _timer = new PowerUpTimer();
        private readonly FireHazardNode[] _hazardNodes = new FireHazardNode[FireTailsConstants.MAX_HAZARD_NODES];
        private int _activeHazardNodeCount;
        private readonly Vector3[] _lastSpawnPositions = new Vector3[FireTailsConstants.REAR_TIRE_TRACK_COUNT];
        private readonly bool[] _hasTrackContinuity = new bool[FireTailsConstants.REAR_TIRE_TRACK_COUNT];
        private readonly Collider[] _overlapBuffer = new Collider[FireTailsConstants.OVERLAP_COLLIDER_BUFFER_SIZE];
        private readonly HashSet<IDamageable> _touchedDamageablesThisScan = new HashSet<IDamageable>();
        private readonly Dictionary<IDamageable, float> _nextBurnTickTimeByTarget = new Dictionary<IDamageable, float>(FireTailsConstants.TARGET_TRACKING_INITIAL_CAPACITY);
        private readonly List<IDamageable> _staleTargetsScratch = new List<IDamageable>(FireTailsConstants.TARGET_TRACKING_INITIAL_CAPACITY);
        private readonly HashSet<IMovementController> _slowedMoversThisScan = new HashSet<IMovementController>();
        private readonly List<IMovementController> _slowedMoversPreviousScan = new List<IMovementController>(FireTailsConstants.TARGET_TRACKING_INITIAL_CAPACITY);
        private float _combatClock;
        private float _nextOverlapScanCombatTime;
        private bool _wasDriftingWithFireTails;

        public bool IsActive
        {
            get
            {
                return _timer.IsRunning;
            }
        }

        public float RemainingSeconds
        {
            get
            {
                return _timer.Remaining;
            }
        }

        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException("FireTailsService requires an authored FireTailsConfigSO.");
            }

            _config.ValidateConfiguration();
        }

        private void OnEnable()
        {
            if (_timeFreezeState != null)
            {
                _timeFreezeState.OnFrozenChanged += HandleFrozenChanged;
            }
        }

        private void Start()
        {
            if (_collectibleDropNotifier != null)
            {
                _collectibleDropNotifier.OnPowerUpCollected += HandlePowerUpCollected;
            }

            if (_playerManager != null && _playerManager.Health != null)
            {
                _playerManager.Health.OnNoHealth += HandlePlayerNoHealth;
            }

            _playerManager?.CarVfxEffectsController?.ConfigureFireTailsTimings(_config.HazardLifetimeSeconds, _config.TrailFadeOutDuration);
        }

        private void Update()
        {
            if (_playerManager != null && _playerManager.Health != null && !_playerManager.Health.IsAlive())
            {
                DeactivateAndClearAllImmediate();
                return;
            }

            bool isFrozen = _timeFreezeState != null && _timeFreezeState.IsFrozen;

            if (_timer.IsRunning)
            {
                if (!isFrozen && _timer.Tick(Time.deltaTime))
                {
                    EndActiveEmission();
                }
                else if (!isFrozen)
                {
                    UpdateDriftHazardEmission();
                }
            }

            if (!isFrozen && (_activeHazardNodeCount > 0 || _slowedMoversPreviousScan.Count > 0))
            {
                _combatClock += Time.deltaTime;

                int i = 0;
                while (i < _activeHazardNodeCount)
                {
                    if (_combatClock >= _hazardNodes[i].ExpireCombatTime)
                    {
                        _activeHazardNodeCount--;
                        _hazardNodes[i] = _hazardNodes[_activeHazardNodeCount];
                    }
                    else
                    {
                        i++;
                    }
                }

                if (_combatClock >= _nextOverlapScanCombatTime || (_activeHazardNodeCount == 0 && _slowedMoversPreviousScan.Count > 0))
                {
                    _nextOverlapScanCombatTime = _combatClock + _config.OverlapCheckIntervalSeconds;
                    EvaluateHazardCombatOverlaps();
                }
            }
        }

        private void OnDisable()
        {
            if (_timeFreezeState != null)
            {
                _timeFreezeState.OnFrozenChanged -= HandleFrozenChanged;
            }

            DeactivateAndClearAllImmediate();
        }

        private void OnDestroy()
        {
            if (_collectibleDropNotifier != null)
            {
                _collectibleDropNotifier.OnPowerUpCollected -= HandlePowerUpCollected;
            }

            if (_playerManager != null && _playerManager.Health != null)
            {
                _playerManager.Health.OnNoHealth -= HandlePlayerNoHealth;
            }
        }

        private void HandleFrozenChanged(bool isFrozen)
        {
            _playerManager?.CarVfxEffectsController?.SetFireTailsFrozen(isFrozen);
        }

        private void HandlePowerUpCollected(PowerUpType powerUpType)
        {
            if (powerUpType != PowerUpType.FireTails)
            {
                return;
            }

            _timer.Start(_config.DurationSeconds);
            for (int i = 0; i < FireTailsConstants.REAR_TIRE_TRACK_COUNT; i++)
            {
                _hasTrackContinuity[i] = false;
            }

            _playerManager?.CarVfxEffectsController?.SetFireTailsActive(true);
            _audioClipPlayer?.PlayOneShot(PowerUpConstants.SFX_FIRE_TAILS_START);
        }

        private void HandlePlayerNoHealth(object sender, EventArgs e)
        {
            DeactivateAndClearAllImmediate();
        }

        private void EndActiveEmission()
        {
            _wasDriftingWithFireTails = false;
            _playerManager?.CarVfxEffectsController?.SetFireTailsActive(false);
            _audioClipPlayer?.PlayOneShot(PowerUpConstants.SFX_FIRE_TAILS_EXPIRE);
        }

        private void DeactivateAndClearAllImmediate()
        {
            _timer.Stop();
            _wasDriftingWithFireTails = false;
            for (int i = 0; i < FireTailsConstants.REAR_TIRE_TRACK_COUNT; i++)
            {
                _hasTrackContinuity[i] = false;
            }

            _playerManager?.CarVfxEffectsController?.SetFireTailsActive(false);

            for (int i = 0; i < _slowedMoversPreviousScan.Count; i++)
            {
                IMovementController mover = _slowedMoversPreviousScan[i];
                if (mover != null)
                {
                    mover.SetMovementSpeedMultiplier(FireTailsConstants.NORMAL_SPEED_MULTIPLIER);
                }
            }

            _slowedMoversPreviousScan.Clear();
            _slowedMoversThisScan.Clear();
            _touchedDamageablesThisScan.Clear();
            _nextBurnTickTimeByTarget.Clear();
            _activeHazardNodeCount = 0;
        }

        private void UpdateDriftHazardEmission()
        {
            if (_playerManager == null)
            {
                return;
            }

            ICarController car = _playerManager.CarController;
            ICarVfxEffectsController carVfx = _playerManager.CarVfxEffectsController;

            if (car == null || carVfx == null || !car.IsGrounded || !car.IsDrifting)
            {
                for (int i = 0; i < FireTailsConstants.REAR_TIRE_TRACK_COUNT; i++)
                {
                    _hasTrackContinuity[i] = false;
                }
                _wasDriftingWithFireTails = false;
                return;
            }

            if (!_wasDriftingWithFireTails)
            {
                _wasDriftingWithFireTails = true;
                _audioClipPlayer?.PlayOneShot(PowerUpConstants.SFX_FIRE_TAILS_IGNITE);
            }

            int tracksToSample = Mathf.Min(FireTailsConstants.REAR_TIRE_TRACK_COUNT, carVfx.RearDriftEmitterCount);
            float sqrSpawnInterval = _config.SpawnIntervalDistance * _config.SpawnIntervalDistance;

            for (int trackIndex = 0; trackIndex < tracksToSample; trackIndex++)
            {
                if (!carVfx.TryGetRearDriftEmitterWorldCandidate(trackIndex, out Vector3 candidate)
                    || !car.TryGetTireGroundContact(candidate, out RaycastHit groundHit))
                {
                    _hasTrackContinuity[trackIndex] = false;
                    continue;
                }

                Vector3 spawnPoint = groundHit.point + Vector3.up * FireTailsConstants.GROUND_SURFACE_OFFSET;

                if (!_hasTrackContinuity[trackIndex])
                {
                    AddHazardNode(spawnPoint);
                    _lastSpawnPositions[trackIndex] = spawnPoint;
                    _hasTrackContinuity[trackIndex] = true;
                }
                else if ((spawnPoint - _lastSpawnPositions[trackIndex]).sqrMagnitude >= sqrSpawnInterval)
                {
                    AddHazardNode(spawnPoint);
                    _lastSpawnPositions[trackIndex] = spawnPoint;
                }
            }
        }

        private void AddHazardNode(Vector3 position)
        {
            float expireTime = _combatClock + _config.HazardLifetimeSeconds;

            if (_activeHazardNodeCount < FireTailsConstants.MAX_HAZARD_NODES)
            {
                _hazardNodes[_activeHazardNodeCount].Position = position;
                _hazardNodes[_activeHazardNodeCount].ExpireCombatTime = expireTime;
                _activeHazardNodeCount++;
            }
            else
            {
                int oldestIndex = 0;
                float oldestExpiry = _hazardNodes[0].ExpireCombatTime;
                for (int i = 1; i < FireTailsConstants.MAX_HAZARD_NODES; i++)
                {
                    if (_hazardNodes[i].ExpireCombatTime < oldestExpiry)
                    {
                        oldestExpiry = _hazardNodes[i].ExpireCombatTime;
                        oldestIndex = i;
                    }
                }

                _hazardNodes[oldestIndex].Position = position;
                _hazardNodes[oldestIndex].ExpireCombatTime = expireTime;
            }
        }

        private void EvaluateHazardCombatOverlaps()
        {
            _touchedDamageablesThisScan.Clear();
            _slowedMoversThisScan.Clear();

            for (int i = 0; i < _activeHazardNodeCount; i++)
            {
                int hitCount = Physics.OverlapSphereNonAlloc(
                    _hazardNodes[i].Position,
                    _config.HazardRadius,
                    _overlapBuffer,
                    EntityLayers.Enemies,
                    QueryTriggerInteraction.Collide);

                for (int h = 0; h < hitCount; h++)
                {
                    Collider col = _overlapBuffer[h];
                    if (col == null || !col.enabled)
                    {
                        continue;
                    }

                    IDamageable damageable;
                    if (!col.TryGetComponent(out damageable))
                    {
                        damageable = col.GetComponentInParent<IDamageable>();
                    }

                    if (damageable != null)
                    {
                        _touchedDamageablesThisScan.Add(damageable);
                    }

                    if (EntityLayers.Enemy.ContainsLayer(col.gameObject.layer))
                    {
                        IMovementController mover;
                        if (!col.TryGetComponent(out mover))
                        {
                            mover = col.GetComponentInParent<IMovementController>();
                        }

                        if (mover != null && _slowedMoversThisScan.Add(mover))
                        {
                            mover.SetMovementSpeedMultiplier(_config.SlowFactor);
                        }
                    }
                }
            }

            for (int i = 0; i < _slowedMoversPreviousScan.Count; i++)
            {
                IMovementController mover = _slowedMoversPreviousScan[i];
                if (mover != null && !_slowedMoversThisScan.Contains(mover))
                {
                    mover.SetMovementSpeedMultiplier(FireTailsConstants.NORMAL_SPEED_MULTIPLIER);
                }
            }

            _slowedMoversPreviousScan.Clear();
            foreach (IMovementController mover in _slowedMoversThisScan)
            {
                _slowedMoversPreviousScan.Add(mover);
            }

            foreach (IDamageable target in _touchedDamageablesThisScan)
            {
                if (target is UnityEngine.Object unityObj && unityObj == null)
                {
                    continue;
                }

                if (!_nextBurnTickTimeByTarget.TryGetValue(target, out float nextTickTime) || _combatClock >= nextTickTime)
                {
                    target.TakeDamage(_config.DamagePerTick);
                    _nextBurnTickTimeByTarget[target] = _combatClock + _config.TickIntervalSeconds;
                }
            }

            _staleTargetsScratch.Clear();
            foreach (KeyValuePair<IDamageable, float> kvp in _nextBurnTickTimeByTarget)
            {
                IDamageable target = kvp.Key;
                if (target is UnityEngine.Object unityObj && unityObj == null)
                {
                    _staleTargetsScratch.Add(target);
                }
                else if (_combatClock >= kvp.Value && !_touchedDamageablesThisScan.Contains(target))
                {
                    _staleTargetsScratch.Add(target);
                }
            }

            for (int i = 0; i < _staleTargetsScratch.Count; i++)
            {
                _nextBurnTickTimeByTarget.Remove(_staleTargetsScratch[i]);
            }
        }
    }
}
