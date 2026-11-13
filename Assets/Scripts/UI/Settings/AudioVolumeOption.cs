using Assets.Scripts.Input;
using Assets.Scripts.Settings;
using Assets.Scripts.UI.Input;
using Assets.Scripts.UI.Settings.Constants;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Settings
{
    public class AudioVolumeOption : MonoBehaviour, IOptionComponent<float>
    {
        [Inject] private readonly ISetting<AudioVolumeSetting, float> _audioVolumeSetting = null;
        [Inject] private readonly IInputActionSource _inputActionSource = null;
        [Inject] private readonly UiInputScope _uiInputScope = null;

        [SerializeField] private TextMeshProUGUI _volumeText;
        [SerializeField] private Slider _slider;

        private float _volume;
        private int _holdDirection;
        private float _holdElapsed;
        private float _tickAccumulator;

        private void Awake()
        {
            ConfigureSliderRange();
        }

        private void OnEnable()
        {
            ConfigureSliderRange();
            LoadComponent();
            ResetHoldState();
            _slider.onValueChanged.AddListener(PerformValueChange);
        }

        private void OnDisable()
        {
            ResetHoldState();
            _slider.onValueChanged.RemoveListener(PerformValueChange);
        }

        private void Update()
        {
            UpdateAcceleratedSlide();
        }

        public void PerformValueChange(float value)
        {
            _volume = SliderValueToVolumeValue();
            _audioVolumeSetting.SaveValue(_volume);
            _audioVolumeSetting.Load();
            UpdateText();
        }

        public void LoadComponent()
        {
            _audioVolumeSetting.Load();
            _volume = _audioVolumeSetting.GetValueOrStoredDefault();
            _slider.value = VolumeValueToSliderValue();
            UpdateText();
        }

        private void ConfigureSliderRange()
        {
            if (_slider == null)
            {
                return;
            }

            _slider.wholeNumbers = true;
            _slider.minValue = SettingsConstants.VOLUME_MIN_PERCENT;
            _slider.maxValue = SettingsConstants.VOLUME_MAX_PERCENT;
        }

        private void UpdateAcceleratedSlide()
        {
            if (_slider == null
                || _uiInputScope == null
                || _uiInputScope.EventSystem == null
                || _uiInputScope.EventSystem.currentSelectedGameObject != _slider.gameObject
                || _inputActionSource == null
                || !_inputActionSource.TryGetAction(InputActionId.UiNavigate, out InputAction navigateAction)
                || navigateAction == null)
            {
                ResetHoldState();
                return;
            }

            Vector2 navigate = navigateAction.ReadValue<Vector2>();
            int direction = 0;
            if (navigate.x >= SettingsConstants.VOLUME_NAVIGATE_THRESHOLD)
            {
                direction = 1;
            }
            else if (navigate.x <= -SettingsConstants.VOLUME_NAVIGATE_THRESHOLD)
            {
                direction = -1;
            }

            if (direction == 0)
            {
                ResetHoldState();
                return;
            }

            if (direction != _holdDirection)
            {
                _holdDirection = direction;
                _holdElapsed = 0f;
                _tickAccumulator = 0f;
                return;
            }

            _holdElapsed += Time.unscaledDeltaTime;
            if (_holdElapsed < SettingsConstants.VOLUME_HOLD_DELAY_SECONDS)
            {
                return;
            }

            ResolveHoldTier(_holdElapsed, out float tickInterval, out int stepSize);
            _tickAccumulator += Time.unscaledDeltaTime;

            while (_tickAccumulator >= tickInterval)
            {
                _tickAccumulator -= tickInterval;
                float nextPercent = Mathf.Clamp(
                    _slider.value + (_holdDirection * stepSize),
                    SettingsConstants.VOLUME_MIN_PERCENT,
                    SettingsConstants.VOLUME_MAX_PERCENT);
                if (!Mathf.Approximately(nextPercent, _slider.value))
                {
                    _slider.value = nextPercent;
                }
            }
        }

        private void ResetHoldState()
        {
            _holdDirection = 0;
            _holdElapsed = 0f;
            _tickAccumulator = 0f;
        }

        private static void ResolveHoldTier(float holdSeconds, out float tickInterval, out int stepSize)
        {
            if (holdSeconds >= SettingsConstants.VOLUME_TURBO_HOLD_SECONDS)
            {
                tickInterval = SettingsConstants.VOLUME_TURBO_TICK_INTERVAL;
                stepSize = SettingsConstants.VOLUME_TURBO_STEP;
                return;
            }

            if (holdSeconds >= SettingsConstants.VOLUME_FAST_HOLD_SECONDS)
            {
                tickInterval = SettingsConstants.VOLUME_FAST_TICK_INTERVAL;
                stepSize = SettingsConstants.VOLUME_FAST_STEP;
                return;
            }

            tickInterval = SettingsConstants.VOLUME_SLOW_TICK_INTERVAL;
            stepSize = SettingsConstants.VOLUME_SLOW_STEP;
        }

        private void UpdateText()
        {
            _volumeText.text = $"{Mathf.RoundToInt(_slider.value)}%";
        }

        private float SliderValueToVolumeValue()
        {
            float normalized = Mathf.Clamp(
                _slider.value / SettingsConstants.VOLUME_MAX_PERCENT,
                SettingsConstants.VOLUME_MIN_LINEAR,
                1f);
            return Mathf.Log10(normalized) * SettingsConstants.VOLUME_DECIBEL_SCALE;
        }

        private float VolumeValueToSliderValue()
        {
            float normalized = Mathf.Clamp01(Mathf.Pow(10f, _volume / SettingsConstants.VOLUME_DECIBEL_SCALE));
            return Mathf.Round(normalized * SettingsConstants.VOLUME_MAX_PERCENT);
        }
    }
}
