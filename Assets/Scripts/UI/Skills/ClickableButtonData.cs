using System;

using Assets.Scripts.Skills.UpgradeFlow;

namespace Assets.Scripts.UI.Skills
{
    public struct ClickableButtonData
    {
        public string Text { get; set; }
        public SkillUpgradeRarity Rarity { get; set; }
        public Action OnClick { get; set; }
    }
}
