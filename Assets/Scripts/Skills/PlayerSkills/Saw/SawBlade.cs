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
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.Saw
{
    public class SawBlade : MonoBehaviour, IInitializableWithScriptableConfig<SawSkillUpgradeableConfigSO>
    {
        [Inject] private readonly IPlayerManager _playerManager = null;

        [SerializeField] private float _maxTurnSpeedMultiplier = 0.25f;
        [SerializeField] private float _spinRampSpeed = 8.0f;

        private SawSkillUpgradeableConfigSO _config;
        private IAudioClipPlayer _audioClipPlayer;
        private XYZRotationLoop[] _rotationLoops;
        private float _currentSpeedMultiplier = 1.0f;
        private bool _isInitialized;

        private void Awake()
        {
            _audioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
            _rotationLoops = GetComponentsInChildren<XYZRotationLoop>(true);
        }

        private void Update()
        {
            UpdateRotationSpeedMultiplier();
        }

        private void OnDisable()
        {
            _currentSpeedMultiplier = 1.0f;

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
            if (EntityLayers.Enemies.ContainsLayer(other.gameObject.layer))
            {
                AttackCollidingEnemy(other);
            }
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

        private void UpdateRotationSpeedMultiplier()
        {
            if (_rotationLoops == null || _rotationLoops.Length == 0)
            {
                return;
            }

            ICarController carController = _playerManager?.CarController;
            if (carController == null)
            {
                carController = GetComponentInParent<ICarController>();
            }

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
                damageable.TakeDamage(_config.Damage.Value);
            }

            float knockback = Mathf.Max(
                SkillConstants.DEFAULT_COLLISION_KNOCKBACK,
                _config.KnockbackRange.Value * _playerManager.CarController.GetMovementSpeed()
            );

            if (other.TryGetComponent(out IKnockable knockable) || (knockable = other.GetComponentInParent<IKnockable>()) != null)
            {
                Vector3 knockbackDirection = transform.forward;
                knockbackDirection.y = 0;
                knockable.ApplyKnockBack(
                    knockbackDirection,
                    knockback,
                    _config.TimeToArriveAtKnockbackLocation);
            }
        }
    }
}

