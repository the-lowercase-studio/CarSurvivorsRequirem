using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.ScriptableObjects.Player.Skills;
using Assets.ScriptableObjects.Skills;
using Assets.Scripts.Common.Types;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.Constants;
using Assets.Scripts.Skills.UpgradeFlow;
using Assets.Scripts.Stats;
using NUnit.Framework;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class SkillUpgradeFlowTests
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
        public void SkillUpgradeFlow_ReservesNewSkillSlots_FallsBackAtMaxCap_AndEvaluatesJit()
        {
            TestSkillsRegistry registry = new TestSkillsRegistry();
            TestUpgradeableSkill skill1 = CreateTestSkill("SkillOne", initialized: true);
            TestUpgradeableSkill skill2 = CreateTestSkill("SkillTwo", initialized: false);
            TestUpgradeableSkill skill3 = CreateTestSkill("SkillThree", initialized: false);
            TestUpgradeableSkill skill4 = CreateTestSkill("SkillFour", initialized: false);
            registry.SetSkills(skill1, skill2, skill3, skill4);

            SkillUpgradeFlow flow = new SkillUpgradeFlow();

            Assert.IsTrue(flow.QueueRandomNewSkillRequest(registry));
            Assert.IsTrue(flow.QueueRandomNewSkillRequest(registry));
            Assert.IsFalse(flow.QueueRandomNewSkillRequest(registry));

            flow.QueueRandomSkillUpgradeRequest(registry);

            Assert.IsTrue(flow.TryGetNextRequest(registry, out SkillUpgradeRequest firstChoice));
            Assert.AreEqual(SkillUpgradeRequestType.NewSkillChoice, firstChoice.RequestType);
            Assert.AreEqual(SkillConstants.NEW_SKILL_CHOICE_COUNT, firstChoice.SkillChoices.Count);
            registry.InitializeSkill(firstChoice.SkillChoices[0]);

            Assert.IsTrue(flow.TryGetNextRequest(registry, out SkillUpgradeRequest secondChoice));
            Assert.AreEqual(SkillUpgradeRequestType.NewSkillChoice, secondChoice.RequestType);
            Assert.AreEqual(SkillConstants.NEW_SKILL_CHOICE_COUNT, secondChoice.SkillChoices.Count);
            registry.InitializeSkill(secondChoice.SkillChoices[0]);

            Assert.AreEqual(SkillConstants.MAX_ACTIVE_SKILLS, registry.InitializedSkillsCount);

            Assert.IsTrue(flow.TryGetNextRequest(registry, out SkillUpgradeRequest upgradeReq));
            Assert.AreEqual(SkillUpgradeRequestType.UpgradeSkill, upgradeReq.RequestType);
            Assert.IsNotNull(upgradeReq.UpgradeableSkill);
            Assert.IsTrue(upgradeReq.UpgradeableSkill.IsInitialized());
        }

        [Test]
        public void SkillUpgradeFlow_NewSkillChoiceFallsBackToStatUpgrade_WhenPoolOrCapBecomesExhausted()
        {
            TestSkillsRegistry registry = new TestSkillsRegistry();
            TestUpgradeableSkill skill1 = CreateTestSkill("SkillOne", initialized: true);
            TestUpgradeableSkill skill2 = CreateTestSkill("SkillTwo", initialized: false);
            registry.SetSkills(skill1, skill2);

            SkillUpgradeFlow flow = new SkillUpgradeFlow();
            Assert.IsTrue(flow.QueueRandomNewSkillRequest(registry));

            // Exhaust the uninitialized pool before the queued NewSkillChoice is dequeued.
            registry.InitializeSkill(skill2);

            Assert.IsTrue(flow.TryGetNextRequest(registry, out SkillUpgradeRequest fallbackRequest));
            Assert.AreEqual(SkillUpgradeRequestType.UpgradeSkill, fallbackRequest.RequestType);
            Assert.IsNotNull(fallbackRequest.UpgradeableSkill);
            Assert.IsTrue(fallbackRequest.UpgradeableSkill.IsInitialized());
            Assert.Greater(fallbackRequest.UpgradeOptions.Count, 0);
        }

        [Test]
        public void SkillUpgradeFlow_CreateUpgradeOptions_CapsAtMaxOptionsAndDeterministicallyAppliesStatsAndOverrides()
        {
            TestSkillsRegistry registry = new TestSkillsRegistry();
            TestUpgradeableSkill skill = CreateTestSkill("TestSkill", initialized: true);
            TestSkillConfig config = (TestSkillConfig)skill.Config;
            config.Cooldown = CreateFloatStat(8f, 1f, 2f, 4f, isSubtract: true, unlimited: false);
            config.FixedCount = CreateIntStat(1, 2, 1, 3, unlimited: false, alwaysUseMin: true);
            typeof(UpgradeableStat<int>).GetProperty(nameof(IntUpgradeableStat.OverrideDefaultRarity)).SetValue(config.FixedCount, true);
            typeof(UpgradeableStat<int>).GetProperty(nameof(IntUpgradeableStat.Rarity)).SetValue(config.FixedCount, SkillUpgradeRarity.UltraRare);
            config.Power = CreateIntStat(5, 10, 5, 5, unlimited: false);
            config.Extra = CreateFloatStat(1f, 2f, 0.5f, 0.5f, isSubtract: false, unlimited: false);
            registry.SetSkills(skill);

            SkillUpgradeFlow flow = new SkillUpgradeFlow();

            // With 4 eligible stats, option count is capped at MAX_SKILL_UPGRADE_OPTIONS (3).
            flow.QueueRandomSkillUpgradeRequest(registry);
            Assert.IsTrue(flow.TryGetNextRequest(registry, out SkillUpgradeRequest cappedRequest));
            Assert.AreEqual(SkillConstants.MAX_SKILL_UPGRADE_OPTIONS, cappedRequest.UpgradeOptions.Count);

            // Exhaust Extra so exactly 3 stats remain, guaranteeing all 3 appear deterministically.
            config.Extra.RestoreRuntimeState(2d, false);

            int fixedCountUpgradeEvents = 0;
            int cooldownUpgradeEvents = 0;
            config.FixedCount.OnUpgrade += (sender, args) =>
            {
                fixedCountUpgradeEvents++;
            };
            config.Cooldown.OnUpgrade += (sender, args) =>
            {
                cooldownUpgradeEvents++;
            };

            flow.QueueRandomSkillUpgradeRequest(registry);
            Assert.IsTrue(flow.TryGetNextRequest(registry, out SkillUpgradeRequest request));
            Assert.AreEqual(3, request.UpgradeOptions.Count);

            bool foundFixedCountOption = false;
            bool foundCooldownDecreaseOption = false;
            for (int i = 0; i < request.UpgradeOptions.Count; i++)
            {
                SkillUpgradeOption opt = request.UpgradeOptions[i];
                if (opt.Text.Contains("fixed count"))
                {
                    foundFixedCountOption = true;
                    Assert.IsTrue(opt.Text.Contains("Increase") && opt.Text.Contains("by <Color=#F8D61C>1</Color>"));
                    Assert.AreEqual(SkillUpgradeRarity.UltraRare, opt.Rarity);
                    opt.Apply();
                }
                else if (opt.Text.Contains("cooldown"))
                {
                    foundCooldownDecreaseOption = true;
                    Assert.IsTrue(opt.Text.StartsWith("Decrease"));
                    opt.Apply();
                }
            }

            Assert.IsTrue(foundFixedCountOption, "Expected FixedCount option to be present deterministically.");
            Assert.IsTrue(foundCooldownDecreaseOption, "Expected Cooldown option to be present deterministically.");
            Assert.AreEqual(2, config.FixedCount.Value);
            Assert.IsFalse(config.FixedCount.CanBeUpgraded);
            Assert.AreEqual(1, fixedCountUpgradeEvents);
            Assert.Less(config.Cooldown.Value, 8f);
            Assert.AreEqual(1, cooldownUpgradeEvents);

            config.Cooldown.RestoreRuntimeState(1d, false);
            config.FixedCount.RestoreRuntimeState(2d, false);
            config.Power.RestoreRuntimeState(10d, false);
            config.Extra.RestoreRuntimeState(2d, false);

            flow.QueueRandomSkillUpgradeRequest(registry);
            Assert.IsFalse(flow.TryGetNextRequest(registry, out _));
        }

        [Test]
        public void SkillUpgradeRarityCalculator_ComputesThresholdsOverridesAndInclusiveIntRanges()
        {
            FloatUpgradeableStat floatStat = CreateFloatStat(1f, 10f, 2f, 6f, isSubtract: false, unlimited: false);

            Assert.AreEqual(SkillUpgradeRarity.Common, SkillUpgradeRarityCalculator.Calculate(floatStat, 3.9f));
            Assert.AreEqual(SkillUpgradeRarity.Rare, SkillUpgradeRarityCalculator.Calculate(floatStat, 4.0f));
            Assert.AreEqual(SkillUpgradeRarity.Rare, SkillUpgradeRarityCalculator.Calculate(floatStat, 5.1f));
            Assert.AreEqual(SkillUpgradeRarity.UltraRare, SkillUpgradeRarityCalculator.Calculate(floatStat, 5.2f));
            Assert.AreEqual(SkillUpgradeRarity.UltraRare, SkillUpgradeRarityCalculator.Calculate(floatStat, 6.0f));

            IntUpgradeableStat intStat = CreateIntStat(0, 20, 4, 6, unlimited: false);

            Assert.AreEqual(SkillUpgradeRarity.Common, SkillUpgradeRarityCalculator.Calculate(intStat, 4f));
            Assert.AreEqual(SkillUpgradeRarity.Rare, SkillUpgradeRarityCalculator.Calculate(intStat, 5f));
            Assert.AreEqual(SkillUpgradeRarity.UltraRare, SkillUpgradeRarityCalculator.Calculate(intStat, 6f));

            IntUpgradeableStat alwaysMinStat = CreateIntStat(1, 5, 1, 3, unlimited: false, alwaysUseMin: true);
            Assert.AreEqual(SkillUpgradeRarity.Common, SkillUpgradeRarityCalculator.Calculate(alwaysMinStat, 3f));
        }

        [Test]
        public void RandomUpgradeableSkillFinder_SkipsUninitializedAndExhaustedSkills()
        {
            TestSkillsRegistry registry = new TestSkillsRegistry();
            TestUpgradeableSkill uninitialized = CreateTestSkill("Uninit", initialized: false);
            TestUpgradeableSkill exhausted = CreateTestSkill("Exhausted", initialized: true);
            ((TestSkillConfig)exhausted.Config).Cooldown.RestoreRuntimeState(1d, false);
            TestUpgradeableSkill eligible = CreateTestSkill("Eligible", initialized: true);

            registry.SetSkills(uninitialized, exhausted, eligible);

            IUpgradeableSkill found = RandomUpgradeableSkillFinder.Find(registry);

            Assert.AreSame(eligible, found);
        }

        private TestUpgradeableSkill CreateTestSkill(string skillName, bool initialized)
        {
            GameObject go = new GameObject(skillName);
            _createdObjects.Add(go);

            TestUpgradeableSkill skill = go.AddComponent<TestUpgradeableSkill>();
            SkillInfoSO info = ScriptableObject.CreateInstance<SkillInfoSO>();
            _createdObjects.Add(info);
            typeof(SkillInfoSO).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(info, skillName);

            TestSkillConfig config = ScriptableObject.CreateInstance<TestSkillConfig>();
            _createdObjects.Add(config);
            config.Cooldown = CreateFloatStat(8f, 1f, 2f, 4f, isSubtract: true, unlimited: false);

            skill.Setup(info, config, initialized);
            return skill;
        }

        private static FloatUpgradeableStat CreateFloatStat(
            float min,
            float max,
            float upgradeMin,
            float upgradeMax,
            bool isSubtract,
            bool unlimited)
        {
            FloatUpgradeableStat stat = new FloatUpgradeableStat(
                min,
                max,
                new FloatValueRange(min, max),
                new FloatValueRange(upgradeMin, upgradeMax),
                false);
            stat.RestoreRuntimeState(min, true);
            typeof(UpgradeableStat<float>).GetProperty(nameof(FloatUpgradeableStat.IsSubstractModeOn)).SetValue(stat, isSubtract);
            typeof(UpgradeableStat<float>).GetField("_hasUnlimitedMaxValue", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stat, unlimited);
            return stat;
        }

        private static IntUpgradeableStat CreateIntStat(
            int min,
            int max,
            int upgradeMin,
            int upgradeMax,
            bool unlimited,
            bool alwaysUseMin = false)
        {
            IntUpgradeableStat stat = new IntUpgradeableStat(
                min,
                max,
                new IntValueRange(min, max),
                new IntValueRange(upgradeMin, upgradeMax),
                alwaysUseMin);
            stat.RestoreRuntimeState(min, true);
            typeof(UpgradeableStat<int>).GetField("_hasUnlimitedMaxValue", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stat, unlimited);
            return stat;
        }

        private sealed class TestSkillConfig : SkillUpgradeableStatsConfig
        {
            public FloatUpgradeableStat Cooldown { get; set; }
            public IntUpgradeableStat FixedCount { get; set; }
            public IntUpgradeableStat Power { get; set; }
            public FloatUpgradeableStat Extra { get; set; }

            public override void ResetRuntimeState()
            {
            }

            public override void AppendStatsForDisplay(List<NameUpgradableStatPair> destination)
            {
                if (Cooldown != null)
                {
                    destination.Add(new NameUpgradableStatPair(nameof(Cooldown), Cooldown, null));
                }
                if (FixedCount != null)
                {
                    destination.Add(new NameUpgradableStatPair(nameof(FixedCount), FixedCount, null));
                }
                if (Power != null)
                {
                    destination.Add(new NameUpgradableStatPair(nameof(Power), Power, null));
                }
                if (Extra != null)
                {
                    destination.Add(new NameUpgradableStatPair(nameof(Extra), Extra, null));
                }
            }
        }

        private sealed class TestUpgradeableSkill : MonoBehaviour, IUpgradeableSkill
        {
            private bool _isInitialized;

            public SkillInfoSO SkillInfo { get; private set; }
            public ISkillUpgradeableStatsConfig Config { get; private set; }

            public void Setup(SkillInfoSO info, ISkillUpgradeableStatsConfig config, bool initialized)
            {
                SkillInfo = info;
                Config = config;
                _isInitialized = initialized;
            }

            public void Initialize()
            {
                _isInitialized = true;
            }

            public bool IsInitialized()
            {
                return _isInitialized;
            }

            public bool CanBeUpgraded()
            {
                using IEnumerator<NameUpgradableStatPair> enumerator = Config.GetUpgradeableStatsThatCanBeUpgraded().GetEnumerator();
                return enumerator.MoveNext();
            }
        }

        private sealed class TestSkillsRegistry : ISkillsRegistry
        {
            private readonly List<ISkillBase> _skills = new();

            public IReadOnlyList<ISkillBase> Skills
            {
                get { return _skills; }
            }

            public int UninitializedSkillsCount
            {
                get { return GetUninitializedSkills().Count; }
            }

            public int InitializedSkillsCount
            {
                get { return GetInitializedSkills().Count; }
            }

            public event Action<ISkillBase> OnSkillInitialized;

            public void SetSkills(params ISkillBase[] skills)
            {
                _skills.Clear();
                for (int i = 0; i < skills.Length; i++)
                {
                    _skills.Add(skills[i]);
                }
            }

            public IReadOnlyList<ISkillBase> GetInitializedSkills()
            {
                List<ISkillBase> result = new();
                for (int i = 0; i < _skills.Count; i++)
                {
                    if (_skills[i].IsInitialized())
                    {
                        result.Add(_skills[i]);
                    }
                }

                return result;
            }

            public IReadOnlyList<ISkillBase> GetUninitializedSkills()
            {
                List<ISkillBase> result = new();
                for (int i = 0; i < _skills.Count; i++)
                {
                    if (!_skills[i].IsInitialized())
                    {
                        result.Add(_skills[i]);
                    }
                }

                return result;
            }

            public ISkillBase InitializeSkill(ISkillBase skill)
            {
                if (skill != null && !skill.IsInitialized())
                {
                    skill.Initialize();
                    OnSkillInitialized?.Invoke(skill);
                    return skill;
                }

                return null;
            }
        }
    }
}
