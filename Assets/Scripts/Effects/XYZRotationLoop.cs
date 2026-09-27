using UnityEngine;

namespace Assets.Scripts.Effects
{
    public enum RotationUpdateMode
    {
        Update = 0,
        LateUpdate = 1,
        FixedUpdate = 2,
    }

    public class XYZRotationLoop : MonoBehaviour
    {
        [SerializeField] private RotationUpdateMode _updateMode = RotationUpdateMode.Update;
        [SerializeField] private bool _rotateX;
        [SerializeField] private bool _rotateY;
        [SerializeField] private bool _rotateZ;
        [SerializeField, Range(0, 360f)] private float _maxRotationOnAxis;
        [SerializeField] private bool _useLocalRotation;
        [SerializeField] private float _tweenIterationTime = 2.5f;
        [SerializeField] private bool _unscaleWithTime;

        private Vector3 _maxTweenRotation;
        private Vector3 _angularVelocity;

        public float SpeedMultiplier { get; set; } = 1.0f;

        private void OnEnable()
        {
            SetMaxRotationTween();
        }

        private void Update()
        {
            if (_updateMode != RotationUpdateMode.Update)
            {
                return;
            }

            ApplyRotation(_unscaleWithTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (_updateMode != RotationUpdateMode.LateUpdate)
            {
                return;
            }

            ApplyRotation(_unscaleWithTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (_updateMode != RotationUpdateMode.FixedUpdate)
            {
                return;
            }

            ApplyRotation(_unscaleWithTime ? Time.fixedUnscaledDeltaTime : Time.fixedDeltaTime);
        }

        private void ApplyRotation(float deltaTime)
        {
            if (_tweenIterationTime <= 0f)
            {
                return;
            }

            float effectiveDeltaTime = deltaTime * SpeedMultiplier;
            transform.Rotate(_angularVelocity * effectiveDeltaTime, _useLocalRotation ? Space.Self : Space.World);
        }

        private void SetMaxRotationTween()
        {
            _maxTweenRotation = new Vector3(
                _rotateX ? _maxRotationOnAxis : 0f,
                _rotateY ? _maxRotationOnAxis : 0f,
                _rotateZ ? _maxRotationOnAxis : 0f
            );
            _angularVelocity = _tweenIterationTime > 0f ? _maxTweenRotation / _tweenIterationTime : Vector3.zero;
        }
    }
}
