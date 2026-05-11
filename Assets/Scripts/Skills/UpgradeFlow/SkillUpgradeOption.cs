using System;

namespace Assets.Scripts.Skills.UpgradeFlow
{
    public readonly struct SkillUpgradeOption
    {
        public SkillUpgradeOption(string text, Action apply)
        {
            Text = text;
            Apply = apply;
        }

        public string Text { get; }
        public Action Apply { get; }
    }
}
