using System;
using Assets.Scripts.Enemies.Bosses.Golem.Constants;
using UnityEngine;

namespace Assets.Scripts.Enemies.Bosses.Golem.Animation
{
    public interface IGolemAnimator
    {
        bool IsMovingAnimationPlaying { get; }
        bool IsAttackAnimationPlaying { get; }
        void SetMoving(bool isMoving, float speed = 0f);
        void PlayLeapTakeoff();
        void PlayLeapLand();
        void PlayStomp();
        void PlayLinearFist();
        void PlaySkyBarrage();

        event Action OnLinearFistRelease;
        event Action OnSkyBarrageRelease;
        event Action OnLeapTakeoffComplete;
        event Action OnLeapLandComplete;
        event Action OnStompImpact;
    }

    public class GolemAnimator : MonoBehaviour, IGolemAnimator
    {
        [SerializeField] private Animator _animator;

        private int _isMovingHash;
        private int _speedHash;
        private int _leapTakeoffHash;
        private int _leapLandHash;
        private int _leapSlamLegacyHash;
        private int _stompHash;
        private int _linearFistHash;
        private int _skyBarrageHash;
        private int _walkingStateHash;

        public event Action OnLinearFistRelease;
        public event Action OnSkyBarrageRelease;
        public event Action OnLeapTakeoffComplete;
        public event Action OnLeapLandComplete;
        public event Action OnStompImpact;

        public bool IsMovingAnimationPlaying
        {
            get
            {
                if (_animator == null)
                {
                    return true;
                }

                if (_animator.IsInTransition(0))
                {
                    AnimatorStateInfo nextState = _animator.GetNextAnimatorStateInfo(0);
                    return nextState.shortNameHash == _walkingStateHash;
                }

                AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(0);
                return currentState.shortNameHash == _walkingStateHash;
            }
        }

        public bool IsAttackAnimationPlaying
        {
            get
            {
                if (_animator == null)
                {
                    return false;
                }

                return !IsMovingAnimationPlaying;
            }
        }

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            _isMovingHash = Animator.StringToHash(GolemBossConstants.ANIM_PARAM_IS_MOVING);
            _speedHash = Animator.StringToHash(GolemBossConstants.ANIM_PARAM_SPEED);
            _leapTakeoffHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_LEAP_TAKEOFF);
            _leapLandHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_LEAP_LAND);
            _leapSlamLegacyHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_LEAP_SLAM);
            _stompHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_STOMP);
            _linearFistHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_LINEAR_FIST);
            _skyBarrageHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_SKY_BARRAGE);
            _walkingStateHash = Animator.StringToHash(GolemBossConstants.ANIM_STATE_WALKING);
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

        public void PlayLeapTakeoff()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.ResetTrigger(_leapLandHash);
            _animator.SetTrigger(_leapTakeoffHash);
            _animator.SetTrigger(_leapSlamLegacyHash);
        }

        public void PlayLeapLand()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.ResetTrigger(_leapTakeoffHash);
            _animator.ResetTrigger(_leapSlamLegacyHash);
            _animator.SetTrigger(_leapLandHash);
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

        public void Call_OnLinearFistRelease()
        {
            OnLinearFistRelease?.Invoke();
        }

        public void Call_OnSkyBarrageRelease()
        {
            OnSkyBarrageRelease?.Invoke();
        }

        public void Call_OnLeapTakeoffComplete()
        {
            OnLeapTakeoffComplete?.Invoke();
        }

        public void Call_OnLeapLandComplete()
        {
            OnLeapLandComplete?.Invoke();
        }

        public void Call_OnStompImpact()
        {
            OnStompImpact?.Invoke();
        }
    }
}
