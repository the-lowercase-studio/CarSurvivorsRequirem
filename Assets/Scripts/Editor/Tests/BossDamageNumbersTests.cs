using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Audio;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Effects;
using Assets.Scripts.Enemies.Bosses;
using Assets.Scripts.Enemies.Bosses.Golem;
using Assets.Scripts.Enemies.Bosses.Towers;
using Assets.Scripts.Enemies.Bosses.Towers.Arena;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower;
using Assets.Scripts.Enemies.Bosses.Towers.RailgunTower;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.Interactables;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Shapes;
using Assets.Scripts.Skills;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.StatusEffects;
using DG.Tweening;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEditor;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class BossDamageNumbersTests
    {
        private const string DAMAGE_NUMBER_PREFAB_PATH = "Assets/Prefabs/DamageNumber.prefab";
        private const string RAILGUN_TOWER_PREFAB_PATH = "Assets/Prefabs/Enemies/Bosses/Towers/Railgun/RailgunTower.prefab";
        private const string MORTAR_TOWER_PREFAB_PATH = "Assets/Prefabs/Enemies/Bosses/Towers/Mortal/MortalTower.prefab";
        private const string GOLEM_BOSS_PREFAB_PATH = "Assets/Prefabs/Enemies/Bosses/Golem/GolemBoss.prefab";

        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
        private readonly List<Container> _createdContainers = new List<Container>();

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();

            for (int i = 0; i < _createdContainers.Count; i++)
            {
                _createdContainers[i].Dispose();
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

            CircularTelegraphIndicator[] strayIndicators = UnityEngine.Object.FindObjectsByType<CircularTelegraphIndicator>();
            for (int i = 0; i < strayIndicators.Length; i++)
            {
                if (strayIndicators[i] != null && strayIndicators[i].gameObject.scene.IsValid())
                {
                    UnityEngine.Object.DestroyImmediate(strayIndicators[i].gameObject);
                }
            }
        }

        [Test]
        public void DamageNumbersSpawner_NormalEnemyHit_UsesUnscaledBaseMultiplier()
        {
            DamageNumbersSpawner spawner = CreateDamageNumbersSpawner(baseScaleMultiplier: 1.4f, bossScaleMultiplier: 1.5f);

            spawner.Spawn(Vector3.zero, new DamageNubmersSpawnerConfig(25f, ShapeModes.Hemisphere));

            DamageNumber spawnedPopup = spawner.GetComponentInChildren<DamageNumber>(false);
            Assert.IsNotNull(spawnedPopup);
            Assert.AreEqual(1.4f, spawnedPopup.TargetScale.x, 0.0001f);
            Assert.AreEqual(1.4f, spawnedPopup.TargetScale.y, 0.0001f);
            Assert.AreEqual(1.4f, spawnedPopup.TargetScale.z, 0.0001f);
        }

        [Test]
        public void DamageNumbersSpawner_BossHit_ScalesPopupByBossMultiplier()
        {
            DamageNumbersSpawner spawner = CreateDamageNumbersSpawner(baseScaleMultiplier: 1.4f, bossScaleMultiplier: 1.5f);

            spawner.Spawn(Vector3.zero, new DamageNubmersSpawnerConfig(25f, ShapeModes.Hemisphere, isBoss: true));

            DamageNumber spawnedPopup = spawner.GetComponentInChildren<DamageNumber>(false);
            Assert.IsNotNull(spawnedPopup);
            Assert.AreEqual(2.1f, spawnedPopup.TargetScale.x, 0.0001f);
            Assert.AreEqual(2.1f, spawnedPopup.TargetScale.y, 0.0001f);
            Assert.AreEqual(2.1f, spawnedPopup.TargetScale.z, 0.0001f);
        }

        [Test]
        public void RailgunTowerBoss_TakeDamageAtLeftLeg_SpawnsNumberOnLeftLegInsteadOfCenter()
        {
            RecordingDamageNumbersSpawner recorder = new RecordingDamageNumbersSpawner();
            GameObject playerObject = CreateTrackedGameObject("Player");
            RailgunTowerBoss railgunTower = InstantiateBoss<RailgunTowerBoss>(
                RAILGUN_TOWER_PREFAB_PATH,
                new Vector3(20f, 0f, -10f),
                Quaternion.Euler(0f, -24.84f, 0f),
                recorder,
                playerObject
            );
            BoxCollider leftLeg = GetRailgunLegCollider(railgunTower, "LegCollider_Left");
            Vector3 leftLegCenter = leftLeg.transform.TransformPoint(leftLeg.center);
            Vector3 frontHitOnLeftLeg = leftLegCenter + (railgunTower.transform.forward * -1.2f);

            EntityManipulationHelper.Damage(leftLeg, 50f, frontHitOnLeftLeg);

            Assert.AreEqual(1, recorder.Spawns.Count);
            Assert.IsTrue(recorder.Spawns[0].Config.IsBoss);
            Assert.AreEqual(50f, recorder.Spawns[0].Config.Damage, 0.0001f);

            Vector3 spawnPos = recorder.Spawns[0].Position;
            Assert.AreEqual(3.5f, spawnPos.y, 0.0001f);
            Assert.Less(Vector2.Distance(new Vector2(spawnPos.x, spawnPos.z), new Vector2(leftLegCenter.x, leftLegCenter.z)), 2f);
            Assert.Greater(Vector2.Distance(new Vector2(spawnPos.x, spawnPos.z), new Vector2(20f, -10f)), 4f);
        }

        [Test]
        public void RailgunTowerBoss_TakeDamageAtRightLeg_SpawnsNumberOnRightLegInsteadOfCenter()
        {
            RecordingDamageNumbersSpawner recorder = new RecordingDamageNumbersSpawner();
            GameObject playerObject = CreateTrackedGameObject("Player");
            RailgunTowerBoss railgunTower = InstantiateBoss<RailgunTowerBoss>(
                RAILGUN_TOWER_PREFAB_PATH,
                new Vector3(20f, 0f, -10f),
                Quaternion.Euler(0f, -24.84f, 0f),
                recorder,
                playerObject
            );
            BoxCollider rightLeg = GetRailgunLegCollider(railgunTower, "LegCollider_Right");
            Vector3 rightLegCenter = rightLeg.transform.TransformPoint(rightLeg.center);
            Vector3 frontHitOnRightLeg = rightLegCenter + (railgunTower.transform.forward * -1.2f);

            EntityManipulationHelper.Damage(rightLeg, 75f, frontHitOnRightLeg);

            Assert.AreEqual(1, recorder.Spawns.Count);
            Assert.IsTrue(recorder.Spawns[0].Config.IsBoss);
            Assert.AreEqual(75f, recorder.Spawns[0].Config.Damage, 0.0001f);

            Vector3 spawnPos = recorder.Spawns[0].Position;
            Assert.AreEqual(3.5f, spawnPos.y, 0.0001f);
            Assert.Less(Vector2.Distance(new Vector2(spawnPos.x, spawnPos.z), new Vector2(rightLegCenter.x, rightLegCenter.z)), 2f);
            Assert.Greater(Vector2.Distance(new Vector2(spawnPos.x, spawnPos.z), new Vector2(20f, -10f)), 4f);
        }

        [Test]
        public void RailgunTowerBoss_TakeDamageWithoutHitPosition_FallsBackToLegClosestToPlayer()
        {
            RecordingDamageNumbersSpawner recorder = new RecordingDamageNumbersSpawner();
            GameObject playerObject = CreateTrackedGameObject("Player");
            RailgunTowerBoss railgunTower = InstantiateBoss<RailgunTowerBoss>(
                RAILGUN_TOWER_PREFAB_PATH,
                new Vector3(20f, 0f, -10f),
                Quaternion.Euler(0f, -24.84f, 0f),
                recorder,
                playerObject
            );
            BoxCollider rightLeg = GetRailgunLegCollider(railgunTower, "LegCollider_Right");
            Vector3 rightLegCenter = rightLeg.transform.TransformPoint(rightLeg.center);
            playerObject.transform.position = rightLegCenter + new Vector3(1f, 0f, -2f);

            railgunTower.TakeDamage(100f);

            Assert.AreEqual(1, recorder.Spawns.Count);
            Assert.IsTrue(recorder.Spawns[0].Config.IsBoss);

            Vector3 spawnPos = recorder.Spawns[0].Position;
            Assert.AreEqual(rightLegCenter.x, spawnPos.x, 0.0001f);
            Assert.AreEqual(3.5f, spawnPos.y, 0.0001f);
            Assert.AreEqual(rightLegCenter.z, spawnPos.z, 0.0001f);
        }

        [Test]
        public void MortarTowerBoss_TakeDamage_MarksDamageNumberAsBoss()
        {
            RecordingDamageNumbersSpawner recorder = new RecordingDamageNumbersSpawner();
            GameObject playerObject = CreateTrackedGameObject("Player");
            MortarTowerBoss mortarTower = InstantiateBoss<MortarTowerBoss>(
                MORTAR_TOWER_PREFAB_PATH,
                Vector3.zero,
                Quaternion.identity,
                recorder,
                playerObject
            );

            mortarTower.TakeDamage(40f);

            Assert.AreEqual(1, recorder.Spawns.Count);
            Assert.IsTrue(recorder.Spawns[0].Config.IsBoss);
            Assert.AreEqual(40f, recorder.Spawns[0].Config.Damage, 0.0001f);
        }

        [Test]
        public void GolemBoss_TakeDamage_MarksDamageNumberAsBoss()
        {
            RecordingDamageNumbersSpawner recorder = new RecordingDamageNumbersSpawner();
            GameObject playerObject = CreateTrackedGameObject("Player");
            GolemBoss golemBoss = InstantiateBoss<GolemBoss>(
                GOLEM_BOSS_PREFAB_PATH,
                Vector3.zero,
                Quaternion.identity,
                recorder,
                playerObject
            );

            golemBoss.TakeDamage(60f);

            Assert.AreEqual(1, recorder.Spawns.Count);
            Assert.IsTrue(recorder.Spawns[0].Config.IsBoss);
            Assert.AreEqual(60f, recorder.Spawns[0].Config.Damage, 0.0001f);
        }

        [Test]
        public void DamageNumbersSpawner_SpawnWithInitializedCamera_FacesCameraImmediatelyWithoutMirroring()
        {
            DamageNumbersSpawner spawner = CreateDamageNumbersSpawner(baseScaleMultiplier: 1.4f, bossScaleMultiplier: 1.5f);
            Camera camera = CreateTrackedCamera(new Vector3(0f, 11f, -7f), Vector3.zero);
            spawner.Initialize(camera);

            spawner.Spawn(Vector3.zero, new DamageNubmersSpawnerConfig(60f, ShapeModes.Hemisphere, isBoss: true));

            DamageNumber spawnedPopup = spawner.GetComponentInChildren<DamageNumber>(false);
            Assert.IsNotNull(spawnedPopup);

            TMPro.TextMeshPro textComponent = spawnedPopup.GetComponentInChildren<TMPro.TextMeshPro>(true);
            Assert.IsNotNull(textComponent);

            Assert.Greater(Vector3.Dot(textComponent.transform.forward, camera.transform.forward), 0.99f);
            Assert.Greater(Vector3.Dot(textComponent.transform.right, camera.transform.right), 0.99f);
        }

        [Test]
        public void DamageNumbersSpawner_ReinitializeCameraAfterSceneReload_UpdatesPooledPopupCameraReference()
        {
            DamageNumbersSpawner spawner = CreateDamageNumbersSpawner(baseScaleMultiplier: 1.4f, bossScaleMultiplier: 1.5f);
            Camera firstCamera = CreateTrackedCamera(new Vector3(0f, 11f, -7f), Vector3.zero);
            spawner.Initialize(firstCamera);

            spawner.Spawn(Vector3.zero, new DamageNubmersSpawnerConfig(60f, ShapeModes.Hemisphere, isBoss: true));
            DamageNumber firstPopup = spawner.GetComponentInChildren<DamageNumber>(false);
            Assert.IsNotNull(firstPopup);

            CompleteDamageNumberLifecycle(firstPopup);
            Assert.IsFalse(firstPopup.gameObject.activeSelf);

            UnityEngine.Object.DestroyImmediate(firstCamera.gameObject);

            Vector3 secondSpawnPos = new Vector3(20f, 0f, -7f);
            Camera secondCamera = CreateTrackedCamera(new Vector3(20f, 11f, 0f), secondSpawnPos);
            spawner.Initialize(secondCamera);

            spawner.Spawn(secondSpawnPos, new DamageNubmersSpawnerConfig(60f, ShapeModes.Hemisphere, isBoss: true));
            DamageNumber reusedPopup = spawner.GetComponentInChildren<DamageNumber>(false);
            Assert.AreSame(firstPopup, reusedPopup);

            // Reset rotation and run LateUpdate to prove FaceMainCameraDirection now holds secondCamera instead of destroyed firstCamera.
            reusedPopup.transform.rotation = Quaternion.identity;
            FaceMainCameraDirection cameraFacing = reusedPopup.GetComponentInChildren<FaceMainCameraDirection>(true);
            Assert.IsNotNull(cameraFacing);
            SetRotationSmoothSpeed(cameraFacing, 0f);
            InvokeLifecycleMethod(cameraFacing, "LateUpdate");

            TMPro.TextMeshPro textComponent = reusedPopup.GetComponentInChildren<TMPro.TextMeshPro>(true);
            Assert.IsNotNull(textComponent);
            Assert.Greater(Vector3.Dot(textComponent.transform.forward, secondCamera.transform.forward), 0.99f);
            Assert.Greater(Vector3.Dot(textComponent.transform.right, secondCamera.transform.right), 0.99f);
        }

        private DamageNumbersSpawner CreateDamageNumbersSpawner(float baseScaleMultiplier, float bossScaleMultiplier)
        {
            DamageNumber popupPrefab = AssetDatabase.LoadAssetAtPath<DamageNumber>(DAMAGE_NUMBER_PREFAB_PATH);
            Assert.IsNotNull(popupPrefab);

            GameObject spawnerObject = new GameObject("TestDamageNumbersSpawner");
            spawnerObject.SetActive(false);
            _createdObjects.Add(spawnerObject);

            DamageNumbersSpawner spawner = spawnerObject.AddComponent<DamageNumbersSpawner>();

            SerializedObject serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("_damagePopupVisibilityDuration").floatValue = 0.6f;
            serializedSpawner.FindProperty("_damagePopupPrefab").objectReferenceValue = popupPrefab;
            serializedSpawner.FindProperty("_popupsMovementRange").floatValue = 2f;
            serializedSpawner.FindProperty("_bossDamagePopupScaleMultiplier").floatValue = bossScaleMultiplier;

            SerializedProperty thresholdsProperty = serializedSpawner.FindProperty("_visualApearanceByDamageTresholds");
            thresholdsProperty.arraySize = 1;
            SerializedProperty firstEntry = thresholdsProperty.GetArrayElementAtIndex(0);
            firstEntry.FindPropertyRelative("_treshold").floatValue = 0f;

            SerializedProperty appearanceProperty = firstEntry.FindPropertyRelative("_damagePopupApearance");
            appearanceProperty.FindPropertyRelative("FontSize").floatValue = 2.1f;
            appearanceProperty.FindPropertyRelative("GrowFontSizeAnimationScaleMultiplier").floatValue = baseScaleMultiplier;
            appearanceProperty.FindPropertyRelative("Color").colorValue = Color.white;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

            spawnerObject.SetActive(true);
            InvokeLifecycleMethod(spawner, "Awake");
            return spawner;
        }

        private TBoss InstantiateBoss<TBoss>(
            string prefabPath,
            Vector3 position,
            Quaternion rotation,
            RecordingDamageNumbersSpawner recorder,
            GameObject playerObject) where TBoss : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab);

            GameObject inactiveParent = new GameObject("TempInactiveBossHost");
            inactiveParent.SetActive(false);

            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation, inactiveParent.transform);
            instance.SetActive(false);
            instance.transform.SetParent(null, true);
            UnityEngine.Object.DestroyImmediate(inactiveParent);

            _createdObjects.Add(instance);

            Container container = new ContainerBuilder()
                .AddSingleton(new FakePlayerManager(playerObject), typeof(IPlayerManager))
                .AddSingleton(new FakeGridManager(), typeof(IGridManager))
                .AddSingleton(new FakeExpParticleSpawner(), typeof(IInWorldSpaceSpawner<ExpParticleSpawner, float>))
                .AddSingleton(recorder, typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>))
                .AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState))
                .AddSingleton(new FakeRunSessionService(), typeof(IRunSessionService))
                .AddSingleton(new FakeBossEncounterService(), typeof(IBossEncounterService))
                .Build();
            _createdContainers.Add(container);

            GameObjectInjector.InjectRecursive(instance, container);
            instance.SetActive(true);

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

            TBoss boss = instance.GetComponent<TBoss>();
            Assert.IsNotNull(boss);
            return boss;
        }

        private static void InvokeLifecycleMethod(object target, string methodName)
        {
            Type type = target.GetType();
            while (type != null)
            {
                MethodInfo method = type.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                );
                if (method != null)
                {
                    method.Invoke(target, null);
                    return;
                }

                type = type.BaseType;
            }
        }

        private GameObject CreateTrackedGameObject(string objectName)
        {
            GameObject obj = new GameObject(objectName);
            _createdObjects.Add(obj);
            return obj;
        }

        private Camera CreateTrackedCamera(Vector3 position, Vector3 lookTarget)
        {
            GameObject cameraObject = CreateTrackedGameObject("TestCamera");
            cameraObject.transform.position = position;
            cameraObject.transform.LookAt(lookTarget);
            return cameraObject.AddComponent<Camera>();
        }

        private static void CompleteDamageNumberLifecycle(DamageNumber damageNumber)
        {
            FieldInfo elapsedTimeField = typeof(DamageNumber).GetField(
                "_elapsedTime",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(elapsedTimeField);
            elapsedTimeField.SetValue(damageNumber, 10f);
            InvokeLifecycleMethod(damageNumber, "Update");
        }

        private static void SetRotationSmoothSpeed(FaceMainCameraDirection cameraFacing, float smoothSpeed)
        {
            FieldInfo smoothSpeedField = typeof(FaceMainCameraDirection).GetField(
                "_rotationSmoothSpeed",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(smoothSpeedField);
            smoothSpeedField.SetValue(cameraFacing, smoothSpeed);
        }

        private static BoxCollider GetRailgunLegCollider(RailgunTowerBoss railgunTower, string legObjectName)
        {
            BoxCollider[] colliders = railgunTower.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].gameObject.name == legObjectName)
                {
                    return colliders[i];
                }
            }

            Assert.Fail($"Expected BoxCollider '{legObjectName}' on RailgunTower.");
            return null;
        }

        private readonly struct RecordedDamageNumberSpawn
        {
            public Vector3 Position { get; }
            public DamageNubmersSpawnerConfig Config { get; }

            public RecordedDamageNumberSpawn(Vector3 position, DamageNubmersSpawnerConfig config)
            {
                Position = position;
                Config = config;
            }
        }

        private sealed class RecordingDamageNumbersSpawner : IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>
        {
            public List<RecordedDamageNumberSpawn> Spawns { get; } = new List<RecordedDamageNumberSpawn>();
            public uint CurrentlySpawnedObjectsCount { get { return (uint)Spawns.Count; } }

            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public void Spawn(Vector3 pos, DamageNubmersSpawnerConfig specificConfig, int count = 1)
            {
                Spawns.Add(new RecordedDamageNumberSpawn(pos, specificConfig));
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

            public FakePlayerManager(GameObject playerObject)
            {
                GameObject = playerObject;
            }
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

            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public void Spawn(Vector3 pos, float specificConfig, int count = 1)
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

        private sealed class FakeRunSessionService : IRunSessionService
        {
            private bool _isGodModeEnabled;

            public int CurrentTierIndex { get { return 0; } }
            public int TotalTiersCount { get { return 1; } }
            public bool IsFinalTier { get { return true; } }
            public LoopTierConfig ActiveTierConfig { get { return null; } }
            public GameLoopProgressionConfigSO ProgressionConfig { get { return null; } }
            public bool HasPendingRestorationSnapshot { get { return false; } }
            public uint AccumulatedRunSeconds { get { return 0; } }
            public bool IsGodModeEnabled { get { return _isGodModeEnabled; } }

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
                _isGodModeEnabled = isEnabled;
            }

            public void ResetSession()
            {
                _isGodModeEnabled = false;
            }

            public void ResetSessionToTier(int tierIndex)
            {
                _isGodModeEnabled = false;
            }
        }

        private sealed class FakeBossEncounterService : IBossEncounterService
        {
            public bool IsAnyBossActive { get { return false; } }
            public int RequiredTowersCount { get { return 2; } }
            public int DefeatedTowersCount { get { return 0; } }
            public int RemainingTowersCount { get { return 2; } }

            public event Action<int, int> OnTowerDefeated
            {
                add { }
                remove { }
            }

            public void RegisterTower(IEncounterArenaController towerArena)
            {
            }

            public void UnregisterTower(IEncounterArenaController towerArena)
            {
            }

            public void RegisterGolemStatue(IGolemStatue statue)
            {
            }

            public void UnregisterGolemStatue(IGolemStatue statue)
            {
            }

            public void NotifyEncounterEngaged(IBossHUDSource source, string displayName)
            {
            }

            public void NotifyEncounterDisengaged()
            {
            }

            public void NotifyTowerDefeated(IEncounterArenaController towerArena)
            {
            }

            public bool IsPositionWithinAnyTowerArena(Vector3 worldPosition, float extraBuffer = 0f)
            {
                return false;
            }

            public void SpawnGolemBoss(Vector3? spawnPosition = null)
            {
            }

            public int CollectEngagedBosses(List<IBossCombatTarget> buffer)
            {
                return 0;
            }
        }
    }
}
