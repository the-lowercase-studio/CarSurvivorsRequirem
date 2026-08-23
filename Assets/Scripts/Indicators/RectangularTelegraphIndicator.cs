using System;
using DG.Tweening;
using Assets.Scripts.Indicators.Constants;
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

        private void OnDestroy()
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

            Vector3 spawnPosition = origin;
            spawnPosition.y += IndicatorConstants.GROUND_Y_OFFSET;

            transform.position = spawnPosition;
            transform.rotation = Quaternion.LookRotation(forwardDirection, Vector3.up);
            gameObject.SetActive(true);

            float targetScaleX = width / IndicatorConstants.UNITY_PLANE_SIZE;
            float targetScaleZ = length / IndicatorConstants.UNITY_PLANE_SIZE;
            float halfLength = length * 0.5f;

            Vector3 borderScale = new Vector3(targetScaleX, 1f, targetScaleZ);

            if (_outerBorder != null)
            {
                _outerBorder.localPosition = new Vector3(0f, 0f, halfLength);
                _outerBorder.localScale = new Vector3(targetScaleX, 1f, 0f);
            }

            if (_innerFill != null)
            {
                _innerFill.localPosition = new Vector3(0f, IndicatorConstants.FILL_Y_OFFSET, 0f);
                _innerFill.localScale = new Vector3(targetScaleX, 1f, 0f);
            }

            _activeSequence = DOTween.Sequence();

            if (_outerBorder != null)
            {
                _activeSequence.Append(_outerBorder.DOScale(borderScale, _expandDuration).SetEase(Ease.OutQuad));
            }

            if (_innerFill != null)
            {
                _activeSequence.Join(_innerFill.DOLocalMoveZ(halfLength, duration).SetEase(Ease.Linear));
                _activeSequence.Join(_innerFill.DOScaleZ(targetScaleZ, duration).SetEase(Ease.Linear));
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
            Destroy(gameObject);
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
                Destroy(gameObject);
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
