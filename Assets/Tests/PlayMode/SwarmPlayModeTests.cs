using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.PowerUps;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Spawners;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.Spawners.Swarm;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.UI.HUD;
using Assets.Scripts.VFX;
using Assets.Scripts.Waves;
using DG.Tweening;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using NavigationGrid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Tests.PlayMode
{
    public class SwarmPlayModeTests
    {
        private const string STANDING_ZOMBIE_PREFAB_PATH = "Assets/Prefabs/Enemies/Basic/Zombies/Standing/StandingZombieLevel1.prefab";
        private const string DROP_CONFIG_ASSET_PATH = "Assets/ScriptableObjects/Collectibles/DropAnimationConfiguration.asset";

        private PlayModeTestHarness _harness;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _harness = new PlayModeTestHarness();
            _harness.BeginIsolatedScene("SwarmPlayMode");
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
        public IEnumerator SwarmSpawner_UpdateLifecycle_RunsWarningOngoingSpawnsAndRestoresGammaAndWaves()
        {
            LiftGammaGain liftGammaGain = CreatePostProcessVolume(out Volume volume);
            Vector4 originalGamma = new Vector4(1f, 1f, 1f, 0f);
            liftGammaGain.gamma.value = originalGamma;

            SwarmNotificationPresenter presenter = CreateNotificationPresenter(
                volume,
                out GameObject visualGo,
                out TextMeshProUGUI swarmText);

            FakeWaveFreezer waveFreezer = new FakeWaveFreezer();
            FakeTimeFreezeState timeFreezeState = new FakeTimeFreezeState();
            FakeSwarmEnemySpawner swarmEnemySpawner = new FakeSwarmEnemySpawner();
            FakeRunSessionService runSessionService = new FakeRunSessionService();

            Container spawnerContainer = _harness.BuildContainer(builder =>
            {
                builder.AddSingleton(swarmEnemySpawner, typeof(ISwarmEnemySpawner));
                builder.AddSingleton(waveFreezer, typeof(IWaveFreezer));
                builder.AddSingleton(presenter, typeof(ISwarmNotificationPresenter));
                builder.AddSingleton(timeFreezeState, typeof(ITimeFreezeState));
                builder.AddSingleton(runSessionService, typeof(IRunSessionService));
            });

            GameObject spawnerGo = _harness.TrackGameObject("SwarmSpawner", false);
            SwarmSpawner swarmSpawner = spawnerGo.AddComponent<SwarmSpawner>();
            PlayModeTestHarness.SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_minSwarmSize", 2);
            PlayModeTestHarness.SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_maxSwarmSize", 2);
            PlayModeTestHarness.SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_swarmWarningDuration", 0.2f);
            PlayModeTestHarness.SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_swarmDuration", 0.1f);
            PlayModeTestHarness.SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_spawnTickInterval", 0.1f);
            AttributeInjector.Inject(swarmSpawner, spawnerContainer);
            spawnerGo.SetActive(true);

            yield return null;
            PlayModeTestHarness.SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_nextSwarmTime", 0.05f);

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => waveFreezer.IsFrozen && visualGo.activeSelf && swarmText.gameObject.activeSelf,
                1.5f,
                "Expected SwarmSpawner Update to enter Warning, freeze waves, and display warning text.");

            Assert.IsTrue(swarmText.text.Contains("Swarm Incoming"));

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => liftGammaGain.gamma.value.w < -0.005f,
                1.5f,
                "Expected SwarmNotificationPresenter to tween LiftGammaGain gamma.w during swarm warning.");

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => swarmEnemySpawner.TotalSpawnedCount == 2 && !waveFreezer.IsFrozen,
                2.5f,
                "Expected SwarmSpawner Update to spawn swarm enemies and unfreeze waves after swarm completes.");

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => !visualGo.activeSelf && !swarmText.gameObject.activeSelf,
                2f,
                "Expected SwarmNotificationPresenter Hide tween to deactivate warning UI.");
        }

        [UnityTest]
        public IEnumerator SwarmNotificationPresenter_OnDisable_KillsTweensAndRestoresOriginalGammaImmediately()
        {
            LiftGammaGain liftGammaGain = CreatePostProcessVolume(out Volume volume);
            Vector4 originalGamma = new Vector4(1f, 1f, 1f, 0.12f);
            liftGammaGain.gamma.value = originalGamma;

            SwarmNotificationPresenter presenter = CreateNotificationPresenter(
                volume,
                out GameObject visualGo,
                out TextMeshProUGUI swarmText);

            yield return null;

            presenter.ShowIncoming(4);
            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => Math.Abs(liftGammaGain.gamma.value.w - originalGamma.w) > 0.01f,
                1.5f,
                "Expected active gamma tween to shift gamma.w away from originalGamma.");

            presenter.gameObject.SetActive(false);

            Assert.AreEqual(originalGamma, liftGammaGain.gamma.value);
            Assert.IsNull(DOTween.TweensByTarget(swarmText.rectTransform, true));
        }

        [UnityTest]
        public IEnumerator SwarmSpawner_IsSuppressedSetMidWarning_CancelsSwarmUnfreezesAndSpawnsZeroEnemies()
        {
            LiftGammaGain liftGammaGain = CreatePostProcessVolume(out Volume volume);
            Vector4 originalGamma = new Vector4(1f, 1f, 1f, 0f);
            liftGammaGain.gamma.value = originalGamma;

            SwarmNotificationPresenter presenter = CreateNotificationPresenter(
                volume,
                out GameObject visualGo,
                out TextMeshProUGUI swarmText);

            FakeWaveFreezer waveFreezer = new FakeWaveFreezer();
            FakeSwarmEnemySpawner swarmEnemySpawner = new FakeSwarmEnemySpawner();
            Container spawnerContainer = _harness.BuildContainer(builder =>
            {
                builder.AddSingleton(swarmEnemySpawner, typeof(ISwarmEnemySpawner));
                builder.AddSingleton(waveFreezer, typeof(IWaveFreezer));
                builder.AddSingleton(presenter, typeof(ISwarmNotificationPresenter));
                builder.AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState));
                builder.AddSingleton(new FakeRunSessionService(), typeof(IRunSessionService));
            });

            GameObject spawnerGo = _harness.TrackGameObject("SwarmSpawnerSuppressed", false);
            SwarmSpawner swarmSpawner = spawnerGo.AddComponent<SwarmSpawner>();
            PlayModeTestHarness.SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_swarmWarningDuration", 1f);
            PlayModeTestHarness.SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_swarmDuration", 0.2f);
            PlayModeTestHarness.SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_spawnTickInterval", 0.1f);
            AttributeInjector.Inject(swarmSpawner, spawnerContainer);
            spawnerGo.SetActive(true);

            yield return null;
            PlayModeTestHarness.SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_nextSwarmTime", 0.05f);

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => waveFreezer.IsFrozen && visualGo.activeSelf,
                1.5f,
                "Expected SwarmSpawner to enter Warning phase.");

            ((ISwarmFreezer)swarmSpawner).IsSuppressed = true;
            Assert.IsFalse(waveFreezer.IsFrozen);

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => !visualGo.activeSelf && Mathf.Abs(liftGammaGain.gamma.value.w - originalGamma.w) < 0.01f,
                2.5f,
                "Expected cancelled swarm warning to hide banner and restore LiftGammaGain gamma.");

            Assert.AreEqual(0, swarmEnemySpawner.TotalSpawnedCount);
        }

        [UnityTest]
        public IEnumerator EnemiesSpawner_SpawnSpecificEnemyWithSwarmVfx_ActivatesEnemyOnlyAfterVfxFinishes()
        {
            GameObject ground = _harness.TrackGameObject("GroundSupport");
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.That(groundLayer, Is.GreaterThanOrEqualTo(0));
            ground.layer = groundLayer;
            ground.transform.position = new Vector3(3f, -0.5f, 3f);
            BoxCollider groundCollider = ground.AddComponent<BoxCollider>();
            groundCollider.size = new Vector3(30f, 1f, 30f);

            GameObject camGo = _harness.TrackGameObject("TestCamera");
            Camera cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(3f, 20f, 3f);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            FakeGridManager gridManager = CreateSmallWalkableGrid();

            GameObject playerGo = _harness.TrackGameObject("DummyPlayer");
            Enemy enemyPrefab =
                PlayModeTestHarness.LoadEditorAsset<GameObject>(STANDING_ZOMBIE_PREFAB_PATH).GetComponent<Enemy>();
            Assert.IsNotNull(enemyPrefab);

            GameObject vfxPrefabGo = _harness.TrackGameObject("FastSwarmVfxPrefab", false);
            ParticleSystem ps = vfxPrefabGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.25f;
            main.loop = false;
            main.playOnAwake = false;
            VFXPlayer vfxPrefab = vfxPrefabGo.AddComponent<VFXPlayer>();

            GameObject enemiesParent = _harness.TrackGameObject("EnemiesParent");
            GameObject spawnerGo = _harness.TrackGameObject("EnemiesSpawner", false);
            EnemiesSpawner enemiesSpawner = spawnerGo.AddComponent<EnemiesSpawner>();

            EnemySpawnInfo spawnInfo = new EnemySpawnInfo(
                enemyPrefab,
                5,
                new SpawnChanceInfo { SpawnChance = 100f });

            DropAnimationConfiguration dropConfig =
                PlayModeTestHarness.LoadEditorAsset<DropAnimationConfiguration>(DROP_CONFIG_ASSET_PATH);

            Container enemyDependencyContainer = _harness.BuildContainer(builder =>
            {
                builder.AddSingleton(gridManager, typeof(IGridManager));
                builder.AddSingleton(cam);
                builder.AddSingleton(new FakeRunSessionService(), typeof(IRunSessionService));
                builder.AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState));
                builder.AddSingleton(dropConfig);
                builder.AddSingleton(new FakeDropNotifier(), typeof(ICollectibleDropNotifier));
                builder.AddSingleton(new FakeDropSuppressor(), typeof(ICollectibleDropSuppressor));
                builder.AddSingleton(new FakeExpSpawnerNoOp(), typeof(IInWorldSpaceSpawner<ExpParticleSpawner, float>));
                builder.AddSingleton(
                    new FakeDamageNumbersSpawnerNoOp(),
                    typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>));
            });

            PlayModeTestHarness.SetPrivateField(typeof(EnemiesSpawner), enemiesSpawner, "_enemiesParent", enemiesParent.transform);
            PlayModeTestHarness.SetPrivateField(typeof(EnemiesSpawner), enemiesSpawner, "_poolEnemiesInfo", new List<EnemySpawnInfo> { spawnInfo });
            PlayModeTestHarness.SetPrivateField(typeof(EnemiesSpawner), enemiesSpawner, "_swarmSpawnVfxPrefab", vfxPrefab);
            PlayModeTestHarness.SetPrivateField(typeof(EnemiesSpawner), enemiesSpawner, "_maxEnemiesPerCell", 5);
            AttributeInjector.Inject(enemiesSpawner, enemyDependencyContainer);
            spawnerGo.SetActive(true);

            yield return null;
            Assert.AreEqual(0u, enemiesSpawner.CurrentlySpawnedObjectsCount);

            enemiesSpawner.SpawnSpecificEnemy(enemiesSpawner.EnemyConfigs[0], 1);
            Assert.AreEqual(1u, enemiesSpawner.CurrentlySpawnedObjectsCount,
                "Enemy must spawn immediately in an inactive materializing state while SwarmSpawnVfx plays.");

            List<Enemy> activeEnemiesBuffer = new List<Enemy>();
            enemiesSpawner.CopyActiveEnemies(activeEnemiesBuffer);
            Assert.AreEqual(1, activeEnemiesBuffer.Count);
            Enemy spawnedEnemy = activeEnemiesBuffer[0];
            Collider enemyCollider = spawnedEnemy.GetComponent<Collider>();
            Assert.IsNotNull(enemyCollider);
            Assert.IsTrue(spawnedEnemy.IsMaterializing);
            Assert.IsFalse(enemyCollider.enabled);

            Vector3 spawnPosition = spawnedEnemy.transform.position;
            float initialHealth = spawnedEnemy.Health.CurrentHealth;
            spawnedEnemy.TakeDamage(50f);
            Assert.AreEqual(initialHealth, spawnedEnemy.Health.CurrentHealth,
                "Materializing enemy must ignore damage before activation.");

            yield return new WaitForFixedUpdate();
            Assert.IsTrue(spawnedEnemy.IsMaterializing);
            Assert.IsFalse(enemyCollider.enabled);
            Vector2 horizontalDrift = new Vector2(
                spawnedEnemy.transform.position.x - spawnPosition.x,
                spawnedEnemy.transform.position.z - spawnPosition.z);
            Assert.Less(horizontalDrift.magnitude, 0.01f,
                "Materializing enemy must stand stationary before VFX finishes.");

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => !spawnedEnemy.IsMaterializing,
                2.5f,
                "Expected HandleSwarmVfxFinished to activate materializing enemy once SwarmSpawnVfx finished.");

            Assert.IsFalse(spawnedEnemy.IsMaterializing);
            Assert.IsTrue(enemyCollider.enabled);
            Assert.AreEqual(1u, enemiesSpawner.CurrentlySpawnedObjectsCount);
        }

        private LiftGammaGain CreatePostProcessVolume(out Volume volume)
        {
            VolumeProfile profile = _harness.TrackScriptableObject<VolumeProfile>();
            LiftGammaGain lgg = profile.Add<LiftGammaGain>(true);
            GameObject volumeGo = _harness.TrackGameObject("PostProcessVolume");
            volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;
            return lgg;
        }

        private SwarmNotificationPresenter CreateNotificationPresenter(
            Volume volume,
            out GameObject visualGo,
            out TextMeshProUGUI swarmText)
        {
            Container container = _harness.BuildContainer(builder =>
            {
                builder.AddSingleton(volume);
            });

            GameObject presenterGo = _harness.TrackGameObject("SwarmNotificationPresenter", false);
            visualGo = _harness.TrackGameObject("Visual", false);
            visualGo.transform.SetParent(presenterGo.transform, false);
            visualGo.SetActive(true);

            GameObject textGo = _harness.TrackGameObject("SwarmText", false);
            textGo.transform.SetParent(visualGo.transform, false);
            swarmText = textGo.AddComponent<TextMeshProUGUI>();
            textGo.SetActive(true);

            SwarmNotificationPresenter presenter = presenterGo.AddComponent<SwarmNotificationPresenter>();
            PlayModeTestHarness.SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_visual", visualGo);
            PlayModeTestHarness.SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_swarmText", swarmText);
            PlayModeTestHarness.SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_growDuration", 0.05f);
            PlayModeTestHarness.SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_punchDuration", 0.08f);
            PlayModeTestHarness.SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_exitDuration", 0.05f);
            PlayModeTestHarness.SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_gammaEnterDuration", 0.15f);
            PlayModeTestHarness.SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_gammaExitDuration", 0.12f);
            AttributeInjector.Inject(presenter, container);
            presenterGo.SetActive(true);
            return presenter;
        }

        private static FakeGridManager CreateSmallWalkableGrid()
        {
            GridConfiguration config = new GridConfiguration(3, 3, 2f);
            NavigationGrid grid = new NavigationGrid(config);
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    grid.Cells[x, y].Cost = 1;
                }
            }

            return new FakeGridManager(grid, grid, grid.Cells[1, 1]);
        }

        private sealed class FakeGridManager : IGridManager
        {
            public FakeGridManager(NavigationGrid worldGrid, NavigationGrid chunkGrid, Cell destination)
            {
                WorldGrid = worldGrid;
                GridPlayerChunk = chunkGrid;
                DestinationCell = destination;
            }

            public Cell DestinationCell { get; }
            public NavigationGrid GridPlayerChunk { get; }
            public NavigationGrid WorldGrid { get; }
        }

        private sealed class FakeWaveFreezer : IWaveFreezer
        {
            public bool IsFrozen { get; set; }
        }

        private sealed class FakeTimeFreezeState : ITimeFreezeState
        {
            public bool IsFrozen { get { return false; } }
            public float UnfrozenTime { get { return Time.time; } }
            public event Action<bool> OnFrozenChanged
            {
                add { }
                remove { }
            }
        }

        private sealed class FakeSwarmEnemySpawner : ISwarmEnemySpawner
        {
            private readonly List<EnemySpawnInfo> _configs;

            public int TotalSpawnedCount { get; private set; }
            public IReadOnlyList<EnemySpawnInfo> EnemyConfigs { get { return _configs; } }

            public FakeSwarmEnemySpawner()
            {
                _configs = new List<EnemySpawnInfo>
                {
                    new EnemySpawnInfo(null, 50, new SpawnChanceInfo { SpawnChance = 100f })
                };
            }

            public void SpawnSpecificEnemy(EnemySpawnInfo enemyInfo, int count = 1)
            {
                TotalSpawnedCount += count;
            }

            public bool TrySpawnEnemyAt(EnemySpawnInfo enemyInfo, Vector3 position, out Enemy enemy)
            {
                enemy = null;
                return false;
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

        private sealed class FakeDropNotifier : ICollectibleDropNotifier
        {
            public event EventHandler OnSkillUpgradeCollectibleCollected
            {
                add { }
                remove { }
            }

            public event Action<PowerUpType> OnPowerUpCollected
            {
                add { }
                remove { }
            }

            public void SpawnCollectible(GameObject prefab, Vector3 spawnPosition, Vector3 targetPosition) { }
        }

        private sealed class FakeDropSuppressor : ICollectibleDropSuppressor
        {
            public bool AreDropsSuppressed { get { return true; } }
            public void BeginDropSuppression() { }
            public void EndDropSuppression() { }
        }

        private sealed class FakeExpSpawnerNoOp : IInWorldSpaceSpawner<ExpParticleSpawner, float>
        {
            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public uint CurrentlySpawnedObjectsCount { get { return 0u; } }

            public void Spawn(Vector3 pos, float specificConfig, int count = 1) { }
        }

        private sealed class FakeDamageNumbersSpawnerNoOp : IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>
        {
            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public uint CurrentlySpawnedObjectsCount { get { return 0u; } }

            public void Spawn(Vector3 pos, DamageNubmersSpawnerConfig specificConfig, int count = 1) { }
        }
    }
}
