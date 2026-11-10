using Assets.Scripts.Player.Constants;
using System;
using UnityEngine;

namespace Assets.Scripts.Player.Car
{
    public interface ICarVfxEffectsController
    {
        int RearDriftEmitterCount { get; }
        bool TryGetRearDriftEmitterWorldCandidate(int emitterIndex, out Vector3 worldCandidate);
        void ConfigureFireTailsTimings(float trailLifetime, float trailFadeTime);
        void SetFireTailsActive(bool isActive);
        void SetFireTailsFrozen(bool isFrozen);
    }

    [RequireComponent(typeof(CarController))]
    public class CarVfxEffectsController : MonoBehaviour, ICarVfxEffectsController
    {
        private sealed class DriftEmitter
        {
            public readonly TrailRenderer Template;
            public readonly Transform ContactParent;
            public readonly Vector3 LocalContactPosition;
            public readonly CarDriftTrailSegmentPool Pool;
            public readonly Vector3[] ContinuityPoints = new Vector3[CarVfxConstants.DRIFT_MAX_CONTINUITY_SAMPLES];
            public Vector3 PreviousCandidate;
            public bool HasContinuity;

            public DriftEmitter(TrailRenderer template)
            {
                Template = template;
                ContactParent = template.transform.parent;
                LocalContactPosition = template.transform.localPosition;
                Pool = new CarDriftTrailSegmentPool(template);
            }
        }

        [SerializeField] private MeshRenderer _carMeshRenderer;

        [Header("Car Stop Effect")]
        [SerializeField] private GameObject _carBackLightsHolder;

        [Header("Car Fast Effect")]
        [Tooltip("Speed trail effects for the rear of the vehicle (active during high-speed forward driving).")]
        [SerializeField] private TrailRenderer[] _rearTrailRenderers;

        [Tooltip("Speed trail effects for the front of the vehicle (active during high-speed reversing).")]
        [SerializeField] private TrailRenderer[] _frontTrailRenderers;

        [Tooltip("Fade-out time of the speed trail.")]
        [SerializeField] private float _trailDisappearingSpeed = 0.3f;

        [Tooltip("Speed threshold above which speed trails are enabled.")]
        [SerializeField] private float _thresholdToStartSpeedTrail = 5f;

        [Header("Car Drift Effect")]
        [Tooltip("Drift tire skid mark effects assigned to the 2 rear wheels (rear left and rear right).")]
        [SerializeField] private TrailRenderer[] _rearDriftTrailRenderers;

        [Tooltip("Duration in seconds that drift skid marks remain visible on the ground.")]
        [SerializeField] private float _driftTrailLifetime = 3.0f;

        [Tooltip("Duration in seconds over which drift skid marks smoothly fade out at the end of their lifetime.")]
        [SerializeField] private float _driftTrailFadeTime = 0.5f;

        [Header("Car Fire Tails Effect")]
        [Tooltip("Fire trail renderers assigned to the 2 rear wheels (used instead of black drift skid marks when Fire Tails is active).")]
        [SerializeField] private TrailRenderer[] _rearFireDriftTrailRenderers;

        [Tooltip("Constant flame particle systems on the 2 rear wheels active for the entire Fire Tails duration.")]
        [SerializeField] private ParticleSystem[] _rearWheelConstantFireParticles;

        [Tooltip("World-space flame particle systems on the 2 rear wheels that emit lingering ground fire particles only while drifting with Fire Tails active.")]
        [SerializeField] private ParticleSystem[] _rearDriftFireTrailParticles;

        private ICarController _carController;
        private Material _carStopLightsMat;
        private DriftEmitter[] _driftEmitters;
        private DriftEmitter[] _fireDriftEmitters;
        private bool _isFireTailsActive;
        private bool _isFireTailsFrozen;
        private bool _isInitialized;

        public int RearDriftEmitterCount
        {
            get
            {
                int standardCount = _driftEmitters != null ? _driftEmitters.Length : (_rearDriftTrailRenderers != null ? _rearDriftTrailRenderers.Length : 0);
                int fireCount = _fireDriftEmitters != null ? _fireDriftEmitters.Length : (_rearFireDriftTrailRenderers != null ? _rearFireDriftTrailRenderers.Length : 0);
                return Mathf.Max(standardCount, fireCount);
            }
        }

