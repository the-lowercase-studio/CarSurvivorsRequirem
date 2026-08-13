using Assets.Scripts.Settings;
using Assets.Scripts.Settings.Resolution;
using Reflex.Core;
using UnityEngine;

namespace Assets.Scripts.ReflexDI
{
    public class MainMenuInstaller : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder builder)
        {
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

