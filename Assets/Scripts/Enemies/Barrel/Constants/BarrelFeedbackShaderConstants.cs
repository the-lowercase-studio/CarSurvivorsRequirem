using UnityEngine;

namespace Assets.Scripts.Enemies.Barrel.Constants
{
    public static class BarrelFeedbackShaderConstants
    {
        public const string EMISSION_KEYWORD = "_EMISSION";
        public static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        public static readonly int COLOR_ID = Shader.PropertyToID("_Color");
        public static readonly int EMISSION_COLOR_ID = Shader.PropertyToID("_EmissionColor");
    }
}
