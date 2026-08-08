using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Player.Car
{
    public interface ICarController
    {
        event EventHandler OnBrakePress;

        event EventHandler OnBrakeRelease;

        float GetMovementSpeed();

        Vector3 GetMovementVelocity();
    }

    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour, ICarController
    {
        private enum Axel
        {
            Front,
            Rear
        }

        [Serializable]
        private struct Wheel
        {
            [Tooltip("Model 3D koła w hierarchii Transform (używany do animacji obrotu i skrętu).")]
            [SerializeField] private GameObject _wheelModel;

            [Tooltip("Oś przednia (Front - skręca i obraca) lub tylna (Rear - tylko obraca).")]
            [SerializeField] private Axel _axel;

            public GameObject WheelModel => _wheelModel;
            public Axel Axel => _axel;
        }

        [Header("Engine & Acceleration")]
        [Tooltip("Maksymalna prędkość liniowa jazdy do przodu w m/s (np. 16.0).")]
        [SerializeField] private float _maxSpeed = 16f;

        [Tooltip("Szybkość przyspieszania do przodu w m/s². Niższa wartość (8-12) daje płynniejszy przyrost prędkości, wyższa (20-25) daje natychmiastowy zryw.")]
        [SerializeField] private float _acceleration = 25f;

        [Tooltip("Maksymalna prędkość cofania do tyłu w m/s (np. 8.0).")]
        [SerializeField] private float _reverseMaxSpeed = 8f;

        [Tooltip("Szybkość przyspieszania do tyłu w m/s². Pozwala na osobne, płynne rozpędzanie się na biegu wstecznym.")]
        [SerializeField] private float _reverseAcceleration = 12f;

        [Tooltip("Siła hamowania przy przytrzymaniu przycisku hamulca w m/s². Wyższa wartość = szybsze zatrzymanie auta.")]
        [SerializeField] private float _brakeDeceleration = 40f;

        [Tooltip("Wytracanie prędkości po puszczeniu gazu i hamulca w m/s². Niższa wartość = dłuższe toczenie się auta.")]
        [SerializeField] private float _naturalDeceleration = 15f;

        [Header("Steering & Responsiveness")]
        [Tooltip("Szybkość obrotu auta w stopniach na sekundę. Wartości 90-110 dają płynniejsze, szersze łuki przy dużej prędkości.")]
        [SerializeField] private float _turnSpeed = 160f;

        [Tooltip("Maksymalny kąt skrętu wizualnych kół przedniej osi w stopniach.")]
        [SerializeField] private float _maxSteerAngle = 30f;

        [Tooltip("Szybkość reakcji kierownicy na naciśnięcie/puszczenie klawisza. Wygładza wejście, eliminuje szarpnięcia (sugerowane: 8-12).")]
        [Range(1f, 50f)]
        [SerializeField] private float _steerResponseSpeed = 10f;

        [Header("Arcade Grip & Drift")]
        [Tooltip("Przyczepność boczna przy zwykłej jeździe (0 = poślizg/lodowisko, 1 = idealna przyczepność). Sugerowane: 0.75 - 0.85.")]
        [Range(0f, 1f)]
        [SerializeField] private float _normalGrip = 0.90f;

        [Tooltip("Przyczepność boczna podczas driftu po wciśnięciu hamulca w zakręcie. Niższa wartość = dłuższego uślizgu tyłu.")]
        [Range(0f, 1f)]
        [SerializeField] private float _driftGrip = 0.25f;

        [Tooltip("Minimalna prędkość auta (m/s) wymagana do wejścia w stan driftu.")]
        [SerializeField] private float _minSpeedToDrift = 3f;

        [Tooltip("Mnożnik prędkości skrętu w drifcie. Pozwala na ciasne i dynamiczne nawroty przy hamulcu ręcznym.")]
        [SerializeField] private float _driftTurnMultiplier = 1.3f;

        [Header("Visual Wheels")]
        [Tooltip("Lista kół auta wraz z ich modelami 3D oraz oznaczeniem osi (Przednia/Tylna).")]
        [SerializeField] private List<Wheel> _wheels;

        [Tooltip("Promień wizualny kół w metrach, używany do wyliczania prędkości obrotowej modeli kół.")]
        [SerializeField] private float _wheelVisualRadius = 0.35f;

        [Header("Ground Check & Raycasts")]
        [Tooltip("Długość promieni raycast skierowanych w dół z kół do szukania płaszczyzny terenu.")]
        [SerializeField] private float _groundCheckDistance = 2.0f;

        [Tooltip("Offset wysokości Y podnoszący środek auta nad terenem (używany do dostrojenia zawieszenia).")]
        [SerializeField] private float _groundTargetYOffset = 0.0f;

        [Tooltip("Wysokość punktu startowego promieni raycast liczona od środka obiektu.")]
        [SerializeField] private float _raycastOriginYOffset = 1.0f;

        [Tooltip("Maska warstw fizycznych uznawanych za przejezdny teren.")]
        [SerializeField] private LayerMask _groundLayerMask;

        [Header("Physics Stability")]
        [Tooltip("Lokalny środek ciężkości Rigidbody. Obniżona wartość Y (np. -0.5) zapobiega dachowaniu i poprawia stabilność.")]
        [SerializeField] private Vector3 _centerOfMass = new Vector3(0f, -0.5f, 0f);

        private Rigidbody _rb;
        private InputAction _moveAction;
        private Vector2 _moveInput;
        private InputAction _brakeAction;
        private bool _brakeInput;
        private bool _isDrifting;
        private bool _isGrounded;
        private float _currentForwardSpeed;
        private float _smoothedSteerInput;
        private float _currentVisualSteerAngle;
        private float _visualWheelSpinAngle;
        private readonly List<Vector3> _raycastOriginsCache = new List<Vector3>();

        public event EventHandler OnBrakePress;
        public event EventHandler OnBrakeRelease;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _moveAction = InputSystem.actions.FindAction("Move");
            _brakeAction = InputSystem.actions.FindAction("Brake");

            _rb.centerOfMass = _centerOfMass;

            if (_groundLayerMask.value == 0)
            {
                _groundLayerMask = Assets.Scripts.LayerMasks.TerrainLayers.All;
            }
        }

        private void OnEnable()
        {
            if (_brakeAction != null)
            {
                _brakeAction.started += BrakeAction_Started;
                _brakeAction.canceled += BrakeAction_Canceled;
            }
        }

        private void OnDisable()
        {
            if (_brakeAction != null)
            {
                _brakeAction.started -= BrakeAction_Started;
                _brakeAction.canceled -= BrakeAction_Canceled;
            }
            _isDrifting = false;
        }

        private void Update()
        {
            if (_moveAction != null)
            {
                _moveInput = _moveAction.ReadValue<Vector2>();
            }

            if (_brakeAction != null)
            {
                _brakeInput = _brakeAction.IsPressed();
            }

            UpdateDriftState();
            AnimateWheelsVisuals();
        }

        private void FixedUpdate()
        {
            _rb.angularVelocity = Vector3.zero;

            HandleRaycastGrounding();
            HandleArcadeMovement();
            HandleArcadeSteering();
        }

        private LayerMask GetEffectiveGroundLayerMask()
        {
            if (_groundLayerMask.value != 0)
            {
                return _groundLayerMask;
            }
            return Assets.Scripts.LayerMasks.TerrainLayers.All;
        }

        private float GetEffectiveRaycastOriginYOffset()
        {
            return Mathf.Max(_raycastOriginYOffset, 0.5f);
        }

        private float GetEffectiveGroundCheckDistance()
        {
            return Mathf.Max(_groundCheckDistance, 2.0f);
        }

        private float GetEffectiveGroundTargetYOffset()
        {
            if (_groundTargetYOffset < -0.1f)
            {
                return 0.0f;
            }
            return _groundTargetYOffset;
        }

        private void OnDrawGizmos()
        {
            List<Vector3> origins = GetWheelRaycastOrigins();
            LayerMask mask = GetEffectiveGroundLayerMask();
            float checkDistance = GetEffectiveGroundCheckDistance();

            foreach (Vector3 origin in origins)
            {
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hitInfo, checkDistance, mask))
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(origin, hitInfo.point);
                    Gizmos.DrawWireSphere(hitInfo.point, 0.05f);
                }
                else
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(origin, origin + Vector3.down * checkDistance);
                }
            }
        }

        public float GetMovementSpeed()
        {
            Vector3 velocityXZ = _rb.linearVelocity;
            velocityXZ.y = 0f;
            return velocityXZ.magnitude;
        }

        public Vector3 GetMovementVelocity()
        {
            Vector3 velocity = _rb.linearVelocity;
            velocity.y = 0f;
            return velocity;
        }

        private float GetRequiredPivotHeightAboveGround()
        {
            float lowestWheelYOffset = 0f;

            if (_wheels != null && _wheels.Count > 0)
            {
                foreach (var wheel in _wheels)
                {
                    if (wheel.WheelModel != null)
                    {
                        float localY = transform.InverseTransformPoint(wheel.WheelModel.transform.position).y;
                        if (localY < lowestWheelYOffset)
                        {
                            lowestWheelYOffset = localY;
                        }
                    }
                }
            }

            if (Mathf.Approximately(lowestWheelYOffset, 0f))
            {
                lowestWheelYOffset = -0.375f;
            }

            return Mathf.Abs(lowestWheelYOffset) + _wheelVisualRadius;
        }

        private void HandleRaycastGrounding()
        {
            List<Vector3> raycastOrigins = GetWheelRaycastOrigins();
            bool hasHitGround = false;
            float highestGroundY = float.MinValue;
            LayerMask mask = GetEffectiveGroundLayerMask();
            float checkDistance = GetEffectiveGroundCheckDistance();

            foreach (Vector3 origin in raycastOrigins)
            {
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hitInfo, checkDistance, mask))
                {
                    hasHitGround = true;
                    if (hitInfo.point.y > highestGroundY)
                    {
                        highestGroundY = hitInfo.point.y;
                    }
                }
            }

            _isGrounded = hasHitGround;

            if (_isGrounded)
            {
                float pivotHeight = GetRequiredPivotHeightAboveGround();
                Vector3 currentPosition = _rb.position;
                float targetY = highestGroundY + pivotHeight + GetEffectiveGroundTargetYOffset();

                currentPosition.y = Mathf.MoveTowards(currentPosition.y, targetY, 25f * Time.fixedDeltaTime);
                _rb.position = currentPosition;

                Vector3 currentVelocity = _rb.linearVelocity;
                currentVelocity.y = 0f;
                _rb.linearVelocity = currentVelocity;
            }
        }

        private List<Vector3> GetWheelRaycastOrigins()
        {
            _raycastOriginsCache.Clear();
            float originYOffset = GetEffectiveRaycastOriginYOffset();

            if (_wheels != null && _wheels.Count > 0)
            {
                foreach (var wheel in _wheels)
                {
                    if (wheel.WheelModel != null)
                    {
                        Vector3 wheelPos = wheel.WheelModel.transform.position;
                        _raycastOriginsCache.Add(new Vector3(wheelPos.x, transform.position.y + originYOffset, wheelPos.z));
                    }
                }
            }

            if (_raycastOriginsCache.Count == 0)
            {
                Vector3 center = transform.position + Vector3.up * originYOffset;
                _raycastOriginsCache.Add(center + transform.forward * 1f + transform.right * 0.8f);
                _raycastOriginsCache.Add(center + transform.forward * 1f - transform.right * 0.8f);
                _raycastOriginsCache.Add(center - transform.forward * 1f + transform.right * 0.8f);
                _raycastOriginsCache.Add(center - transform.forward * 1f - transform.right * 0.8f);
            }

            return _raycastOriginsCache;
        }

        private void HandleArcadeMovement()
        {
            Vector3 currentVelocity = _rb.linearVelocity;

            Vector3 localVelocity = transform.InverseTransformDirection(currentVelocity);
            _currentForwardSpeed = localVelocity.z;

            float inputY = _moveInput.y;
            float targetForwardSpeed;

            if (_brakeInput)
            {
                targetForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, 0f, _brakeDeceleration * Time.fixedDeltaTime);
            }
            else if (inputY > 0.05f)
            {
                float maxTargetSpeed = _maxSpeed * inputY;
                targetForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, maxTargetSpeed, _acceleration * Time.fixedDeltaTime);
            }
            else if (inputY < -0.05f)
            {
                float maxTargetSpeed = _reverseMaxSpeed * inputY;
                targetForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, maxTargetSpeed, _reverseAcceleration * Time.fixedDeltaTime);
            }
            else
            {
                targetForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, 0f, _naturalDeceleration * Time.fixedDeltaTime);
            }

            _currentForwardSpeed = targetForwardSpeed;

            float currentGrip = _isDrifting ? _driftGrip : _normalGrip;
            float targetLateralSpeed = localVelocity.x * (1f - currentGrip);

            Vector3 targetLocalVelocity = new Vector3(targetLateralSpeed, 0f, _currentForwardSpeed);
            Vector3 targetWorldHorizontalVelocity = transform.TransformDirection(targetLocalVelocity);

            Vector3 currentWorldHorizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            Vector3 velocityChange = targetWorldHorizontalVelocity - currentWorldHorizontalVelocity;

            _rb.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        private void HandleArcadeSteering()
        {
            _smoothedSteerInput = Mathf.MoveTowards(_smoothedSteerInput, _moveInput.x, _steerResponseSpeed * Time.fixedDeltaTime);

            if (Mathf.Abs(_smoothedSteerInput) < 0.001f)
            {
                return;
            }

            float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
            float directionSign = 1f;

            if (_moveInput.y < -0.1f || (forwardSpeed < -0.5f && _moveInput.y <= 0f))
            {
                directionSign = -1f;
            }

            float turnMultiplier = _isDrifting ? _driftTurnMultiplier : 1.0f;
            float turnAmount = _smoothedSteerInput * _turnSpeed * turnMultiplier * directionSign * Time.fixedDeltaTime;

            Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
            _rb.MoveRotation(_rb.rotation * turnRotation);
        }

        private void UpdateDriftState()
        {
            bool canDrift = _brakeInput
                && GetMovementSpeed() >= _minSpeedToDrift
                && _moveInput.y >= 0.1f
                && Mathf.Abs(_smoothedSteerInput) >= 0.2f;

            _isDrifting = canDrift;
        }

        private void AnimateWheelsVisuals()
        {
            if (_wheels == null || _wheels.Count == 0)
            {
                return;
            }

            float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
            float rotationAngleDegrees = (forwardSpeed / Mathf.Max(0.01f, _wheelVisualRadius)) * Mathf.Rad2Deg * Time.deltaTime;
            _visualWheelSpinAngle = (_visualWheelSpinAngle + rotationAngleDegrees) % 360f;

            float targetSteerAngle = _smoothedSteerInput * _maxSteerAngle;
            _currentVisualSteerAngle = Mathf.Lerp(_currentVisualSteerAngle, targetSteerAngle, Time.deltaTime * 15f);

            Quaternion spinRotation = Quaternion.Euler(_visualWheelSpinAngle, 0f, 0f);

            foreach (var wheel in _wheels)
            {
                if (wheel.WheelModel == null)
                {
                    continue;
                }

                if (wheel.Axel == Axel.Front)
                {
                    Quaternion steerRotation = Quaternion.Euler(0f, _currentVisualSteerAngle, 0f);
                    wheel.WheelModel.transform.localRotation = steerRotation * spinRotation;
                }
                else
                {
                    wheel.WheelModel.transform.localRotation = spinRotation;
                }
            }
        }

        private void BrakeAction_Started(InputAction.CallbackContext context)
        {
            OnBrakePress?.Invoke(this, EventArgs.Empty);
        }

        private void BrakeAction_Canceled(InputAction.CallbackContext context)
        {
            OnBrakeRelease?.Invoke(this, EventArgs.Empty);
        }
    }
}