        public bool TryGetRearDriftEmitterWorldCandidate(int emitterIndex, out Vector3 worldCandidate)
        {
            if (_fireDriftEmitters != null && emitterIndex >= 0 && emitterIndex < _fireDriftEmitters.Length && _fireDriftEmitters[emitterIndex] != null)
            {
                DriftEmitter emitter = _fireDriftEmitters[emitterIndex];
                worldCandidate = emitter.ContactParent == null
                    ? emitter.LocalContactPosition
                    : emitter.ContactParent.TransformPoint(emitter.LocalContactPosition);
                return true;
            }

            if (_driftEmitters != null && emitterIndex >= 0 && emitterIndex < _driftEmitters.Length && _driftEmitters[emitterIndex] != null)
            {
                DriftEmitter emitter = _driftEmitters[emitterIndex];
                worldCandidate = emitter.ContactParent == null
                    ? emitter.LocalContactPosition
                    : emitter.ContactParent.TransformPoint(emitter.LocalContactPosition);
                return true;
            }

            if (_rearFireDriftTrailRenderers != null && emitterIndex >= 0 && emitterIndex < _rearFireDriftTrailRenderers.Length && _rearFireDriftTrailRenderers[emitterIndex] != null)
            {
                worldCandidate = _rearFireDriftTrailRenderers[emitterIndex].transform.position;
                return true;
            }

            if (_rearDriftTrailRenderers != null && emitterIndex >= 0 && emitterIndex < _rearDriftTrailRenderers.Length && _rearDriftTrailRenderers[emitterIndex] != null)
            {
                worldCandidate = _rearDriftTrailRenderers[emitterIndex].transform.position;
                return true;
            }

            worldCandidate = Vector3.zero;
            return false;
        }

        public void ConfigureFireTailsTimings(float trailLifetime, float trailFadeTime)
        {
            SetTrailTime(_rearFireDriftTrailRenderers, trailLifetime);
            ApplyDriftTrailFadeGradient(_rearFireDriftTrailRenderers, trailLifetime, trailFadeTime);

            if (_fireDriftEmitters != null)
            {
                for (int i = 0; i < _fireDriftEmitters.Length; i++)
                {
                    if (_fireDriftEmitters[i] != null && _fireDriftEmitters[i].Template != null)
                    {
                        _fireDriftEmitters[i].Pool.UpdateTimingAndGradient(trailLifetime, _fireDriftEmitters[i].Template.colorGradient);
                    }
                }
            }
        }

        public void SetFireTailsActive(bool isActive)
        {
            _isFireTailsActive = isActive;
            if (isActive)
            {
                if (!_isFireTailsFrozen)
                {
                    SetParticleSystemsPlaying(_rearWheelConstantFireParticles, true);
                }

                EndDriftSegments(_driftEmitters);
            }
            else
            {
                SetParticleSystemsPlaying(_rearWheelConstantFireParticles, false);
                SetParticleSystemsPlaying(_rearDriftFireTrailParticles, false);
                EndDriftSegments(_fireDriftEmitters);
            }
        }

        public void SetFireTailsFrozen(bool isFrozen)
        {
            _isFireTailsFrozen = isFrozen;
            if (isFrozen)
            {
                PauseParticleSystems(_rearWheelConstantFireParticles);
                PauseParticleSystems(_rearDriftFireTrailParticles);
            }
            else
            {
                if (_isFireTailsActive)
                {
                    ResumeParticleSystems(_rearWheelConstantFireParticles);
                    if (_carController != null && _carController.IsGrounded && _carController.IsDrifting)
                    {
                        ResumeParticleSystems(_rearDriftFireTrailParticles);
                    }
                }
            }
        }

        private void Awake()
        {
            _carController = GetComponent<ICarController>();
            SetTrailEmitting(_rearDriftTrailRenderers, false);
            SetTrailEmitting(_rearFireDriftTrailRenderers, false);
            ClearParticleSystems(_rearWheelConstantFireParticles);
            ClearParticleSystems(_rearDriftFireTrailParticles);
        }

        private void OnEnable()
        {
            _carController.OnBrakePress += CarController_OnBrakePress;
            _carController.OnBrakeRelease += CarController_OnBrakeRelease;
            _carController.OnDriftStop += CarController_OnDriftStop;
            if (_isInitialized)
            {
                StartSpeedTrailPolling();
            }
        }

