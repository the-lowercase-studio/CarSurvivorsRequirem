using Assets.Scripts.Stats;
using Assets.Scripts.Skills.UpgradeFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Assets.ScriptableObjects.Player.Skills
{
    public readonly struct NameUpgradableStatPair
    {
        public string Name { get; }
        public IUpgradeableStat UpgradeableStat { get; }
        public SkillUpgradeRarity? RarityOverride { get; }

        public NameUpgradableStatPair(
            string name,
            IUpgradeableStat upgradeableStat,
            SkillUpgradeRarity? rarityOverride)
        {
            Name = name;
            UpgradeableStat = upgradeableStat;
            RarityOverride = rarityOverride;
        }
    }

    public interface ISkillUpgradeableStatsConfig
    {
        public IEnumerable<NameUpgradableStatPair> GetUpgradeableStatsThatCanBeUpgraded();
    }

    public abstract class SkillUpgradeableStatsConfig : ScriptableObject, ISkillUpgradeableStatsConfig
    {
        [SerializeField] private SkillUpgradeStatRarityOverride[] _rarityOverrides;

        public IEnumerable<NameUpgradableStatPair> GetUpgradeableStatsThatCanBeUpgraded()
        {
            List<NameUpgradableStatPair> upgradeableStats = new();

            PropertyInfo[] upgradeableStatsPropertyInfos = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                          .Where(f => typeof(IUpgradeableStat).IsAssignableFrom(f.PropertyType))
                                          .ToArray();

            foreach (PropertyInfo propertyInfo in upgradeableStatsPropertyInfos)
            {
                if (propertyInfo.GetValue(this) is IUpgradeableStat upgradeableStat
                    && upgradeableStat.CanBeUpgraded)
                {
                    upgradeableStats.Add(new NameUpgradableStatPair(
                        propertyInfo.Name,
                        upgradeableStat,
                        GetRarityOverride(propertyInfo.Name)));
                }
            }

            return upgradeableStats;
        }

        private SkillUpgradeRarity? GetRarityOverride(string statName)
        {
            if (_rarityOverrides == null)
            {
                return null;
            }

            foreach (SkillUpgradeStatRarityOverride rarityOverride in _rarityOverrides)
            {
                if (rarityOverride.StatName == statName)
                {
                    return rarityOverride.Rarity;
                }
            }

            return null;
        }
    }

    [Serializable]
    public struct SkillUpgradeStatRarityOverride
    {
        [field: SerializeField] public string StatName { get; private set; }
        [field: SerializeField] public SkillUpgradeRarity Rarity { get; private set; }
    }
}
