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

    public override void ResetRuntimeState()
    {
        DeepCopyUpgradeableStats();

        PrepareProjectileConfig();

        TurretConfig.ProjectileStatsSO = _projectileConfig;
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
        _projectileConfig.Range = Range.Value;
        _projectileConfig.Damage = Damage.Value;

        Range.OnUpgrade += (s, e) => _projectileConfig.Range = Range.Value;
        Damage.OnUpgrade += (s, e) => _projectileConfig.Damage = Damage.Value;
    }
}
