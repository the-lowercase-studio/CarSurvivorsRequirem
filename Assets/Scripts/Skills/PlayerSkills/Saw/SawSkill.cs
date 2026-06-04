using Assets.ScriptableObjects.Skills;
using Assets.ScriptableObjects.Skills.PlayerSkills.SawSkill;
using System;
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

            InitializeSawBladesToConfiguredCount();

            _config.NuberOfSaws.OnUpgrade -= NuberOfSaws_OnUpgrade;
            _config.NuberOfSaws.OnUpgrade += NuberOfSaws_OnUpgrade;
        }

        private void OnDestroy()
        {
            if (_config?.NuberOfSaws is not null)
            {
                _config.NuberOfSaws.OnUpgrade -= NuberOfSaws_OnUpgrade;
            }
        }

        private void InitializeSawBladesToConfiguredCount()
        {
            _sawBladesActivator.InitializeFirst(_config);
            _sawBladesActivator.InitializeUntilCount(_config, _config.NuberOfSaws.Value);
        }

        private void NuberOfSaws_OnUpgrade(object sender, EventArgs e)
        {
            InitializeSawBladesToConfiguredCount();
        }
    }
}
