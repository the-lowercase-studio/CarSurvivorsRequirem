using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Common.Types;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Enemies.Constants;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.ReflexDI;
using Assets.Scripts.Spawners;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.Spawners.WorldSpace;
using NUnit.Framework;
using Reflex.Core;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class TierConfigDrivenEnemySpawningTests
    {
        private const string PROGRESSION_CONFIG_PATH = "Assets/ScriptableObjects/GameFlow/GameLoopProgressionConfig.asset";
        private const string ENEMY_SURFACE_SHADER_NAME = "Custom/EnemySurface";
        private const string STANDARD_LIT_SHADER_NAME = "Universal Render Pipeline/Lit";

        private static readonly string[] BASE_ZOMBIE_PREFAB_PATHS =
        {
            "Assets/Prefabs/Enemies/Basic/Zombies/Crawling/CrawlingZombie.prefab",
            "Assets/Prefabs/Enemies/Basic/Zombies/Standing/StandingZombie.prefab"
        };

        private static readonly string[] TIER1_PREFAB_PATHS =
        {
            "Assets/Prefabs/Enemies/Basic/BarrelEnemy.prefab",
            "Assets/Prefabs/Enemies/Basic/Zombies/Crawling/CrawlingZombieLevel2.prefab",
            "Assets/Prefabs/Enemies/Basic/Zombies/Crawling/CrawlingZombieLevel3.prefab",
            "Assets/Prefabs/Enemies/Basic/Zombies/Standing/StandingZombieLevel1.prefab",
            "Assets/Prefabs/Enemies/Basic/Zombies/Standing/StandingZombieLevel2.prefab",
            "Assets/Prefabs/Enemies/Basic/Zombies/Standing/StandingZombieLevel3.prefab",
            "Assets/Prefabs/Enemies/Basic/Zombies/Standing/StandingZombieLevel4.prefab"
        };

        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdContainers.Count; i++)
            {
                _createdContainers[i]?.Dispose();
            }

            _createdContainers.Clear();

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
        public void EnemySpawnInfo_CreateRuntimeCopy_PreservesPrefabMaxAmountAndConfigOverride()
        {
            GameObject prefabGo = TrackGameObject("EnemyPrefab", active: false);
            Enemy prefabEnemy = prefabGo.AddComponent<Enemy>();
            EnemyConfigSO overrideConfig = CreateEnemyConfig(maxHealth: 500f, isShiny: true);
            SpawnChanceInfo sourceChance = new SpawnChanceInfo
            {
                SpawnChance = 35f,
                TresholdToStartAddingSpawnChanceToOtherInfos = 25f,
                SpawnChanceWillNotChange = false,
                HasEverReachedThreshold = true
            };
            EnemySpawnInfo source = new EnemySpawnInfo(prefabEnemy, 150, sourceChance, overrideConfig);

            EnemySpawnInfo copy = source.CreateRuntimeCopy();

            Assert.AreSame(prefabEnemy, copy.EnemyPrefab);
            Assert.AreEqual(150, copy.MaxAmount);
            Assert.AreSame(overrideConfig, copy.ConfigOverride);
            Assert.AreNotSame(sourceChance, copy.SpawnChanceInfo);
            Assert.AreEqual(35f, copy.SpawnChanceInfo.SpawnChance, 0.0001f);
            Assert.AreEqual(25f, copy.SpawnChanceInfo.TresholdToStartAddingSpawnChanceToOtherInfos, 0.0001f);
            Assert.IsFalse(copy.SpawnChanceInfo.HasEverReachedThreshold);
        }

        [Test]
        public void EnemySpawnInfo_MutatingRuntimeCopy_DoesNotMutateSourceAssetInfo()
        {
            EnemyConfigSO overrideConfig = CreateEnemyConfig(maxHealth: 220f, isShiny: true);
            SpawnChanceInfo sourceChance = new SpawnChanceInfo
            {
                SpawnChance = 65f,
                TresholdToStartAddingSpawnChanceToOtherInfos = 0f,
                SpawnChanceWillNotChange = false,
                HasEverReachedThreshold = false
            };
            EnemySpawnInfo source = new EnemySpawnInfo(null, 150, sourceChance, overrideConfig);

            EnemySpawnInfo copy = source.CreateRuntimeCopy();
            copy.SpawnChanceInfo.SpawnChance = 10f;
            copy.SpawnChanceInfo.HasEverReachedThreshold = true;

            Assert.AreEqual(65f, source.SpawnChanceInfo.SpawnChance, 0.0001f);
            Assert.IsFalse(source.SpawnChanceInfo.HasEverReachedThreshold);
        }

        [Test]
        public void EnemyTierShineController_WhenNeverEnabled_LeavesRendererWithoutPropertyBlock()
        {
            Material shinyMaterial = CreateTrackedMaterial(ENEMY_SURFACE_SHADER_NAME);
            MeshRenderer renderer = CreateMeshRendererWithMaterials("Tier1Body", shinyMaterial);
            EnemyTierShineController controller = AttachShineController(renderer.gameObject, false, renderer);

            controller.ApplyShineState();
            controller.SetShineEnabled(false);

            Assert.IsFalse(controller.IsShineEnabled);
            Assert.IsFalse(renderer.HasPropertyBlock());
        }

        [Test]
        public void EnemyTierShineController_SetShineEnabledTrue_SetsPropertyOnCompatibleSlotOnly()
        {
            Material shinyMaterial = CreateTrackedMaterial(ENEMY_SURFACE_SHADER_NAME);
            Material nonShinyMaterial = CreateTrackedMaterial(STANDARD_LIT_SHADER_NAME);
            MeshRenderer renderer = CreateMeshRendererWithMaterials("Tier2Body", shinyMaterial, nonShinyMaterial);
            EnemyTierShineController controller = AttachShineController(renderer.gameObject, false, renderer);

            controller.SetShineEnabled(true);

            MaterialPropertyBlock slot0Block = new MaterialPropertyBlock();
            MaterialPropertyBlock slot1Block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(slot0Block, 0);
            renderer.GetPropertyBlock(slot1Block, 1);

            Assert.IsTrue(controller.IsShineEnabled);
            Assert.IsTrue(renderer.HasPropertyBlock());
            Assert.AreEqual(1f, slot0Block.GetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID), 0.0001f);
            Assert.IsTrue(slot1Block.isEmpty);
        }

        [Test]
        public void EnemyTierShineController_DisableAfterEnable_ResetsShineToZeroAndPreservesBaseColor()
        {
            Material shinyMaterial = CreateTrackedMaterial(ENEMY_SURFACE_SHADER_NAME);
            MeshRenderer renderer = CreateMeshRendererWithMaterials("BarrelBody", shinyMaterial);
            EnemyTierShineController controller = AttachShineController(renderer.gameObject, false, renderer);

            controller.SetShineEnabled(true);

            MaterialPropertyBlock feedbackBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(feedbackBlock, 0);
            feedbackBlock.SetColor(BASE_COLOR_ID, Color.red);
            renderer.SetPropertyBlock(feedbackBlock, 0);

            controller.SetShineEnabled(false);

            MaterialPropertyBlock verifyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(verifyBlock, 0);

            Assert.IsFalse(controller.IsShineEnabled);
            Assert.AreEqual(0f, verifyBlock.GetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID), 0.0001f);
            Assert.AreEqual(Color.red, verifyBlock.GetColor(BASE_COLOR_ID));
        }

        [Test]
        public void ReflexPrefabInstantiator_BeforeActivateSetBaseConfig_InitializesHealthAndShineBeforeOnEnable()
        {
            EnemyConfigSO tier1Config = CreateEnemyConfig(maxHealth: 100f, isShiny: false);
            EnemyConfigSO tier2Config = CreateEnemyConfig(maxHealth: 500f, isShiny: true);
            Material shinyMaterial = CreateTrackedMaterial(ENEMY_SURFACE_SHADER_NAME);
            Enemy enemyPrefab = CreateEnemyPrefabHarness("StandingZombiePrefab", tier1Config, shinyMaterial, out _);

            GameObject activeParent = TrackGameObject("ActiveParent", active: true);
            Transform stagingParent = ReflexPrefabInstantiator.CreateInactiveStagingParent("TestPool");
            _createdObjects.Add(stagingParent.gameObject);
            Container container = BuildEnemyTestContainer();

            Enemy instance = ReflexPrefabInstantiator.InstantiateInjectedObject(
                enemyPrefab,
                activeParent.transform,
                container,
                stagingParent,
                enemy => enemy.SetBaseConfig(tier2Config));
            _createdObjects.Add(instance.gameObject);
            ActivateInstanceInEditMode(instance.gameObject);

            MaterialPropertyBlock verifyBlock = new MaterialPropertyBlock();
            Renderer bodyRenderer = instance.GetComponentInChildren<MeshRenderer>(true);
            bodyRenderer.GetPropertyBlock(verifyBlock, 0);

            Assert.AreSame(tier2Config, instance.Config);
            Assert.AreEqual(500f, instance.Health.MaxHealth, 0.0001f);
            Assert.AreEqual(500f, instance.Health.CurrentHealth, 0.0001f);
            Assert.AreEqual(1f, verifyBlock.GetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID), 0.0001f);
        }

        [Test]
        public void Enemy_ApplyRuntimeConfig_UpdatesConfigMaxHealthAndShine()
        {
            EnemyConfigSO tier1Config = CreateEnemyConfig(maxHealth: 100f, isShiny: false);
            EnemyConfigSO rareTier2Config = CreateEnemyConfig(maxHealth: 900f, isShiny: true);
            Material shinyMaterial = CreateTrackedMaterial(ENEMY_SURFACE_SHADER_NAME);
            Enemy enemyPrefab = CreateEnemyPrefabHarness("Tier1ZombiePrefab", tier1Config, shinyMaterial, out _);

            GameObject activeParent = TrackGameObject("ActiveParent", active: true);
            Transform stagingParent = ReflexPrefabInstantiator.CreateInactiveStagingParent("TestPool");
            _createdObjects.Add(stagingParent.gameObject);
            Container container = BuildEnemyTestContainer();

            Enemy instance = ReflexPrefabInstantiator.InstantiateInjectedObject(
                enemyPrefab,
                activeParent.transform,
                container,
                stagingParent);
            _createdObjects.Add(instance.gameObject);
            ActivateInstanceInEditMode(instance.gameObject);
            instance.OnGet();

            instance.ApplyRuntimeConfig(rareTier2Config);

            MaterialPropertyBlock verifyBlock = new MaterialPropertyBlock();
            Renderer bodyRenderer = instance.GetComponentInChildren<MeshRenderer>(true);
            bodyRenderer.GetPropertyBlock(verifyBlock, 0);

            Assert.AreSame(rareTier2Config, instance.Config);
            Assert.AreEqual(900f, instance.Health.MaxHealth, 0.0001f);
            Assert.AreEqual(1f, verifyBlock.GetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID), 0.0001f);
        }

        [Test]
        public void Enemy_OnReleaseAfterRareOverride_RestoresBaseConfigHealthAndDisablesShine()
        {
            EnemyConfigSO tier1Config = CreateEnemyConfig(maxHealth: 100f, isShiny: false);
            EnemyConfigSO rareTier2Config = CreateEnemyConfig(maxHealth: 900f, isShiny: true);
            Material shinyMaterial = CreateTrackedMaterial(ENEMY_SURFACE_SHADER_NAME);
            Enemy enemyPrefab = CreateEnemyPrefabHarness("Tier1ZombiePrefab", tier1Config, shinyMaterial, out _);

            GameObject activeParent = TrackGameObject("ActiveParent", active: true);
            Transform stagingParent = ReflexPrefabInstantiator.CreateInactiveStagingParent("TestPool");
            _createdObjects.Add(stagingParent.gameObject);
            Container container = BuildEnemyTestContainer();

            Enemy instance = ReflexPrefabInstantiator.InstantiateInjectedObject(
                enemyPrefab,
                activeParent.transform,
                container,
                stagingParent);
            _createdObjects.Add(instance.gameObject);
            ActivateInstanceInEditMode(instance.gameObject);
            instance.OnGet();
            instance.ApplyRuntimeConfig(rareTier2Config);

            instance.OnRelease();

            MaterialPropertyBlock verifyBlock = new MaterialPropertyBlock();
            Renderer bodyRenderer = instance.GetComponentInChildren<MeshRenderer>(true);
            bodyRenderer.GetPropertyBlock(verifyBlock, 0);

            Assert.AreSame(tier1Config, instance.Config);
            Assert.AreEqual(100f, instance.Health.MaxHealth, 0.0001f);
            Assert.AreEqual(0f, verifyBlock.GetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID), 0.0001f);
        }

        [Test]
        public void Enemy_OnReleaseForStandardTier2Pool_KeepsBaseConfigAndShineEnabled()
        {
            EnemyConfigSO tier1Config = CreateEnemyConfig(maxHealth: 100f, isShiny: false);
            EnemyConfigSO tier2Config = CreateEnemyConfig(maxHealth: 500f, isShiny: true);
            Material shinyMaterial = CreateTrackedMaterial(ENEMY_SURFACE_SHADER_NAME);
            Enemy enemyPrefab = CreateEnemyPrefabHarness("Tier2ZombiePrefab", tier1Config, shinyMaterial, out _);

            GameObject activeParent = TrackGameObject("ActiveParent", active: true);
            Transform stagingParent = ReflexPrefabInstantiator.CreateInactiveStagingParent("TestPool");
            _createdObjects.Add(stagingParent.gameObject);
            Container container = BuildEnemyTestContainer();

            Enemy instance = ReflexPrefabInstantiator.InstantiateInjectedObject(
                enemyPrefab,
                activeParent.transform,
                container,
                stagingParent,
                enemy => enemy.SetBaseConfig(tier2Config));
            _createdObjects.Add(instance.gameObject);
            ActivateInstanceInEditMode(instance.gameObject);

            instance.OnGet();
            instance.OnRelease();

            MaterialPropertyBlock verifyBlock = new MaterialPropertyBlock();
            Renderer bodyRenderer = instance.GetComponentInChildren<MeshRenderer>(true);
            bodyRenderer.GetPropertyBlock(verifyBlock, 0);

            Assert.AreSame(tier2Config, instance.Config);
            Assert.AreEqual(500f, instance.Health.MaxHealth, 0.0001f);
            Assert.AreEqual(1f, verifyBlock.GetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID), 0.0001f);
        }

        [Test]
        public void RedistributionSystem_Tier2FourEntryRoster_UnlocksBarrelImmediatelyAndConservesTotalChance()
        {
            List<EnemySpawnInfo> tier2Entries = CreateTier2RedistributionEntries();
            EnemiesSpawnChanceRedistributionSystem system = CreateInitializedRedistributionSystem(tier2Entries, 2.5f);

            system.RedistributeSpawnChance();

            float total = SumSpawnChances(tier2Entries);
            Assert.AreEqual(62.5f, tier2Entries[0].SpawnChanceInfo.SpawnChance, 0.0001f);
            Assert.AreEqual(36.25f, tier2Entries[1].SpawnChanceInfo.SpawnChance, 0.0001f);
            Assert.AreEqual(1.25f, tier2Entries[2].SpawnChanceInfo.SpawnChance, 0.0001f);
            Assert.AreEqual(0f, tier2Entries[3].SpawnChanceInfo.SpawnChance, 0.0001f);
            Assert.AreEqual(100f, total, 0.0001f);
        }

        [Test]
        public void RedistributionSystem_Tier2FourEntryRoster_UnlocksStanding4OnceBarrelReachesThresholdAndConverges()
        {
            List<EnemySpawnInfo> tier2Entries = CreateTier2RedistributionEntries();
            EnemiesSpawnChanceRedistributionSystem system = CreateInitializedRedistributionSystem(tier2Entries, 2.5f);

            RunRedistributionWaves(system, 18);

            Assert.AreEqual(22.5f, tier2Entries[2].SpawnChanceInfo.SpawnChance, 0.0001f);
            Assert.AreEqual(0f, tier2Entries[3].SpawnChanceInfo.SpawnChance, 0.0001f);

            system.RedistributeSpawnChance();

            Assert.AreEqual(0.625f, tier2Entries[3].SpawnChanceInfo.SpawnChance, 0.0001f);
            Assert.AreEqual(100f, SumSpawnChances(tier2Entries), 0.0001f);

            RunRedistributionWaves(system, 100);

            Assert.AreEqual(0f, tier2Entries[0].SpawnChanceInfo.SpawnChance, 0.0001f);
            Assert.AreEqual(0f, tier2Entries[1].SpawnChanceInfo.SpawnChance, 0.0001f);
            Assert.AreEqual(0f, tier2Entries[2].SpawnChanceInfo.SpawnChance, 0.0001f);
            Assert.AreEqual(100f, tier2Entries[3].SpawnChanceInfo.SpawnChance, 0.0001f);
        }

        [Test]
        public void AuthoredAssets_Tier2RosterAndTier1Prefabs_MatchApprovedConfiguration()
        {
            GameLoopProgressionConfigSO progression = AssetDatabase.LoadAssetAtPath<GameLoopProgressionConfigSO>(PROGRESSION_CONFIG_PATH);
            Assert.IsNotNull(progression);
            progression.ValidateConfiguration();

            LoopTierConfig tier2 = progression.GetTier(1);
            IReadOnlyList<EnemySpawnInfo> tier2Infos = tier2.EnemySpawning.EnemySpawnInfos;

            Assert.AreEqual(4, tier2Infos.Count);
            AssertTier2Entry(tier2Infos[0], "CrawlingZombieLevel3", "CrawlingZombie3Config_Tier2", 150, 65f, 0f);
            AssertTier2Entry(tier2Infos[1], "StandingZombieLevel3", "StandingZombie3Config_Tier2", 150, 35f, 25f);
            AssertTier2Entry(tier2Infos[2], "BarrelEnemy", "BarrelEnemyConfig_Tier2", 40, 0f, 22f);
            AssertTier2Entry(tier2Infos[3], "StandingZombieLevel4", "StandingZombie4Config_Tier2", 130, 0f, 0f);

            int totalPrewarm = tier2Infos[0].MaxAmount + tier2Infos[1].MaxAmount + tier2Infos[2].MaxAmount + tier2Infos[3].MaxAmount;
            Assert.AreEqual(470, totalPrewarm);

            VerifyAllTier1PrefabsShareNonShinyController();
        }

        [Test]
        public void AuthoredAssets_AllBasicEnemyPrefabs_TargetRenderersSupportMaterializationWithoutTier1Shine()
        {
            VerifyPrefabTargetRenderersSupportMaterialization(TIER1_PREFAB_PATHS);
            VerifyPrefabTargetRenderersSupportMaterialization(BASE_ZOMBIE_PREFAB_PATHS);
        }

        private static void VerifyPrefabTargetRenderersSupportMaterialization(string[] prefabPaths)
        {
            for (int i = 0; i < prefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
                Assert.IsNotNull(prefab, $"Missing prefab at {prefabPaths[i]}");

                EnemyTierShineController shine = prefab.GetComponent<EnemyTierShineController>();
                Assert.IsNotNull(shine, $"Missing EnemyTierShineController on {prefab.name}");
                Assert.IsFalse(shine.IsShineEnabled, $"Default shine state on {prefab.name} must be false.");

                Renderer[] targetRenderers = GetPrivateField<Renderer[]>(typeof(EnemyTierShineController), shine, "_targetRenderers");
                Assert.IsNotNull(targetRenderers, $"_targetRenderers is null on {prefab.name}");
                Assert.IsNotEmpty(targetRenderers, $"_targetRenderers is empty on {prefab.name}");

                for (int rIndex = 0; rIndex < targetRenderers.Length; rIndex++)
                {
                    Renderer renderer = targetRenderers[rIndex];
                    Assert.IsNotNull(renderer, $"Target renderer [{rIndex}] is null on {prefab.name}");

                    Material[] materials = renderer.sharedMaterials;
                    Assert.IsNotEmpty(materials, $"Target renderer {renderer.name} has no materials on {prefab.name}");

                    for (int slot = 0; slot < materials.Length; slot++)
                    {
                        Material material = materials[slot];
                        Assert.IsNotNull(material, $"Material slot [{slot}] on {prefab.name}.{renderer.name} is null.");
                        Assert.AreEqual(
                            ENEMY_SURFACE_SHADER_NAME,
                            material.shader.name,
                            $"Material '{material.name}' on {prefab.name}.{renderer.name}[{slot}] must use {ENEMY_SURFACE_SHADER_NAME} for swarm materialization.");
                        Assert.IsTrue(
                            material.HasProperty(EnemyVisualConstants.MATERIALIZE_ENABLED_PROPERTY_ID),
                            $"Material '{material.name}' on {prefab.name}.{renderer.name}[{slot}] must expose _MaterializeEnabled.");
                        Assert.AreEqual(
                            0f,
                            material.GetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID),
                            0.0001f,
                            $"Shared material '{material.name}' on {prefab.name}.{renderer.name}[{slot}] must keep _ShineEnabled = 0 by default.");
                    }
                }
            }
        }

        private void VerifyAllTier1PrefabsShareNonShinyController()
        {
            for (int i = 0; i < TIER1_PREFAB_PATHS.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TIER1_PREFAB_PATHS[i]);
                Assert.IsNotNull(prefab, $"Missing prefab at {TIER1_PREFAB_PATHS[i]}");

                Enemy enemy = prefab.GetComponent<Enemy>();
                EnemyTierShineController shine = prefab.GetComponent<EnemyTierShineController>();

                Assert.IsNotNull(enemy, $"Missing Enemy on {prefab.name}");
                Assert.IsNotNull(enemy.Config, $"Missing Enemy.Config on {prefab.name}");
                Assert.IsFalse(enemy.Config.IsShiny, $"Tier 1 config on {prefab.name} should not be shiny.");
                Assert.IsNotNull(shine, $"Missing EnemyTierShineController on {prefab.name}");
                Assert.IsFalse(shine.IsShineEnabled, $"Default shine state on {prefab.name} must be false.");
            }
        }

        private static void AssertTier2Entry(
            EnemySpawnInfo entry,
            string expectedPrefabName,
            string expectedConfigName,
            ushort expectedMaxAmount,
            float expectedChance,
            float expectedThreshold)
        {
            Assert.IsNotNull(entry);
            Assert.IsNotNull(entry.EnemyPrefab);
            Assert.AreEqual(expectedPrefabName, entry.EnemyPrefab.name);
            Assert.IsNotNull(entry.ConfigOverride);
            Assert.AreEqual(expectedConfigName, entry.ConfigOverride.name);
            Assert.IsTrue(entry.ConfigOverride.IsShiny);
            Assert.AreEqual(expectedMaxAmount, entry.MaxAmount);
            Assert.AreEqual(expectedChance, entry.SpawnChanceInfo.SpawnChance, 0.0001f);
            Assert.AreEqual(expectedThreshold, entry.SpawnChanceInfo.TresholdToStartAddingSpawnChanceToOtherInfos, 0.0001f);
        }

        private static List<EnemySpawnInfo> CreateTier2RedistributionEntries()
        {
            return new List<EnemySpawnInfo>
            {
                new EnemySpawnInfo(null, 150, new SpawnChanceInfo { SpawnChance = 65f, TresholdToStartAddingSpawnChanceToOtherInfos = 0f }),
                new EnemySpawnInfo(null, 150, new SpawnChanceInfo { SpawnChance = 35f, TresholdToStartAddingSpawnChanceToOtherInfos = 25f }),
                new EnemySpawnInfo(null, 40, new SpawnChanceInfo { SpawnChance = 0f, TresholdToStartAddingSpawnChanceToOtherInfos = 22f }),
                new EnemySpawnInfo(null, 130, new SpawnChanceInfo { SpawnChance = 0f, TresholdToStartAddingSpawnChanceToOtherInfos = 0f })
            };
        }

        private static EnemiesSpawnChanceRedistributionSystem CreateInitializedRedistributionSystem(
            List<EnemySpawnInfo> entries,
            float fixedDecreaseStep)
        {
            EnemiesSpawnChanceRedistributionSystem system = new EnemiesSpawnChanceRedistributionSystem();
            system.Initialize(new EnemiesSpawnChanceRedistributionSystem.Configuration
            {
                EnemiesInfo = entries,
                SpawnChanceDecreaseFactor = new FloatValueRange(fixedDecreaseStep, fixedDecreaseStep)
            });
            return system;
        }

        private static void RunRedistributionWaves(EnemiesSpawnChanceRedistributionSystem system, int waveCount)
        {
            for (int i = 0; i < waveCount; i++)
            {
                system.RedistributeSpawnChance();
            }
        }

        private static float SumSpawnChances(List<EnemySpawnInfo> entries)
        {
            float sum = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                sum += entries[i].SpawnChanceInfo.SpawnChance;
            }

            return sum;
        }

        private GameObject TrackGameObject(string name, bool active)
        {
            GameObject go = new GameObject(name);
            go.SetActive(active);
            _createdObjects.Add(go);
            return go;
        }

        private EnemyConfigSO CreateEnemyConfig(float maxHealth, bool isShiny)
        {
            EnemyConfigSO config = ScriptableObject.CreateInstance<EnemyConfigSO>();
            _createdObjects.Add(config);
            config.MaxHealth = maxHealth;
            SetPrivateField(typeof(EnemyConfigSO), config, "_isShiny", isShiny);
            return config;
        }

        private Material CreateTrackedMaterial(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.IsNotNull(shader, $"Shader not found: {shaderName}");
            Material material = new Material(shader);
            _createdObjects.Add(material);
            return material;
        }

        private MeshRenderer CreateMeshRendererWithMaterials(string name, params Material[] materials)
        {
            GameObject go = TrackGameObject(name, active: true);
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            return renderer;
        }

        private static EnemyTierShineController AttachShineController(
            GameObject target,
            bool isEnabled,
            params Renderer[] renderers)
        {
            EnemyTierShineController controller = target.AddComponent<EnemyTierShineController>();
            SetPrivateField(typeof(EnemyTierShineController), controller, "_isShineEnabled", isEnabled);
            SetPrivateField(typeof(EnemyTierShineController), controller, "_targetRenderers", renderers);
            return controller;
        }

        private Enemy CreateEnemyPrefabHarness(
            string name,
            EnemyConfigSO baseConfig,
            Material bodyMaterial,
            out MeshRenderer bodyRenderer)
        {
            GameObject templateHost = TrackGameObject($"{name}_TemplateHost", active: false);
            GameObject root = TrackGameObject(name, active: true);
            root.transform.SetParent(templateHost.transform, false);
            root.AddComponent<Health>();
            root.AddComponent<FakeDeathSequence>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            bodyRenderer = visual.AddComponent<MeshRenderer>();
            bodyRenderer.sharedMaterials = new[] { bodyMaterial };

            visual.AddComponent<Animator>();
            visual.AddComponent<EnemyAnimator>();

            EnemyTierShineController shineController = AttachShineController(root, false, bodyRenderer);
            Enemy enemy = root.AddComponent<Enemy>();

            SetPrivateField(typeof(Enemy), enemy, "<Config>k__BackingField", baseConfig);
            SetPrivateField(typeof(Enemy), enemy, "_visual", visual);
            SetPrivateField(typeof(Enemy), enemy, "_tierShineController", shineController);

            return enemy;
        }

        private Container BuildEnemyTestContainer()
        {
            ContainerBuilder builder = new ContainerBuilder().SetName("EnemySpawningTestContainer");
            builder.AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>));
            builder.AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState));
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private static void ActivateInstanceInEditMode(GameObject instance)
        {
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                {
                    InvokeLifecycleMethod(behaviours[i], "Awake");
                }
            }

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].enabled && behaviours[i].gameObject.activeInHierarchy)
                {
                    InvokeLifecycleMethod(behaviours[i], "OnEnable");
                }
            }
        }

        private static void InvokeLifecycleMethod(MonoBehaviour behaviour, string methodName)
        {
            MethodInfo method = behaviour.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                method.Invoke(behaviour, null);
            }
        }

        private static T GetPrivateField<T>(Type ownerType, object target, string fieldName) where T : class
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {ownerType.Name}");
            return field.GetValue(target) as T;
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {ownerType.Name}");
            field.SetValue(target, value);
        }

        private sealed class FakeDeathSequence : MonoBehaviour, INeedToCompleteBeforeDisable
        {
            public event EventHandler OnCompleted
            {
                add { }
                remove { }
            }
        }

        private sealed class FakeDamageNumbersSpawner : IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>
        {
            public uint CurrentlySpawnedObjectsCount => 0;

            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public void Spawn(Vector3 pos, DamageNubmersSpawnerConfig data, int count = 1)
            {
            }
        }

        private sealed class FakeTimeFreezeState : ITimeFreezeState
        {
            public bool IsFrozen => false;
            public float UnfrozenTime => Time.time;

            public event Action<bool> OnFrozenChanged
            {
                add { }
                remove { }
            }
        }
    }
}
