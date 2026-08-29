using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class XYZRotationLoop : MonoBehaviour
    {
        [SerializeField] private bool _rotateX;
        [SerializeField] private bool _rotateY;
        [SerializeField] private bool _rotateZ;
        [SerializeField, Range(0, 360f)] private float _maxRotationOnAxis;
        [SerializeField] private bool _useLocalRotation;
        [SerializeField] private float _tweenIterationTime = 2.5f;
        [SerializeField] private bool _unscaleWithTime;

        private Vector3 _maxTweenRotation;
        private Vector3 _angularVelocity;

        private void OnEnable()
        {
            SetMaxRotationTween();
        }

        private void Update()
        {
            if (_tweenIterationTime <= 0f)
            {
                return;
            }

            float dt = _unscaleWithTime ? Time.unscaledDeltaTime : Time.deltaTime;
            transform.Rotate(_angularVelocity * dt, _useLocalRotation ? Space.Self : Space.World);
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
