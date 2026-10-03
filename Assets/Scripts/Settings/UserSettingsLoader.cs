using System.Collections.Generic;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.Settings
{
    public sealed class UserSettingsLoader : MonoBehaviour
    {
        [Inject] private readonly IEnumerable<ISettingLoader> _settingLoaders = null;

        public void Awake()
        {
            foreach (var settingLoader in _settingLoaders)
            {
                settingLoader.Load();
            }
        }
    }
}