using UnityEngine;

namespace Assets.ScriptableObjects.Enemies.Bosses
{
    public abstract class TowerBossConfigSO : ScriptableObject
    {
        [Header("Health & Phase Configuration")]
        [Tooltip("Maximum health of the tower boss.")]
        [SerializeField] private float _maxHealth = 3500f;
        [Tooltip("Health ratio threshold [0..1] that triggers enrage.")]
        [SerializeField] private float _enrageHealthPercent = 0.4f;
        [Tooltip("Experience awarded when the tower boss is defeated.")]
        [SerializeField] private float _expReward = 1000f;
        [Tooltip("Base material tint color applied during enrage.")]
        [SerializeField] private Color _enrageColor = new Color(1f, 0.2f, 0.1f, 1f);
        [Tooltip("HDR emission color applied during enrage.")]
        [SerializeField] [ColorUsage(true, true)] private Color _enrageEmissionColor = new Color(2f, 0.4f, 0.1f, 1f);

        [Header("Arena & Leash Settings")]
        [Tooltip("Radius of the circular encounter arena in meters.")]
        [SerializeField] private float _arenaRadius = 22f;
        [Tooltip("Grace period in seconds before leaving the arena resets the encounter.")]
        [SerializeField] private float _leashGracePeriodSeconds = 2.5f;

        [Header("Camera Settings")]
        [Tooltip("Cinemachine follow offset transitioned to during active tower combat.")]
        [SerializeField] private Vector3 _combatFollowOffset = new Vector3(0f, 17f, -13f);
        [Tooltip("Duration in seconds of the camera follow offset transition.")]
        [SerializeField] private float _cameraTransitionDuration = 0.8f;

        [Header("Cooldown & Phase Modifiers")]
        [Tooltip("Minimum cooldown duration in seconds between attacks in normal phase.")]
        [SerializeField] private float _cooldownMin = 3.5f;
        [Tooltip("Maximum cooldown duration in seconds between attacks in normal phase.")]
        [SerializeField] private float _cooldownMax = 4.5f;
        [Tooltip("Minimum cooldown duration in seconds between attacks when enraged.")]
        [SerializeField] private float _enrageCooldownMin = 2.0f;
        [Tooltip("Maximum cooldown duration in seconds between attacks when enraged.")]
        [SerializeField] private float _enrageCooldownMax = 2.5f;

        public float MaxHealth
        {
            get { return _maxHealth; }
        }

        public float EnrageHealthPercent
        {
            get { return _enrageHealthPercent; }
        }

        public float ExpReward
        {
            get { return _expReward; }
        }

        public Color EnrageColor
        {
            get { return _enrageColor; }
        }

        public Color EnrageEmissionColor
        {
            get { return _enrageEmissionColor; }
        }

        public float ArenaRadius
        {
            get { return _arenaRadius; }
        }

        public float LeashGracePeriodSeconds
        {
            get { return _leashGracePeriodSeconds; }
        }

        public Vector3 CombatFollowOffset
        {
            get { return _combatFollowOffset; }
        }

        public float CameraTransitionDuration
        {
            get { return _cameraTransitionDuration; }
        }

        public float CooldownMin
        {
            get { return _cooldownMin; }
        }

        public float CooldownMax
        {
            get { return _cooldownMax; }
        }

        public float EnrageCooldownMin
        {
            get { return _enrageCooldownMin; }
        }

        public float EnrageCooldownMax
        {
            get { return _enrageCooldownMax; }
        }
    }
}
