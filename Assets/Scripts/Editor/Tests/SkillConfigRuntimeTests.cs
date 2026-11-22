using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.ScriptableObjects;
using Assets.ScriptableObjects.Player.Skills;
using Assets.ScriptableObjects.Skills.PlayerSkills.LandmineSkill;
using Assets.ScriptableObjects.Skills.PlayerSkills.MinigunSkill;
using Assets.ScriptableObjects.Skills.PlayerSkills.SawSkill;
using Assets.Scripts.Common.Types;
using Assets.Scripts.Stats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class SkillConfigRuntimeTests
    {
        private const string LANDMINE_ASSET_PATH = "Assets/ScriptableObjects/Skills/PlayerSkills/LandmineSkill/LandmineSkillUpgradeableConfig.asset";
        private const string LASERGUN_ASSET_PATH = "Assets/ScriptableObjects/Skills/PlayerSkills/LasergunSkill/LasergunUpgradeableSkillConfig.asset";
        private const string MINIGUN_ASSET_PATH = "Assets/ScriptableObjects/Skills/PlayerSkills/MinigunSkill/MinigunSkillUpgradeableConfig.asset";
        private const string SAW_ASSET_PATH = "Assets/ScriptableObjects/Skills/PlayerSkills/SawSkill/SawSkillUpgradeableConfig.asset";

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
        public void ResetRuntimeState_CreatesIndependentClonesWithoutMutatingSerializedSources()
        {
            LandmineSkillUpgradeableConfigSO config = CreateLandmineConfig();
            config.ResetRuntimeState();

            FloatUpgradeableStat initialCooldownRef = config.SpawnCooldown;
            initialCooldownRef.Upgrade(3f);

            Assert.AreEqual(5f, config.SpawnCooldown.Value, 0.0001f);

            config.ResetRuntimeState();

            Assert.AreNotSame(initialCooldownRef, config.SpawnCooldown);
            Assert.AreEqual(8f, config.SpawnCooldown.Value, 0.0001f);
            Assert.IsTrue(config.SpawnCooldown.CanBeUpgraded);
        }

        [Test]
        public void AppendStatsForDisplay_IncludesCappedStats_WhileGetUpgradeableFiltersThemOut()
        {
            SawSkillUpgradeableConfigSO sawConfig = CreateSawConfig();
            sawConfig.ResetRuntimeState();

            sawConfig.KnockbackRange.RestoreRuntimeState(4d, false);

            List<NameUpgradableStatPair> displayStats = new();
            sawConfig.AppendStatsForDisplay(displayStats);

            Assert.AreEqual(2, displayStats.Count);
            Assert.AreEqual(nameof(SawSkillUpgradeableConfigSO.KnockbackRange), displayStats[0].Name);
            Assert.AreEqual(nameof(SawSkillUpgradeableConfigSO.Damage), displayStats[1].Name);

            List<NameUpgradableStatPair> upgradeCandidates = new();
            foreach (NameUpgradableStatPair pair in sawConfig.GetUpgradeableStatsThatCanBeUpgraded())
            {
                upgradeCandidates.Add(pair);
            }

            Assert.AreEqual(1, upgradeCandidates.Count);
            Assert.AreEqual(nameof(SawSkillUpgradeableConfigSO.Damage), upgradeCandidates[0].Name);
        }

        [Test]
        public void LasergunSkillSO_RangeAndDamageUpgrades_AndSyncDerivedRuntimeState_UpdateTurretAndProjectileConfigs()
        {
            TurretConfigSO turretConfig = CreateInstance<TurretConfigSO>();
            ProjectileConfigSO projectileConfig = CreateInstance<ProjectileConfigSO>();
            LasergunSkillSO config = CreateLasergunConfig(turretConfig, projectileConfig);

            config.ResetRuntimeState();

            Assert.AreEqual(8f, turretConfig.Range, 0.0001f);
            Assert.AreEqual(8f, projectileConfig.Range, 0.0001f);
            Assert.AreEqual(5, projectileConfig.Damage);

            config.Range.Upgrade(5f);
            config.Damage.Upgrade(7f);

            Assert.AreEqual(13f, turretConfig.Range, 0.0001f);
            Assert.AreEqual(13f, projectileConfig.Range, 0.0001f);
            Assert.AreEqual(12, projectileConfig.Damage);

            config.ResetRuntimeState();
            config.Range.RestoreRuntimeState(16d, true);
            config.Damage.RestoreRuntimeState(22d, true);
            config.SyncDerivedRuntimeState();

            Assert.AreEqual(16f, turretConfig.Range, 0.0001f);
            Assert.AreEqual(16f, projectileConfig.Range, 0.0001f);
            Assert.AreEqual(22, projectileConfig.Damage);
        }

        [Test]
        public void TrackedProjectSkillAssets_HaveExpectedStatCountsValidRangesAndIcons()
        {
            ValidateTrackedAsset(LANDMINE_ASSET_PATH, 5, new[] { "SpawnCooldown", "ExplosionRadius", "Size", "KnockbackRange", "Damage" });
            ValidateTrackedAsset(LASERGUN_ASSET_PATH, 5, new[] { "DelayBetweenShoots", "NumberOfTurrets", "NumberOfTargets", "Range", "Damage" });
            ValidateTrackedAsset(MINIGUN_ASSET_PATH, 6, new[] { "ShotDelay", "Range", "NumberOfTurrets", "BeamWidth", "Damage", "Piercing" });
            ValidateTrackedAsset(SAW_ASSET_PATH, 2, new[] { "KnockbackRange", "Damage" });
        }

        private void ValidateTrackedAsset(string assetPath, int expectedCount, string[] expectedStatNames)
        {
            SkillUpgradeableStatsConfig sourceAsset = AssetDatabase.LoadAssetAtPath<SkillUpgradeableStatsConfig>(assetPath);
            Assert.IsNotNull(sourceAsset, $"Expected skill config asset at '{assetPath}'.");

            SkillUpgradeableStatsConfig clonedConfig = UnityEngine.Object.Instantiate(sourceAsset);
            _createdObjects.Add(clonedConfig);
            clonedConfig.ResetRuntimeState();

            List<NameUpgradableStatPair> displayStats = new();
            clonedConfig.AppendStatsForDisplay(displayStats);

            Assert.AreEqual(expectedCount, displayStats.Count, $"Unexpected stat count in '{assetPath}'.");

            for (int i = 0; i < expectedStatNames.Length; i++)
            {
                NameUpgradableStatPair pair = displayStats[i];
                Assert.AreEqual(expectedStatNames[i], pair.Name, $"Unexpected stat name at index {i} in '{assetPath}'.");
                Assert.IsNotNull(pair.UpgradeableStat, $"Stat '{pair.Name}' was null after ResetRuntimeState in '{assetPath}'.");
                Assert.IsNotNull(pair.UpgradeableStat.Icon, $"Stat '{pair.Name}' is missing an Icon in '{assetPath}'.");
                Assert.IsTrue(pair.UpgradeableStat.UpgradeRangeMin > 0f, $"Stat '{pair.Name}' upgrade min must be positive in '{assetPath}'.");
                Assert.IsTrue(pair.UpgradeableStat.UpgradeRangeMax >= pair.UpgradeableStat.UpgradeRangeMin, $"Stat '{pair.Name}' upgrade max < min in '{assetPath}'.");

                ValidateStatBounds(pair.Name, pair.UpgradeableStat, assetPath);
            }
        }

        private static void ValidateStatBounds(string statName, IUpgradeableStat stat, string assetPath)
        {
            if (stat is UpgradeableStat<float> floatStat)
            {
                Assert.IsNotNull(floatStat.MinMaxRange, $"Missing MinMaxRange for '{statName}' in '{assetPath}'.");
                if (floatStat.IsSubstractModeOn)
                {
                    Assert.Greater(floatStat.MinMaxRange.Min, floatStat.MinMaxRange.Max, $"Subtract stat '{statName}' should have Min > Max in '{assetPath}'.");
                }
                else
                {
                    Assert.LessOrEqual(floatStat.MinMaxRange.Min, floatStat.MinMaxRange.Max, $"Additive stat '{statName}' should have Min <= Max in '{assetPath}'.");
                }
            }
            else if (stat is UpgradeableStat<int> intStat)
            {
                Assert.IsNotNull(intStat.MinMaxRange, $"Missing MinMaxRange for '{statName}' in '{assetPath}'.");
                if (intStat.IsSubstractModeOn)
                {
                    Assert.Greater(intStat.MinMaxRange.Min, intStat.MinMaxRange.Max, $"Subtract stat '{statName}' should have Min > Max in '{assetPath}'.");
                }
                else
                {
                    Assert.LessOrEqual(intStat.MinMaxRange.Min, intStat.MinMaxRange.Max, $"Additive stat '{statName}' should have Min <= Max in '{assetPath}'.");
                }
            }
        }

        private LandmineSkillUpgradeableConfigSO CreateLandmineConfig()
        {
            LandmineSkillUpgradeableConfigSO config = CreateInstance<LandmineSkillUpgradeableConfigSO>();
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_spawnCooldown", CreateFloatStat(8f, 1f, 2f, 4f, isSubtract: true, unlimited: false));
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_explosionRadius", CreateFloatStat(1f, 4f, 1f, 1.2f, isSubtract: false, unlimited: true));
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_size", CreateFloatStat(1f, 2f, 0.7f, 1.2f, isSubtract: false, unlimited: true));
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_knockbackRange", CreateFloatStat(1f, 4f, 0.5f, 0.8f, isSubtract: false, unlimited: true));
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_damage", CreateIntStat(5, 18, 4, 6, unlimited: true));
            return config;
        }

        private SawSkillUpgradeableConfigSO CreateSawConfig()
        {
            SawSkillUpgradeableConfigSO config = CreateInstance<SawSkillUpgradeableConfigSO>();
            SetPrivateField(typeof(SawSkillUpgradeableConfigSO), config, "_knockbackRange", CreateFloatStat(1f, 4f, 0.5f, 0.8f, isSubtract: false, unlimited: false));
            SetPrivateField(typeof(SawSkillUpgradeableConfigSO), config, "_damage", CreateIntStat(10, 35, 4, 7, unlimited: true));
            return config;
        }

        private LasergunSkillSO CreateLasergunConfig(TurretConfigSO turretConfig, ProjectileConfigSO projectileConfig)
        {
            LasergunSkillSO config = CreateInstance<LasergunSkillSO>();
            SetPrivateField(typeof(LasergunSkillSO), config, "_turretConfig", turretConfig);
            SetPrivateField(typeof(LasergunSkillSO), config, "_projectileConfig", projectileConfig);
            SetPrivateField(typeof(LasergunSkillSO), config, "_delayBetweenShoots", CreateFloatStat(2f, 0.4f, 0.2f, 0.6f, isSubtract: true, unlimited: false));
            SetPrivateField(typeof(LasergunSkillSO), config, "_numberOfTurrets", CreateIntStat(1, 3, 1, 1, unlimited: false, alwaysUseMin: true));
            SetPrivateField(typeof(LasergunSkillSO), config, "_numberOfTargets", CreateIntStat(1, 5, 1, 1, unlimited: false, alwaysUseMin: true));
            SetPrivateField(typeof(LasergunSkillSO), config, "_startRange", CreateFloatStat(8f, 18f, 4f, 7f, isSubtract: false, unlimited: false));
            SetPrivateField(typeof(LasergunSkillSO), config, "_startDamage", CreateIntStat(5, 25, 5, 8, unlimited: true));
            return config;
        }

        private T CreateInstance<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
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

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
