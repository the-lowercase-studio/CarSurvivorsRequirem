using System;
using System.Linq;
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
        [SerializeField] private TrailRenderer[] _trailRenderers;
        [SerializeField] private float _trailDisappearingSpeed = 0.3f;
        [SerializeField] private float _thresholdToStartSpeedTrail;

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
        }

        private void Start()
        {
            _carStopLightsMat = _carMeshRenderer.materials.FirstOrDefault(m => m.name == CAR_STOP_LIGHTS_MAT_NAME);

            InvokeRepeating(
                nameof(ActivateSpeedTrailWhenSpeedExceedsThreshold),
                SPEED_CHECK_FOR_TRAIL_DELAY,
                SPEED_CHECK_FOR_TRAIL_DELAY
            );

            SetTrailsDisappearingSpeed(_trailDisappearingSpeed);

            SetTrailEmitting(false);
        }

        private void OnDisable()
        {
            _carController.OnBrakePress -= CarController_OnBrakePress;
            _carController.OnBrakeRelease -= CarController_OnBrakeRelease;
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

        private void ActivateSpeedTrailWhenSpeedExceedsThreshold()
        {
            if (_thresholdToStartSpeedTrail <= _carController.GetMovementSpeed())
            {
                SetTrailEmitting(true);
            }
            else
            {
                SetTrailEmitting(false);
            }
        }

        private void SetTrailsDisappearingSpeed(float speed)
        {
            foreach (var trailRenderer in _trailRenderers)
            {
                trailRenderer.time = speed;
            }
        }

        private void SetTrailEmitting(bool isEmitting)
        {
            foreach (var trailRenderer in _trailRenderers)
            {
                trailRenderer.emitting = isEmitting;
            }
        }
    }
}
