using Assets.Scripts.Player.Constants;
using System;
using UnityEngine;

namespace Assets.Scripts.Player.Car
{
    [RequireComponent(typeof(CarController))]
    public class CarVfxEffectsController : MonoBehaviour
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

        private ICarController _carController;
        private Material _carStopLightsMat;
        private DriftEmitter[] _driftEmitters;
        private bool _isInitialized;

        private void Awake()
        {
            _carController = GetComponent<ICarController>();
            SetTrailEmitting(_rearDriftTrailRenderers, false);
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
            _driftEmitters = new DriftEmitter[_rearDriftTrailRenderers == null ? 0 : _rearDriftTrailRenderers.Length];
            for (int i = 0; i < _driftEmitters.Length; i++)
            {
                if (_rearDriftTrailRenderers[i] != null)
                {
                    _driftEmitters[i] = new DriftEmitter(_rearDriftTrailRenderers[i]);
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
        }

        private void LateUpdate()
        {
            if (!_isInitialized || Time.deltaTime <= 0f)
            {
                return;
            }

            float scaledTime = Time.time;
            for (int i = 0; i < _driftEmitters.Length; i++)
            {
                DriftEmitter emitter = _driftEmitters[i];
                if (emitter == null)
                {
                    continue;
                }

                emitter.Pool.TickExpiry(scaledTime);
                if (emitter.Template == null || !emitter.Template.gameObject.activeInHierarchy
                    || !_carController.IsGrounded || !_carController.IsDrifting)
                {
                    EndDriftSegment(emitter, scaledTime);
                    continue;
                }

                UpdateDriftEmitter(emitter, scaledTime);
            }
        }

        private void OnDestroy()
        {
            if (_driftEmitters == null)
            {
                return;
            }

            for (int i = 0; i < _driftEmitters.Length; i++)
            {
                if (_driftEmitters[i] != null)
                {
                    _driftEmitters[i].Pool.Dispose();
                }
            }

            _driftEmitters = null;
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

        private void EndDriftSegments()
        {
            if (_driftEmitters == null)
            {
                return;
            }

            for (int i = 0; i < _driftEmitters.Length; i++)
            {
                if (_driftEmitters[i] != null)
                {
                    EndDriftSegment(_driftEmitters[i], Time.time);
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
