using System;
using Assets.Scripts.GameFlow.Config;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.GameFlow
{
    public interface ITierVisualThemeController
    {
        void ApplyActiveTierTheme();
    }

    public class TierVisualThemeController : MonoBehaviour, ITierVisualThemeController
    {
        [Inject] private readonly IRunSessionService _runSessionService = null;
        [Inject] private readonly Volume _postProcessVolume = null;

        [Tooltip("Optional override for scene post-process Volume. Uses injected Volume if unassigned.")]
        [SerializeField] private Volume _postProcessVolumeOverride;

        [Tooltip("Scene directional light adjusted when the active tier overrides lighting.")]
        [SerializeField] private Light _directionalLight;

        [Tooltip("Renderers using the large water material swapped per tier.")]
        [SerializeField] private Renderer[] _waterLargeRenderers;

        [Tooltip("Renderers using the small water material swapped per tier.")]
        [SerializeField] private Renderer[] _waterSmallRenderers;

        private void Start()
        {
            ApplyActiveTierTheme();
        }

        public void ApplyActiveTierTheme()
        {
            if (_runSessionService == null)
            {
                throw new InvalidOperationException($"[{nameof(TierVisualThemeController)}] IRunSessionService was not injected on '{name}'.");
            }

            LoopTierConfig tierConfig = _runSessionService.ActiveTierConfig;
            if (tierConfig == null || tierConfig.VisualTheme == null)
            {
                return;
            }

            TierVisualThemeConfig theme = tierConfig.VisualTheme;

            Volume targetVolume = _postProcessVolumeOverride != null ? _postProcessVolumeOverride : _postProcessVolume;
            if (theme.PostProcessProfile != null && targetVolume != null)
            {
                targetVolume.sharedProfile = theme.PostProcessProfile;
            }

            if (theme.SkyboxMaterial != null)
            {
                RenderSettings.skybox = theme.SkyboxMaterial;
                DynamicGI.UpdateEnvironment();
            }

            if (theme.OverrideDirectionalLight)
            {
                if (_directionalLight == null)
                {
                    throw new InvalidOperationException($"[{nameof(TierVisualThemeController)}] _directionalLight is unassigned on '{name}' while tier '{tierConfig.TierId}' enables directional light override.");
                }

                _directionalLight.color = theme.DirectionalLightColor;
                _directionalLight.intensity = theme.DirectionalLightIntensity;
                _directionalLight.transform.rotation = Quaternion.Euler(theme.DirectionalLightEulerAngles);
            }

            ApplySharedMaterial(_waterLargeRenderers, theme.WaterLargeMaterial);
            ApplySharedMaterial(_waterSmallRenderers, theme.WaterSmallMaterial);
        }

        private static void ApplySharedMaterial(Renderer[] renderers, Material material)
        {
            if (material == null || renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer != null)
                {
                    targetRenderer.sharedMaterial = material;
                }
            }
        }
    }
}
