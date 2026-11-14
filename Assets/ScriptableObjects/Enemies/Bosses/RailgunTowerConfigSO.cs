using UnityEngine;

namespace Assets.ScriptableObjects.Enemies.Bosses
{
    [CreateAssetMenu(fileName = "RailgunTowerConfig", menuName = "ScriptableObjects/Bosses/RailgunTowerConfig")]
    public class RailgunTowerConfigSO : TowerBossConfigSO
    {
        [Header("Attack 1: Railgun Laser Sweep")]
        [Tooltip("Stationary telegraph duration in seconds before the sweep begins in normal phase.")]
        [SerializeField] private float _attack1PreSweepWarningDuration = 1.5f;
        [Tooltip("Stationary telegraph duration in seconds before sweep pass 1 begins when enraged.")]
        [SerializeField] private float _attack1EnragePreSweepWarningDuration = 0.75f;
        [Tooltip("Short re-aim warning duration in seconds before enrage sweep pass 2.")]
        [SerializeField] private float _attack1EnrageReaimWarningDuration = 0.45f;
        [Tooltip("Total angular arc in degrees covered by a normal-phase sweep pass.")]
        [SerializeField] private float _attack1SweepAngleDegrees = 90f;
        [Tooltip("Active sweep duration in seconds in normal phase.")]
        [SerializeField] private float _attack1SweepDuration = 2.4f;
        [Tooltip("Total angular arc in degrees covered by each enraged sweep pass.")]
        [SerializeField] private float _attack1EnrageSweepAngleDegrees = 110f;
        [Tooltip("Active sweep duration in seconds per pass when enraged.")]
        [SerializeField] private float _attack1EnrageSweepDuration = 1.7f;
        [Tooltip("Number of sweep passes executed during enrage.")]
        [SerializeField] private int _attack1EnragePassCount = 2;
        [Tooltip("Maximum forward reach of the sweeping laser lane in meters.")]
        [SerializeField] private float _attack1LaneLength = 22f;
        [Tooltip("Width of the sweeping laser lane hitbox and telegraph in meters.")]
        [SerializeField] private float _attack1LaneWidth = 2.6f;
        [Tooltip("Forward propagation speed of the laser wavefront in meters per second.")]
        [SerializeField] private float _attack1WavefrontSpeed = 36f;
        [Tooltip("Damage dealt to the player once per sweep pass.")]
        [SerializeField] private float _attack1Damage = 28f;

        [Header("Attack 2: Chasing Laser Circle")]
        [Tooltip("Initial stationary lock-on warning delay in seconds before chase and tick damage start.")]
        [SerializeField] private float _attack2LockOnDelay = 0.6f;
        [Tooltip("Duration in seconds of active pursuit in both normal and enraged phases.")]
        [SerializeField] private float _attack2ChaseDuration = 4.0f;
        [Tooltip("Radius in meters of the chasing laser zone in normal phase.")]
        [SerializeField] private float _attack2NormalRadius = 2.24f;
        [Tooltip("Radius in meters of the chasing laser zone when enraged.")]
        [SerializeField] private float _attack2EnrageRadius = 4.0f;
        [Tooltip("Chase speed multiplier relative to player MaxForwardSpeed in normal phase.")]
        [SerializeField] private float _attack2NormalSpeedRatio = 0.85f;
        [Tooltip("Chase speed multiplier relative to player MaxForwardSpeed when enraged (requires drift-boost to outrun).")]
        [SerializeField] private float _attack2EnrageSpeedRatio = 1.15f;
        [Tooltip("Interval in seconds between damage pulses while the player is inside the laser circle.")]
        [SerializeField] private float _attack2DamageTickInterval = 0.35f;
        [Tooltip("Damage dealt per tick inside the chasing laser circle.")]
        [SerializeField] private float _attack2TickDamage = 14f;

