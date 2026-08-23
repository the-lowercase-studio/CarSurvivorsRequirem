using Assets.Scripts.Enemies.Bosses.Golem.Constants;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.Animation
{
    public interface IGolemAnimator
    {
        void SetMoving(bool isMoving, float speed = 0f);
        void PlayLeapSlam();
        void PlayStomp();
        void PlayLinearFist();
        void PlaySkyBarrage();
    }

    public class GolemAnimator : MonoBehaviour, IGolemAnimator
    {
        [SerializeField] private Animator _animator;

        private int _isMovingHash;
        private int _speedHash;
        private int _leapSlamHash;
        private int _stompHash;
        private int _linearFistHash;
        private int _skyBarrageHash;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            _isMovingHash = Animator.StringToHash(GolemBossConstants.ANIM_PARAM_IS_MOVING);
            _speedHash = Animator.StringToHash(GolemBossConstants.ANIM_PARAM_SPEED);
            _leapSlamHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_LEAP_SLAM);
            _stompHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_STOMP);
            _linearFistHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_LINEAR_FIST);
            _skyBarrageHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_SKY_BARRAGE);
        }

        public void SetMoving(bool isMoving, float speed = 0f)
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetBool(_isMovingHash, isMoving);
            _animator.SetFloat(_speedHash, speed);
        }

        public void PlayLeapSlam()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetTrigger(_leapSlamHash);
        }

        public void PlayStomp()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetTrigger(_stompHash);
        }

        public void PlayLinearFist()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetTrigger(_linearFistHash);
        }

        public void PlaySkyBarrage()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetTrigger(_skyBarrageHash);
        }
    }
}
