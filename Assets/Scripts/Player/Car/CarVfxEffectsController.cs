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

        [Header("Car Drift Effect")]
        [Tooltip("Efekty śladów/linii driftu dla lewej strony (aktywne przy drifcie w lewo, np. 2 TrailRenderery na przednie i tylne koło).")]
        [SerializeField] private TrailRenderer[] _leftDriftTrailRenderers;

        [Tooltip("Efekty śladów/linii driftu dla prawej strony (aktywne przy drifcie w prawo, np. 2 TrailRenderery na przednie i tylne koło).")]
        [SerializeField] private TrailRenderer[] _rightDriftTrailRenderers;

        [Tooltip("Czas znikania śladu prędkości oraz śladów driftu.")]
        [SerializeField] private float _trailDisappearingSpeed = 0.3f;

        [Tooltip("Próg prędkości, po przekroczeniu którego włączają się ślady prędkości.")]
        [SerializeField] private float _thresholdToStartSpeedTrail = 5f;

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
            _carController.OnDriftDirectionChanged += CarController_OnDriftDirectionChanged;
            _carController.OnDriftStop += CarController_OnDriftStop;
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

            SetTrailsDisappearingSpeed(_trailDisappearingSpeed);

            SetTrailEmitting(_rearTrailRenderers, false);
            SetTrailEmitting(_frontTrailRenderers, false);
            SetTrailEmitting(_leftDriftTrailRenderers, false);
            SetTrailEmitting(_rightDriftTrailRenderers, false);
        }

        private void OnDisable()
        {
            _carController.OnBrakePress -= CarController_OnBrakePress;
            _carController.OnBrakeRelease -= CarController_OnBrakeRelease;
            _carController.OnDriftDirectionChanged -= CarController_OnDriftDirectionChanged;
            _carController.OnDriftStop -= CarController_OnDriftStop;
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

        private void CarController_OnDriftDirectionChanged(object sender, int driftDirection)
        {
            UpdateDriftTrails(driftDirection);
        }

        private void CarController_OnDriftStop(object sender, EventArgs e)
        {
            UpdateDriftTrails(0);
        }

        private void UpdateDriftTrails(int driftDirection)
        {
            if (!_carController.IsGrounded)
            {
                SetTrailEmitting(_leftDriftTrailRenderers, false);
                SetTrailEmitting(_rightDriftTrailRenderers, false);
                return;
            }

            if (driftDirection < 0)
            {
                SetTrailEmitting(_leftDriftTrailRenderers, true);
                SetTrailEmitting(_rightDriftTrailRenderers, false);
            }
            else if (driftDirection > 0)
            {
                SetTrailEmitting(_leftDriftTrailRenderers, false);
                SetTrailEmitting(_rightDriftTrailRenderers, true);
            }
            else
            {
                SetTrailEmitting(_leftDriftTrailRenderers, false);
                SetTrailEmitting(_rightDriftTrailRenderers, false);
            }
        }

        private void ActivateSpeedTrailWhenSpeedExceedsThreshold()
        {
            if (!_carController.IsGrounded)
            {
                SetTrailEmitting(_rearTrailRenderers, false);
                SetTrailEmitting(_frontTrailRenderers, false);
                SetTrailEmitting(_leftDriftTrailRenderers, false);
                SetTrailEmitting(_rightDriftTrailRenderers, false);
                return;
            }

            if (_carController.IsDrifting)
            {
                SetTrailEmitting(_rearTrailRenderers, false);
                SetTrailEmitting(_frontTrailRenderers, false);
                UpdateDriftTrails(_carController.DriftDirection);
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

        private void SetTrailsDisappearingSpeed(float speed)
        {
            SetTrailTime(_rearTrailRenderers, speed);
            SetTrailTime(_frontTrailRenderers, speed);
        }

        private void SetTrailTime(TrailRenderer[] trailRenderers, float speed)
        {
            if (trailRenderers == null)
            {
                return;
            }

            foreach (var trailRenderer in trailRenderers)
            {
                if (trailRenderer != null)
                {
                    trailRenderer.time = speed;
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
