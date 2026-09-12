using UnityEngine;

namespace Assets.ScriptableObjects.Enemies.Bosses
{
    [CreateAssetMenu(fileName = "MortarTowerConfig", menuName = "ScriptableObjects/Bosses/MortarTowerConfig")]
    public class MortarTowerConfigSO : ScriptableObject
    {
        [Header("Health & Phase Configuration")]
        [SerializeField] private float _maxHealth = 5000f;
        [SerializeField] private float _enrageHealthPercent = 0.40f;
        [SerializeField] private float _expReward = 500f;
        [SerializeField] private Color _enrageColor = new Color(1f, 0.2f, 0.1f);
        [SerializeField] [ColorUsage(true, true)] private Color _enrageEmissionColor = new Color(2f, 0.4f, 0.1f);

        [Header("Arena & Leash Settings")]
        [SerializeField] private float _arenaRadius = 22f;
        [SerializeField] private float _leashGracePeriodSeconds = 2.5f;

        [Header("Camera Settings")]
        [SerializeField] private Vector3 _combatFollowOffset = new Vector3(0f, 17f, -13f);
        [SerializeField] private float _cameraTransitionDuration = 0.8f;

        [Header("Attack 1: Cluster Shrapnel Burst")]
        [SerializeField] private float _attack1InitialWarning = 1.2f;
        [SerializeField] private float _attack1CenterExplosionRadius = 2.5f;
        [SerializeField] private float _attack1CenterDamage = 30f;
        [SerializeField] private int _attack1SubShellCount = 6;
        [SerializeField] private float _attack1SubShellScatterRadius = 6.0f;
        [SerializeField] private float _attack1SubShellJumpPower = 4.0f;
        [SerializeField] private float _attack1SubShellDuration = 0.6f;
        [SerializeField] private float _attack1SubShellExplosionRadius = 2.0f;
        [SerializeField] private float _attack1SubShellDamage = 20f;
        [SerializeField] private float _attack1EnrageSecondWaveRadius = 11.0f;

        [Header("Attack 2: Diagonal Line Bounce")]
        [SerializeField] private float _attack2CenterMinOffset = 6.0f;
        [SerializeField] private float _attack2CenterMaxOffset = 10.0f;
        [SerializeField] private float _attack2InitialWarning = 0.9f;
        [SerializeField] private int _attack2BounceStepCount = 3;
        [SerializeField] private float _attack2BounceStepDistance = 3.5f;
        [SerializeField] private float _attack2BounceJumpPower = 3.0f;
        [SerializeField] private float _attack2BounceDurationPerStep = 0.45f;
        [SerializeField] private float _attack2BounceExplosionRadius = 1.8f;
        [SerializeField] private float _attack2BounceDamage = 22f;
        [SerializeField] private int _attack2EnrageSalvoCount = 3;
        [SerializeField] private float _attack2EnrageSalvoInterval = 0.4f;

        [Header("Attack 3: Rolling Boulder")]
        [SerializeField] private float _attack3DropWarningDuration = 1.0f;
        [SerializeField] private float _attack3RollTelegraphLength = 20.0f;
        [SerializeField] private float _attack3RollTelegraphWidth = 2.5f;
        [SerializeField] private float _attack3RollSpeed = 16.0f;
        [SerializeField] private float _attack3EnrageRollSpeed = 22.0f;
        [SerializeField] private float _attack3RollDamage = 35f;

        [Header("Cooldown & Phase Modifiers")]
        [SerializeField] private float _cooldownMin = 3.5f;
        [SerializeField] private float _cooldownMax = 4.5f;
        [SerializeField] private float _enrageCooldownMin = 2.0f;
        [SerializeField] private float _enrageCooldownMax = 2.5f;

        public float MaxHealth => _maxHealth;
        public float EnrageHealthPercent => _enrageHealthPercent;
        public float ExpReward => _expReward;
        public Color EnrageColor => _enrageColor;
        public Color EnrageEmissionColor => _enrageEmissionColor;

        public float ArenaRadius => _arenaRadius;
        public float LeashGracePeriodSeconds => _leashGracePeriodSeconds;

        public Vector3 CombatFollowOffset => _combatFollowOffset;
        public float CameraTransitionDuration => _cameraTransitionDuration;

        public float Attack1InitialWarning => _attack1InitialWarning;
        public float Attack1CenterExplosionRadius => _attack1CenterExplosionRadius;
        public float Attack1CenterDamage => _attack1CenterDamage;
        public int Attack1SubShellCount => _attack1SubShellCount;
        public float Attack1SubShellScatterRadius => _attack1SubShellScatterRadius;
        public float Attack1SubShellJumpPower => _attack1SubShellJumpPower;
        public float Attack1SubShellDuration => _attack1SubShellDuration;
        public float Attack1SubShellExplosionRadius => _attack1SubShellExplosionRadius;
        public float Attack1SubShellDamage => _attack1SubShellDamage;
        public float Attack1EnrageSecondWaveRadius => _attack1EnrageSecondWaveRadius;

        public float Attack2CenterMinOffset => _attack2CenterMinOffset;
        public float Attack2CenterMaxOffset => _attack2CenterMaxOffset;
        public float Attack2InitialWarning => _attack2InitialWarning;
        public int Attack2BounceStepCount => _attack2BounceStepCount;
        public float Attack2BounceStepDistance => _attack2BounceStepDistance;
        public float Attack2BounceJumpPower => _attack2BounceJumpPower;
        public float Attack2BounceDurationPerStep => _attack2BounceDurationPerStep;
        public float Attack2BounceExplosionRadius => _attack2BounceExplosionRadius;
        public float Attack2BounceDamage => _attack2BounceDamage;
        public int Attack2EnrageSalvoCount => _attack2EnrageSalvoCount;
        public float Attack2EnrageSalvoInterval => _attack2EnrageSalvoInterval;

        public float Attack3DropWarningDuration => _attack3DropWarningDuration;
        public float Attack3RollTelegraphLength => _attack3RollTelegraphLength;
        public float Attack3RollTelegraphWidth => _attack3RollTelegraphWidth;
        public float Attack3RollSpeed => _attack3RollSpeed;
        public float Attack3EnrageRollSpeed => _attack3EnrageRollSpeed;
        public float Attack3RollDamage => _attack3RollDamage;

        public float CooldownMin => _cooldownMin;
        public float CooldownMax => _cooldownMax;
        public float EnrageCooldownMin => _enrageCooldownMin;
        public float EnrageCooldownMax => _enrageCooldownMax;
    }
}
