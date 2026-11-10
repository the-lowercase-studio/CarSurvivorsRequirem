using Assets.Scripts.PowerUps.Constants;
using DG.Tweening;
using UnityEngine;

namespace Assets.Scripts.PowerUps.Shield
{
    public class BubbleShieldVisualController : MonoBehaviour
    {
        [SerializeField] private Renderer _shieldRenderer;

        private MaterialPropertyBlock _propertyBlock;
        private Sequence _transitionSequence;
        private Vector3 _originalLocalScale;
        private Color _currentColor = Color.white;
        private float _currentAlpha = 1f;
        private float _transitionAlphaFactor = 1f;
        private bool _isInitialized;
        private bool _isShowing;

        private void Awake()
        {
            EnsureInitialized();

            if (!_isShowing)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            KillTransitionSequence();
        }

        private void OnDestroy()
        {
            KillTransitionSequence();
        }

        public void Show(float duration, float targetAlpha)
        {
            EnsureInitialized();
            KillTransitionSequence();

            _isShowing = true;
            _currentAlpha = Mathf.Clamp01(targetAlpha);
            _transitionAlphaFactor = 0f;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            transform.localScale = Vector3.zero;
            ApplyPropertyBlock();

            _transitionSequence = DOTween.Sequence();
            _transitionSequence.Join(transform.DOScale(_originalLocalScale, duration).SetEase(Ease.OutBack));
            _transitionSequence.Join(DOVirtual.Float(0f, 1f, duration, SetTransitionAlphaFactor).SetEase(Ease.OutQuad));
        }

        public void RestoreSteadyState(float targetAlpha)
        {
            EnsureInitialized();

            if (!_isShowing || !gameObject.activeSelf)
            {
                Show(0.01f, targetAlpha);
                return;
            }

            KillTransitionSequence();
            transform.localScale = _originalLocalScale;
            _transitionAlphaFactor = 1f;
            _currentAlpha = Mathf.Clamp01(targetAlpha);
            ApplyPropertyBlock();
        }

        public void Hide(float duration)
        {
            EnsureInitialized();
            KillTransitionSequence();

            _isShowing = false;
            float startFactor = _transitionAlphaFactor;

            _transitionSequence = DOTween.Sequence();
            _transitionSequence.Join(transform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack));
            _transitionSequence.Join(DOVirtual.Float(startFactor, 0f, duration, SetTransitionAlphaFactor).SetEase(Ease.InQuad));
            _transitionSequence.OnComplete(CompleteHide);
        }

        public void HideImmediate()
        {
            EnsureInitialized();
            KillTransitionSequence();

            _isShowing = false;
            _transitionAlphaFactor = 0f;
            transform.localScale = _originalLocalScale;
            gameObject.SetActive(false);
        }

        public void SetAlpha(float alpha)
        {
            EnsureInitialized();
            _currentAlpha = Mathf.Clamp01(alpha);
            ApplyPropertyBlock();
        }

        public void SetColor(Color color)
        {
            EnsureInitialized();
            _currentColor = color;
            ApplyPropertyBlock();
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            _propertyBlock = new MaterialPropertyBlock();
            _originalLocalScale = transform.localScale;
        }

        private void SetTransitionAlphaFactor(float factor)
        {
            _transitionAlphaFactor = Mathf.Clamp01(factor);
            ApplyPropertyBlock();
        }

        private void CompleteHide()
        {
            _transitionSequence = null;
            gameObject.SetActive(false);
            transform.localScale = _originalLocalScale;
        }

        private void ApplyPropertyBlock()
        {
            if (_shieldRenderer == null)
            {
                return;
            }

            _shieldRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BubbleShieldShaderConstants.BASE_COLOR_ID, _currentColor);
            _propertyBlock.SetFloat(BubbleShieldShaderConstants.ALPHA_ID, Mathf.Clamp01(_currentAlpha * _transitionAlphaFactor));
            _shieldRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void KillTransitionSequence()
        {
            if (_transitionSequence != null && _transitionSequence.IsActive())
            {
                _transitionSequence.Kill();
            }

            _transitionSequence = null;
        }
    }
}
