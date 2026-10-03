using Assets.Scripts.Settings;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Settings
{
    public class FullScreenOption : MonoBehaviour, IOptionComponent<bool>
    {
        [Inject] private readonly ISetting<FullScreenSetting, FullScreenMode> _fullScreenSetting = null;

        [SerializeField] private Toggle _toggle;

        private void OnEnable()
        {
            LoadComponent();
            _toggle.onValueChanged.AddListener(PerformValueChange);
        }

        private void OnDisable()
        {
            _toggle.onValueChanged.RemoveListener(PerformValueChange);
        }

        public void LoadComponent()
        {
            _fullScreenSetting.Load();
            _toggle.SetIsOnWithoutNotify(
                _fullScreenSetting.GetValueOrStoredDefault() == FullScreenMode.ExclusiveFullScreen);
        }

        public void PerformValueChange(bool value)
        {
            _fullScreenSetting.SaveValue(
                 value ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed
            );
            _fullScreenSetting.Load();
        }
    }
}
