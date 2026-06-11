using Assets.ScriptableObjects.Player.Skills;
using Assets.Scripts.Stats;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.ScriptableObjects.Skills.PlayerSkills.SawSkill
{
    [CreateAssetMenu(fileName = "SawSkillSO", menuName = "Scriptable Objects/Skills/SawSkillSO")]
    public class SawSkillUpgradeableConfigSO : SkillUpgradeableStatsConfig
    {
        [field: SerializeField] public float TimeToArriveAtKnockbackLocation { get; private set; }
        [field: SerializeField] public float AttackCooldown { get; private set; } = 0.05f;
        [SerializeField] private FloatUpgradeableStat _knockbackRange;
        [SerializeField] private IntUpgradeableStat _damage;
        [SerializeField] private IntUpgradeableStat _numberOfSaws;

        public FloatUpgradeableStat KnockbackRange { get; private set; }
        public IntUpgradeableStat Damage { get; private set; }
        public IntUpgradeableStat NuberOfSaws { get; private set; }

        private void OnEnable()
        {
            ResetRuntimeState();
        }

        public override void ResetRuntimeState()
        {
            KnockbackRange = DeepCopyUtility.DeepCopy(_knockbackRange);
            Damage = DeepCopyUtility.DeepCopy(_damage);
            NuberOfSaws = DeepCopyUtility.DeepCopy(_numberOfSaws);
        }
    }
}
