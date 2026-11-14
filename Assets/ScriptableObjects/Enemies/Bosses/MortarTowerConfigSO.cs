using UnityEngine;

namespace Assets.ScriptableObjects.Enemies.Bosses
{
    [CreateAssetMenu(fileName = "MortarTowerConfig", menuName = "ScriptableObjects/Bosses/MortarTowerConfig")]
    public class MortarTowerConfigSO : TowerBossConfigSO
    {
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

        public float Attack1InitialWarning
        {
            get { return _attack1InitialWarning; }
        }

        public float Attack1CenterExplosionRadius
        {
            get { return _attack1CenterExplosionRadius; }
        }

        public float Attack1CenterDamage
        {
            get { return _attack1CenterDamage; }
        }

        public int Attack1SubShellCount
        {
            get { return _attack1SubShellCount; }
        }

        public float Attack1SubShellScatterRadius
        {
            get { return _attack1SubShellScatterRadius; }
        }

        public float Attack1SubShellJumpPower
        {
            get { return _attack1SubShellJumpPower; }
        }

        public float Attack1SubShellDuration
        {
            get { return _attack1SubShellDuration; }
        }

        public float Attack1SubShellExplosionRadius
        {
            get { return _attack1SubShellExplosionRadius; }
        }

        public float Attack1SubShellDamage
        {
            get { return _attack1SubShellDamage; }
        }

        public float Attack1EnrageSecondWaveRadius
        {
            get { return _attack1EnrageSecondWaveRadius; }
        }

        public float Attack2CenterMinOffset
        {
            get { return _attack2CenterMinOffset; }
        }

        public float Attack2CenterMaxOffset
        {
            get { return _attack2CenterMaxOffset; }
        }

        public float Attack2InitialWarning
        {
            get { return _attack2InitialWarning; }
        }

        public int Attack2BounceStepCount
        {
            get { return _attack2BounceStepCount; }
        }

        public float Attack2BounceStepDistance
        {
            get { return _attack2BounceStepDistance; }
        }

        public float Attack2BounceJumpPower
        {
            get { return _attack2BounceJumpPower; }
        }

        public float Attack2BounceDurationPerStep
        {
            get { return _attack2BounceDurationPerStep; }
        }

        public float Attack2BounceExplosionRadius
        {
            get { return _attack2BounceExplosionRadius; }
        }

        public float Attack2BounceDamage
        {
            get { return _attack2BounceDamage; }
        }

        public int Attack2EnrageSalvoCount
        {
            get { return _attack2EnrageSalvoCount; }
        }

        public float Attack2EnrageSalvoInterval
        {
            get { return _attack2EnrageSalvoInterval; }
        }

        public float Attack3DropWarningDuration
        {
            get { return _attack3DropWarningDuration; }
        }

        public float Attack3RollTelegraphLength
        {
            get { return _attack3RollTelegraphLength; }
        }

        public float Attack3RollTelegraphWidth
        {
            get { return _attack3RollTelegraphWidth; }
        }

        public float Attack3RollSpeed
        {
            get { return _attack3RollSpeed; }
        }

        public float Attack3EnrageRollSpeed
        {
            get { return _attack3EnrageRollSpeed; }
        }

        public float Attack3RollDamage
        {
            get { return _attack3RollDamage; }
        }
    }
}
