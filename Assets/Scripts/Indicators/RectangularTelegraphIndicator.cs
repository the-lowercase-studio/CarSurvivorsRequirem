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

        public void ShowPersistent(Vector3 origin, Vector3 direction, float length, float width)
        {
            KillActiveSequence();
            _onImpactCallback = null;
            UpdatePersistent(origin, direction, length, width);
        }

        public void UpdatePersistent(Vector3 origin, Vector3 direction, float length, float width)
        {
            if (_outerBorder == null || _innerFill == null)
            {
                throw new InvalidOperationException("Persistent rectangular indicator requires border and fill references.");
            }
            transform.SetPositionAndRotation(origin + Vector3.up * IndicatorConstants.GROUND_Y_OFFSET,
                Quaternion.LookRotation(direction, Vector3.up));
            // Compensate authored parent/model scaling so query dimensions remain world dimensions.
            transform.localScale = Vector3.one;
            Vector3 worldScale = transform.lossyScale;
            if (worldScale.x <= 0f || worldScale.y <= 0f || worldScale.z <= 0f)
            {
                throw new InvalidOperationException("Persistent indicator requires positive parent scaling.");
            }
            transform.localScale = new Vector3(1f / worldScale.x, 1f / worldScale.y, 1f / worldScale.z);
            Vector3 scale = new Vector3(width / IndicatorConstants.UNITY_PLANE_SIZE, 1f, length / IndicatorConstants.UNITY_PLANE_SIZE);
            _outerBorder.localPosition = new Vector3(0f, 0f, length * 0.5f);
            _innerFill.localPosition = new Vector3(0f, IndicatorConstants.FILL_Y_OFFSET, length * 0.5f);
            _outerBorder.localScale = scale;
            _innerFill.localScale = scale;
            gameObject.SetActive(true);
        }

        public void HidePersistent()
        {
            KillActiveSequence();
            _onImpactCallback = null;
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            KillActiveSequence();
        }

        private void OnDestroy()
        {
            KillActiveSequence();
        }

        public void Show(Vector3 origin, Vector3 forwardDirection, float length, float width, float duration, Action onImpact = null, bool autoContractOnFillComplete = false)
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
                if (autoContractOnFillComplete)
                {
                    PlayContractAndDismiss();
                }
            });
        }

        public void ContractAndDismiss()
        {
            PlayContractAndDismiss();
        }

        public void Dismiss()
        {
            KillActiveSequence();
            if (this != null && gameObject != null)
            {
                Destroy(gameObject);
            }
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

            if (_outerBorder == null && _innerFill == null)
            {
                if (this != null && gameObject != null)
                {
                    Destroy(gameObject);
                }
                return;
            }

            _activeSequence.OnComplete(() =>
            {
                if (this != null && gameObject != null)
                {
                    Destroy(gameObject);
                }
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
