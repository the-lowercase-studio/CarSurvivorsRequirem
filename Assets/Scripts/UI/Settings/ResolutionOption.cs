using System.Collections.Generic;
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
            SerializableResolution current = _resolutionSetting.GetValueOrStoredDefault();
            int targetIndex = 0;
            int index = 0;
            foreach (SerializableResolution res in ScreenSerializableResolutionHelper.GetAvailableResolutions())
            {
                if (res.Equals(current))
                {
                    targetIndex = index;
                    break;
                }
                index++;
            }

            _resolutionDropdown.SetValueWithoutNotify(targetIndex);
        }

        public void PerformValueChange(int value)
        {
            int index = 0;
            foreach (SerializableResolution res in ScreenSerializableResolutionHelper.GetAvailableResolutions())
            {
                if (index == value)
                {
                    _resolutionSetting.SaveValue(res);
                    _resolutionSetting.Load();
                    return;
                }
                index++;
            }
        }

        private void SetDropdownOptions()
        {
            _resolutionDropdown.ClearOptions();

            List<string> options = new List<string>();
            foreach (SerializableResolution r in ScreenSerializableResolutionHelper.GetAvailableResolutions())
            {
                options.Add($"{r.Width} x {r.Height}");
            }

            _resolutionDropdown.AddOptions(options);
        }
    }
}
