using UnityEngine;

namespace Assets.Scripts.PowerUps.Constants
{
    public static class BubbleShieldShaderConstants
    {
        public static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        public static readonly int ALPHA_ID = Shader.PropertyToID("_Alpha");
        public static readonly int FRESNEL_POWER_ID = Shader.PropertyToID("_FresnelPower");
        public static readonly int RIM_INTENSITY_ID = Shader.PropertyToID("_RimIntensity");
    }
}
