using System;
using DG.Tweening;
using UnityEngine;

namespace Assets.Scripts.Indicators
{
    public class RectangularTelegraphIndicator : MonoBehaviour, ITelegraphIndicator
    {
        [SerializeField] private Transform _outerBorder;
        [SerializeField] private Transform _innerFill;
        [SerializeField] private float _expandDuration = 0.15f;
        [SerializeField] private float _contractDuration = 0.12f;

        private Sequence _activeSequence;
        private Action _onImpactCallback;

        private void OnDisable()
        {
            KillActiveSequence();
        }

        public void Show(Vector3 origin, Vector3 forwardDirection, float length, float width, float duration, Action onImpact = null)
        {
            KillActiveSequence();
            _onImpactCallback = onImpact;

            forwardDirection.y = 0f;
            if (forwardDirection.sqrMagnitude < 0.001f)
            {
                forwardDirection = Vector3.forward;
            }
            forwardDirection.Normalize();

            transform.position = origin;
            transform.rotation = Quaternion.LookRotation(forwardDirection, Vector3.up);
            gameObject.SetActive(true);

            Vector3 borderScale = new Vector3(width, 1f, length);
            Vector3 startFillScale = new Vector3(width, 1f, 0f);
            Vector3 targetFillScale = new Vector3(width, 1f, length);

            if (_outerBorder != null)
            {
                _outerBorder.localScale = new Vector3(width, 1f, 0f);
            }

            if (_innerFill != null)
            {
                _innerFill.localScale = startFillScale;
            }

            _activeSequence = DOTween.Sequence();

            if (_outerBorder != null)
            {
                _activeSequence.Append(_outerBorder.DOScale(borderScale, _expandDuration).SetEase(Ease.OutQuad));
            }

            if (_innerFill != null)
            {
                _activeSequence.Join(_innerFill.DOScale(targetFillScale, duration).SetEase(Ease.Linear));
            }
            else
            {
                _activeSequence.AppendInterval(duration);
            }

            _activeSequence.OnComplete(() =>
            {
                _onImpactCallback?.Invoke();
                PlayContractAndDismiss();
            });
        }

        public void Dismiss()
        {
            KillActiveSequence();
            gameObject.SetActive(false);
        }

        private void PlayContractAndDismiss()
        {
            KillActiveSequence();

            _activeSequence = DOTween.Sequence();

            if (_outerBorder != null)
            {
                _activeSequence.Join(_outerBorder.DOScale(new Vector3(0f, 1f, _outerBorder.localScale.z), _contractDuration).SetEase(Ease.InQuad));
            }

            if (_innerFill != null)
            {
                _activeSequence.Join(_innerFill.DOScale(new Vector3(0f, 1f, _innerFill.localScale.z), _contractDuration).SetEase(Ease.InQuad));
            }

            _activeSequence.OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
        }

        private void KillActiveSequence()
        {
            if (_activeSequence != null && _activeSequence.IsActive())
            {
                _activeSequence.Kill();
            }
            _activeSequence = null;
        }
    }
}
