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

        private void Awake()
        {
            _slider = GetComponent<Slider>();
        }

        private void OnEnable()
        {
            _slider.maxValue = _health.MaxHealth;
            _slider.value = _health.MaxHealth;
            _health.OnHealthChange += UpdateSlider_OnHealthChange;

            if (_shakeOnHealthDecrease)
            {
                _health.OnHealthDecreased += Health_OnHealthDecreased;
            }
        }

        private void OnDisable()
        {
            _health.OnHealthChange -= UpdateSlider_OnHealthChange;

            if (_shakeOnHealthDecrease)
            {
                _health.OnHealthDecreased -= Health_OnHealthDecreased;
            }
        }

        private void UpdateSlider_OnHealthChange(object sender, System.EventArgs e)
        {
            _fillImage.color = _gradient.Evaluate(_health.CurrentHealth / _health.MaxHealth);
            _slider.value = _health.CurrentHealth;
        }

        private void Health_OnHealthDecreased(object sender, System.EventArgs e)
        {
            const float DURATION = 0.1f;
            const float STRENGTH = 0.14f;
            const float RANDOMNESS = 90f;
            const int VIBRATO = 3;
            const bool SNAPPING = false;
            const bool FADE_OUT = true;

            transform.DOShakePosition(DURATION,
                                      STRENGTH,
                                      VIBRATO,
                                      RANDOMNESS,
                                      SNAPPING,
                                      FADE_OUT,
                                      ShakeRandomnessMode.Harmonic);
        }
    }
}
