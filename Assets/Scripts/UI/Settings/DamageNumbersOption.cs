using Assets.Scripts.Settings;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Settings
{
    public class DamageNumbersOption : MonoBehaviour, IOptionComponent<bool>
    {
        [Inject] private readonly ISetting<DamageNumbersSetting, bool> _damageNumbersSetting = null;

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
            _damageNumbersSetting.Load();
            _toggle.SetIsOnWithoutNotify(_damageNumbersSetting.GetValueOrStoredDefault());
        }

        public void PerformValueChange(bool value)
        {
            _damageNumbersSetting.SaveValue(value);
            _damageNumbersSetting.Load();
        }
    }
}
