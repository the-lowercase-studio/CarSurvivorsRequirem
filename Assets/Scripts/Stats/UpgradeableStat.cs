using System;
using Assets.Scripts.Common.Types;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.UpgradeFlow;
using UnityEngine;

namespace Assets.Scripts.Stats
{
    public interface IUpgradeableStat
    {
        bool CanBeUpgraded { get; }
        bool HasUnlimitedMaxValue { get; }
        bool IsSubstractModeOn { get; }
        bool AlwaysUseMinValueForUpgrade { get; }
        bool IsIntegerUpgradeRange { get; }
        float UpgradeRangeMin { get; }
        float UpgradeRangeMax { get; }
        bool OverrideDefaultRarity { get; }
        SkillUpgradeRarity Rarity { get; }
        Sprite Icon { get; }
        StatsUnits Unit { get; }

        void SetIcon(Sprite icon);
        void Upgrade(float upgradeValue);
        float GetUpgradeValueBasedOnUpdateRange();
        float GetWhatPercentOfValueIsUpgradeValue(float upgradeValue);

        event EventHandler OnUpgrade;
    }

    [Serializable]
    public abstract class UpgradeableStat<T> : IUpgradeableStat, ISerializationCallbackReceiver
        where T : struct, IComparable<T>, IConvertible
    {
        [field: SerializeField] public Sprite Icon { get; protected set; }
        [field: SerializeField] public bool IsSubstractModeOn { get; protected set; }
        [field: SerializeField] public StatsUnits Unit { get; protected set; }
        [field: SerializeField] public bool OverrideDefaultRarity { get; protected set; }
        [field: SerializeField] public SkillUpgradeRarity Rarity { get; protected set; }
        [field: SerializeField, HideInInspector] public bool CanBeUpgraded { get; protected set; } = true;
        [field: SerializeField, HideInInspector] public T Value { get; protected set; }

        [SerializeField] private bool _alwaysUseMinValueForUpgrade;
        [SerializeField] private bool _hasUnlimitedMaxValue;

        public ValueRange<T> MinMaxRange { get; protected set; }
        protected ValueRange<T> _rangeOfPossibleValuesForUpgrade;

        public bool HasUnlimitedMaxValue => _hasUnlimitedMaxValue;
        public bool AlwaysUseMinValueForUpgrade => _alwaysUseMinValueForUpgrade;
        public bool IsIntegerUpgradeRange => typeof(T) == typeof(int);
        public float UpgradeRangeMin => _rangeOfPossibleValuesForUpgrade is null
            ? 0f
            : Convert.ToSingle(_rangeOfPossibleValuesForUpgrade.Min);
        public float UpgradeRangeMax => _rangeOfPossibleValuesForUpgrade is null
            ? 0f
            : Convert.ToSingle(_rangeOfPossibleValuesForUpgrade.Max);

        public event EventHandler OnUpgrade;

        public UpgradeableStat(
            T value,
            bool alwaysUseMinValueForUpgrade = false)
        {
            Value = value;
            _alwaysUseMinValueForUpgrade = alwaysUseMinValueForUpgrade;
        }

        public UpgradeableStat(
            T value,
            ValueRange<T> minMaxRange,
            ValueRange<T> rangeOfPossibleValuesForUpgrade,
            bool alwaysUseMinValueForUpgrade = false) : this(value, alwaysUseMinValueForUpgrade)
        {
            MinMaxRange = minMaxRange;
            _rangeOfPossibleValuesForUpgrade = rangeOfPossibleValuesForUpgrade;
        }

        public void SetIcon(Sprite icon)
        {
            Icon = icon;
        }

        public virtual void Upgrade(float upgradeValue)
        {
            if (!CanBeUpgraded)
            {
                return;
            }

            float value = Convert.ToSingle(Value);
            float minValue = Convert.ToSingle(MinMaxRange.Min);
            float maxValue = Convert.ToSingle(MinMaxRange.Max);

            float delta = _alwaysUseMinValueForUpgrade ? minValue : upgradeValue;
            delta = IsSubstractModeOn ? -delta : delta;

            float newValue = value + delta;

            if (ShouldApplyMaxValueLimit() && IsValueExceedingOrEqualMaxValue(newValue, maxValue))
            {
                newValue = maxValue;
                CanBeUpgraded = false;
            }

            Value = FromFloatToType(newValue);

            OnUpgrade?.Invoke(this, EventArgs.Empty);
        }

        public float GetUpgradeValueBasedOnUpdateRange()
        {
            return Convert.ToSingle(_rangeOfPossibleValuesForUpgrade.GetRandomValueInRange());
        }

        public float GetWhatPercentOfValueIsUpgradeValue(float upgradeValue)
        {
            return Convert.ToSingle(Math.Round(upgradeValue / Convert.ToSingle(Value) * 100f, 2));
        }

        public virtual void OnBeforeSerialize()
        {
        }

        public virtual void OnAfterDeserialize()
        {
            if (MinMaxRange is null)
            {
                return;
            }

            float maxValueFloat = Convert.ToSingle(MinMaxRange.Max);
            float minValueFloat = Convert.ToSingle(MinMaxRange.Min);

            Value = MinMaxRange.Min;

            CanBeUpgraded = !ShouldApplyMaxValueLimit()
                || !Mathf.Approximately(minValueFloat, maxValueFloat);
        }

        private bool ShouldApplyMaxValueLimit()
        {
            return IsSubstractModeOn || !HasUnlimitedMaxValue;
        }

        private bool IsValueExceedingOrEqualMaxValue(float value, float maxValue)
        {
            return IsValueExceedingMaxValue(value, maxValue) || Mathf.Approximately(value, maxValue);
        }

        private bool IsValueExceedingMaxValue(float value, float maxValue)
        {
            return IsSubstractModeOn && value < maxValue
                || !IsSubstractModeOn && value > maxValue;
        }

        private T FromFloatToType(float value)
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
    }
}

