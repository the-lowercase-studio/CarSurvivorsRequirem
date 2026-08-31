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

        event EventHandler OnDriftStart;

        event EventHandler OnDriftStop;

        event EventHandler<int> OnDriftDirectionChanged;

        float GetMovementSpeed();

        Vector3 GetMovementVelocity();

        float MaxForwardSpeed { get; }

        float MaxOverallSpeed { get; }

        bool IsDrifting { get; }

        int DriftDirection { get; }

        float DriftYawAngle { get; }

        bool IsGrounded { get; }
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
            [Tooltip("3D wheel model in the Transform hierarchy (used for spin and steer animations).")]
            [SerializeField] private GameObject _wheelModel;

            [Tooltip("Front axle (Front - steers and spins) or rear axle (Rear - spins only).")]
            [SerializeField] private Axel _axel;

            public GameObject WheelModel
            {
                get
                {
                    return _wheelModel;
                }
            }

            public Axel Axel
            {
                get
                {
                    return _axel;
                }
            }
        }

        [Header("Engine & Acceleration")]
        [Tooltip("Standard maximum forward linear speed in m/s (e.g., 16.0).")]
        [SerializeField] private float _maxForwardSpeed = 16f;

        [Tooltip("Absolute maximum overall speed reachable ONLY during and immediately after drifting in m/s (e.g., 24.0).")]
        [SerializeField] private float _maxOverallSpeed = 24f;

        [Tooltip("Forward acceleration rate during straight driving in m/s². Lower values (8-12) provide smooth acceleration; higher values (20-25) give instant responsiveness.")]
        [SerializeField] private float _acceleration = 25f;

        [Tooltip("Forward acceleration rate while drifting toward _maxOverallSpeed in m/s².")]
        [SerializeField] private float _driftAcceleration = 18f;

        [Tooltip("Rate of bleeding off excess speed after exiting a drift back to _maxForwardSpeed in m/s².")]
        [SerializeField] private float _driftSpeedDecayRate = 5f;

        [Tooltip("Minimum continuous drift duration (in seconds) required to exceed normal speed limit and accelerate toward _maxOverallSpeed (anti-snaking guard).")]
        [SerializeField] private float _minDriftTimeToBoost = 0.25f;

        [Tooltip("Maximum reverse speed in m/s (e.g., 8.0).")]
        [SerializeField] private float _reverseMaxSpeed = 8f;

        [Tooltip("Reverse acceleration rate in m/s² for smooth reversing.")]
        [SerializeField] private float _reverseAcceleration = 12f;

        [Tooltip("Braking deceleration when holding the brake button in m/s². Higher values result in faster stopping.")]
        [SerializeField] private float _brakeDeceleration = 40f;

        [Tooltip("Natural coasting deceleration when neither throttle nor brake is pressed in m/s². Lower values result in longer coasting.")]
        [SerializeField] private float _naturalDeceleration = 15f;

        [Header("Steering & Responsiveness")]
        [Tooltip("Car yaw rotation speed in degrees per second. Values around 90-110 produce wider, smoother arcs at high speed.")]
        [SerializeField] private float _turnSpeed = 160f;

        [Tooltip("Maximum steering angle of visual front wheels in degrees.")]
        [SerializeField] private float _maxSteerAngle = 30f;

        [Tooltip("Steering response speed when pressing/releasing steering inputs. Smooths input and prevents abrupt jerks (recommended: 8-12).")]
        [Range(1f, 50f)]
        [SerializeField] private float _steerResponseSpeed = 10f;

        [Header("Arcade Grip & Drift")]
        [Tooltip("Lateral grip during normal driving (0 = icy slip, 1 = perfect traction). Recommended: 0.75 - 0.85.")]
        [Range(0f, 1f)]
        [SerializeField] private float _normalGrip = 0.90f;

        [Tooltip("Lateral grip during drift when braking into a turn. Lower values produce longer rear slide.")]
        [Range(0f, 1f)]
        [SerializeField] private float _driftGrip = 0.25f;

        [Tooltip("Speed deceleration while drifting in m/s² (maintains fast arcade slides without emergency stopping).")]
        [SerializeField] private float _driftDeceleration = 5f;

        [Tooltip("Minimum vehicle speed in m/s required to initiate a drift.")]
        [SerializeField] private float _minSpeedToDrift = 8f;

        [Tooltip("Turn speed multiplier while drifting. Allows sharp, dynamic handbrake turns.")]
        [SerializeField] private float _driftTurnMultiplier = 1.3f;

        [Header("Initial D Sideways Drift")]
        [Tooltip("Target sideways car body yaw angle during drift in degrees (e.g., 40.0 for pronounced Initial D style slides).")]
        [SerializeField] private float _targetDriftAngle = 40f;

        [Tooltip("Response speed for snapping the car body into the target drift yaw angle.")]
        [SerializeField] private float _driftYawResponseSpeed = 12f;

        [Tooltip("Impact of counter-steering on drift angle (steering into turn deepens drift, counter-steering straightens the car).")]
        [SerializeField] private float _counterSteerImpact = 0.5f;

        [Header("Visual Wheels")]
        [Tooltip("List of car wheels with their 3D models and axle assignments (Front/Rear).")]
        [SerializeField] private List<Wheel> _wheels;

        [Tooltip("Visual wheel radius in meters used to calculate wheel model rotation speed.")]
        [SerializeField] private float _wheelVisualRadius = 0.35f;

        [Header("Ground Check & Raycasts")]
        [Tooltip("Downward raycast distance from wheels to detect ground plane.")]
        [SerializeField] private float _groundCheckDistance = 2.0f;

        [Tooltip("Y height offset to raise the car center above the ground (used for suspension tuning).")]
        [SerializeField] private float _groundTargetYOffset = 0.0f;

        [Tooltip("Raycast origin height offset relative to the object center.")]
        [SerializeField] private float _raycastOriginYOffset = 1.0f;

        [Tooltip("Physics layer mask representing drivable ground.")]
        [SerializeField] private LayerMask _groundLayerMask;

        [Header("Physics Stability")]
        [Tooltip("Local Rigidbody center of mass. Lower Y (e.g., -0.5) prevents rollovers and enhances stability.")]
        [SerializeField] private Vector3 _centerOfMass = new Vector3(0f, -0.5f, 0f);

        private Rigidbody _rb;
        private InputAction _moveAction;
        private Vector2 _moveInput;
        private InputAction _brakeAction;
        private bool _brakeInput;
        private bool _isDrifting;
        private int _driftDirection;
        private float _currentDriftDuration;
        private float _currentDriftYawAngle;
        private float _lastAppliedDriftYaw;
        private bool _isGrounded;
        private float _currentForwardSpeed;
        private float _smoothedSteerInput;
        private float _currentLateralGrip;
        private float _currentTurnMultiplier = 1.0f;
        private float _groundYVelocity;
        private float _currentVisualSteerAngle;
        private float _visualWheelSpinAngle;
        private readonly List<Vector3> _raycastOriginsCache = new List<Vector3>();

        public event EventHandler OnBrakePress;
        public event EventHandler OnBrakeRelease;
        public event EventHandler OnDriftStart;
        public event EventHandler OnDriftStop;
        public event EventHandler<int> OnDriftDirectionChanged;

        public float MaxForwardSpeed
        {
            get { return _maxForwardSpeed; }
        }

        public float MaxOverallSpeed
        {
            get { return _maxOverallSpeed; }
        }

        public bool IsDrifting
        {
            get { return _isDrifting; }
        }

        public int DriftDirection
        {
            get { return _driftDirection; }
        }

        public float DriftYawAngle
        {
            get { return _currentDriftYawAngle; }
        }

        public bool IsGrounded
        {
            get { return _isGrounded; }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _moveAction = InputSystem.actions.FindAction("Move");
            _brakeAction = InputSystem.actions.FindAction("Brake");

            _rb.centerOfMass = _centerOfMass;
            _currentLateralGrip = _normalGrip;

            if (_groundLayerMask.value == 0)
            {
                throw new InvalidOperationException($"[{nameof(CarController)}] GroundLayerMask is unassigned on '{name}'!");
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

            if (_isDrifting || _driftDirection != 0 || !Mathf.Approximately(_currentDriftYawAngle, 0f))
            {
                _isDrifting = false;
                _driftDirection = 0;
                _currentDriftYawAngle = 0f;
                _lastAppliedDriftYaw = 0f;
                OnDriftStop?.Invoke(this, EventArgs.Empty);
                OnDriftDirectionChanged?.Invoke(this, 0);
            }
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

            AnimateWheelsVisuals();
        }

        private void FixedUpdate()
        {
            _rb.angularVelocity = Vector3.zero;

            HandleRaycastGrounding();
            UpdateDriftState();
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

                currentPosition.y = Mathf.SmoothDamp(currentPosition.y, targetY, ref _groundYVelocity, 0.05f, 25f, Time.fixedDeltaTime);
                _rb.position = currentPosition;

                Vector3 currentVelocity = _rb.linearVelocity;
                currentVelocity.y = 0f;
                _rb.linearVelocity = currentVelocity;
            }
            else
            {
                _groundYVelocity = 0f;
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

            if (_isDrifting)
            {
                if (inputY > 0.05f)
                {
                    float effectiveMaxSpeed = (_currentDriftDuration >= _minDriftTimeToBoost) ? _maxOverallSpeed : _maxForwardSpeed;
                    float maxTargetSpeed = effectiveMaxSpeed * inputY;
                    targetForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, maxTargetSpeed, _driftAcceleration * Time.fixedDeltaTime);
                }
                else
                {
                    targetForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, 0f, _driftDeceleration * Time.fixedDeltaTime);
                }
            }
            else if (_brakeInput)
            {
                targetForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, 0f, _brakeDeceleration * Time.fixedDeltaTime);
            }
            else if (inputY > 0.05f)
            {
                if (_currentForwardSpeed > _maxForwardSpeed)
                {
                    targetForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, _maxForwardSpeed, _driftSpeedDecayRate * Time.fixedDeltaTime);
                }
                else
                {
                    float maxTargetSpeed = _maxForwardSpeed * inputY;
                    targetForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, maxTargetSpeed, _acceleration * Time.fixedDeltaTime);
                }
            }
            else if (inputY < -0.05f)
            {
                float maxTargetSpeed = _reverseMaxSpeed * inputY;
                targetForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, maxTargetSpeed, _reverseAcceleration * Time.fixedDeltaTime);
            }
            else
            {
                float decayRate = (_currentForwardSpeed > _maxForwardSpeed) ? (_naturalDeceleration + _driftSpeedDecayRate) : _naturalDeceleration;
                targetForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, 0f, decayRate * Time.fixedDeltaTime);
            }

            _currentForwardSpeed = targetForwardSpeed;

            float targetGrip = _isDrifting ? _driftGrip : _normalGrip;
            _currentLateralGrip = Mathf.MoveTowards(_currentLateralGrip, targetGrip, 4.0f * Time.fixedDeltaTime);
            float targetLateralSpeed = localVelocity.x * (1f - _currentLateralGrip);

            Vector3 targetLocalVelocity = new Vector3(targetLateralSpeed, 0f, _currentForwardSpeed);
            Vector3 targetWorldHorizontalVelocity = transform.TransformDirection(targetLocalVelocity);

            Vector3 currentWorldHorizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            Vector3 velocityChange = targetWorldHorizontalVelocity - currentWorldHorizontalVelocity;

            _rb.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        private void HandleArcadeSteering()
        {
            _smoothedSteerInput = Mathf.MoveTowards(_smoothedSteerInput, _moveInput.x, _steerResponseSpeed * Time.fixedDeltaTime);

            float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
            float directionSign = 1f;

            if (_moveInput.y < -0.1f || (forwardSpeed < -0.5f && _moveInput.y <= 0f))
            {
                directionSign = -1f;
            }

            float targetTurnMultiplier = 1.0f;
            if (_isDrifting)
            {
                float steerIntensity = Mathf.Abs(_smoothedSteerInput);
                targetTurnMultiplier = Mathf.Lerp(0.35f, _driftTurnMultiplier, steerIntensity);

                bool isCounterSteering = (_driftDirection < 0 && _smoothedSteerInput > 0.05f) || (_driftDirection > 0 && _smoothedSteerInput < -0.05f);
                if (isCounterSteering)
                {
                    targetTurnMultiplier *= 0.5f;
                }
            }

            _currentTurnMultiplier = Mathf.MoveTowards(_currentTurnMultiplier, targetTurnMultiplier, 6.0f * Time.fixedDeltaTime);
            float turnAmount = _smoothedSteerInput * _turnSpeed * _currentTurnMultiplier * directionSign * Time.fixedDeltaTime;

            float yawDelta = _currentDriftYawAngle - _lastAppliedDriftYaw;
            _lastAppliedDriftYaw = _currentDriftYawAngle;

            Quaternion turnRotation = Quaternion.Euler(0f, turnAmount + yawDelta, 0f);

            _rb.MoveRotation(_rb.rotation * turnRotation);
        }

        private void UpdateDriftState()
        {
            bool wasDrifting = _isDrifting;
            int oldDirection = _driftDirection;

            float currentSpeed = GetMovementSpeed();
            bool hasMinSpeed = currentSpeed >= _minSpeedToDrift;
            bool isSteering = Mathf.Abs(_smoothedSteerInput) >= 0.2f;

            if (_isDrifting)
            {
                _currentDriftDuration += Time.fixedDeltaTime;
                bool speedOrMoveExit = !hasMinSpeed || _moveInput.y < 0.05f;
                bool steerExitWithoutBrake = !_brakeInput && Mathf.Abs(_smoothedSteerInput) < 0.1f;
                if (speedOrMoveExit || steerExitWithoutBrake)
                {
                    _isDrifting = false;
                    _currentDriftDuration = 0f;
                }
            }
            else
            {
                _currentDriftDuration = 0f;
                bool startCondition = _brakeInput && hasMinSpeed && _moveInput.y >= 0.1f && isSteering;
                if (startCondition)
                {
                    _isDrifting = true;
                    _currentDriftDuration = 0f;
                }
            }

            if (_isDrifting)
            {
                if (_smoothedSteerInput < -0.05f)
                {
                    _driftDirection = -1;
                }
                else if (_smoothedSteerInput > 0.05f)
                {
                    _driftDirection = 1;
                }
            }
            else
            {
                _driftDirection = 0;
            }

            float targetAngle = 0f;
            if (_isDrifting)
            {
                targetAngle = _driftDirection * _targetDriftAngle;

                bool isCounterSteering = (_driftDirection < 0 && _moveInput.x > 0.05f) || (_driftDirection > 0 && _moveInput.x < -0.05f);
                if (isCounterSteering)
                {
                    float counterSteerFactor = Mathf.Abs(_moveInput.x);
                    targetAngle *= (1f - counterSteerFactor * _counterSteerImpact);
                }
                else
                {
                    float steerDeepenFactor = Mathf.Abs(_moveInput.x);
                    targetAngle *= Mathf.Lerp(0.85f, 1.15f, steerDeepenFactor);
                }
            }

            _currentDriftYawAngle = Mathf.MoveTowards(_currentDriftYawAngle, targetAngle, _driftYawResponseSpeed * 10f * Time.fixedDeltaTime);

            if (!wasDrifting && _isDrifting)
            {
                OnDriftStart?.Invoke(this, EventArgs.Empty);
            }
            else if (wasDrifting && !_isDrifting)
            {
                _currentDriftDuration = 0f;
                _currentDriftYawAngle = 0f;
                _lastAppliedDriftYaw = 0f;
                OnDriftStop?.Invoke(this, EventArgs.Empty);
            }

            if (oldDirection != _driftDirection)
            {
                OnDriftDirectionChanged?.Invoke(this, _driftDirection);
            }
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
