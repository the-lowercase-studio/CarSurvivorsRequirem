using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

namespace Assets.Scripts.Cameras
{
    public interface ICinemachineCombatFollowOffsetController
    {
        void TransitionToCombatOffset(Vector3 combatOffset, float duration, Ease ease = Ease.InOutSine);
        void RestoreDefaultOffset(float duration, Ease ease = Ease.InOutSine);
    }

    public class CinemachineCombatFollowOffsetController : MonoBehaviour, ICinemachineCombatFollowOffsetController
    {
        [Tooltip("Reference to the CinemachineFollow component on the active virtual camera.")]
        [SerializeField] private CinemachineFollow _cinemachineFollow;
        [Tooltip("Default camera follow offset restored outside combat.")]
        [SerializeField] private Vector3 _defaultFollowOffset = new Vector3(0f, 11f, -7f);

        private Tween _offsetTween;

        public Vector3 DefaultFollowOffset
        {
            get
            {
                return _defaultFollowOffset;
            }
        }

        private void Awake()
        {
            if (_cinemachineFollow != null)
            {
                _cinemachineFollow.FollowOffset = _defaultFollowOffset;
            }
        }

        private void OnDisable()
        {
            KillTween();
        }

        private void OnDestroy()
        {
            KillTween();
        }

        public void TransitionToCombatOffset(Vector3 combatOffset, float duration, Ease ease = Ease.InOutSine)
        {
            KillTween();
            if (_cinemachineFollow == null)
            {
                return;
            }

            _offsetTween = DOTween.To(
                () => _cinemachineFollow.FollowOffset,
                x => _cinemachineFollow.FollowOffset = x,
                combatOffset,
                duration
            ).SetEase(ease);
        }

        public void RestoreDefaultOffset(float duration, Ease ease = Ease.InOutSine)
        {
            KillTween();
            if (_cinemachineFollow == null)
            {
                return;
            }

            _offsetTween = DOTween.To(
                () => _cinemachineFollow.FollowOffset,
                x => _cinemachineFollow.FollowOffset = x,
                _defaultFollowOffset,
                duration
            ).SetEase(ease);
        }

        private void KillTween()
        {
            if (_offsetTween != null && _offsetTween.IsActive())
            {
                _offsetTween.Kill();
            }
            _offsetTween = null;
        }
    }
}
