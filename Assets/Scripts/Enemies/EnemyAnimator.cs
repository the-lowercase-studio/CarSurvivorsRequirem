using System;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    [RequireComponent(typeof(Animator))]
    public class EnemyAnimator : MonoBehaviour, IAttackAnimationPlayer
    {
        [SerializeField] private Enemy _enemy;
        [SerializeField] private float _animationResponseSpeed = 0.05f;

        private Animator _animator;
        private int _walkingLayerIndex = 0;
        private int _crawlingLayerIndex = 1;

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
            InvokeRepeating(nameof(HandleTransitionPropertiesChanges), 0, _animationResponseSpeed);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(HandleTransitionPropertiesChanges));
        }

        public void PlayAttackAnimation()
        {
            _animator.SetTrigger("Attack");
        }

        public void Call_OnAttackAnimationStart()
        {
            OnAttackAnimationStart?.Invoke(this, EventArgs.Empty);
            IsPlayingAttackAnimation = true;
        }

        public void Call_OnAttackAnimationEnd()
        {
            OnAttackAnimationEnd?.Invoke(this, EventArgs.Empty);
            IsPlayingAttackAnimation = false;
        }

        public void Call_OnAttackHitFrame()
        {
            OnAttackHitFrame?.Invoke(this, EventArgs.Empty);
        }

        private void HandleTransitionPropertiesChanges()
        {
            SetCrawlingTransitionProperties();

            _animator.SetFloat("Speed", _enemy.MovementController.GetCurrentMovementSpeed());

            _animator.SetBool("IsOnGround", _enemy.MovementController.IsOnGround());
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

            _animator.SetBool("IsMovingByCrawling", isMovingByCrawling);
        }
    }
}
