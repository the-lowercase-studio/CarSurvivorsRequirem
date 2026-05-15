using UnityEngine;

namespace Assets.Scripts.UI.Level
{
    public static class EaseUtils
    {
        public static float EaseOutQuad(float t)
        {
            return 1 - (1 - t) * (1 - t);
        }

        public static float EaseInOutSine(float t)
        {
            return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
        }
    }
}
