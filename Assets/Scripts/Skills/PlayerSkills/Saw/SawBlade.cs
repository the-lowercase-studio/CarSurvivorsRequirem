using Assets.ScriptableObjects.Skills.PlayerSkills.SawSkill;
using Assets.Scripts.Audio;
using Assets.Scripts.Effects;
using Assets.Scripts.Extensions;
using Assets.Scripts.Initializers;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.Skills.Constants;
using Assets.Scripts.StatusEffects;
using Reflex.Attributes;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.Saw
{
    public class SawBlade : MonoBehaviour, IInitializableWithScriptableConfig<SawSkillUpgradeableConfigSO>
    {
        [Inject] private readonly IPlayerManager _playerManager = null;

        [SerializeField] private float _maxTurnSpeedMultiplier = 0.25f;
        [SerializeField] private float _spinRampSpeed = 8.0f;

        private readonly Dictionary<Collider, float> _lastHitTimesByCollider = new(32);
        private readonly List<Collider> _staleCollidersCache = new(32);

        private SawSkillUpgradeableConfigSO _config;
        private IAudioClipPlayer _audioClipPlayer;
        private XYZRotationLoop[] _rotationLoops;
        private float _currentSpeedMultiplier = 1.0f;
        private float _timeSinceLastPurge;
        private bool _isInitialized;

        private void Awake()
        {
            _audioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
            _rotationLoops = GetComponentsInChildren<XYZRotationLoop>(true);
            _lastHitTimesByCollider.Clear();
            _staleCollidersCache.Clear();
        }

        private void Update()
        {
            UpdateRotationSpeedMultiplier();
            UpdateHitCooldownPurge();
        }

        private void OnDisable()
        {
            _currentSpeedMultiplier = 1.0f;
            _timeSinceLastPurge = 0f;
            _lastHitTimesByCollider.Clear();
            _staleCollidersCache.Clear();

            if (_rotationLoops != null)
            {
                for (int i = 0; i < _rotationLoops.Length; i++)
                {
                    if (_rotationLoops[i] != null)
                    {
                        _rotationLoops[i].SpeedMultiplier = 1.0f;
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            ProcessEnemyCollision(other);
        }

        private void OnTriggerStay(Collider other)
        {
            ProcessEnemyCollision(other);
        }

        public void Initialize(SawSkillUpgradeableConfigSO config)
        {
            _config = config;

            gameObject.SetActive(true);

            _isInitialized = true;
        }

        public bool IsInitialized()
        {
            return _isInitialized;
        }

        private void ProcessEnemyCollision(Collider other)
        {
            if (other == null || !EntityLayers.Enemies.ContainsLayer(other.gameObject.layer))
            {
                return;
            }

            float currentTime = Time.time;
            if (_lastHitTimesByCollider.TryGetValue(other, out float lastHitTime))
            {
                float cooldown = _config != null ? _config.AttackCooldown : 0.14f;
                if (currentTime - lastHitTime < cooldown)
                {
                    return;
                }
            }

            _lastHitTimesByCollider[other] = currentTime;
            AttackCollidingEnemy(other);
        }

        private void UpdateHitCooldownPurge()
        {
            _timeSinceLastPurge += Time.deltaTime;
            if (_timeSinceLastPurge >= SkillConstants.SAW_COOLDOWN_PURGE_INTERVAL)
            {
                _timeSinceLastPurge = 0f;
                PurgeStaleHitRecords();
            }
        }

        private void PurgeStaleHitRecords()
        {
            if (_lastHitTimesByCollider.Count == 0)
            {
                return;
            }

            float currentTime = Time.time;
            _staleCollidersCache.Clear();

            foreach (KeyValuePair<Collider, float> kvp in _lastHitTimesByCollider)
            {
                if (kvp.Key == null || currentTime - kvp.Value > 2.0f)
                {
                    _staleCollidersCache.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _staleCollidersCache.Count; i++)
            {
                _lastHitTimesByCollider.Remove(_staleCollidersCache[i]);
            }

            _staleCollidersCache.Clear();
        }

        private ICarController GetCarController()
        {
            ICarController carController = _playerManager?.CarController;
            if (carController == null)
            {
                carController = GetComponentInParent<ICarController>();
            }

            return carController;
        }

        private void UpdateRotationSpeedMultiplier()
        {
            if (_rotationLoops == null || _rotationLoops.Length == 0)
            {
                return;
            }

            ICarController carController = GetCarController();

            float targetMultiplier = 1.0f;

            if (carController != null)
            {
                float steerIntensity = Mathf.Abs(carController.CurrentSteerInput);
                if (carController.IsDrifting)
                {
                    steerIntensity = Mathf.Max(steerIntensity, 0.85f);
                }

                targetMultiplier = 1.0f + (_maxTurnSpeedMultiplier * steerIntensity);
            }

            _currentSpeedMultiplier = Mathf.MoveTowards(
                _currentSpeedMultiplier,
                targetMultiplier,
                _spinRampSpeed * Time.deltaTime
            );

            for (int i = 0; i < _rotationLoops.Length; i++)
            {
                if (_rotationLoops[i] != null)
                {
                    _rotationLoops[i].SpeedMultiplier = _currentSpeedMultiplier;
                }
            }
        }

        private void AttackCollidingEnemy(Collider other)
        {
            if (_audioClipPlayer != null)
            {
                _audioClipPlayer.Play("Attack");
            }

            if (other.TryGetComponent(out IDamageable damageable) || (damageable = other.GetComponentInParent<IDamageable>()) != null)
            {
                int damage = _config != null && _config.Damage != null ? _config.Damage.Value : 0;
                damageable.TakeDamage(damage);
            }

            if (other.TryGetComponent(out IKnockable knockable) || (knockable = other.GetComponentInParent<IKnockable>()) != null)
            {
                ICarController carController = GetCarController();
                float speedRatio = 0f;
                if (carController != null && carController.MaxForwardSpeed > 0.001f)
                {
                    speedRatio = Mathf.Clamp01(carController.GetMovementSpeed() / carController.MaxForwardSpeed);
                }

                float knockbackStat = _config != null && _config.KnockbackRange != null ? _config.KnockbackRange.Value : 1f;
                float baseDistance = SkillConstants.DEFAULT_COLLISION_KNOCKBACK * knockbackStat;
                float speedMultiplier = 1.0f + (SkillConstants.SAW_KNOCKBACK_SPEED_FACTOR * speedRatio);
                float knockbackDistance = Mathf.Clamp(
                    baseDistance * speedMultiplier,
                    SkillConstants.SAW_MIN_KNOCKBACK_DISTANCE,
                    SkillConstants.SAW_MAX_KNOCKBACK_DISTANCE
                );
                float arrivalDuration = Mathf.Clamp(
                    knockbackDistance * SkillConstants.TIME_TO_ARRIVE_AT_LOCATION_MULTIPLIER,
                    SkillConstants.SAW_MIN_KNOCKBACK_DURATION,
                    SkillConstants.SAW_MAX_KNOCKBACK_DURATION
                );

                Vector3 knockbackDirection = transform.forward;
                knockbackDirection.y = 0f;
                if (knockbackDirection.sqrMagnitude > 0.001f)
                {
                    knockbackDirection.Normalize();
                }
                else
                {
                    knockbackDirection = Vector3.forward;
                }

                knockable.ApplyKnockBack(
                    knockbackDirection,
                    knockbackDistance,
                    arrivalDuration);
            }
        }
    }
}

