using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Audio;
using Assets.Scripts.Collisions;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Barrel;
using Assets.Scripts.Enemies.Barrel.Constants;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Enemies.Bosses;
using Assets.Scripts.Enemies.Bosses.Golem;
using Assets.Scripts.Enemies.Bosses.Towers.Arena;
using Assets.Scripts.Enemies.Constants;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.Interactables;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.Navigation.FlowFieldSystem;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.PowerUps;
using Assets.Scripts.PowerUps.Config;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.PowerUps.Nuke;
using Assets.Scripts.Shapes;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.VFX;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Assets.Scripts.Editor.Tests
{
    public class EnemyCoreAndDeathTests
    {
        private const float TOLERANCE = 0.0001f;
        private const string TIER2_SHINY_SHADER_NAME = "Custom/Tier2ShinyEnemy";
        private const string BARREL_PREFAB_PATH = "Assets/Prefabs/Enemies/Basic/BarrelEnemy.prefab";
        private const string BARREL_EXPLOSION_CONFIG_PATH = "Assets/ScriptableObjects/Enemy/Basic/Barrel/BarrelExplosionConfig.asset";
        private const string DROP_ANIM_CONFIG_PATH = "Assets/ScriptableObjects/Collectibles/DropAnimationConfiguration.asset";
        private const string NUKE_CONFIG_PATH = "Assets/ScriptableObjects/Collectibles/PowerUps/NukeConfig.asset";

        private static readonly string[] ZOMBIE_PREFAB_PATHS =
        {
            "Assets/Prefabs/Enemies/Basic/Zombies/Crawling/CrawlingZombieLevel2.prefab",
            "Assets/Prefabs/Enemies/Basic/Zombies/Crawling/CrawlingZombieLevel3.prefab",
            "Assets/Prefabs/Enemies/Basic/Zombies/Standing/StandingZombieLevel1.prefab",
            "Assets/Prefabs/Enemies/Basic/Zombies/Standing/StandingZombieLevel2.prefab",
            "Assets/Prefabs/Enemies/Basic/Zombies/Standing/StandingZombieLevel3.prefab",
            "Assets/Prefabs/Enemies/Basic/Zombies/Standing/StandingZombieLevel4.prefab"
        };

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();

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
        }

        [Test]
        public void EnemyAwake_WithInitialShinyConfig_CachesSiblingsAndAppliesMaxHealthAndShine()
        {
            // Arrange
            EnemyConfigSO shinyConfig = CreateEnemyConfig(maxHealth: 175f, expForKill: 25f, isShiny: true);
            EnemyHarness harness = CreateEnemyHarness(shinyConfig, withShineController: true, withBloodVfx: false, runAwake: false, runEnable: false);

            // Act
            InvokeLifecycleMethod(harness.Enemy, "Awake");

            // Assert
            Assert.AreSame(harness.Health, harness.Enemy.Health);
            Assert.AreSame(harness.Collisions, harness.Enemy.CollisionsController);
            Assert.AreSame(harness.Movement, harness.Enemy.MovementController);
            Assert.AreSame(harness.AudioPlayer, harness.Enemy.AudioClipPlayer);
            Assert.AreSame(harness.Animator, harness.Enemy.EnemyAnimator);
            Assert.AreEqual(175f, harness.Health.MaxHealth, TOLERANCE);
            Assert.IsTrue(harness.ShineController.IsShineEnabled);
        }

        [Test]
        public void EnemyAwake_WithNullConfig_CachesComponentsWithoutThrowing()
        {
            // Arrange
            EnemyHarness harness = CreateEnemyHarness(config: null, withShineController: false, withBloodVfx: false, runAwake: false, runEnable: false);

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                InvokeLifecycleMethod(harness.Enemy, "Awake");
            });

            Assert.AreSame(harness.Health, harness.Enemy.Health);
            Assert.AreEqual(0f, harness.Health.MaxHealth, TOLERANCE);
        }

        [Test]
        public void EnemyFreezeState_WhenEnabledVsDisabled_ForwardsOnFrozenChangedOnlyWhileEnabled()
        {
            // Arrange
            EnemyConfigSO config = CreateEnemyConfig(maxHealth: 100f, expForKill: 10f, isShiny: false);
            EnemyHarness harness = CreateEnemyHarness(config, withShineController: false, withBloodVfx: false, runAwake: true, runEnable: true);

            List<bool> receivedStates = new List<bool>();
            harness.Enemy.OnFrozenChanged += delegate (bool isFrozen)
            {
                receivedStates.Add(isFrozen);
            };

            // Act
            harness.TimeFreeze.SetFrozen(true, unfrozenTime: 12.5f);
            bool frozenWhileEnabled = harness.Enemy.IsFrozen;
            float unfrozenTimeReading = harness.Enemy.UnfrozenTime;

            InvokeLifecycleMethod(harness.Enemy, "OnDisable");
            harness.TimeFreeze.SetFrozen(false, unfrozenTime: 18f);

            // Assert
            Assert.IsTrue(frozenWhileEnabled);
            Assert.AreEqual(12.5f, unfrozenTimeReading, TOLERANCE);
            Assert.AreEqual(1, receivedStates.Count);
            Assert.IsTrue(receivedStates[0]);
        }

        [Test]
        public void EnemyFreezeProperties_WhenTimeFreezeStateNotInjected_FallbackToUnfrozenAndUnityTime()
        {
            // Arrange
            GameObject root = TrackGameObject("UninjectedEnemy", active: false);
            root.AddComponent<Health>();
            root.AddComponent<FakeCollisionsController>();
            root.AddComponent<FakeMovementController>();
            root.AddComponent<FakeDeathSequence>();
            Enemy enemy = root.AddComponent<Enemy>();

            // Act & Assert
            Assert.IsFalse(enemy.IsFrozen);
            Assert.AreEqual(Time.time, enemy.UnfrozenTime, TOLERANCE);
        }

        [Test]
        public void EnemyOnGet_ReactivatesHiddenVisualResetsAnimatorAttackAndUpdatesMaxHealth()
        {
            // Arrange
            EnemyConfigSO config = CreateEnemyConfig(maxHealth: 220f, expForKill: 15f, isShiny: false);
            EnemyHarness harness = CreateEnemyHarness(config, withShineController: false, withBloodVfx: false, runAwake: true, runEnable: true);
            harness.Visual.SetActive(false);
            harness.Health.MaxHealth = 50f;
            harness.Animator.Call_OnAttackAnimationStart();
            Assert.IsTrue(harness.Animator.IsPlayingAttackAnimation);

            // Act
            harness.Enemy.OnGet();

            // Assert
            Assert.IsTrue(harness.Visual.activeSelf);
            Assert.AreEqual(220f, harness.Health.MaxHealth, TOLERANCE);
            Assert.IsFalse(harness.Animator.IsPlayingAttackAnimation);
        }

        [Test]
        public void EnemyPoolLifecycle_RepeatedOnGetAndOnRelease_DoesNotDuplicateReleaseEvents()
        {
            // Arrange
            EnemyConfigSO config = CreateEnemyConfig(maxHealth: 100f, expForKill: 10f, isShiny: false);
            EnemyHarness harness = CreateEnemyHarness(config, withShineController: false, withBloodVfx: false, runAwake: true, runEnable: true);

            int releasedCount = 0;
            harness.Enemy.OnCanBeReleased += delegate
            {
                releasedCount++;
            };

            // Act — Two full pool cycles
            harness.Enemy.OnGet();
            harness.FakeDeath.TriggerCompleted();
            harness.Enemy.OnRelease();
            harness.Enemy.OnGet();
            harness.FakeDeath.TriggerCompleted();

            // Assert — Exactly 1 event per cycle
            Assert.AreEqual(2, releasedCount);
        }

        [Test]
        public void EnemyReturnToPool_AfterRuntimeOverride_RestoresBaseConfigUnsubscribesAndEmitsOnCanBeReleased()
        {
            // Arrange
            EnemyConfigSO baseConfig = CreateEnemyConfig(maxHealth: 100f, expForKill: 10f, isShiny: false);
            EnemyConfigSO overrideConfig = CreateEnemyConfig(maxHealth: 350f, expForKill: 40f, isShiny: false);
            EnemyHarness harness = CreateEnemyHarness(baseConfig, withShineController: false, withBloodVfx: false, runAwake: true, runEnable: true);

            harness.Enemy.OnGet();
            harness.Enemy.ApplyRuntimeConfig(overrideConfig);

            int releasedCount = 0;
            harness.Enemy.OnCanBeReleased += delegate
            {
                releasedCount++;
            };

            // Act
            harness.Enemy.ReturnToPool();
            harness.FakeDeath.TriggerCompleted();

            // Assert
            Assert.AreSame(baseConfig, harness.Enemy.Config);
            Assert.AreEqual(100f, harness.Health.MaxHealth, TOLERANCE);
            Assert.AreEqual(1, releasedCount);
        }

        [Test]
        public void EnemyApplyKnockBack_RespectsFrozenStateAndForwardsWorldTargetAndDurationWhenUnfrozen()
        {
            // Arrange
            EnemyConfigSO config = CreateEnemyConfig(maxHealth: 100f, expForKill: 10f, isShiny: false);
            EnemyHarness harness = CreateEnemyHarness(config, withShineController: false, withBloodVfx: false, runAwake: true, runEnable: true);
            harness.Enemy.transform.position = new Vector3(10f, 0f, 20f);

            // Act 1 — Frozen enemy ignores knockback
            harness.TimeFreeze.SetFrozen(true, unfrozenTime: 0f);
            harness.Enemy.ApplyKnockBack(Vector3.right, 4f, 0.25f);
            int callsWhileFrozen = harness.Movement.KnockbackCallCount;

            // Act 2 — Unfrozen enemy delegates target = pos + dir * power
            harness.TimeFreeze.SetFrozen(false, unfrozenTime: 0f);
            harness.Enemy.ApplyKnockBack(Vector3.right, 4f, 0.25f);

            // Assert
            Assert.AreEqual(0, callsWhileFrozen);
            Assert.AreEqual(1, harness.Movement.KnockbackCallCount);
            Assert.AreEqual(14f, harness.Movement.LastKnockbackTarget.x, TOLERANCE);
            Assert.AreEqual(0f, harness.Movement.LastKnockbackTarget.y, TOLERANCE);
            Assert.AreEqual(20f, harness.Movement.LastKnockbackTarget.z, TOLERANCE);
            Assert.AreEqual(0.25f, harness.Movement.LastKnockbackDuration, TOLERANCE);
        }

        [Test]
        public void EnemyTakeDamage_NonLethalWithBloodVfx_SpawnsHemisphereNumberAtBloodPosAndPlaysBloodVfx()
        {
            // Arrange
            EnemyConfigSO config = CreateEnemyConfig(maxHealth: 100f, expForKill: 10f, isShiny: false);
            EnemyHarness harness = CreateEnemyHarness(config, withShineController: false, withBloodVfx: true, runAwake: true, runEnable: true);
            harness.Enemy.transform.position = new Vector3(5f, 0f, 5f);
            harness.BloodVfxPlayer.transform.position = new Vector3(5f, 1.4f, 5f);

            int bloodPlayedCount = 0;
            harness.BloodVfxPlayer.OnVFXFinished += delegate
            {
                bloodPlayedCount++;
            };

            // Act
            harness.Enemy.TakeDamage(35f);

            // Assert
            Assert.AreEqual(65f, harness.Health.CurrentHealth, TOLERANCE);
            Assert.IsTrue(harness.Health.IsAlive());
            Assert.AreEqual(1, harness.DamageSpawner.Spawns.Count);
            Assert.AreEqual(5f, harness.DamageSpawner.Spawns[0].Position.x, TOLERANCE);
            Assert.AreEqual(1.4f, harness.DamageSpawner.Spawns[0].Position.y, TOLERANCE);
            Assert.AreEqual(5f, harness.DamageSpawner.Spawns[0].Position.z, TOLERANCE);
            Assert.AreEqual(35f, harness.DamageSpawner.Spawns[0].Config.Damage, TOLERANCE);
            Assert.AreEqual(ShapeModes.Hemisphere, harness.DamageSpawner.Spawns[0].Config.SpawnShapeMode);
            Assert.AreEqual(1, bloodPlayedCount);
        }

        [Test]
        public void EnemyTakeDamage_NullBloodVfxFallsBackToRootPlusUp_AndLethalHitSkipsBloodVfx()
        {
            // Arrange
            EnemyConfigSO config = CreateEnemyConfig(maxHealth: 50f, expForKill: 10f, isShiny: false);
            EnemyHarness nullBloodHarness = CreateEnemyHarness(config, withShineController: false, withBloodVfx: false, runAwake: true, runEnable: true);
            nullBloodHarness.Enemy.transform.position = new Vector3(2f, 3f, 4f);

            EnemyHarness lethalHarness = CreateEnemyHarness(config, withShineController: false, withBloodVfx: true, runAwake: true, runEnable: true);
            int lethalBloodPlayedCount = 0;
            lethalHarness.BloodVfxPlayer.OnVFXFinished += delegate
            {
                lethalBloodPlayedCount++;
            };

            // Act
            nullBloodHarness.Enemy.TakeDamage(20f);
            lethalHarness.Enemy.TakeDamage(50f);

            // Assert
            Assert.AreEqual(1, nullBloodHarness.DamageSpawner.Spawns.Count);
            Assert.AreEqual(2f, nullBloodHarness.DamageSpawner.Spawns[0].Position.x, TOLERANCE);
            Assert.AreEqual(4f, nullBloodHarness.DamageSpawner.Spawns[0].Position.y, TOLERANCE);
            Assert.AreEqual(4f, nullBloodHarness.DamageSpawner.Spawns[0].Position.z, TOLERANCE);
            Assert.AreEqual(30f, nullBloodHarness.Health.CurrentHealth, TOLERANCE);

            Assert.IsFalse(lethalHarness.Health.IsAlive());
            Assert.AreEqual(0f, lethalHarness.Health.CurrentHealth, TOLERANCE);
            Assert.AreEqual(1, lethalHarness.DamageSpawner.Spawns.Count);
            Assert.AreEqual(0, lethalBloodPlayedCount);
        }

        [Test]
        public void EnemyTakeDamage_WhenAlreadyDead_IgnoresDamageWithoutDamageNumbersOrBlood()
        {
            // Arrange
            EnemyConfigSO config = CreateEnemyConfig(maxHealth: 40f, expForKill: 10f, isShiny: false);
            EnemyHarness harness = CreateEnemyHarness(config, withShineController: false, withBloodVfx: true, runAwake: true, runEnable: true);

            int bloodPlayedCount = 0;
            harness.BloodVfxPlayer.OnVFXFinished += delegate
            {
                bloodPlayedCount++;
            };

            harness.Enemy.TakeFullHpDamage();
            Assert.IsFalse(harness.Health.IsAlive());

            // Act
            harness.Enemy.TakeDamage(15f);

            // Assert
            Assert.AreEqual(0f, harness.Health.CurrentHealth, TOLERANCE);
            Assert.AreEqual(0, harness.DamageSpawner.Spawns.Count);
            Assert.AreEqual(0, bloodPlayedCount);
        }

        [Test]
        public void EnemyTakeFullHpDamage_KillsEnemyWithoutSpawningDamageNumbersOrBloodVfx()
        {
            // Arrange
            EnemyConfigSO config = CreateEnemyConfig(maxHealth: 80f, expForKill: 10f, isShiny: false);
            EnemyHarness harness = CreateEnemyHarness(config, withShineController: false, withBloodVfx: true, runAwake: true, runEnable: true);

            int bloodPlayedCount = 0;
            harness.BloodVfxPlayer.OnVFXFinished += delegate
            {
                bloodPlayedCount++;
            };

            // Act
            harness.Enemy.TakeFullHpDamage();

            // Assert
            Assert.IsFalse(harness.Health.IsAlive());
            Assert.AreEqual(0f, harness.Health.CurrentHealth, TOLERANCE);
            Assert.AreEqual(0, harness.DamageSpawner.Spawns.Count);
            Assert.AreEqual(0, bloodPlayedCount);
        }

        [TestCase(true, TestName = "EnemyDeathHandler_VfxThenAudioFinish_CompletesOnlyAfterBothEffects")]
        [TestCase(false, TestName = "EnemyDeathHandler_AudioThenVfxFinish_CompletesOnlyAfterBothEffects")]
        public void EnemyDeathHandler_OnNoHealth_DisablesPhysicsHidesVisualSpawnsExpAndCompletesAfterBothEffects(bool finishVfxFirst)
        {
            // Arrange
            EnemyConfigSO config = CreateEnemyConfig(maxHealth: 90f, expForKill: 45f, isShiny: false);
            DeathHandlerHarness harness = CreateDeathHandlerHarness(config);
            harness.Enemy.transform.position = new Vector3(12f, 1f, -4f);

            int completedCount = 0;
            harness.DeathHandler.OnCompleted += delegate
            {
                completedCount++;
            };

            // Act — Kill enemy
            harness.Enemy.TakeFullHpDamage();

            // Assert immediate death side effects before effects finish
            Assert.IsFalse(harness.Collider.enabled);
            Assert.IsTrue(harness.Rigidbody.isKinematic);
            Assert.IsFalse(harness.Visual.activeSelf);
            Assert.AreEqual(1, harness.ExpSpawner.Spawns.Count);
            Assert.AreEqual(12f, harness.ExpSpawner.Spawns[0].Position.x, TOLERANCE);
            Assert.AreEqual(1f, harness.ExpSpawner.Spawns[0].Position.y, TOLERANCE);
            Assert.AreEqual(-4f, harness.ExpSpawner.Spawns[0].Position.z, TOLERANCE);
            Assert.AreEqual(45f, harness.ExpSpawner.Spawns[0].ExpAmount, TOLERANCE);
            Assert.AreSame(harness.DeathClip, harness.AudioSource.clip);
            Assert.AreEqual(0, completedCount);

            // Act — Complete effects in parameterized order
            CompleteDeathEffectsInOrder(harness, finishVfxFirst, out int afterFirstCount);

            // Assert
            Assert.AreEqual(0, afterFirstCount);
            Assert.AreEqual(1, completedCount);
        }

        [Test]
        public void EnemyDeathHandler_DisableAndReEnable_ResetsEffectCounterAndPreventsDuplicateSubscriptions()
        {
            // Arrange
            EnemyConfigSO config = CreateEnemyConfig(maxHealth: 90f, expForKill: 30f, isShiny: false);
            DeathHandlerHarness harness = CreateDeathHandlerHarness(config);

            int completedCount = 0;
            harness.DeathHandler.OnCompleted += delegate
            {
                completedCount++;
            };

            // Cycle 1: kill and finish both
            harness.Enemy.TakeFullHpDamage();
            InvokeLifecycleMethod(harness.DeathVfxPlayer, "OnAllParticlesFinished");
            InvokeLifecycleMethod(harness.AudioClipPlayer, "OnAudioClipPlayFinished");
            Assert.AreEqual(1, completedCount);

            // Cycle 2: disable and re-enable (resetting Health + EnemyDeathHandler + visual)
            InvokeLifecycleMethod(harness.Enemy, "OnDisable");
            InvokeLifecycleMethod(harness.DeathHandler, "OnDisable");
            InvokeLifecycleMethod(harness.Health, "OnDisable");
            harness.Root.SetActive(false);

            harness.Root.SetActive(true);
            InvokeLifecycleMethod(harness.Health, "OnEnable");
            InvokeLifecycleMethod(harness.Enemy, "OnEnable");
            InvokeLifecycleMethod(harness.DeathHandler, "OnEnable");
            harness.Enemy.OnGet();
            Assert.IsTrue(harness.Collider.enabled);
            Assert.IsTrue(harness.Rigidbody.isKinematic);

            // Act — Kill again and finish only 1 of 2 effects, then the 2nd
            harness.Enemy.TakeFullHpDamage();
            InvokeLifecycleMethod(harness.DeathVfxPlayer, "OnAllParticlesFinished");
            int afterOneEffect = completedCount;
            InvokeLifecycleMethod(harness.AudioClipPlayer, "OnAudioClipPlayFinished");

            // Assert
            Assert.AreEqual(1, afterOneEffect);
            Assert.AreEqual(2, completedCount);
        }

        [Test]
        public void EnemyDeathHandler_WatchdogTimeout_LogsErrorAndForcesCompletionInLateUpdate()
        {
            // Arrange
            EnemyConfigSO config = CreateEnemyConfig(maxHealth: 90f, expForKill: 25f, isShiny: false);
            DeathHandlerHarness harness = CreateDeathHandlerHarness(config);

            int completedCount = 0;
            harness.DeathHandler.OnCompleted += delegate
            {
                completedCount++;
            };

            harness.Enemy.TakeFullHpDamage();

            // Act 1 — Before 5.0s timeout -> stays incomplete
            SetPrivateField(typeof(EnemyDeathHandler), harness.DeathHandler, "_deathStartedAt", Time.time - 4.9f);
            InvokeLifecycleMethod(harness.DeathHandler, "LateUpdate");
            int beforeTimeoutCount = completedCount;

            // Act 2 — Past 5.0s timeout -> logs expected error and forces completion once
            SetPrivateField(typeof(EnemyDeathHandler), harness.DeathHandler, "_deathStartedAt", Time.time - 5.1f);
            LogAssert.Expect(LogType.Error, "Enemy death presentation exceeded its watchdog; completing release.");
            InvokeLifecycleMethod(harness.DeathHandler, "LateUpdate");
            InvokeLifecycleMethod(harness.DeathHandler, "LateUpdate");

            // Assert
            Assert.AreEqual(0, beforeTimeoutCount);
            Assert.AreEqual(1, completedCount);
        }

        [Test]
        public void AuthoredAssets_BarrelExplosionAndDropConfigs_MatchApprovedValues()
        {
            // Arrange
            BarrelExplosionConfigSO explosionConfig = AssetDatabase.LoadAssetAtPath<BarrelExplosionConfigSO>(BARREL_EXPLOSION_CONFIG_PATH);
            DropAnimationConfiguration dropConfig = AssetDatabase.LoadAssetAtPath<DropAnimationConfiguration>(DROP_ANIM_CONFIG_PATH);

            // Act & Assert
            Assert.IsNotNull(explosionConfig);
            Assert.DoesNotThrow(() =>
            {
                explosionConfig.ValidateConfiguration();
            });
            Assert.AreEqual(2.5f, explosionConfig.ExplosionRadius, TOLERANCE);
            Assert.AreEqual(0.75f, explosionConfig.ActivationRange, TOLERANCE);

            Assert.IsNotNull(dropConfig);
            Assert.AreEqual(2f, dropConfig.ScatterRadius, TOLERANCE);
            Assert.AreEqual(0.6f, dropConfig.ScaleDuration, TOLERANCE);
            Assert.AreEqual(0.8f, dropConfig.ScatterDuration, TOLERANCE);
            Assert.AreEqual(1f, dropConfig.MinJumpPower, TOLERANCE);
            Assert.AreEqual(1.22f, dropConfig.MaxJumpPower, TOLERANCE);
        }

        [Test]
        public void AuthoredAssets_BarrelEnemyPrefab_HasRequiredComponentsAndSingleHitEventWithoutSelfInjector()
        {
            // Arrange
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BARREL_PREFAB_PATH);
            Assert.IsNotNull(prefab);

            // Act
            Enemy enemy = prefab.GetComponent<Enemy>();
            EnemyMovementController movement = prefab.GetComponent<EnemyMovementController>();
            EnemyCollisionsController collisions = prefab.GetComponent<EnemyCollisionsController>();
            BarrelEnemyExplosionController explosion = prefab.GetComponent<BarrelEnemyExplosionController>();
            BarrelEnemyDeathHandler deathHandler = prefab.GetComponent<BarrelEnemyDeathHandler>();
            BarrelEnemyAttackFeedback feedback = prefab.GetComponent<BarrelEnemyAttackFeedback>();
            EnemyDropHandler dropHandler = prefab.GetComponent<EnemyDropHandler>();
            EnemyTierShineController shine = prefab.GetComponent<EnemyTierShineController>();
            EnemyAnimator animator = prefab.GetComponentInChildren<EnemyAnimator>(true);
            Health health = prefab.GetComponent<Health>();
            BoxCollider boxCollider = prefab.GetComponent<BoxCollider>();
            Rigidbody rigidbody = prefab.GetComponent<Rigidbody>();
            AnimationClip attackClip = GetPrivateField<AnimationClip>(typeof(BarrelEnemyAttackFeedback), feedback, "_attackClip");

            // Assert
            Assert.IsNotNull(enemy);
            Assert.IsNotNull(enemy.Config);
            Assert.IsNotNull(movement);
            Assert.IsNotNull(collisions);
            Assert.IsNotNull(explosion);
            Assert.IsNotNull(deathHandler);
            Assert.IsNotNull(feedback);
            Assert.IsNotNull(dropHandler);
            Assert.IsNotNull(shine);
            Assert.IsNotNull(animator);
            Assert.IsNotNull(health);
            Assert.IsNotNull(boxCollider);
            Assert.IsNotNull(rigidbody);
            Assert.IsTrue(rigidbody.isKinematic);
            Assert.IsNotNull(prefab.GetComponentInChildren<CircularTelegraphIndicator>(true));
            Assert.IsNull(prefab.GetComponent("GameObjectSelfInjector"));
            Assert.IsNotNull(attackClip);
            Assert.AreEqual(1, CountAnimationEvents(attackClip, BarrelEnemyConstants.ATTACK_HIT_EVENT));
        }

        [Test]
        public void AuthoredAssets_AllBasicZombiePrefabs_HaveRequiredCombatDeathAndMovementWiring()
        {
            // Act & Assert
            VerifyAllZombiePrefabs();
        }

        [Test]
        public void Enemy_BeginMaterialization_DisablesColliderAndIgnoresDamageAndKnockbackUntilCompleted()
        {
            // Arrange
            EnemyHarness harness = CreateEnemyHarness(
                CreateEnemyConfig(maxHealth: 100f, expForKill: 10f, isShiny: false),
                withShineController: true,
                withBloodVfx: false,
                runAwake: true,
                runEnable: true,
                withCollider: true);
            BoxCollider collider = harness.Root.GetComponent<BoxCollider>();
            harness.Enemy.OnGet();

            // Act
            harness.Enemy.BeginMaterialization(1.5f);
            harness.Enemy.TakeDamage(50f);
            harness.Enemy.TakeFullHpDamage();
            harness.Enemy.ApplyKnockBack(Vector3.forward, 4f, 0.25f);

            // Assert — Completely untouchable while materializing
            Assert.IsTrue(harness.Enemy.IsMaterializing);
            Assert.IsFalse(collider.enabled);
            Assert.AreEqual(100f, harness.Health.CurrentHealth, TOLERANCE);
            Assert.AreEqual(0, harness.DamageSpawner.Spawns.Count);
            Assert.AreEqual(0, harness.Movement.KnockbackCallCount);

            // Act — Completing materialization re-enables combat interaction
            harness.Enemy.CompleteMaterialization();
            Assert.IsFalse(harness.Enemy.IsMaterializing);
            Assert.IsTrue(collider.enabled);
            Assert.AreEqual(1, harness.Movement.ResetCount);

            harness.Enemy.TakeDamage(25f);
            Assert.AreEqual(75f, harness.Health.CurrentHealth, TOLERANCE);
        }

        [Test]
        public void Enemy_BeginMaterializationWithoutCollider_ThrowsInvalidOperationException()
        {
            // Arrange
            EnemyHarness harness = CreateEnemyHarness(
                CreateEnemyConfig(maxHealth: 100f, expForKill: 10f, isShiny: false),
                withShineController: false,
                withBloodVfx: false,
                runAwake: true,
                runEnable: true,
                withCollider: false);
            harness.Enemy.OnGet();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(delegate
            {
                harness.Enemy.BeginMaterialization(1f);
            });
        }

        [Test]
        public void EnemyTierShineController_MaterializeLifecycle_UpdatesAndClearsPropertyBlockWithoutOverwritingShine()
        {
            // Arrange
            EnemyHarness harness = CreateEnemyHarness(
                CreateEnemyConfig(maxHealth: 100f, expForKill: 10f, isShiny: true),
                withShineController: true,
                withBloodVfx: false,
                runAwake: true,
                runEnable: true,
                withCollider: true);
            Renderer targetRenderer = harness.Visual.GetComponent<MeshRenderer>();
            MaterialPropertyBlock block = new MaterialPropertyBlock();

            // Act — Start materialization and advance progress halfway
            harness.ShineController.BeginMaterialization();
            harness.ShineController.SetMaterializeProgress(0.5f);
            targetRenderer.GetPropertyBlock(block, 0);

            // Assert
            Assert.IsTrue(harness.ShineController.IsMaterializing);
            Assert.AreEqual(1f, block.GetFloat(EnemyVisualConstants.MATERIALIZE_ENABLED_PROPERTY_ID), TOLERANCE);
            Assert.AreEqual(0.5f, block.GetFloat(EnemyVisualConstants.MATERIALIZE_PROGRESS_PROPERTY_ID), TOLERANCE);
            Assert.AreEqual(1f, block.GetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID), TOLERANCE);

            // Act — End materialization
            harness.ShineController.EndMaterialization();
            targetRenderer.GetPropertyBlock(block, 0);

            // Assert — Materialization cleared while Tier 2 shine remains intact
            Assert.IsFalse(harness.ShineController.IsMaterializing);
            Assert.AreEqual(0f, block.GetFloat(EnemyVisualConstants.MATERIALIZE_ENABLED_PROPERTY_ID), TOLERANCE);
            Assert.AreEqual(1f, block.GetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID), TOLERANCE);
        }

        [Test]
        public void NukeEffectService_ExecuteNuke_SkipsMaterializingEnemiesWhileKillingActiveOnes()
        {
            // Arrange
            EnemyHarness activeEnemy = CreateEnemyHarness(
                CreateEnemyConfig(maxHealth: 100f, expForKill: 10f, isShiny: false),
                withShineController: false,
                withBloodVfx: false,
                runAwake: true,
                runEnable: true,
                withCollider: true);
            EnemyHarness materializingEnemy = CreateEnemyHarness(
                CreateEnemyConfig(maxHealth: 100f, expForKill: 10f, isShiny: false),
                withShineController: true,
                withBloodVfx: false,
                runAwake: true,
                runEnable: true,
                withCollider: true);

            activeEnemy.Enemy.OnGet();
            materializingEnemy.Enemy.OnGet();
            activeEnemy.Root.transform.position = new Vector3(0f, 0f, 10f);
            materializingEnemy.Root.transform.position = new Vector3(1f, 0f, 10f);
            materializingEnemy.Enemy.BeginMaterialization(2f);

            GameObject camObj = TrackGameObject("NukeTestCamera", active: true);
            Camera cam = camObj.AddComponent<Camera>();
            camObj.transform.position = Vector3.zero;
            camObj.transform.rotation = Quaternion.identity;

            NukeConfigSO nukeConfig = AssetDatabase.LoadAssetAtPath<NukeConfigSO>(NUKE_CONFIG_PATH);
            Assert.IsNotNull(nukeConfig, $"Missing NukeConfigSO at {NUKE_CONFIG_PATH}");

            FakeDropSuppressor dropSuppressor = new FakeDropSuppressor();
            FakeBossEncounterService bossService = new FakeBossEncounterService();
            FakeActiveEnemiesProvider activeProvider = new FakeActiveEnemiesProvider(
                new[] { activeEnemy.Enemy, materializingEnemy.Enemy });

            Container nukeContainer = new ContainerBuilder()
                .AddSingleton(dropSuppressor, typeof(ICollectibleDropNotifier))
                .AddSingleton(dropSuppressor, typeof(ICollectibleDropSuppressor))
                .AddSingleton(activeProvider, typeof(IActiveEnemiesProvider))
                .AddSingleton(bossService, typeof(IBossEncounterService))
                .AddSingleton(cam)
                .Build();
            _createdContainers.Add(nukeContainer);

            GameObject nukeObj = TrackGameObject("NukeEffectServiceTest", active: false);
            NukeEffectService nukeService = nukeObj.AddComponent<NukeEffectService>();
            SetPrivateField(typeof(NukeEffectService), nukeService, "_config", nukeConfig);
            GameObjectInjector.InjectRecursive(nukeObj, nukeContainer);
            InvokeLifecycleMethod(nukeService, "Awake");

            // Act
            InvokeLifecycleMethod(nukeService, "ExecuteNuke");

            // Assert
            Assert.IsFalse(activeEnemy.Health.IsAlive());
            Assert.IsTrue(materializingEnemy.Health.IsAlive());
            Assert.AreEqual(100f, materializingEnemy.Health.CurrentHealth, TOLERANCE);
        }

        private void VerifyAllZombiePrefabs()
        {
            for (int i = 0; i < ZOMBIE_PREFAB_PATHS.Length; i++)
            {
                string path = ZOMBIE_PREFAB_PATHS[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Missing zombie prefab at {path}");

                Assert.IsNotNull(prefab.GetComponent<Enemy>(), $"Missing Enemy on {prefab.name}");
                Assert.IsNotNull(prefab.GetComponent<Enemy>().Config, $"Missing Config on {prefab.name}");
                Assert.IsNotNull(prefab.GetComponent<EnemyMovementController>(), $"Missing EnemyMovementController on {prefab.name}");
                Assert.IsNotNull(prefab.GetComponent<EnemyAttackController>(), $"Missing EnemyAttackController on {prefab.name}");
                Assert.IsNotNull(prefab.GetComponent<EnemyDeathHandler>(), $"Missing EnemyDeathHandler on {prefab.name}");
                Assert.IsNotNull(prefab.GetComponent<EnemyDropHandler>(), $"Missing EnemyDropHandler on {prefab.name}");
                Assert.IsNotNull(prefab.GetComponent<EnemyCollisionsController>(), $"Missing EnemyCollisionsController on {prefab.name}");
                Assert.IsNotNull(prefab.GetComponent<FlowFieldMovementController>(), $"Missing FlowFieldMovementController on {prefab.name}");
                Assert.IsNotNull(prefab.GetComponent<EnemyTierShineController>(), $"Missing EnemyTierShineController on {prefab.name}");
                Assert.IsNotNull(prefab.GetComponent<Health>(), $"Missing Health on {prefab.name}");
                Assert.IsNotNull(prefab.GetComponent<Collider>(), $"Missing Collider on {prefab.name}");

                Rigidbody rb = prefab.GetComponent<Rigidbody>();
                Assert.IsNotNull(rb, $"Missing Rigidbody on {prefab.name}");
                Assert.IsTrue(rb.isKinematic, $"Rigidbody must be kinematic on {prefab.name}");

                Assert.IsNotNull(prefab.GetComponentInChildren<EnemyAnimator>(true), $"Missing EnemyAnimator on {prefab.name}");
                Assert.IsNotNull(prefab.GetComponentInChildren<IAudioClipPlayer>(true), $"Missing IAudioClipPlayer on {prefab.name}");
                Assert.IsNull(prefab.GetComponent("GameObjectSelfInjector"), $"Unexpected GameObjectSelfInjector on {prefab.name}");
            }
        }

        private static void CompleteDeathEffectsInOrder(DeathHandlerHarness harness, bool finishVfxFirst, out int countAfterFirst)
        {
            int observed = 0;
            EventHandler counter = delegate
            {
                observed++;
            };
            harness.DeathHandler.OnCompleted += counter;

            if (finishVfxFirst)
            {
                InvokeLifecycleMethod(harness.DeathVfxPlayer, "OnAllParticlesFinished");
                countAfterFirst = observed;
                InvokeLifecycleMethod(harness.AudioClipPlayer, "OnAudioClipPlayFinished");
            }
            else
            {
                InvokeLifecycleMethod(harness.AudioClipPlayer, "OnAudioClipPlayFinished");
                countAfterFirst = observed;
                InvokeLifecycleMethod(harness.DeathVfxPlayer, "OnAllParticlesFinished");
            }

            harness.DeathHandler.OnCompleted -= counter;
        }

        private static int CountAnimationEvents(AnimationClip clip, string functionName)
        {
            int count = 0;
            AnimationEvent[] events = clip.events;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].functionName == functionName)
                {
                    count++;
                }
            }

            return count;
        }

        private EnemyHarness CreateEnemyHarness(
            EnemyConfigSO config,
            bool withShineController,
            bool withBloodVfx,
            bool runAwake,
            bool runEnable,
            bool withCollider = false)
        {
            GameObject root = TrackGameObject("TestEnemyRoot", active: false);
            if (withCollider)
            {
                root.AddComponent<BoxCollider>();
            }

            Health health = root.AddComponent<Health>();
            FakeCollisionsController collisions = root.AddComponent<FakeCollisionsController>();
            FakeMovementController movement = root.AddComponent<FakeMovementController>();
            FakeDeathSequence fakeDeath = root.AddComponent<FakeDeathSequence>();
            Enemy enemy = root.AddComponent<Enemy>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<Animator>();
            EnemyAnimator animator = visual.AddComponent<EnemyAnimator>();

            GameObject audioObj = new GameObject("Audio");
            audioObj.transform.SetParent(root.transform, false);
            audioObj.AddComponent<AudioSource>();
            AudioClipPlayer audioPlayer = audioObj.AddComponent<AudioClipPlayer>();

            EnemyTierShineController shineController = null;
            if (withShineController)
            {
                Shader shinyShader = Shader.Find(TIER2_SHINY_SHADER_NAME);
                Assert.IsNotNull(shinyShader, $"Shader '{TIER2_SHINY_SHADER_NAME}' not found.");
                Material shinyMaterial = new Material(shinyShader);
                _createdObjects.Add(shinyMaterial);

                MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = shinyMaterial;

                shineController = root.AddComponent<EnemyTierShineController>();
                SetPrivateField(typeof(EnemyTierShineController), shineController, "_targetRenderers", new Renderer[] { renderer });
                SetPrivateField(typeof(Enemy), enemy, "_tierShineController", shineController);
            }

            VFXPlayer bloodVfxPlayer = null;
            if (withBloodVfx)
            {
                GameObject bloodObj = new GameObject("BloodVfx");
                bloodObj.transform.SetParent(root.transform, false);
                bloodVfxPlayer = bloodObj.AddComponent<VFXPlayer>();
                SetPrivateField(typeof(Enemy), enemy, "_bloodVfxPlayer", bloodVfxPlayer);
            }

            SetPrivateField(typeof(Enemy), enemy, "<Config>k__BackingField", config);
            SetPrivateField(typeof(Enemy), enemy, "_visual", visual);
            SetPrivateField(typeof(EnemyAnimator), animator, "_enemy", enemy);

            FakeDamageNumbersSpawner damageSpawner = new FakeDamageNumbersSpawner();
            FakeTimeFreezeState timeFreeze = new FakeTimeFreezeState();

            Container container = new ContainerBuilder()
                .AddSingleton(damageSpawner, typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>))
                .AddSingleton(timeFreeze, typeof(ITimeFreezeState))
                .Build();
            _createdContainers.Add(container);
            GameObjectInjector.InjectRecursive(root, container);

            if (runAwake)
            {
                InvokeLifecycleMethod(animator, "Awake");
                InvokeLifecycleMethod(audioPlayer, "Awake");
                InvokeLifecycleMethod(enemy, "Awake");
            }

            if (runEnable)
            {
                root.SetActive(true);
                InvokeLifecycleMethod(health, "OnEnable");
                InvokeLifecycleMethod(enemy, "OnEnable");
            }

            return new EnemyHarness(
                root,
                enemy,
                health,
                collisions,
                movement,
                fakeDeath,
                audioPlayer,
                animator,
                visual,
                shineController,
                bloodVfxPlayer,
                damageSpawner,
                timeFreeze);
        }

        private DeathHandlerHarness CreateDeathHandlerHarness(EnemyConfigSO config)
        {
            GameObject root = TrackGameObject("TestDeathEnemyRoot", active: false);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            Rigidbody rb = root.AddComponent<Rigidbody>();
            Health health = root.AddComponent<Health>();
            root.AddComponent<FakeCollisionsController>();
            root.AddComponent<FakeMovementController>();
            EnemyDeathHandler deathHandler = root.AddComponent<EnemyDeathHandler>();
            Enemy enemy = root.GetComponent<Enemy>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<Animator>();
            EnemyAnimator animator = visual.AddComponent<EnemyAnimator>();

            GameObject vfxObj = new GameObject("DeathVfx");
            vfxObj.transform.SetParent(root.transform, false);
            ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.duration = 0.5f;
            VFXPlayer deathVfxPlayer = vfxObj.AddComponent<VFXPlayer>();

            GameObject audioObj = new GameObject("Audio");
            audioObj.transform.SetParent(root.transform, false);
            AudioSource audioSource = audioObj.AddComponent<AudioSource>();
            AudioClipPlayer audioClipPlayer = audioObj.AddComponent<AudioClipPlayer>();

            AudioClip deathClip = AudioClip.Create("TestDeathClip", 4410, 1, 44100, false);
            _createdObjects.Add(deathClip);
            AudioClipConfig clipConfig = new AudioClipConfig();
            SetPrivateField(typeof(AudioClipConfig), clipConfig, "_audioClip", deathClip);
            SetPrivateField(typeof(AudioClipConfig), clipConfig, "_volume", 1f);
            SetPrivateField(typeof(AudioClipConfig), clipConfig, "_pitch", 1f);
            SetPrivateField(typeof(AudioClipConfig), clipConfig, "_loop", false);

            AudioClipPlayer.AudioClipPlayerConfig playerConfig = new AudioClipPlayer.AudioClipPlayerConfig();
            SetPrivateField(typeof(AudioClipPlayer.AudioClipPlayerConfig), playerConfig, "_name", "Death");
            SetPrivateField(typeof(AudioClipPlayer.AudioClipPlayerConfig), playerConfig, "_clipVariants", new AudioClipConfig[] { clipConfig });
            SetPrivateField(typeof(AudioClipPlayer), audioClipPlayer, "_audioClipPlayerConfigs", new AudioClipPlayer.AudioClipPlayerConfig[] { playerConfig });

            SetPrivateField(typeof(Enemy), enemy, "<Config>k__BackingField", config);
            SetPrivateField(typeof(Enemy), enemy, "_visual", visual);
            SetPrivateField(typeof(EnemyAnimator), animator, "_enemy", enemy);
            SetPrivateField(typeof(EnemyDeathHandler), deathHandler, "_visual", visual);
            SetPrivateField(typeof(EnemyDeathHandler), deathHandler, "_deathVfxPlayer", deathVfxPlayer);

            FakeDamageNumbersSpawner damageSpawner = new FakeDamageNumbersSpawner();
            FakeExpParticleSpawner expSpawner = new FakeExpParticleSpawner();
            FakeTimeFreezeState timeFreeze = new FakeTimeFreezeState();

            Container container = new ContainerBuilder()
                .AddSingleton(damageSpawner, typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>))
                .AddSingleton(expSpawner, typeof(IInWorldSpaceSpawner<ExpParticleSpawner, float>))
                .AddSingleton(timeFreeze, typeof(ITimeFreezeState))
                .Build();
            _createdContainers.Add(container);
            GameObjectInjector.InjectRecursive(root, container);

            root.SetActive(true);

            InvokeLifecycleMethod(animator, "Awake");
            InvokeLifecycleMethod(deathVfxPlayer, "Awake");
            InvokeLifecycleMethod(audioClipPlayer, "Awake");
            InvokeLifecycleMethod(enemy, "Awake");
            InvokeLifecycleMethod(deathHandler, "Awake");

            InvokeLifecycleMethod(health, "OnEnable");
            InvokeLifecycleMethod(enemy, "OnEnable");
            InvokeLifecycleMethod(deathHandler, "OnEnable");
            InvokeLifecycleMethod(animator, "OnEnable");

            return new DeathHandlerHarness(
                root,
                enemy,
                deathHandler,
                health,
                collider,
                rb,
                visual,
                deathVfxPlayer,
                audioClipPlayer,
                audioSource,
                deathClip,
                expSpawner);
        }

        private EnemyConfigSO CreateEnemyConfig(float maxHealth, float expForKill, bool isShiny)
        {
            EnemyConfigSO config = ScriptableObject.CreateInstance<EnemyConfigSO>();
            config.MaxHealth = maxHealth;
            config.MovementSpeed = 4f;
            config.RotationSpeed = 8f;
            config.Acceleration = 10f;
            config.Damage = 12f;
            config.ExpForKill = expForKill;
            SetPrivateField(typeof(EnemyConfigSO), config, "_isShiny", isShiny);
            _createdObjects.Add(config);
            return config;
        }

        private GameObject TrackGameObject(string name, bool active)
        {
            GameObject go = new GameObject(name);
            go.SetActive(active);
            _createdObjects.Add(go);
            return go;
        }

        private static T GetPrivateField<T>(Type ownerType, object target, string fieldName) where T : class
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}' on {ownerType.Name}");
            return field.GetValue(target) as T;
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

            Assert.Fail($"Method '{methodName}' not found on {target.GetType().Name}.");
        }

        private readonly struct EnemyHarness
        {
            public readonly GameObject Root;
            public readonly Enemy Enemy;
            public readonly Health Health;
            public readonly FakeCollisionsController Collisions;
            public readonly FakeMovementController Movement;
            public readonly FakeDeathSequence FakeDeath;
            public readonly AudioClipPlayer AudioPlayer;
            public readonly EnemyAnimator Animator;
            public readonly GameObject Visual;
            public readonly EnemyTierShineController ShineController;
            public readonly VFXPlayer BloodVfxPlayer;
            public readonly FakeDamageNumbersSpawner DamageSpawner;
            public readonly FakeTimeFreezeState TimeFreeze;

            public EnemyHarness(
                GameObject root,
                Enemy enemy,
                Health health,
                FakeCollisionsController collisions,
                FakeMovementController movement,
                FakeDeathSequence fakeDeath,
                AudioClipPlayer audioPlayer,
                EnemyAnimator animator,
                GameObject visual,
                EnemyTierShineController shineController,
                VFXPlayer bloodVfxPlayer,
                FakeDamageNumbersSpawner damageSpawner,
                FakeTimeFreezeState timeFreeze)
            {
                Root = root;
                Enemy = enemy;
                Health = health;
                Collisions = collisions;
                Movement = movement;
                FakeDeath = fakeDeath;
                AudioPlayer = audioPlayer;
                Animator = animator;
                Visual = visual;
                ShineController = shineController;
                BloodVfxPlayer = bloodVfxPlayer;
                DamageSpawner = damageSpawner;
                TimeFreeze = timeFreeze;
            }
        }

        private readonly struct DeathHandlerHarness
        {
            public readonly GameObject Root;
            public readonly Enemy Enemy;
            public readonly EnemyDeathHandler DeathHandler;
            public readonly Health Health;
            public readonly BoxCollider Collider;
            public readonly Rigidbody Rigidbody;
            public readonly GameObject Visual;
            public readonly VFXPlayer DeathVfxPlayer;
            public readonly AudioClipPlayer AudioClipPlayer;
            public readonly AudioSource AudioSource;
            public readonly AudioClip DeathClip;
            public readonly FakeExpParticleSpawner ExpSpawner;

            public DeathHandlerHarness(
                GameObject root,
                Enemy enemy,
                EnemyDeathHandler deathHandler,
                Health health,
                BoxCollider collider,
                Rigidbody rigidbody,
                GameObject visual,
                VFXPlayer deathVfxPlayer,
                AudioClipPlayer audioClipPlayer,
                AudioSource audioSource,
                AudioClip deathClip,
                FakeExpParticleSpawner expSpawner)
            {
                Root = root;
                Enemy = enemy;
                DeathHandler = deathHandler;
                Health = health;
                Collider = collider;
                Rigidbody = rigidbody;
                Visual = visual;
                DeathVfxPlayer = deathVfxPlayer;
                AudioClipPlayer = audioClipPlayer;
                AudioSource = audioSource;
                DeathClip = deathClip;
                ExpSpawner = expSpawner;
            }
        }

        private sealed class FakeCollisionsController : MonoBehaviour, ICollisionsController
        {
            public event EventHandler<CollisionEventArgs> OnCollisionWithPlayer
            {
                add { }
                remove { }
            }
        }

        private sealed class FakeMovementController : MonoBehaviour, IMovementController
        {
            public int KnockbackCallCount { get; private set; }
            public Vector3 LastKnockbackTarget { get; private set; }
            public float LastKnockbackDuration { get; private set; }
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
                KnockbackCallCount++;
                LastKnockbackTarget = pos;
                LastKnockbackDuration = time;
            }

            public void SetMovementSpeedMultiplier(float multiplier)
            {
            }
        }

        private sealed class FakeDeathSequence : MonoBehaviour, INeedToCompleteBeforeDisable
        {
            public event EventHandler OnCompleted;

            public void TriggerCompleted()
            {
                OnCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class FakeTimeFreezeState : ITimeFreezeState
        {
            public bool IsFrozen { get; private set; }
            public float UnfrozenTime { get; private set; }

            public event Action<bool> OnFrozenChanged;

            public void SetFrozen(bool isFrozen, float unfrozenTime)
            {
                IsFrozen = isFrozen;
                UnfrozenTime = unfrozenTime;
                OnFrozenChanged?.Invoke(isFrozen);
            }
        }

        private sealed class FakeDamageNumbersSpawner : IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>
        {
            public List<RecordedDamageSpawn> Spawns { get; } = new List<RecordedDamageSpawn>();
            public uint CurrentlySpawnedObjectsCount { get { return 0; } }

            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public void Spawn(Vector3 pos, DamageNubmersSpawnerConfig specificConfig, int count = 1)
            {
                Spawns.Add(new RecordedDamageSpawn(pos, specificConfig));
            }
        }

        private readonly struct RecordedDamageSpawn
        {
            public readonly Vector3 Position;
            public readonly DamageNubmersSpawnerConfig Config;

            public RecordedDamageSpawn(Vector3 position, DamageNubmersSpawnerConfig config)
            {
                Position = position;
                Config = config;
            }
        }

        private sealed class FakeExpParticleSpawner : IInWorldSpaceSpawner<ExpParticleSpawner, float>
        {
            public List<RecordedExpSpawn> Spawns { get; } = new List<RecordedExpSpawn>();
            public uint CurrentlySpawnedObjectsCount { get { return 0; } }

            public event EventHandler OnSpawnedEntityReleased
            {
                add { }
                remove { }
            }

            public void Spawn(Vector3 pos, float specificConfig, int count = 1)
            {
                Spawns.Add(new RecordedExpSpawn(pos, specificConfig));
            }
        }

        private readonly struct RecordedExpSpawn
        {
            public readonly Vector3 Position;
            public readonly float ExpAmount;

            public RecordedExpSpawn(Vector3 position, float expAmount)
            {
                Position = position;
                ExpAmount = expAmount;
            }
        }

        private sealed class FakeDropSuppressor : ICollectibleDropSuppressor, ICollectibleDropNotifier
        {
            public bool AreDropsSuppressed { get; private set; }

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

            public void SpawnCollectible(GameObject prefab, Vector3 spawnPos, Vector3 targetPos)
            {
            }

            public void BeginDropSuppression()
            {
                AreDropsSuppressed = true;
            }

            public void EndDropSuppression()
            {
                AreDropsSuppressed = false;
            }
        }

        private sealed class FakeBossEncounterService : IBossEncounterService
        {
            public bool IsAnyBossActive { get { return false; } }
            public int RequiredTowersCount { get { return 0; } }
            public int DefeatedTowersCount { get { return 0; } }
            public int RemainingTowersCount { get { return 0; } }

            public event Action<int, int> OnTowerDefeated
            {
                add { }
                remove { }
            }

            public void RegisterTower(IEncounterArenaController towerArena) { }
            public void UnregisterTower(IEncounterArenaController towerArena) { }
            public void RegisterGolemStatue(IGolemStatue statue) { }
            public void UnregisterGolemStatue(IGolemStatue statue) { }
            public void NotifyEncounterEngaged(IBossHUDSource source, string displayName) { }
            public void NotifyEncounterDisengaged() { }
            public void NotifyTowerDefeated(IEncounterArenaController towerArena) { }

            public bool IsPositionWithinAnyTowerArena(Vector3 worldPosition, float extraBuffer = 0f)
            {
                return false;
            }

            public void SpawnGolemBoss(Vector3? spawnPosition = null) { }

            public int CollectEngagedBosses(List<IBossCombatTarget> buffer)
            {
                return 0;
            }
        }

        private sealed class FakeActiveEnemiesProvider : IActiveEnemiesProvider
        {
            private readonly Enemy[] _enemies;

            public FakeActiveEnemiesProvider(Enemy[] enemies)
            {
                _enemies = enemies;
            }

            public int CopyActiveEnemies(List<Enemy> buffer)
            {
                buffer.Clear();
                for (int i = 0; i < _enemies.Length; i++)
                {
                    buffer.Add(_enemies[i]);
                }

                return buffer.Count;
            }
        }
    }
}
