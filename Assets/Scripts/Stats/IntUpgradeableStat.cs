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

        public override void OnAfterDeserialize()
        {
            MinMaxRange = _intMinMaxRange;
            _rangeOfPossibleValuesForUpgrade = _intRangeOfPossibleValuesForUpgrade;

            base.OnAfterDeserialize();
        }
    }
}
