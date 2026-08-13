using Assets.ScriptableObjects;
using Assets.ScriptableObjects.Skills;
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

            if (_config is not null)
            {
                if (_config.NumberOfTurrets is not null)
                {
                    _config.NumberOfTurrets.OnUpgrade -= OnNumberOfTurretsUpgraded;
                    _config.NumberOfTurrets.OnUpgrade += OnNumberOfTurretsUpgraded;
                }

                if (_config.NumberOfTargets is not null)
                {
                    _config.NumberOfTargets.OnUpgrade -= OnNumberOfTargetsUpgraded;
                    _config.NumberOfTargets.OnUpgrade += OnNumberOfTargetsUpgraded;
                }
            }

            InvokeRepeating(nameof(ShootFromTurrets), 0f, _config.DelayBetweenShoots.Value);
        }

        private void OnDestroy()
        {
            if (_config is null)
            {
                return;
            }

            if (_config.NumberOfTurrets is not null)
            {
                _config.NumberOfTurrets.OnUpgrade -= OnNumberOfTurretsUpgraded;
            }

            if (_config.NumberOfTargets is not null)
            {
                _config.NumberOfTargets.OnUpgrade -= OnNumberOfTargetsUpgraded;
            }
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

        private void OnNumberOfTurretsUpgraded(object sender, EventArgs e)
        {
            InitializeTurretsToConfiguredCount();
        }

        private void OnNumberOfTargetsUpgraded(object sender, EventArgs e)
        {
            ApplyNumberOfTargetsToInitializedTurrets();
        }
    }
}

