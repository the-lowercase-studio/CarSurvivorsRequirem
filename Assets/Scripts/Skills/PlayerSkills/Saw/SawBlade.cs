using Assets.ScriptableObjects.Skills.PlayerSkills.SawSkill;
using Assets.Scripts.Audio;
using Assets.Scripts.Initializers;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Player;
using Assets.Scripts.StatusEffects;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.Saw
{
    public class SawBlade : MonoBehaviour, IInitializableWithScriptableConfig<SawSkillUpgradeableConfigSO>
    {
        [Inject] private readonly IPlayerManager _playerManager;

        private SawSkillUpgradeableConfigSO _config;
        private bool _isInitialized;
        private const float _defaultCollisionKnockback = 2f;
        private IAudioClipPlayer _audioClipPlayer;

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

            EntityManipulationHelper.Damage(other, _config.Damage.Value);

            float knockback = Mathf.Max(
                _defaultCollisionKnockback,
                _config.KnockbackRange.Value * _playerManager.CarController.GetMovementSpeed()
            );

            EntityManipulationHelper.Knockback(
                other,
                transform.forward,
                knockback,
                _config.TimeToArriveAtKnockbackLocation);

            EntityManipulationHelper.Stun(other, _config.TimeToArriveAtKnockbackLocation);
        }
    }
}