        private void Start()
        {
            if (_carMeshRenderer.materials != null)
            {
                foreach (Material mat in _carMeshRenderer.materials)
                {
                    if (mat != null && mat.name.StartsWith(CarVfxConstants.CAR_STOP_LIGHTS_MAT_NAME))
                    {
                        _carStopLightsMat = mat;
                        break;
                    }
                }
            }

            SetTrailTime(_rearTrailRenderers, _trailDisappearingSpeed);
            SetTrailTime(_frontTrailRenderers, _trailDisappearingSpeed);
            SetTrailTime(_rearDriftTrailRenderers, _driftTrailLifetime);
            ApplyDriftTrailFadeGradient(_rearDriftTrailRenderers, _driftTrailLifetime, _driftTrailFadeTime);

            SetTrailEmitting(_rearTrailRenderers, false);
            SetTrailEmitting(_frontTrailRenderers, false);
            SetTrailEmitting(_rearDriftTrailRenderers, false);
            SetTrailEmitting(_rearFireDriftTrailRenderers, false);

            _driftEmitters = new DriftEmitter[_rearDriftTrailRenderers == null ? 0 : _rearDriftTrailRenderers.Length];
            for (int i = 0; i < _driftEmitters.Length; i++)
            {
                if (_rearDriftTrailRenderers[i] != null)
                {
                    _driftEmitters[i] = new DriftEmitter(_rearDriftTrailRenderers[i]);
                }
            }

            _fireDriftEmitters = new DriftEmitter[_rearFireDriftTrailRenderers == null ? 0 : _rearFireDriftTrailRenderers.Length];
            for (int i = 0; i < _fireDriftEmitters.Length; i++)
            {
                if (_rearFireDriftTrailRenderers[i] != null)
                {
                    _fireDriftEmitters[i] = new DriftEmitter(_rearFireDriftTrailRenderers[i]);
                }
            }

            _isInitialized = true;
            StartSpeedTrailPolling();
        }

        private void OnDisable()
        {
            _carController.OnBrakePress -= CarController_OnBrakePress;
            _carController.OnBrakeRelease -= CarController_OnBrakeRelease;
            _carController.OnDriftStop -= CarController_OnDriftStop;
            CancelInvoke(nameof(ActivateSpeedTrailWhenSpeedExceedsThreshold));
            SetTrailEmitting(_rearTrailRenderers, false);
            SetTrailEmitting(_frontTrailRenderers, false);
            EndDriftSegments();
            SetParticleSystemsPlaying(_rearWheelConstantFireParticles, false);
            SetParticleSystemsPlaying(_rearDriftFireTrailParticles, false);
        }

