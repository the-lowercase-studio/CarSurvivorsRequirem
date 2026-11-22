using System;
using System.Collections;
using System.Collections.Generic;
using Assets.ScriptableObjects.Player.Skills;
using Assets.Scripts.Audio;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.Enemies;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LayerMasks.Constants;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.Skills;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.UI.Level;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using NavigationGrid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Tests.PlayMode
{
    public class LevelAndExpPlayModeTests
    {
        private const string EXP_PARTICLE_PREFAB_PATH = "Assets/Prefabs/Exp/ExpParticle.prefab";

        private PlayModeTestHarness _harness;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _harness = new PlayModeTestHarness();
            _harness.BeginIsolatedScene("LevelAndExpPlayMode");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_harness != null)
            {
                yield return _harness.CleanupAsync();
            }
        }

        [UnityTest]
        public IEnumerator ExpParticleSpawner_InvokeRepeating_DrainsQueuedSpawnAndSplitsExpParticles()
        {
            ExpParticle expPrefab =
                PlayModeTestHarness.LoadEditorAsset<GameObject>(EXP_PARTICLE_PREFAB_PATH).GetComponent<ExpParticle>();
            Assert.IsNotNull(expPrefab);

            FakeGridManager gridManager = CreateEastwardWalkableGrid();
            LevelController levelController = CreateLevelController(
                AnimationCurve.Linear(1f, 200f, 10f, 2000f),
                out GameObject playerGo);
            FakePlayerManager playerManager = new FakePlayerManager(playerGo, levelController);

            Container container = BuildLevelContainer(playerManager, gridManager);

            GameObject particlesParent = _harness.TrackGameObject("ExpParticlesParent");
            ExpParticleSpawner spawner = CreateExpSpawner(
                container,
                expPrefab,
                particlesParent.transform,
                new float[] { 0f, 50f },
                new float[] { 1f, 4f });

            spawner.Spawn(new Vector3(1f, 0f, 3f), 80f, count: 1);
            Assert.AreEqual(0u, spawner.CurrentlySpawnedObjectsCount);

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => spawner.CurrentlySpawnedObjectsCount == 4u,
                1.5f,
                "Expected ExpParticleSpawner InvokeRepeating to drain queued spawn and split 80 EXP into 4 particles.");

            Assert.AreEqual(4, particlesParent.transform.childCount);
        }

        [UnityTest]
        public IEnumerator ExpParticle_FlowFieldFixedUpdateAndTrigger_HomingShrinksAwardsExpAndReleasesToPool()
        {
            ExpParticle expPrefab =
                PlayModeTestHarness.LoadEditorAsset<GameObject>(EXP_PARTICLE_PREFAB_PATH).GetComponent<ExpParticle>();
            Assert.IsNotNull(expPrefab);

            FakeGridManager gridManager = CreateEastwardWalkableGrid();
            AnimationCurve expCurve = new AnimationCurve(
                new Keyframe(1f, 100f),
                new Keyframe(2f, 200f),
                new Keyframe(10f, 1000f));
            LevelController levelController = CreateLevelController(expCurve, out GameObject playerGo);
            playerGo.layer = LayerMask.NameToLayer(LayerMaskConstants.PLAYER);
            playerGo.transform.position = new Vector3(4.5f, 0f, 3f);
            BoxCollider playerCollider = playerGo.AddComponent<BoxCollider>();
            playerCollider.size = new Vector3(1.5f, 2f, 1.5f);
            Rigidbody playerRb = playerGo.AddComponent<Rigidbody>();
            playerRb.isKinematic = true;
            playerRb.useGravity = false;

            FakePlayerManager playerManager = new FakePlayerManager(playerGo, levelController);
            Container container = BuildLevelContainer(playerManager, gridManager);

            GameObject particlesParent = _harness.TrackGameObject("ExpParticlesParent");
            ExpParticleSpawner spawner = CreateExpSpawner(
                container,
                expPrefab,
                particlesParent.transform,
                new float[] { 0f },
                new float[] { 1f });

            int releasedCount = 0;
            spawner.OnSpawnedEntityReleased += (sender, args) => { releasedCount++; };

            spawner.Spawn(new Vector3(1.5f, 0f, 3f), 60f, count: 1);
            spawner.DrainQueuedSpawns();
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);

            ExpParticle particle = particlesParent.GetComponentInChildren<ExpParticle>(true);
            Assert.IsNotNull(particle);

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => releasedCount == 1 && spawner.CurrentlySpawnedObjectsCount == 0u,
                4f,
                "Expected ExpParticle to follow FlowField FixedUpdate into player trigger, shrink, pay EXP, and release.");

            Assert.IsFalse(particle.gameObject.activeSelf);
            Assert.AreEqual(1, levelController.LevelData.Lvl);
            Assert.AreEqual(60f, levelController.LevelData.Exp, 0.01f);
        }

        [UnityTest]
        public IEnumerator PlayerLevelPresenter_RealCoroutineQueue_AnimatesMultiLevelUpSequentially()
        {
            AnimationCurve expCurve = new AnimationCurve(
                new Keyframe(1f, 50f),
                new Keyframe(2f, 50f),
                new Keyframe(3f, 100f));
            LevelController levelController = CreateLevelController(expCurve, out GameObject playerGo);
            FakePlayerManager playerManager = new FakePlayerManager(playerGo, levelController);

            Container container = _harness.BuildContainer(builder =>
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
            });

            GameObject canvasGo = _harness.TrackGameObject("LevelHudCanvas", false);
            canvasGo.AddComponent<Canvas>();
            GameObject sliderGo = _harness.TrackGameObject("ExpSlider", false);
            sliderGo.transform.SetParent(canvasGo.transform, false);
            Slider expSlider = sliderGo.AddComponent<Slider>();

            GameObject textGo = _harness.TrackGameObject("LevelText", false);
            textGo.transform.SetParent(canvasGo.transform, false);
            TextMeshProUGUI levelText = textGo.AddComponent<TextMeshProUGUI>();

            PlayerLevelPresenter presenter = canvasGo.AddComponent<PlayerLevelPresenter>();
            PlayModeTestHarness.SetPrivateField(typeof(PlayerLevelPresenter), presenter, "_levelText", levelText);
            PlayModeTestHarness.SetPrivateField(typeof(PlayerLevelPresenter), presenter, "_expSlider", expSlider);
            AttributeInjector.Inject(presenter, container);

            List<byte> reachedLevels = new List<byte>();
            presenter.OnExpSliderVisualEndValueReached += (sender, args) =>
            {
                reachedLevels.Add(args.Value.Lvl);
            };

            canvasGo.SetActive(true);
            yield return null;

            Assert.AreEqual("1", levelText.text);
            Assert.AreEqual(50f, expSlider.maxValue, 0.01f);

            levelController.AddExp(120f);
            Assert.AreEqual(3, levelController.LevelData.Lvl);
            Assert.AreEqual(20f, levelController.LevelData.Exp, 0.01f);

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => reachedLevels.Count == 2 && Mathf.Approximately(expSlider.value, 20f),
                5f,
                "Expected PlayerLevelPresenter coroutine queue to complete level 2 and level 3 transitions.");

            CollectionAssert.AreEqual(new byte[] { 2, 3 }, reachedLevels);
            Assert.AreEqual("3", levelText.text);
            Assert.AreEqual(100f, expSlider.maxValue, 0.01f);
        }

        private Container BuildLevelContainer(IPlayerManager playerManager, IGridManager gridManager)
        {
            return _harness.BuildContainer(builder =>
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
                builder.AddSingleton(gridManager, typeof(IGridManager));
            });
        }

        private LevelController CreateLevelController(AnimationCurve expCurve, out GameObject playerGo)
        {
            GameObject go = _harness.TrackGameObject("TestPlayerLevelOwner", false);
            LevelController controller = go.AddComponent<LevelController>();
            PlayModeTestHarness.SetPrivateField(typeof(LevelController), controller, "_expCurve", expCurve);

            Container levelContainer = _harness.BuildContainer(builder =>
            {
                builder.AddSingleton(new FakeRunSessionService(), typeof(IRunSessionService));
            });
            AttributeInjector.Inject(controller, levelContainer);
            go.SetActive(true);
            playerGo = go;
            return controller;
        }

        private ExpParticleSpawner CreateExpSpawner(
            Container container,
            ExpParticle prefab,
            Transform particlesParent,
            float[] thresholds,
            float[] dividers)
        {
            GameObject spawnerGo = _harness.TrackGameObject("ExpParticleSpawner", false);
            ExpParticleSpawner spawner = spawnerGo.AddComponent<ExpParticleSpawner>();

            Type dividerType = typeof(ExpParticleSpawner).GetNestedType(
                "ExpTresholdDevider",
                System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(dividerType);

            Array dividerArray = Array.CreateInstance(dividerType, thresholds.Length);
            for (int i = 0; i < thresholds.Length; i++)
            {
                object item = Activator.CreateInstance(dividerType);
                PlayModeTestHarness.SetPrivateField(dividerType, item, "_treshold", thresholds[i]);
                PlayModeTestHarness.SetPrivateField(dividerType, item, "_divider", dividers[i]);
                dividerArray.SetValue(item, i);
            }

            PlayModeTestHarness.SetPrivateField(typeof(ExpParticleSpawner), spawner, "_expParticlesParent", particlesParent);
            PlayModeTestHarness.SetPrivateField(typeof(ExpParticleSpawner), spawner, "_particlesYOffset", 0f);
            PlayModeTestHarness.SetPrivateField(typeof(ExpParticleSpawner), spawner, "_expParticlePrefab", prefab);
            PlayModeTestHarness.SetPrivateField(typeof(ExpParticleSpawner), spawner, "_expTresholdDeviders", dividerArray);
            PlayModeTestHarness.SetPrivateField(typeof(ExpParticleSpawner), spawner, "_spawningCircleRadius", 0f);
            AttributeInjector.Inject(spawner, container);
            spawnerGo.SetActive(true);
            return spawner;
        }

        private static FakeGridManager CreateEastwardWalkableGrid()
        {
            GridConfiguration config = new GridConfiguration(4, 3, 2f);
            NavigationGrid worldGrid = new NavigationGrid(config);
            for (int x = 0; x < worldGrid.Width; x++)
            {
                for (int y = 0; y < worldGrid.Height; y++)
                {
                    Cell cell = worldGrid.Cells[x, y];
                    cell.Cost = 1;
                    cell.BestCost = (ushort)(worldGrid.Width - x);
                    cell.BestDirection = x < 2 ? GridDirection.East : GridDirection.None;
                }
            }

            Cell destination = worldGrid.Cells[2, 1];
            destination.BestCost = 0;
            destination.BestDirection = GridDirection.None;
            return new FakeGridManager(worldGrid, worldGrid, destination);
        }

        private sealed class FakeGridManager : IGridManager
        {
            public Cell DestinationCell { get; }
            public NavigationGrid GridPlayerChunk { get; }
            public NavigationGrid WorldGrid { get; }

            public FakeGridManager(NavigationGrid worldGrid, NavigationGrid chunkGrid, Cell destinationCell)
            {
                WorldGrid = worldGrid;
                GridPlayerChunk = chunkGrid;
                DestinationCell = destinationCell;
            }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            public FakePlayerManager(GameObject gameObject, ILevelController levelController)
            {
                GameObject = gameObject;
                LevelController = levelController;
            }

            public GameObject GameObject { get; }
            public IHealth Health { get { return null; } }
            public IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public ICarController CarController { get { return null; } }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get; }
            public ISkillsRegistry SkillsRegistry { get { return null; } }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }
        }

        private sealed class FakeExpSpawnerNoOp : IInWorldSpaceSpawner<ExpParticleSpawner, float>
        {
            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public uint CurrentlySpawnedObjectsCount { get { return 0u; } }

            public void Spawn(Vector3 pos, float specificConfig, int count = 1)
            {
            }
        }

        private sealed class FakeRunSessionService : IRunSessionService
        {
            public int CurrentTierIndex { get { return 0; } }
            public int TotalTiersCount { get { return 1; } }
            public bool IsFinalTier { get { return true; } }
            public LoopTierConfig ActiveTierConfig { get { return null; } }
            public GameLoopProgressionConfigSO ProgressionConfig { get { return null; } }
            public bool HasPendingRestorationSnapshot { get { return false; } }
            public uint AccumulatedRunSeconds { get { return 0u; } }
            public bool IsGodModeEnabled { get { return false; } }

            public void CaptureSnapshotAndAdvanceTier(IPlayerManager playerManager, uint elapsedSeconds) { }
            public void CaptureSnapshotAndSetTier(int tierIndex, IPlayerManager playerManager, uint elapsedSeconds) { }
            public void CaptureFinalRunTimer(uint elapsedSeconds) { }
            public bool TryGetRestoredLevel(out byte level, out float exp)
            {
                level = 1;
                exp = 0f;
                return false;
            }
            public bool TryGetRestoredSkills(out IReadOnlyList<SavedSkillSnapshot> skills)
            {
                skills = null;
                return false;
            }
            public void SetGodModeEnabled(bool isEnabled) { }
            public void ResetSession() { }
            public void ResetSessionToTier(int tierIndex) { }
        }
    }
}
