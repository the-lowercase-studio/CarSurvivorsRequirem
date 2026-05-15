using Assets.Scripts.Common.Types;
using System;
using UnityEngine;

namespace Assets.Scripts.Stats
{
    // For unity serialization we need to use nongeneric class.

    [Serializable]
    public class FloatUpgradeableStat : UpgradeableStat<float>
    {
        [SerializeField] private FloatValueRange _floatMinMaxRange;
        [SerializeField] private FloatValueRange _floatRangeOfPossibleValuesForUpgrade;

        public FloatUpgradeableStat(float value, float maxValue, FloatValueRange minMaxRange, FloatValueRange rangeOfPossibleValuesForUpgrade, bool alwaysUseMinValueForUpgrade = false)
            : base(value, minMaxRange, rangeOfPossibleValuesForUpgrade, alwaysUseMinValueForUpgrade)
        {
        }

        public FloatUpgradeableStat(float value, float maxValue, bool alwaysUseMinValueForUpgrade = false)
            : base(value, alwaysUseMinValueForUpgrade)
        {
            MinMaxRange = _floatMinMaxRange;
            _rangeOfPossibleValuesForUpgrade = _floatRangeOfPossibleValuesForUpgrade;
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();

            MinMaxRange = _floatMinMaxRange;
            _rangeOfPossibleValuesForUpgrade = _floatRangeOfPossibleValuesForUpgrade;
        }
    }
}
