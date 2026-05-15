using System.Linq;
using Assets.Scripts.Settings;
using Assets.Scripts.Settings.Resolution;
using Reflex.Attributes;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Settings
{
    public class ResolutionOption : MonoBehaviour, IOptionComponent<int>
    {
        [Inject] private readonly ISetting<ResolutionSetting, SerializableResolution> _resolutionSetting;

        [SerializeField] private TMP_Dropdown _resolutionDropdown;

        private void Awake()
        {
            SetDropdownOptions();
        }

        private void OnEnable()
        {
            LoadComponent();
            _resolutionDropdown.onValueChanged.AddListener(PerformValueChange);
        }

        private void OnDisable()
        {
            _resolutionDropdown.onValueChanged.RemoveListener(PerformValueChange);
        }

        public void LoadComponent()
        {
            _resolutionDropdown.SetValueWithoutNotify(
                ScreenSerializableResolutionHelper.GetAvailableResolutions()
                .ToList()
                .IndexOf(_resolutionSetting.GetValueOrStoredDefault())
            );
        }

        public void PerformValueChange(int value)
        {
            _resolutionSetting.SaveValue(
                ScreenSerializableResolutionHelper.GetAvailableResolutions().ToList()[value]
            );

            _resolutionSetting.Load();
        }

        private void SetDropdownOptions()
        {
            _resolutionDropdown.ClearOptions();

            var options = ScreenSerializableResolutionHelper.GetAvailableResolutions()
                .Select(r => $"{r.Width} x {r.Height}")
                .ToList();

            _resolutionDropdown.AddOptions(options);
        }
    }
}
