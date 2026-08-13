using Assets.Scripts.DamageNumbers;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.Settings.Constants;
using Assets.Scripts.Storage;

namespace Assets.Scripts.Settings
{
    public class DamageNumbersSetting : ISetting<DamageNumbersSetting, bool>
    {
        public bool DefaultValue => true;

        private readonly IEnableDisableFunctionalityTrigger<DamageNumbersSpawner> _damageNumbersFunctionalityTrigger;

        public DamageNumbersSetting(IEnableDisableFunctionalityTrigger<DamageNumbersSpawner> damageNumbersFunctionalityTrigger)
        {
            _damageNumbersFunctionalityTrigger = damageNumbersFunctionalityTrigger;
        }

        public string GetKey()
        {
            return SettingsConstants.DAMAGE_NUMBERS_KEY;
        }

        public bool GetValueOrStoredDefault()
        {
            if (AppStorage.TryGetValue(GetKey(), out bool value))
            {
                return value;
            }

            return DefaultValue;
        }

        public void Load()
        {
            if (GetValueOrStoredDefault())
            {
                _damageNumbersFunctionalityTrigger.EnableFunctionality();
            }
            else
            {
                _damageNumbersFunctionalityTrigger.DisableFunctionality();
            }
        }

        public void SaveValue(bool value)
        {
            AppStorage.SetValue(GetKey(), value);
        }
    }
}

