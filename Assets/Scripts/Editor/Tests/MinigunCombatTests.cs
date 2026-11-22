using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.ScriptableObjects;
using Assets.ScriptableObjects.Skills;
using Assets.ScriptableObjects.Skills.PlayerSkills.MinigunSkill;
using Assets.Scripts.Audio;
using Assets.Scripts.Common.Types;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.PlayerSkills.Minigun;
using Assets.Scripts.Skills.PlayerSkills.Minigun.Constants;
using Assets.Scripts.Stats;
using Assets.Scripts.StatusEffects;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class MinigunCombatTests
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
        public void MinigunShotResolver_ValidateGeometry_RejectsInvalidDirectionDimensionsAndNonFiniteValues()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                MinigunShotResolver.ValidateGeometry(Vector3.zero, new Vector3(0f, 0.5f, 0.866f).normalized, 10f, 0.5f);
            });

            Assert.Throws<InvalidOperationException>(() =>
            {
                MinigunShotResolver.ValidateGeometry(Vector3.zero, new Vector3(0f, 0f, 2f), 10f, 0.5f);
            });

            Assert.Throws<InvalidOperationException>(() =>
            {
                MinigunShotResolver.ValidateGeometry(Vector3.zero, Vector3.forward, 0f, 0.5f);
            });

            Assert.Throws<InvalidOperationException>(() =>
            {
                MinigunShotResolver.ValidateGeometry(Vector3.zero, Vector3.forward, 10f, -0.1f);
            });

            Assert.Throws<InvalidOperationException>(() =>
            {
                MinigunShotResolver.ValidateGeometry(new Vector3(float.NaN, 0f, 0f), Vector3.forward, 10f, 0.5f);
            });

            Assert.Throws<InvalidOperationException>(() =>
            {
                MinigunShotResolver.ValidateGeometry(Vector3.zero, Vector3.forward, float.PositiveInfinity, 0.5f);
            });
        }

        [Test]
        public void MinigunShotResolver_ResolveShot_RejectsOutOfRangePiercingAndIndexOutOfBounds()
        {
            MinigunShotResolver resolver = new MinigunShotResolver();

            Assert.Throws<InvalidOperationException>(() =>
            {
                resolver.ResolveShot(Vector3.zero, Vector3.forward, 10f, 0.5f, -1);
            });

            Assert.Throws<InvalidOperationException>(() =>
            {
                resolver.ResolveShot(Vector3.zero, Vector3.forward, 10f, 0.5f, MinigunConstants.MAX_TARGETS);
            });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                resolver.GetSelectedHit(0);
            });
        }

        [Test]
        public void MinigunShotResolver_ResolveShot_OpenLane_ReturnsFullRangeWithZeroTargets()
        {
            MinigunShotResolver resolver = new MinigunShotResolver();

            MinigunShotResult result = resolver.ResolveShot(new Vector3(1000f, 0f, 1000f), Vector3.forward, 15f, 0.4f, 2);

            Assert.AreEqual(15f, result.Distance, 0.0001f);
            Assert.AreEqual(0, result.TargetCount);
            Assert.AreEqual(MinigunShotStopReason.Range, result.StopReason);
            Assert.AreEqual(0, resolver.SelectedCount);
        }

        [Test]
        public void MinigunShotResolver_ResolveShot_TerrainStopsCorridorAndExcludesOccludedEnemies()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int wallLayer = LayerMask.NameToLayer("Impassable");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");
            Assert.GreaterOrEqual(wallLayer, 0, "Impassable layer must exist.");

            Vector3 origin = new Vector3(200f, 0f, 0f);
            CreateBoxObstacle("Wall", wallLayer, origin + new Vector3(0f, 0f, 6f), new Vector3(2f, 2f, 1f));
            TestMinigunEnemy frontEnemy = CreateEnemy("FrontEnemy", enemyLayer, origin + new Vector3(0f, 0f, 3f), Vector3.one, isAlive: true);
            CreateEnemy("BehindWallEnemy", enemyLayer, origin + new Vector3(0f, 0f, 9f), Vector3.one, isAlive: true);
            Physics.SyncTransforms();

            MinigunShotResolver resolver = new MinigunShotResolver();
            MinigunShotResult result = resolver.ResolveShot(origin, Vector3.forward, 20f, 0.5f, 3);
            float terrainLimit = resolver.GetTerrainLimitedLength(origin, Vector3.forward, 20f, 0.5f);

            // Wall center at z=6 with depth=1 means front face is at z=5.5; query width/depth stops near 5.5.
            Assert.AreEqual(MinigunShotStopReason.Terrain, result.StopReason);
            Assert.AreEqual(5.5f, result.Distance, 0.05f);
            Assert.AreEqual(result.Distance, terrainLimit, 0.0001f);
            Assert.AreEqual(1, result.TargetCount);
            Assert.AreSame(frontEnemy, resolver.GetSelectedHit(0).Target);
        }

        [Test]
        public void MinigunShotResolver_ResolveShot_OrdersDeduplicatesSkipsDeadAndEnforcesPiercingBudget()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");

            Vector3 origin = new Vector3(400f, 0f, 0f);
            // Enemy with two child colliders at different distances must deduplicate to nearest distance.
            TestMinigunEnemy multiColliderEnemy = CreateMultiColliderEnemy("MultiEnemy", enemyLayer, origin + new Vector3(0f, 0f, 4f), 2f);
            CreateEnemy("DeadEnemy", enemyLayer, origin + new Vector3(0f, 0f, 2f), Vector3.one, isAlive: false);
            TestMinigunEnemy secondLiving = CreateEnemy("SecondLiving", enemyLayer, origin + new Vector3(0f, 0f, 8f), Vector3.one, isAlive: true);
            CreateEnemy("BeyondBudget", enemyLayer, origin + new Vector3(0f, 0f, 12f), Vector3.one, isAlive: true);
            Physics.SyncTransforms();

            MinigunShotResolver resolver = new MinigunShotResolver();
            // Piercing = 1 allows budget = 1 + 1 = 2 distinct living enemies.
            MinigunShotResult result = resolver.ResolveShot(origin, Vector3.forward, 20f, 0.5f, 1);

            Assert.AreEqual(MinigunShotStopReason.Piercing, result.StopReason);
            Assert.AreEqual(2, result.TargetCount);
            Assert.AreSame(multiColliderEnemy, resolver.GetSelectedHit(0).Target);
            Assert.AreSame(secondLiving, resolver.GetSelectedHit(1).Target);
            Assert.Less(resolver.GetSelectedHit(0).Distance, resolver.GetSelectedHit(1).Distance);
            Assert.AreEqual(resolver.GetSelectedHit(1).Distance, result.Distance, 0.0001f);
        }

        [Test]
        public void MinigunTurret_ValidateReferencesAndInitialize_ThrowOnInvalidWiring()
        {
            GameObject turretGo = TrackGameObject("InvalidMinigunTurret", active: false);
            MinigunTurret turret = turretGo.AddComponent<MinigunTurret>();

            Assert.Throws<InvalidOperationException>(() =>
            {
                turret.ValidateReferences();
            });

            GameObject gunTip = CreateChildObject(turretGo.transform, "GunTip");
            GameObject visual = CreateChildObject(turretGo.transform, "Visual");
            RectangularTelegraphIndicator indicator = CreateTelegraphIndicator(turretGo.transform);

            SetPrivateField(typeof(MinigunTurret), turret, "_gunTip", gunTip.transform);
            SetPrivateField(typeof(MinigunTurret), turret, "_visual", visual.transform);
            SetPrivateField(typeof(MinigunTurret), turret, "_laneIndicator", indicator);
            SetPrivateField(typeof(MinigunTurret), turret, "_tracerSpeed", 0f);

            Assert.Throws<InvalidOperationException>(() =>
            {
                turret.ValidateReferences();
            });

            SetPrivateField(typeof(MinigunTurret), turret, "_tracerSpeed", 70f);
            MinigunSkillUpgradeableConfigSO badConfig = CreateMinigunConfig(rotationDuration: 0f);
            badConfig.ResetRuntimeState();

            Assert.Throws<InvalidOperationException>(() =>
            {
                turret.Initialize(badConfig);
            });
        }

        [Test]
        public void MinigunTurret_UpdatePresentation_AppliesCosineRotationAndTogglesGroundIndicator()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.GreaterOrEqual(groundLayer, 0, "Ground layer must exist.");

            Vector3 groundPos = new Vector3(600f, -1f, 0f);
            CreateBoxObstacle("WalkableGround", groundLayer, groundPos, new Vector3(10f, 1f, 10f));
            Physics.SyncTransforms();

            MinigunTurretRig rig = CreateMinigunTurretRig(new Vector3(600f, 1f, 0f), inverseRotation: false, tracerSpeed: 70f);
            MinigunSkillUpgradeableConfigSO config = CreateMinigunConfig(rotationDuration: 4f, rotationAngle: 90f);
            config.ResetRuntimeState();
            rig.Turret.Initialize(config);

            // At scaledTime = 0, cos(0) = 1 => angle = -1 * (90 * 0.5) * 1 = -45 deg (315 deg Euler Y).
            rig.Turret.UpdatePresentation(0f, 0.016f);
            Assert.AreEqual(315f, rig.Visual.localEulerAngles.y, 0.1f);
            Assert.IsTrue(rig.Indicator.gameObject.activeSelf);

            // Move turret away from walkable ground -> indicator hides.
            rig.TurretObject.transform.position = new Vector3(900f, 1f, 0f);
            rig.Turret.UpdatePresentation(4f, 0.016f);
            // At scaledTime = 4 with duration 4, cos(pi) = -1 => angle = +45 deg.
            Assert.AreEqual(45f, rig.Visual.localEulerAngles.y, 0.1f);
            Assert.IsFalse(rig.Indicator.gameObject.activeSelf);
        }

        [Test]
        public void MinigunTurret_Shoot_PointBlankAppliesImmediateDamage_AndDistantWaitsForTracerFlight()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");

            Vector3 origin = new Vector3(800f, 0f, 0f);
            MinigunTurretRig rig = CreateMinigunTurretRig(origin, inverseRotation: false, tracerSpeed: 70f);
            MinigunSkillUpgradeableConfigSO config = CreateMinigunConfig(damage: 11, range: 20f, piercing: 1);
            config.ResetRuntimeState();
            rig.Turret.Initialize(config);

            // Point-blank enemy overlapping gunTip -> result.Distance == 0 (< 0.2m) => immediate damage.
            TestPositionalEnemy pointBlankEnemy = CreatePositionalEnemy("PointBlank", enemyLayer, origin, Vector3.one * 2f, isAlive: true);
            Physics.SyncTransforms();

            rig.Turret.Shoot();
            Assert.AreEqual(11f, pointBlankEnemy.TotalDamageTaken, 0.001f);
            Assert.AreEqual(1, pointBlankEnemy.PositionalCallCount);

            // Move point-blank away, place distant enemy at z = 7.5 (surface at z = 7.0 => delay = 7.0 / 70 = 0.1s).
            pointBlankEnemy.gameObject.SetActive(false);
            TestMinigunEnemy distantEnemy = CreateEnemy("Distant", enemyLayer, origin + new Vector3(0f, 0f, 7.5f), Vector3.one, isAlive: true);
            Physics.SyncTransforms();

            rig.Turret.Shoot();
            Assert.AreEqual(0f, distantEnemy.TotalDamageTaken, 0.001f);

            rig.Turret.UpdatePresentation(0.05f, 0.05f);
            Assert.AreEqual(0f, distantEnemy.TotalDamageTaken, 0.001f);

            rig.Turret.UpdatePresentation(0.11f, 0.06f);
            Assert.AreEqual(11f, distantEnemy.TotalDamageTaken, 0.001f);
        }

        [Test]
        public void MinigunTurret_DelayedHit_SkipsDeadTargetAndClearsPendingOnHide()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");

            Vector3 origin = new Vector3(1000f, 0f, 0f);
            MinigunTurretRig rig = CreateMinigunTurretRig(origin, inverseRotation: false, tracerSpeed: 70f);
            MinigunSkillUpgradeableConfigSO config = CreateMinigunConfig(damage: 10, range: 20f, piercing: 0);
            config.ResetRuntimeState();
            rig.Turret.Initialize(config);

            TestMinigunEnemy target = CreateEnemy("DyingTarget", enemyLayer, origin + new Vector3(0f, 0f, 7.5f), Vector3.one, isAlive: true);
            Physics.SyncTransforms();

            rig.Turret.Shoot();
            target.SetAlive(false);
            rig.Turret.UpdatePresentation(0.2f, 0.2f);
            Assert.AreEqual(0f, target.TotalDamageTaken, 0.001f);

            // Re-alive target, schedule hit, then HideAttackPresentation before arrival -> no damage.
            target.SetAlive(true);
            rig.Turret.Shoot();
            rig.Turret.HideAttackPresentation();
            rig.Turret.UpdatePresentation(0.5f, 0.5f);
            Assert.AreEqual(0f, target.TotalDamageTaken, 0.001f);
        }

        [Test]
        public void MinigunSkill_InitializeAndAdvanceVolleyTimer_ValidatesCapacityFiresOnCadenceAndStopsOnDeath()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");

            Vector3 origin = new Vector3(1200f, 0f, 0f);
            MinigunTurretRig turretRig = CreateMinigunTurretRig(origin, inverseRotation: false, tracerSpeed: 70f);
            TestMinigunEnemy pointBlankTarget = CreateEnemy("PointBlank", enemyLayer, origin, Vector3.one * 2f, isAlive: true);
            Physics.SyncTransforms();

            MinigunSkillUpgradeableConfigSO config = CreateMinigunConfig(shotDelay: 1.0f, damage: 9, piercing: 0, numberOfTurrets: 1, maxTurrets: 1);
            config.ResetRuntimeState();

            GameObject skillGo = TrackGameObject("MinigunSkill", active: false);
            skillGo.transform.position = origin;
            turretRig.TurretObject.transform.SetParent(skillGo.transform, true);

            MinigunSkill skill = skillGo.AddComponent<MinigunSkill>();
            SkillInfoSO info = TrackScriptableObject<SkillInfoSO>();
            SetPrivateField(typeof(SkillInfoSO), info, "<Name>k__BackingField", "Minigun");
            SetProperty(typeof(MinigunSkill), skill, nameof(MinigunSkill.SkillInfo), info);
            SetProperty(typeof(MinigunSkill), skill, "_config", config);
            SetPrivateField(typeof(MinigunSkill), skill, "_turrets", new[] { turretRig.Turret });

            FakeHealth health = new FakeHealth(true);
            FakePlayerManager playerManager = new FakePlayerManager(health);
            Container container = BuildContainer((builder) =>
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
            });
            GameObjectInjector.InjectRecursive(skillGo, container);

            skill.Initialize();
            Assert.IsTrue(skill.IsInitialized());
            Assert.IsTrue(turretRig.Turret.IsInitialized());

            // First update fires pending initial shot immediately.
            Assert.IsTrue(skill.AdvanceVolleyTimer(0.016f, 0.016f));
            Assert.AreEqual(9f, pointBlankTarget.TotalDamageTaken, 0.001f);

            // Before ShotDelay (1.0s), no volley fires.
            Assert.IsFalse(skill.AdvanceVolleyTimer(0.5f, 0.5f));
            Assert.AreEqual(9f, pointBlankTarget.TotalDamageTaken, 0.001f);

            // Crossing ShotDelay fires second volley.
            Assert.IsTrue(skill.AdvanceVolleyTimer(1.1f, 0.6f));
            Assert.AreEqual(18f, pointBlankTarget.TotalDamageTaken, 0.001f);

            // Player death stops firing.
            health.Kill();
            Assert.IsFalse(skill.AdvanceVolleyTimer(2.5f, 1.5f));
            Assert.AreEqual(18f, pointBlankTarget.TotalDamageTaken, 0.001f);
        }

        [Test]
        public void MinigunSkill_Initialize_ThrowsWhenConfigOrCapacityInvalid()
        {
            MinigunTurretRig turretRig = CreateMinigunTurretRig(Vector3.zero, inverseRotation: false, tracerSpeed: 70f);
            MinigunSkillUpgradeableConfigSO oversizedConfig = CreateMinigunConfig(numberOfTurrets: 1, maxTurrets: 2);
            oversizedConfig.ResetRuntimeState();

            GameObject skillGo = TrackGameObject("InvalidMinigunSkill", active: false);
            MinigunSkill skill = skillGo.AddComponent<MinigunSkill>();
            SetProperty(typeof(MinigunSkill), skill, "_config", oversizedConfig);
            SetPrivateField(typeof(MinigunSkill), skill, "_turrets", new[] { turretRig.Turret });

            Assert.Throws<InvalidOperationException>(() =>
            {
                skill.Initialize();
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
            ContainerBuilder builder = new ContainerBuilder().SetName("MinigunTestContainer");
            configure?.Invoke(builder);
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private GameObject CreateChildObject(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private RectangularTelegraphIndicator CreateTelegraphIndicator(Transform parent)
        {
            GameObject indicatorGo = CreateChildObject(parent, "Indicator");
            GameObject border = CreateChildObject(indicatorGo.transform, "OuterBorder");
            GameObject fill = CreateChildObject(indicatorGo.transform, "InnerFill");
            RectangularTelegraphIndicator indicator = indicatorGo.AddComponent<RectangularTelegraphIndicator>();
            SetPrivateField(typeof(RectangularTelegraphIndicator), indicator, "_outerBorder", border.transform);
            SetPrivateField(typeof(RectangularTelegraphIndicator), indicator, "_innerFill", fill.transform);
            return indicator;
        }

        private MinigunTurretRig CreateMinigunTurretRig(Vector3 position, bool inverseRotation, float tracerSpeed)
        {
            GameObject turretGo = TrackGameObject("MinigunTurret", active: false);
            turretGo.transform.position = position;
            GameObject gunTip = CreateChildObject(turretGo.transform, "GunTip");
            GameObject visual = CreateChildObject(turretGo.transform, "Visual");
            RectangularTelegraphIndicator indicator = CreateTelegraphIndicator(turretGo.transform);

            MinigunTurret turret = turretGo.AddComponent<MinigunTurret>();
            SetPrivateField(typeof(MinigunTurret), turret, "_gunTip", gunTip.transform);
            SetPrivateField(typeof(MinigunTurret), turret, "_visual", visual.transform);
            SetPrivateField(typeof(MinigunTurret), turret, "_laneIndicator", indicator);
            SetPrivateField(typeof(MinigunTurret), turret, "_inverseRotation", inverseRotation);
            SetPrivateField(typeof(MinigunTurret), turret, "_tracerSpeed", tracerSpeed);

            return new MinigunTurretRig
            {
                TurretObject = turretGo,
                Turret = turret,
                Visual = visual.transform,
                Indicator = indicator
            };
        }

        private void CreateBoxObstacle(string name, int layer, Vector3 position, Vector3 size)
        {
            GameObject go = TrackGameObject(name, active: true);
            go.layer = layer;
            go.transform.position = position;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
        }

        private TestMinigunEnemy CreateEnemy(string name, int layer, Vector3 position, Vector3 size, bool isAlive)
        {
            GameObject go = TrackGameObject(name, active: true);
            go.layer = layer;
            go.transform.position = position;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
            TestMinigunEnemy enemy = go.AddComponent<TestMinigunEnemy>();
            enemy.SetAlive(isAlive);
            return enemy;
        }

        private TestPositionalEnemy CreatePositionalEnemy(string name, int layer, Vector3 position, Vector3 size, bool isAlive)
        {
            GameObject go = TrackGameObject(name, active: true);
            go.layer = layer;
            go.transform.position = position;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
            TestPositionalEnemy enemy = go.AddComponent<TestPositionalEnemy>();
            enemy.SetAlive(isAlive);
            return enemy;
        }

        private TestMinigunEnemy CreateMultiColliderEnemy(string name, int layer, Vector3 basePosition, float offsetZ)
        {
            GameObject root = TrackGameObject(name, active: true);
            root.layer = layer;
            root.transform.position = basePosition;
            TestMinigunEnemy enemy = root.AddComponent<TestMinigunEnemy>();
            enemy.SetAlive(true);

            GameObject nearColliderGo = CreateChildObject(root.transform, "NearCollider");
            nearColliderGo.layer = layer;
            nearColliderGo.transform.localPosition = Vector3.zero;
            nearColliderGo.AddComponent<BoxCollider>();

            GameObject farColliderGo = CreateChildObject(root.transform, "FarCollider");
            farColliderGo.layer = layer;
            farColliderGo.transform.localPosition = new Vector3(0f, 0f, offsetZ);
            farColliderGo.AddComponent<BoxCollider>();

            return enemy;
        }

        private MinigunSkillUpgradeableConfigSO CreateMinigunConfig(
            float shotDelay = 1.2f,
            float range = 20f,
            int numberOfTurrets = 1,
            int maxTurrets = 1,
            float beamWidth = 0.5f,
            int damage = 5,
            int piercing = 1,
            float rotationAngle = 90f,
            float rotationDuration = 8f)
        {
            MinigunSkillUpgradeableConfigSO config = TrackScriptableObject<MinigunSkillUpgradeableConfigSO>();
            TurretConfigSO turretConfig = TrackScriptableObject<TurretConfigSO>();
            SetProperty(typeof(TurretConfigSO), turretConfig, nameof(TurretConfigSO.RotationAngle), rotationAngle);
            SetProperty(typeof(TurretConfigSO), turretConfig, nameof(TurretConfigSO.RotationDuration), rotationDuration);
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_turretConfig", turretConfig);
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_delayBetweenShootingBullets", CreateFloatStat(shotDelay, 0.2f, 0.2f, 0.3f, true, false));
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_range", CreateFloatStat(range, 30f, 2f, 4f, false, false));
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_numberOfTurrets", CreateIntStat(numberOfTurrets, maxTurrets, 1, 1, false, true));
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_startBulletSize", CreateFloatStat(beamWidth, 1f, 0.1f, 0.2f, false, true));
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_startBulletDamage", CreateIntStat(damage, 25, 2, 5, true));
            SetPrivateField(typeof(MinigunSkillUpgradeableConfigSO), config, "_startBulletMaxPiercing", CreateIntStat(piercing, 5, 1, 1, false));
            return config;
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

        private sealed class MinigunTurretRig
        {
            public GameObject TurretObject { get; set; }
            public MinigunTurret Turret { get; set; }
            public Transform Visual { get; set; }
            public RectangularTelegraphIndicator Indicator { get; set; }
        }

        private class TestMinigunEnemy : MonoBehaviour, IDamageable, IHealth
        {
            private bool _isAlive = true;

            public float TotalDamageTaken { get; private set; }
            public float MaxHealth { get { return 100f; } set { } }
            public float CurrentHealth { get { return _isAlive ? 100f : 0f; } }

            public event EventHandler OnHealthChanged { add { } remove { } }
            public event EventHandler OnHealthDecreased { add { } remove { } }
            public event EventHandler OnHealthIncreased { add { } remove { } }
            public event EventHandler OnNoHealth { add { } remove { } }

            public void SetAlive(bool alive)
            {
                _isAlive = alive;
            }

            public virtual void TakeDamage(float damage)
            {
                TotalDamageTaken += damage;
            }

            public void TakeFullHpDamage()
            {
                TotalDamageTaken += 1000f;
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

        private sealed class TestPositionalEnemy : TestMinigunEnemy, IPositionalDamageable
        {
            public int PositionalCallCount { get; private set; }
            public Vector3 LastHitPosition { get; private set; }

            public void TakeDamage(float damage, Vector3 hitWorldPosition)
            {
                PositionalCallCount++;
                LastHitPosition = hitWorldPosition;
                base.TakeDamage(damage);
            }
        }

        private sealed class FakeHealth : IHealth
        {
            private bool _isAlive;

            public FakeHealth(bool isAlive)
            {
                _isAlive = isAlive;
            }

            public float MaxHealth { get { return 100f; } set { } }
            public float CurrentHealth { get { return _isAlive ? 100f : 0f; } }

            public event EventHandler OnHealthChanged { add { } remove { } }
            public event EventHandler OnHealthDecreased { add { } remove { } }
            public event EventHandler OnHealthIncreased { add { } remove { } }
            public event EventHandler OnNoHealth;

            public void Kill()
            {
                _isAlive = false;
                OnNoHealth?.Invoke(this, EventArgs.Empty);
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

        private sealed class FakePlayerManager : IPlayerManager
        {
            public FakePlayerManager(IHealth health)
            {
                Health = health;
            }

            public IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public ICarController CarController { get { return null; } }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get { return null; } }
            public ISkillsRegistry SkillsRegistry { get { return null; } }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }
            public IHealth Health { get; }
            public GameObject GameObject { get { return null; } }
        }
    }
}
