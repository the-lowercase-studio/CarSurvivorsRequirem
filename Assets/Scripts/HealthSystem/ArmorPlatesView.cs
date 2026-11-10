using System;
using Assets.Scripts.HealthSystem.Constants;
using Assets.Scripts.PowerUps.Config;
using Assets.Scripts.PowerUps.Constants;
using DG.Tweening;
using UnityEngine;

namespace Assets.Scripts.HealthSystem
{
    public class ArmorPlatesView : MonoBehaviour
    {
        [SerializeField, Tooltip("RectTransform of the base health bar used as layout anchor and reference width.")]
        private RectTransform _baseHealthBarRect;

        [SerializeField, Tooltip("Authored armor plate views ordered left-to-right (Plate 1, Plate 2, Plate 3).")]
        private ArmorPlateItemView[] _plateViews;

        private Tween _shakeTween;

        private void Awake()
        {
            if (_baseHealthBarRect == null)
            {
                throw new InvalidOperationException("ArmorPlatesView requires an authored base health bar RectTransform reference.");
            }

            if (_plateViews == null || _plateViews.Length != ArmorConstants.MAX_ARMOR_LEVEL)
            {
                throw new InvalidOperationException($"ArmorPlatesView requires exactly {ArmorConstants.MAX_ARMOR_LEVEL} authored ArmorPlateItemView elements.");
            }

            for (int i = 0; i < _plateViews.Length; i++)
            {
                if (_plateViews[i] == null)
                {
                    throw new InvalidOperationException($"ArmorPlatesView plate view at index {i} is unassigned.");
                }
            }
        }

        private void OnDisable()
        {
            KillShakeTween();
        }

        private void OnDestroy()
        {
            KillShakeTween();
        }

        public void Initialize(ArmorConfigSO config)
        {
            if (config == null || _baseHealthBarRect == null || _plateViews == null)
            {
                return;
            }

            int baseSiblingIndex = _baseHealthBarRect.GetSiblingIndex();
            for (int i = 0; i < _plateViews.Length; i++)
            {
                if (_plateViews[i] != null)
                {
                    _plateViews[i].transform.SetSiblingIndex(baseSiblingIndex + 1 + i);
                }
            }

            float baseWidth = _baseHealthBarRect.rect.width > 0f ? _baseHealthBarRect.rect.width : _baseHealthBarRect.sizeDelta.x;
            float baseVisualRightEdge = _baseHealthBarRect.anchoredPosition.x - (baseWidth * _baseHealthBarRect.pivot.x);
            float baseCenterY = _baseHealthBarRect.anchoredPosition.y + (_baseHealthBarRect.rect.height * (0.5f - _baseHealthBarRect.pivot.y));
            float plateHeight = _baseHealthBarRect.sizeDelta.y > 0f ? _baseHealthBarRect.sizeDelta.y : _baseHealthBarRect.rect.height;

            float currentEdge = baseVisualRightEdge;
            for (int i = 0; i < _plateViews.Length; i++)
            {
                if (_plateViews[i] == null)
                {
                    continue;
                }

                float capacity = config.GetPlateCapacity(i);
                float plateWidth = (capacity / config.ReferenceBaseMaxHealth) * config.ReferenceBaseHealthWidth;
                float startX = currentEdge + (plateWidth * config.HorizontalOverlapRatio);
                float posY = baseCenterY;

                _plateViews[i].SetupLayout(new Vector2(startX, posY), new Vector2(plateWidth, plateHeight), new Vector2(1f, 0.5f));
                _plateViews[i].SetColor(config.PlateColor);
                _plateViews[i].SetFillAmount(1f);
                _plateViews[i].SetVisible(false);

                currentEdge = startX - plateWidth;
            }
        }

        public void UpdatePlates(int currentLevel, float[] plateHealth, ArmorConfigSO config)
        {
            if (_plateViews == null || config == null)
            {
                return;
            }

            for (int i = 0; i < _plateViews.Length; i++)
            {
                if (_plateViews[i] == null)
                {
                    continue;
                }

                if (i < currentLevel)
                {
                    _plateViews[i].SetVisible(true);
                    float capacity = config.GetPlateCapacity(i);
                    float currentHp = (plateHealth != null && i < plateHealth.Length) ? plateHealth[i] : 0f;
                    float fill = capacity > 0f ? Mathf.Clamp01(currentHp / capacity) : 0f;
                    _plateViews[i].SetFillAmount(fill);
                }
                else
                {
                    _plateViews[i].SetVisible(false);
                }
            }
        }

        public void PlayDamageShake()
        {
            if (_shakeTween != null && _shakeTween.IsActive())
            {
                _shakeTween.Complete();
                _shakeTween.Kill();
            }

            _shakeTween = transform.DOShakePosition(
                HealthConstants.HEALTH_BAR_SHAKE_DURATION,
                HealthConstants.HEALTH_BAR_SHAKE_STRENGTH,
                HealthConstants.HEALTH_BAR_SHAKE_VIBRATO,
                HealthConstants.HEALTH_BAR_SHAKE_RANDOMNESS,
                HealthConstants.HEALTH_BAR_SHAKE_SNAPPING,
                HealthConstants.HEALTH_BAR_SHAKE_FADE_OUT,
                ShakeRandomnessMode.Harmonic);
        }

        private void KillShakeTween()
        {
            if (_shakeTween != null && _shakeTween.IsActive())
            {
                _shakeTween.Kill();
            }

            _shakeTween = null;
        }
    }
}
