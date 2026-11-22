using System;
using UnityEngine;

namespace Assets.Scripts.DamageNumbers
{
    [Serializable]
    public struct DamageNumberApearance
    {
        public float FontSize;
        public float GrowFontSizeAnimationScaleMultiplier;
        public Color Color;

        public void Deconstruct(out float fontSize, out float growFontSizeAnimationScaleMultiplier, out Color color)
        {
            growFontSizeAnimationScaleMultiplier = GrowFontSizeAnimationScaleMultiplier;
            fontSize = FontSize;
            color = Color;
        }
    }
}
