using Assets.ScriptableObjects;
using Assets.ScriptableObjects.Skills;
using Assets.Scripts.Skills;
using System;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.Lasergun
{
    public class LasergunSkill : UpgradeableSkill<LasergunSkillSO>
    {
        [field: SerializeField] public override SkillInfoSO SkillInfo { get; protected set; }
        [field: SerializeField] protected override LasergunSkillSO _config { get; set; }
        [SerializeField] private LasergunTurret[] _turrets;
        private IItemsWithScriptableConfigsActivator<LasergunTurret, TurretConfigSO> _turretsActivator;

        public override void Initialize()
        {
            base.Initialize();

            _turretsActivator =
                new ItemsWithScriptableConfigsActivator<LasergunTurret, TurretConfigSO>(_turrets);

            InitializeTurretsToConfiguredCount();
            ApplyNumberOfTargetsToInitializedTurrets();

            _config.NumberOfTurrets.OnUpgrade += NumberOfTurrets_OnUpgrade;
            _config.NumberOfTargets.OnUpgrade += NumberOfTargets_OnUpgrade;

            InvokeRepeating(nameof(ShootFromTurrets), 0f, _config.DelayBetweenShoots.Value);
        }

        private void OnDestroy()
        {
            if (_config is null || _config.NumberOfTurrets is null || _config.NumberOfTargets is null)
            {
                return;
            }

            _config.NumberOfTurrets.OnUpgrade -= NumberOfTurrets_OnUpgrade;
            _config.NumberOfTargets.OnUpgrade -= NumberOfTargets_OnUpgrade;
        }

        private void ShootFromTurrets()
        {
            foreach (LasergunTurret turret in _turretsActivator.GetInitialized())
            {
                turret.Shoot(_config.DelayBetweenShoots.MinMaxRange.Min / _config.DelayBetweenShoots.Value);
            }
        }

        private void InitializeTurretsToConfiguredCount()
        {
            _turretsActivator.InitializeUntilCount(_config.TurretConfig, _config.NumberOfTurrets.Value);
            ApplyNumberOfTargetsToInitializedTurrets();
        }

        private void ApplyNumberOfTargetsToInitializedTurrets()
        {
            foreach (LasergunTurret turret in _turretsActivator.GetInitialized())
            {
                turret.SetNumberOfTargets(_config.NumberOfTargets.Value);
            }
        }

        private void NumberOfTurrets_OnUpgrade(object sender, EventArgs e)
        {
            InitializeTurretsToConfiguredCount();
        }

        private void NumberOfTargets_OnUpgrade(object sender, EventArgs e)
        {
            ApplyNumberOfTargetsToInitializedTurrets();
        }
    }
}
