using System.Collections.Generic;
using Assets.ScriptableObjects;
using Assets.ScriptableObjects.Player.Skills;
using Assets.Scripts.Stats;
using Assets.Scripts.Utils;
using UnityEngine;

[CreateAssetMenu(fileName = "LasergunSkillSO", menuName = "Scriptable Objects/Skills/LasergunSkillSO")]
public class LasergunSkillSO : SkillUpgradeableStatsConfig
{
    [Header("Turrets Stats")]
    public float SearchForTargetInterval { get; private set; } = 0.2f;

    [SerializeField] private TurretConfigSO _turretConfig;
    [SerializeField] private FloatUpgradeableStat _delayBetweenShoots;
    [SerializeField] private IntUpgradeableStat _numberOfTurrets;
    [SerializeField] private IntUpgradeableStat _numberOfTargets;
    public TurretConfigSO TurretConfig => _turretConfig;
    public FloatUpgradeableStat DelayBetweenShoots { get; private set; }
    public IntUpgradeableStat NumberOfTurrets { get; private set; }
    public IntUpgradeableStat NumberOfTargets { get; private set; }

    [Header("Laser Stats")]
    [SerializeField] private ProjectileConfigSO _projectileConfig;
    [SerializeField] private FloatUpgradeableStat _startRange;
    [SerializeField] private IntUpgradeableStat _startDamage;
    public FloatUpgradeableStat Range { get; private set; }
    public IntUpgradeableStat Damage { get; private set; }

    private void OnEnable()
    {
        ResetRuntimeState();
    }

    public override void AppendStatsForDisplay(List<NameUpgradableStatPair> destination)
    {
        destination.Add(new NameUpgradableStatPair(nameof(DelayBetweenShoots), DelayBetweenShoots, null));
        destination.Add(new NameUpgradableStatPair(nameof(NumberOfTurrets), NumberOfTurrets, null));
        destination.Add(new NameUpgradableStatPair(nameof(NumberOfTargets), NumberOfTargets, null));
        destination.Add(new NameUpgradableStatPair(nameof(Range), Range, null));
        destination.Add(new NameUpgradableStatPair(nameof(Damage), Damage, null));
    }

    public override void ResetRuntimeState()
    {
        DeepCopyUpgradeableStats();

        PrepareProjectileConfig();

        if (TurretConfig != null)
        {
            TurretConfig.ProjectileStatsSO = _projectileConfig;
        }
    }

    public override void SyncDerivedRuntimeState()
    {
        if (Range != null)
        {
            if (_projectileConfig != null)
            {
                _projectileConfig.Range = Range.Value;
            }

            if (_turretConfig != null)
            {
                _turretConfig.Range = Range.Value;
            }
        }

        if (_projectileConfig != null && Damage != null)
        {
            _projectileConfig.Damage = Damage.Value;
        }
    }

    private void DeepCopyUpgradeableStats()
    {
        NumberOfTurrets = DeepCopyUtility.DeepCopy(_numberOfTurrets);
        NumberOfTargets = DeepCopyUtility.DeepCopy(_numberOfTargets);
        DelayBetweenShoots = DeepCopyUtility.DeepCopy(_delayBetweenShoots);
        Damage = DeepCopyUtility.DeepCopy(_startDamage);
        Range = DeepCopyUtility.DeepCopy(_startRange);
    }

    private void PrepareProjectileConfig()
    {
        SyncDerivedRuntimeState();

        if (Range != null)
        {
            Range.OnUpgrade += HandleRangeUpgraded;
        }

        if (Damage != null)
        {
            Damage.OnUpgrade += HandleDamageUpgraded;
        }
    }

    private void HandleRangeUpgraded(object sender, System.EventArgs e)
    {
        if (_projectileConfig != null)
        {
            _projectileConfig.Range = Range.Value;
        }

        if (_turretConfig != null)
        {
            _turretConfig.Range = Range.Value;
        }
    }

    private void HandleDamageUpgraded(object sender, System.EventArgs e)
    {
        if (_projectileConfig != null)
        {
            _projectileConfig.Damage = Damage.Value;
        }
    }
}
