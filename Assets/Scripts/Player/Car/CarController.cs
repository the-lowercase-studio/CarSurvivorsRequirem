using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Assets.Scripts.Player.Car
{
    public interface ICarController
    {
        event EventHandler OnBrakePress;

        event EventHandler OnBrakeRelease;

        float GetMovementSpeed();
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
            [FormerlySerializedAs("WheelModel")]
            [SerializeField] private GameObject _wheelModel;

            [FormerlySerializedAs("WheelCollider")]
            [SerializeField] private WheelCollider _wheelCollider;

            [FormerlySerializedAs("Axel")]
            [SerializeField] private Axel _axel;

            public GameObject WheelModel => _wheelModel;
            public WheelCollider WheelCollider => _wheelCollider;
            public Axel Axel => _axel;
        }

        [Header("BulletTimeToArriveAtRangeEnd")]
        [SerializeField] private float _speed = 600f;
        [SerializeField] private float _maxAcceleration = 30.0f;

        [Header("Rotation")]
        [SerializeField] private float _turnSensitivity = 1.0f;
        [SerializeField] private float _maxSteerAngle = 30.0f;

        [Header("Physics")]
        [SerializeField] private Vector3 _centerOfMass;

        [SerializeField] private List<Wheel> _wheels;

        [Header("Drift")]
        [SerializeField] private float _minSpeedToDrift = 8f;
        [SerializeField] private float _minForwardInputToDrift = 0.5f;
        [SerializeField] private float _minSteerInputToDrift = 0.4f;
        [SerializeField] private float _driftRearSidewaysStiffnessMultiplier = 0.55f;

        private Rigidbody _rb;
        private InputAction _moveAction;
        private Vector2 _moveInput;
        private InputAction _brakeAction;
        private bool _brakeInput;
        private float _brakeTorqueMultiplier = 1000f;
        private bool _isDrifting;
        private readonly Dictionary<WheelCollider, WheelFrictionCurve> _rearSidewaysFrictionByWheel = new();

        public event EventHandler OnBrakePress;

        public event EventHandler OnBrakeRelease;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _moveAction = InputSystem.actions.FindAction("Move");
            _brakeAction = InputSystem.actions.FindAction("Brake");
            _rb.centerOfMass = _centerOfMass;
            CacheRearWheelSidewaysFriction();
        }

        private void OnEnable()
        {
            _brakeAction.started += BrakeAction_Started;
            _brakeAction.canceled += BrakeAction_Canceled;
        }

        private void OnDisable()
        {
            _brakeAction.started -= BrakeAction_Started;
            _brakeAction.canceled -= BrakeAction_Canceled;
            RestoreDriftState();
        }

        private void Update()
        {
            _moveInput = _moveAction.ReadValue<Vector2>().normalized;
            _brakeInput = _brakeAction.IsPressed();
        }

        private void FixedUpdate()
        {
            HandleMove();
            HandleSteer();
            HandleBrake();
            HandleDrift();
            AnimateWheels();
        }

        public float GetMovementSpeed()
        {
            return _rb.linearVelocity.magnitude;
        }

        private void HandleMove()
        {
            foreach (var wheel in _wheels)
            {
                wheel.WheelCollider.motorTorque = _moveInput.y * _speed * _maxAcceleration * Time.deltaTime;
            }
        }

        private void HandleSteer()
        {
            foreach (var wheel in _wheels)
            {
                if (wheel.Axel == Axel.Front)
                {
                    float steerAngle = _moveInput.x * _turnSensitivity * _maxSteerAngle;
                    wheel.WheelCollider.steerAngle = Mathf.Lerp(wheel.WheelCollider.steerAngle, steerAngle, 0.6f);
                }
            }
        }

        private void HandleBrake()
        {
            float brakeTorque = _brakeInput ? _maxAcceleration * _brakeTorqueMultiplier : 0f;
            foreach (var wheel in _wheels)
            {
                wheel.WheelCollider.brakeTorque = brakeTorque;
            }
        }

        private void HandleDrift()
        {
            bool shouldDrift = CanDrift();

            if (shouldDrift == _isDrifting)
            {
                return;
            }

            _isDrifting = shouldDrift;

            if (_isDrifting)
            {
                ApplyDriftFriction();
                return;
            }

            RestoreRearSidewaysFriction();
        }

        private bool CanDrift()
        {
            return _brakeInput
                && GetMovementSpeed() >= _minSpeedToDrift
                && _moveInput.y >= _minForwardInputToDrift
                && Mathf.Abs(_moveInput.x) >= _minSteerInputToDrift;
        }

        private void CacheRearWheelSidewaysFriction()
        {
            _rearSidewaysFrictionByWheel.Clear();

            foreach (var wheel in _wheels)
            {
                if (wheel.Axel == Axel.Rear)
                {
                    _rearSidewaysFrictionByWheel[wheel.WheelCollider] = wheel.WheelCollider.sidewaysFriction;
                }
            }
        }

        private void ApplyDriftFriction()
        {
            foreach (var rearWheelFriction in _rearSidewaysFrictionByWheel)
            {
                WheelFrictionCurve sidewaysFriction = rearWheelFriction.Value;
                sidewaysFriction.stiffness *= _driftRearSidewaysStiffnessMultiplier;
                rearWheelFriction.Key.sidewaysFriction = sidewaysFriction;
            }
        }

        private void RestoreRearSidewaysFriction()
        {
            foreach (var rearWheelFriction in _rearSidewaysFrictionByWheel)
            {
                rearWheelFriction.Key.sidewaysFriction = rearWheelFriction.Value;
            }
        }

        private void RestoreDriftState()
        {
            if (!_isDrifting)
            {
                return;
            }

            _isDrifting = false;
            RestoreRearSidewaysFriction();
        }

        private void AnimateWheels()
        {
            foreach (var wheel in _wheels)
            {
                wheel.WheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
                wheel.WheelModel.transform.SetPositionAndRotation(pos, rot);
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
