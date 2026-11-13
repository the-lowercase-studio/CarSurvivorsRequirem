using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.Effects
{
    public class UIElementEffects : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private float _growScaleMultiplier = 1.2f;
        [SerializeField] private float _growAnimationDuration = 0.2f;
        [SerializeField] private float _shrinkAnimatioDuration = 0.1f;

        private readonly List<RectTransform> _trackedRects = new List<RectTransform>(8);

        public void OnSelect(BaseEventData eventData)
        {
            if (transform is RectTransform rect)
            {
                PlayGrowAnimation(rect);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (transform is RectTransform rect)
            {
                PlayShrinkAnimation(rect);
            }
        }

        public void PlayGrowAnimation(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            TrackRect(rect);
            DOTween.Kill(rect);
            rect.DOScale(Vector3.one * _growScaleMultiplier, _growAnimationDuration)
                .SetUpdate(true);
        }

        public void PlayShrinkAnimation(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            TrackRect(rect);
            DOTween.Kill(rect);
            rect.DOScale(Vector3.one, _shrinkAnimatioDuration)
                .SetUpdate(true);
        }

        private void OnDisable()
        {
            KillTrackedTweens();
        }

        private void OnDestroy()
        {
            KillTrackedTweens();
        }

        private void TrackRect(RectTransform rect)
        {
            if (!_trackedRects.Contains(rect))
            {
                _trackedRects.Add(rect);
            }
        }

        private void KillTrackedTweens()
        {
            for (int i = 0; i < _trackedRects.Count; i++)
            {
                RectTransform rect = _trackedRects[i];
                if (rect != null)
                {
                    DOTween.Kill(rect);
                    rect.localScale = Vector3.one;
                }
            }
        }
    }
}
