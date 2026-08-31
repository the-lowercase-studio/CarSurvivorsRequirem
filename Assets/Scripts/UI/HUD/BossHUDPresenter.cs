using System;
using DG.Tweening;
using Assets.Scripts.HealthSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.HUD
{
    public interface IBossHUDPresenter
    {
        void Show(IHealth bossHealth, string bossName);
        void Hide();
    }

    public class BossHUDPresenter : MonoBehaviour, IBossHUDPresenter
    {
        [SerializeField] private GameObject _visual;
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private Image _fillImage;
        [SerializeField] private Gradient _healthGradient;
        [SerializeField] private TextMeshProUGUI _bossTitleText;

        private IHealth _activeHealth;
        private Tween _sliderTween;

        private void Awake()
        {
            _visual.SetActive(false);
        }

        private void OnDisable()
        {
            UnsubscribeHealth();
            KillTweens();
        }

        private void OnDestroy()
        {
            UnsubscribeHealth();
            KillTweens();
        }

        public void Show(IHealth bossHealth, string bossName)
        {
            UnsubscribeHealth();
            KillTweens();

            _activeHealth = bossHealth;

            if (_bossTitleText != null)
            {
                _bossTitleText.text = bossName;
            }

            if (_healthSlider != null && _activeHealth != null)
            {
                _healthSlider.maxValue = _activeHealth.MaxHealth;
                _healthSlider.value = _activeHealth.CurrentHealth;
                UpdateFillColor();
            }

            if (_activeHealth != null)
            {
                _activeHealth.OnHealthChanged += Health_OnHealthChanged;
                _activeHealth.OnNoHealth += Health_OnNoHealth;
            }

            _visual.SetActive(true);
        }

        public void Hide()
        {
            UnsubscribeHealth();
            KillTweens();

            _visual.SetActive(false);
        }

        private void Health_OnHealthChanged(object sender, EventArgs e)
        {
            if (_activeHealth == null || _healthSlider == null)
            {
                return;
            }

            _sliderTween?.Kill();
            _sliderTween = _healthSlider.DOValue(_activeHealth.CurrentHealth, 0.2f).SetEase(Ease.OutQuad);
            UpdateFillColor();
        }

        private void Health_OnNoHealth(object sender, EventArgs e)
        {
            Hide();
        }

        private void UpdateFillColor()
        {
            if (_fillImage != null && _healthGradient != null && _activeHealth != null && _activeHealth.MaxHealth > 0f)
            {
                float ratio = Mathf.Clamp01(_activeHealth.CurrentHealth / _activeHealth.MaxHealth);
                _fillImage.color = _healthGradient.Evaluate(ratio);
            }
        }

        private void UnsubscribeHealth()
        {
            if (_activeHealth != null)
            {
                _activeHealth.OnHealthChanged -= Health_OnHealthChanged;
                _activeHealth.OnNoHealth -= Health_OnNoHealth;
                _activeHealth = null;
            }
        }

        private void KillTweens()
        {
            if (_sliderTween != null && _sliderTween.IsActive())
            {
                _sliderTween.Kill();
            }
            _sliderTween = null;
        }
    }
}
