using Assets.ScriptableObjects.Skills;
using Assets.ScriptableObjects.Skills.PlayerSkills.SawSkill;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.Saw
{
    public class SawSkill : UpgradeableSkill<SawSkillUpgradeableConfigSO>
    {
        [field: SerializeField] public override SkillInfoSO SkillInfo { get; protected set; }
        [field: SerializeField] protected override SawSkillUpgradeableConfigSO _config { get; set; }
        [SerializeField] private SawBlade[] _sawBlades;
        private IItemsWithScriptableConfigsActivator<SawBlade, SawSkillUpgradeableConfigSO> _sawBladesActivator;

        public override void Initialize()
        {
            base.Initialize();

            _sawBladesActivator =
                new ItemsWithScriptableConfigsActivator<SawBlade, SawSkillUpgradeableConfigSO>(_sawBlades);

            _sawBladesActivator.InitializeFirst(_config);

            _config.NuberOfSaws.OnUpgrade += (s, e) =>
                _sawBladesActivator.InitializeRandom(_config);
        }
    }
}
