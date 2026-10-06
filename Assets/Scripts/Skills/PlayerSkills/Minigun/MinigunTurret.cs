using System;
using Assets.ScriptableObjects;
using Assets.ScriptableObjects.Skills.PlayerSkills.MinigunSkill;
using Assets.Scripts.Audio;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.Initializers;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Skills.PlayerSkills.Minigun.Constants;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.Minigun
{
    public class MinigunTurret : MonoBehaviour, IInitializableWithScriptableConfig<MinigunSkillUpgradeableConfigSO>
    {
        [SerializeField] private TurretConfigSO _config;
        [SerializeField] private Transform _gunTip;
        [SerializeField] private Transform _visual;
        [SerializeField] private bool _inverseRotation;
        [SerializeField] private RectangularTelegraphIndicator _laneIndicator;
        [Tooltip("Maximum downward support probe distance in meters; missing support hides only the preview.")]
        [SerializeField] private float _groundProbeDistance = 5f;
        [SerializeField] private VFXPlayer _muzzleFlashVFXPlayer;
        [SerializeField] private AudioClipPlayer _audioClipPlayer;
        [SerializeField] private ParticleSystem _tracerParticleSystem;
        [SerializeField] private ParticleSystem _impactParticleSystem;
        [Tooltip("Visual bullet speed in meters per second.")]
        [SerializeField] private float _tracerSpeed = MinigunConstants.DEFAULT_TRACER_SPEED;

        private struct PendingHit
        {
            public float TimeRemaining;
            public IDamageable Target;
            public IHealth Health;
            public Vector3 ImpactPosition;
            public int Damage;
            public bool IsTerrainEndpoint;
        }

        private readonly PendingHit[] _pendingHits = new PendingHit[MinigunConstants.MAX_PENDING_HITS];
        private int _pendingHitCount;
        private MinigunSkillUpgradeableConfigSO _runtimeConfig;
        private MinigunShotResolver _resolver;
        private VFXPlayConfig _muzzleConfig;
        private bool _isInitialized;

        private void OnDisable()
        {
            if (_isInitialized) { HideAttackPresentation(); }
        }

        public bool IsInitialized()
        {
            return _isInitialized;
        }

        public void ValidateReferences()
        {
            if (_gunTip == null || _visual == null || _laneIndicator == null)
            {
                throw new InvalidOperationException("Minigun turret requires gun tip, visual, and lane indicator assignments.");
            }
            if (!MinigunShotResolver.IsFinite(_groundProbeDistance) || _groundProbeDistance <= 0f)
            {
                throw new InvalidOperationException("Minigun ground probe distance must be positive and finite.");
            }
            if (!MinigunShotResolver.IsFinite(_tracerSpeed) || _tracerSpeed <= 0f)
            {
                throw new InvalidOperationException("Minigun tracer speed must be positive and finite.");
            }
        }

        public void Initialize(MinigunSkillUpgradeableConfigSO config)
        {
            if (_isInitialized) { return; }
            ValidateReferences();
            _runtimeConfig = config;
            _config = config.TurretConfig;
            if (!MinigunShotResolver.IsFinite(_config.RotationDuration) || _config.RotationDuration <= 0f
                || !MinigunShotResolver.IsFinite(_config.RotationAngle))
            {
                throw new InvalidOperationException("Minigun sweep configuration is invalid.");
            }
            _resolver = new MinigunShotResolver();
            _muzzleConfig = new VFXPlayConfig();
            if (_tracerParticleSystem != null && !_tracerParticleSystem.isPlaying) { _tracerParticleSystem.Play(); }
            if (_impactParticleSystem != null && !_impactParticleSystem.isPlaying) { _impactParticleSystem.Play(); }
            _isInitialized = true;
            HideAttackPresentation();
            gameObject.SetActive(true);
        }

        public void UpdatePresentation(float scaledTime, float scaledDeltaTime)
        {
            if (scaledDeltaTime <= 0f) { return; }
            float angle = (_inverseRotation ? 1f : -1f) * _config.RotationAngle * 0.5f
                * Mathf.Cos(scaledTime * Mathf.PI / _config.RotationDuration);
            _visual.localRotation = Quaternion.Euler(0f, angle, 0f);
            Vector3 origin = _gunTip.position;
            Vector3 direction = GetDirection();
            float length = _resolver.GetTerrainLimitedLength(origin, direction, _runtimeConfig.Range.Value, _runtimeConfig.BeamWidth.Value);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit ground, _groundProbeDistance,
                TerrainLayers.Walkable, QueryTriggerInteraction.Ignore))
            {
                _laneIndicator.UpdatePersistent(new Vector3(origin.x, ground.point.y, origin.z), direction, length, _runtimeConfig.BeamWidth.Value);
            }
            else { _laneIndicator.HidePersistent(); }

            UpdatePendingHits(scaledDeltaTime);
        }

        public void Shoot()
        {
            Vector3 origin = _gunTip.position;
            Vector3 direction = GetDirection();
            float width = _runtimeConfig.BeamWidth.Value;
            int damage = _runtimeConfig.Damage.Value;
            MinigunShotResult result = _resolver.ResolveShot(origin, direction, _runtimeConfig.Range.Value, width, _runtimeConfig.Piercing.Value);
            try
            {
                if (_muzzleFlashVFXPlayer != null) { _muzzleFlashVFXPlayer.Play(_muzzleConfig); }
                if (_audioClipPlayer != null) { _audioClipPlayer.Play("Shoot"); }

                if (result.Distance >= MinigunConstants.MIN_TRACER_SPAWN_DISTANCE)
                {
                    EmitTracer(origin, direction, result.Distance);
                }

                ScheduleOrApplyShotHits(origin, direction, damage, result);
            }
            finally
            {
                _resolver.Clear();
            }
        }

        public void HideAttackPresentation()
        {
            if (!_isInitialized) { return; }
            _laneIndicator.HidePersistent();
            _resolver.Clear();
            ClearPendingHits();
        }

        private void ScheduleOrApplyShotHits(Vector3 origin, Vector3 direction, int damage, MinigunShotResult result)
        {
            bool isImmediate = result.Distance < MinigunConstants.MIN_TRACER_SPAWN_DISTANCE;
            int targetCount = _resolver.SelectedCount;

            for (int i = 0; i < targetCount; i++)
            {
                MinigunTargetHit hit = _resolver.GetSelectedHit(i);
                Vector3 impactPosition = origin + direction * hit.Distance;

                if (isImmediate)
                {
                    ApplyDamageIfAlive(hit.Target, hit.Health, damage);
                    EmitImpactSparks(impactPosition);
                }
                else
                {
                    float delay = hit.Distance / _tracerSpeed;
                    EnqueuePendingHit(new PendingHit
                    {
                        TimeRemaining = delay,
                        Target = hit.Target,
                        Health = hit.Health,
                        ImpactPosition = impactPosition,
                        Damage = damage,
                        IsTerrainEndpoint = false
                    });
                }
            }

            if (result.StopReason != MinigunShotStopReason.Range)
            {
                Vector3 endpointPosition = origin + direction * result.Distance;
                if (isImmediate)
                {
                    EmitImpactSparks(endpointPosition);
                }
                else
                {
                    float endpointDelay = result.Distance / _tracerSpeed;
                    EnqueuePendingHit(new PendingHit
                    {
                        TimeRemaining = endpointDelay,
                        Target = null,
                        Health = null,
                        ImpactPosition = endpointPosition,
                        Damage = 0,
                        IsTerrainEndpoint = true
                    });
                }
            }
        }

        private void EnqueuePendingHit(in PendingHit hit)
        {
            if (_pendingHitCount < _pendingHits.Length)
            {
                _pendingHits[_pendingHitCount++] = hit;
            }
            else
            {
                ExecutePendingHit(in hit);
            }
        }

        private void UpdatePendingHits(float scaledDeltaTime)
        {
            if (_pendingHitCount == 0) { return; }

            int writeIndex = 0;
            for (int i = 0; i < _pendingHitCount; i++)
            {
                _pendingHits[i].TimeRemaining -= scaledDeltaTime;
                if (_pendingHits[i].TimeRemaining <= 0f)
                {
                    ExecutePendingHit(in _pendingHits[i]);
                }
                else
                {
                    if (writeIndex != i)
                    {
                        _pendingHits[writeIndex] = _pendingHits[i];
                    }
                    writeIndex++;
                }
            }

            for (int i = writeIndex; i < _pendingHitCount; i++)
            {
                _pendingHits[i] = default;
            }
            _pendingHitCount = writeIndex;
        }

        private void ExecutePendingHit(in PendingHit hit)
        {
            if (!hit.IsTerrainEndpoint)
            {
                ApplyDamageIfAlive(hit.Target, hit.Health, hit.Damage);
            }
            EmitImpactSparks(hit.ImpactPosition);
        }

        private static void ApplyDamageIfAlive(IDamageable target, IHealth health, int damage)
        {
            if (IsTargetAlive(target, health))
            {
                target.TakeDamage(damage);
            }
        }

        private void EmitTracer(Vector3 origin, Vector3 direction, float distance)
        {
            if (_tracerParticleSystem == null) { return; }
            if (!_tracerParticleSystem.isPlaying) { _tracerParticleSystem.Play(); }

            float lifetime = Mathf.Min(distance / _tracerSpeed, MinigunConstants.MAX_TRACER_LIFETIME);
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = origin,
                velocity = direction * _tracerSpeed,
                startLifetime = lifetime,
                applyShapeToPosition = false
            };
            _tracerParticleSystem.Emit(emitParams, 1);
        }

        private void EmitImpactSparks(Vector3 impactPosition)
        {
            if (_impactParticleSystem == null) { return; }
            if (!_impactParticleSystem.isPlaying) { _impactParticleSystem.Play(); }

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = impactPosition,
                applyShapeToPosition = true
            };
            _impactParticleSystem.Emit(emitParams, MinigunConstants.DEFAULT_IMPACT_SPARK_COUNT);
        }

        private void ClearPendingHits()
        {
            Array.Clear(_pendingHits, 0, _pendingHitCount);
            _pendingHitCount = 0;
            if (_tracerParticleSystem != null) { _tracerParticleSystem.Clear(); }
            if (_impactParticleSystem != null) { _impactParticleSystem.Clear(); }
        }

        private static bool IsTargetAlive(IDamageable target, IHealth health)
        {
            if (target == null) { return false; }
            if (target is Component component)
            {
                if (component == null || !component.gameObject.activeInHierarchy) { return false; }
                if (component is Behaviour behaviour && !behaviour.isActiveAndEnabled) { return false; }
            }
            else if (target is UnityEngine.Object targetObject && targetObject == null)
            {
                return false;
            }

            if (health is UnityEngine.Object healthObject && healthObject == null)
            {
                return false;
            }

            return health == null || health.IsAlive();
        }

        private Vector3 GetDirection()
        {
            Vector3 direction = _gunTip.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0f) { throw new InvalidOperationException("Minigun muzzle has no horizontal firing direction."); }
            return direction.normalized;
        }

        private void OnDrawGizmosSelected()
        {
            if (!_isInitialized) { return; }
            Vector3 direction = GetDirection();
            float range = _runtimeConfig.Range.Value;
            float width = _runtimeConfig.BeamWidth.Value;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(_gunTip.position + direction * range * 0.5f,
                Quaternion.LookRotation(direction), Vector3.one);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, width, range));
            Gizmos.matrix = previous;
        }
    }
}
