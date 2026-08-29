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

        private static readonly int _isMovingHash = Animator.StringToHash(GolemBossConstants.ANIM_PARAM_IS_MOVING);
        private static readonly int _speedHash = Animator.StringToHash(GolemBossConstants.ANIM_PARAM_SPEED);
        private static readonly int _leapTakeoffHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_LEAP_TAKEOFF);
        private static readonly int _leapLandHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_LEAP_LAND);
        private static readonly int _leapSlamLegacyHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_LEAP_SLAM);
        private static readonly int _stompHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_STOMP);
        private static readonly int _linearFistHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_LINEAR_FIST);
        private static readonly int _skyBarrageHash = Animator.StringToHash(GolemBossConstants.ANIM_TRIGGER_SKY_BARRAGE);
        private static readonly int _walkingStateHash = Animator.StringToHash(GolemBossConstants.ANIM_STATE_WALKING);

        public event Action OnLinearFistRelease;
        public event Action OnSkyBarrageRelease;
        public event Action OnLeapTakeoffComplete;
        public event Action OnLeapLandComplete;
        public event Action OnStompImpact;

        public bool IsMovingAnimationPlaying
        {
            get
            {
                EnsureAnimator();
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
                EnsureAnimator();
                if (_animator == null)
                {
                    return false;
                }

                return !IsMovingAnimationPlaying;
            }
        }

        private void Awake()
        {
            EnsureAnimator();
        }

        public void SetMoving(bool isMoving, float speed = 0f)
        {
            EnsureAnimator();
            if (_animator == null)
            {
                return;
            }

            _animator.SetBool(_isMovingHash, isMoving);
            _animator.SetFloat(_speedHash, speed);
        }

        public void PlayLeapTakeoff()
        {
            EnsureAnimator();
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
            EnsureAnimator();
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
            EnsureAnimator();
            if (_animator == null)
            {
                return;
            }

            _animator.SetTrigger(_stompHash);
        }

        public void PlayLinearFist()
        {
            EnsureAnimator();
            if (_animator == null)
            {
                return;
            }

            _animator.SetTrigger(_linearFistHash);
        }

        public void PlaySkyBarrage()
        {
            EnsureAnimator();
            if (_animator == null)
            {
                return;
            }

            _animator.SetTrigger(_skyBarrageHash);
        }

        private void EnsureAnimator()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
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
