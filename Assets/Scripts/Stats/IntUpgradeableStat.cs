using System;
using Assets.Scripts.Common.Types;
using UnityEngine;

namespace Assets.Scripts.Stats
{
    // For unity serialization we need to use nongeneric class.
    [Serializable]
    public class IntUpgradeableStat : UpgradeableStat<int>
    {
        [SerializeField] private IntValueRange _intMinMaxRange;
        [SerializeField] private IntValueRange _intRangeOfPossibleValuesForUpgrade;

        public IntUpgradeableStat(
            int value,
            int maxValue,
            IntValueRange minMaxRange,
            IntValueRange rangeOfPossibleValuesForUpgrade,
            bool alwaysUseMinValueForUpgrade = false)
            : base(value, minMaxRange, rangeOfPossibleValuesForUpgrade, alwaysUseMinValueForUpgrade)
        {
            _intMinMaxRange = minMaxRange;
            _intRangeOfPossibleValuesForUpgrade = rangeOfPossibleValuesForUpgrade;
        }

        public IntUpgradeableStat(
            int value,
            int maxValue,
            bool alwaysUseMinValueForUpgrade = false)
            : base(value, alwaysUseMinValueForUpgrade)
        {
            MinMaxRange = _intMinMaxRange;
            _rangeOfPossibleValuesForUpgrade = _intRangeOfPossibleValuesForUpgrade;
        }

        public override UpgradeableStat<int> Clone()
        {
            IntValueRange clonedMinMaxRange = _intMinMaxRange != null
                ? new IntValueRange(_intMinMaxRange.Min, _intMinMaxRange.Max)
                : (MinMaxRange != null ? new IntValueRange(MinMaxRange.Min, MinMaxRange.Max) : null);

            IntValueRange clonedUpgradeRange = _intRangeOfPossibleValuesForUpgrade != null
                ? new IntValueRange(_intRangeOfPossibleValuesForUpgrade.Min, _intRangeOfPossibleValuesForUpgrade.Max)
                : (_rangeOfPossibleValuesForUpgrade != null ? new IntValueRange(_rangeOfPossibleValuesForUpgrade.Min, _rangeOfPossibleValuesForUpgrade.Max) : null);

            IntUpgradeableStat clone = new IntUpgradeableStat(
                Value,
                0,
                clonedMinMaxRange,
                clonedUpgradeRange,
                AlwaysUseMinValueForUpgrade);

            clone._intMinMaxRange = clonedMinMaxRange;
            clone._intRangeOfPossibleValuesForUpgrade = clonedUpgradeRange;
            clone.MinMaxRange = clonedMinMaxRange;
            clone._rangeOfPossibleValuesForUpgrade = clonedUpgradeRange;

            CopyBasePropertiesTo(clone);

            return clone;
        }

        public override void OnAfterDeserialize()
        {
            MinMaxRange = _intMinMaxRange;
            _rangeOfPossibleValuesForUpgrade = _intRangeOfPossibleValuesForUpgrade;

            base.OnAfterDeserialize();
        }
    }
}
