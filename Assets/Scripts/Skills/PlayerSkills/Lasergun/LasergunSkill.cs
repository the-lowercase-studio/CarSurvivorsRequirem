using Assets.ScriptableObjects;
using Assets.ScriptableObjects.Skills;
using Assets.Scripts.Skills;
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

            _config.NumberOfTurrets.OnUpgrade += (s, e) =>
                InitializeTurretsToConfiguredCount();

            InvokeRepeating(nameof(ShootFromTurrets), 0f, _config.DelayBetweenShoots.Value);
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
        }
    }
}
