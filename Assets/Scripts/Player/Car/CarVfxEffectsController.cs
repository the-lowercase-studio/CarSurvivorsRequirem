using Assets.Scripts.Player.Constants;
using System;
using UnityEngine;

namespace Assets.Scripts.Player.Car
{
    [RequireComponent(typeof(CarController))]
    public class CarVfxEffectsController : MonoBehaviour
    {
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

        private void Awake()
        {
            _carController = GetComponent<ICarController>();
        }

        private void OnEnable()
        {
            _carController.OnBrakePress += CarController_OnBrakePress;
            _carController.OnBrakeRelease += CarController_OnBrakeRelease;
            _carController.OnDriftStart += CarController_OnDriftStart;
            _carController.OnDriftStop += CarController_OnDriftStop;
            _carController.OnDriftDirectionChanged += CarController_OnDriftDirectionChanged;
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

            InvokeRepeating(
                nameof(ActivateSpeedTrailWhenSpeedExceedsThreshold),
                CarVfxConstants.SPEED_CHECK_FOR_TRAIL_DELAY,
                CarVfxConstants.SPEED_CHECK_FOR_TRAIL_DELAY
            );

            SetTrailTime(_rearTrailRenderers, _trailDisappearingSpeed);
            SetTrailTime(_frontTrailRenderers, _trailDisappearingSpeed);
            SetTrailTime(_rearDriftTrailRenderers, _driftTrailLifetime);
            ApplyDriftTrailFadeGradient(_rearDriftTrailRenderers, _driftTrailLifetime, _driftTrailFadeTime);

            SetTrailEmitting(_rearTrailRenderers, false);
            SetTrailEmitting(_frontTrailRenderers, false);
            SetTrailEmitting(_rearDriftTrailRenderers, false);
        }

        private void OnDisable()
        {
            _carController.OnBrakePress -= CarController_OnBrakePress;
            _carController.OnBrakeRelease -= CarController_OnBrakeRelease;
            _carController.OnDriftStart -= CarController_OnDriftStart;
            _carController.OnDriftStop -= CarController_OnDriftStop;
            _carController.OnDriftDirectionChanged -= CarController_OnDriftDirectionChanged;
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

        private void CarController_OnDriftStart(object sender, EventArgs e)
        {
            UpdateDriftTrails();
        }

        private void CarController_OnDriftStop(object sender, EventArgs e)
        {
            UpdateDriftTrails();
        }

        private void CarController_OnDriftDirectionChanged(object sender, int driftDirection)
        {
            UpdateDriftTrails();
        }

        private void UpdateDriftTrails()
        {
            bool isEmitting = _carController.IsGrounded && _carController.IsDrifting;
            SetTrailEmitting(_rearDriftTrailRenderers, isEmitting);
        }

        private void ActivateSpeedTrailWhenSpeedExceedsThreshold()
        {
            if (!_carController.IsGrounded)
            {
                SetTrailEmitting(_rearTrailRenderers, false);
                SetTrailEmitting(_frontTrailRenderers, false);
                SetTrailEmitting(_rearDriftTrailRenderers, false);
                return;
            }

            if (_carController.IsDrifting)
            {
                SetTrailEmitting(_rearTrailRenderers, false);
                SetTrailEmitting(_frontTrailRenderers, false);
                UpdateDriftTrails();
                return;
            }

            UpdateDriftTrails();

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
