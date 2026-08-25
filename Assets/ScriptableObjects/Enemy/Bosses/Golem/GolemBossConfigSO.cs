using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.Config
{
    [CreateAssetMenu(fileName = "GolemBossConfig", menuName = "ScriptableObjects/Bosses/GolemBossConfig")]
    public class GolemBossConfigSO : ScriptableObject
    {
        [Header("Base Stats")]
        [SerializeField] private float _maxHealth = 5000f;
        [SerializeField] private float _moveSpeed = 7.5f;
        [SerializeField] private float _rotationSpeed = 120f;
        [SerializeField] private float _bodyContactDamage = 25f;
        [SerializeField] private float _expForKill = 500f;

        [Header("Phase Thresholds & Multipliers")]
        [SerializeField] private float _phase2HealthPercent = 0.6f;
        [SerializeField] private float _phase3HealthPercent = 0.3f;
        [SerializeField] private float _phase2CooldownMultiplier = 0.7f;
        [SerializeField] private float _phase3CooldownMultiplier = 0.4f;
        [SerializeField] private float _phase2SpeedMultiplier = 1.15f;
        [SerializeField] private float _phase3SpeedMultiplier = 1.35f;
        [SerializeField] private float _phase2ArmSpeedMultiplier = 1.25f;
        [SerializeField] private float _phase3ArmSpeedMultiplier = 1.6f;

        [Header("Attack 1: Leap Slam (AOE / Anti-Kiting)")]
        [SerializeField] private float _leapTriggerMaxDistance = 25f;
        [SerializeField] private float _leapCooldown = 8f;
        [SerializeField] private float _leapAirTime = 1.5f;
        [SerializeField] private float _leapMaxHeight = 35f;
        [SerializeField] private float _slamRadius = 13.0f;
        [SerializeField] private float _slamDamage = 50f;
        [SerializeField] private float _leapWarningDuration = 1.2f;
        [SerializeField] private float _leapTakeoffDuration = 0.57f;
        [SerializeField] private float _leapLandingDuration = 1.27f;

        [Header("Attack 2: Melee Foot Stomp")]
        [SerializeField] private float _stompRadius = 3.5f;
        [SerializeField] private float _stompCooldown = 2.5f;
        [SerializeField] private float _stompDamage = 30f;

        [Header("Attack 3: Linear Rocket Fists")]
        [SerializeField] private float _linearFistCooldown = 6f;
        [SerializeField] private float _linearFistChargeDuration = 0.8f;
        [SerializeField] private float _linearFistReleaseDelay = 1.2f;
        [SerializeField] private float _linearFistSpeed = 30f;
        [SerializeField] private float _linearFistMaxDistance = 20f;
        [SerializeField] private float _linearFistDamage = 40f;
        [SerializeField] private float _linearFistWidth = 2f;
        [SerializeField] private float _linearFistWarningDuration = 0.8f;
        [SerializeField] private float _linearFistHitboxHeight = 2.5f;
        [SerializeField] private float _linearFistHitboxDepth = 1.5f;
        [SerializeField] private float _linearFistHitboxVerticalOffset = 1.0f;

        [Header("Attack 4: Sky Arm Barrage")]
        [SerializeField] private float _skyBarrageCooldown = 10f;
        [SerializeField] private float _skyBarrageReleaseDelay = 1.5f;
        [SerializeField] private int _skyBarrageCyclesPhase1 = 2;
        [SerializeField] private int _skyBarrageCyclesPhase2 = 2;
        [SerializeField] private int _skyBarrageCyclesPhase3 = 3;
        [SerializeField] private float _skyArmLaunchAirTime = 1.0f;
        [SerializeField] private float _skyArmFallSpeed = 35f;
        [SerializeField] private float _skyArmImpactRadius = 4.8f;
        [SerializeField] private float _skyArmDamage = 45f;
        [SerializeField] private float _skyArmTargetOffsetMinRadius = 1.5f;
        [SerializeField] private float _skyArmTargetOffsetMaxRadius = 5.5f;
        [SerializeField] private float _skyArmInitialStaggerDelay = 0.4f;
        [SerializeField] private float _skyArmCycleResetDelay = 0.5f;
        [SerializeField] private float _skyArmWarningDuration = 1.0f;

        [Header("Enrage Settings")]
        [SerializeField] private Color _enrageColor = Color.red;
        [SerializeField][ColorUsage(true, true)] private Color _enrageEmissionColor = new Color(2f, 0.2f, 0.2f);

        public float MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public float RotationSpeed => _rotationSpeed;
        public float BodyContactDamage => _bodyContactDamage;
        public float ExpForKill => _expForKill;

        public float Phase2HealthPercent => _phase2HealthPercent;
        public float Phase3HealthPercent => _phase3HealthPercent;
        public float Phase2CooldownMultiplier => _phase2CooldownMultiplier;
        public float Phase3CooldownMultiplier => _phase3CooldownMultiplier;
        public float Phase2SpeedMultiplier => _phase2SpeedMultiplier;
        public float Phase3SpeedMultiplier => _phase3SpeedMultiplier;
        public float Phase2ArmSpeedMultiplier => _phase2ArmSpeedMultiplier;
        public float Phase3ArmSpeedMultiplier => _phase3ArmSpeedMultiplier;

        public float LeapTriggerMaxDistance => _leapTriggerMaxDistance;
        public float LeapCooldown => _leapCooldown;
        public float LeapAirTime => _leapAirTime;
        public float LeapMaxHeight => _leapMaxHeight;
        public float SlamRadius => _slamRadius;
        public float SlamDamage => _slamDamage;
        public float LeapWarningDuration => _leapWarningDuration;
        public float LeapTakeoffDuration => _leapTakeoffDuration;
        public float LeapLandingDuration => _leapLandingDuration;

        public float StompRadius => _stompRadius;
        public float StompCooldown => _stompCooldown;
        public float StompDamage => _stompDamage;

        public float LinearFistCooldown => _linearFistCooldown;
        public float LinearFistChargeDuration => _linearFistChargeDuration;
        public float LinearFistReleaseDelay => _linearFistReleaseDelay;
        public float LinearFistSpeed => _linearFistSpeed;
        public float LinearFistMaxDistance => _linearFistMaxDistance;
        public float LinearFistDamage => _linearFistDamage;
        public float LinearFistWidth => _linearFistWidth;
        public float LinearFistWarningDuration => _linearFistWarningDuration;
        public float LinearFistHitboxHeight => _linearFistHitboxHeight;
        public float LinearFistHitboxDepth => _linearFistHitboxDepth;
        public float LinearFistHitboxVerticalOffset => _linearFistHitboxVerticalOffset;

        public float SkyBarrageCooldown => _skyBarrageCooldown;
        public float SkyBarrageReleaseDelay => _skyBarrageReleaseDelay;
        public int SkyBarrageCyclesPhase1 => _skyBarrageCyclesPhase1;
        public int SkyBarrageCyclesPhase2 => _skyBarrageCyclesPhase2;
        public int SkyBarrageCyclesPhase3 => _skyBarrageCyclesPhase3;
        public float SkyArmLaunchAirTime => _skyArmLaunchAirTime;
        public float SkyArmFallSpeed => _skyArmFallSpeed;
        public float SkyArmImpactRadius => _skyArmImpactRadius;
        public float SkyArmDamage => _skyArmDamage;
        public float SkyArmTargetOffsetMinRadius => _skyArmTargetOffsetMinRadius;
        public float SkyArmTargetOffsetMaxRadius => _skyArmTargetOffsetMaxRadius;
        public float SkyArmInitialStaggerDelay => _skyArmInitialStaggerDelay;
        public float SkyArmCycleResetDelay => _skyArmCycleResetDelay;
        public float SkyArmWarningDuration => _skyArmWarningDuration;

        public Color EnrageColor => _enrageColor;
        public Color EnrageEmissionColor => _enrageEmissionColor;
    }
}
