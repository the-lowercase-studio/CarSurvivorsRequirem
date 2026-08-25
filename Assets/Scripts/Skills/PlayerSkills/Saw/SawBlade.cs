using Assets.ScriptableObjects.Skills.PlayerSkills.SawSkill;
using Assets.Scripts.Audio;
using Assets.Scripts.Initializers;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Player;
using Assets.Scripts.Skills.Constants;
using Assets.Scripts.StatusEffects;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.Saw
{
    public class SawBlade : MonoBehaviour, IInitializableWithScriptableConfig<SawSkillUpgradeableConfigSO>
    {
        [Inject] private readonly IPlayerManager _playerManager;

        private SawSkillUpgradeableConfigSO _config;
        private IAudioClipPlayer _audioClipPlayer;
        private bool _isInitialized;

        private void Awake()
        {
            _audioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((1 << other.gameObject.layer) == EntityLayers.Enemy)
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

        private void AttackCollidingEnemy(Collider other)
        {
            _audioClipPlayer.Play("Attack");

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

            if (other.TryGetComponent(out IStunnable stunnable) || (stunnable = other.GetComponentInParent<IStunnable>()) != null)
            {
                stunnable.ApplyStun(_config.TimeToArriveAtKnockbackLocation);
            }
        }
    }
}

