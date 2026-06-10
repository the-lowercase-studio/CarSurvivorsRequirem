using Assets.Scripts.Skills.UpgradeFlow;
using UnityEngine;

namespace Assets.ScriptableObjects.UI
{
    [CreateAssetMenu(fileName = "SkillUpgradeRaritySpriteMapping", menuName = "Scriptable Objects/UI/Skill Upgrade Rarity Sprite Mapping")]
    public class SkillUpgradeRaritySpriteMapping : ScriptableObject
    {
        [SerializeField] private Sprite _commonSprite;
        [SerializeField] private Sprite _outlinedSprite;
        [SerializeField] private Color _commonColor = Color.white;
        [SerializeField] private Color _rareColor = Color.white;
        [SerializeField] private Color _ultraRareColor = Color.white;

        public bool TryGetVisual(SkillUpgradeRarity rarity, out Sprite sprite, out Color color)
        {
            color = rarity switch
            {
                SkillUpgradeRarity.Rare => _rareColor,
                SkillUpgradeRarity.UltraRare => _ultraRareColor,
                _ => _commonColor
            };

            sprite = rarity switch
            {
                SkillUpgradeRarity.Rare => _outlinedSprite,
                SkillUpgradeRarity.UltraRare => _outlinedSprite,
                _ => _commonSprite
            };

            return sprite != null;
        }
    }
}
