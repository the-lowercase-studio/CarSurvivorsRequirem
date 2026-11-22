using System;
using System.Collections;
using System.Collections.Generic;
using Assets.ScriptableObjects;
using Assets.ScriptableObjects.Skills;
using Assets.ScriptableObjects.Skills.PlayerSkills.LandmineSkill;
using Assets.ScriptableObjects.Skills.PlayerSkills.MinigunSkill;
using Assets.ScriptableObjects.Skills.PlayerSkills.SawSkill;
using Assets.Scripts.Audio;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Barrel;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.LayerMasks.Constants;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.PowerUps;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.PlayerSkills.LandmineTrap;
using Assets.Scripts.Skills.PlayerSkills.Minigun;
using Assets.Scripts.Skills.PlayerSkills.Saw;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.TestTools;
using NavigationGrid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Tests.PlayMode
{
    public class EnemiesAndCombatPlayModeTests
    {
        private const string STANDING_ZOMBIE_PREFAB_PATH = "Assets/Prefabs/Enemies/Basic/Zombies/Standing/StandingZombieLevel1.prefab";
        private const string BARREL_ENEMY_PREFAB_PATH = "Assets/Prefabs/Enemies/Basic/BarrelEnemy.prefab";
        private const string DROP_CONFIG_ASSET_PATH = "Assets/ScriptableObjects/Collectibles/DropAnimationConfiguration.asset";
        private const string SAW_PREFAB_PATH = "Assets/Prefabs/Skills/Saw/Saw.prefab";
        private const string SAW_CONFIG_ASSET_PATH = "Assets/ScriptableObjects/Skills/PlayerSkills/SawSkill/SawSkillUpgradeableConfig.asset";
        private const string LANDMINE_PREFAB_PATH = "Assets/Prefabs/Skills/Landmine/Landmine.prefab";
        private const string LANDMINE_CONFIG_ASSET_PATH = "Assets/ScriptableObjects/Skills/PlayerSkills/LandmineSkill/LandmineSkillUpgradeableConfig.asset";
        private const string MINIGUN_TURRET_PREFAB_PATH = "Assets/Prefabs/Skills/Minigun/MinigunTurret.prefab";
        private const string MINIGUN_INFO_ASSET_PATH = "Assets/ScriptableObjects/Skills/PlayerSkills/MinigunSkill/MinigunSkillInfo.asset";
        private const string MINIGUN_CONFIG_ASSET_PATH = "Assets/ScriptableObjects/Skills/PlayerSkills/MinigunSkill/MinigunSkillUpgradeableConfig.asset";

        private PlayModeTestHarness _harness;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _harness = new PlayModeTestHarness();
            _harness.BeginIsolatedScene("EnemiesAndCombatPlayMode");
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
        public IEnumerator StandingZombie_InvokeRepeatingCollisionAndHitFrame_DamagesPlayerInFrontArc()
        {
            CreateFlatGround();

            GameObject cameraGo = _harness.TrackGameObject("TestCamera");
            Camera testCamera = cameraGo.AddComponent<Camera>();
            testCamera.transform.position = new Vector3(3f, 2f, 0.8f);
            testCamera.transform.LookAt(new Vector3(1f, 0.5f, 0.8f));

            GameObject playerGo = CreatePlayerTargetWithHealth(
                "PlayerTarget",
                new Vector3(1f, 0.5f, 1.4f),
                new Vector3(1.2f, 1.5f, 0.8f),
                200f,
                out Health playerHealth);

            FakeGridManager movingGrid = new FakeGridManager();
            movingGrid.SetDestinationCell(movingGrid.WorldGrid.Cells[0, 0]);

            FakePlayerManager playerManager = new FakePlayerManager(playerGo, playerHealth);
            Container container = BuildEnemyContainer(
                playerManager,
                new FakeExplosionVfxPool(),
                new FakeExpSpawner(),
                movingGrid);

            GameObject zombiePrefab = PlayModeTestHarness.LoadEditorAsset<GameObject>(STANDING_ZOMBIE_PREFAB_PATH);
            GameObject zombieGo = _harness.InstantiateTrackedPrefab(
                zombiePrefab,
                new Vector3(1f, 0f, 0.2f),
                Quaternion.identity,
                container);

            Animator zombieAnimator = zombieGo.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(zombieAnimator);
            zombieAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Enemy enemy = zombieGo.GetComponent<Enemy>();
            Assert.IsNotNull(enemy);
            enemy.OnGet();

            float expectedHealthAfterHit = 200f - enemy.Config.Damage;
            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => playerHealth.CurrentHealth < 200f,
                3.5f,
                "Expected EnemyCollisionsController + EnemyAttackController + ZombieAnimController hit frame to damage player.");

            Assert.AreEqual(expectedHealthAfterHit, playerHealth.CurrentHealth, 0.01f);
        }

        [UnityTest]
        public IEnumerator EnemyDeathHandler_WhenPresentationEffectsHang_CompletesViaWatchdogTimeout()
        {
            CreateFlatGround();

            GameObject playerGo = CreatePlayerTargetWithHealth(
                "PlayerDummy",
                new Vector3(50f, 0.5f, 50f),
                Vector3.one,
                100f,
                out Health playerHealth);

            FakeExpSpawner expSpawner = new FakeExpSpawner();
            Container container = BuildEnemyContainer(
                new FakePlayerManager(playerGo, playerHealth),
                new FakeExplosionVfxPool(),
                expSpawner);

            GameObject zombiePrefab = PlayModeTestHarness.LoadEditorAsset<GameObject>(STANDING_ZOMBIE_PREFAB_PATH);
            GameObject zombieGo = _harness.InstantiateTrackedPrefab(
                zombiePrefab,
                Vector3.zero,
                Quaternion.identity,
                container);

            Enemy enemy = zombieGo.GetComponent<Enemy>();
            EnemyDeathHandler deathHandler = zombieGo.GetComponent<EnemyDeathHandler>();
            PlayModeTestHarness.SetPrivateField(typeof(EnemyDeathHandler), deathHandler, "_presentationTimeout", 0.20f);
            enemy.OnGet();

            int releaseCount = 0;
            enemy.OnCanBeReleased += (sender, args) => { releaseCount++; };

            LogAssert.Expect(LogType.Error, "Enemy death presentation exceeded its watchdog; completing release.");

            enemy.Health.DecreaseHealth(enemy.Health.MaxHealth + 100f);
            Assert.AreEqual(0, releaseCount, "Death release must wait while death VFX/Audio presentation is active.");

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => releaseCount == 1,
                1.5f,
                "Expected EnemyDeathHandler watchdog timeout to complete and signal pool release.");

            Assert.AreEqual(1, expSpawner.CallsCount);
        }

        [UnityTest]
        public IEnumerator BarrelEnemy_PlayerContactPrimingAndHitFrame_DetonatesDamagesPlayerAndCompletesDeathHandler()
        {
            CreateFlatGround();

            GameObject playerGo = CreatePlayerTargetWithHealth(
                "BarrelPlayerTarget",
                new Vector3(0f, 0.5f, 0.4f),
                new Vector3(1.2f, 1.5f, 1.2f),
                300f,
                out Health playerHealth);

            FakeExplosionVfxPool explosionPool = new FakeExplosionVfxPool();
            FakeExpSpawner expSpawner = new FakeExpSpawner();
            Container container = BuildEnemyContainer(
                new FakePlayerManager(playerGo, playerHealth),
                explosionPool,
                expSpawner);

            GameObject barrelPrefab = PlayModeTestHarness.LoadEditorAsset<GameObject>(BARREL_ENEMY_PREFAB_PATH);
            GameObject barrelGo = _harness.InstantiateTrackedPrefab(
                barrelPrefab,
                new Vector3(0f, 0.5f, 0f),
                Quaternion.identity,
                container);

            Enemy barrelEnemy = barrelGo.GetComponent<Enemy>();
            BarrelEnemyExplosionController explosionController = barrelGo.GetComponent<BarrelEnemyExplosionController>();
            Assert.IsNotNull(barrelEnemy);
            Assert.IsNotNull(explosionController);
            barrelEnemy.OnGet();

            int releasedCount = 0;
            barrelEnemy.OnCanBeReleased += (sender, args) => { releasedCount++; };

            CircularTelegraphIndicator warning = PlayModeTestHarness.GetPrivateField<CircularTelegraphIndicator>(
                typeof(BarrelEnemyExplosionController),
                explosionController,
                "_warning");

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => warning.gameObject.activeInHierarchy,
                1.5f,
                "Expected BarrelEnemy contact to enter Priming and activate CircularTelegraphIndicator.");

            barrelEnemy.EnemyAnimator.Call_OnAttackAnimationStart();
            barrelEnemy.EnemyAnimator.Call_OnAttackHitFrame();
            barrelEnemy.EnemyAnimator.Call_OnAttackAnimationEnd();

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => releasedCount == 1,
                2f,
                "Expected BarrelEnemy detonation to fire OnCanBeReleased cleanly without audio hang.");

            Assert.IsTrue(explosionController.HasDetonated);
            Assert.AreEqual(300f - barrelEnemy.Config.Damage, playerHealth.CurrentHealth, 0.01f);
            Assert.AreEqual(1, explosionPool.PlayCount);
            Assert.AreEqual(1, expSpawner.CallsCount);
        }

        [UnityTest]
        public IEnumerator SawBlade_PhysicsTriggerEnterAndStay_EnforcesCooldownAndAppliesKnockback()
        {
            GameObject sawPrefab = PlayModeTestHarness.LoadEditorAsset<GameObject>(SAW_PREFAB_PATH);
            SawSkillUpgradeableConfigSO assetConfig =
                PlayModeTestHarness.LoadEditorAsset<SawSkillUpgradeableConfigSO>(SAW_CONFIG_ASSET_PATH);
            SawSkillUpgradeableConfigSO runtimeConfig = _harness.TrackObject(UnityEngine.Object.Instantiate(assetConfig));
            runtimeConfig.ResetRuntimeState();
            PlayModeTestHarness.SetPrivateField(
                typeof(SawSkillUpgradeableConfigSO),
                runtimeConfig,
                "<AttackCooldown>k__BackingField",
                0.22f);

            Container sawContainer = _harness.BuildContainer(builder =>
            {
                builder.AddSingleton(new FakePlayerManager(null, null), typeof(IPlayerManager));
            });

            GameObject sawGo = _harness.InstantiateTrackedPrefab(sawPrefab, Vector3.zero, Quaternion.identity, sawContainer);
            SawBlade saw = sawGo.GetComponent<SawBlade>();
            Assert.IsNotNull(saw);
            saw.Initialize(runtimeConfig);

            GameObject enemyGo = PlayModeTestHarness.CreateBoxColliderBody(
                _harness,
                "SawEnemyTarget",
                LayerMaskConstants.ENEMY,
                new Vector3(0f, 0f, 5f),
                Vector3.one * 1.5f,
                isTrigger: false,
                addKinematicRigidbody: true);
            DamageableKnockableProbe probe = enemyGo.AddComponent<DamageableKnockableProbe>();

            yield return PlayModeTestHarness.WaitForFixedUpdates(2);
            Assert.AreEqual(0, probe.DamageCalls);

            enemyGo.transform.position = Vector3.zero;
            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => probe.DamageCalls == 1,
                1.5f,
                "Expected SawBlade OnTriggerEnter to deal initial hit.");

            Assert.AreEqual(runtimeConfig.Damage.Value, probe.TotalDamage, 0.01f);
            Assert.AreEqual(1, probe.KnockbackCalls);

            yield return new WaitForSeconds(0.08f);
            yield return PlayModeTestHarness.WaitForFixedUpdates(1);
            Assert.AreEqual(1, probe.DamageCalls, "OnTriggerStay within AttackCooldown must not deal extra damage.");

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => probe.DamageCalls >= 2,
                1.5f,
                "Expected SawBlade OnTriggerStay after AttackCooldown to deal second hit.");
        }

        [UnityTest]
        public IEnumerator Landmine_EnemyTriggerOverlapSphere_DamagesInRadiusIgnoresOutsideAndFinishesVfx()
        {
            GameObject landminePrefab = PlayModeTestHarness.LoadEditorAsset<GameObject>(LANDMINE_PREFAB_PATH);
            LandmineSkillUpgradeableConfigSO assetConfig =
                PlayModeTestHarness.LoadEditorAsset<LandmineSkillUpgradeableConfigSO>(LANDMINE_CONFIG_ASSET_PATH);
            LandmineSkillUpgradeableConfigSO runtimeConfig = _harness.TrackObject(UnityEngine.Object.Instantiate(assetConfig));
            runtimeConfig.ResetRuntimeState();
            runtimeConfig.ExplosionRadius.RestoreRuntimeState(3f, true);

            GameObject mineGo = _harness.InstantiateTrackedPrefab(landminePrefab, Vector3.zero, Quaternion.identity);
            Landmine landmine = mineGo.GetComponent<Landmine>();
            Assert.IsNotNull(landmine);
            landmine.Initialize(runtimeConfig);

            GameObject insideNear = CreateDamageableEnemyProbe("InsideTriggerEnemy", new Vector3(0f, 0f, 5f));
            DamageableKnockableProbe nearProbe = insideNear.GetComponent<DamageableKnockableProbe>();

            GameObject insideEdge = CreateDamageableEnemyProbe("InsideRadiusEnemy", new Vector3(2.2f, 0f, 0f));
            DamageableKnockableProbe edgeProbe = insideEdge.GetComponent<DamageableKnockableProbe>();

            GameObject outside = CreateDamageableEnemyProbe("OutsideRadiusEnemy", new Vector3(6.5f, 0f, 0f));
            DamageableKnockableProbe outsideProbe = outside.GetComponent<DamageableKnockableProbe>();

            yield return PlayModeTestHarness.WaitForFixedUpdates(2);

            insideNear.transform.position = Vector3.zero;
            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => nearProbe.DamageCalls == 1 && edgeProbe.DamageCalls == 1,
                1.5f,
                "Expected Landmine OverlapSphere explosion to damage all enemies inside ExplosionRadius.");

            Assert.AreEqual(runtimeConfig.Damage.Value, nearProbe.TotalDamage, 0.01f);
            Assert.AreEqual(runtimeConfig.Damage.Value, edgeProbe.TotalDamage, 0.01f);
            Assert.AreEqual(0, outsideProbe.DamageCalls);

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => mineGo == null,
                4f,
                "Expected Landmine explosion VFX completion to destroy Landmine GameObject.");
        }

        [UnityTest]
        public IEnumerator MinigunSkill_LateUpdateVolleyAndTracerFlight_HitsPointBlankImmediatelyAndDelaysDistantHitWithPiercing()
        {
            CreateFlatGround();

            GameObject turretPrefab = PlayModeTestHarness.LoadEditorAsset<GameObject>(MINIGUN_TURRET_PREFAB_PATH);
            SkillInfoSO skillInfo = PlayModeTestHarness.LoadEditorAsset<SkillInfoSO>(MINIGUN_INFO_ASSET_PATH);
            MinigunSkillUpgradeableConfigSO assetConfig =
                PlayModeTestHarness.LoadEditorAsset<MinigunSkillUpgradeableConfigSO>(MINIGUN_CONFIG_ASSET_PATH);

            MinigunSkillUpgradeableConfigSO runtimeConfig = _harness.TrackObject(UnityEngine.Object.Instantiate(assetConfig));
            TurretConfigSO runtimeTurretConfig = _harness.TrackObject(UnityEngine.Object.Instantiate(assetConfig.TurretConfig));
            PlayModeTestHarness.SetPrivateField(
                typeof(TurretConfigSO),
                runtimeTurretConfig,
                "<RotationAngle>k__BackingField",
                0f);
            PlayModeTestHarness.SetPrivateField(
                typeof(MinigunSkillUpgradeableConfigSO),
                runtimeConfig,
                "_turretConfig",
                runtimeTurretConfig);
            runtimeConfig.ResetRuntimeState();
            runtimeConfig.NumberOfTurrets.RestoreRuntimeState(1, false);
            runtimeConfig.Piercing.RestoreRuntimeState(1, false);
            runtimeConfig.Range.RestoreRuntimeState(20f, false);
            runtimeConfig.Damage.RestoreRuntimeState(12, false);
            runtimeConfig.ShotDelay.RestoreRuntimeState(10f, false);
            runtimeConfig.BeamWidth.RestoreRuntimeState(1.2f, false);

            GameObject playerOwner = _harness.TrackGameObject("MinigunPlayerOwner", false);
            Health playerHealth = playerOwner.AddComponent<Health>();
            playerHealth.MaxHealth = 100f;
            playerOwner.SetActive(true);
            FakePlayerManager playerManager = new FakePlayerManager(playerOwner, playerHealth);

            Container skillContainer = _harness.BuildContainer(builder =>
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
            });

            GameObject skillGo = _harness.TrackGameObject("MinigunSkillRig", false);
            skillGo.transform.position = Vector3.zero;
            skillGo.transform.rotation = Quaternion.identity;

            MinigunTurret[] turrets = new MinigunTurret[4];
            for (int i = 0; i < turrets.Length; i++)
            {
                GameObject turretGo = _harness.InstantiateTrackedPrefab(
                    turretPrefab,
                    new Vector3(0f, 0.5f, 0f),
                    Quaternion.identity,
                    skillContainer,
                    skillGo.transform);
                MinigunTurret turret = turretGo.GetComponent<MinigunTurret>();
                Assert.IsNotNull(turret);
                PlayModeTestHarness.SetPrivateField(typeof(MinigunTurret), turret, "_tracerSpeed", 35f);
                turrets[i] = turret;
            }

            MinigunSkill skill = skillGo.AddComponent<MinigunSkill>();
            PlayModeTestHarness.SetPrivateField(typeof(MinigunSkill), skill, "<SkillInfo>k__BackingField", skillInfo);
            PlayModeTestHarness.SetPrivateField(typeof(MinigunSkill), skill, "<_config>k__BackingField", runtimeConfig);
            PlayModeTestHarness.SetPrivateField(typeof(MinigunSkill), skill, "_turrets", turrets);
            AttributeInjector.Inject(skill, skillContainer);

            GameObject pointBlankEnemy = CreateDamageableEnemyProbe(
                "PointBlankEnemy",
                new Vector3(0f, 0.5f, 50f),
                size: new Vector3(0.4f, 1.5f, 0.12f),
                addHealth: true);
            DamageableKnockableProbe pointBlankProbe = pointBlankEnemy.GetComponent<DamageableKnockableProbe>();

            GameObject distantEnemy = CreateDamageableEnemyProbe(
                "DistantEnemy",
                new Vector3(0f, 0.5f, 65f),
                size: new Vector3(1.2f, 1.5f, 0.5f),
                addHealth: true);
            DamageableKnockableProbe distantProbe = distantEnemy.GetComponent<DamageableKnockableProbe>();

            Transform gunTip = PlayModeTestHarness.GetPrivateField<Transform>(
                typeof(MinigunTurret),
                turrets[0],
                "_gunTip");
            Vector3 tipOrigin = gunTip.position;
            Vector3 fireDir = gunTip.forward;
            fireDir.y = 0f;
            fireDir.Normalize();

            pointBlankEnemy.transform.position = tipOrigin + fireDir * 0.12f;
            distantEnemy.transform.position = tipOrigin + fireDir * 14f;

            yield return PlayModeTestHarness.WaitForFixedUpdates(2);

            skillGo.SetActive(true);
            skill.Initialize();

            yield return null;

            Assert.AreEqual(1, pointBlankProbe.DamageCalls,
                "Point-blank target (< MIN_TRACER_SPAWN_DISTANCE) must take immediate damage on the shot frame.");
            Assert.AreEqual(0, distantProbe.DamageCalls,
                "Distant target (>= MIN_TRACER_SPAWN_DISTANCE) must wait for tracer flight delay before taking damage.");

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => distantProbe.DamageCalls == 1,
                1.5f,
                "Expected LateUpdate UpdatePendingHits to apply pierced distant hit after tracer travel time.");

            Assert.AreEqual(12f, distantProbe.TotalDamage, 0.01f);
        }

        private GameObject CreatePlayerTargetWithHealth(
            string name,
            Vector3 position,
            Vector3 size,
            float maxHealth,
            out Health playerHealth)
        {
            GameObject go = _harness.TrackGameObject(name, false);
            int playerLayer = LayerMask.NameToLayer(LayerMaskConstants.PLAYER);
            Assert.That(playerLayer, Is.GreaterThanOrEqualTo(0));
            go.layer = playerLayer;
            go.transform.position = position;

            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = false;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            playerHealth = go.AddComponent<Health>();
            playerHealth.MaxHealth = maxHealth;

            DamageableHealthProbe probe = go.AddComponent<DamageableHealthProbe>();
            probe.BindHealth(playerHealth);

            go.SetActive(true);
            return go;
        }

        private GameObject CreateDamageableEnemyProbe(
            string name,
            Vector3 position,
            Vector3? size = null,
            bool addHealth = false)
        {
            GameObject go = PlayModeTestHarness.CreateBoxColliderBody(
                _harness,
                name,
                LayerMaskConstants.ENEMY,
                position,
                size ?? Vector3.one,
                isTrigger: false,
                addKinematicRigidbody: true);
            DamageableKnockableProbe probe = go.AddComponent<DamageableKnockableProbe>();
            if (addHealth)
            {
                Health health = go.AddComponent<Health>();
                health.MaxHealth = 500f;
            }

            return go;
        }

        private void CreateFlatGround()
        {
            GameObject ground = _harness.TrackGameObject("FlatGround");
            ground.layer = LayerMask.NameToLayer(LayerMaskConstants.GROUND);
            ground.transform.position = new Vector3(0f, -0.5f, 10f);
            BoxCollider groundCollider = ground.AddComponent<BoxCollider>();
            groundCollider.size = new Vector3(60f, 1f, 60f);
        }

        private Container BuildEnemyContainer(
            IPlayerManager playerManager,
            IExplosionVfxPool explosionPool,
            IInWorldSpaceSpawner<ExpParticleSpawner, float> expSpawner,
            FakeGridManager gridManager = null)
        {
            DropAnimationConfiguration dropConfig =
                PlayModeTestHarness.LoadEditorAsset<DropAnimationConfiguration>(DROP_CONFIG_ASSET_PATH);

            return _harness.BuildContainer(builder =>
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
                builder.AddSingleton(explosionPool, typeof(IExplosionVfxPool));
                builder.AddSingleton(expSpawner, typeof(IInWorldSpaceSpawner<ExpParticleSpawner, float>));
                builder.AddSingleton(gridManager ?? new FakeGridManager(), typeof(IGridManager));
                builder.AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState));
                builder.AddSingleton(
                    new FakeDamageNumbersSpawner(),
                    typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>));
                builder.AddSingleton(dropConfig);
                builder.AddSingleton(new FakeDropNotifier(), typeof(ICollectibleDropNotifier));
                builder.AddSingleton(new FakeDropSuppressor(), typeof(ICollectibleDropSuppressor));
            });
        }

        public sealed class DamageableHealthProbe : MonoBehaviour, IDamageable
        {
            private Health _health;

            public void BindHealth(Health health)
            {
                _health = health;
            }

            public void TakeDamage(float damage)
            {
                if (_health != null)
                {
                    _health.DecreaseHealth(damage);
                }
            }

            public void TakeFullHpDamage()
            {
                if (_health != null)
                {
                    _health.DecreaseHealth(_health.MaxHealth);
                }
            }
        }

        public sealed class DamageableKnockableProbe : MonoBehaviour, IDamageable, IKnockable
        {
            public int DamageCalls { get; private set; }
            public float TotalDamage { get; private set; }
            public int KnockbackCalls { get; private set; }

            public void TakeDamage(float damage)
            {
                DamageCalls++;
                TotalDamage += damage;
            }

            public void TakeFullHpDamage()
            {
                DamageCalls++;
                TotalDamage += float.MaxValue;
            }

            public void ApplyKnockBack(Vector3 dir, float range, float timeToArriveAtLocation)
            {
                KnockbackCalls++;
            }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            public FakePlayerManager(GameObject gameObject, IHealth health)
            {
                GameObject = gameObject;
                Health = health;
            }

            public GameObject GameObject { get; }
            public IHealth Health { get; }
            public IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public ICarController CarController { get { return null; } }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get { return null; } }
            public ISkillsRegistry SkillsRegistry { get { return null; } }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }
        }

        private sealed class FakeGridManager : IGridManager
        {
            public Cell DestinationCell { get; private set; }
            public NavigationGrid GridPlayerChunk { get; }
            public NavigationGrid WorldGrid { get; }

            public FakeGridManager()
            {
                GridConfiguration config = new GridConfiguration(4, 4, 2f);
                WorldGrid = new NavigationGrid(config);
                GridPlayerChunk = WorldGrid;
                DestinationCell = WorldGrid.Cells[1, 1];
                for (int x = 0; x < WorldGrid.Width; x++)
                {
                    for (int y = 0; y < WorldGrid.Height; y++)
                    {
                        WorldGrid.Cells[x, y].Cost = 1;
                    }
                }
            }

            public void SetDestinationCell(Cell destinationCell)
            {
                DestinationCell = destinationCell;
            }
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

        private sealed class FakeExplosionVfxPool : IExplosionVfxPool
        {
            public int PlayCount { get; private set; }

            public void Play(Vector3 position, float radius)
            {
                PlayCount++;
            }
        }

        private sealed class FakeExpSpawner : IInWorldSpaceSpawner<ExpParticleSpawner, float>
        {
            public int CallsCount { get; private set; }
            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public uint CurrentlySpawnedObjectsCount { get { return 0u; } }

            public void Spawn(Vector3 pos, float specificConfig, int count = 1)
            {
                CallsCount++;
            }
        }

        private sealed class FakeDamageNumbersSpawner : IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>
        {
            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public uint CurrentlySpawnedObjectsCount { get { return 0u; } }

            public void Spawn(Vector3 pos, DamageNubmersSpawnerConfig specificConfig, int count = 1) { }
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
    }
}
