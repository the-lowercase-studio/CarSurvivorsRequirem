using Assets.ScriptableObjects.Player.Skills;
using Assets.Scripts.Stats;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.ScriptableObjects.Skills.PlayerSkills.MinigunSkill
{
    [CreateAssetMenu(fileName = "MinigunSkillSO", menuName = "Scriptable Objects/Skills/MinigunSkillSO")]
    public class MinigunSkillUpgradeableConfigSO : SkillUpgradeableStatsConfig
    {
        [Header("Turrets Stats")]
        [SerializeField] private TurretConfigSO _turretConfig;
        [SerializeField] private FloatUpgradeableStat _delayBetweenShootingBullets;
        [SerializeField] private FloatUpgradeableStat _range;
        [SerializeField] private IntUpgradeableStat _numberOfTurrets;
        public TurretConfigSO TurretConfig => _turretConfig;
        public FloatUpgradeableStat DelayBetweenShoots { get; private set; }
        public FloatUpgradeableStat Range { get; private set; }
        public IntUpgradeableStat NumberOfTurrets { get; private set; }

        [Header("Bullets Stats")]
        [SerializeField] private FloatUpgradeableStat _startBulletSpeed;
        [SerializeField] private FloatUpgradeableStat _startBulletSize;
        [SerializeField] private IntUpgradeableStat _startBulletDamage;
        [SerializeField] private IntUpgradeableStat _startBulletMaxPiercing;
        public FloatUpgradeableStat BulletSize { get; private set; }
        public FloatUpgradeableStat BulletSpeed { get; private set; }
        public IntUpgradeableStat BulletDamage { get; private set; }
        public IntUpgradeableStat BulletMaxPiercing { get; private set; }

        private ProjectileConfigSO _projectileConfig;

        private void OnEnable()
        {
            DeepCopyUpgradeableStats();

            PrepareTurretConfig();

            PrepareProjectileConfig();

            TurretConfig.ProjectileStatsSO = _projectileConfig;
        }

        private void DeepCopyUpgradeableStats()
        {
            Range = DeepCopyUtility.DeepCopy(_range);
            NumberOfTurrets = DeepCopyUtility.DeepCopy(_numberOfTurrets);
            DelayBetweenShoots = DeepCopyUtility.DeepCopy(_delayBetweenShootingBullets);
            BulletSize = DeepCopyUtility.DeepCopy(_startBulletSize);
            BulletSpeed = DeepCopyUtility.DeepCopy(_startBulletSpeed);
            BulletDamage = DeepCopyUtility.DeepCopy(_startBulletDamage);
            BulletMaxPiercing = DeepCopyUtility.DeepCopy(_startBulletMaxPiercing);
        }

        private void PrepareTurretConfig()
        {
            TurretConfig.Range = Range.Value;
            Range.OnUpgrade += (s, e) => TurretConfig.Range = Range.Value;
        }

        private void PrepareProjectileConfig()
        {
            _projectileConfig = ScriptableObject.CreateInstance<ProjectileConfigSO>();

            _projectileConfig.Damage = BulletDamage.Value;
            _projectileConfig.Size = BulletSize.Value;
            _projectileConfig.Range = Range.Value;
            _projectileConfig.Speed = BulletSpeed.Value;
            _projectileConfig.MaxPiercing = BulletMaxPiercing.Value;

            Range.OnUpgrade += (s, e) => _projectileConfig.Range = Range.Value;
            BulletSize.OnUpgrade += (s, e) => _projectileConfig.Size = BulletSize.Value;
            BulletSpeed.OnUpgrade += (s, e) => _projectileConfig.Speed = BulletSpeed.Value;
            BulletDamage.OnUpgrade += (s, e) => _projectileConfig.Damage = BulletDamage.Value;
            BulletMaxPiercing.OnUpgrade += (s, e) => _projectileConfig.MaxPiercing = BulletMaxPiercing.Value;
        }
    }
}
