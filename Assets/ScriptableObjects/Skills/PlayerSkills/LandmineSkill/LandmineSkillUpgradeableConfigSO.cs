using Assets.ScriptableObjects.Player.Skills;
using Assets.Scripts.Stats;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.ScriptableObjects.Skills.PlayerSkills.LandmineSkill
{
    [CreateAssetMenu(fileName = "LandmineSkillSO", menuName = "Scriptable Objects/Skills/LandmineSkillSO")]
    public class LandmineSkillUpgradeableConfigSO : SkillUpgradeableStatsConfig
    {
        [SerializeField] private FloatUpgradeableStat _spawnCooldown;
        [SerializeField] private FloatUpgradeableStat _explosionRadius;
        [SerializeField] private FloatUpgradeableStat _size;
        [SerializeField] private FloatUpgradeableStat _knockbackRange;
        [SerializeField] private IntUpgradeableStat _damage;

        public FloatUpgradeableStat SpawnCooldown { get; private set; }
        public FloatUpgradeableStat ExplosionRadius { get; private set; }
        public FloatUpgradeableStat Size { get; private set; }
        public FloatUpgradeableStat KnockbackRange { get; private set; }
        public IntUpgradeableStat Damage { get; private set; }

        private void OnEnable()
        {
            SpawnCooldown = DeepCopyUtility.DeepCopy(_spawnCooldown);
            ExplosionRadius = DeepCopyUtility.DeepCopy(_explosionRadius);
            Damage = DeepCopyUtility.DeepCopy(_damage);
            Size = DeepCopyUtility.DeepCopy(_size);
            KnockbackRange = DeepCopyUtility.DeepCopy(_knockbackRange);
        }
    }
}
