using System;
using Assets.Scripts.Common.Types;
using UnityEngine;

namespace Assets.Scripts.Stats
{
    // For unity serialization we need to use nongeneric class.
    [Serializable]
    public class FloatUpgradeableStat : UpgradeableStat<float>
    {
        [SerializeField] private FloatValueRange _floatMinMaxRange;
        [SerializeField] private FloatValueRange _floatRangeOfPossibleValuesForUpgrade;

        public FloatUpgradeableStat(
            float value,
            float maxValue,
            FloatValueRange minMaxRange,
            FloatValueRange rangeOfPossibleValuesForUpgrade,
            bool alwaysUseMinValueForUpgrade = false)
            : base(value, minMaxRange, rangeOfPossibleValuesForUpgrade, alwaysUseMinValueForUpgrade)
        {
            _floatMinMaxRange = minMaxRange;
            _floatRangeOfPossibleValuesForUpgrade = rangeOfPossibleValuesForUpgrade;
        }

        public FloatUpgradeableStat(
            float value,
            float maxValue,
            bool alwaysUseMinValueForUpgrade = false)
            : base(value, alwaysUseMinValueForUpgrade)
        {
            MinMaxRange = _floatMinMaxRange;
            _rangeOfPossibleValuesForUpgrade = _floatRangeOfPossibleValuesForUpgrade;
        }

        public override UpgradeableStat<float> Clone()
        {
            FloatValueRange clonedMinMaxRange = _floatMinMaxRange != null
                ? new FloatValueRange(_floatMinMaxRange.Min, _floatMinMaxRange.Max)
                : (MinMaxRange != null ? new FloatValueRange(MinMaxRange.Min, MinMaxRange.Max) : null);

            FloatValueRange clonedUpgradeRange = _floatRangeOfPossibleValuesForUpgrade != null
                ? new FloatValueRange(_floatRangeOfPossibleValuesForUpgrade.Min, _floatRangeOfPossibleValuesForUpgrade.Max)
                : (_rangeOfPossibleValuesForUpgrade != null ? new FloatValueRange(_rangeOfPossibleValuesForUpgrade.Min, _rangeOfPossibleValuesForUpgrade.Max) : null);

            FloatUpgradeableStat clone = new FloatUpgradeableStat(
                Value,
                0f,
                clonedMinMaxRange,
                clonedUpgradeRange,
                AlwaysUseMinValueForUpgrade);

            clone._floatMinMaxRange = clonedMinMaxRange;
            clone._floatRangeOfPossibleValuesForUpgrade = clonedUpgradeRange;
            clone.MinMaxRange = clonedMinMaxRange;
            clone._rangeOfPossibleValuesForUpgrade = clonedUpgradeRange;

            CopyBasePropertiesTo(clone);

            return clone;
        }

        public override void OnAfterDeserialize()
        {
            MinMaxRange = _floatMinMaxRange;
            _rangeOfPossibleValuesForUpgrade = _floatRangeOfPossibleValuesForUpgrade;

            base.OnAfterDeserialize();
        }
    }
}

