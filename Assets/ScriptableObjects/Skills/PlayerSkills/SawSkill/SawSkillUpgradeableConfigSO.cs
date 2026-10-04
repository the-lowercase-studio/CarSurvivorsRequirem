using System.Collections.Generic;
using Assets.ScriptableObjects.Player.Skills;
using Assets.Scripts.Stats;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.ScriptableObjects.Skills.PlayerSkills.SawSkill
{
    [CreateAssetMenu(fileName = "SawSkillSO", menuName = "Scriptable Objects/Skills/SawSkillSO")]
    public class SawSkillUpgradeableConfigSO : SkillUpgradeableStatsConfig
    {
        [field: SerializeField] public float AttackCooldown { get; private set; } = 0.05f;
        [SerializeField] private FloatUpgradeableStat _knockbackRange;
        [SerializeField] private IntUpgradeableStat _damage;

        public FloatUpgradeableStat KnockbackRange { get; private set; }
        public IntUpgradeableStat Damage { get; private set; }

        private void OnEnable()
        {
            ResetRuntimeState();
        }

        public override void AppendStatsForDisplay(List<NameUpgradableStatPair> destination)
        {
            destination.Add(new NameUpgradableStatPair(nameof(KnockbackRange), KnockbackRange, null));
            destination.Add(new NameUpgradableStatPair(nameof(Damage), Damage, null));
        }

        public override void ResetRuntimeState()
        {
            KnockbackRange = DeepCopyUtility.DeepCopy(_knockbackRange);
            Damage = DeepCopyUtility.DeepCopy(_damage);
        }
    }
}
