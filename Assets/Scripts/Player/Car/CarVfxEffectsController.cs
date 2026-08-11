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
        [Tooltip("Efekty śladów prędkości na tył pojazdu (aktywne przy szybkiej jeździe do przodu).")]
        [SerializeField] private TrailRenderer[] _rearTrailRenderers;

        [Tooltip("Efekty śladów prędkości na przód pojazdu (aktywne przy szybkiej jeździe w tył).")]
        [SerializeField] private TrailRenderer[] _frontTrailRenderers;

        [Tooltip("Czas znikania śladu prędkości.")]
        [SerializeField] private float _trailDisappearingSpeed = 0.3f;

        [Tooltip("Próg prędkości, po przekroczeniu którego włączają się ślady prędkości.")]
        [SerializeField] private float _thresholdToStartSpeedTrail = 5f;

        [Header("Car Drift Effect")]
        [Tooltip("Efekty śladów opon z driftu przypisane do 2 tylnych kół pojazdu (tylne lewe i tylne prawe).")]
        [SerializeField] private TrailRenderer[] _rearDriftTrailRenderers;

        [Tooltip("Czas w sekundach, przez jaki ślady opon z driftu pozostają widoczne na ziemi.")]
        [SerializeField] private float _driftTrailLifetime = 3.0f;

        private const float SPEED_CHECK_FOR_TRAIL_DELAY = 0.1f;
        private const string CAR_STOP_LIGHTS_MAT_NAME = "CarStopLights";

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
            if (_carMeshRenderer != null && _carMeshRenderer.materials != null)
            {
                foreach (Material mat in _carMeshRenderer.materials)
                {
                    if (mat != null && mat.name.StartsWith(CAR_STOP_LIGHTS_MAT_NAME))
                    {
                        _carStopLightsMat = mat;
                        break;
                    }
                }
            }

            InvokeRepeating(
                nameof(ActivateSpeedTrailWhenSpeedExceedsThreshold),
                SPEED_CHECK_FOR_TRAIL_DELAY,
                SPEED_CHECK_FOR_TRAIL_DELAY
            );

            SetTrailTime(_rearTrailRenderers, _trailDisappearingSpeed);
            SetTrailTime(_frontTrailRenderers, _trailDisappearingSpeed);
            SetTrailTime(_rearDriftTrailRenderers, _driftTrailLifetime);

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