        [Header("Attack 3: Explosive Cross Burst")]
        [Tooltip("Number of energy orbs launched per volley in normal phase.")]
        [SerializeField] private int _attack3NormalProjectileCount = 2;
        [Tooltip("Number of energy orbs launched per volley when enraged.")]
        [SerializeField] private int _attack3EnrageProjectileCount = 4;
        [Tooltip("Delay in seconds between consecutive orb launches in a volley.")]
        [SerializeField] private float _attack3LaunchStagger = 0.2f;
        [Tooltip("Orb flight and telegraph duration in seconds in normal phase.")]
        [SerializeField] private float _attack3NormalFlightDuration = 0.9f;
        [Tooltip("Orb flight and telegraph duration in seconds when enraged.")]
        [SerializeField] private float _attack3EnrageFlightDuration = 0.75f;
        [Tooltip("Radius in meters of each orb's direct center impact check.")]
        [SerializeField] private float _attack3CenterImpactRadius = 2.4f;
        [Tooltip("Damage dealt on direct center impact (suppresses bidirectional wave split for that orb).")]
        [SerializeField] private float _attack3CenterImpactDamage = 38f;
        [Tooltip("Travel distance in meters for each opposing wave half from the impact center.")]
        [SerializeField] private float _attack3WaveHalfLength = 12f;
        [Tooltip("Width in meters of each split energy wave corridor.")]
        [SerializeField] private float _attack3WaveWidth = 2.2f;
        [Tooltip("Forward propagation speed in meters per second of each split energy wave.")]
        [SerializeField] private float _attack3WaveSpeed = 26f;
        [Tooltip("Damage dealt once per detonation by the bidirectional split waves.")]
        [SerializeField] private float _attack3WaveDamage = 24f;
        [Tooltip("Random angular jitter in degrees applied to each orb's split axis.")]
        [SerializeField] private float _attack3AngleJitterDegrees = 10f;
        [Tooltip("Minimum offset distance in meters from the scatter zone center for orb landing targets.")]
        [SerializeField] private float _attack3TargetOffsetMin = 0f;
        [Tooltip("Maximum radius in meters of the invisible target scatter circle initially centered on the player.")]
        [SerializeField] private float _attack3TargetOffsetMax = 11.5f;
        [Tooltip("Minimum center-to-center XZ distance in meters between orb landing targets inside the burst.")]
        [SerializeField] private float _attack3MinTargetSpacing = 6.2f;

        public float Attack1PreSweepWarningDuration
        {
            get { return _attack1PreSweepWarningDuration; }
        }

        public float Attack1EnragePreSweepWarningDuration
        {
            get { return _attack1EnragePreSweepWarningDuration; }
        }

        public float Attack1EnrageReaimWarningDuration
        {
            get { return _attack1EnrageReaimWarningDuration; }
        }

        public float Attack1SweepAngleDegrees
        {
            get { return _attack1SweepAngleDegrees; }
        }

        public float Attack1SweepDuration
        {
            get { return _attack1SweepDuration; }
        }

        public float Attack1EnrageSweepAngleDegrees
        {
            get { return _attack1EnrageSweepAngleDegrees; }
        }

        public float Attack1EnrageSweepDuration
        {
            get { return _attack1EnrageSweepDuration; }
        }

        public int Attack1EnragePassCount
        {
            get { return _attack1EnragePassCount; }
        }

        public float Attack1LaneLength
        {
            get { return _attack1LaneLength; }
        }

        public float Attack1LaneWidth
        {
            get { return _attack1LaneWidth; }
        }

        public float Attack1WavefrontSpeed
        {
            get { return _attack1WavefrontSpeed; }
        }

        public float Attack1Damage
        {
            get { return _attack1Damage; }
        }

        public float Attack2LockOnDelay
        {
            get { return _attack2LockOnDelay; }
        }

        public float Attack2ChaseDuration
        {
            get { return _attack2ChaseDuration; }
        }

        public float Attack2NormalRadius
        {
            get { return _attack2NormalRadius; }
        }

        public float Attack2EnrageRadius
        {
            get { return _attack2EnrageRadius; }
        }

        public float Attack2NormalSpeedRatio
        {
            get { return _attack2NormalSpeedRatio; }
        }

        public float Attack2EnrageSpeedRatio
        {
            get { return _attack2EnrageSpeedRatio; }
        }

        public float Attack2DamageTickInterval
        {
            get { return _attack2DamageTickInterval; }
        }

        public float Attack2TickDamage
        {
            get { return _attack2TickDamage; }
        }

        public int Attack3NormalProjectileCount
        {
            get { return _attack3NormalProjectileCount; }
        }

        public int Attack3EnrageProjectileCount
        {
            get { return _attack3EnrageProjectileCount; }
        }

        public float Attack3LaunchStagger
        {
            get { return _attack3LaunchStagger; }
        }

        public float Attack3NormalFlightDuration
        {
            get { return _attack3NormalFlightDuration; }
        }

        public float Attack3EnrageFlightDuration
        {
            get { return _attack3EnrageFlightDuration; }
        }

        public float Attack3CenterImpactRadius
        {
            get { return _attack3CenterImpactRadius; }
        }

        public float Attack3CenterImpactDamage
        {
            get { return _attack3CenterImpactDamage; }
        }

        public float Attack3WaveHalfLength
        {
            get { return _attack3WaveHalfLength; }
        }

        public float Attack3WaveWidth
        {
            get { return _attack3WaveWidth; }
        }

        public float Attack3WaveSpeed
        {
            get { return _attack3WaveSpeed; }
        }

        public float Attack3WaveDamage
        {
            get { return _attack3WaveDamage; }
        }

        public float Attack3AngleJitterDegrees
        {
            get { return _attack3AngleJitterDegrees; }
        }

        public float Attack3TargetOffsetMin
        {
            get { return _attack3TargetOffsetMin; }
        }

        public float Attack3TargetOffsetMax
        {
            get { return _attack3TargetOffsetMax; }
        }

        public float Attack3MinTargetSpacing
        {
            get { return _attack3MinTargetSpacing; }
        }
    }
}
