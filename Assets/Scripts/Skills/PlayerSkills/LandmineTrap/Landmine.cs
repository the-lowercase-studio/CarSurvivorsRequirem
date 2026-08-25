using Assets.ScriptableObjects.Skills.PlayerSkills.LandmineSkill;
using Assets.Scripts.Audio;
using Assets.Scripts.Extensions;
using Assets.Scripts.Initializers;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Skills.Constants;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.LandmineTrap
{
    public class Landmine : MonoBehaviour, IInitializableWithScriptableConfig<LandmineSkillUpgradeableConfigSO>
    {
        [SerializeField] private LandmineSkillUpgradeableConfigSO _config;
        [SerializeField] private GameObject _landmineVisual;
        [SerializeField] private VFXPlayer _deathVfxPlayer;

        private IAudioClipPlayer _audioClipPlayer;
        private bool _isInitialized;

        private void Awake()
        {
            _audioClipPlayer = GetComponentInChildren<IAudioClipPlayer>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_landmineVisual.activeSelf || !EntityLayers.Enemies.ContainsLayer(other.gameObject.layer))
            {
                return;
            }

            Explode();

            _audioClipPlayer.Play("Explosion");
        }

        private void OnDrawGizmos()
        {
            if (_config is not null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, _config.ExplosionRadius.Value);
            }
        }

        private void OnEnable()
        {
            _deathVfxPlayer.OnVFXFinished += OnDeathVfxFinished;
            _landmineVisual.SetActive(true);
        }

        private void OnDisable()
        {
            _deathVfxPlayer.OnVFXFinished -= OnDeathVfxFinished;
        }

        private void OnDeathVfxFinished(object sender, System.EventArgs e)
        {
            Destroy(gameObject);
        }

        private void Explode()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, _config.ExplosionRadius.Value, EntityLayers.Enemies, QueryTriggerInteraction.Collide);

            if (colliders.Length == 0)
            {
                return;
            }

            _deathVfxPlayer.Play(new VFXPlayConfig(scale: _config.ExplosionRadius.Value));

            foreach (Collider collider in colliders)
            {
                EntityManipulationHelper.Damage(collider, _config.Damage.Value);

                float timeToArriveAtLocation = _config.KnockbackRange.Value * SkillConstants.TIME_TO_ARRIVE_AT_LOCATION_MULTIPLIER;

                ApplyExplosionKnockbackOnKnockableEntity(collider, timeToArriveAtLocation);

                EntityManipulationHelper.Stun(collider, timeToArriveAtLocation);
            }

            _landmineVisual.SetActive(false);
        }

        private void ApplyExplosionKnockbackOnKnockableEntity(Collider collider, float timeToArriveAtLocation)
        {
            Vector3 dir = (collider.transform.position - transform.position).normalized;
            EntityManipulationHelper.Knockback(collider, dir, _config.KnockbackRange.Value, timeToArriveAtLocation);
        }

        public void Initialize(LandmineSkillUpgradeableConfigSO config)
        {
            _config = config;

            transform.localScale = new Vector3(_config.Size.Value, transform.localScale.y, _config.Size.Value);

            gameObject.SetActive(true);

            _isInitialized = true;
        }

        public bool IsInitialized()
        {
            return _isInitialized;
        }
    }
}

