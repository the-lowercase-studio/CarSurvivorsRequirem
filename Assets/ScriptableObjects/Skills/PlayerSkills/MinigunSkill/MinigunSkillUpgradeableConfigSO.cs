using System.Collections.Generic;
using Assets.ScriptableObjects.Player.Skills;
using Assets.Scripts.Stats;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.ScriptableObjects.Skills.PlayerSkills.MinigunSkill
{
    [CreateAssetMenu(fileName = "MinigunSkillSO", menuName = "Scriptable Objects/Skills/MinigunSkillSO")]
    public class MinigunSkillUpgradeableConfigSO : SkillUpgradeableStatsConfig
    {
        [SerializeField] private TurretConfigSO _turretConfig;
        [Tooltip("Scaled seconds between instant shots.")]
        [SerializeField] private FloatUpgradeableStat _delayBetweenShootingBullets;
        [SerializeField] private FloatUpgradeableStat _range;
        [SerializeField] private IntUpgradeableStat _numberOfTurrets;
        [Tooltip("Square beam corridor width in meters.")]
        [SerializeField] private FloatUpgradeableStat _startBulletSize;
        [SerializeField] private IntUpgradeableStat _startBulletDamage;
        [Tooltip("Additional distinct enemies penetrated after the first hit.")]
        [SerializeField] private IntUpgradeableStat _startBulletMaxPiercing;

        public TurretConfigSO TurretConfig => _turretConfig;
        public FloatUpgradeableStat ShotDelay { get; private set; }
        public FloatUpgradeableStat Range { get; private set; }
        public IntUpgradeableStat NumberOfTurrets { get; private set; }
        public FloatUpgradeableStat BeamWidth { get; private set; }
        public IntUpgradeableStat Damage { get; private set; }
        public IntUpgradeableStat Piercing { get; private set; }

        private void OnEnable()
        {
            ResetRuntimeState();
        }

        public override void AppendStatsForDisplay(List<NameUpgradableStatPair> destination)
        {
            destination.Add(new NameUpgradableStatPair(nameof(ShotDelay), ShotDelay, null));
            destination.Add(new NameUpgradableStatPair(nameof(Range), Range, null));
            destination.Add(new NameUpgradableStatPair(nameof(NumberOfTurrets), NumberOfTurrets, null));
            destination.Add(new NameUpgradableStatPair(nameof(BeamWidth), BeamWidth, null));
            destination.Add(new NameUpgradableStatPair(nameof(Damage), Damage, null));
            destination.Add(new NameUpgradableStatPair(nameof(Piercing), Piercing, null));
        }

        public override void ResetRuntimeState()
        {
            ShotDelay = DeepCopyUtility.DeepCopy(_delayBetweenShootingBullets);
            Range = DeepCopyUtility.DeepCopy(_range);
            NumberOfTurrets = DeepCopyUtility.DeepCopy(_numberOfTurrets);
            BeamWidth = DeepCopyUtility.DeepCopy(_startBulletSize);
            Damage = DeepCopyUtility.DeepCopy(_startBulletDamage);
            Piercing = DeepCopyUtility.DeepCopy(_startBulletMaxPiercing);
        }
    }
}
