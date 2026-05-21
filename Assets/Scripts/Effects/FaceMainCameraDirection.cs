using DG.Tweening;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class FaceMainCameraDirection : MonoBehaviour
    {
        [Inject] private readonly Camera _mainCamera = null;

        [Tooltip("When enabled, the transform's -Z axis faces the main camera instead of +Z.")]
        [SerializeField] private bool _isDirectionFlipped;
        [Tooltip("Rotation tween duration. Use 0 for instant LookAt behavior.")]
        [SerializeField] private float _rotationTransitionDuration;
        [SerializeField] private Ease _rotationEase = Ease.OutSine;
        [Tooltip("Minimum target angle change needed to restart the rotation tween.")]
        [SerializeField] private float _rotationRetargetAngle = 1f;
        [Tooltip("Maximum absolute X rotation in degrees. Use 180 for unrestricted pitch.")]
        [SerializeField] private float _maxXRotation = 180f;
        [Tooltip("Maximum absolute Y rotation in degrees. Use 180 for unrestricted yaw.")]
        [SerializeField] private float _maxYRotation = 180f;
        [Tooltip("Local-space vertical position offset applied from the starting local position.")]
        [SerializeField] private float _localYOffset;

        private Tween _rotationTween;
        private Quaternion _targetRotation;
        private Vector3 _startLocalPosition;

        private void Start()
        {
            _startLocalPosition = transform.localPosition;
            transform.localPosition = _startLocalPosition + Vector3.up * _localYOffset;
        }

        private void OnDisable()
        {
            _rotationTween?.Kill();
            _rotationTween = null;
        }

        private void FixedUpdate()
        {
            Vector3 desiredDirection = _mainCamera.transform.position - transform.position;

            if (_isDirectionFlipped)
            {
                desiredDirection = -desiredDirection;
            }

            if (desiredDirection == Vector3.zero)
            {
                return;
            }

            Quaternion desiredRotation = ClampRotation(Quaternion.LookRotation(desiredDirection));

            if (_rotationTransitionDuration <= 0)
            {
                _rotationTween?.Kill();
                _rotationTween = null;
                transform.rotation = desiredRotation;
                _targetRotation = desiredRotation;
                return;
            }

            if (_rotationTween != null && _rotationTween.IsActive() && _rotationTween.IsPlaying()
                && Quaternion.Angle(_targetRotation, desiredRotation) < _rotationRetargetAngle)
            {
                return;
            }

            if (Quaternion.Angle(transform.rotation, desiredRotation) < _rotationRetargetAngle)
            {
                return;
            }

            _rotationTween?.Kill();
            _targetRotation = desiredRotation;
            _rotationTween = transform
                .DORotateQuaternion(desiredRotation, _rotationTransitionDuration)
                .SetEase(_rotationEase)
                .OnKill(() => _rotationTween = null);
        }

        private Quaternion ClampRotation(Quaternion rotation)
        {
            Vector3 eulerAngles = rotation.eulerAngles;
            float xRotation = Mathf.Clamp(NormalizeAngle(eulerAngles.x), -_maxXRotation, _maxXRotation);
            float yRotation = Mathf.Clamp(NormalizeAngle(eulerAngles.y), -_maxYRotation, _maxYRotation);

            return Quaternion.Euler(xRotation, yRotation, eulerAngles.z);
        }

        private float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
