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

            if (_config?.NuberOfSaws is not null)
            {
                _config.NuberOfSaws.OnUpgrade -= OnNumberOfSawsUpgraded;
                _config.NuberOfSaws.OnUpgrade += OnNumberOfSawsUpgraded;
            }
        }

        private void OnDestroy()
        {
            if (_config?.NuberOfSaws is not null)
            {
                _config.NuberOfSaws.OnUpgrade -= OnNumberOfSawsUpgraded;
            }
        }

        private void InitializeSawBladesToConfiguredCount()
        {
            _sawBladesActivator.InitializeFirst(_config);
            _sawBladesActivator.InitializeUntilCount(_config, _config.NuberOfSaws.Value);
        }

        private void OnNumberOfSawsUpgraded(object sender, EventArgs e)
        {
            InitializeSawBladesToConfiguredCount();
        }
    }
}

