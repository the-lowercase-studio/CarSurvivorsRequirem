using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Enemies.Bosses;
using Assets.Scripts.Enemies.Bosses.Towers.Arena;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.Player;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Spawners;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.Spawners.GridSpace;
using Assets.Scripts.Spawners.Swarm;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.UI.HUD;
using Assets.Scripts.VFX;
using Assets.Scripts.Waves;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEditor;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class SwarmSystemIntegrationTests
    {
        private const string PROGRESSION_CONFIG_PATH = "Assets/ScriptableObjects/GameFlow/GameLoopProgressionConfig.asset";
        private const float TOLERANCE = 0.0001f;
        private const int DEFAULT_RANDOM_SEED = 424242;
        private static readonly Vector3 TEST_ORIGIN = new Vector3(5000f, 500f, 5000f);

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();
        private readonly List<EnemiesSpawner> _createdEnemiesSpawners = new();
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
            for (int i = 0; i < _createdEnemiesSpawners.Count; i++)
            {
                EnemiesSpawner spawner = _createdEnemiesSpawners[i];
                if (spawner == null)
                {
                    continue;
                }

                FieldInfo stagingField = typeof(EnemiesSpawner).GetField("_injectionStagingParent", BindingFlags.Instance | BindingFlags.NonPublic);
                Transform stagingParent = stagingField != null ? stagingField.GetValue(spawner) as Transform : null;
                if (stagingParent != null)
                {
                    UnityEngine.Object.DestroyImmediate(stagingParent.gameObject);
                    stagingField.SetValue(spawner, null);
                }
            }

            _createdEnemiesSpawners.Clear();

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
        public void EnemiesSpawner_InitializeConfigs_PrefersTierConfigsOverSceneDefaultsAndClonesEntries()
        {
            // Arrange
            EnemySpawnInfo sceneDefault = CreateEnemySpawnInfo("SceneDefaultEnemy", 15, 100f);
            EnemySpawnInfo tierEntry = CreateEnemySpawnInfo("TierSwarmEnemy", 25, 60f);
            EnemySpawnInfo invalidNullPrefabEntry = new EnemySpawnInfo(null, 10, new SpawnChanceInfo { SpawnChance = 40f });

            GameLoopProgressionConfigSO progression = CreateProgressionWithTierEntries(
                new[] { tierEntry, invalidNullPrefabEntry },
                1f,
                1f);
            RunSessionService runSession = new RunSessionService(progression);

            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { sceneDefault },
                runSessionService: runSession,
                gridManager: CreateGridHarness(out _, out _),
                camera: CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f)),
                vfxPrefab: null,
                maxEnemiesPerCell: 2,
                callStart: false);

            // Assert — Cloned tier entry used and invalid null-prefab entry skipped
            Assert.AreEqual(1, spawner.EnemyConfigs.Count);
            Assert.AreNotSame(tierEntry, spawner.EnemyConfigs[0]);
            Assert.AreSame(tierEntry.EnemyPrefab, spawner.EnemyConfigs[0].EnemyPrefab);
            Assert.AreEqual(25, spawner.EnemyConfigs[0].MaxAmount);
        }

        [Test]
        public void EnemiesSpawner_InitializeConfigs_FallsBackToSceneDefaultsWhenTierListEmpty()
        {
            // Arrange
            EnemySpawnInfo sceneDefault = CreateEnemySpawnInfo("FallbackSceneEnemy", 12, 100f);
            GameLoopProgressionConfigSO progression = CreateProgressionWithTierEntries(
                Array.Empty<EnemySpawnInfo>(),
                1f,
                1f);
            RunSessionService runSession = new RunSessionService(progression);

            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { sceneDefault },
                runSessionService: runSession,
                gridManager: CreateGridHarness(out _, out _),
                camera: CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f)),
                vfxPrefab: null,
                maxEnemiesPerCell: 2,
                callStart: false);

            // Assert
            Assert.AreEqual(1, spawner.EnemyConfigs.Count);
            Assert.AreNotSame(sceneDefault, spawner.EnemyConfigs[0]);
            Assert.AreSame(sceneDefault.EnemyPrefab, spawner.EnemyConfigs[0].EnemyPrefab);
        }

        [Test]
        public void EnemiesSpawner_SpawnSpecificEnemy_WithoutVfx_SpawnsOnlyOnWalkableSupportedCellsAndResetsMovement()
        {
            // Arrange — Grid at TEST_ORIGIN:
            //   (0,0) & (1,0) at z=5001 are walkable and supported by floor collider at y=500.0
            //   (0,1) unwalkable (Cost = byte.MaxValue)
            //   (1,1) at (5003, 500, 5003) outside floor collider (unsupported)
            FakeGridManager gridManager = CreateGridHarness(out Cell[] supportedCells, out _);
            CreateWalkableFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 0f), new Vector3(10f, 1f, 4f));
            Camera camera = CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f));

            EnemySpawnInfo info = CreateEnemySpawnInfo("SwarmUnit", 10, 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { info },
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: gridManager,
                camera: camera,
                vfxPrefab: null,
                maxEnemiesPerCell: 2,
                callStart: false);

            EnemySpawnInfo runtimeInfo = spawner.EnemyConfigs[0];
            List<Enemy> spawnedBuffer = new List<Enemy>();

            // Act
            spawner.SpawnSpecificEnemy(runtimeInfo, 2);
            int activeCount = spawner.CopyActiveEnemies(spawnedBuffer);

            // Assert
            Assert.AreEqual(2u, spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(2, activeCount);
            AssertSpawnedOnExpectedCellsAndReset(spawnedBuffer, supportedCells, expectedY: 500.02f);
        }

        [Test]
        public void EnemiesSpawner_SpawnSpecificEnemy_RespectsMaxEnemiesPerCellOccupancyLimit()
        {
            // Arrange — Only cell (0,0) is hidden & walkable; 1 existing enemy collider leaves 1 slot for maxEnemiesPerCell=2
            FakeGridManager gridManager = CreateGridHarness(out Cell[] hiddenCells, out Grid chunk);
            chunk.Cells[1, 0].Cost = byte.MaxValue;
            CreateWalkableFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 0f), new Vector3(10f, 1f, 4f));
            CreateExistingEnemyCollider(hiddenCells[0].WorldPos);
            Camera camera = CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f));

            EnemySpawnInfo info = CreateEnemySpawnInfo("OccupancyEnemy", 10, 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { info },
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: gridManager,
                camera: camera,
                vfxPrefab: null,
                maxEnemiesPerCell: 2,
                callStart: false);

            // Act — Request 3 enemies, but only 1 free slot exists on the single valid cell
            spawner.SpawnSpecificEnemy(spawner.EnemyConfigs[0], 3);

            // Assert
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);
        }

        [Test]
        public void EnemiesSpawner_SpawnSpecificEnemy_UnregisteredConfig_ReturnsWithoutSpawning()
        {
            // Arrange
            FakeGridManager gridManager = CreateGridHarness(out _, out _);
            CreateWalkableFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 0f), new Vector3(10f, 1f, 4f));
            Camera camera = CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f));

            EnemySpawnInfo registered = CreateEnemySpawnInfo("RegisteredEnemy", 10, 100f);
            EnemySpawnInfo unregistered = CreateEnemySpawnInfo("UnregisteredEnemy", 10, 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { registered },
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: gridManager,
                camera: camera,
                vfxPrefab: null,
                maxEnemiesPerCell: 2,
                callStart: false);

            // Act
            spawner.SpawnSpecificEnemy(unregistered, 4);

            // Assert
            Assert.AreEqual(0u, spawner.CurrentlySpawnedObjectsCount);
        }

        [Test]
        public void EnemiesSpawner_SpawnSpecificEnemy_WithVfx_SpawnsMaterializingEnemyImmediatelyAndActivatesOnVfxFinish()
        {
            // Arrange
            FakeGridManager gridManager = CreateGridHarness(out _, out Grid chunk);
            chunk.Cells[1, 0].Cost = byte.MaxValue;
            CreateWalkableFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 0f), new Vector3(10f, 1f, 4f));
            Camera camera = CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f));
            VFXPlayer vfxPrefab = CreateTrackedParticleVfxPrefab();

            EnemySpawnInfo info = CreateEnemySpawnInfo("VfxSwarmEnemy", 10, 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { info },
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: gridManager,
                camera: camera,
                vfxPrefab: vfxPrefab,
                maxEnemiesPerCell: 2,
                callStart: false);

            // Act — Trigger spawn with VFX: enemy spawns immediately in inactive materializing state
            spawner.SpawnSpecificEnemy(spawner.EnemyConfigs[0], 1);
            VFXPlayer spawnedVfx = FindLatestSpawnedVfxInstance(vfxPrefab);

            Assert.IsNotNull(spawnedVfx);
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);

            List<Enemy> spawnedBuffer = new List<Enemy>();
            spawner.CopyActiveEnemies(spawnedBuffer);
            Assert.AreEqual(1, spawnedBuffer.Count);
            Enemy spawnedEnemy = spawnedBuffer[0];
            Collider enemyCollider = spawnedEnemy.GetComponent<Collider>();
            Assert.IsTrue(spawnedEnemy.IsMaterializing);
            Assert.IsFalse(enemyCollider.enabled);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, spawnedEnemy.transform.eulerAngles.x), TOLERANCE);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, spawnedEnemy.transform.eulerAngles.z), TOLERANCE);

            CompleteVfxPlaybackInEditMode(spawnedVfx);

            // Assert
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);
            Assert.IsFalse(spawnedEnemy.IsMaterializing);
            Assert.IsTrue(enemyCollider.enabled);
        }

        [Test]
        public void EnemiesSpawner_SpawnSpecificEnemy_WithVfx_SpawnsWhenCellBecomesVisible()
        {
            // Arrange
            FakeGridManager gridManager = CreateGridHarness(out Cell[] hiddenCells, out Grid chunk);
            chunk.Cells[1, 0].Cost = byte.MaxValue;
            CreateWalkableFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 0f), new Vector3(10f, 1f, 4f));
            Camera camera = CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f));
            VFXPlayer vfxPrefab = CreateTrackedParticleVfxPrefab();

            EnemySpawnInfo info = CreateEnemySpawnInfo("VisibleVfxEnemy", 10, 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { info },
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: gridManager,
                camera: camera,
                vfxPrefab: vfxPrefab,
                maxEnemiesPerCell: 2,
                callStart: false);

            spawner.SpawnSpecificEnemy(spawner.EnemyConfigs[0], 1);
            VFXPlayer spawnedVfx = FindLatestSpawnedVfxInstance(vfxPrefab);
            Assert.IsNotNull(spawnedVfx);
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);

            // Rotate camera directly onto cell (0,0) so the indicator is visible on screen when VFX completes
            camera.transform.position = hiddenCells[0].WorldPos + new Vector3(0f, 0.5f, -1.5f);
            camera.transform.LookAt(hiddenCells[0].WorldPos);

            // Act
            CompleteVfxPlaybackInEditMode(spawnedVfx);

            // Assert
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);
            List<Enemy> spawnedBuffer = new List<Enemy>();
            spawner.CopyActiveEnemies(spawnedBuffer);
            Assert.IsFalse(spawnedBuffer[0].IsMaterializing);
            Assert.IsTrue(spawnedBuffer[0].GetComponent<Collider>().enabled);
            AssertSpawnedOnExpectedCellsAndReset(spawnedBuffer, new[] { hiddenCells[0] }, expectedY: 500.02f, expectedResetCount: 2);
        }

        [Test]
        public void EnemiesSpawner_SpawnSpecificEnemy_WithVfx_PlacesIndicatorAndSpawnsInsideCameraView()
        {
            // Arrange — Only cell (1,1) is walkable & supported, and it is directly inside the camera frustum
            FakeGridManager gridManager = CreateGridHarness(out _, out Grid chunk);
            chunk.Cells[0, 0].Cost = byte.MaxValue;
            chunk.Cells[1, 0].Cost = byte.MaxValue;
            Cell visibleCell = chunk.Cells[1, 1];
            CreateWalkableFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 2f), new Vector3(10f, 1f, 10f));
            Camera camera = CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f));
            Assert.IsTrue(CellCameraVisibilityChecker.IsCellVisibleFromCamera(visibleCell.WorldPos, camera));
            VFXPlayer vfxPrefab = CreateTrackedParticleVfxPrefab();

            EnemySpawnInfo info = CreateEnemySpawnInfo("VisibleInitialVfxEnemy", 10, 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { info },
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: gridManager,
                camera: camera,
                vfxPrefab: vfxPrefab,
                maxEnemiesPerCell: 2,
                callStart: false);

            // Act — Indicator is placed on the visible cell and materializing enemy is immediately positioned there
            spawner.SpawnSpecificEnemy(spawner.EnemyConfigs[0], 1);
            VFXPlayer spawnedVfx = FindLatestSpawnedVfxInstance(vfxPrefab);
            Assert.IsNotNull(spawnedVfx);
            Assert.AreEqual(visibleCell.WorldPos.x, spawnedVfx.transform.position.x, TOLERANCE);
            Assert.AreEqual(visibleCell.WorldPos.z, spawnedVfx.transform.position.z, TOLERANCE);
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);

            List<Enemy> spawnedBuffer = new List<Enemy>();
            spawner.CopyActiveEnemies(spawnedBuffer);
            Assert.IsTrue(spawnedBuffer[0].IsMaterializing);
            Assert.IsFalse(spawnedBuffer[0].GetComponent<Collider>().enabled);

            CompleteVfxPlaybackInEditMode(spawnedVfx);

            // Assert
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);
            spawner.CopyActiveEnemies(spawnedBuffer);
            Assert.IsFalse(spawnedBuffer[0].IsMaterializing);
            Assert.IsTrue(spawnedBuffer[0].GetComponent<Collider>().enabled);
            AssertSpawnedOnExpectedCellsAndReset(spawnedBuffer, new[] { visibleCell }, expectedY: 500.02f, expectedResetCount: 2);
        }

        [Test]
        public void EnemiesSpawner_SpawnSpecificEnemy_WithVfx_SpawnsWhenPlayerChunkShiftsBeforeVfxFinishes()
        {
            // Arrange
            FakeGridManager gridManager = CreateGridHarness(out Cell[] hiddenCells, out Grid chunk);
            chunk.Cells[1, 0].Cost = byte.MaxValue;
            CreateWalkableFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 0f), new Vector3(10f, 1f, 4f));
            Camera camera = CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f));
            VFXPlayer vfxPrefab = CreateTrackedParticleVfxPrefab();

            EnemySpawnInfo info = CreateEnemySpawnInfo("ChunkShiftVfxEnemy", 10, 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { info },
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: gridManager,
                camera: camera,
                vfxPrefab: vfxPrefab,
                maxEnemiesPerCell: 2,
                callStart: false);

            spawner.SpawnSpecificEnemy(spawner.EnemyConfigs[0], 1);
            VFXPlayer spawnedVfx = FindLatestSpawnedVfxInstance(vfxPrefab);
            Assert.IsNotNull(spawnedVfx);
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);

            // Simulate GridPlayerChunk shifting away from the indicator cell while the car moves
            chunk.Cells[0, 0] = null;

            // Act
            CompleteVfxPlaybackInEditMode(spawnedVfx);

            // Assert
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);
            List<Enemy> spawnedBuffer = new List<Enemy>();
            spawner.CopyActiveEnemies(spawnedBuffer);
            Assert.IsFalse(spawnedBuffer[0].IsMaterializing);
            Assert.IsTrue(spawnedBuffer[0].GetComponent<Collider>().enabled);
            AssertSpawnedOnExpectedCellsAndReset(spawnedBuffer, new[] { hiddenCells[0] }, expectedY: 500.02f, expectedResetCount: 2);
        }

        [Test]
        public void EnemiesSpawner_SpawnSpecificEnemy_WithVfx_CancelsSpawnWhenGroundSupportLostBeforeVfxFinishes()
        {
            // Arrange
            FakeGridManager gridManager = CreateGridHarness(out _, out Grid chunk);
            chunk.Cells[1, 0].Cost = byte.MaxValue;
            GameObject floor = CreateWalkableFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 0f), new Vector3(10f, 1f, 4f));
            Camera camera = CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f));
            VFXPlayer vfxPrefab = CreateTrackedParticleVfxPrefab();

            EnemySpawnInfo info = CreateEnemySpawnInfo("UnsupportedCancelVfxEnemy", 10, 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { info },
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: gridManager,
                camera: camera,
                vfxPrefab: vfxPrefab,
                maxEnemiesPerCell: 2,
                callStart: false);

            spawner.SpawnSpecificEnemy(spawner.EnemyConfigs[0], 1);
            VFXPlayer spawnedVfx = FindLatestSpawnedVfxInstance(vfxPrefab);
            Assert.IsNotNull(spawnedVfx);
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);

            List<Enemy> spawnedBuffer = new List<Enemy>();
            spawner.CopyActiveEnemies(spawnedBuffer);
            Enemy spawnedEnemy = spawnedBuffer[0];
            Assert.IsTrue(spawnedEnemy.IsMaterializing);

            UnityEngine.Object.DestroyImmediate(floor);
            Physics.SyncTransforms();

            // Act
            CompleteVfxPlaybackInEditMode(spawnedVfx);

            // Assert
            Assert.AreEqual(0u, spawner.CurrentlySpawnedObjectsCount);
            Assert.IsFalse(spawnedEnemy.IsMaterializing);
            Assert.IsFalse(spawnedEnemy.gameObject.activeSelf);
        }

        [Test]
        public void EnemiesSpawner_SpawnSpecificEnemy_WithVfx_CancelsSpawnWhenSpawnerDisabled()
        {
            // Arrange
            FakeGridManager gridManager = CreateGridHarness(out _, out Grid chunk);
            chunk.Cells[1, 0].Cost = byte.MaxValue;
            CreateWalkableFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 0f), new Vector3(10f, 1f, 4f));
            Camera camera = CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f));
            VFXPlayer vfxPrefab = CreateTrackedParticleVfxPrefab();

            EnemySpawnInfo info = CreateEnemySpawnInfo("DisableCancelVfxEnemy", 10, 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { info },
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: gridManager,
                camera: camera,
                vfxPrefab: vfxPrefab,
                maxEnemiesPerCell: 2,
                callStart: false);

            spawner.SpawnSpecificEnemy(spawner.EnemyConfigs[0], 1);
            VFXPlayer spawnedVfx = FindLatestSpawnedVfxInstance(vfxPrefab);
            Assert.IsNotNull(spawnedVfx);
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);

            List<Enemy> spawnedBuffer = new List<Enemy>();
            spawner.CopyActiveEnemies(spawnedBuffer);
            Enemy spawnedEnemy = spawnedBuffer[0];
            Assert.IsTrue(spawnedEnemy.IsMaterializing);

            // Act — Spawner disables before VFX finishes
            InvokeLifecycleMethod(spawner, "OnDisable");
            Assert.AreEqual(0u, spawner.CurrentlySpawnedObjectsCount);
            Assert.IsFalse(spawnedEnemy.IsMaterializing);
            Assert.IsFalse(spawnedEnemy.gameObject.activeSelf);

            CompleteVfxPlaybackInEditMode(spawnedVfx);

            // Assert
            Assert.AreEqual(0u, spawner.CurrentlySpawnedObjectsCount);
        }

        [Test]
        public void EnemiesSpawner_SpawnSpecificEnemy_WithVfx_RecycledEnemyIgnoresStaleVfxCallback()
        {
            // Arrange
            FakeGridManager gridManager = CreateGridHarness(out _, out Grid chunk);
            chunk.Cells[1, 0].Cost = byte.MaxValue;
            CreateWalkableFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 0f), new Vector3(10f, 1f, 4f));
            Camera camera = CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f));
            VFXPlayer vfxPrefab = CreateTrackedParticleVfxPrefab();

            EnemySpawnInfo info = CreateEnemySpawnInfo("RecycledTokenVfxEnemy", 1, 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { info },
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: gridManager,
                camera: camera,
                vfxPrefab: vfxPrefab,
                maxEnemiesPerCell: 2,
                callStart: false);

            spawner.SpawnSpecificEnemy(spawner.EnemyConfigs[0], 1);
            VFXPlayer staleVfx = FindLatestSpawnedVfxInstance(vfxPrefab);
            Assert.IsNotNull(staleVfx);

            List<Enemy> spawnedBuffer = new List<Enemy>();
            spawner.CopyActiveEnemies(spawnedBuffer);
            Enemy enemy = spawnedBuffer[0];
            enemy.ReturnToPool();

            Assert.IsTrue(spawner.TrySpawnEnemyAt(spawner.EnemyConfigs[0], TEST_ORIGIN, out Enemy respawned));
            Assert.AreSame(enemy, respawned);
            Assert.IsFalse(respawned.IsMaterializing);

            // Act — Stale VFX completes for the previous checkout token
            CompleteVfxPlaybackInEditMode(staleVfx);

            // Assert
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);
            Assert.IsFalse(respawned.IsMaterializing);
            Assert.IsTrue(respawned.gameObject.activeSelf);
        }

        [Test]
        public void EnemiesSpawner_TrySpawnEnemyAt_ValidNullAndUnregisteredConfigs_BehavesDeterministically()
        {
            // Arrange
            EnemySpawnInfo registered = CreateEnemySpawnInfo("DirectSpawnEnemy", 10, 100f);
            EnemySpawnInfo unregistered = CreateEnemySpawnInfo("OtherEnemy", 10, 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { registered },
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: CreateGridHarness(out _, out _),
                camera: CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f)),
                vfxPrefab: null,
                maxEnemiesPerCell: 2,
                callStart: false);

            EnemySpawnInfo runtimeInfo = spawner.EnemyConfigs[0];
            Vector3 targetPosition = new Vector3(12f, 3f, -7f);

            // Act & Assert — Null and unregistered return false; null SpawnSpecificEnemy is a safe no-op
            Assert.DoesNotThrow(delegate { spawner.SpawnSpecificEnemy(null, 1); });
            Assert.AreEqual(0u, spawner.CurrentlySpawnedObjectsCount);

            Assert.IsFalse(spawner.TrySpawnEnemyAt(null, targetPosition, out Enemy nullResult));
            Assert.IsNull(nullResult);

            Assert.IsFalse(spawner.TrySpawnEnemyAt(unregistered, targetPosition, out Enemy unregisteredResult));
            Assert.IsNull(unregisteredResult);
            Assert.AreEqual(0u, spawner.CurrentlySpawnedObjectsCount);

            // Act & Assert — Registered config spawns at exact position and resets movement
            Assert.IsTrue(spawner.TrySpawnEnemyAt(runtimeInfo, targetPosition, out Enemy spawned));
            Assert.IsNotNull(spawned);
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(targetPosition.x, spawned.transform.position.x, TOLERANCE);
            Assert.AreEqual(targetPosition.y, spawned.transform.position.y, TOLERANCE);
            Assert.AreEqual(targetPosition.z, spawned.transform.position.z, TOLERANCE);
            Assert.AreEqual(1, spawned.GetComponent<FakeMovementController>().ResetCount);
        }

        [Test]
        public void EnemiesSpawner_SpawnSpecificEnemy_PooledEnemyReturnAndRespawn_ReusesInstanceAndUpdatesCount()
        {
            // Arrange
            FakeGridManager gridManager = CreateGridHarness(out _, out Grid chunk);
            chunk.Cells[1, 0].Cost = byte.MaxValue;
            CreateWalkableFloorCollider(TEST_ORIGIN + new Vector3(2f, -0.5f, 0f), new Vector3(10f, 1f, 4f));
            Camera camera = CreateTestCamera(TEST_ORIGIN + new Vector3(3f, 0.5f, 1.5f));

            EnemySpawnInfo info = CreateEnemySpawnInfo("PooledSwarmEnemy", 2, 100f);
            EnemiesSpawner spawner = CreateEnemiesSpawner(
                sceneDefaults: new[] { info },
                runSessionService: new FakeNullTierRunSessionService(),
                gridManager: gridManager,
                camera: camera,
                vfxPrefab: null,
                maxEnemiesPerCell: 2,
                callStart: false);

            int releasedCount = 0;
            spawner.OnSpawnedEntityReleased += delegate { releasedCount++; };
            List<Enemy> buffer = new List<Enemy>();

            // Act — Spawn once, return to pool, then spawn again
            spawner.SpawnSpecificEnemy(spawner.EnemyConfigs[0], 1);
            spawner.CopyActiveEnemies(buffer);
            Enemy firstInstance = buffer[0];

            firstInstance.ReturnToPool();
            Assert.AreEqual(0u, spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(1, releasedCount);
            Assert.IsFalse(firstInstance.gameObject.activeSelf);

            spawner.SpawnSpecificEnemy(spawner.EnemyConfigs[0], 1);
            spawner.CopyActiveEnemies(buffer);

            // Assert — Same pooled Enemy instance reused cleanly
            Assert.AreEqual(1u, spawner.CurrentlySpawnedObjectsCount);
            Assert.AreEqual(1, buffer.Count);
            Assert.AreSame(firstInstance, buffer[0]);
            Assert.IsTrue(buffer[0].gameObject.activeSelf);
        }

        [Test]
        public void BossEncounterService_And_SwarmSpawner_SuppressesAndRestoresSwarmAcrossEngageDefeatAndDisable()
        {
            // Arrange
            BossSwarmIntegrationHarness harness = CreateBossSwarmIntegrationHarness();
            FakeBossHUDSource hudSource = CreateBossHudSource();

            // Act & Assert — Tower engage suppresses SwarmSpawner and freezes its countdown
            harness.BossService.NotifyEncounterEngaged(hudSource, "Test Tower");
            Assert.IsTrue(harness.SwarmSpawner.IsSuppressed);

            harness.SwarmSpawner.AdvanceSwarm(20f);
            Assert.AreEqual(0, harness.Presenter.IncomingSeconds.Count);

            // Act & Assert — Tower defeat restores suppression and allows countdown to trigger
            FakeEncounterArena defeatedTower = new FakeEncounterArena();
            harness.BossService.RegisterTower(defeatedTower);
            harness.BossService.NotifyTowerDefeated(defeatedTower);
            Assert.IsFalse(harness.SwarmSpawner.IsSuppressed);

            harness.SwarmSpawner.AdvanceSwarm(5f);
            Assert.AreEqual(1, harness.Presenter.IncomingSeconds.Count);

            // Act & Assert — BossEncounterService.OnDisable clears suppression if disabled mid-encounter
            harness.BossService.NotifyEncounterEngaged(hudSource, "Second Tower");
            Assert.IsTrue(harness.SwarmSpawner.IsSuppressed);

            InvokeLifecycleMethod(harness.BossService, "OnDisable");
            Assert.IsFalse(harness.SwarmSpawner.IsSuppressed);
        }

        [Test]
        public void SwarmSpawner_And_WaveManager_ActiveSwarmFreezesWavesAndCompletionResumesWaves()
        {
            // Arrange
            WaveSwarmIntegrationHarness harness = CreateWaveSwarmIntegrationHarness();

            // Act & Assert — Wave 1 spawns while swarm is in cooldown
            harness.WaveManager.AdvanceWaves(1f);
            harness.WaveManager.AdvanceWaves(0.01f);
            Assert.AreEqual(1, harness.WaveSpawner.SpawnCallsCount);
            Assert.IsFalse(harness.WaveManager.IsFrozen);

            // Act & Assert — Swarm starts -> WaveManager is frozen and ignores wave advancement
            harness.SwarmSpawner.AdvanceSwarm(5f);
            Assert.IsTrue(harness.WaveManager.IsFrozen);

            harness.WaveSpawner.ActiveCount = 0;
            harness.WaveManager.AdvanceWaves(10f);
            Assert.AreEqual(1, harness.WaveSpawner.SpawnCallsCount);

            // Act & Assert — Swarm finishes -> WaveManager unfreezes and resumes spawning
            harness.SwarmSpawner.AdvanceSwarm(1f);
            Assert.IsFalse(harness.WaveManager.IsFrozen);

            harness.WaveManager.AdvanceWaves(0.01f);
            Assert.AreEqual(2, harness.WaveSpawner.SpawnCallsCount);
        }

        [Test]
        public void AuthoredAssets_GameLoopProgressionConfig_DefinesExpectedSwarmMultipliersPerTier()
        {
            // Arrange
            GameLoopProgressionConfigSO progression = AssetDatabase.LoadAssetAtPath<GameLoopProgressionConfigSO>(PROGRESSION_CONFIG_PATH);
            Assert.IsNotNull(progression);

            // Act
            progression.ValidateConfiguration();
            LoopTierConfig tier1 = progression.GetTier(0);
            LoopTierConfig tier2 = progression.GetTier(1);

            // Assert
            Assert.AreEqual(1f, tier1.EnemySpawning.SwarmIntervalMultiplier, TOLERANCE);
            Assert.AreEqual(1f, tier1.EnemySpawning.SwarmSizeMultiplier, TOLERANCE);
            Assert.Greater(tier1.EnemySpawning.EnemySpawnInfos.Count, 0);

            Assert.AreEqual(0.85f, tier2.EnemySpawning.SwarmIntervalMultiplier, TOLERANCE);
            Assert.AreEqual(1.25f, tier2.EnemySpawning.SwarmSizeMultiplier, TOLERANCE);
            Assert.Greater(tier2.EnemySpawning.EnemySpawnInfos.Count, 0);
        }

        private EnemiesSpawner CreateEnemiesSpawner(
            EnemySpawnInfo[] sceneDefaults,
            IRunSessionService runSessionService,
            IGridManager gridManager,
            Camera camera,
            VFXPlayer vfxPrefab,
            int maxEnemiesPerCell,
            bool callStart)
        {
            GameObject enemiesParent = new GameObject("TestEnemiesParent");
            _createdObjects.Add(enemiesParent);

            GameObject spawnerGo = new GameObject("TestEnemiesSpawner");
            spawnerGo.SetActive(false);
            _createdObjects.Add(spawnerGo);

            EnemiesSpawner spawner = spawnerGo.AddComponent<EnemiesSpawner>();
            _createdEnemiesSpawners.Add(spawner);

            SetPrivateField(typeof(EnemiesSpawner), spawner, "_enemiesParent", enemiesParent.transform);
            SetPrivateField(typeof(EnemiesSpawner), spawner, "_poolEnemiesInfo", new List<EnemySpawnInfo>(sceneDefaults));
            SetPrivateField(typeof(EnemiesSpawner), spawner, "_swarmSpawnVfxPrefab", vfxPrefab);
            SetPrivateField(typeof(EnemiesSpawner), spawner, "_maxEnemiesPerCell", maxEnemiesPerCell);

            ContainerBuilder builder = new ContainerBuilder().SetName("EnemiesSpawnerSwarmTestContainer");
            builder.AddSingleton(gridManager, typeof(IGridManager));
            builder.AddSingleton(camera);
            builder.AddSingleton(runSessionService, typeof(IRunSessionService));
            builder.AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>));
            builder.AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState));
            Container container = builder.Build();
            _createdContainers.Add(container);

            GameObjectInjector.InjectRecursive(spawnerGo, container);
            spawnerGo.SetActive(true);
            InvokeLifecycleMethod(spawner, "Awake");
            if (callStart)
            {
                InvokeLifecycleMethod(spawner, "Start");
            }

            return spawner;
        }

        private EnemySpawnInfo CreateEnemySpawnInfo(string name, ushort maxAmount, float spawnChance)
        {
            GameObject templateHost = new GameObject($"{name}_TemplateHost");
            templateHost.SetActive(false);
            _createdObjects.Add(templateHost);

            GameObject root = new GameObject(name);
            root.transform.SetParent(templateHost.transform, false);
            root.AddComponent<EditModeEnemyAwakeShim>();
            root.AddComponent<BoxCollider>();
            root.AddComponent<Health>();
            root.AddComponent<FakeDeathSequence>();
            root.AddComponent<FakeMovementController>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<Animator>();
            visual.AddComponent<EnemyAnimator>();

            EnemyConfigSO config = ScriptableObject.CreateInstance<EnemyConfigSO>();
            _createdObjects.Add(config);
            config.MaxHealth = 100f;

            Enemy enemy = root.AddComponent<Enemy>();
            SetPrivateField(typeof(Enemy), enemy, "<Config>k__BackingField", config);
            SetPrivateField(typeof(Enemy), enemy, "_visual", visual);

            return new EnemySpawnInfo(enemy, maxAmount, new SpawnChanceInfo { SpawnChance = spawnChance });
        }

        private VFXPlayer CreateTrackedParticleVfxPrefab()
        {
            GameObject vfxHost = new GameObject("TestVfxTemplateHost");
            vfxHost.SetActive(false);
            _createdObjects.Add(vfxHost);

            GameObject vfxGo = new GameObject("SwarmSpawnVfx");
            vfxGo.transform.SetParent(vfxHost.transform, false);
            ParticleSystem ps = vfxGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.duration = 2f;
            main.playOnAwake = false;

            VFXPlayer player = vfxGo.AddComponent<VFXPlayer>();
            InvokeLifecycleMethod(player, "Awake");
            return player;
        }

        private VFXPlayer FindLatestSpawnedVfxInstance(VFXPlayer prefab)
        {
            VFXPlayer[] all = UnityEngine.Object.FindObjectsByType<VFXPlayer>();
            for (int i = all.Length - 1; i >= 0; i--)
            {
                if (all[i] != null && all[i] != prefab && all[i].gameObject.scene.IsValid())
                {
                    _createdObjects.Add(all[i].gameObject);
                    return all[i];
                }
            }

            return null;
        }

        private FakeGridManager CreateGridHarness(out Cell[] hiddenCells, out Grid playerChunk)
        {
            Cell[,] cells = new Cell[2, 2];
            cells[0, 0] = new Cell(TEST_ORIGIN + new Vector3(1f, 0f, 1f), new Vector2Int(0, 0), new Vector2Int(0, 0));
            cells[1, 0] = new Cell(TEST_ORIGIN + new Vector3(3f, 0f, 1f), new Vector2Int(1, 0), new Vector2Int(1, 0));
            cells[0, 1] = new Cell(TEST_ORIGIN + new Vector3(1f, 0f, 3f), new Vector2Int(0, 1), new Vector2Int(0, 1))
            {
                Cost = byte.MaxValue
            };
            cells[1, 1] = new Cell(TEST_ORIGIN + new Vector3(3f, 0f, 3f), new Vector2Int(1, 1), new Vector2Int(1, 1));

            hiddenCells = new[] { cells[0, 0], cells[1, 0] };
            playerChunk = new Grid(new GridConfiguration(2, 2, 2f), cells);
            return new FakeGridManager(playerChunk);
        }

        private GameObject CreateWalkableFloorCollider(Vector3 center, Vector3 size)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.GreaterOrEqual(groundLayer, 0);

            GameObject floor = new GameObject("TestWalkableFloor");
            floor.layer = groundLayer;
            floor.transform.position = center;
            BoxCollider collider = floor.AddComponent<BoxCollider>();
            collider.size = size;
            _createdObjects.Add(floor);
            Physics.SyncTransforms();
            return floor;
        }

        private void CreateExistingEnemyCollider(Vector3 position)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0);

            GameObject existing = new GameObject("ExistingEnemyOccupant");
            existing.layer = enemyLayer;
            existing.transform.position = position;
            BoxCollider collider = existing.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.5f, 1f, 0.5f);
            _createdObjects.Add(existing);
            Physics.SyncTransforms();
        }

        private Camera CreateTestCamera(Vector3 position)
        {
            GameObject cameraGo = new GameObject("TestSwarmCamera");
            cameraGo.transform.position = position;
            cameraGo.transform.rotation = Quaternion.identity;
            _createdObjects.Add(cameraGo);
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private GameLoopProgressionConfigSO CreateProgressionWithTierEntries(
            EnemySpawnInfo[] tierEntries,
            float intervalMultiplier,
            float sizeMultiplier)
        {
            GameLoopProgressionConfigSO config = ScriptableObject.CreateInstance<GameLoopProgressionConfigSO>();
            _createdObjects.Add(config);

            LoopTierConfig tier = new LoopTierConfig();
            TierEnemySpawningConfig spawning = tier.EnemySpawning;
            SetPrivateField(typeof(TierEnemySpawningConfig), spawning, "_enemySpawnInfos", new List<EnemySpawnInfo>(tierEntries));
            SetPrivateField(typeof(TierEnemySpawningConfig), spawning, "_swarmIntervalMultiplier", intervalMultiplier);
            SetPrivateField(typeof(TierEnemySpawningConfig), spawning, "_swarmSizeMultiplier", sizeMultiplier);

            SetPrivateField(typeof(GameLoopProgressionConfigSO), config, "_tiers", new List<LoopTierConfig> { tier });
            return config;
        }

        private BossSwarmIntegrationHarness CreateBossSwarmIntegrationHarness()
        {
            RecordingSwarmPresenter presenter = new RecordingSwarmPresenter();
            FakeSwarmEnemySpawner swarmEnemySpawner = new FakeSwarmEnemySpawner(CreateEnemySpawnInfo("BossTestSwarmEnemy", 20, 100f));

            GameObject swarmGo = new GameObject("IntegrationSwarmSpawner");
            swarmGo.SetActive(false);
            _createdObjects.Add(swarmGo);
            SwarmSpawner swarmSpawner = swarmGo.AddComponent<SwarmSpawner>();
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_minSwarmInterval", 5f);
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_maxSwarmInterval", 5f);
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_minSwarmSize", 4);
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_maxSwarmSize", 4);
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_swarmWarningDuration", 1f);
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_swarmDuration", 1f);
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_spawnTickInterval", 1f);

            GameObject bossGo = new GameObject("IntegrationBossEncounterService");
            bossGo.SetActive(false);
            _createdObjects.Add(bossGo);
            BossEncounterService bossService = bossGo.AddComponent<BossEncounterService>();

            ContainerBuilder builder = new ContainerBuilder().SetName("BossSwarmIntegrationContainer");
            builder.AddSingleton(new FakeBossHUDPresenter(), typeof(IBossHUDPresenter));
            builder.AddSingleton(new FakePlayerManager(bossGo), typeof(IPlayerManager));
            builder.AddSingleton(swarmSpawner, typeof(ISwarmFreezer));
            builder.AddSingleton(swarmEnemySpawner, typeof(ISwarmEnemySpawner));
            builder.AddSingleton(new FakeWaveFreezer(), typeof(IWaveFreezer));
            builder.AddSingleton(presenter, typeof(ISwarmNotificationPresenter));
            builder.AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState));
            builder.AddSingleton(new FakeNullTierRunSessionService(), typeof(IRunSessionService));
            Container container = builder.Build();
            _createdContainers.Add(container);

            GameObjectInjector.InjectRecursive(swarmGo, container);
            GameObjectInjector.InjectRecursive(bossGo, container);

            swarmGo.SetActive(true);
            bossGo.SetActive(true);
            InvokeLifecycleMethod(swarmSpawner, "Start");

            return new BossSwarmIntegrationHarness(bossService, swarmSpawner, presenter);
        }

        private WaveSwarmIntegrationHarness CreateWaveSwarmIntegrationHarness()
        {
            FakeRandomWaveEnemySpawner waveSpawner = new FakeRandomWaveEnemySpawner();
            FakeSwarmEnemySpawner swarmEnemySpawner = new FakeSwarmEnemySpawner(CreateEnemySpawnInfo("WaveTestSwarmEnemy", 10, 100f));
            RecordingSwarmPresenter presenter = new RecordingSwarmPresenter();
            FakeTimeFreezeState timeFreeze = new FakeTimeFreezeState();
            FakeNullTierRunSessionService runSession = new FakeNullTierRunSessionService();

            WaveConfig waveConfig = ScriptableObject.CreateInstance<WaveConfig>();
            _createdObjects.Add(waveConfig);
            SetPrivateField(typeof(WaveConfig), waveConfig, "_firstWaveDelay", 1f);
            SetPrivateField(typeof(WaveConfig), waveConfig, "_startSpawnWaveDelay", 5f);
            SetPrivateField(typeof(WaveConfig), waveConfig, "_initialMaxEnemiesInWave", (ushort)3);
            SetPrivateField(typeof(WaveConfig), waveConfig, "_maxEnemiesInWaveMultiplier", 1f);

            GameObject waveGo = new GameObject("IntegrationWaveManager");
            waveGo.SetActive(false);
            _createdObjects.Add(waveGo);
            WaveManager waveManager = waveGo.AddComponent<WaveManager>();
            SetPrivateField(typeof(WaveManager), waveManager, "_config", waveConfig);

            GameObject swarmGo = new GameObject("IntegrationSwarmSpawner");
            swarmGo.SetActive(false);
            _createdObjects.Add(swarmGo);
            SwarmSpawner swarmSpawner = swarmGo.AddComponent<SwarmSpawner>();
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_minSwarmInterval", 5f);
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_maxSwarmInterval", 5f);
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_minSwarmSize", 3);
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_maxSwarmSize", 3);
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_swarmWarningDuration", 1f);
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_swarmDuration", 1f);
            SetPrivateField(typeof(SwarmSpawner), swarmSpawner, "_spawnTickInterval", 1f);

            ContainerBuilder builder = new ContainerBuilder().SetName("WaveSwarmIntegrationContainer");
            builder.AddSingleton(waveSpawner, typeof(IOnRandomGridPosSpawner<EnemiesSpawner>));
            builder.AddSingleton(waveManager, typeof(IWaveFreezer));
            builder.AddSingleton(swarmEnemySpawner, typeof(ISwarmEnemySpawner));
            builder.AddSingleton(presenter, typeof(ISwarmNotificationPresenter));
            builder.AddSingleton(timeFreeze, typeof(ITimeFreezeState));
            builder.AddSingleton(runSession, typeof(IRunSessionService));
            Container container = builder.Build();
            _createdContainers.Add(container);

            GameObjectInjector.InjectRecursive(waveGo, container);
            GameObjectInjector.InjectRecursive(swarmGo, container);

            waveGo.SetActive(true);
            swarmGo.SetActive(true);
            InvokeLifecycleMethod(waveManager, "Start");
            InvokeLifecycleMethod(swarmSpawner, "Start");

            return new WaveSwarmIntegrationHarness(swarmSpawner, waveManager, waveSpawner);
        }

        private FakeBossHUDSource CreateBossHudSource()
        {
            GameObject bossObj = new GameObject("FakeBossSource");
            _createdObjects.Add(bossObj);
            Health health = bossObj.AddComponent<Health>();
            health.MaxHealth = 100f;
            InvokeLifecycleMethod(health, "OnEnable");
            FakeBossHUDSource source = bossObj.AddComponent<FakeBossHUDSource>();
            source.Setup(health);
            return source;
        }

        private static void AssertSpawnedOnExpectedCellsAndReset(
            List<Enemy> spawned,
            Cell[] validCells,
            float expectedY,
            int expectedResetCount = 1)
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                Enemy enemy = spawned[i];
                Assert.IsNotNull(enemy);
                Assert.IsTrue(enemy.gameObject.activeSelf);
                Assert.AreEqual(expectedY, enemy.transform.position.y, TOLERANCE);
                Assert.IsTrue(IsAtAnyCellXZ(enemy.transform.position, validCells), "Spawned enemy placed outside expected hidden walkable cells.");
                Assert.AreEqual(expectedResetCount, enemy.GetComponent<FakeMovementController>().ResetCount);
            }
        }

        private static bool IsAtAnyCellXZ(Vector3 pos, Cell[] validCells)
        {
            for (int i = 0; i < validCells.Length; i++)
            {
                if (Mathf.Abs(pos.x - validCells[i].WorldPos.x) < TOLERANCE
                    && Mathf.Abs(pos.z - validCells[i].WorldPos.z) < TOLERANCE)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CompleteVfxPlaybackInEditMode(VFXPlayer vfx)
        {
            FieldInfo configField = typeof(VFXPlayer).GetField("_vfxPlayConfig", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(configField, "Missing private field '_vfxPlayConfig' on VFXPlayer.");
            VFXPlayConfig playConfig = configField.GetValue(vfx) as VFXPlayConfig;
            Assert.IsNotNull(playConfig);
            Assert.IsTrue(playConfig.DestroyOnEnd);
            // Suppress runtime Destroy(gameObject) in EditMode; TearDown destroys the instance via DestroyImmediate.
            playConfig.DestroyOnEnd = false;

            InvokeLifecycleMethod(vfx, "OnAllParticlesFinished");
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {ownerType.Name}");
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

        private readonly struct BossSwarmIntegrationHarness
        {
            public readonly BossEncounterService BossService;
            public readonly SwarmSpawner SwarmSpawner;
            public readonly RecordingSwarmPresenter Presenter;

            public BossSwarmIntegrationHarness(
                BossEncounterService bossService,
                SwarmSpawner swarmSpawner,
                RecordingSwarmPresenter presenter)
            {
                BossService = bossService;
                SwarmSpawner = swarmSpawner;
                Presenter = presenter;
            }
        }

        private readonly struct WaveSwarmIntegrationHarness
        {
            public readonly SwarmSpawner SwarmSpawner;
            public readonly WaveManager WaveManager;
            public readonly FakeRandomWaveEnemySpawner WaveSpawner;

            public WaveSwarmIntegrationHarness(
                SwarmSpawner swarmSpawner,
                WaveManager waveManager,
                FakeRandomWaveEnemySpawner waveSpawner)
            {
                SwarmSpawner = swarmSpawner;
                WaveManager = waveManager;
                WaveSpawner = waveSpawner;
            }
        }

        [ExecuteAlways]
        private sealed class EditModeEnemyAwakeShim : MonoBehaviour
        {
            private bool _initialized;

            private void Awake()
            {
                if (_initialized || Application.isPlaying)
                {
                    return;
                }

                _initialized = true;
                EnemyAnimator animator = GetComponentInChildren<EnemyAnimator>(true);
                if (animator != null)
                {
                    InvokeLifecycleMethod(animator, "Awake");
                }

                Enemy enemy = GetComponent<Enemy>();
                if (enemy != null)
                {
                    InvokeLifecycleMethod(enemy, "Awake");
                }
            }
        }

        private sealed class FakeDeathSequence : MonoBehaviour, INeedToCompleteBeforeDisable
        {
            public event EventHandler OnCompleted
            {
                add { }
                remove { }
            }
        }

        private sealed class FakeMovementController : MonoBehaviour, IMovementController
        {
            public int ResetCount { get; private set; }
            public bool CanBeTeleported { get { return true; } }

            public void ResetAfterRelocation()
            {
                ResetCount++;
            }

            public float GetCurrentMovementSpeed()
            {
                return 0f;
            }

            public bool IsOnGround()
            {
                return true;
            }

            public void MoveToPositionInTimeIgnoringSpeed(Vector3 pos, float time)
            {
            }

            public void SetMovementSpeedMultiplier(float multiplier)
            {
            }
        }

        private sealed class FakeBossHUDSource : MonoBehaviour, IBossHUDSource
        {
            public IHealth Health { get; private set; }
            public BossHealthBarKind HealthBarKind { get { return BossHealthBarKind.Tower; } }
            public float EnrageHealthPercent { get { return 0.4f; } }
            public bool IsEnraged { get { return false; } }
            public bool IsActive { get { return true; } }

            public event Action OnPresentationChanged
            {
                add { }
                remove { }
            }

            public void Setup(IHealth health)
            {
                Health = health;
            }
        }

        private sealed class FakeBossHUDPresenter : IBossHUDPresenter
        {
            public void Show(IBossHUDSource source, string bossName)
            {
            }

            public void Hide()
            {
            }
        }

        private sealed class FakeEncounterArena : IEncounterArenaController
        {
            public bool IsEncounterActive { get { return false; } }
            public bool IsEncounterCompleted { get { return true; } }
            public bool IsLeashCountdownActive { get { return false; } }
            public float RemainingLeashSeconds { get { return 0f; } }
            public float ArenaRadius { get { return 10f; } }
            public Vector3 ArenaCenter { get { return Vector3.zero; } }
            public IBossCombatTarget BossTarget { get { return null; } }

            public void NotifyDirectCombatEngaged()
            {
            }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            public GameObject GameObject { get; }
            public Assets.Scripts.HealthSystem.IHealth Health { get { return null; } }
            public Assets.Scripts.Audio.IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public Assets.Scripts.Player.Car.ICarController CarController { get { return null; } }
            public Assets.Scripts.Player.Car.ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public Assets.Scripts.LevelSystem.ILevelController LevelController { get { return null; } }
            public Assets.Scripts.Skills.ISkillsRegistry SkillsRegistry { get { return null; } }
            public Assets.Scripts.Player.IPlayerDamagedHandler DamagedHandler { get { return null; } }

            public FakePlayerManager(GameObject playerObject)
            {
                GameObject = playerObject;
            }
        }

        private sealed class FakeGridManager : IGridManager
        {
            public Cell DestinationCell { get { return null; } }
            public Grid GridPlayerChunk { get; }
            public Grid WorldGrid { get; }

            public FakeGridManager(Grid grid)
            {
                GridPlayerChunk = grid;
                WorldGrid = grid;
            }
        }

        private sealed class FakeDamageNumbersSpawner : IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>
        {
            public uint CurrentlySpawnedObjectsCount { get { return 0; } }

            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public void Spawn(Vector3 pos, DamageNubmersSpawnerConfig specificConfig, int count = 1)
            {
            }
        }

        private sealed class FakeSwarmEnemySpawner : ISwarmEnemySpawner
        {
            private readonly List<EnemySpawnInfo> _configs;

            public IReadOnlyList<EnemySpawnInfo> EnemyConfigs { get { return _configs; } }

            public FakeSwarmEnemySpawner(EnemySpawnInfo info)
            {
                _configs = new List<EnemySpawnInfo> { info };
            }

            public void SpawnSpecificEnemy(EnemySpawnInfo enemyInfo, int count = 1)
            {
            }

            public bool TrySpawnEnemyAt(EnemySpawnInfo enemyInfo, Vector3 position, out Enemy enemy)
            {
                enemy = null;
                return false;
            }
        }

        private sealed class FakeRandomWaveEnemySpawner : IOnRandomGridPosSpawner<EnemiesSpawner>
        {
            public int SpawnCallsCount { get; private set; }
            public uint ActiveCount { get; set; }
            public uint CurrentlySpawnedObjectsCount { get { return ActiveCount; } }

            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public void SpawnAtRandomGridPos(int count = 1)
            {
                SpawnCallsCount++;
                ActiveCount += (uint)Mathf.Max(0, count);
            }
        }

        private sealed class FakeWaveFreezer : IWaveFreezer
        {
            public bool IsFrozen { get; set; }
        }

        private sealed class RecordingSwarmPresenter : ISwarmNotificationPresenter
        {
            public List<int> IncomingSeconds { get; } = new List<int>();

            public void ShowIncoming(int secondsRemaining)
            {
                IncomingSeconds.Add(secondsRemaining);
            }

            public void ShowOngoing()
            {
            }

            public void Hide()
            {
            }
        }

        private sealed class FakeTimeFreezeState : ITimeFreezeState
        {
            public bool IsFrozen { get { return false; } }
            public float UnfrozenTime { get { return 0f; } }

            public event Action<bool> OnFrozenChanged
            {
                add { }
                remove { }
            }
        }

        private sealed class FakeNullTierRunSessionService : IRunSessionService
        {
            public int CurrentTierIndex { get { return 0; } }
            public int TotalTiersCount { get { return 1; } }
            public bool IsFinalTier { get { return true; } }
            public LoopTierConfig ActiveTierConfig { get { return null; } }
            public GameLoopProgressionConfigSO ProgressionConfig { get { return null; } }
            public bool HasPendingRestorationSnapshot { get { return false; } }
            public uint AccumulatedRunSeconds { get { return 0; } }
            public bool IsGodModeEnabled { get { return false; } }

            public void CaptureSnapshotAndAdvanceTier(IPlayerManager playerManager, uint elapsedSeconds)
            {
            }

            public void CaptureSnapshotAndSetTier(int tierIndex, IPlayerManager playerManager, uint elapsedSeconds)
            {
            }

            public void CaptureFinalRunTimer(uint elapsedSeconds)
            {
            }

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

            public void SetGodModeEnabled(bool isEnabled)
            {
            }

            public void ResetSession()
            {
            }

            public void ResetSessionToTier(int tierIndex)
            {
            }
        }
    }
}
