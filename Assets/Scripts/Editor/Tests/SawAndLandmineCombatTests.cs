using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.ScriptableObjects.Skills;
using Assets.ScriptableObjects.Skills.PlayerSkills.LandmineSkill;
using Assets.ScriptableObjects.Skills.PlayerSkills.SawSkill;
using Assets.Scripts.Audio;
using Assets.Scripts.Common.Types;
using Assets.Scripts.Effects;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.Constants;
using Assets.Scripts.Skills.PlayerSkills.LandmineTrap;
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
    public class SawAndLandmineCombatTests
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
        public void SawSkill_Initialize_ActivatesSkillAndInitializesFirstSawBlade()
        {
            GameObject skillGo = TrackGameObject("SawSkill", active: false);
            GameObject bladeGo = new GameObject("SawBlade");
            bladeGo.transform.SetParent(skillGo.transform, false);
            bladeGo.SetActive(false);
            SawBlade blade = bladeGo.AddComponent<SawBlade>();

            SawSkill skill = skillGo.AddComponent<SawSkill>();
            SawSkillUpgradeableConfigSO config = CreateSawConfig(damage: 12, knockbackRange: 1f, attackCooldown: 0.2f);
            config.ResetRuntimeState();

            SkillInfoSO info = TrackScriptableObject<SkillInfoSO>();
            SetPrivateField(typeof(SkillInfoSO), info, "<Name>k__BackingField", "Saw");
            SetProperty(typeof(SawSkill), skill, nameof(SawSkill.SkillInfo), info);
            SetProperty(typeof(SawSkill), skill, "_config", config);
            SetPrivateField(typeof(SawSkill), skill, "_sawBlades", new[] { blade });

            skill.Initialize();

            Assert.IsTrue(skill.IsInitialized());
            Assert.IsTrue(skillGo.activeSelf);
            Assert.IsTrue(blade.IsInitialized());
            Assert.IsTrue(bladeGo.activeSelf);
        }

        [Test]
        public void SawBlade_ProcessEnemyCollisionAtTime_EnforcesCooldownFiltersNonEnemyAndForwardsClosestPoint()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");
            Assert.GreaterOrEqual(groundLayer, 0, "Ground layer must exist.");

            SawSkillUpgradeableConfigSO config = CreateSawConfig(damage: 14, knockbackRange: 1f, attackCooldown: 0.25f);
            config.ResetRuntimeState();

            FakeCarController car = new FakeCarController();
            SawBlade blade = CreateSawBlade(Vector3.zero, Vector3.forward, car, config);

            GameObject groundGo = TrackGameObject("GroundCollider", active: true);
            groundGo.layer = groundLayer;
            BoxCollider groundCollider = groundGo.AddComponent<BoxCollider>();

            GameObject enemyGo = TrackGameObject("Enemy", active: true);
            enemyGo.layer = enemyLayer;
            enemyGo.transform.position = new Vector3(0f, 0f, 3f);
            BoxCollider enemyCollider = enemyGo.AddComponent<BoxCollider>();
            TestSawEnemy enemy = enemyGo.AddComponent<TestSawEnemy>();
            Physics.SyncTransforms();

            blade.ProcessEnemyCollisionAtTime(null, 1.0f);
            blade.ProcessEnemyCollisionAtTime(groundCollider, 1.0f);
            Assert.AreEqual(0f, enemy.TotalDamageTaken, 0.001f);

            // First valid hit at t=1.0 succeeds.
            blade.ProcessEnemyCollisionAtTime(enemyCollider, 1.0f);
            Assert.AreEqual(14f, enemy.TotalDamageTaken, 0.001f);
            Assert.AreEqual(1, enemy.PositionalHitCount);
            Assert.AreEqual(2.5f, enemy.LastHitPosition.z, 0.01f);

            // Within 0.25s cooldown (t=1.20) is blocked.
            blade.ProcessEnemyCollisionAtTime(enemyCollider, 1.20f);
            Assert.AreEqual(14f, enemy.TotalDamageTaken, 0.001f);

            // At or after 0.25s cooldown (t=1.25) hits again.
            blade.ProcessEnemyCollisionAtTime(enemyCollider, 1.25f);
            Assert.AreEqual(28f, enemy.TotalDamageTaken, 0.001f);
        }

        [Test]
        public void SawBlade_Knockback_ClampsDistanceDurationToBoundsAndFlattensDirectionToXZ()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");

            // Test lower clamp: knockbackStat = 0.2 => base = 2 * 0.2 = 0.4, speedRatio=0 => clamped to MIN 1.2m, duration clamped to 0.12s.
            SawSkillUpgradeableConfigSO lowConfig = CreateSawConfig(damage: 5, knockbackRange: 0.2f, attackCooldown: 0.1f);
            lowConfig.ResetRuntimeState();
            FakeCarController slowCar = new FakeCarController { Speed = -10f, MaxForwardSpeedValue = 20f };
            SawBlade lowBlade = CreateSawBlade(Vector3.zero, new Vector3(1f, 2f, 0f).normalized, slowCar, lowConfig);

            GameObject enemy1Go = TrackGameObject("EnemyLow", active: true);
            enemy1Go.layer = enemyLayer;
            BoxCollider enemy1Collider = enemy1Go.AddComponent<BoxCollider>();
            TestSawEnemy enemy1 = enemy1Go.AddComponent<TestSawEnemy>();
            Physics.SyncTransforms();

            lowBlade.ProcessEnemyCollisionAtTime(enemy1Collider, 1.0f);

            Assert.AreEqual(SkillConstants.SAW_MIN_KNOCKBACK_DISTANCE, enemy1.LastKnockbackPower, 0.0001f);
            Assert.AreEqual(SkillConstants.SAW_MIN_KNOCKBACK_DURATION, enemy1.LastKnockbackDuration, 0.0001f);
            Assert.AreEqual(0f, enemy1.LastKnockbackDirection.y, 0.0001f);
            Assert.AreEqual(1f, enemy1.LastKnockbackDirection.x, 0.0001f);

            // Test upper clamp: knockbackStat = 3.0 => base = 6.0, speedRatio=1 => 9.0 => clamped to MAX 3.5m, duration clamped to 0.22s.
            SawSkillUpgradeableConfigSO highConfig = CreateSawConfig(damage: 5, knockbackRange: 3.0f, attackCooldown: 0.1f);
            highConfig.ResetRuntimeState();
            FakeCarController fastCar = new FakeCarController { Speed = 20f, MaxForwardSpeedValue = 20f };
            SawBlade highBlade = CreateSawBlade(Vector3.zero, Vector3.forward, fastCar, highConfig);

            GameObject enemy2Go = TrackGameObject("EnemyHigh", active: true);
            enemy2Go.layer = enemyLayer;
            BoxCollider enemy2Collider = enemy2Go.AddComponent<BoxCollider>();
            TestSawEnemy enemy2 = enemy2Go.AddComponent<TestSawEnemy>();
            Physics.SyncTransforms();

            highBlade.ProcessEnemyCollisionAtTime(enemy2Collider, 1.0f);

            Assert.AreEqual(SkillConstants.SAW_MAX_KNOCKBACK_DISTANCE, enemy2.LastKnockbackPower, 0.0001f);
            Assert.AreEqual(SkillConstants.SAW_MAX_KNOCKBACK_DURATION, enemy2.LastKnockbackDuration, 0.0001f);
        }

        [Test]
        public void SawBlade_StepFrame_RampsSpinOnDriftAndPurgesStaleHitsAfterInterval()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");

            SawSkillUpgradeableConfigSO config = CreateSawConfig(damage: 10, knockbackRange: 1f, attackCooldown: 0.2f);
            config.ResetRuntimeState();
            FakeCarController car = new FakeCarController { IsDriftingValue = true, SteerInput = 0f };

            SawBlade blade = CreateSawBlade(Vector3.zero, Vector3.forward, car, config, withRotationLoop: true);
            XYZRotationLoop loop = blade.GetComponentInChildren<XYZRotationLoop>(true);

            // Default _maxTurnSpeedMultiplier = 0.25, drift floor = 0.85 => target multiplier = 1 + 0.25 * 0.85 = 1.2125.
            blade.StepFrame(1.0f, 1.0f);
            Assert.AreEqual(1.2125f, loop.SpeedMultiplier, 0.0001f);

            // Record a hit at t=1.0, then step 3.1s so SAW_COOLDOWN_PURGE_INTERVAL (3.0s) triggers and purges entries > 2.0s old.
            GameObject enemyGo = TrackGameObject("PurgeEnemy", active: true);
            enemyGo.layer = enemyLayer;
            BoxCollider enemyCollider = enemyGo.AddComponent<BoxCollider>();
            enemyGo.AddComponent<TestSawEnemy>();
            Physics.SyncTransforms();

            blade.ProcessEnemyCollisionAtTime(enemyCollider, 1.0f);
            Dictionary<Collider, float> hitMap = GetPrivateField<Dictionary<Collider, float>>(typeof(SawBlade), blade, "_lastHitTimesByCollider");
            Assert.AreEqual(1, hitMap.Count);

            blade.StepFrame(4.1f, 3.1f);
            Assert.AreEqual(0, hitMap.Count);

            // OnDisable resets loop multiplier back to 1.0.
            InvokeMethod(blade, "OnDisable");
            Assert.AreEqual(1.0f, loop.SpeedMultiplier, 0.0001f);
        }

        [Test]
        public void Landmine_InitializeAndOnTriggerEnter_ScalesXZDamagesAppliesRadialKnockbackAndHidesVisual()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");
            Assert.GreaterOrEqual(groundLayer, 0, "Ground layer must exist.");

            LandmineSkillUpgradeableConfigSO config = CreateLandmineConfig(size: 1.8f, explosionRadius: 4.0f, knockbackRange: 2.5f, damage: 15);
            config.ResetRuntimeState();

            GameObject mineGo = TrackGameObject("Landmine", active: false);
            mineGo.transform.position = new Vector3(1400f, 0f, 0f);
            mineGo.transform.localScale = new Vector3(1f, 0.75f, 1f);

            GameObject visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(mineGo.transform, false);

            GameObject vfxGo = new GameObject("Vfx");
            vfxGo.transform.SetParent(mineGo.transform, false);
            ParticleSystem ps = vfxGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            VFXPlayer vfxPlayer = vfxGo.AddComponent<VFXPlayer>();

            Landmine landmine = mineGo.AddComponent<Landmine>();
            SetPrivateField(typeof(Landmine), landmine, "_landmineVisual", visualGo);
            SetPrivateField(typeof(Landmine), landmine, "_deathVfxPlayer", vfxPlayer);
            InvokeMethod(vfxPlayer, "Awake");
            InvokeMethod(landmine, "Awake");

            landmine.Initialize(config);
            Assert.IsTrue(landmine.IsInitialized());
            Assert.AreEqual(1.8f, mineGo.transform.localScale.x, 0.0001f);
            Assert.AreEqual(0.75f, mineGo.transform.localScale.y, 0.0001f);
            Assert.AreEqual(1.8f, mineGo.transform.localScale.z, 0.0001f);

            GameObject groundGo = TrackGameObject("NonEnemy", active: true);
            groundGo.layer = groundLayer;
            BoxCollider groundCollider = groundGo.AddComponent<BoxCollider>();

            GameObject enemyNearGo = TrackGameObject("NearEnemy", active: true);
            enemyNearGo.layer = enemyLayer;
            enemyNearGo.transform.position = new Vector3(1402f, 0f, 0f);
            BoxCollider enemyNearCollider = enemyNearGo.AddComponent<BoxCollider>();
            TestSawEnemy enemyNear = enemyNearGo.AddComponent<TestSawEnemy>();

            GameObject enemyFarGo = TrackGameObject("FarEnemy", active: true);
            enemyFarGo.layer = enemyLayer;
            enemyFarGo.transform.position = new Vector3(1410f, 0f, 0f);
            enemyFarGo.AddComponent<BoxCollider>();
            TestSawEnemy enemyFar = enemyFarGo.AddComponent<TestSawEnemy>();
            Physics.SyncTransforms();

            // Non-enemy trigger does not detonate mine.
            InvokeMethod(landmine, "OnTriggerEnter", groundCollider);
            Assert.IsTrue(visualGo.activeSelf);

            // Enemy trigger detonates mine, damaging and knocking back enemies within ExplosionRadius (4.0m).
            InvokeMethod(landmine, "OnTriggerEnter", enemyNearCollider);
            Assert.IsFalse(visualGo.activeSelf);
            Assert.AreEqual(15f, enemyNear.TotalDamageTaken, 0.001f);
            Assert.AreEqual(2.5f, enemyNear.LastKnockbackPower, 0.0001f);
            Assert.AreEqual(0.5f, enemyNear.LastKnockbackDuration, 0.0001f);
            Assert.AreEqual(1f, enemyNear.LastKnockbackDirection.x, 0.001f);
            Assert.AreEqual(0f, enemyFar.TotalDamageTaken, 0.001f);

            // Second trigger while visual is inactive must not re-detonate.
            InvokeMethod(landmine, "OnTriggerEnter", enemyNearCollider);
            Assert.AreEqual(15f, enemyNear.TotalDamageTaken, 0.001f);
        }

        [Test]
        public void Landmine_ExplodeWithoutDeathVfx_DamagesKnocksBackAndDestroysCleanlyInEditMode()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");

            LandmineSkillUpgradeableConfigSO config = CreateLandmineConfig(size: 1f, explosionRadius: 4.0f, knockbackRange: 2.5f, damage: 20);
            config.ResetRuntimeState();

            GameObject mineGo = TrackGameObject("LandmineNoVfx", active: false);
            mineGo.transform.position = new Vector3(1450f, 0f, 0f);
            GameObject visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(mineGo.transform, false);

            Landmine landmine = mineGo.AddComponent<Landmine>();
            SetPrivateField(typeof(Landmine), landmine, "_landmineVisual", visualGo);
            SetPrivateField(typeof(Landmine), landmine, "_deathVfxPlayer", null);
            InvokeMethod(landmine, "Awake");
            landmine.Initialize(config);

            GameObject enemyGo = TrackGameObject("NoVfxTarget", active: true);
            enemyGo.layer = enemyLayer;
            enemyGo.transform.position = new Vector3(1452f, 0f, 0f);
            BoxCollider enemyCollider = enemyGo.AddComponent<BoxCollider>();
            TestSawEnemy enemy = enemyGo.AddComponent<TestSawEnemy>();
            Physics.SyncTransforms();

            InvokeMethod(landmine, "OnTriggerEnter", enemyCollider);

            Assert.AreEqual(20f, enemy.TotalDamageTaken, 0.001f);
            Assert.AreEqual(2.5f, enemy.LastKnockbackPower, 0.0001f);
            Assert.IsTrue(mineGo == null);
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
            ContainerBuilder builder = new ContainerBuilder().SetName("SawLandmineTestContainer");
            configure?.Invoke(builder);
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private SawBlade CreateSawBlade(
            Vector3 position,
            Vector3 forward,
            ICarController carController,
            SawSkillUpgradeableConfigSO config,
            bool withRotationLoop = false)
        {
            GameObject bladeGo = TrackGameObject("SawBlade", active: false);
            bladeGo.transform.position = position;
            if (forward.sqrMagnitude > 0.001f)
            {
                bladeGo.transform.forward = forward;
            }

            if (withRotationLoop)
            {
                GameObject loopGo = new GameObject("RotationLoop");
                loopGo.transform.SetParent(bladeGo.transform, false);
                loopGo.AddComponent<XYZRotationLoop>();
            }

            SawBlade blade = bladeGo.AddComponent<SawBlade>();
            FakePlayerManager playerManager = new FakePlayerManager(carController);
            Container container = BuildContainer((builder) =>
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
            });
            GameObjectInjector.InjectRecursive(bladeGo, container);

            InvokeMethod(blade, "Awake");
            blade.Initialize(config);
            return blade;
        }

        private SawSkillUpgradeableConfigSO CreateSawConfig(int damage, float knockbackRange, float attackCooldown)
        {
            SawSkillUpgradeableConfigSO config = TrackScriptableObject<SawSkillUpgradeableConfigSO>();
            SetProperty(typeof(SawSkillUpgradeableConfigSO), config, nameof(SawSkillUpgradeableConfigSO.AttackCooldown), attackCooldown);
            SetPrivateField(typeof(SawSkillUpgradeableConfigSO), config, "_knockbackRange", CreateFloatStat(knockbackRange, 4f, 0.5f, 0.8f, false, false));
            SetPrivateField(typeof(SawSkillUpgradeableConfigSO), config, "_damage", CreateIntStat(damage, 50, 4, 7, true));
            return config;
        }

        private LandmineSkillUpgradeableConfigSO CreateLandmineConfig(float size, float explosionRadius, float knockbackRange, int damage)
        {
            LandmineSkillUpgradeableConfigSO config = TrackScriptableObject<LandmineSkillUpgradeableConfigSO>();
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_spawnCooldown", CreateFloatStat(8f, 1f, 2f, 4f, true, false));
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_explosionRadius", CreateFloatStat(explosionRadius, 8f, 1f, 1.2f, false, true));
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_size", CreateFloatStat(size, 4f, 0.5f, 0.8f, false, true));
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_knockbackRange", CreateFloatStat(knockbackRange, 6f, 0.5f, 0.8f, false, true));
            SetPrivateField(typeof(LandmineSkillUpgradeableConfigSO), config, "_damage", CreateIntStat(damage, 50, 4, 6, true));
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

        private static IntUpgradeableStat CreateIntStat(int min, int max, int upgradeMin, int upgradeMax, bool unlimited)
        {
            IntUpgradeableStat stat = new IntUpgradeableStat(
                min,
                max,
                new IntValueRange(min, max),
                new IntValueRange(upgradeMin, upgradeMax),
                false);
            stat.RestoreRuntimeState(min, true);
            typeof(UpgradeableStat<int>).GetField("_hasUnlimitedMaxValue", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stat, unlimited);
            return stat;
        }

        private static void InvokeMethod(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            method.Invoke(target, args);
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

        private sealed class TestSawEnemy : MonoBehaviour, IPositionalDamageable, IKnockable
        {
            public float TotalDamageTaken { get; private set; }
            public int PositionalHitCount { get; private set; }
            public Vector3 LastHitPosition { get; private set; }
            public Vector3 LastKnockbackDirection { get; private set; }
            public float LastKnockbackPower { get; private set; }
            public float LastKnockbackDuration { get; private set; }

            public void TakeDamage(float damage)
            {
                TotalDamageTaken += damage;
            }

            public void TakeDamage(float damage, Vector3 hitWorldPosition)
            {
                PositionalHitCount++;
                LastHitPosition = hitWorldPosition;
                TotalDamageTaken += damage;
            }

            public void TakeFullHpDamage()
            {
                TotalDamageTaken += 1000f;
            }

            public void ApplyKnockBack(Vector3 direction, float power, float timeToArriveAtLocation)
            {
                LastKnockbackDirection = direction;
                LastKnockbackPower = power;
                LastKnockbackDuration = timeToArriveAtLocation;
            }
        }

        private sealed class FakeCarController : ICarController
        {
            public float Speed { get; set; }
            public float MaxForwardSpeedValue { get; set; } = 20f;
            public bool IsDriftingValue { get; set; }
            public float SteerInput { get; set; }

            public event EventHandler OnBrakePress { add { } remove { } }
            public event EventHandler OnBrakeRelease { add { } remove { } }
            public event EventHandler OnDriftStop { add { } remove { } }

            public float GetMovementSpeed()
            {
                return Speed;
            }

            public Vector3 GetMovementVelocity()
            {
                return Vector3.forward * Speed;
            }

            public bool TryGetTireGroundContact(Vector3 contactPosition, out RaycastHit groundHit)
            {
                groundHit = default;
                return false;
            }

            public float MaxForwardSpeed { get { return MaxForwardSpeedValue; } }
            public float MaxOverallSpeed { get { return MaxForwardSpeedValue; } }
            public bool IsDrifting { get { return IsDriftingValue; } }
            public int DriftDirection { get { return 0; } }
            public float DriftYawAngle { get { return 0f; } }
            public bool IsGrounded { get { return true; } }
            public float CurrentSteerInput { get { return SteerInput; } }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            public FakePlayerManager(ICarController carController)
            {
                CarController = carController;
            }

            public IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public ICarController CarController { get; }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get { return null; } }
            public ISkillsRegistry SkillsRegistry { get { return null; } }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }
            public IHealth Health { get { return null; } }
            public GameObject GameObject { get { return null; } }
        }
    }
}
