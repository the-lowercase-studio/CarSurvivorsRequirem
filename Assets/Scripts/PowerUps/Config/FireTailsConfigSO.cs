using System;
using UnityEngine;

namespace Assets.Scripts.PowerUps.Config
{
    [CreateAssetMenu(fileName = "FireTailsConfig", menuName = "Scriptable Objects/Power-Ups/Fire Tails Config")]
    public class FireTailsConfigSO : ScriptableObject
    {
        [SerializeField, Min(0.1f), Tooltip("Total active duration of the Fire Tails power-up in scaled seconds. Re-picking resets to this value.")]
        private float _durationSeconds = 8.0f;

        [SerializeField, Min(0.1f), Tooltip("Lifetime in seconds of each ground fire trail segment and hazard node.")]
        private float _hazardLifetimeSeconds = 3.5f;

        [SerializeField, Min(0.05f), Tooltip("Duration in seconds at the end of a fire trail's lifetime over which its visual gradient fades out.")]
        private float _trailFadeOutDuration = 0.5f;

        [SerializeField, Min(0.2f), Tooltip("Minimum distance in meters travelled by a drifting rear wheel before recording the next fire hazard node on that track.")]
        private float _spawnIntervalDistance = 1.2f;

        [SerializeField, Min(0.1f), Tooltip("World-space radius in meters of each fire hazard node queried against enemies.")]
        private float _hazardRadius = 1.1f;

        [SerializeField, Min(0.1f), Tooltip("Fire damage dealt per tick to any enemy or boss inside one or more fire hazard nodes.")]
        private float _damagePerTick = 25f;

        [SerializeField, Min(0.05f), Tooltip("Interval in seconds between consecutive fire damage ticks on an affected target.")]
        private float _tickIntervalSeconds = 0.5f;

        [SerializeField, Min(0.02f), Tooltip("Interval in seconds between Physics.OverlapSphereNonAlloc combat scans across active fire hazard nodes (0.1s = 10 Hz).")]
        private float _overlapCheckIntervalSeconds = 0.1f;

        [SerializeField, Range(0.1f, 1f), Tooltip("Movement speed multiplier applied to regular enemies inside any fire hazard node (0.6 = 40% slow). Bosses ignore slow.")]
        private float _slowFactor = 0.6f;

        public float DurationSeconds
        {
            get
            {
                return _durationSeconds;
            }
        }

        public float HazardLifetimeSeconds
        {
            get
            {
                return _hazardLifetimeSeconds;
            }
        }

        public float TrailFadeOutDuration
        {
            get
            {
                return _trailFadeOutDuration;
            }
        }

        public float SpawnIntervalDistance
        {
            get
            {
                return _spawnIntervalDistance;
            }
        }

        public float HazardRadius
        {
            get
            {
                return _hazardRadius;
            }
        }

        public float DamagePerTick
        {
            get
            {
                return _damagePerTick;
            }
        }

        public float TickIntervalSeconds
        {
            get
            {
                return _tickIntervalSeconds;
            }
        }

        public float OverlapCheckIntervalSeconds
        {
            get
            {
                return _overlapCheckIntervalSeconds;
            }
        }

        public float SlowFactor
        {
            get
            {
                return _slowFactor;
            }
        }

        public void ValidateConfiguration()
        {
            if (!IsFinite(_durationSeconds) || _durationSeconds <= 0f)
            {
                throw new InvalidOperationException("FireTailsConfig durationSeconds must be a finite positive number.");
            }

            if (!IsFinite(_hazardLifetimeSeconds) || _hazardLifetimeSeconds <= 0f)
            {
                throw new InvalidOperationException("FireTailsConfig hazardLifetimeSeconds must be a finite positive number.");
            }

            if (!IsFinite(_trailFadeOutDuration) || _trailFadeOutDuration <= 0f)
            {
                throw new InvalidOperationException("FireTailsConfig trailFadeOutDuration must be a finite positive number.");
            }

            if (_trailFadeOutDuration > _hazardLifetimeSeconds)
            {
                throw new InvalidOperationException("FireTailsConfig trailFadeOutDuration cannot exceed hazardLifetimeSeconds.");
            }

            if (!IsFinite(_spawnIntervalDistance) || _spawnIntervalDistance <= 0f)
            {
                throw new InvalidOperationException("FireTailsConfig spawnIntervalDistance must be a finite positive number.");
            }

            if (!IsFinite(_hazardRadius) || _hazardRadius <= 0f)
            {
                throw new InvalidOperationException("FireTailsConfig hazardRadius must be a finite positive number.");
            }

            if (!IsFinite(_damagePerTick) || _damagePerTick <= 0f)
            {
                throw new InvalidOperationException("FireTailsConfig damagePerTick must be a finite positive number.");
            }

            if (!IsFinite(_tickIntervalSeconds) || _tickIntervalSeconds <= 0f)
            {
                throw new InvalidOperationException("FireTailsConfig tickIntervalSeconds must be a finite positive number.");
            }

            if (!IsFinite(_overlapCheckIntervalSeconds) || _overlapCheckIntervalSeconds <= 0f)
            {
                throw new InvalidOperationException("FireTailsConfig overlapCheckIntervalSeconds must be a finite positive number.");
            }

            if (_overlapCheckIntervalSeconds > _tickIntervalSeconds)
            {
                throw new InvalidOperationException("FireTailsConfig overlapCheckIntervalSeconds cannot exceed tickIntervalSeconds.");
            }

            if (!IsFinite(_slowFactor) || _slowFactor <= 0f || _slowFactor > 1f)
            {
                throw new InvalidOperationException("FireTailsConfig slowFactor must be in the range (0, 1].");
            }
        }

        private void OnValidate()
        {
            _durationSeconds = Mathf.Max(0.1f, _durationSeconds);
            _hazardLifetimeSeconds = Mathf.Max(0.1f, _hazardLifetimeSeconds);
            _trailFadeOutDuration = Mathf.Clamp(_trailFadeOutDuration, 0.05f, _hazardLifetimeSeconds);
            _spawnIntervalDistance = Mathf.Max(0.2f, _spawnIntervalDistance);
            _hazardRadius = Mathf.Max(0.1f, _hazardRadius);
            _damagePerTick = Mathf.Max(0.1f, _damagePerTick);
            _tickIntervalSeconds = Mathf.Max(0.05f, _tickIntervalSeconds);
            _overlapCheckIntervalSeconds = Mathf.Clamp(_overlapCheckIntervalSeconds, 0.02f, _tickIntervalSeconds);
            _slowFactor = Mathf.Clamp(_slowFactor, 0.1f, 1f);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
