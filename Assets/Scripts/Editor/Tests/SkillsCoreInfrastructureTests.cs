using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.ScriptableObjects.Player.Skills;
using Assets.ScriptableObjects.Skills;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.Common.Types;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.Initializers;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using Assets.Scripts.Stats;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.TestTools;

namespace Assets.Scripts.Editor.Tests
{
    public class SkillsCoreInfrastructureTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdContainers.Count; i++)
            {
                if (_createdContainers[i] != null)
                {
                    _createdContainers[i].Dispose();
                }
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
        public void SkillsRegistry_Awake_DiscoversDirectChildSkillsAndIgnoresNestedGrandchildren()
        {
            GameObject registryGo = TrackGameObject("SkillsRegistry", active: false);
            SkillsRegistry registry = registryGo.AddComponent<SkillsRegistry>();

            TestChildUpgradeableSkill firstDirect = CreateChildSkill(registryGo.transform, "FirstSkill");
            TestChildUpgradeableSkill secondDirect = CreateChildSkill(registryGo.transform, "SecondSkill");
            CreateChildSkill(firstDirect.transform, "NestedGrandchildSkill");

            InvokeMethod(registry, "Awake");

            Assert.AreEqual(2, registry.Skills.Count);
            Assert.AreSame(firstDirect, registry.Skills[0]);
            Assert.AreSame(secondDirect, registry.Skills[1]);
        }

        [Test]
        public void SkillsRegistry_Start_InitializesFirstSkillAndResetsConfigsWithoutSessionSnapshot()
        {
            GameObject registryGo = TrackGameObject("SkillsRegistry", active: false);
            SkillsRegistry registry = registryGo.AddComponent<SkillsRegistry>();

            TestChildUpgradeableSkill firstSkill = CreateChildSkill(registryGo.transform, "StartSkill", startActive: false);
            TestChildUpgradeableSkill secondSkill = CreateChildSkill(registryGo.transform, "SecondSkill", startActive: false);

            int initializedEventCount = 0;
            ISkillBase eventSkill = null;
            registry.OnSkillInitialized += (skill) =>
            {
                initializedEventCount++;
                eventSkill = skill;
            };

            InvokeMethod(registry, "Awake");
            InvokeMethod(registry, "Start");

            Assert.AreEqual(1, firstSkill.ConfigRef.ResetCount);
            Assert.AreEqual(1, secondSkill.ConfigRef.ResetCount);
            Assert.IsTrue(firstSkill.IsInitialized());
            Assert.IsFalse(secondSkill.IsInitialized());
            Assert.AreEqual(1, registry.InitializedSkillsCount);
            Assert.AreEqual(1, registry.UninitializedSkillsCount);
            Assert.AreEqual(1, initializedEventCount);
            Assert.AreSame(firstSkill, eventSkill);
        }

        [Test]
        public void SkillsRegistry_Start_WithEmptyHierarchy_CompletesSafelyWithZeroCounts()
        {
            GameObject registryGo = TrackGameObject("EmptyRegistry", active: false);
            SkillsRegistry registry = registryGo.AddComponent<SkillsRegistry>();

            InvokeMethod(registry, "Awake");
            InvokeMethod(registry, "Start");

            Assert.AreEqual(0, registry.Skills.Count);
            Assert.AreEqual(0, registry.InitializedSkillsCount);
            Assert.AreEqual(0, registry.UninitializedSkillsCount);
            Assert.AreEqual(0, registry.GetInitializedSkills().Count);
            Assert.AreEqual(0, registry.GetUninitializedSkills().Count);
        }

        [Test]
        public void SkillsRegistry_Start_FallsBackToFirstSkillWhenRestoredSnapshotMatchesNoSkill()
        {
            GameLoopProgressionConfigSO progressionConfig = TrackScriptableObject<GameLoopProgressionConfigSO>();
            List<LoopTierConfig> tiers = new List<LoopTierConfig> { new LoopTierConfig(), new LoopTierConfig() };
            SetPrivateField(typeof(GameLoopProgressionConfigSO), progressionConfig, "_tiers", tiers);
            RunSessionService sessionService = new RunSessionService(progressionConfig);

            GameObject sourceRegistryGo = TrackGameObject("SourceRegistry", active: false);
            SkillsRegistry sourceRegistry = sourceRegistryGo.AddComponent<SkillsRegistry>();
            TestChildUpgradeableSkill unknownSkill = CreateChildSkill(sourceRegistryGo.transform, "UnknownSkillOnly", startActive: false);
            InvokeMethod(sourceRegistry, "Awake");
            sourceRegistry.InitializeSkill(unknownSkill);

            FakePlayerManager snapshotPlayer = new FakePlayerManager(sourceRegistry);
            sessionService.CaptureSnapshotAndAdvanceTier(snapshotPlayer, 10u);

            GameObject targetRegistryGo = TrackGameObject("TargetRegistry", active: false);
            SkillsRegistry targetRegistry = targetRegistryGo.AddComponent<SkillsRegistry>();
            TestChildUpgradeableSkill fallbackFirst = CreateChildSkill(targetRegistryGo.transform, "ActualFirstSkill", startActive: false);
            TestChildUpgradeableSkill actualSecond = CreateChildSkill(targetRegistryGo.transform, "ActualSecondSkill", startActive: false);

            Container container = BuildContainer((builder) =>
            {
                builder.AddSingleton(sessionService, typeof(IRunSessionService));
            });
            GameObjectInjector.InjectRecursive(targetRegistryGo, container);

            InvokeMethod(targetRegistry, "Awake");
            InvokeMethod(targetRegistry, "Start");

            Assert.IsTrue(fallbackFirst.IsInitialized());
            Assert.IsFalse(actualSecond.IsInitialized());
            Assert.AreEqual(1, targetRegistry.InitializedSkillsCount);
            Assert.AreEqual(1, targetRegistry.UninitializedSkillsCount);
        }

        [Test]
        public void SkillsRegistry_InitializeSkill_UpdatesCountsFiresEventOnceAndRejectsNullOrDuplicate()
        {
            GameObject registryGo = TrackGameObject("SkillsRegistry", active: false);
            SkillsRegistry registry = registryGo.AddComponent<SkillsRegistry>();

            TestChildUpgradeableSkill firstSkill = CreateChildSkill(registryGo.transform, "First", startActive: false);
            TestChildUpgradeableSkill secondSkill = CreateChildSkill(registryGo.transform, "Second", startActive: false);

            InvokeMethod(registry, "Awake");
            InvokeMethod(registry, "Start");

            int eventCount = 0;
            ISkillBase lastEventSkill = null;
            registry.OnSkillInitialized += (skill) =>
            {
                eventCount++;
                lastEventSkill = skill;
            };

            ISkillBase initialized = registry.InitializeSkill(secondSkill);
            ISkillBase duplicateResult = registry.InitializeSkill(secondSkill);
            ISkillBase nullResult = registry.InitializeSkill(null);

            Assert.AreSame(secondSkill, initialized);
            Assert.IsNull(duplicateResult);
            Assert.IsNull(nullResult);
            Assert.AreEqual(1, eventCount);
            Assert.AreSame(secondSkill, lastEventSkill);
            Assert.AreEqual(2, registry.InitializedSkillsCount);
            Assert.AreEqual(0, registry.UninitializedSkillsCount);
            Assert.AreEqual(2, registry.GetInitializedSkills().Count);
            Assert.AreEqual(0, registry.GetUninitializedSkills().Count);
            Assert.IsTrue(firstSkill.IsInitialized());
            Assert.IsTrue(secondSkill.IsInitialized());
        }

        [Test]
        public void ItemsWithScriptableConfigsActivator_InitializeFirst_HandlesEmptyIdempotentAndFirstItem()
        {
            TestScriptableConfig config = TrackScriptableObject<TestScriptableConfig>();

            ItemsWithScriptableConfigsActivator<TestInitializableItem, TestScriptableConfig> emptyActivator =
                new ItemsWithScriptableConfigsActivator<TestInitializableItem, TestScriptableConfig>(Array.Empty<TestInitializableItem>());

            Assert.IsNull(emptyActivator.InitializeFirst(config));

            TestInitializableItem item0 = CreateInitializableItem("Item0");
            TestInitializableItem item1 = CreateInitializableItem("Item1");
            ItemsWithScriptableConfigsActivator<TestInitializableItem, TestScriptableConfig> activator =
                new ItemsWithScriptableConfigsActivator<TestInitializableItem, TestScriptableConfig>(new[] { item0, item1 });

            TestInitializableItem first = activator.InitializeFirst(config);
            TestInitializableItem secondCall = activator.InitializeFirst(config);

            Assert.AreSame(item0, first);
            Assert.AreSame(item0, secondCall);
            Assert.AreEqual(1, item0.InitializeCount);
            Assert.AreSame(config, item0.LastConfig);
            Assert.IsFalse(item1.IsInitialized());
        }

        [Test]
        public void ItemsWithScriptableConfigsActivator_InitializeRandomAndUntilCount_RespectsCapacityAndOrder()
        {
            TestScriptableConfig config = TrackScriptableObject<TestScriptableConfig>();
            TestInitializableItem item0 = CreateInitializableItem("Item0");
            TestInitializableItem item1 = CreateInitializableItem("Item1");
            TestInitializableItem item2 = CreateInitializableItem("Item2");

            ItemsWithScriptableConfigsActivator<TestInitializableItem, TestScriptableConfig> activator =
                new ItemsWithScriptableConfigsActivator<TestInitializableItem, TestScriptableConfig>(new[] { item0, item1, item2 });

            TestInitializableItem randomItem = activator.InitializeRandom(config);
            Assert.IsNotNull(randomItem);
            Assert.IsTrue(randomItem.IsInitialized());

            activator.InitializeUntilCount(config, 2);
            List<TestInitializableItem> afterTwo = CollectItems(activator.GetInitialized());
            Assert.AreEqual(2, afterTwo.Count);

            // Requesting more than total length must terminate cleanly at capacity.
            activator.InitializeUntilCount(config, 10);
            List<TestInitializableItem> allInitialized = CollectItems(activator.GetInitialized());
            Assert.AreEqual(3, allInitialized.Count);
            Assert.AreSame(item0, allInitialized[0]);
            Assert.AreSame(item1, allInitialized[1]);
            Assert.AreSame(item2, allInitialized[2]);

            Assert.IsNull(activator.InitializeRandom(config));
        }

        [Test]
        public void UpgradeableSkill_NullConfig_LogsWarningRemainsInactiveAndCannotUpgrade()
        {
            GameObject skillGo = TrackGameObject("NullConfigSkill", active: false);
            TestChildUpgradeableSkill skill = skillGo.AddComponent<TestChildUpgradeableSkill>();
            SkillInfoSO info = CreateSkillInfo("BrokenSkill");
            skill.Setup(info, null);

            LogAssert.Expect(LogType.Warning, "Skill BrokenSkill is not initialized. Please assign a valid config.");
            skill.Initialize();

            Assert.IsFalse(skillGo.activeSelf);
            Assert.IsFalse(skill.IsInitialized());
            Assert.IsFalse(skill.CanBeUpgraded());
        }

        [Test]
        public void SkillCrate_OnTriggerEnter_FiresEventsOnlyForPlayerLayerAndReturnToPoolRaisesRelease()
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(playerLayer, 0, "Player layer must exist.");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");

            GameObject crateGo = TrackGameObject("SkillCrate", active: true);
            SkillCrate crate = crateGo.AddComponent<SkillCrate>();
            InvokeMethod(crate, "Awake");

            int collectedCount = 0;
            int releasedCount = 0;
            crate.OnCollected += (sender, args) =>
            {
                collectedCount++;
            };
            crate.OnCanBeReleased += (sender, args) =>
            {
                releasedCount++;
            };

            GameObject enemyGo = TrackGameObject("Enemy", active: true);
            enemyGo.layer = enemyLayer;
            BoxCollider enemyCollider = enemyGo.AddComponent<BoxCollider>();

            InvokeMethod(crate, "OnTriggerEnter", enemyCollider);
            Assert.AreEqual(0, collectedCount);
            Assert.AreEqual(0, releasedCount);

            GameObject playerGo = TrackGameObject("Player", active: true);
            playerGo.layer = playerLayer;
            BoxCollider playerCollider = playerGo.AddComponent<BoxCollider>();

            InvokeMethod(crate, "OnTriggerEnter", playerCollider);
            Assert.AreEqual(1, collectedCount);
            Assert.AreEqual(1, releasedCount);

            crate.ReturnToPool();
            Assert.AreEqual(2, releasedCount);
        }

        [Test]
        public void StatsUnitsExtensions_ToDisplayString_ReturnsExpectedStringsAndThrowsOnUnknownUnit()
        {
            Assert.AreEqual("", StatsUnits.None.ToDisplayString());
            Assert.AreEqual("%", StatsUnits.Percentage.ToDisplayString());
            Assert.AreEqual("s", StatsUnits.Seconds.ToDisplayString());
            Assert.AreEqual("m", StatsUnits.Meters.ToDisplayString());

            StatsUnits invalidUnit = (StatsUnits)999;
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                invalidUnit.ToDisplayString();
            });
        }

        private GameObject TrackGameObject(string name, bool active)
        {
            GameObject go = new GameObject(name);
            go.SetActive(active);
            _createdObjects.Add(go);
            return go;
        }

        private T TrackScriptableObject<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private Container BuildContainer(Action<ContainerBuilder> configure)
        {
            ContainerBuilder builder = new ContainerBuilder().SetName("SkillsCoreTestContainer");
            configure?.Invoke(builder);
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private TestChildUpgradeableSkill CreateChildSkill(Transform parent, string skillName, bool startActive = false)
        {
            GameObject skillGo = new GameObject(skillName);
            skillGo.transform.SetParent(parent, false);
            skillGo.SetActive(startActive);
            _createdObjects.Add(skillGo);

            TestChildUpgradeableSkill skill = skillGo.AddComponent<TestChildUpgradeableSkill>();
            SkillInfoSO info = CreateSkillInfo(skillName);
            TestScriptableConfig config = TrackScriptableObject<TestScriptableConfig>();
            config.Stat = CreateIntStat(1, 5, 1, 1);
            skill.Setup(info, config);
            return skill;
        }

        private SkillInfoSO CreateSkillInfo(string skillName)
        {
            SkillInfoSO info = TrackScriptableObject<SkillInfoSO>();
            SetPrivateField(typeof(SkillInfoSO), info, "<Name>k__BackingField", skillName);
            info.name = skillName;
            return info;
        }

        private TestInitializableItem CreateInitializableItem(string name)
        {
            GameObject go = TrackGameObject(name, active: false);
            return go.AddComponent<TestInitializableItem>();
        }

        private static List<TestInitializableItem> CollectItems(IEnumerable<TestInitializableItem> source)
        {
            List<TestInitializableItem> result = new();
            foreach (TestInitializableItem item in source)
            {
                result.Add(item);
            }

            return result;
        }

        private static IntUpgradeableStat CreateIntStat(int min, int max, int upgradeMin, int upgradeMax)
        {
            IntUpgradeableStat stat = new IntUpgradeableStat(
                min,
                max,
                new IntValueRange(min, max),
                new IntValueRange(upgradeMin, upgradeMax),
                false);
            stat.RestoreRuntimeState(min, true);
            return stat;
        }

        private static void InvokeMethod(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            method.Invoke(target, args);
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        private sealed class TestScriptableConfig : SkillUpgradeableStatsConfig
        {
            public int ResetCount { get; private set; }
            public IntUpgradeableStat Stat { get; set; }

            public override void ResetRuntimeState()
            {
                ResetCount++;
            }

            public override void AppendStatsForDisplay(List<NameUpgradableStatPair> destination)
            {
                if (Stat != null)
                {
                    destination.Add(new NameUpgradableStatPair(nameof(Stat), Stat, null));
                }
            }
        }

        private sealed class TestChildUpgradeableSkill : UpgradeableSkill<TestScriptableConfig>
        {
            private SkillInfoSO _skillInfo;
            private TestScriptableConfig _backingConfig;

            public override SkillInfoSO SkillInfo
            {
                get { return _skillInfo; }
                protected set { _skillInfo = value; }
            }

            protected override TestScriptableConfig _config
            {
                get { return _backingConfig; }
                set { _backingConfig = value; }
            }

            public TestScriptableConfig ConfigRef
            {
                get { return _backingConfig; }
            }

            public void Setup(SkillInfoSO info, TestScriptableConfig config)
            {
                _skillInfo = info;
                _backingConfig = config;
            }
        }

        private sealed class TestInitializableItem : MonoBehaviour, IInitializableWithScriptableConfig<TestScriptableConfig>
        {
            private bool _initialized;

            public int InitializeCount { get; private set; }
            public TestScriptableConfig LastConfig { get; private set; }

            public void Initialize(TestScriptableConfig config)
            {
                _initialized = true;
                InitializeCount++;
                LastConfig = config;
                gameObject.SetActive(true);
            }

            public bool IsInitialized()
            {
                return _initialized;
            }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            private readonly FakeLevelController _levelController = new FakeLevelController();

            public FakePlayerManager(ISkillsRegistry skillsRegistry)
            {
                SkillsRegistry = skillsRegistry;
            }

            public Audio.IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public ICarController CarController { get { return null; } }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get { return _levelController; } }
            public ISkillsRegistry SkillsRegistry { get; }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }
            public HealthSystem.IHealth Health { get { return null; } }
            public GameObject GameObject { get { return null; } }
        }

        private sealed class FakeLevelController : ILevelController
        {
            public LevelData LevelData { get { return new LevelData(1, 0f, 100f); } }

            public event EventHandler<ValueEventArgs<LevelData>> OnExpChange
            {
                add { }
                remove { }
            }

            public event EventHandler<ValueEventArgs<LevelData>> OnLvlUp
            {
                add { }
                remove { }
            }

            public void AddExp(float value)
            {
            }
        }
    }
}
