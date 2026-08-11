using System;
using UnityEngine;

namespace Assets.Scripts.Skills.UpgradeFlow
{
    public readonly struct SkillUpgradeOption
    {
        public SkillUpgradeOption(string text, Action apply, SkillUpgradeRarity rarity, Sprite icon = null)
        {
            Text = text;
            Apply = apply;
            Rarity = rarity;
            Icon = icon;
        }

        public string Text { get; }
        public Action Apply { get; }
        public SkillUpgradeRarity Rarity { get; }
        public Sprite Icon { get; }
    }
}
