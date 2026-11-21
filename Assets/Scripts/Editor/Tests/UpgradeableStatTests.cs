using System;
using System.Collections.Generic;
using Assets.Scripts.Common.Types;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.UpgradeFlow;
using Assets.Scripts.Stats;
using Assets.Scripts.Utils;
using NUnit.Framework;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class UpgradeableStatTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdObjects.Count; i++)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Upgrade_AdditiveFloat_IncreasesValueAndRaisesOnUpgrade()
        {
            FloatUpgradeableStat stat = CreateFloatStat(
                startValue: 1f,
                minValue: 1f,
                maxValue: 4f,
                upgradeMin: 0.5f,
                upgradeMax: 0.8f);

            int eventCount = 0;
            stat.OnUpgrade += (sender, args) =>
            {
                eventCount++;
            };

            stat.Upgrade(0.65f);

            Assert.AreEqual(1.65f, stat.Value, 0.0001f);
            Assert.AreEqual(1.65d, stat.CurrentValue, 0.0001d);
            Assert.IsTrue(stat.CanBeUpgraded);
            Assert.AreEqual(1, eventCount);
        }

        [Test]
        public void Upgrade_AdditiveFloatWithLimit_ClampsToMaxAndDisablesFurtherUpgrades()
        {
            FloatUpgradeableStat stat = CreateFloatStat(
                startValue: 3.5f,
                minValue: 1f,
                maxValue: 4f,
                upgradeMin: 0.5f,
                upgradeMax: 1f,
                hasUnlimitedMaxValue: false);

            stat.Upgrade(0.9f);

            Assert.AreEqual(4f, stat.Value, 0.0001f);
            Assert.IsFalse(stat.CanBeUpgraded);

            int eventCountAfterCap = 0;
            stat.OnUpgrade += (sender, args) =>
            {
                eventCountAfterCap++;
            };

            stat.Upgrade(0.9f);

            Assert.AreEqual(4f, stat.Value, 0.0001f);
            Assert.AreEqual(0, eventCountAfterCap);
        }

        [Test]
        public void Upgrade_AdditiveFloatWithUnlimitedMax_ExceedsMaxAndStaysUpgradeable()
        {
            FloatUpgradeableStat stat = CreateFloatStat(
                startValue: 3.5f,
                minValue: 1f,
                maxValue: 4f,
                upgradeMin: 0.8f,
                upgradeMax: 1.2f,
                hasUnlimitedMaxValue: true);

            stat.Upgrade(1.2f);
            stat.Upgrade(1.2f);

            Assert.AreEqual(5.9f, stat.Value, 0.0001f);
            Assert.IsTrue(stat.CanBeUpgraded);
        }

        [Test]
        public void Upgrade_SubtractMode_DecreasesDownToTargetAndIgnoresUnlimitedMax()
        {
            FloatUpgradeableStat stat = CreateFloatStat(
                startValue: 8f,
                minValue: 8f,
                maxValue: 1f,
                upgradeMin: 2f,
                upgradeMax: 4f,
                isSubtractMode: true,
                hasUnlimitedMaxValue: true);

            stat.Upgrade(3.5f);
            Assert.AreEqual(4.5f, stat.Value, 0.0001f);
            Assert.IsTrue(stat.CanBeUpgraded);

            stat.Upgrade(4.0f);
            Assert.AreEqual(1f, stat.Value, 0.0001f);
            Assert.IsFalse(stat.CanBeUpgraded);
        }

        [Test]
        public void Upgrade_AlwaysUseMinValueForUpgrade_UsesUpgradeRangeMinRegardlessOfDelta()
        {
            IntUpgradeableStat stat = CreateIntStat(
                startValue: 1,
                minValue: 1,
                maxValue: 4,
                upgradeMin: 1,
                upgradeMax: 3,
                alwaysUseMinValueForUpgrade: true);

            stat.Upgrade(3f);

            Assert.AreEqual(2, stat.Value);
            Assert.IsTrue(stat.CanBeUpgraded);
        }

        [Test]
        public void Upgrade_AdditiveInt_AddsAndClampsExactIntegers()
        {
            IntUpgradeableStat stat = CreateIntStat(
                startValue: 5,
                minValue: 5,
                maxValue: 16,
                upgradeMin: 4,
                upgradeMax: 7,
                hasUnlimitedMaxValue: false);

            stat.Upgrade(6f);
            Assert.AreEqual(11, stat.Value);
            Assert.IsTrue(stat.CanBeUpgraded);

            stat.Upgrade(7f);
            Assert.AreEqual(16, stat.Value);
            Assert.IsFalse(stat.CanBeUpgraded);
        }

        [Test]
        public void DeepCopyUtility_ClonesStatWithoutSharingMutableStateAndPreservesSpriteIcon()
        {
            Sprite icon = CreateTestSprite();
            FloatUpgradeableStat source = CreateFloatStat(
                startValue: 8f,
                minValue: 8f,
                maxValue: 1f,
                upgradeMin: 2f,
                upgradeMax: 4f,
                isSubtractMode: true,
                hasUnlimitedMaxValue: false,
                unit: StatsUnits.Seconds,
                overrideRarity: true,
                rarity: SkillUpgradeRarity.UltraRare,
                icon: icon);

            FloatUpgradeableStat copy = DeepCopyUtility.DeepCopy(source);

            Assert.AreNotSame(source, copy);
            Assert.AreNotSame(source.MinMaxRange, copy.MinMaxRange);
            Assert.AreSame(icon, copy.Icon);
            Assert.AreEqual(StatsUnits.Seconds, copy.Unit);
            Assert.IsTrue(copy.IsSubstractModeOn);
            Assert.IsTrue(copy.OverrideDefaultRarity);
            Assert.AreEqual(SkillUpgradeRarity.UltraRare, copy.Rarity);

            copy.Upgrade(3f);

            Assert.AreEqual(5f, copy.Value, 0.0001f);
            Assert.AreEqual(8f, source.Value, 0.0001f);
            Assert.IsTrue(source.CanBeUpgraded);
        }

        [Test]
        public void OnAfterDeserialize_SetsStartingValueToMinAndComputesCanBeUpgraded()
        {
            FloatUpgradeableStat equalBounds = CreateFloatStat(
                startValue: 99f,
                minValue: 2f,
                maxValue: 2f,
                upgradeMin: 1f,
                upgradeMax: 1f,
                hasUnlimitedMaxValue: false);

            equalBounds.OnAfterDeserialize();

            Assert.AreEqual(2f, equalBounds.Value, 0.0001f);
            Assert.IsFalse(equalBounds.CanBeUpgraded);

            FloatUpgradeableStat equalBoundsUnlimited = CreateFloatStat(
                startValue: 99f,
                minValue: 2f,
                maxValue: 2f,
                upgradeMin: 1f,
                upgradeMax: 1f,
                hasUnlimitedMaxValue: true);

            equalBoundsUnlimited.OnAfterDeserialize();

            Assert.AreEqual(2f, equalBoundsUnlimited.Value, 0.0001f);
            Assert.IsTrue(equalBoundsUnlimited.CanBeUpgraded);

            FloatUpgradeableStat nonEqualAdditive = CreateFloatStat(
                startValue: 99f,
                minValue: 2f,
                maxValue: 5f,
                upgradeMin: 0.5f,
                upgradeMax: 1f,
                hasUnlimitedMaxValue: false);

            nonEqualAdditive.OnAfterDeserialize();

            Assert.AreEqual(2f, nonEqualAdditive.Value, 0.0001f);
            Assert.IsTrue(nonEqualAdditive.CanBeUpgraded);

            FloatUpgradeableStat subtractStat = CreateFloatStat(
                startValue: 1f,
                minValue: 8f,
                maxValue: 1f,
                upgradeMin: 1f,
                upgradeMax: 2f,
                isSubtractMode: true,
                hasUnlimitedMaxValue: false);

            subtractStat.OnAfterDeserialize();

            Assert.AreEqual(8f, subtractStat.Value, 0.0001f);
            Assert.IsTrue(subtractStat.CanBeUpgraded);
        }

        [Test]
        public void RestoreRuntimeState_RestoresFloatAndRoundedIntAndAllowsFurtherUpgrades()
        {
            FloatUpgradeableStat floatStat = CreateFloatStat(
                startValue: 8f,
                minValue: 8f,
                maxValue: 1f,
                upgradeMin: 2f,
                upgradeMax: 4f,
                isSubtractMode: true,
                hasUnlimitedMaxValue: false);

            floatStat.RestoreRuntimeState(3.5d, true);

            Assert.AreEqual(3.5f, floatStat.Value, 0.0001f);
            Assert.IsTrue(floatStat.CanBeUpgraded);

            floatStat.Upgrade(2.0f);
            Assert.AreEqual(1.5f, floatStat.Value, 0.0001f);
            Assert.IsTrue(floatStat.CanBeUpgraded);

            floatStat.RestoreRuntimeState(1.0d, false);
            Assert.AreEqual(1.0f, floatStat.Value, 0.0001f);
            Assert.IsFalse(floatStat.CanBeUpgraded);

            IntUpgradeableStat intStat = CreateIntStat(
                startValue: 5,
                minValue: 5,
                maxValue: 25,
                upgradeMin: 4,
                upgradeMax: 7,
                hasUnlimitedMaxValue: true);

            intStat.RestoreRuntimeState(19.6d, true);
            Assert.AreEqual(20, intStat.Value);
            Assert.IsTrue(intStat.CanBeUpgraded);

            intStat.RestoreRuntimeState(19.4d, true);
            Assert.AreEqual(19, intStat.Value);
            Assert.IsTrue(intStat.CanBeUpgraded);

            intStat.Upgrade(6f);
            Assert.AreEqual(25, intStat.Value);
        }

        [Test]
        public void GetWhatPercentOfValueIsUpgradeValue_ReturnsExpectedRoundedPercentage()
        {
            FloatUpgradeableStat stat = CreateFloatStat(
                startValue: 1.5f,
                minValue: 1f,
                maxValue: 3f,
                upgradeMin: 0.5f,
                upgradeMax: 1.2f);

            float percent = stat.GetWhatPercentOfValueIsUpgradeValue(0.75f);

            Assert.AreEqual(50f, percent, 0.0001f);
        }

        private Sprite CreateTestSprite()
        {
            Texture2D texture = new Texture2D(2, 2);
            _createdObjects.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            _createdObjects.Add(sprite);
            return sprite;
        }

        private static FloatUpgradeableStat CreateFloatStat(
            float startValue,
            float minValue,
            float maxValue,
            float upgradeMin,
            float upgradeMax,
            bool isSubtractMode = false,
            bool hasUnlimitedMaxValue = false,
            bool alwaysUseMinValueForUpgrade = false,
            StatsUnits unit = StatsUnits.None,
            bool overrideRarity = false,
            SkillUpgradeRarity rarity = SkillUpgradeRarity.Common,
            Sprite icon = null)
        {
            FloatValueRange minMaxRange = new FloatValueRange(minValue, maxValue);
            FloatValueRange upgradeRange = new FloatValueRange(upgradeMin, upgradeMax);
            FloatUpgradeableStat stat = new FloatUpgradeableStat(
                startValue,
                maxValue,
                minMaxRange,
                upgradeRange,
                alwaysUseMinValueForUpgrade);

            stat.RestoreRuntimeState(startValue, true);
            stat.SetIcon(icon);

            SetBaseAutoProperty(stat, nameof(FloatUpgradeableStat.IsSubstractModeOn), isSubtractMode);
            SetBaseAutoProperty(stat, nameof(FloatUpgradeableStat.Unit), unit);
            SetBaseAutoProperty(stat, nameof(FloatUpgradeableStat.OverrideDefaultRarity), overrideRarity);
            SetBaseAutoProperty(stat, nameof(FloatUpgradeableStat.Rarity), rarity);
            SetPrivateField(typeof(UpgradeableStat<float>), stat, "_hasUnlimitedMaxValue", hasUnlimitedMaxValue);
            return stat;
        }

        private static IntUpgradeableStat CreateIntStat(
            int startValue,
            int minValue,
            int maxValue,
            int upgradeMin,
            int upgradeMax,
            bool hasUnlimitedMaxValue = false,
            bool alwaysUseMinValueForUpgrade = false)
        {
            IntValueRange minMaxRange = new IntValueRange(minValue, maxValue);
            IntValueRange upgradeRange = new IntValueRange(upgradeMin, upgradeMax);
            IntUpgradeableStat stat = new IntUpgradeableStat(
                startValue,
                maxValue,
                minMaxRange,
                upgradeRange,
                alwaysUseMinValueForUpgrade);

            stat.RestoreRuntimeState(startValue, true);
            SetPrivateField(typeof(UpgradeableStat<int>), stat, "_hasUnlimitedMaxValue", hasUnlimitedMaxValue);
            return stat;
        }

        private static void SetBaseAutoProperty<T>(UpgradeableStat<T> target, string propertyName, object value)
            where T : struct, IComparable<T>, IConvertible
        {
            var prop = typeof(UpgradeableStat<T>).GetProperty(propertyName);
            prop.SetValue(target, value);
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            var field = ownerType.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
