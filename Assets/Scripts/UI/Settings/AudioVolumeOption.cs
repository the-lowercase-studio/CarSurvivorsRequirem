using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Reflex.Attributes;
using Assets.Scripts.Settings;

namespace Assets.Scripts.UI.Settings
{
    public class AudioVolumeOption : MonoBehaviour, IOptionComponent<float>
    {
        [Inject] private readonly ISetting<AudioVolumeSetting, float> _audioVolumeSetting = null;

        [SerializeField] private TextMeshProUGUI _volumeText;
        [SerializeField] private Slider _slider;

        private float _volume;

        private void OnEnable()
        {
            LoadComponent();
            _slider.onValueChanged.AddListener(PerformValueChange);
        }

        private void OnDisable()
        {
            _slider.onValueChanged.RemoveListener(PerformValueChange);
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

        private void UpdateText()
        {
            _volumeText.text = $"{Mathf.Round(_slider.value * 100)}%";
        }

        private float SliderValueToVolumeValue()
        {
            return Mathf.Log10(_slider.value) * 20f;
        }

        private float VolumeValueToSliderValue()
        {
            return Mathf.Pow(10f, _volume / 20f);
        }
    }
}
