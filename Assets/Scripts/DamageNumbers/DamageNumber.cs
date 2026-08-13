using System;
using Assets.Scripts.DamageNumbers.Constants;
using Assets.Scripts.Initializers;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.DamageNumbers
{
    public struct DamageNumberConfig
    {
        public float Damage;
        public DamageNumberApearance DamagePopupApearance;

        public DamageNumberConfig(float damage, DamageNumberApearance damagePopupApearance)
        {
            Damage = damage;
            DamagePopupApearance = damagePopupApearance;
        }
    }

    public class DamageNumber : MonoBehaviour, IInitializable<DamageNumberConfig>
    {
        [SerializeField] private TextMeshPro _textMeshPro;

        private bool _isInitialized;
        private DamageNumberConfig _config;
        private Sequence _animationSequence;

        public event EventHandler OnLifeEnd;

        private void OnDisable()
        {
            KillAnimation();
        }

        private void OnDestroy()
        {
            KillAnimation();
        }

        public void Initialize(DamageNumberConfig config)
        {
            KillAnimation();

            _config = config;

            SetTextApearance(config);

            var (_, growScaleMultiplier, _) = _config.DamagePopupApearance;
            Vector3 targetScale = Vector3.one * growScaleMultiplier;

            transform.localScale = Vector3.zero;

            _animationSequence = DOTween.Sequence()
                .Append(transform.DOScale(targetScale, DamageNumberConstants.RESIZING_ANIMATION_SPEED).SetEase(Ease.InOutSine))
                .Append(transform.DOScale(Vector3.zero, DamageNumberConstants.RESIZING_ANIMATION_SPEED).SetEase(Ease.InOutSine))
                .OnComplete(HandleAnimationComplete);

            _isInitialized = true;
        }

        public bool IsInitialized()
        {
            return _isInitialized;
        }

        private void SetTextApearance(DamageNumberConfig config)
        {
            var (fontSize, _, color) = config.DamagePopupApearance;
            _textMeshPro.text = config.Damage.ToString();
            _textMeshPro.color = color;
            _textMeshPro.fontSize = fontSize;
        }

        private void HandleAnimationComplete()
        {
            OnLifeEnd?.Invoke(this, EventArgs.Empty);
        }

        private void KillAnimation()
        {
            if (_animationSequence != null && _animationSequence.IsActive())
            {
                _animationSequence.Kill();
                _animationSequence = null;
            }
        }
    }
}

