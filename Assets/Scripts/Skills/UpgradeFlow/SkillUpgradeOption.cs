using System;

namespace Assets.Scripts.Skills.UpgradeFlow
{
    public readonly struct SkillUpgradeOption
    {
        public SkillUpgradeOption(string text, Action apply, SkillUpgradeRarity rarity)
        {
            Text = text;
            Apply = apply;
            Rarity = rarity;
        }

        public string Text { get; }
        public Action Apply { get; }
        public SkillUpgradeRarity Rarity { get; }
    }
}
