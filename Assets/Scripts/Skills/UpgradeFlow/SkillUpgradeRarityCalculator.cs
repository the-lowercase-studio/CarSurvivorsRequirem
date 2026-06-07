using Assets.Scripts.Stats;
using UnityEngine;

namespace Assets.Scripts.Skills.UpgradeFlow
{
    public static class SkillUpgradeRarityCalculator
    {
        private const float RARE_THRESHOLD = 0.5f;
        private const float ULTRA_RARE_THRESHOLD = 0.8f;

        public static SkillUpgradeRarity Calculate(IUpgradeableStat upgradeableStat, float rolledUpgradeValue)
        {
            float upgradeValue = upgradeableStat.AlwaysUseMinValueForUpgrade
                ? upgradeableStat.UpgradeRangeMin
                : rolledUpgradeValue;

            if (TryGetNormalizedRangePosition(upgradeableStat, upgradeValue, out float normalizedRangePosition))
            {
                if (normalizedRangePosition >= ULTRA_RARE_THRESHOLD)
                {
                    return SkillUpgradeRarity.UltraRare;
                }

                if (normalizedRangePosition >= RARE_THRESHOLD)
                {
                    return SkillUpgradeRarity.Rare;
                }
            }

            return SkillUpgradeRarity.Common;
        }

        private static bool TryGetNormalizedRangePosition(
            IUpgradeableStat upgradeableStat,
            float upgradeValue,
            out float normalizedRangePosition)
        {
            float min = upgradeableStat.UpgradeRangeMin;
            float max = upgradeableStat.IsIntegerUpgradeRange
                ? upgradeableStat.UpgradeRangeMax - 1f
                : upgradeableStat.UpgradeRangeMax;

            if (max <= min || Mathf.Approximately(max, min))
            {
                normalizedRangePosition = 0f;
                return false;
            }

            normalizedRangePosition = Mathf.InverseLerp(min, max, upgradeValue);
            return true;
        }
    }
}
