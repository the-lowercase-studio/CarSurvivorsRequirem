using System;
using Assets.Scripts.HealthSystem.Constants;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.HealthSystem
{
    [RequireComponent(typeof(Slider))]
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Gradient _gradient;
        [SerializeField] private Health _health;
        [SerializeField] private Image _fillImage;
        [SerializeField] private bool _shakeOnHealthDecrease;

        private Slider _slider;
        private Tween _shakeTween;

        private void Awake()
        {
            _slider = GetComponent<Slider>();
        }

        private void OnEnable()
        {
            _slider.maxValue = _health.MaxHealth;
            _slider.value = _health.MaxHealth;
            _health.OnHealthChanged += UpdateSlider_OnHealthChanged;

            if (_shakeOnHealthDecrease)
            {
                _health.OnHealthDecreased += Health_OnHealthDecreased;
            }
        }

        private void OnDisable()
        {
            _health.OnHealthChanged -= UpdateSlider_OnHealthChanged;

            if (_shakeOnHealthDecrease)
            {
                _health.OnHealthDecreased -= Health_OnHealthDecreased;
            }

            _shakeTween?.Kill();
            _shakeTween = null;
        }

        private void OnDestroy()
        {
            _shakeTween?.Kill();
            _shakeTween = null;
        }

        private void UpdateSlider_OnHealthChanged(object sender, EventArgs e)
        {
            _fillImage.color = _gradient.Evaluate(_health.CurrentHealth / _health.MaxHealth);
            _slider.value = _health.CurrentHealth;
        }

        private void Health_OnHealthDecreased(object sender, EventArgs e)
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
    }
}
