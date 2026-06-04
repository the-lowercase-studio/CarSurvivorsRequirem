using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class FaceMainCameraDirection : MonoBehaviour
    {
        [Inject] private Camera _mainCamera = null;

        [Tooltip("When enabled, the transform's -Z axis faces the main camera instead of +Z.")]
        [SerializeField] private bool _isDirectionFlipped;
        [Tooltip("Higher values follow the camera faster. Use 0 for instant LookAt behavior.")]
        [SerializeField] private float _rotationSmoothSpeed = 12f;
        [Tooltip("Maximum absolute X rotation in degrees. Use 180 for unrestricted pitch.")]
        [SerializeField] private float _maxXRotation = 180f;
        [Tooltip("Maximum absolute Y rotation in degrees. Use 180 for unrestricted yaw.")]
        [SerializeField] private float _maxYRotation = 180f;
        [Tooltip("Local-space vertical position offset applied from the starting local position.")]
        [SerializeField] private float _localYOffset;

        private Vector3 _startLocalPosition;

        private void Start()
        {
            _startLocalPosition = transform.localPosition;
            transform.localPosition = _startLocalPosition + Vector3.up * _localYOffset;
        }

        public void Initialize(Camera mainCamera)
        {
            _mainCamera = mainCamera;
        }

        private void LateUpdate()
        {
            if (_mainCamera == null)
            {
                return;
            }

            Vector3 desiredDirection = _mainCamera.transform.position - transform.position;

            if (_isDirectionFlipped)
            {
                desiredDirection = -desiredDirection;
            }

            if (desiredDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Quaternion desiredRotation = ClampRotation(Quaternion.LookRotation(desiredDirection));

            if (_rotationSmoothSpeed <= 0f)
            {
                transform.rotation = desiredRotation;
                return;
            }

            float interpolation = 1f - Mathf.Exp(-_rotationSmoothSpeed * Time.unscaledDeltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, interpolation);
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