        private void LateUpdate()
        {
            if (!_isInitialized || Time.deltaTime <= 0f)
            {
                return;
            }

            float scaledTime = Time.time;
            if (_driftEmitters != null)
            {
                for (int i = 0; i < _driftEmitters.Length; i++)
                {
                    DriftEmitter emitter = _driftEmitters[i];
                    if (emitter != null)
                    {
                        emitter.Pool.TickExpiry(scaledTime);
                    }
                }
            }

            if (_fireDriftEmitters != null)
            {
                for (int i = 0; i < _fireDriftEmitters.Length; i++)
                {
                    DriftEmitter emitter = _fireDriftEmitters[i];
                    if (emitter != null)
                    {
                        emitter.Pool.TickExpiry(scaledTime);
                    }
                }
            }

            bool isGroundedAndDrifting = _carController != null && _carController.IsGrounded && _carController.IsDrifting;

            if (_isFireTailsActive)
            {
                EndDriftSegments(_driftEmitters);

                if (_fireDriftEmitters != null)
                {
                    for (int i = 0; i < _fireDriftEmitters.Length; i++)
                    {
                        DriftEmitter emitter = _fireDriftEmitters[i];
                        if (emitter == null)
                        {
                            continue;
                        }

                        if (emitter.Template == null || !emitter.Template.gameObject.activeInHierarchy || !isGroundedAndDrifting)
                        {
                            EndDriftSegment(emitter, scaledTime);
                            continue;
                        }

                        UpdateDriftEmitter(emitter, scaledTime);
                    }
                }

                if (isGroundedAndDrifting && !_isFireTailsFrozen)
                {
                    SetParticleSystemsPlaying(_rearDriftFireTrailParticles, true);
                }
                else
                {
                    SetParticleSystemsPlaying(_rearDriftFireTrailParticles, false);
                }
            }
            else
            {
                EndDriftSegments(_fireDriftEmitters);
                SetParticleSystemsPlaying(_rearDriftFireTrailParticles, false);

                if (_driftEmitters != null)
                {
                    for (int i = 0; i < _driftEmitters.Length; i++)
                    {
                        DriftEmitter emitter = _driftEmitters[i];
                        if (emitter == null)
                        {
                            continue;
                        }

                        if (emitter.Template == null || !emitter.Template.gameObject.activeInHierarchy || !isGroundedAndDrifting)
                        {
                            EndDriftSegment(emitter, scaledTime);
                            continue;
                        }

                        UpdateDriftEmitter(emitter, scaledTime);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            DisposeEmitters(_driftEmitters);
            DisposeEmitters(_fireDriftEmitters);
            _driftEmitters = null;
            _fireDriftEmitters = null;
        }

        private void DisposeEmitters(DriftEmitter[] emitters)
        {
            if (emitters == null)
            {
                return;
            }

            for (int i = 0; i < emitters.Length; i++)
            {
                if (emitters[i] != null)
                {
                    emitters[i].Pool.Dispose();
                }
            }
        }

        private void StartSpeedTrailPolling()
        {
            InvokeRepeating(nameof(ActivateSpeedTrailWhenSpeedExceedsThreshold),
                CarVfxConstants.SPEED_CHECK_FOR_TRAIL_DELAY, CarVfxConstants.SPEED_CHECK_FOR_TRAIL_DELAY);
        }

        private void UpdateDriftEmitter(DriftEmitter emitter, float scaledTime)
        {
            Vector3 candidate = emitter.ContactParent == null
                ? emitter.LocalContactPosition
                : emitter.ContactParent.TransformPoint(emitter.LocalContactPosition);
            if (!_carController.TryGetTireGroundContact(candidate, out RaycastHit hit))
            {
                EndDriftSegment(emitter, scaledTime);
                return;
            }

            Vector3 supportedPoint = GetDriftSurfacePoint(hit);
            int sampleCount = 0;
            if (emitter.HasContinuity && !TrySampleContinuity(emitter, candidate, out sampleCount))
            {
                EndDriftSegment(emitter, scaledTime);
            }

            if (!emitter.HasContinuity)
            {
                if (!emitter.Pool.TryBeginSegment(supportedPoint))
                {
                    return;
                }

                emitter.HasContinuity = true;
            }
            else
            {
                // Validate the whole interval before adding any of its points to the old strip.
                for (int i = 0; i < sampleCount; i++)
                {
                    emitter.Pool.AppendPoint(emitter.ContinuityPoints[i]);
                }

                emitter.Pool.AppendPoint(supportedPoint);
            }

            emitter.PreviousCandidate = candidate;
        }

        private bool TrySampleContinuity(DriftEmitter emitter, Vector3 candidate, out int sampleCount)
        {
            sampleCount = 0;
            float distance = Vector3.Distance(emitter.PreviousCandidate, candidate);
            if (float.IsNaN(distance) || float.IsInfinity(distance)
                || distance > (CarVfxConstants.DRIFT_MAX_CONTINUITY_SAMPLES + 1)
                    * CarVfxConstants.DRIFT_CONTINUITY_SAMPLE_SPACING)
            {
                return false;
            }

            int intervals = Mathf.Max(1, Mathf.CeilToInt(distance / CarVfxConstants.DRIFT_CONTINUITY_SAMPLE_SPACING));
            sampleCount = intervals - 1;
            if (sampleCount > CarVfxConstants.DRIFT_MAX_CONTINUITY_SAMPLES)
            {
                return false;
            }

            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 sample = Vector3.Lerp(emitter.PreviousCandidate, candidate, (i + 1f) / intervals);
                if (!_carController.TryGetTireGroundContact(sample, out RaycastHit hit))
                {
                    return false;
                }

                emitter.ContinuityPoints[i] = GetDriftSurfacePoint(hit);
            }

            return true;
        }

        private Vector3 GetDriftSurfacePoint(RaycastHit hit)
        {
            return hit.point + Vector3.up * CarVfxConstants.DRIFT_GROUND_SURFACE_OFFSET;
        }

        private void EndDriftSegment(DriftEmitter emitter, float scaledTime)
        {
            emitter.Pool.EndSegment(scaledTime);
            emitter.HasContinuity = false;
        }

        private void EndDriftSegments(DriftEmitter[] emitters)
        {
            if (emitters == null)
            {
                return;
            }

            for (int i = 0; i < emitters.Length; i++)
            {
                if (emitters[i] != null)
                {
                    EndDriftSegment(emitters[i], Time.time);
                }
            }
        }

        private void EndDriftSegments()
        {
            EndDriftSegments(_driftEmitters);
            EndDriftSegments(_fireDriftEmitters);
            SetParticleSystemsPlaying(_rearDriftFireTrailParticles, false);
        }

        private void SetParticleSystemsPlaying(ParticleSystem[] particleSystems, bool isPlaying)
        {
            if (particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null)
                {
                    continue;
                }

                if (isPlaying)
                {
                    if (!ps.isEmitting)
                    {
                        ps.Play();
                    }
                }
                else
                {
                    if (ps.isEmitting)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }
        }

        private void ClearParticleSystems(ParticleSystem[] particleSystems)
        {
            if (particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void PauseParticleSystems(ParticleSystem[] particleSystems)
        {
            if (particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps != null && ps.isPlaying)
                {
                    ps.Pause();
                }
            }
        }

        private void ResumeParticleSystems(ParticleSystem[] particleSystems)
        {
            if (particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps != null && ps.isPaused)
                {
                    ps.Play();
                }
            }
        }

        private void CarController_OnBrakePress(object sender, EventArgs e)
        {
            _carStopLightsMat?.SetFloat("IsGlowing", 1f);
            _carBackLightsHolder.SetActive(true);
        }

        private void CarController_OnBrakeRelease(object sender, EventArgs e)
        {
            _carStopLightsMat?.SetFloat("IsGlowing", 0f);
            _carBackLightsHolder.SetActive(false);
        }

        private void CarController_OnDriftStop(object sender, EventArgs e)
        {
            EndDriftSegments();
        }

        private void ActivateSpeedTrailWhenSpeedExceedsThreshold()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (!_carController.IsGrounded)
            {
                SetTrailEmitting(_rearTrailRenderers, false);
                SetTrailEmitting(_frontTrailRenderers, false);
                return;
            }

            if (_carController.IsDrifting)
            {
                SetTrailEmitting(_rearTrailRenderers, false);
                SetTrailEmitting(_frontTrailRenderers, false);
                return;
            }

            Vector3 velocity = _carController.GetMovementVelocity();
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            float activeThreshold = Mathf.Max(0.1f, _thresholdToStartSpeedTrail);

            if (forwardSpeed >= activeThreshold)
            {
                SetTrailEmitting(_rearTrailRenderers, true);
                SetTrailEmitting(_frontTrailRenderers, false);
            }
            else if (forwardSpeed <= -activeThreshold)
            {
                SetTrailEmitting(_rearTrailRenderers, false);
                SetTrailEmitting(_frontTrailRenderers, true);
            }
            else
            {
                SetTrailEmitting(_rearTrailRenderers, false);
                SetTrailEmitting(_frontTrailRenderers, false);
            }
        }

        private void SetTrailTime(TrailRenderer[] trailRenderers, float timeSeconds)
        {
            if (trailRenderers == null)
            {
                return;
            }

            foreach (var trailRenderer in trailRenderers)
            {
                if (trailRenderer != null)
                {
                    trailRenderer.time = timeSeconds;
                }
            }
        }

        private void ApplyDriftTrailFadeGradient(TrailRenderer[] trailRenderers, float lifetime, float fadeTime)
        {
            if (trailRenderers == null)
            {
                return;
            }

            float safeLifetime = Mathf.Max(0.01f, lifetime);
            float safeFadeTime = Mathf.Clamp(fadeTime, 0f, safeLifetime);
            float fadeStartRatio = 1f - (safeFadeTime / safeLifetime);

            foreach (var trailRenderer in trailRenderers)
            {
                if (trailRenderer == null)
                {
                    continue;
                }

                Gradient existingGradient = trailRenderer.colorGradient;
                GradientColorKey[] colorKeys = existingGradient != null && existingGradient.colorKeys != null && existingGradient.colorKeys.Length > 0
                    ? existingGradient.colorKeys
                    : new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) };

                GradientAlphaKey[] alphaKeys;
                if (fadeStartRatio >= 0.999f)
                {
                    alphaKeys = new[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(1.0f, 1.0f)
                    };
                }
                else if (fadeStartRatio <= 0.001f)
                {
                    alphaKeys = new[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(0.0f, 1.0f)
                    };
                }
                else
                {
                    alphaKeys = new[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(1.0f, fadeStartRatio),
                        new GradientAlphaKey(0.0f, 1.0f)
                    };
                }

                Gradient gradient = new Gradient();
                gradient.SetKeys(colorKeys, alphaKeys);
                trailRenderer.colorGradient = gradient;
            }
        }

        private void SetTrailEmitting(TrailRenderer[] trailRenderers, bool isEmitting)
        {
            if (trailRenderers == null)
            {
                return;
            }

            foreach (var trailRenderer in trailRenderers)
            {
                if (trailRenderer != null)
                {
                    trailRenderer.emitting = isEmitting;
                }
            }
        }
    }
}
