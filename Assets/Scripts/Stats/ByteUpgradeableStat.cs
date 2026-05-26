using Assets.Scripts.Common.Types;
using System;
using UnityEngine;

namespace Assets.Scripts.Stats
{
    // For unity serialization we need to use nongeneric class.

    [Serializable]
    public class ByteUpgradeableStat : UpgradeableStat<byte>
    {
        [SerializeField] private ByteValueRange _byteMinMaxRange;
        [SerializeField] private ByteValueRange _byteRangeOfPossibleValuesForUpgrade;

        public ByteUpgradeableStat(byte value, byte maxValue, ByteValueRange minMaxRange, ByteValueRange rangeOfPossibleValuesForUpgrade, bool alwaysUseMinValueForUpgrade = false)
            : base(value, minMaxRange, rangeOfPossibleValuesForUpgrade, alwaysUseMinValueForUpgrade)
        {
        }

        public ByteUpgradeableStat(byte value, byte maxValue, bool alwaysUseMinValueForUpgrade = false)
            : base(value, alwaysUseMinValueForUpgrade)
        {
            MinMaxRange = _byteMinMaxRange;
            _rangeOfPossibleValuesForUpgrade = _byteRangeOfPossibleValuesForUpgrade;
        }

        public override void OnAfterDeserialize()
        {
            MinMaxRange = _byteMinMaxRange;
            _rangeOfPossibleValuesForUpgrade = _byteRangeOfPossibleValuesForUpgrade;

            base.OnAfterDeserialize();
        }
    }
}
