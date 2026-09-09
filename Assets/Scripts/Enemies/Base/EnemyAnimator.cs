using Assets.Scripts.Enemies.Constants;
using System;
using UnityEngine;

namespace Assets.Scripts.Enemies.Base
{
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimator : MonoBehaviour, IAttackAnimationPlayer
    {
        [SerializeField] private Enemy _enemy;
        [SerializeField] private float _animationResponseSpeed = 0.05f;

        private static readonly int _speedHash = Animator.StringToHash(EnemyAnimationConstants.ANIM_PARAM_SPEED);
        private static readonly int _isOnGroundHash = Animator.StringToHash(EnemyAnimationConstants.ANIM_PARAM_IS_ON_GROUND);
        private static readonly int _isMovingByCrawlingHash = Animator.StringToHash(EnemyAnimationConstants.ANIM_PARAM_IS_MOVING_BY_CRAWLING);
        private static readonly int _attackTriggerHash = Animator.StringToHash(EnemyAnimationConstants.ANIM_TRIGGER_ATTACK);

        private static readonly int _zombieAttackStandingStateHash = Animator.StringToHash(EnemyAnimationConstants.ANIM_STATE_ZOMBIE_ATTACK_STANDING);
        private static readonly int _zombieBitingCrawlingStateHash = Animator.StringToHash(EnemyAnimationConstants.ANIM_STATE_ZOMBIE_BITING_CRAWLING);
        private static readonly int _attackStateHash = Animator.StringToHash(EnemyAnimationConstants.ANIM_STATE_ATTACK);
        private static readonly int _fallingStateHash = Animator.StringToHash(EnemyAnimationConstants.ANIM_STATE_FALLING);
        private static readonly int _fallOverStateHash = Animator.StringToHash(EnemyAnimationConstants.ANIM_STATE_FALL_OVER);

        private Animator _animator;
        private int _walkingLayerIndex = 0;
        private int _crawlingLayerIndex = 1;
        private float _attackStartTime;

        public bool IsPlayingAttackAnimation { get; private set; }

        public event EventHandler OnAttackAnimationStart;

        public event EventHandler OnAttackAnimationEnd;

        public event EventHandler OnAttackHitFrame;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            ResetAttackAnimationState();
            InvokeRepeating(nameof(HandleTransitionPropertiesChanges), 0f, _animationResponseSpeed);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(HandleTransitionPropertiesChanges));
            ResetAttackAnimationState();
        }

        public void PlayAttackAnimation()
        {
            _attackStartTime = Time.time;
            _animator.SetTrigger(_attackTriggerHash);
        }

        public void ResetAttackAnimationState()
        {
            IsPlayingAttackAnimation = false;
            _attackStartTime = 0f;

            if (_animator != null)
            {
                _animator.ResetTrigger(_attackTriggerHash);
            }
        }

        public void Call_OnAttackAnimationStart()
        {
            IsPlayingAttackAnimation = true;
            _attackStartTime = Time.time;
            OnAttackAnimationStart?.Invoke(this, EventArgs.Empty);
        }

        public void Call_OnAttackAnimationEnd()
        {
            if (!IsPlayingAttackAnimation)
            {
                return;
            }

            IsPlayingAttackAnimation = false;
            _attackStartTime = 0f;
            OnAttackAnimationEnd?.Invoke(this, EventArgs.Empty);
        }

        public void Call_OnAttackHitFrame()
        {
            OnAttackHitFrame?.Invoke(this, EventArgs.Empty);
        }

        private void HandleTransitionPropertiesChanges()
        {
            SetCrawlingTransitionProperties();

            _animator.SetFloat(_speedHash, _enemy.MovementController.GetCurrentMovementSpeed());
            _animator.SetBool(_isOnGroundHash, _enemy.MovementController.IsOnGround());

            SynchronizeAttackAnimationState();
        }

        private void SynchronizeAttackAnimationState()
        {
            if (!IsPlayingAttackAnimation)
            {
                return;
            }

            if (Time.time - _attackStartTime >= EnemyAnimationConstants.MAX_ATTACK_ANIMATION_DURATION)
            {
                Call_OnAttackAnimationEnd();
                return;
            }

            int activeLayerIndex = _enemy.Config.IsMovingByCrawling ? _crawlingLayerIndex : _walkingLayerIndex;

            if (_animator.layerCount <= activeLayerIndex)
            {
                return;
            }

            bool isTransitioning = _animator.IsInTransition(activeLayerIndex);
            AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(activeLayerIndex);

            if (isTransitioning)
            {
                AnimatorStateInfo nextState = _animator.GetNextAnimatorStateInfo(activeLayerIndex);
                if (nextState.shortNameHash == _fallingStateHash || nextState.shortNameHash == _fallOverStateHash)
                {
                    Call_OnAttackAnimationEnd();
                    return;
                }
            }

            bool isInAttackState = IsAttackState(currentState.shortNameHash);

            if (!isInAttackState && isTransitioning)
            {
                AnimatorStateInfo nextState = _animator.GetNextAnimatorStateInfo(activeLayerIndex);
                isInAttackState = IsAttackState(nextState.shortNameHash);
            }

            if (!isInAttackState)
            {
                Call_OnAttackAnimationEnd();
            }
        }

        private bool IsAttackState(int stateShortNameHash)
        {
            return stateShortNameHash == _zombieAttackStandingStateHash
                || stateShortNameHash == _zombieBitingCrawlingStateHash
                || stateShortNameHash == _attackStateHash;
        }

        private void SetCrawlingTransitionProperties()
        {
            bool isMovingByCrawling = _enemy.Config.IsMovingByCrawling;
            if (isMovingByCrawling)
            {
                _animator.SetLayerWeight(_walkingLayerIndex, 0);
                _animator.SetLayerWeight(_crawlingLayerIndex, 1);
            }
            else
            {
                _animator.SetLayerWeight(_walkingLayerIndex, 1);
                _animator.SetLayerWeight(_crawlingLayerIndex, 0);
            }

            _animator.SetBool(_isMovingByCrawlingHash, isMovingByCrawling);
        }
    }
}
