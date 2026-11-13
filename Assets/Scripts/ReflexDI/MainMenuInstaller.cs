using System;
using Assets.Scripts.Settings;
using Assets.Scripts.Settings.Resolution;
using Assets.Scripts.UI.Input;
using Reflex.Core;
using UnityEngine;

namespace Assets.Scripts.ReflexDI
{
    public class MainMenuInstaller : MonoBehaviour, IInstaller
    {
        [SerializeField] private UiInputScope _uiInputScope;

        public void InstallBindings(ContainerBuilder builder)
        {
            if (_uiInputScope == null)
            {
                throw new InvalidOperationException($"[{nameof(MainMenuInstaller)}] _uiInputScope is unassigned.");
            }

            builder.AddSingleton(_uiInputScope);

            //Settings
            builder.AddScoped(
                typeof(AudioVolumeSetting),
                typeof(ISetting<AudioVolumeSetting, float>),
                typeof(ISettingLoader)
            );

            builder.AddScoped(
                typeof(GraphicSetting),
                typeof(ISetting<GraphicSetting, string>),
                typeof(ISettingLoader)
            );

            builder.AddScoped(
                typeof(FullScreenSetting),
                typeof(ISetting<FullScreenSetting, FullScreenMode>),
                typeof(ISettingLoader)
            );

            builder.AddScoped(
                typeof(ResolutionSetting),
                typeof(ISetting<ResolutionSetting, SerializableResolution>),
                typeof(ISettingLoader)
            );

            builder.AddScoped(
                typeof(DamageNumbersSetting),
                typeof(ISetting<DamageNumbersSetting, bool>),
                typeof(ISettingLoader)
            );
        }
    }
}

