using Assets.Scripts.Stats;
using Assets.Scripts.Skills.UpgradeFlow;
using System;
using System.Collections.Generic;
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
        void ResetRuntimeState();
        void SyncDerivedRuntimeState();
        void AppendStatsForDisplay(List<NameUpgradableStatPair> destination);

        IEnumerable<NameUpgradableStatPair> GetUpgradeableStatsThatCanBeUpgraded();
    }

    public abstract class SkillUpgradeableStatsConfig : ScriptableObject, ISkillUpgradeableStatsConfig
    {
        public abstract void ResetRuntimeState();

        public virtual void SyncDerivedRuntimeState()
        {
        }

        public abstract void AppendStatsForDisplay(List<NameUpgradableStatPair> destination);

        public IEnumerable<NameUpgradableStatPair> GetUpgradeableStatsThatCanBeUpgraded()
        {
            List<NameUpgradableStatPair> upgradeableStats = new();

            PropertyInfo[] allProperties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < allProperties.Length; i++)
            {
                PropertyInfo propertyInfo = allProperties[i];
                if (!typeof(IUpgradeableStat).IsAssignableFrom(propertyInfo.PropertyType))
                {
                    continue;
                }

                if (propertyInfo.GetValue(this) is IUpgradeableStat upgradeableStat
                    && upgradeableStat.CanBeUpgraded)
                {
                    upgradeableStats.Add(new NameUpgradableStatPair(
                        propertyInfo.Name,
                        upgradeableStat,
                        GetRarityOverride(upgradeableStat)));
                }
            }

            return upgradeableStats;
        }

        private SkillUpgradeRarity? GetRarityOverride(IUpgradeableStat upgradeableStat)
        {
            if (upgradeableStat.OverrideDefaultRarity)
            {
                return upgradeableStat.Rarity;
            }

            return null;
        }
    }
}
