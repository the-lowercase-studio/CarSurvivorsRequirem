using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.ScriptableObjects;
using Assets.ScriptableObjects.Skills;
using Assets.ScriptableObjects.Skills.PlayerSkills.LandmineSkill;
using Assets.ScriptableObjects.Skills.PlayerSkills.MinigunSkill;
using Assets.ScriptableObjects.Skills.PlayerSkills.SawSkill;
using Assets.Scripts.Audio;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.Common.Types;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.PlayerSkills.LandmineTrap;
using Assets.Scripts.Skills.PlayerSkills.Lasergun;
using Assets.Scripts.Skills.PlayerSkills.Minigun;
using Assets.Scripts.Skills.PlayerSkills.Saw;
using Assets.Scripts.Stats;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class SkillsTierCarryoverTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();

        [TearDown]
        public void TearDown()
        {
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
        }

        [Test]
        public void RunSessionService_AndSkillsRegistry_CarryOverSkillsStatsAndCanBeUpgradedToTier2WithoutReset()
        {
            GameLoopProgressionConfigSO progressionConfig = CreateProgressionConfig();
            RunSessionService runSessionService = new RunSessionService(progressionConfig);

            SkillRig tier1Rig = BuildSkillRig(runSessionService, out Container tier1Container);
            _createdContainers.Add(tier1Container);

            InvokeMethod(tier1Rig.Registry, "Start");

            Assert.AreEqual(1, tier1Rig.Registry.InitializedSkillsCount);
            Assert.IsTrue(tier1Rig.Saw.IsInitialized());

            tier1Rig.Registry.InitializeSkill(tier1Rig.Landmine);
            tier1Rig.Registry.InitializeSkill(tier1Rig.Lasergun);

            tier1Rig.SawConfig.Damage.Upgrade(7f);
            tier1Rig.LandmineConfig.SpawnCooldown.Upgrade(4.0f);
            tier1Rig.LandmineConfig.SpawnCooldown.Upgrade(3.5f);
            tier1Rig.LasergunConfig.DelayBetweenShoots.Upgrade(0.5f);
            tier1Rig.LasergunConfig.Range.Upgrade(6f);
            tier1Rig.LasergunConfig.Damage.Upgrade(8f);
            tier1Rig.LasergunConfig.NumberOfTargets.Upgrade(1f);

            double expectedLandmineCooldown = tier1Rig.LandmineConfig.SpawnCooldown.CurrentValue;
            bool expectedLandmineCanUpgrade = tier1Rig.LandmineConfig.SpawnCooldown.CanBeUpgraded;
            double expectedLaserDelay = tier1Rig.LasergunConfig.DelayBetweenShoots.CurrentValue;
            double expectedLaserRange = tier1Rig.LasergunConfig.Range.CurrentValue;
            int expectedLaserDamage = tier1Rig.LasergunConfig.Damage.Value;
            int expectedSawDamage = tier1Rig.SawConfig.Damage.Value;

            Assert.AreEqual(1d, expectedLandmineCooldown, 0.0001d);
            Assert.IsFalse(expectedLandmineCanUpgrade);
            Assert.AreEqual(1.5d, expectedLaserDelay, 0.0001d);
            Assert.AreEqual(14d, expectedLaserRange, 0.0001d);

            tier1Rig.RegistryGameObject.SetActive(false);

            FakeLevelController levelController = new FakeLevelController(new LevelData(7, 42f, 150f));
            FakePlayerManager playerManager = new FakePlayerManager(tier1Rig.Registry, levelController, new FakeHealth(true));
            runSessionService.CaptureSnapshotAndAdvanceTier(playerManager, 180u);

            Assert.AreEqual(1, runSessionService.CurrentTierIndex);

            SkillRig tier2Rig = BuildSkillRig(runSessionService, out Container tier2Container);
            _createdContainers.Add(tier2Container);

            InvokeMethod(tier2Rig.Registry, "Start");

            Assert.AreEqual(3, tier2Rig.Registry.InitializedSkillsCount);
            Assert.AreEqual(1, tier2Rig.Registry.UninitializedSkillsCount);
            Assert.IsTrue(tier2Rig.Saw.IsInitialized());
            Assert.IsTrue(tier2Rig.Landmine.IsInitialized());
            Assert.IsTrue(tier2Rig.Lasergun.IsInitialized());
            Assert.IsFalse(tier2Rig.Minigun.IsInitialized());

            Assert.AreEqual(expectedSawDamage, tier2Rig.SawConfig.Damage.Value);
            Assert.AreEqual(expectedLandmineCooldown, tier2Rig.LandmineConfig.SpawnCooldown.CurrentValue, 0.0001d);
            Assert.IsFalse(tier2Rig.LandmineConfig.SpawnCooldown.CanBeUpgraded);
            Assert.AreEqual(expectedLaserDelay, tier2Rig.LasergunConfig.DelayBetweenShoots.CurrentValue, 0.0001d);
            Assert.IsTrue(tier2Rig.LasergunConfig.DelayBetweenShoots.CanBeUpgraded);
            Assert.AreEqual(expectedLaserRange, tier2Rig.LasergunConfig.Range.CurrentValue, 0.0001d);
            Assert.AreEqual(expectedLaserDamage, tier2Rig.LasergunConfig.Damage.Value);
            Assert.AreEqual(2, tier2Rig.LasergunConfig.NumberOfTargets.Value);

            Assert.AreEqual((float)expectedLaserRange, tier2Rig.LasergunTurretConfig.Range, 0.0001f);
            Assert.AreEqual((float)expectedLaserRange, tier2Rig.LasergunProjectileConfig.Range, 0.0001f);
            Assert.AreEqual(expectedLaserDamage, tier2Rig.LasergunProjectileConfig.Damage);

            tier2Rig.LasergunConfig.DelayBetweenShoots.Upgrade(0.4f);
            Assert.AreEqual(1.1f, tier2Rig.LasergunConfig.DelayBetweenShoots.Value, 0.0001f);
        }

        [Test]
        public void LandmineSkill_SpawnCooldownUpgrade_ImmediatelyAcceleratesSpawnCadence()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.GreaterOrEqual(groundLayer, 0, "Ground layer must exist in TagManager.");

            GameObject ground = new GameObject("TestGround");
            ground.layer = groundLayer;
            ground.transform.position = new Vector3(0f, -1f, 0f);
            BoxCollider groundCollider = ground.AddComponent<BoxCollider>();
            groundCollider.size = new Vector3(10f, 1f, 10f);
            _createdObjects.Add(ground);
            Physics.SyncTransforms();

            GameObject landminesParent = new GameObject("LandminesParent");
            _createdObjects.Add(landminesParent);

            Landmine landminePrefab = CreateLandminePrefab();
            LandmineSkillUpgradeableConfigSO config = CreateLandmineConfig();
            config.ResetRuntimeState();

            GameObject skillGo = new GameObject("LandmineSkill");
            skillGo.SetActive(false);
            skillGo.transform.position = new Vector3(0f, 1f, 0f);
            _createdObjects.Add(skillGo);

            LandmineSkill skill = skillGo.AddComponent<LandmineSkill>();
            SkillInfoSO info = CreateSkillInfo("Landmine");
            SetProperty(typeof(LandmineSkill), skill, nameof(LandmineSkill.SkillInfo), info);
            SetProperty(typeof(LandmineSkill), skill, "_config", config);
            SetPrivateField(typeof(LandmineSkill), skill, "_landminePrefab", landminePrefab);
            SetPrivateField(typeof(LandmineSkill), skill, "_landminesParent", landminesParent.transform);

            skill.Initialize();

            Assert.IsTrue(skill.AdvanceSpawnTimer(0.016f));
            Assert.AreEqual(1, landminesParent.transform.childCount);

            Assert.IsFalse(skill.AdvanceSpawnTimer(2.0f));
            Assert.AreEqual(1, landminesParent.transform.childCount);

            config.SpawnCooldown.Upgrade(4.0f);
            config.SpawnCooldown.Upgrade(3.0f);
            Assert.AreEqual(1f, config.SpawnCooldown.Value, 0.0001f);

            Assert.IsTrue(skill.AdvanceSpawnTimer(0.016f));
            Assert.AreEqual(2, landminesParent.transform.childCount);

            Assert.IsFalse(skill.AdvanceSpawnTimer(0.5f));
            Assert.IsTrue(skill.AdvanceSpawnTimer(0.51f));
            Assert.AreEqual(3, landminesParent.transform.childCount);
        }

        [Test]
        public void LasergunSkill_VfxPlayerAndTurret_ReflectCooldownAndRangeUpgrades()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist in TagManager.");

            TurretConfigSO turretConfig = CreateSO<TurretConfigSO>();
            SetPrivateField(typeof(TurretConfigSO), turretConfig, "_startRange", 8f);
            SetProperty(typeof(TurretConfigSO), turretConfig, nameof(TurretConfigSO.RotationAngle), 360f);
            SetProperty(typeof(TurretConfigSO), turretConfig, nameof(TurretConfigSO.RotationDuration), 1f);
            SetProperty(typeof(TurretConfigSO), turretConfig, nameof(TurretConfigSO.RotationSpeed), 360f);
            SetProperty(typeof(TurretConfigSO), turretConfig, nameof(TurretConfigSO.SearchForTargetInterval), 0.1f);
            turretConfig.Range = 8f;

            ProjectileConfigSO projectileConfig = CreateSO<ProjectileConfigSO>();
            LasergunSkillSO config = CreateLasergunConfig(turretConfig, projectileConfig);
            config.ResetRuntimeState();

            GameObject turretGo = new GameObject("LasergunTurret");
            turretGo.SetActive(false);
            _createdObjects.Add(turretGo);

            GameObject gunTip = new GameObject("GunTip");
            gunTip.transform.SetParent(turretGo.transform, false);
            LineRenderer lineRenderer = gunTip.AddComponent<LineRenderer>();

            GameObject vfxGo = new GameObject("Vfx_LaserCumulating");
            vfxGo.transform.SetParent(turretGo.transform, false);
            ParticleSystem ps = vfxGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.7f;
            main.playOnAwake = false;
            VFXPlayer vfxPlayer = vfxGo.AddComponent<VFXPlayer>();

            LasergunTurret turret = turretGo.AddComponent<LasergunTurret>();
            SetPrivateField(typeof(Turret<TurretConfigSO>), turret, "_gunTip", gunTip.transform);
            SetPrivateField(typeof(LasergunTurret), turret, "_laserLineRenderer", lineRenderer);
            SetPrivateField(typeof(LasergunTurret), turret, "_laserCumulatingVFX", vfxPlayer);

            GameObject skillGo = new GameObject("LasergunSkill");
            skillGo.SetActive(false);
            _createdObjects.Add(skillGo);

            LasergunSkill skill = skillGo.AddComponent<LasergunSkill>();
            SetProperty(typeof(LasergunSkill), skill, nameof(LasergunSkill.SkillInfo), CreateSkillInfo("Lasergun"));
            SetProperty(typeof(LasergunSkill), skill, "_config", config);
            SetPrivateField(typeof(LasergunSkill), skill, "_turrets", new[] { turret });

            GameObject enemyGo = new GameObject("EnemyTarget");
            enemyGo.layer = enemyLayer;
            enemyGo.transform.position = new Vector3(0f, 0f, 12f);
            BoxCollider enemyCollider = enemyGo.AddComponent<BoxCollider>();
            TestDamageableTarget damageable = enemyGo.AddComponent<TestDamageableTarget>();
            _createdObjects.Add(enemyGo);
            Physics.SyncTransforms();

            InvokeMethod(vfxPlayer, "Awake");
            InvokeMethod(turret, "Awake");
            skill.Initialize();
            InvokeMethod(turret, "OnEnable");

            InvokeMethod(turret, "AssignNewTargets");
            Assert.IsNull(GetPrivateField<Collider>(typeof(LasergunTurret), turret, "_primaryTarget"));

            config.Range.Upgrade(6f);
            Assert.AreEqual(14f, turretConfig.Range, 0.0001f);

            InvokeMethod(turret, "AssignNewTargets");
            Assert.AreSame(enemyCollider, GetPrivateField<Collider>(typeof(LasergunTurret), turret, "_primaryTarget"));
            SetPrivateField(typeof(LasergunTurret), turret, "_isLookingAtTarget", true);

            Assert.IsTrue(skill.AdvanceFireTimer(0.016f));
            Assert.AreEqual(0.7f, vfxPlayer.LastScheduledDuration, 0.001f);

            InvokeMethod(vfxPlayer, "OnAllParticlesFinished");
            Assert.AreEqual(5f, damageable.TotalDamageTaken, 0.001f);

            SetPrivateField(typeof(LasergunTurret), turret, "_isShowingLaser", false);
            config.DelayBetweenShoots.Upgrade(1.6f);
            Assert.AreEqual(0.4f, config.DelayBetweenShoots.Value, 0.0001f);

            Assert.IsFalse(skill.AdvanceFireTimer(0.25f));
            Assert.IsTrue(skill.AdvanceFireTimer(0.2f));
            Assert.AreEqual(0.14f, vfxPlayer.LastScheduledDuration, 0.001f);

            // Re-invoking Shoot with a different speed while charging must be ignored (no VFX restart).
            turret.Shoot(1f);
            Assert.IsTrue(GetPrivateField<bool>(typeof(LasergunTurret), turret, "_isChargingLaser"));
            Assert.AreEqual(0.14f, vfxPlayer.LastScheduledDuration, 0.001f);

            InvokeMethod(vfxPlayer, "OnAllParticlesFinished");
            Assert.AreEqual(10f, damageable.TotalDamageTaken, 0.001f);
        }

        [Test]
        public void LandmineSkill_RestoredTier2Cooldown_AppliesImmediatelyAndAcceptsFurtherUpgrades()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.GreaterOrEqual(groundLayer, 0, "Ground layer must exist in TagManager.");

            GameObject ground = new GameObject("TestGroundTier2");
            ground.layer = groundLayer;
            ground.transform.position = new Vector3(0f, -1f, 0f);
            BoxCollider groundCollider = ground.AddComponent<BoxCollider>();
            groundCollider.size = new Vector3(10f, 1f, 10f);
            _createdObjects.Add(ground);
            Physics.SyncTransforms();

            GameObject landminesParent = new GameObject("LandminesParentTier2");
            _createdObjects.Add(landminesParent);

            Landmine landminePrefab = CreateLandminePrefab();
            LandmineSkillUpgradeableConfigSO config = CreateLandmineConfig();
            config.ResetRuntimeState();
            config.SpawnCooldown.RestoreRuntimeState(3.0d, true);

            GameObject skillGo = new GameObject("LandmineSkillTier2");
            skillGo.SetActive(false);
            skillGo.transform.position = new Vector3(0f, 1f, 0f);
            _createdObjects.Add(skillGo);

            LandmineSkill skill = skillGo.AddComponent<LandmineSkill>();
            SetProperty(typeof(LandmineSkill), skill, nameof(LandmineSkill.SkillInfo), CreateSkillInfo("Landmine"));
            SetProperty(typeof(LandmineSkill), skill, "_config", config);
            SetPrivateField(typeof(LandmineSkill), skill, "_landminePrefab", landminePrefab);
            SetPrivateField(typeof(LandmineSkill), skill, "_landminesParent", landminesParent.transform);

            skill.Initialize();

            Assert.IsTrue(skill.AdvanceSpawnTimer(0.016f));
            Assert.AreEqual(1, landminesParent.transform.childCount);

            Assert.IsFalse(skill.AdvanceSpawnTimer(2.5f));
            Assert.IsTrue(skill.AdvanceSpawnTimer(0.6f));
            Assert.AreEqual(2, landminesParent.transform.childCount);

            config.SpawnCooldown.Upgrade(2.0f);
            Assert.AreEqual(1f, config.SpawnCooldown.Value, 0.0001f);

            Assert.IsFalse(skill.AdvanceSpawnTimer(0.5f));
            Assert.IsTrue(skill.AdvanceSpawnTimer(0.51f));
            Assert.AreEqual(3, landminesParent.transform.childCount);
        }

        [Test]
        public void LasergunTurret_FiresWithoutVfxOrAudio_AndWithoutDeadlockOnEmptyVfxPlayer()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist in TagManager.");

            TurretConfigSO turretConfig = CreateSO<TurretConfigSO>();
            ProjectileConfigSO projectileConfig = CreateSO<ProjectileConfigSO>();
            projectileConfig.Damage = 7;
            turretConfig.ProjectileStatsSO = projectileConfig;
            turretConfig.Range = 15f;

            GameObject enemyGo = new GameObject("EnemyForOptionalVfxTest");
            enemyGo.layer = enemyLayer;
            enemyGo.transform.position = new Vector3(0f, 0f, 5f);
            enemyGo.AddComponent<BoxCollider>();
            TestDamageableTarget damageable = enemyGo.AddComponent<TestDamageableTarget>();
            _createdObjects.Add(enemyGo);
            Physics.SyncTransforms();

            // Case 1: Unassigned VFXPlayer and unassigned AudioClipPlayer.
            GameObject turretGo = new GameObject("LasergunTurretNoVfx");
            turretGo.SetActive(false);
            _createdObjects.Add(turretGo);

            GameObject gunTip = new GameObject("GunTip");
            gunTip.transform.SetParent(turretGo.transform, false);
            LineRenderer lineRenderer = gunTip.AddComponent<LineRenderer>();

            LasergunTurret turret = turretGo.AddComponent<LasergunTurret>();
            SetPrivateField(typeof(Turret<TurretConfigSO>), turret, "_gunTip", gunTip.transform);
            SetPrivateField(typeof(LasergunTurret), turret, "_laserLineRenderer", lineRenderer);
            SetPrivateField(typeof(LasergunTurret), turret, "_laserCumulatingVFX", null);

            InvokeMethod(turret, "Awake");
            turret.Initialize(turretConfig);
            InvokeMethod(turret, "AssignNewTargets");
            SetPrivateField(typeof(LasergunTurret), turret, "_isLookingAtTarget", true);

            turret.Shoot(1f);
            Assert.AreEqual(7f, damageable.TotalDamageTaken, 0.001f);
            Assert.IsFalse(GetPrivateField<bool>(typeof(LasergunTurret), turret, "_isChargingLaser"));

            // Case 2: Assigned VFXPlayer with zero ParticleSystems must complete synchronously and never deadlock _isChargingLaser.
            GameObject emptyVfxGo = new GameObject("EmptyVfxPlayer");
            emptyVfxGo.transform.SetParent(turretGo.transform, false);
            VFXPlayer emptyVfxPlayer = emptyVfxGo.AddComponent<VFXPlayer>();
            InvokeMethod(emptyVfxPlayer, "Awake");

            SetPrivateField(typeof(LasergunTurret), turret, "_isShowingLaser", false);
            SetPrivateField(typeof(LasergunTurret), turret, "_laserCumulatingVFX", emptyVfxPlayer);
            InvokeMethod(turret, "OnEnable");

            turret.Shoot(1f);
            Assert.AreEqual(14f, damageable.TotalDamageTaken, 0.001f);
            Assert.IsFalse(GetPrivateField<bool>(typeof(LasergunTurret), turret, "_isChargingLaser"));
        }

        [Test]
        public void VFXPlayer_CalculateEffectiveDuration_ScalesBySimulationSpeedAndHandlesInvalidSpeed()
        {
            Assert.AreEqual(0.14f, VFXPlayer.CalculateEffectiveDuration(0.7f, 5f), 0.0001f);
            Assert.AreEqual(70f, VFXPlayer.CalculateEffectiveDuration(0.7f, 0.0001f), 0.0001f);
            Assert.AreEqual(0.7f, VFXPlayer.CalculateEffectiveDuration(0.7f, 0f), 0.0001f);
            Assert.AreEqual(0.7f, VFXPlayer.CalculateEffectiveDuration(0.7f, -2f), 0.0001f);
            Assert.AreEqual(0.7f, VFXPlayer.CalculateEffectiveDuration(0.7f, float.NaN), 0.0001f);
            Assert.AreEqual(0.7f, VFXPlayer.CalculateEffectiveDuration(0.7f, float.PositiveInfinity), 0.0001f);
            Assert.AreEqual(0f, VFXPlayer.CalculateEffectiveDuration(-1f, 2f), 0.0001f);
            Assert.AreEqual(0f, VFXPlayer.CalculateEffectiveDuration(float.NaN, 2f), 0.0001f);
            Assert.AreEqual(0f, VFXPlayer.CalculateEffectiveDuration(float.PositiveInfinity, 2f), 0.0001f);
        }

        private GameLoopProgressionConfigSO CreateProgressionConfig()
        {
            GameLoopProgressionConfigSO config = CreateSO<GameLoopProgressionConfigSO>();
            List<LoopTierConfig> tiers = new List<LoopTierConfig>
            {
                new LoopTierConfig(),
                new LoopTierConfig()
            };
            SetPrivateField(typeof(GameLoopProgressionConfigSO), config, "_tiers", tiers);
            return config;
        }

        private SkillRig BuildSkillRig(IRunSessionService runSessionService, out Container container)
        {
            GameObject registryGo = new GameObject("SkillsRegistry");
            registryGo.SetActive(false);
            _createdObjects.Add(registryGo);

            SkillsRegistry registry = registryGo.AddComponent<SkillsRegistry>();

            GameObject sawGo = new GameObject("Saw");
            sawGo.transform.SetParent(registryGo.transform, false);
            sawGo.SetActive(false);
            SawSkill saw = sawGo.AddComponent<SawSkill>();
            SawSkillUpgradeableConfigSO sawConfig = CreateSawConfig();
            GameObject bladeGo = new GameObject("SawBlade");
            bladeGo.transform.SetParent(sawGo.transform, false);
            bladeGo.SetActive(false);
            SawBlade blade = bladeGo.AddComponent<SawBlade>();
            SetProperty(typeof(SawSkill), saw, nameof(SawSkill.SkillInfo), CreateSkillInfo("Saw"));
            SetProperty(typeof(SawSkill), saw, "_config", sawConfig);
            SetPrivateField(typeof(SawSkill), saw, "_sawBlades", new[] { blade });

            GameObject minigunGo = new GameObject("Minigun");
            minigunGo.transform.SetParent(registryGo.transform, false);
            minigunGo.SetActive(false);
            MinigunSkill minigun = minigunGo.AddComponent<MinigunSkill>();
            MinigunSkillUpgradeableConfigSO minigunConfig = CreateMinigunConfig();
            MinigunTurret minigunTurret = CreateMinigunTurret(minigunGo.transform);
            SetProperty(typeof(MinigunSkill), minigun, nameof(MinigunSkill.SkillInfo), CreateSkillInfo("Minigun"));
            SetProperty(typeof(MinigunSkill), minigun, "_config", minigunConfig);
            SetPrivateField(typeof(MinigunSkill), minigun, "_turrets", new[] { minigunTurret });

            GameObject landmineGo = new GameObject("Landmine");
            landmineGo.transform.SetParent(registryGo.transform, false);
            landmineGo.SetActive(false);
            LandmineSkill landmine = landmineGo.AddComponent<LandmineSkill>();
            LandmineSkillUpgradeableConfigSO landmineConfig = CreateLandmineConfig();
            Landmine landminePrefab = CreateLandminePrefab();
            SetProperty(typeof(LandmineSkill), landmine, nameof(LandmineSkill.SkillInfo), CreateSkillInfo("Landmine"));
            SetProperty(typeof(LandmineSkill), landmine, "_config", landmineConfig);
            SetPrivateField(typeof(LandmineSkill), landmine, "_landminePrefab", landminePrefab);

            GameObject lasergunGo = new GameObject("Lasergun");
            lasergunGo.transform.SetParent(registryGo.transform, false);
            lasergunGo.SetActive(false);
            LasergunSkill lasergun = lasergunGo.AddComponent<LasergunSkill>();
            TurretConfigSO laserTurretConfig = CreateSO<TurretConfigSO>();
            SetPrivateField(typeof(TurretConfigSO), laserTurretConfig, "_startRange", 8f);
            laserTurretConfig.Range = 8f;
            ProjectileConfigSO laserProjConfig = CreateSO<ProjectileConfigSO>();
            LasergunSkillSO lasergunConfig = CreateLasergunConfig(laserTurretConfig, laserProjConfig);
            LasergunTurret lasergunTurret = CreateBasicLasergunTurret(lasergunGo.transform);
            SetProperty(typeof(LasergunSkill), lasergun, nameof(LasergunSkill.SkillInfo), CreateSkillInfo("Lasergun"));
            SetProperty(typeof(LasergunSkill), lasergun, "_config", lasergunConfig);
            SetPrivateField(typeof(LasergunSkill), lasergun, "_turrets", new[] { lasergunTurret });

            FakePlayerManager fakePlayerManager = new FakePlayerManager(registry, new FakeLevelController(new LevelData()), new FakeHealth(true));
            container = new ContainerBuilder()
                .AddSingleton(runSessionService, typeof(IRunSessionService))
                .AddSingleton(fakePlayerManager, typeof(IPlayerManager))
                .Build();

            GameObjectInjector.InjectRecursive(registryGo, container);
            registryGo.SetActive(true);
            InvokeMethod(registry, "Awake");
            InvokeMethod(blade, "Awake");
            InvokeMethod(lasergunTurret, "Awake");

            return new SkillRig
            {
                RegistryGameObject = registryGo,
                Registry = registry,
                Saw = saw,
                SawConfig = sawConfig,
                Minigun = minigun,
                MinigunConfig = minigunConfig,
                Landmine = landmine,
                LandmineConfig = landmineConfig,
                Lasergun = lasergun,
                LasergunConfig = lasergunConfig,
                LasergunTurretConfig = laserTurretConfig,
                LasergunProjectileConfig = laserProjConfig
            };
        }

        private Landmine CreateLandminePrefab()
        {
            GameObject prefabGo = new GameObject("LandminePrefab");
            prefabGo.SetActive(false);
            _createdObjects.Add(prefabGo);

            GameObject visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(prefabGo.transform, false);

            Landmine landmine = prefabGo.AddComponent<Landmine>();
            SetPrivateField(typeof(Landmine), landmine, "_landmineVisual", visualGo);
            return landmine;
        }

        private MinigunTurret CreateMinigunTurret(Transform parent)
        {
            GameObject turretGo = new GameObject("MinigunTurret");
            turretGo.transform.SetParent(parent, false);
            turretGo.SetActive(false);

            GameObject gunTip = new GameObject("GunTip");
            gunTip.transform.SetParent(turretGo.transform, false);
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(turretGo.transform, false);
            GameObject indicatorGo = new GameObject("Indicator");
            indicatorGo.transform.SetParent(turretGo.transform, false);
            var indicator = indicatorGo.AddComponent<Assets.Scripts.Indicators.RectangularTelegraphIndicator>();

            MinigunTurret turret = turretGo.AddComponent<MinigunTurret>();
            SetPrivateField(typeof(MinigunTurret), turret, "_gunTip", gunTip.transform);
            SetPrivateField(typeof(MinigunTurret), turret, "_visual", visual.transform);
            SetPrivateField(typeof(MinigunTurret), turret, "_laneIndicator", indicator);
            return turret;
        }

        private LasergunTurret CreateBasicLasergunTurret(Transform parent)
        {
            GameObject turretGo = new GameObject("LasergunTurret");
            turretGo.transform.SetParent(parent, false);
            turretGo.SetActive(false);

            GameObject gunTip = new GameObject("GunTip");
            gunTip.transform.SetParent(turretGo.transform, false);
            LineRenderer lineRenderer = gunTip.AddComponent<LineRenderer>();

            LasergunTurret turret = turretGo.AddComponent<LasergunTurret>();
            SetPrivateField(typeof(Turret<TurretConfigSO>), turret, "_gunTip", gunTip.transform);
            SetPrivateField(typeof(LasergunTurret), turret, "_laserLineRenderer", lineRenderer);
            return turret;
        }

        private SawSkillUpgradeableConfigSO CreateSawConfig()
        {
            SawSkillUpgradeableConfigSO config = CreateSO<SawSkillUpgradeableConfigSO>();
            SetPrivateField(typeof(SawSkillUpgradeableConfigSO), config, "_knockbackRange", CreateFloatStat(1f, 4f, 0.5f, 0.8f, false, false));
            SetPrivateField(typeof(SawSkillUpgradeableConfigSO), config, "_damage", CreateIntStat(10, 35, 4, 7, true));
            return config;
        }

        private MinigunSkillUpgradeableConfigSO CreateMinigunConfig()
        {
            MinigunSkillUpgradeableConfigSO config = CreateSO<MinigunSkillUpgradeableConfigSO>();
            TurretConfigSO turretConfig = CreateSO<TurretConfigSO>();
            SetProperty(typeof(TurretConfigSO), turretConfig, nameof(TurretConfigSO.RotationAngle), 90f);
            SetProperty(typeof(TurretConfigSO), turretConfig, nameof(TurretConfigSO.RotationDuration), 8f);
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_turretConfig", turretConfig);
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_delayBetweenShootingBullets", CreateFloatStat(1.2f, 0.2f, 0.2f, 0.3f, true, false));
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_range", CreateFloatStat(6f, 25f, 6f, 9f, false, false));
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_numberOfTurrets", CreateIntStat(1, 1, 1, 1, false, true));
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_startBulletSize", CreateFloatStat(0.24f, 0.48f, 0.048f, 0.096f, false, true));
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_startBulletDamage", CreateIntStat(5, 18, 4, 7, true));
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_startBulletMaxPiercing", CreateIntStat(0, 16, 1, 4, false));
            return config;
        }

        private LandmineSkillUpgradeableConfigSO CreateLandmineConfig()
        {
            LandmineSkillUpgradeableConfigSO config = CreateSO<LandmineSkillUpgradeableConfigSO>();
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_spawnCooldown", CreateFloatStat(8f, 1f, 2f, 4f, true, false));
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_explosionRadius", CreateFloatStat(1f, 4f, 1f, 1.2f, false, true));
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_size", CreateFloatStat(1f, 2f, 0.7f, 1.2f, false, true));
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_knockbackRange", CreateFloatStat(1f, 4f, 0.5f, 0.8f, false, true));
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_damage", CreateIntStat(5, 18, 4, 6, true));
            return config;
        }

        private LasergunSkillSO CreateLasergunConfig(TurretConfigSO turretConfig, ProjectileConfigSO projectileConfig)
        {
            LasergunSkillSO config = CreateSO<LasergunSkillSO>();
            SetPrivateField(typeof(LasergunSkillSO), config, "_turretConfig", turretConfig);
            SetPrivateField(typeof(LasergunSkillSO), config, "_projectileConfig", projectileConfig);
            SetPrivateField(typeof(LasergunSkillSO), config, "_delayBetweenShoots", CreateFloatStat(2f, 0.4f, 0.2f, 0.6f, true, false));
            SetPrivateField(typeof(LasergunSkillSO), config, "_numberOfTurrets", CreateIntStat(1, 3, 1, 1, false, true));
            SetPrivateField(typeof(LasergunSkillSO), config, "_numberOfTargets", CreateIntStat(1, 5, 1, 1, false, true));
            SetPrivateField(typeof(LasergunSkillSO), config, "_startRange", CreateFloatStat(8f, 18f, 4f, 7f, false, false));
            SetPrivateField(typeof(LasergunSkillSO), config, "_startDamage", CreateIntStat(5, 25, 5, 8, true));
            return config;
        }

        private SkillInfoSO CreateSkillInfo(string skillName)
        {
            SkillInfoSO info = CreateSO<SkillInfoSO>();
            SetPrivateField(typeof(SkillInfoSO), info, "<Name>k__BackingField", skillName);
            return info;
        }

        private T CreateSO<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private static FloatUpgradeableStat CreateFloatStat(
            float min,
            float max,
            float upgradeMin,
            float upgradeMax,
            bool isSubtract,
            bool unlimited)
        {
            FloatUpgradeableStat stat = new FloatUpgradeableStat(
                min,
                max,
                new FloatValueRange(min, max),
                new FloatValueRange(upgradeMin, upgradeMax),
                false);
            stat.RestoreRuntimeState(min, true);
            typeof(UpgradeableStat<float>).GetProperty(nameof(FloatUpgradeableStat.IsSubstractModeOn)).SetValue(stat, isSubtract);
            typeof(UpgradeableStat<float>).GetField("_hasUnlimitedMaxValue", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stat, unlimited);
            return stat;
        }

        private static IntUpgradeableStat CreateIntStat(
            int min,
            int max,
            int upgradeMin,
            int upgradeMax,
            bool unlimited,
            bool alwaysUseMin = false)
        {
            IntUpgradeableStat stat = new IntUpgradeableStat(
                min,
                max,
                new IntValueRange(min, max),
                new IntValueRange(upgradeMin, upgradeMax),
                alwaysUseMin);
            stat.RestoreRuntimeState(min, true);
            typeof(UpgradeableStat<int>).GetField("_hasUnlimitedMaxValue", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stat, unlimited);
            return stat;
        }

        private static void InvokeMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method.Invoke(target, null);
        }

        private static void SetProperty(Type ownerType, object target, string propertyName, object value)
        {
            PropertyInfo prop = ownerType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(target, value);
                return;
            }

            SetPrivateField(ownerType, target, $"<{propertyName}>k__BackingField", value);
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(Type ownerType, object target, string fieldName)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (T)field.GetValue(target);
        }

        private sealed class SkillRig
        {
            public GameObject RegistryGameObject { get; set; }
            public SkillsRegistry Registry { get; set; }
            public SawSkill Saw { get; set; }
            public SawSkillUpgradeableConfigSO SawConfig { get; set; }
            public MinigunSkill Minigun { get; set; }
            public MinigunSkillUpgradeableConfigSO MinigunConfig { get; set; }
            public LandmineSkill Landmine { get; set; }
            public LandmineSkillUpgradeableConfigSO LandmineConfig { get; set; }
            public LasergunSkill Lasergun { get; set; }
            public LasergunSkillSO LasergunConfig { get; set; }
            public TurretConfigSO LasergunTurretConfig { get; set; }
            public ProjectileConfigSO LasergunProjectileConfig { get; set; }
        }

        private sealed class TestDamageableTarget : MonoBehaviour, IDamageable
        {
            public float TotalDamageTaken { get; private set; }

            public void TakeDamage(float damage)
            {
                TotalDamageTaken += damage;
            }

            public void TakeFullHpDamage()
            {
                TotalDamageTaken += 1000f;
            }
        }

        private sealed class FakeHealth : IHealth
        {
            private readonly bool _isAlive;

            public FakeHealth(bool isAlive)
            {
                _isAlive = isAlive;
            }

            public float MaxHealth
            {
                get { return 100f; }
                set { }
            }

            public float CurrentHealth
            {
                get { return _isAlive ? 100f : 0f; }
            }

            public event EventHandler OnHealthChanged
            {
                add { }
                remove { }
            }

            public event EventHandler OnHealthDecreased
            {
                add { }
                remove { }
            }

            public event EventHandler OnHealthIncreased
            {
                add { }
                remove { }
            }

            public event EventHandler OnNoHealth
            {
                add { }
                remove { }
            }

            public void IncreaseHealth(float health)
            {
            }

            public void DecreaseHealth(float health)
            {
            }

            public bool IsAlive()
            {
                return _isAlive;
            }
        }

        private sealed class FakeLevelController : ILevelController
        {
            public FakeLevelController(LevelData levelData)
            {
                LevelData = levelData;
            }

            public LevelData LevelData { get; private set; }

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

        private sealed class FakePlayerManager : IPlayerManager
        {
            public FakePlayerManager(ISkillsRegistry skillsRegistry, ILevelController levelController, IHealth health)
            {
                SkillsRegistry = skillsRegistry;
                LevelController = levelController;
                Health = health;
            }

            public IAudioClipPlayer AudioClipPlayer
            {
                get { return null; }
            }

            public ICarController CarController
            {
                get { return null; }
            }

            public ICarVfxEffectsController CarVfxEffectsController
            {
                get { return null; }
            }

            public ILevelController LevelController { get; private set; }
            public ISkillsRegistry SkillsRegistry { get; private set; }

            public IPlayerDamagedHandler DamagedHandler
            {
                get { return null; }
            }

            public IHealth Health { get; private set; }

            public GameObject GameObject
            {
                get { return null; }
            }
        }
    }
}
