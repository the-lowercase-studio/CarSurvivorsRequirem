using System;
using Assets.Scripts.Skills.UpgradeFlow;
using UnityEngine;

namespace Assets.ScriptableObjects.UI
{
    [CreateAssetMenu(fileName = "SkillUpgradeRaritySpriteMapping", menuName = "Scriptable Objects/UI/Skill Upgrade Rarity Sprite Mapping")]
    public class SkillUpgradeRaritySpriteMapping : ScriptableObject
    {
        [SerializeField] private SkillUpgradeRaritySprite[] _sprites;

        public bool TryGetSprite(SkillUpgradeRarity rarity, out Sprite sprite)
        {
            if (_sprites == null)
            {
                sprite = null;
                return false;
            }

            foreach (SkillUpgradeRaritySprite raritySprite in _sprites)
            {
                if (raritySprite.Rarity == rarity)
                {
                    sprite = raritySprite.Sprite;
                    return sprite != null;
                }
            }

            sprite = null;
            return false;
        }
    }

    [Serializable]
    public struct SkillUpgradeRaritySprite
    {
        [field: SerializeField] public SkillUpgradeRarity Rarity { get; private set; }
        [field: SerializeField] public Sprite Sprite { get; private set; }
    }
}
