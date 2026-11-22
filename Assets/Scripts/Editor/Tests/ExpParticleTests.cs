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
using UnityEngine;
using UnityEngine.TestTools;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class ExpParticleTests
    {
        private const float TOLERANCE = 0.0001f;
        private const int DEFAULT_RANDOM_SEED = 13579;

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();
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

        [TestCase(5f, 0, 0.5f, TestName = "SetSizeAndMaterial_LowestThreshold_SelectsBaseMaterialAndScale")]
        [TestCase(50f, 1, 1.25f, TestName = "SetSizeAndMaterial_ExactMiddleThreshold_SelectsMiddleMaterialAndScale")]
        [TestCase(249.9f, 1, 1.25f, TestName = "SetSizeAndMaterial_JustBelowHighThreshold_KeepsMiddleMaterialAndScale")]
        [TestCase(250f, 2, 2.5f, TestName = "SetSizeAndMaterial_ExactHighThreshold_SelectsHighMaterialAndScale")]
        [TestCase(1000f, 2, 2.5f, TestName = "SetSizeAndMaterial_AboveHighThreshold_SelectsHighMaterialAndScale")]
        public void SetSizeAndMaterialBasedOnExpAmount_ThresholdPartitions_SelectsHighestMatchingTier(
            float expAmount,
            int expectedTierIndex,
            float expectedScale)
        {
            // Arrange
            Material[] materials =
            {
                CreateTestMaterial("Tier0Mat"),
                CreateTestMaterial("Tier1Mat"),
                CreateTestMaterial("Tier2Mat")
            };

            ExpParticleHarness harness = CreateHarness(
                materials: materials,
                thresholds: new[]
                {
                    (0f, materials[0], 0.5f, 0.5f),
                    (50f, materials[1], 1.25f, 1.25f),
                    (250f, materials[2], 2.5f, 2.5f)
                });

            // Act
            harness.Particle.SetSizeAndMaterialBasedOnExpAmount(expAmount);

            // Assert
            Assert.AreSame(materials[expectedTierIndex], harness.MeshRenderer.sharedMaterial);
            Assert.AreEqual(expectedScale, harness.Particle.transform.localScale.x, TOLERANCE);
            Assert.AreEqual(expectedScale, harness.Particle.transform.localScale.y, TOLERANCE);
            Assert.AreEqual(expectedScale, harness.Particle.transform.localScale.z, TOLERANCE);
        }

        [Test]
        public void SetSizeAndMaterialBasedOnExpAmount_BelowMinimumThreshold_LeavesMaterialAndScaleUnchanged()
        {
            // Arrange
            Material initialMaterial = CreateTestMaterial("InitialMat");
            Material tierMaterial = CreateTestMaterial("TierMat");
            ExpParticleHarness harness = CreateHarness(
                materials: new[] { initialMaterial, tierMaterial },
                thresholds: new[] { (10f, tierMaterial, 2f, 2f) });

            harness.MeshRenderer.sharedMaterial = initialMaterial;
            harness.Particle.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            // Act
            harness.Particle.SetSizeAndMaterialBasedOnExpAmount(9.9f);

            // Assert
            Assert.AreSame(initialMaterial, harness.MeshRenderer.sharedMaterial);
            Assert.AreEqual(1.5f, harness.Particle.transform.localScale.x, TOLERANCE);
        }

        [Test]
        public void SetSizeAndMaterialBasedOnExpAmount_EmptyThresholds_LogsError()
        {
            // Arrange
            ExpParticleHarness harness = CreateHarness(
                materials: Array.Empty<Material>(),
                thresholds: Array.Empty<(float, Material, float, float)>());
            LogAssert.Expect(LogType.Error, "Particle apearance by treshold is not set set for: TestExpParticle");

            // Act
            harness.Particle.SetSizeAndMaterialBasedOnExpAmount(25f);

            // Assert
            Assert.AreEqual(25f, GetPrivateField<float>(typeof(ExpParticle), harness.Particle, "_expAmount"), TOLERANCE);
        }

        [Test]
        public void OnTriggerEnter_PlayerVsNonPlayerAndRepeatedEntries_RaisesOnExpReachedTargetOnlyOnceForPlayer()
        {
            // Arrange
            ExpParticleHarness harness = CreateDefaultHarness();
            Collider nonPlayerCollider = CreateColliderOnLayer("NonPlayerCollider", GetLayerIndexFromMask(EntityLayers.Enemy.value));
            Collider playerCollider = CreateColliderOnLayer("PlayerCollider", GetLayerIndexFromMask(EntityLayers.Player.value));

            int reachedCount = 0;
            harness.Particle.OnExpReachedTarget += delegate { reachedCount++; };

            // Act 1 — Non-player collider is ignored
            InvokeTriggerEnter(harness.Particle, nonPlayerCollider);
            Assert.AreEqual(0, reachedCount);

            // Act 2 — Player collider before collection raises event
            InvokeTriggerEnter(harness.Particle, playerCollider);
            Assert.AreEqual(1, reachedCount);

            // Act 3 — Once CollectExp marks particle collected, subsequent player trigger entries are ignored
            harness.Particle.CollectExp();
            InvokeTriggerEnter(harness.Particle, playerCollider);

            // Assert
            Assert.AreEqual(1, reachedCount);
        }

        [Test]
        public void CollectExp_AndAdvanceCollection_ShrinksSmoothlyAwardsExpOncePlaysAudioAndFiresReleaseAndCallback()
        {
            // Arrange
            ExpParticleHarness harness = CreateDefaultHarness(disappearingDuration: 0.2f, includeAudioPlayer: true);
            harness.Particle.SetSizeAndMaterialBasedOnExpAmount(75f);
            harness.Particle.transform.localScale = new Vector3(2f, 2f, 2f);

            int callbackCount = 0;
            int releaseCount = 0;
            List<string> completionOrder = new();
            harness.Particle.OnCanBeReleased += delegate
            {
                releaseCount++;
                completionOrder.Add("CanBeReleased");
            };

            // Act 1 — First CollectExp begins shrink and plays audio; duplicate CollectExp is ignored
            harness.Particle.CollectExp(delegate
            {
                callbackCount++;
                completionOrder.Add("Callback");
            });
            harness.Particle.CollectExp(delegate { callbackCount += 100; });

            Assert.AreEqual(1, harness.AudioPlayer.PlayedClipNames.Count);
            Assert.AreEqual(AudioConstants.EXP_COLLECTED_CLIP_NAME, harness.AudioPlayer.PlayedClipNames[0]);
            Assert.AreEqual(0f, harness.LevelController.TotalAddedExp, TOLERANCE);

            // Act 2 — Half duration shrinks linearly to 50% without completing yet
            harness.Particle.AdvanceCollection(0.1f);
            Assert.AreEqual(1f, harness.Particle.transform.localScale.x, TOLERANCE);
            Assert.AreEqual(0f, harness.LevelController.TotalAddedExp, TOLERANCE);
            Assert.AreEqual(0, releaseCount);
            Assert.AreEqual(0, callbackCount);

            // Act 3 — Completing remaining duration awards exp, releases, and invokes callback once
            harness.Particle.AdvanceCollection(0.1f);

            // Assert
            Assert.AreEqual(0f, harness.Particle.transform.localScale.x, TOLERANCE);
            Assert.AreEqual(75f, harness.LevelController.TotalAddedExp, TOLERANCE);
            Assert.AreEqual(1, harness.LevelController.AddExpCallCount);
            Assert.AreEqual(1, releaseCount);
            Assert.AreEqual(1, callbackCount);
            CollectionAssert.AreEqual(new[] { "CanBeReleased", "Callback" }, completionOrder);

            // Extra frames after completion do not re-award exp or re-trigger events
            harness.Particle.AdvanceCollection(0.5f);
            Assert.AreEqual(75f, harness.LevelController.TotalAddedExp, TOLERANCE);
            Assert.AreEqual(1, releaseCount);
        }

        [Test]
        public void AdvanceCollection_InvalidAndNonPositiveDeltaTimes_AreIgnored()
        {
            // Arrange
            ExpParticleHarness harness = CreateDefaultHarness(disappearingDuration: 0.1f);
            harness.Particle.SetSizeAndMaterialBasedOnExpAmount(40f);
            harness.Particle.transform.localScale = Vector3.one;
            harness.Particle.CollectExp();

            // Act
            harness.Particle.AdvanceCollection(0f);
            harness.Particle.AdvanceCollection(-0.2f);
            harness.Particle.AdvanceCollection(float.NaN);
            harness.Particle.AdvanceCollection(float.PositiveInfinity);

            // Assert
            Assert.AreEqual(1f, harness.Particle.transform.localScale.x, TOLERANCE);
            Assert.AreEqual(0f, harness.LevelController.TotalAddedExp, TOLERANCE);
        }

        [Test]
        public void CollectExp_WithoutAudioPlayer_CompletesWithoutThrowing()
        {
            // Arrange
            ExpParticleHarness harness = CreateDefaultHarness(disappearingDuration: 0.1f, includeAudioPlayer: false);
            harness.Particle.SetSizeAndMaterialBasedOnExpAmount(30f);

            // Act
            harness.Particle.CollectExp();
            harness.Particle.AdvanceCollection(0.1f);

            // Assert
            Assert.AreEqual(30f, harness.LevelController.TotalAddedExp, TOLERANCE);
        }

        [Test]
        public void PoolingLifecycle_ReturnToPoolOnReleaseAndOnGet_RestoresStartScaleAndResetsStateForReuse()
        {
            // Arrange
            ExpParticleHarness harness = CreateDefaultHarness(disappearingDuration: 0.1f);
            harness.Particle.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
            InvokeLifecycleMethod(harness.Particle, "Start");

            int releaseCount = 0;
            harness.Particle.OnCanBeReleased += delegate { releaseCount++; };

            harness.Particle.SetSizeAndMaterialBasedOnExpAmount(90f);
            harness.Particle.CollectExp();
            harness.Particle.AdvanceCollection(0.05f);

            // Act 1 — Forced return to pool before completion cancels collection, restores _startScale, and notifies pool
            harness.Particle.ReturnToPool();

            Assert.AreEqual(1, releaseCount);
            Assert.AreEqual(1.4f, harness.Particle.transform.localScale.x, TOLERANCE);
            Assert.AreEqual(0f, harness.LevelController.TotalAddedExp, TOLERANCE);

            // Act 2 — Pool re-issues particle via OnGet and collects with new amount
            harness.Particle.OnGet();
            harness.Particle.SetSizeAndMaterialBasedOnExpAmount(25f);
            harness.Particle.CollectExp();
            harness.Particle.AdvanceCollection(0.1f);

            // Assert
            Assert.AreEqual(25f, harness.LevelController.TotalAddedExp, TOLERANCE);
            Assert.AreEqual(1, harness.LevelController.AddExpCallCount);
            Assert.AreEqual(2, releaseCount);
        }

        [Test]
        public void PoolingLifecycle_OnReleaseBeforeStart_PreservesAwakeScaleInsteadOfZero()
        {
            // Arrange
            ExpParticleHarness harness = CreateDefaultHarness(disappearingDuration: 0.1f);
            harness.Particle.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            InvokeLifecycleMethod(harness.Particle, "Awake");
            harness.Particle.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

            // Act — Release before Start() runs
            harness.Particle.OnRelease();

            // Assert
            Assert.AreEqual(1.5f, harness.Particle.transform.localScale.x, TOLERANCE);
            Assert.AreEqual(1.5f, harness.Particle.transform.localScale.y, TOLERANCE);
            Assert.AreEqual(1.5f, harness.Particle.transform.localScale.z, TOLERANCE);
        }

        [Test]
        public void FixedUpdate_MovesAlongFlowFieldBeforeCollectionAndStopsDuringCollection()
        {
            // Arrange
            ExpParticleHarness harness = CreateDefaultHarness(movementSpeed: 10f, setupValidFlowGrid: true);
            harness.Particle.transform.position = new Vector3(0.5f, 0f, 0.5f);

            // Act 1 — Before collection, FixedUpdate moves particle along flow field direction
            InvokeLifecycleMethod(harness.Particle, "FixedUpdate");
            float distanceMovedBeforeCollection = Vector3.Distance(
                new Vector3(0.5f, 0f, 0.5f),
                harness.Particle.transform.position);
            Vector3 positionAtCollectStart = harness.Particle.transform.position;

            // Act 2 — Once CollectExp starts, FixedUpdate stops moving the particle
            harness.Particle.CollectExp();
            InvokeLifecycleMethod(harness.Particle, "FixedUpdate");

            // Assert
            Assert.Greater(distanceMovedBeforeCollection, 0f);
            Assert.AreEqual(positionAtCollectStart.x, harness.Particle.transform.position.x, TOLERANCE);
            Assert.AreEqual(positionAtCollectStart.z, harness.Particle.transform.position.z, TOLERANCE);
        }

        private ExpParticleHarness CreateDefaultHarness(
            float disappearingDuration = 0.1f,
            float movementSpeed = 8f,
            bool includeAudioPlayer = true,
            bool setupValidFlowGrid = false)
        {
            Material mat = CreateTestMaterial("DefaultMat");
            return CreateHarness(
                materials: new[] { mat },
                thresholds: new[] { (0f, mat, 1f, 1f) },
                disappearingDuration: disappearingDuration,
                movementSpeed: movementSpeed,
                includeAudioPlayer: includeAudioPlayer,
                setupValidFlowGrid: setupValidFlowGrid);
        }

        private ExpParticleHarness CreateHarness(
            Material[] materials,
            (float threshold, Material material, float minScale, float maxScale)[] thresholds,
            float disappearingDuration = 0.1f,
            float movementSpeed = 8f,
            bool includeAudioPlayer = false,
            bool setupValidFlowGrid = false)
        {
            GameObject root = new GameObject("TestExpParticle");
            root.SetActive(false);
            _createdObjects.Add(root);

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            MeshRenderer meshRenderer = visual.AddComponent<MeshRenderer>();

            FakeAudioClipPlayer audioPlayer = null;
            if (includeAudioPlayer)
            {
                GameObject audioGo = new GameObject("Audio");
                audioGo.transform.SetParent(root.transform, false);
                audioPlayer = audioGo.AddComponent<FakeAudioClipPlayer>();
            }

            ExpParticle particle = root.AddComponent<ExpParticle>();
            SetPrivateField(typeof(ExpParticle), particle, "_movementSpeed", movementSpeed);
            SetPrivateField(typeof(ExpParticle), particle, "_disapearingDuration", disappearingDuration);
            SetPrivateField(typeof(ExpParticle), particle, "_visual", visual);
            SetPrivateField(typeof(ExpParticle), particle, "_meshRenderer", meshRenderer);
            SetPrivateField(typeof(ExpParticle), particle, "_particleApearanceByTreshold", BuildAppearanceArray(thresholds));

            FakeLevelController levelController = new FakeLevelController();
            FakePlayerManager playerManager = new FakePlayerManager(levelController);
            FakeGridManager gridManager = new FakeGridManager(setupValidFlowGrid);

            Container container = BuildContainer(delegate(ContainerBuilder builder)
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
                builder.AddSingleton(gridManager, typeof(IGridManager));
            });

            GameObjectInjector.InjectRecursive(root, container);
            InvokeLifecycleMethod(particle, "Awake");
            FlowFieldMovementController movementController = root.GetComponent<FlowFieldMovementController>();
            InvokeLifecycleMethod(movementController, "Awake");

            return new ExpParticleHarness(particle, meshRenderer, levelController, audioPlayer);
        }

        private Material CreateTestMaterial(string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material mat = new Material(shader);
            mat.name = name;
            _createdObjects.Add(mat);
            return mat;
        }

        private Collider CreateColliderOnLayer(string name, int layer)
        {
            GameObject go = new GameObject(name);
            go.layer = layer < 0 ? 0 : layer;
            _createdObjects.Add(go);
            return go.AddComponent<BoxCollider>();
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
            ContainerBuilder builder = new ContainerBuilder().SetName("ExpParticleTestContainer");
            configure?.Invoke(builder);
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private static Array BuildAppearanceArray(
            (float threshold, Material material, float minScale, float maxScale)[] entries)
        {
            Type entryType = typeof(ExpParticle).GetNestedType("ExpParticleApearanceByTreshold", BindingFlags.NonPublic);
            Array array = Array.CreateInstance(entryType, entries.Length);

            FieldInfo thresholdField = entryType.GetField("_treshold", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo materialField = entryType.GetField("_material", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo scaleField = entryType.GetField("_scaleValueRange", BindingFlags.Instance | BindingFlags.NonPublic);

            for (int i = 0; i < entries.Length; i++)
            {
                object boxed = Activator.CreateInstance(entryType);
                thresholdField.SetValue(boxed, entries[i].threshold);
                materialField.SetValue(boxed, entries[i].material);
                scaleField.SetValue(boxed, new FloatValueRange(entries[i].minScale, entries[i].maxScale));
                array.SetValue(boxed, i);
            }

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

        private sealed class ExpParticleHarness
        {
            public ExpParticleHarness(
                ExpParticle particle,
                MeshRenderer meshRenderer,
                FakeLevelController levelController,
                FakeAudioClipPlayer audioPlayer)
            {
                Particle = particle;
                MeshRenderer = meshRenderer;
                LevelController = levelController;
                AudioPlayer = audioPlayer;
            }

            public ExpParticle Particle { get; private set; }
            public MeshRenderer MeshRenderer { get; private set; }
            public FakeLevelController LevelController { get; private set; }
            public FakeAudioClipPlayer AudioPlayer { get; private set; }
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

        private sealed class FakeAudioClipPlayer : MonoBehaviour, IAudioClipPlayer
        {
            public List<string> PlayedClipNames { get; private set; } = new();

            public event EventHandler OnAudioClipFinished;

            public void Play(string name)
            {
                PlayedClipNames.Add(name);
            }

            public void PlayOneShot(string name)
            {
                PlayedClipNames.Add(name);
            }

            public void CompleteAudio()
            {
                OnAudioClipFinished?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class FakeGridManager : IGridManager
        {
            public FakeGridManager(bool setupValidFlowGrid)
            {
                if (!setupValidFlowGrid)
                {
                    return;
                }

                WorldGrid = new Grid(new GridConfiguration(3, 3, 1f));
                GridPlayerChunk = WorldGrid;
                DestinationCell = WorldGrid.Cells[2, 2];
                WorldGrid.Cells[0, 0].BestDirection = GridDirection.East;
                WorldGrid.Cells[0, 0].Cost = 1;
            }

            public Cell DestinationCell { get; private set; }
            public Grid GridPlayerChunk { get; private set; }
            public Grid WorldGrid { get; private set; }
        }
    }
}
