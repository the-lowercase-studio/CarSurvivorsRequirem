using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Assets.Scripts.Audio;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies.Bosses;
using Assets.Scripts.Enemies.Bosses.Constants;
using Assets.Scripts.Enemies.Bosses.Golem;
using Assets.Scripts.Enemies.Bosses.Golem.Config;
using Assets.Scripts.Enemies.Bosses.Towers.Arena;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.Interactables;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.Navigation.Constants;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Skills;
using Assets.Scripts.Spawners.Swarm;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.UI.HUD;
using Assets.Scripts.VFX;
using DG.Tweening;
using NUnit.Framework;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class BossEncounterServiceTests
    {
        private const float TOLERANCE = 0.0001f;
        private const string GOLEM_PREFAB_PATH = "Assets/Prefabs/Enemies/Bosses/Golem/GolemBoss.prefab";
        private static readonly Vector3 ISOLATED_ORIGIN = new Vector3(40000f, 1200f, 40000f);

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            DOTween.Clear(true);

            for (int i = 0; i < _createdContainers.Count; i++)
            {
                _createdContainers[i]?.Dispose();
            }

            _createdContainers.Clear();

            DestroyAnySceneInstances<GolemBoss>();
            DestroyAnySceneInstances<NextStagePortal>();
            DestroyAnySceneInstances<PortalInjectionProbe>();
            DestroyAnySceneInstances<CircularTelegraphIndicator>();
            DestroyAnySceneInstances<VFXPlayer>();
            DestroyGameObjectByName("GolemBoss_InjectionStaging");
            DestroyGameObjectByName("PortalProbePrefab_InjectionStaging");

            for (int i = 0; i < _createdObjects.Count; i++)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void TowerRegistrationAndDefeat_TracksCountsRetainsDefeatedOnUnregisterAndDeduplicates()
        {
            // Arrange — Configured _requiredTowersForGolemSpawn = 2
            BossServiceHarness harness = CreateServiceHarness(requiredTowers: 2);
            FakeEncounterArena tower1 = CreateFakeArena(new Vector3(10f, 0f, 10f), radius: 20f);
            FakeEncounterArena tower2 = CreateFakeArena(new Vector3(50f, 0f, 50f), radius: 20f);
            FakeEncounterArena tower3 = CreateFakeArena(new Vector3(90f, 0f, 90f), radius: 20f);

            List<(int defeated, int required)> notifications = new List<(int, int)>();
            harness.Service.OnTowerDefeated += delegate (int defeated, int required)
            {
                notifications.Add((defeated, required));
            };

            // Zero towers registered -> defaults to configured 2
            int defaultRequired = harness.Service.RequiredTowersCount;

            // Null safety
            harness.Service.RegisterTower(null);
            harness.Service.UnregisterTower(null);
            harness.Service.NotifyTowerDefeated(null);

            // Register 1 tower -> Required caps at 1; register 3 -> caps at 2
            harness.Service.RegisterTower(tower1);
            int requiredWithOne = harness.Service.RequiredTowersCount;
            harness.Service.RegisterTower(tower2);
            harness.Service.RegisterTower(tower3);
            int requiredWithThree = harness.Service.RequiredTowersCount;

            // Defeat tower1 twice -> second call is deduplicated
            harness.Service.NotifyTowerDefeated(tower1);
            harness.Service.NotifyTowerDefeated(tower1);

            // Unregistering defeated tower1 retains it; unregistering undefeated tower3 removes it
            harness.Service.UnregisterTower(tower1);
            harness.Service.UnregisterTower(tower3);

            // Defeat tower2 -> RemainingTowersCount reaches 0
            harness.Service.NotifyTowerDefeated(tower2);

            // Assert
            Assert.AreEqual(2, defaultRequired);
            Assert.AreEqual(1, requiredWithOne);
            Assert.AreEqual(2, requiredWithThree);
            Assert.AreEqual(2, harness.Service.DefeatedTowersCount);
            Assert.AreEqual(0, harness.Service.RemainingTowersCount);
            Assert.AreEqual(2, notifications.Count);
            Assert.AreEqual((1, 2), notifications[0]);
            Assert.AreEqual((2, 2), notifications[1]);
        }

        [Test]
        public void IsPositionWithinAnyTowerArena_EvaluatesHorizontalDistanceBufferAndIgnoresDestroyedTowers()
        {
            // Arrange
            BossServiceHarness harness = CreateServiceHarness(requiredTowers: 2);
            FakeEncounterArena activeTower = CreateFakeArena(new Vector3(100f, 0f, 200f), radius: 10f);
            FakeEncounterArena destroyedTower = CreateFakeArena(new Vector3(300f, 0f, 300f), radius: 20f);
            harness.Service.RegisterTower(activeTower);
            harness.Service.RegisterTower(destroyedTower);
            UnityEngine.Object.DestroyImmediate(destroyedTower.gameObject);

            // Act & Assert — Ignores Y offset; exact radius + buffer boundary is inclusive, +0.01 outside
            Assert.IsTrue(harness.Service.IsPositionWithinAnyTowerArena(new Vector3(110f, 99f, 200f), extraBuffer: 0f));
            Assert.IsFalse(harness.Service.IsPositionWithinAnyTowerArena(new Vector3(110.01f, 0f, 200f), extraBuffer: 0f));
            Assert.IsTrue(harness.Service.IsPositionWithinAnyTowerArena(new Vector3(115f, 0f, 200f), extraBuffer: 5f));
            Assert.IsFalse(harness.Service.IsPositionWithinAnyTowerArena(new Vector3(110.01f, 0f, 200f), extraBuffer: -5f));
            Assert.IsFalse(harness.Service.IsPositionWithinAnyTowerArena(new Vector3(300f, 0f, 300f), extraBuffer: 0f));
        }

        [Test]
        public void CollectEngagedBosses_ValidatesNullBufferClearsPreviousItemsAndFiltersActiveLivingTowers()
        {
            // Arrange
            BossServiceHarness harness = CreateServiceHarness(requiredTowers: 2);
            FakeEncounterArena engagedLiving = CreateFakeArena(Vector3.zero, 10f, isActive: true, isCompleted: false, isAlive: true);
            FakeEncounterArena engagedDead = CreateFakeArena(Vector3.zero, 10f, isActive: true, isCompleted: false, isAlive: false);
            FakeEncounterArena completedTower = CreateFakeArena(Vector3.zero, 10f, isActive: true, isCompleted: true, isAlive: true);
            FakeEncounterArena idleTower = CreateFakeArena(Vector3.zero, 10f, isActive: false, isCompleted: false, isAlive: true);

            harness.Service.RegisterTower(engagedLiving);
            harness.Service.RegisterTower(engagedDead);
            harness.Service.RegisterTower(completedTower);
            harness.Service.RegisterTower(idleTower);

            List<IBossCombatTarget> buffer = new List<IBossCombatTarget> { engagedDead.BossTarget };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
            {
                harness.Service.CollectEngagedBosses(null);
            });

            int count = harness.Service.CollectEngagedBosses(buffer);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, buffer.Count);
            Assert.AreSame(engagedLiving.BossTarget, buffer[0]);
        }

        [Test]
        public void EncounterHudAndSwarmSuppression_HandleEngageNullSourceDisengageAndOnDisable()
        {
            // Arrange
            BossServiceHarness harness = CreateServiceHarness(requiredTowers: 2);
            FakeBossHudSource hudSource = new FakeBossHudSource();

            // Act 1 — Null source still marks IsAnyBossActive and suppresses swarm, skipping HUD Show
            harness.Service.NotifyEncounterEngaged(null, "SilentBoss");
            bool activeAfterNullEngage = harness.Service.IsAnyBossActive;
            bool suppressedAfterNullEngage = harness.SwarmFreezer.IsSuppressed;
            int showsAfterNullEngage = harness.HudPresenter.ShowCalls.Count;

            // Act 2 — Valid source shows HUD and suppresses swarm; disengage hides HUD and restores swarm
            harness.Service.NotifyEncounterEngaged(hudSource, "MORTAR TOWER");
            harness.Service.NotifyEncounterDisengaged();

            // Act 3 — Re-engage then OnDisable cleans up HUD and suppression
            harness.Service.NotifyEncounterEngaged(hudSource, "RAILGUN TOWER");
            InvokeLifecycleMethod(harness.Service, "OnDisable");

            // Assert
            Assert.IsTrue(activeAfterNullEngage);
            Assert.IsTrue(suppressedAfterNullEngage);
            Assert.AreEqual(0, showsAfterNullEngage);

            Assert.AreEqual(2, harness.HudPresenter.ShowCalls.Count);
            Assert.AreSame(hudSource, harness.HudPresenter.ShowCalls[0].Source);
            Assert.AreEqual("MORTAR TOWER", harness.HudPresenter.ShowCalls[0].Name);
            Assert.AreEqual(2, harness.HudPresenter.HideCallCount);
            Assert.IsFalse(harness.Service.IsAnyBossActive);
            Assert.IsFalse(harness.SwarmFreezer.IsSuppressed);
        }

        [Test]
        public void SpawnGolemBoss_WhenPrefabNullOrUnregisteredStatueMismatch_DismissesRegisteredStatueOnlyAndSkipsSpawn()
        {
            // Arrange
            BossServiceHarness harness = CreateServiceHarness(requiredTowers: 2);
            FakeGolemStatue statueA = new FakeGolemStatue();
            FakeGolemStatue statueB = new FakeGolemStatue();

            harness.Service.RegisterGolemStatue(statueA);
            harness.Service.UnregisterGolemStatue(statueB);

            // Act 1 — statueA remains registered despite unregister(statueB)
            harness.Service.SpawnGolemBoss(null);
            int statueADismissCount = statueA.DismissCount;

            // Act 2 — Matching unregister prevents subsequent dismissal
            harness.Service.UnregisterGolemStatue(statueA);
            harness.Service.SpawnGolemBoss(null);

            // Assert
            Assert.AreEqual(1, statueADismissCount);
            Assert.IsFalse(harness.Service.IsGolemActive);
            Assert.IsFalse(harness.Service.IsAnyBossActive);
        }

        [Test]
        public void SpawnGolemBoss_And_Defeat_AppliesTierOverridesGuardDuplicatesAndSpawnsGroundSnappedPortal()
        {
            // Arrange — Floor at Y = 1200f under ISOLATED_ORIGIN; Player forward = +X; Tier override sets display name
            CreateWalkableFloor(ISOLATED_ORIGIN + new Vector3(15f, -0.5f, 0f), new Vector3(40f, 1f, 40f));
            GameObject playerGo = CreatePlainObject("BossTestPlayer");
            playerGo.transform.position = ISOLATED_ORIGIN;
            playerGo.transform.rotation = Quaternion.LookRotation(Vector3.right);

            GolemBoss golemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GOLEM_PREFAB_PATH).GetComponent<GolemBoss>();
            Assert.IsNotNull(golemPrefab);
            GolemBossConfigSO overrideConfig = ScriptableObject.CreateInstance<GolemBossConfigSO>();
            _createdObjects.Add(overrideConfig);

            GameObject portalPrefab = CreatePortalProbePrefab();
            TierBossConfig tierBossConfig = new TierBossConfig();
            SetPrivateField(typeof(TierBossConfig), tierBossConfig, "_golemConfigOverride", overrideConfig);
            SetPrivateField(typeof(TierBossConfig), tierBossConfig, "_golemDisplayNameOverride", "TIER 2 TITAN");

            BossServiceHarness harness = CreateServiceHarness(
                requiredTowers: 2,
                golemPrefab: golemPrefab,
                portalPrefab: portalPrefab,
                playerObject: playerGo,
                tierBossConfig: tierBossConfig,
                spawnOffsetDistance: 15f);

            // Act 1 — Spawn without explicit position -> uses player forward * 15 (+X);
            // ReflexPrefabInstantiator calls Object.Destroy on temp staging parent in EditMode.
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode!"));
            harness.Service.SpawnGolemBoss(null);

            GolemBoss activeGolem = GetPrivateField<GolemBoss>(typeof(BossEncounterService), harness.Service, "_activeGolemInstance");
            Assert.IsNotNull(activeGolem);
            _createdObjects.Add(activeGolem.gameObject);

            // Ensure Golem Awake/OnEnable ran in EditMode so Health & OnBossDefeated are active
            InitializeGolemInEditMode(activeGolem);

            // Duplicate SpawnGolemBoss while IsGolemActive == true must be ignored
            harness.Service.SpawnGolemBoss(ISOLATED_ORIGIN + new Vector3(99f, 0f, 99f));
            GolemBoss afterSecondSpawnCall = GetPrivateField<GolemBoss>(typeof(BossEncounterService), harness.Service, "_activeGolemInstance");

            // Act 2 — Kill Golem health (GolemBoss.Health_OnNoHealth emits OnBossDefeated synchronously) -> disengages HUD/swarm and spawns ground-snapped portal
            // ReflexPrefabInstantiator (portal) and GolemBoss.Health_OnNoHealth invoke Object.Destroy in EditMode.
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode!"));
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode!"));
            activeGolem.Health.DecreaseHealth(activeGolem.Health.MaxHealth);

            PortalInjectionProbe spawnedPortal = FindAndTrackSpawnedPortal();

            // Assert
            Assert.AreSame(activeGolem, afterSecondSpawnCall);
            Assert.AreEqual(ISOLATED_ORIGIN.x + 15f, activeGolem.transform.position.x, TOLERANCE);
            Assert.AreEqual(ISOLATED_ORIGIN.z, activeGolem.transform.position.z, TOLERANCE);
            Assert.AreSame(overrideConfig, activeGolem.Config);
            Assert.AreEqual("TIER 2 TITAN", harness.HudPresenter.ShowCalls[0].Name);
            Assert.IsFalse(harness.Service.IsAnyBossActive);
            Assert.IsFalse(harness.SwarmFreezer.IsSuppressed);
            Assert.IsNotNull(spawnedPortal);
            Assert.IsTrue(spawnedPortal.WasInjected);
            Assert.AreEqual(ISOLATED_ORIGIN.y + GridConstants.ROOT_GROUND_CLEARANCE, spawnedPortal.transform.position.y, TOLERANCE);
        }

        private PortalInjectionProbe FindAndTrackSpawnedPortal()
        {
            PortalInjectionProbe[] probes = UnityEngine.Object.FindObjectsByType<PortalInjectionProbe>(FindObjectsInactive.Include);
            for (int i = 0; i < probes.Length; i++)
            {
                if (probes[i] != null && probes[i].transform.parent == null)
                {
                    _createdObjects.Add(probes[i].gameObject);
                    return probes[i];
                }
            }

            return null;
        }

        private GameObject CreatePortalProbePrefab()
        {
            GameObject holder = new GameObject("PortalPrefabHolder");
            holder.SetActive(false);
            _createdObjects.Add(holder);

            GameObject prefab = new GameObject("PortalProbePrefab");
            prefab.transform.SetParent(holder.transform, false);
            prefab.AddComponent<PortalInjectionProbe>();
            _createdObjects.Add(prefab);
            return prefab;
        }

        private void InitializeGolemInEditMode(GolemBoss golem)
        {
            if (golem.Health != null)
            {
                return;
            }

            MonoBehaviour[] behaviours = golem.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                {
                    InvokeOptionalLifecycleMethod(behaviours[i], "Awake");
                }
            }

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].enabled && behaviours[i].gameObject.activeInHierarchy)
                {
                    InvokeOptionalLifecycleMethod(behaviours[i], "OnEnable");
                }
            }
        }

        private BossServiceHarness CreateServiceHarness(
            int requiredTowers,
            GolemBoss golemPrefab = null,
            GameObject portalPrefab = null,
            GameObject playerObject = null,
            TierBossConfig tierBossConfig = null,
            float spawnOffsetDistance = BossEncounterConstants.DEFAULT_SPAWN_OFFSET_DISTANCE)
        {
            GameObject serviceGo = new GameObject("TestBossEncounterService");
            serviceGo.SetActive(false);
            _createdObjects.Add(serviceGo);
            BossEncounterService service = serviceGo.AddComponent<BossEncounterService>();

            SetPrivateField(typeof(BossEncounterService), service, "_requiredTowersForGolemSpawn", requiredTowers);
            SetPrivateField(typeof(BossEncounterService), service, "_golemBossPrefab", golemPrefab);
            SetPrivateField(typeof(BossEncounterService), service, "_nextStagePortalPrefab", portalPrefab);
            SetPrivateField(typeof(BossEncounterService), service, "_spawnOffsetDistance", spawnOffsetDistance);
            SetPrivateField(typeof(BossEncounterService), service, "_golemDisplayName", BossEncounterConstants.DEFAULT_GOLEM_DISPLAY_NAME);

            FakeBossHudPresenter hudPresenter = new FakeBossHudPresenter();
            FakeSwarmFreezer swarmFreezer = new FakeSwarmFreezer();
            FakePlayerManager playerManager = new FakePlayerManager(playerObject);
            FakeRunSessionService runSessionService = new FakeRunSessionService(tierBossConfig);

            Container container = new ContainerBuilder()
                .AddSingleton(hudPresenter, typeof(IBossHUDPresenter))
                .AddSingleton(swarmFreezer, typeof(ISwarmFreezer))
                .AddSingleton(playerManager, typeof(IPlayerManager))
                .AddSingleton(runSessionService, typeof(IRunSessionService))
                .AddSingleton(new FakeGridManager(), typeof(IGridManager))
                .AddSingleton(new FakeExpParticleSpawner(), typeof(IInWorldSpaceSpawner<ExpParticleSpawner, float>))
                .AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>))
                .AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState))
                .AddSingleton(service, typeof(IBossEncounterService))
                .Build();
            _createdContainers.Add(container);

            SetPrivateField(typeof(BossEncounterService), service, "_bossHUDPresenter", hudPresenter);
            SetPrivateField(typeof(BossEncounterService), service, "_swarmFreezer", swarmFreezer);
            SetPrivateField(typeof(BossEncounterService), service, "_playerManager", playerManager);
            SetPrivateField(typeof(BossEncounterService), service, "_runSessionService", runSessionService);
            SetPrivateField(typeof(BossEncounterService), service, "_container", container);

            serviceGo.SetActive(true);
            return new BossServiceHarness(service, hudPresenter, swarmFreezer);
        }

        private FakeEncounterArena CreateFakeArena(
            Vector3 center,
            float radius,
            bool isActive = false,
            bool isCompleted = false,
            bool isAlive = true)
        {
            GameObject go = new GameObject("FakeTowerArena");
            _createdObjects.Add(go);
            FakeEncounterArena arena = go.AddComponent<FakeEncounterArena>();
            arena.ArenaCenter = center;
            arena.ArenaRadius = radius;
            arena.IsEncounterActive = isActive;
            arena.IsEncounterCompleted = isCompleted;
            arena.BossTarget = new FakeBossCombatTarget(go.transform, isAlive);
            return arena;
        }

        private void CreateWalkableFloor(Vector3 center, Vector3 size)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.GreaterOrEqual(groundLayer, 0);

            GameObject floor = new GameObject("BossTestFloor");
            floor.layer = groundLayer;
            floor.transform.position = center;
            BoxCollider box = floor.AddComponent<BoxCollider>();
            box.size = size;
            _createdObjects.Add(floor);
            Physics.SyncTransforms();
        }

        private GameObject CreatePlainObject(string name)
        {
            GameObject go = new GameObject(name);
            _createdObjects.Add(go);
            return go;
        }

        private static void DestroyAnySceneInstances<T>() where T : Component
        {
            T[] found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].gameObject.scene.IsValid())
                {
                    UnityEngine.Object.DestroyImmediate(found[i].gameObject);
                }
            }
        }

        private static void DestroyGameObjectByName(string objectName)
        {
            Transform[] allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t != null && t.parent == null && t.name == objectName)
                {
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
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

        private static void InvokeLifecycleMethod(object target, string methodName)
        {
            if (!TryInvokeLifecycleMethod(target, methodName))
            {
                Assert.Fail($"Method '{methodName}' not found on {target.GetType().Name}.");
            }
        }

        private static void InvokeOptionalLifecycleMethod(object target, string methodName)
        {
            TryInvokeLifecycleMethod(target, methodName);
        }

        private static bool TryInvokeLifecycleMethod(object target, string methodName)
        {
            Type type = target.GetType();
            while (type != null)
            {
                MethodInfo method = type.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                if (method != null)
                {
                    Action callback = (Action)Delegate.CreateDelegate(typeof(Action), target, method);
                    callback();
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private readonly struct BossServiceHarness
        {
            public readonly BossEncounterService Service;
            public readonly FakeBossHudPresenter HudPresenter;
            public readonly FakeSwarmFreezer SwarmFreezer;

            public BossServiceHarness(BossEncounterService service, FakeBossHudPresenter hudPresenter, FakeSwarmFreezer swarmFreezer)
            {
                Service = service;
                HudPresenter = hudPresenter;
                SwarmFreezer = swarmFreezer;
            }
        }

        private sealed class PortalInjectionProbe : MonoBehaviour
        {
            [Inject] private readonly IGridManager _gridManager = null;

            public bool WasInjected
            {
                get
                {
                    return _gridManager != null;
                }
            }
        }

        private sealed class FakeEncounterArena : MonoBehaviour, IEncounterArenaController
        {
            public bool IsEncounterActive { get; set; }
            public bool IsEncounterCompleted { get; set; }
            public bool IsLeashCountdownActive { get { return false; } }
            public float RemainingLeashSeconds { get { return 0f; } }
            public float ArenaRadius { get; set; }
            public Vector3 ArenaCenter { get; set; }
            public IBossCombatTarget BossTarget { get; set; }

            public void NotifyDirectCombatEngaged()
            {
            }
        }

        private sealed class FakeBossCombatTarget : IBossCombatTarget
        {
            public Transform Transform { get; }
            public IHealth Health { get; }
            public BossHealthBarKind HealthBarKind { get { return BossHealthBarKind.Tower; } }
            public float EnrageHealthPercent { get { return 0.4f; } }
            public bool IsEnraged { get { return false; } }
            public bool IsActive { get { return Health.IsAlive(); } }

            public event Action OnPresentationChanged { add { } remove { } }

            public FakeBossCombatTarget(Transform transform, bool isAlive)
            {
                Transform = transform;
                Health = new FakeHealth(isAlive);
            }

            public void TakeDamage(float damage)
            {
            }

            public void TakeFullHpDamage()
            {
            }
        }

        private sealed class FakeHealth : IHealth
        {
            private readonly bool _isAlive;

            public float CurrentHealth { get { return _isAlive ? 100f : 0f; } }
            public float MaxHealth { get { return 100f; } set { } }

            public event EventHandler OnHealthChanged { add { } remove { } }
            public event EventHandler OnHealthDecreased { add { } remove { } }
            public event EventHandler OnHealthIncreased { add { } remove { } }
            public event EventHandler OnNoHealth { add { } remove { } }

            public FakeHealth(bool isAlive)
            {
                _isAlive = isAlive;
            }

            public void DecreaseHealth(float value) { }
            public void IncreaseHealth(float value) { }
            public bool IsAlive() { return _isAlive; }
        }

        private sealed class FakeBossHudSource : IBossHUDSource
        {
            public IHealth Health { get { return new FakeHealth(true); } }
            public BossHealthBarKind HealthBarKind { get { return BossHealthBarKind.Tower; } }
            public float EnrageHealthPercent { get { return 0.4f; } }
            public bool IsEnraged { get { return false; } }
            public bool IsActive { get { return true; } }

            public event Action OnPresentationChanged { add { } remove { } }
        }

        private sealed class FakeBossHudPresenter : IBossHUDPresenter
        {
            public List<RecordedHudShow> ShowCalls { get; } = new List<RecordedHudShow>();
            public int HideCallCount { get; private set; }

            public void Show(IBossHUDSource source, string bossName)
            {
                ShowCalls.Add(new RecordedHudShow(source, bossName));
            }

            public void Hide()
            {
                HideCallCount++;
            }
        }

        private readonly struct RecordedHudShow
        {
            public readonly IBossHUDSource Source;
            public readonly string Name;

            public RecordedHudShow(IBossHUDSource source, string name)
            {
                Source = source;
                Name = name;
            }
        }

        private sealed class FakeSwarmFreezer : ISwarmFreezer
        {
            public bool IsSuppressed { get; set; }
        }

        private sealed class FakeGolemStatue : IGolemStatue
        {
            public int DismissCount { get; private set; }

            public void DismissForExternalSpawn()
            {
                DismissCount++;
            }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            public GameObject GameObject { get; }
            public IHealth Health { get { return null; } }
            public IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public ICarController CarController { get { return null; } }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get { return null; } }
            public ISkillsRegistry SkillsRegistry { get { return null; } }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }

            public FakePlayerManager(GameObject gameObject)
            {
                GameObject = gameObject;
            }
        }

        private sealed class FakeRunSessionService : IRunSessionService
        {
            private readonly LoopTierConfig _activeTierConfig;

            public int CurrentTierIndex { get { return 0; } }
            public int TotalTiersCount { get { return 1; } }
            public bool IsFinalTier { get { return true; } }
            public LoopTierConfig ActiveTierConfig { get { return _activeTierConfig; } }
            public GameLoopProgressionConfigSO ProgressionConfig { get { return null; } }
            public bool HasPendingRestorationSnapshot { get { return false; } }
            public uint AccumulatedRunSeconds { get { return 0; } }
            public bool IsGodModeEnabled { get { return false; } }

            public FakeRunSessionService(TierBossConfig bossConfig)
            {
                if (bossConfig != null)
                {
                    _activeTierConfig = new LoopTierConfig();
                    SetPrivateField(typeof(LoopTierConfig), _activeTierConfig, "_bossConfig", bossConfig);
                }
            }

            public void CaptureSnapshotAndAdvanceTier(IPlayerManager playerManager, uint elapsedSeconds) { }
            public void CaptureSnapshotAndSetTier(int tierIndex, IPlayerManager playerManager, uint elapsedSeconds) { }
            public void CaptureFinalRunTimer(uint elapsedSeconds) { }
            public bool TryGetRestoredLevel(out byte level, out float exp) { level = 1; exp = 0f; return false; }
            public bool TryGetRestoredSkills(out IReadOnlyList<SavedSkillSnapshot> skills) { skills = null; return false; }
            public void SetGodModeEnabled(bool isEnabled) { }
            public void ResetSession() { }
            public void ResetSessionToTier(int tierIndex) { }
        }

        private sealed class FakeGridManager : IGridManager
        {
            public Cell DestinationCell { get { return null; } }
            public Grid GridPlayerChunk { get { return null; } }
            public Grid WorldGrid { get { return null; } }
        }

        private sealed class FakeExpParticleSpawner : IInWorldSpaceSpawner<ExpParticleSpawner, float>
        {
            public uint CurrentlySpawnedObjectsCount { get { return 0; } }
            public event EventHandler OnSpawnedEntityReleased { add { } remove { } }
            public void Spawn(Vector3 pos, float specificConfig, int count = 1) { }
        }

        private sealed class FakeDamageNumbersSpawner : IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>
        {
            public uint CurrentlySpawnedObjectsCount { get { return 0; } }
            public event EventHandler OnSpawnedEntityReleased { add { } remove { } }
            public void Spawn(Vector3 pos, DamageNubmersSpawnerConfig specificConfig, int count = 1) { }
        }

        private sealed class FakeTimeFreezeState : ITimeFreezeState
        {
            public bool IsFrozen { get { return false; } }
            public float UnfrozenTime { get { return 0f; } }
            public event Action<bool> OnFrozenChanged { add { } remove { } }
        }
    }
}
