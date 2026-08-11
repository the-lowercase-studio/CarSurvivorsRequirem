using System;
using Assets.Scripts.Skills.UpgradeFlow;
using UnityEngine;

namespace Assets.Scripts.UI.Skills
{
    public struct ClickableButtonData
    {
        public string Text { get; set; }
        public SkillUpgradeRarity Rarity { get; set; }
        public Sprite Icon { get; set; }
        public Action OnClick { get; set; }
    }
}
