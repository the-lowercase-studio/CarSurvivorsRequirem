using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Audio;
using Assets.Scripts.Audio.Constants;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.Common.Types;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.Navigation.FlowFieldSystem;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.Skills;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class ExpParticleSpawnerTests
    {
        private const string EXP_PARTICLE_PREFAB_PATH = "Assets/Prefabs/Exp/ExpParticle.prefab";
        private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/Player/Player.prefab";
        private const float TOLERANCE = 0.0001f;
        private const int DEFAULT_RANDOM_SEED = 24680;

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();
        private readonly List<ExpParticleSpawner> _createdSpawners = new();
        private UnityEngine.Random.State _previousRandomState;

        [SetUp]
        public void SetUp()
        {
            _previousRandomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(DEFAULT_RANDOM_SEED);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdSpawners.Count; i++)
            {
                ExpParticleSpawner spawner = _createdSpawners[i];
                if (spawner == null)
                {
                    continue;
                }

                FieldInfo stagingField = typeof(ExpParticleSpawner).GetField(
                    "_injectionStagingParent",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Transform stagingParent = stagingField != null ? stagingField.GetValue(spawner) as Transform : null;
                if (stagingParent != null)
                {
                    UnityEngine.Object.DestroyImmediate(stagingParent.gameObject);
                    stagingField.SetValue(spawner, null);
                }
            }

            _createdSpawners.Clear();

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
            UnityEngine.Random.state = _previousRandomState;
        }

        [Test]
        public void Awake_WithMissingPrefabAndEmptyDividers_LogsExplicitErrors()
        {
            // Arrange
            GameObject spawnerGo = new GameObject("BrokenSpawner");
            spawnerGo.SetActive(false);
            _createdObjects.Add(spawnerGo);

            ExpParticleSpawner spawner = spawnerGo.AddComponent<ExpParticleSpawner>();
            _createdSpawners.Add(spawner);
            SetPrivateField(typeof(ExpParticleSpawner), spawner, "_expTresholdDeviders", BuildDividerArray(Array.Empty<(float, float)>()));

            LogAssert.Expect(LogType.Error, "[ExpParticleSpawner] ExpParticlePrefab is not assigned on 'BrokenSpawner'!");
            LogAssert.Expect(LogType.Error, "[ExpParticleSpawner] ExpTresholdDeviders is empty on 'BrokenSpawner'!");

            // Act
            InvokeLifecycleMethod(spawner, "Awake");

            // Assert
            Transform stagingParent = GetPrivateField<Transform>(typeof(ExpParticleSpawner), spawner, "_injectionStagingParent");
            Assert.IsNotNull(stagingParent);
            Assert.IsFalse(stagingParent.gameObject.activeSelf);

            // Act 2 — OnDestroy in EditMode destroys staging parent cleanly without Destroy error
            InvokeLifecycleMethod(spawner, "OnDestroy");
            Assert.IsNull(GetPrivateField<Transform>(typeof(ExpParticleSpawner), spawner, "_injectionStagingParent"));
        }

        [Test]
        public void DrainQueuedSpawns_WithEmptyQueue_DoesNotSpawnParticles()
        {
            // Arrange
            SpawnerHarness harness = CreateSpawnerHarness(
                yOffset: 0.75f,
                spawningRadius: 2f,
                dividers: new[] { (0f, 1f) });

            // Act
            harness.Spawner.DrainQueuedSpawns();

            // Assert
            Assert.AreEqual(0u, harness.Spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(0, harness.ParticlesParent.childCount);
        }

        [Test]
        public void Spawn_WithZeroCountDefaultCountAndMultiCount_EnqueuesAndPlacesParticlesUnderParentAtYOffset()
        {
            // Arrange
            SpawnerHarness harness = CreateSpawnerHarness(
                yOffset: 1.25f,
                spawningRadius: 3f,
                dividers: new[] { (0f, 1f) });
            Vector3 origin = new Vector3(10f, 4f, -6f);

            // Act
            harness.Spawner.Spawn(origin, 15f, count: 0);
            harness.Spawner.DrainQueuedSpawns();
            Assert.AreEqual(0u, harness.Spawner.CurrentlySpawnedObjectsCount);

            harness.Spawner.Spawn(origin, 15f);
            harness.Spawner.Spawn(origin, 20f, count: 2);
            harness.Spawner.DrainQueuedSpawns();

            // Assert
            Assert.AreEqual(3u, harness.Spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(3, harness.ParticlesParent.childCount);

            for (int i = 0; i < harness.ParticlesParent.childCount; i++)
            {
                Transform child = harness.ParticlesParent.GetChild(i);
                Assert.IsTrue(child.gameObject.activeSelf);
                Assert.AreEqual(1.25f, child.position.y, TOLERANCE);

                float dx = child.position.x - origin.x;
                float dz = child.position.z - origin.z;
                Assert.LessOrEqual(Mathf.Sqrt(dx * dx + dz * dz), 3f + TOLERANCE);
            }
        }

        [TestCase(5f, 2, 5f, TestName = "DrainQueuedSpawns_BelowLowestThreshold_FallsBackToFirstDividerAndConservesTotalExp")]
        [TestCase(25f, 2, 25f, TestName = "DrainQueuedSpawns_FirstThresholdRange_UsesFirstDividerAndConservesTotalExp")]
        [TestCase(50f, 3, 50f, TestName = "DrainQueuedSpawns_ExactMiddleThreshold_UsesMiddleDividerAndConservesTotalExp")]
        [TestCase(250f, 5, 250f, TestName = "DrainQueuedSpawns_AboveHighestThreshold_UsesHighestDividerAndConservesTotalExp")]
        public void DrainQueuedSpawns_ThresholdDividerSelection_SpawnsMatchingCountAndAwardsDividedExpPerParticle(
            float queuedExp,
            int expectedParticleCount,
            float expectedTotalAwardedExp)
        {
            // Arrange
            SpawnerHarness harness = CreateSpawnerHarness(
                yOffset: 0.5f,
                spawningRadius: 1f,
                dividers: new[]
                {
                    (10f, 2f),
                    (50f, 3f),
                    (200f, 5f)
                });

            harness.Spawner.Spawn(Vector3.zero, queuedExp);

            // Act
            harness.Spawner.DrainQueuedSpawns();

            // Assert
            Assert.AreEqual((uint)expectedParticleCount, harness.Spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(expectedParticleCount, harness.ParticlesParent.childCount);

            Collider playerCollider = CreatePlayerCollider();
            for (int i = 0; i < harness.ParticlesParent.childCount; i++)
            {
                ExpParticle particle = harness.ParticlesParent.GetChild(i).GetComponent<ExpParticle>();
                InvokeLifecycleMethod(particle, "Awake");
                InvokeTriggerEnter(particle, playerCollider);
                particle.AdvanceCollection(0.1f);
            }

            Assert.AreEqual(expectedTotalAwardedExp, harness.LevelController.TotalAddedExp, TOLERANCE);
            Assert.AreEqual(0u, harness.Spawner.CurrentlySpawnedObjectsCount);
        }

        [Test]
        public void SpawnedParticleLifecycle_TriggerCollectReleaseAndPoolReuse_UpdatesCountEventsAndAvoidsDuplicateHandlers()
        {
            // Arrange
            SpawnerHarness harness = CreateSpawnerHarness(
                yOffset: 0.5f,
                spawningRadius: 0f,
                dividers: new[] { (0f, 1f) });
            Collider playerCollider = CreatePlayerCollider();

            int releasedEvents = 0;
            harness.Spawner.OnSpawnedEntityReleased += delegate { releasedEvents++; };

            // Act 1 — Spawn, trigger collection, shrink to release
            harness.Spawner.Spawn(Vector3.zero, 40f);
            harness.Spawner.DrainQueuedSpawns();
            Assert.AreEqual(1u, harness.Spawner.CurrentlySpawnedObjectsCount);

            ExpParticle firstParticle = harness.ParticlesParent.GetChild(0).GetComponent<ExpParticle>();
            InvokeLifecycleMethod(firstParticle, "Awake");
            InvokeTriggerEnter(firstParticle, playerCollider);
            firstParticle.AdvanceCollection(0.1f);

            // Assert 1
            Assert.AreEqual(40f, harness.LevelController.TotalAddedExp, TOLERANCE);
            Assert.AreEqual(0u, harness.Spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(1, releasedEvents);
            Assert.IsFalse(firstParticle.gameObject.activeSelf);

            // Act 2 — Re-spawn from pool and collect second exp value
            harness.Spawner.Spawn(Vector3.one, 60f);
            harness.Spawner.DrainQueuedSpawns();
            Assert.AreEqual(1u, harness.Spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(1, harness.ParticlesParent.childCount);

            ExpParticle reusedParticle = harness.ParticlesParent.GetChild(0).GetComponent<ExpParticle>();
            Assert.AreSame(firstParticle, reusedParticle);
            Assert.IsTrue(reusedParticle.gameObject.activeSelf);

            InvokeTriggerEnter(reusedParticle, playerCollider);
            reusedParticle.AdvanceCollection(0.1f);

            // Assert 2
            Assert.AreEqual(100f, harness.LevelController.TotalAddedExp, TOLERANCE);
            Assert.AreEqual(2, harness.LevelController.AddExpCallCount);
            Assert.AreEqual(0u, harness.Spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(2, releasedEvents);
        }

        [Test]
        public void AuthoredPrefabs_ExpParticleAndPlayer_HaveExpectedWiringAndNoSelfInjector()
        {
            // Arrange
            GameObject expPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EXP_PARTICLE_PREFAB_PATH);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);

            // Assert — ExpParticle prefab invariants
            Assert.IsNotNull(expPrefab);
            ExpParticle expParticle = expPrefab.GetComponent<ExpParticle>();
            Assert.IsNotNull(expParticle);
            Assert.IsNotNull(expPrefab.GetComponent<FlowFieldMovementController>());

            Collider expCollider = expPrefab.GetComponent<Collider>();
            Assert.IsNotNull(expCollider);
            Assert.IsTrue(expCollider.isTrigger);

            MeshRenderer meshRenderer = GetPrivateField<MeshRenderer>(typeof(ExpParticle), expParticle, "_meshRenderer");
            Assert.IsNotNull(meshRenderer);

            Array thresholds = GetPrivateField<Array>(typeof(ExpParticle), expParticle, "_particleApearanceByTreshold");
            Assert.IsNotNull(thresholds);
            Assert.Greater(thresholds.Length, 0);

            AudioClipPlayer audioPlayer = expPrefab.GetComponentInChildren<AudioClipPlayer>(true);
            Assert.IsNotNull(audioPlayer);

            MonoBehaviour[] allBehaviours = expPrefab.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                Assert.IsNotNull(allBehaviours[i]);
                Assert.AreNotEqual("GameObjectSelfInjector", allBehaviours[i].GetType().Name);
            }

            // Assert — Player prefab invariants
            Assert.IsNotNull(playerPrefab);
            PlayerManager playerManager = playerPrefab.GetComponent<PlayerManager>();
            LevelController levelController = playerPrefab.GetComponent<LevelController>();
            Assert.IsNotNull(playerManager);
            Assert.IsNotNull(levelController);

            AnimationCurve expCurve = GetPrivateField<AnimationCurve>(typeof(LevelController), levelController, "_expCurve");
            Assert.IsNotNull(expCurve);
            Assert.Greater(expCurve.length, 0);
            Assert.Greater(expCurve.Evaluate(1f), 0f);
        }

        private SpawnerHarness CreateSpawnerHarness(
            float yOffset,
            float spawningRadius,
            (float threshold, float divider)[] dividers)
        {
            ExpParticle prefabTemplate = CreateExpParticlePrefabTemplate();
            FakeLevelController levelController = new FakeLevelController();
            FakePlayerManager playerManager = new FakePlayerManager(levelController);
            FakeGridManager gridManager = new FakeGridManager();

            Container container = BuildContainer(delegate(ContainerBuilder builder)
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
                builder.AddSingleton(gridManager, typeof(IGridManager));
            });

            GameObject particlesParentGo = new GameObject("SpawnedExpParticles");
            _createdObjects.Add(particlesParentGo);

            GameObject spawnerGo = new GameObject("TestExpParticleSpawner");
            spawnerGo.SetActive(false);
            _createdObjects.Add(spawnerGo);

            ExpParticleSpawner spawner = spawnerGo.AddComponent<ExpParticleSpawner>();
            _createdSpawners.Add(spawner);

            SetPrivateField(typeof(ExpParticleSpawner), spawner, "_expParticlesParent", particlesParentGo.transform);
            SetPrivateField(typeof(ExpParticleSpawner), spawner, "_particlesYOffset", yOffset);
            SetPrivateField(typeof(ExpParticleSpawner), spawner, "_expParticlePrefab", prefabTemplate);
            SetPrivateField(typeof(ExpParticleSpawner), spawner, "_expTresholdDeviders", BuildDividerArray(dividers));
            SetPrivateField(typeof(ExpParticleSpawner), spawner, "_spawningCircleRadius", spawningRadius);

            GameObjectInjector.InjectObject(spawnerGo, container);
            InvokeLifecycleMethod(spawner, "Awake");

            return new SpawnerHarness(spawner, particlesParentGo.transform, levelController);
        }

        private ExpParticle CreateExpParticlePrefabTemplate()
        {
            GameObject prefabGo = new GameObject("ExpParticleTemplate");
            prefabGo.SetActive(false);
            _createdObjects.Add(prefabGo);

            GameObject visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(prefabGo.transform, false);
            MeshRenderer renderer = visualGo.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            Material material = new Material(shader);
            _createdObjects.Add(material);

            ExpParticle particle = prefabGo.AddComponent<ExpParticle>();
            SetPrivateField(typeof(ExpParticle), particle, "_movementSpeed", 8f);
            SetPrivateField(typeof(ExpParticle), particle, "_disapearingDuration", 0.1f);
            SetPrivateField(typeof(ExpParticle), particle, "_visual", visualGo);
            SetPrivateField(typeof(ExpParticle), particle, "_meshRenderer", renderer);
            SetPrivateField(typeof(ExpParticle), particle, "_particleApearanceByTreshold", BuildAppearanceArray(0f, material, 1f, 1f));

            return particle;
        }

        private Collider CreatePlayerCollider()
        {
            GameObject playerGo = new GameObject("PlayerTriggerCollider");
            int playerLayer = GetLayerIndexFromMask(EntityLayers.Player.value);
            playerGo.layer = playerLayer < 0 ? 0 : playerLayer;
            _createdObjects.Add(playerGo);
            return playerGo.AddComponent<BoxCollider>();
        }

        private static int GetLayerIndexFromMask(int mask)
        {
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private Container BuildContainer(Action<ContainerBuilder> configure)
        {
            ContainerBuilder builder = new ContainerBuilder().SetName("ExpSpawnerTestContainer");
            configure?.Invoke(builder);
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private static Array BuildDividerArray((float threshold, float divider)[] entries)
        {
            Type entryType = typeof(ExpParticleSpawner).GetNestedType("ExpTresholdDevider", BindingFlags.NonPublic);
            Array array = Array.CreateInstance(entryType, entries.Length);

            FieldInfo thresholdField = entryType.GetField("_treshold", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo dividerField = entryType.GetField("_divider", BindingFlags.Instance | BindingFlags.NonPublic);

            for (int i = 0; i < entries.Length; i++)
            {
                object boxed = Activator.CreateInstance(entryType);
                thresholdField.SetValue(boxed, entries[i].threshold);
                dividerField.SetValue(boxed, entries[i].divider);
                array.SetValue(boxed, i);
            }

            return array;
        }

        private static Array BuildAppearanceArray(float threshold, Material material, float minScale, float maxScale)
        {
            Type entryType = typeof(ExpParticle).GetNestedType("ExpParticleApearanceByTreshold", BindingFlags.NonPublic);
            Array array = Array.CreateInstance(entryType, 1);

            object boxed = Activator.CreateInstance(entryType);
            entryType.GetField("_treshold", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boxed, threshold);
            entryType.GetField("_material", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boxed, material);
            entryType.GetField("_scaleValueRange", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(boxed, new FloatValueRange(minScale, maxScale));
            array.SetValue(boxed, 0);

            return array;
        }

        private static void InvokeTriggerEnter(ExpParticle particle, Collider other)
        {
            MethodInfo method = typeof(ExpParticle).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(particle, new object[] { other });
        }

        private static T GetPrivateField<T>(Type ownerType, object target, string fieldName)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        private static void InvokeLifecycleMethod(object target, string methodName)
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
                    return;
                }

                type = type.BaseType;
            }
        }

        private sealed class SpawnerHarness
        {
            public SpawnerHarness(
                ExpParticleSpawner spawner,
                Transform particlesParent,
                FakeLevelController levelController)
            {
                Spawner = spawner;
                ParticlesParent = particlesParent;
                LevelController = levelController;
            }

            public ExpParticleSpawner Spawner { get; private set; }
            public Transform ParticlesParent { get; private set; }
            public FakeLevelController LevelController { get; private set; }
        }

        private sealed class FakeLevelController : ILevelController
        {
            public LevelData LevelData { get; private set; } = new LevelData(1, 0f, 100f);
            public float TotalAddedExp { get; private set; }
            public int AddExpCallCount { get; private set; }

            public event EventHandler<ValueEventArgs<LevelData>> OnExpChange;
            public event EventHandler<ValueEventArgs<LevelData>> OnLvlUp;

            public void AddExp(float value)
            {
                TotalAddedExp += value;
                AddExpCallCount++;
                LevelData = new LevelData(1, TotalAddedExp, 100f);
                OnExpChange?.Invoke(this, new ValueEventArgs<LevelData>(LevelData));
            }

            public void SilenceUnusedWarning()
            {
                OnLvlUp?.Invoke(this, new ValueEventArgs<LevelData>(LevelData));
            }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            public FakePlayerManager(ILevelController levelController)
            {
                LevelController = levelController;
            }

            public IHealth Health { get { return null; } }
            public GameObject GameObject { get { return null; } }
            public IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public ICarController CarController { get { return null; } }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get; private set; }
            public ISkillsRegistry SkillsRegistry { get { return null; } }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }
        }

        private sealed class FakeGridManager : IGridManager
        {
            public Cell DestinationCell { get { return null; } }
            public Grid GridPlayerChunk { get { return null; } }
            public Grid WorldGrid { get { return null; } }
        }
    }
}
