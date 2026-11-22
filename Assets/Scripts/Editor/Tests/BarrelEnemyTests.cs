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
using Assets.Scripts.Enemies.Constants;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Indicators;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.LevelSystem.Exp;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Skills;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using DG.Tweening;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class BarrelEnemyTests
    {
        private const float TOLERANCE = 0.0001f;
        private const string BARREL_PREFAB_PATH = "Assets/Prefabs/Enemies/Basic/BarrelEnemy.prefab";
        private const string BARREL_ANIM_CONTROLLER_PATH = "Assets/Animations/Enemies/Basic/Barrel/BarrelAnimationController.controller";
        private const string BARREL_ATTACK_CLIP_PATH = "Assets/Animations/Enemies/Basic/Barrel/Zombie Attack.anim";
        private const string TIER2_SHINY_SHADER_NAME = "Custom/Tier2ShinyEnemy";
        private const string URP_LIT_SHADER_NAME = "Universal Render Pipeline/Lit";
        private static readonly Vector3 ISOLATED_ORIGIN = new Vector3(36000f, 1200f, 36000f);

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();
        private bool _previousDotweenInitialized;

        [SetUp]
        public void SetUp()
        {
            FieldInfo initializedField = typeof(DOTween).GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic);
            _previousDotweenInitialized = (bool)initializedField.GetValue(null);
            initializedField.SetValue(null, true);
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            DOTween.Clear(true);

            FieldInfo initializedField = typeof(DOTween).GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic);
            initializedField.SetValue(null, _previousDotweenInitialized);

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

            CircularTelegraphIndicator[] strayIndicators = UnityEngine.Object.FindObjectsByType<CircularTelegraphIndicator>();
            for (int i = 0; i < strayIndicators.Length; i++)
            {
                if (strayIndicators[i] != null && strayIndicators[i].gameObject.scene.IsValid())
                {
                    UnityEngine.Object.DestroyImmediate(strayIndicators[i].gameObject);
                }
            }

            Physics.SyncTransforms();
        }

        [TestCase(2.5f, 0.75f, TestName = "BarrelExplosionConfig_ValidStrictRanges_Succeeds")]
        [TestCase(2.0f, 2.0f, TestName = "BarrelExplosionConfig_ActivationEqualsExplosion_Succeeds")]
        public void BarrelExplosionConfig_ValidateConfiguration_AcceptsValidPositiveFiniteRanges(float explosionRadius, float activationRange)
        {
            // Arrange
            BarrelExplosionConfigSO config = CreateExplosionConfig(explosionRadius, activationRange);

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                config.ValidateConfiguration();
            });
        }

        [TestCase(0f, 0.5f, TestName = "BarrelExplosionConfig_ZeroExplosionRadius_Throws")]
        [TestCase(-1f, 0.5f, TestName = "BarrelExplosionConfig_NegativeExplosionRadius_Throws")]
        [TestCase(float.NaN, 0.5f, TestName = "BarrelExplosionConfig_NaNExplosionRadius_Throws")]
        [TestCase(float.PositiveInfinity, 0.5f, TestName = "BarrelExplosionConfig_InfiniteExplosionRadius_Throws")]
        [TestCase(2f, 0f, TestName = "BarrelExplosionConfig_ZeroActivationRange_Throws")]
        [TestCase(2f, -0.25f, TestName = "BarrelExplosionConfig_NegativeActivationRange_Throws")]
        [TestCase(2f, float.NaN, TestName = "BarrelExplosionConfig_NaNActivationRange_Throws")]
        [TestCase(2f, float.PositiveInfinity, TestName = "BarrelExplosionConfig_InfiniteActivationRange_Throws")]
        [TestCase(2f, 2.01f, TestName = "BarrelExplosionConfig_ActivationExceedsExplosion_Throws")]
        public void BarrelExplosionConfig_ValidateConfiguration_RejectsInvalidRanges(float explosionRadius, float activationRange)
        {
            // Arrange
            BarrelExplosionConfigSO config = CreateExplosionConfig(explosionRadius, activationRange);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                config.ValidateConfiguration();
            });
        }

        [Test]
        public void BarrelExplosionController_AwakeAndStartValidation_ThrowsOnMissingRequiredReferencesAndLogsNullFeedback()
        {
            // Arrange — Missing _warning throws InvalidOperationException in Awake
            GameObject invalidAwakeGo = new GameObject("InvalidBarrelAwake");
            invalidAwakeGo.SetActive(false);
            _createdObjects.Add(invalidAwakeGo);
            invalidAwakeGo.AddComponent<Health>();
            invalidAwakeGo.AddComponent<EnemyMovementController>();
            invalidAwakeGo.AddComponent<EnemyCollisionsController>();
            invalidAwakeGo.AddComponent<Enemy>();
            BarrelEnemyExplosionController invalidExplosion = invalidAwakeGo.AddComponent<BarrelEnemyExplosionController>();
            SetPrivateField(typeof(BarrelEnemyExplosionController), invalidExplosion, "_config", CreateExplosionConfig(2.5f, 0.75f));

            Assert.Throws<InvalidOperationException>(() =>
            {
                InvokeLifecycleMethod(invalidExplosion, "Awake");
            });

            // Arrange — Null _attackFeedback logs error without throwing, and missing player BoxCollider throws in Start
            BarrelPrefabHarness missingPlayerHarness = InstantiateBarrelPrefabHarness(
                ISOLATED_ORIGIN,
                CreatePlainObject("PlayerWithoutCollider"),
                withFeedback: false);
            missingPlayerHarness.Root.SetActive(true);
            missingPlayerHarness.UnityAnimator.Rebind();
            missingPlayerHarness.UnityAnimator.Update(0f);
            InvokeOptionalLifecycleMethod(missingPlayerHarness.EnemyAnimator, "Awake");
            InvokeOptionalLifecycleMethod(missingPlayerHarness.Enemy, "Awake");
            InvokeOptionalLifecycleMethod(missingPlayerHarness.Movement, "Awake");
            InvokeOptionalLifecycleMethod(missingPlayerHarness.Collisions, "Awake");

            LogAssert.Expect(LogType.Error, "Barrel attack feedback is not assigned on the prefab.");
            InvokeOptionalLifecycleMethod(missingPlayerHarness.Explosion, "Awake");
            InvokeOptionalLifecycleMethod(missingPlayerHarness.Explosion, "OnEnable");

            Assert.Throws<InvalidOperationException>(() =>
            {
                InvokeLifecycleMethod(missingPlayerHarness.Explosion, "Start");
            });
        }

        [Test]
        public void BarrelExplosionController_PlayerEntersActivationRange_PrimesWarningSuppressesPursuitAndDetonatesOnHitFrame()
        {
            // Arrange — Player body within ActivationRange (0.75m); Barrel Config.Damage = 18
            FakePlayerTarget player = CreatePlayerTarget(ISOLATED_ORIGIN + new Vector3(0f, 0f, 0.7f), new Vector3(0.5f, 1f, 0.5f));
            BarrelPrefabHarness harness = InstantiateBarrelPrefabHarness(ISOLATED_ORIGIN, player.gameObject, withFeedback: true);
            InitializeBarrelHarness(harness);
            harness.Enemy.OnGet();

            int completedCount = 0;
            harness.Enemy.OnCanBeReleased += delegate
            {
                completedCount++;
            };

            // Act 1 — Collision triggers Priming
            InvokeLifecycleMethod(harness.Collisions, "HandleCollisionsCheck");

            bool warningActiveDuringPriming = harness.Warning.gameObject.activeSelf;
            bool triggerSetDuringPriming = harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_TRIGGER_ATTACK);
            bool detonatedBeforeHitFrame = harness.Explosion.HasDetonated;
            float playerDamageBeforeHit = player.TotalDamage;

            // Act 2 — Start and Hit frame fire -> explosion VFX, player damage, hide body, self-kill, completion
            harness.EnemyAnimator.Call_OnAttackAnimationStart();
            harness.EnemyAnimator.Call_OnAttackHitFrame();
            // Stray OnAttackEnded after detonation must be ignored
            harness.EnemyAnimator.Call_OnAttackAnimationEnd();

            // Assert
            Assert.IsTrue(warningActiveDuringPriming);
            Assert.IsTrue(triggerSetDuringPriming);
            Assert.IsFalse(detonatedBeforeHitFrame);
            Assert.AreEqual(0f, playerDamageBeforeHit, TOLERANCE);

            Assert.IsTrue(harness.Explosion.HasDetonated);
            Assert.AreEqual(1, harness.ExplosionVfxPool.PlayCalls.Count);
            Assert.AreEqual(2.5f, harness.ExplosionVfxPool.PlayCalls[0].Radius, TOLERANCE);
            Assert.AreEqual(18f, player.TotalDamage, TOLERANCE);
            Assert.IsFalse(harness.Health.IsAlive());
            Assert.IsFalse(harness.Visual.activeSelf);
            Assert.IsFalse(harness.Collider.enabled);
            Assert.IsFalse(harness.Warning.gameObject.activeSelf);
            Assert.AreEqual(1, harness.ExpSpawner.Spawns.Count);
            Assert.AreEqual(1, completedCount);
        }

        [Test]
        public void BarrelExplosionController_PlayerEscapesExplosionRadiusBeforeHitFrame_DetonatesWithZeroPlayerDamage()
        {
            // Arrange
            FakePlayerTarget player = CreatePlayerTarget(ISOLATED_ORIGIN + new Vector3(0f, 0f, 0.6f), new Vector3(0.5f, 1f, 0.5f));
            BarrelPrefabHarness harness = InstantiateBarrelPrefabHarness(ISOLATED_ORIGIN, player.gameObject, withFeedback: true);
            InitializeBarrelHarness(harness);
            harness.Enemy.OnGet();

            InvokeLifecycleMethod(harness.Collisions, "HandleCollisionsCheck");
            harness.EnemyAnimator.Call_OnAttackAnimationStart();

            // Act — Player escapes well beyond 2.5m ExplosionRadius before hit frame
            player.transform.position = ISOLATED_ORIGIN + new Vector3(0f, 0f, 5.0f);
            Physics.SyncTransforms();
            harness.EnemyAnimator.Call_OnAttackHitFrame();

            // Assert — Barrel still detonates and dies, but deals zero damage to player
            Assert.IsTrue(harness.Explosion.HasDetonated);
            Assert.AreEqual(1, harness.ExplosionVfxPool.PlayCalls.Count);
            Assert.AreEqual(0f, player.TotalDamage, TOLERANCE);
            Assert.IsFalse(harness.Health.IsAlive());
        }

        [Test]
        public void BarrelExplosionController_FrozenOrNonBodyCollider_DoesNotPrime()
        {
            // Arrange
            FakePlayerTarget player = CreatePlayerTarget(ISOLATED_ORIGIN + new Vector3(0f, 0f, 0.6f), new Vector3(0.5f, 1f, 0.5f));
            GameObject decoyPlayerChild = CreatePlayerChildCollider(player.gameObject, new Vector3(0.5f, 1f, 0.5f));
            BarrelPrefabHarness harness = InstantiateBarrelPrefabHarness(ISOLATED_ORIGIN, player.gameObject, withFeedback: true);
            InitializeBarrelHarness(harness);
            harness.Enemy.OnGet();

            // Act 1 — Non-body child collider is ignored
            InvokeLifecycleMethodWithCollider(harness.Explosion, "OnPlayerContact", decoyPlayerChild.GetComponent<Collider>());
            bool warningAfterDecoy = harness.Warning.gameObject.activeSelf;

            // Act 2 — Frozen state ignores valid player body collision
            harness.TimeFreeze.SetFrozen(true, unfrozenTime: 0f);
            InvokeLifecycleMethodWithCollider(harness.Explosion, "OnPlayerContact", player.BodyCollider);

            // Assert
            Assert.IsFalse(warningAfterDecoy);
            Assert.IsFalse(harness.Warning.gameObject.activeSelf);
            Assert.IsFalse(harness.Explosion.HasDetonated);
        }

        [Test]
        public void BarrelExplosionController_PrimingWatchdog_LogsErrorAndCancelsToApproachingAfterContractComplete()
        {
            // Arrange — Prime at UnfrozenTime = 2.0s; watchdog fires at >= 2.0 + 3.5 = 5.5s
            FakePlayerTarget player = CreatePlayerTarget(ISOLATED_ORIGIN + new Vector3(0f, 0f, 0.6f), new Vector3(0.5f, 1f, 0.5f));
            BarrelPrefabHarness harness = InstantiateBarrelPrefabHarness(ISOLATED_ORIGIN, player.gameObject, withFeedback: true);
            InitializeBarrelHarness(harness);
            harness.Enemy.OnGet();

            harness.TimeFreeze.SetFrozen(false, unfrozenTime: 2.0f);
            InvokeLifecycleMethod(harness.Collisions, "HandleCollisionsCheck");
            Assert.IsTrue(harness.Warning.gameObject.activeSelf);

            // Act 1 — Just before 3.5s -> stays priming
            harness.TimeFreeze.SetFrozen(false, unfrozenTime: 2.0f + EnemyAnimationConstants.MAX_ATTACK_ANIMATION_DURATION - 0.01f);
            InvokeLifecycleMethod(harness.Explosion, "Update");
            bool warningBeforeDeadline = harness.Warning.gameObject.activeSelf;

            // Act 2 — Reach 3.5s -> logs error, cancels priming, contracts telegraph, and allows re-priming after contract completes
            harness.TimeFreeze.SetFrozen(false, unfrozenTime: 2.0f + EnemyAnimationConstants.MAX_ATTACK_ANIMATION_DURATION);
            LogAssert.Expect(LogType.Error, "Barrel Attack failed to deliver its hit event; canceling priming.");
            InvokeLifecycleMethod(harness.Explosion, "Update");
            DOTween.CompleteAll(withCallbacks: true);
            bool warningAfterContract = harness.Warning.gameObject.activeSelf;

            // Act 3 — Player still in range can re-prime cleanly once back in Approaching
            InvokeLifecycleMethod(harness.Collisions, "HandleCollisionsCheck");

            // Assert
            Assert.IsTrue(warningBeforeDeadline);
            Assert.IsFalse(warningAfterContract);
            Assert.IsTrue(harness.Warning.gameObject.activeSelf);
        }

        [Test]
        public void BarrelExplosionController_OnDisableAndOnDestroyWithDetachedWarning_HideAndHandleDetachedWarningSafely()
        {
            // Arrange
            FakePlayerTarget player = CreatePlayerTarget(ISOLATED_ORIGIN + new Vector3(0f, 0f, 0.6f), new Vector3(0.5f, 1f, 0.5f));
            BarrelPrefabHarness harness = InstantiateBarrelPrefabHarness(ISOLATED_ORIGIN, player.gameObject, withFeedback: true);
            InitializeBarrelHarness(harness);
            harness.Enemy.OnGet();
            Transform expectedRootTransform = harness.Root.transform;

            InvokeLifecycleMethod(harness.Collisions, "HandleCollisionsCheck");
            Assert.IsNull(harness.Warning.transform.parent);
            Assert.IsTrue(harness.Warning.gameObject.activeSelf);

            // Act 1 — OnDisable hides reusable warning and restores its parent
            InvokeLifecycleMethod(harness.Explosion, "OnDisable");
            bool activeAfterDisable = harness.Warning.gameObject.activeSelf;
            Transform parentAfterDisable = harness.Warning.transform.parent;

            // Act 2 — Re-prime so warning is detached again, then destroy root safely
            InvokeLifecycleMethod(harness.Explosion, "OnEnable");
            InvokeLifecycleMethod(harness.Collisions, "HandleCollisionsCheck");
            Assert.IsNull(harness.Warning.transform.parent);

            // Assert — OnDestroy executes without throwing even when warning is detached
            Assert.DoesNotThrow(() =>
            {
                UnityEngine.Object.DestroyImmediate(harness.Root);
            });
            Assert.IsFalse(activeAfterDisable);
            Assert.AreSame(expectedRootTransform, parentAfterDisable);
        }

        [Test]
        public void BarrelDeathHandler_KilledWhilePriming_ContractsWarningPlaysVfxAndAudioAndCompletesOnlyWhenAllFinish()
        {
            // Arrange — Configure Death audio clip + particle system on Barrel harness so all 3 gates (warning, VFX, audio) must finish
            FakePlayerTarget player = CreatePlayerTarget(ISOLATED_ORIGIN + new Vector3(0f, 0f, 0.6f), new Vector3(0.5f, 1f, 0.5f));
            BarrelPrefabHarness harness = InstantiateBarrelPrefabHarness(ISOLATED_ORIGIN, player.gameObject, withFeedback: true);
            AudioClip deathClip = ConfigureDeathClip(harness.AudioClipPlayer);
            ConfigureVfxDuration(harness.DeathVfxPlayer, duration: 0.5f);
            InitializeBarrelHarness(harness);

            int completedCount = 0;
            harness.DeathHandler.OnCompleted += delegate
            {
                completedCount++;
            };

            // Prime first so warning is active
            InvokeLifecycleMethod(harness.Collisions, "HandleCollisionsCheck");
            Assert.IsTrue(harness.Warning.gameObject.activeSelf);

            // Act 1 — Kill barrel before detonation
            harness.Enemy.TakeDamage(500f);
            Assert.IsFalse(harness.Explosion.HasDetonated);
            Assert.IsFalse(harness.Visual.activeSelf);
            Assert.IsFalse(harness.Collider.enabled);
            Assert.AreSame(deathClip, harness.AudioSource.clip);

            // LateUpdate before all 3 finish -> stays incomplete
            InvokeLifecycleMethod(harness.DeathHandler, "LateUpdate");
            int afterWarningPending = completedCount;

            // Complete warning contraction + VFX, still waiting on audio
            DOTween.CompleteAll(withCallbacks: true);
            InvokeLifecycleMethod(harness.DeathVfxPlayer, "OnAllParticlesFinished");
            InvokeLifecycleMethod(harness.DeathHandler, "LateUpdate");
            int afterWarningAndVfx = completedCount;

            // Complete audio, then LateUpdate -> emits OnCompleted once
            InvokeLifecycleMethod(harness.AudioClipPlayer, "OnAudioClipPlayFinished");
            InvokeLifecycleMethod(harness.DeathHandler, "LateUpdate");

            // Assert
            Assert.AreEqual(0, afterWarningPending);
            Assert.AreEqual(0, afterWarningAndVfx);
            Assert.AreEqual(1, completedCount);
        }

        [Test]
        public void BarrelDeathHandler_WatchdogTimeout_LogsErrorAndForcesCompletionInLateUpdate()
        {
            // Arrange
            FakePlayerTarget player = CreatePlayerTarget(ISOLATED_ORIGIN + new Vector3(0f, 0f, 5f), new Vector3(0.5f, 1f, 0.5f));
            BarrelPrefabHarness harness = InstantiateBarrelPrefabHarness(ISOLATED_ORIGIN, player.gameObject, withFeedback: true);
            ConfigureDeathClip(harness.AudioClipPlayer);
            ConfigureVfxDuration(harness.DeathVfxPlayer, duration: 0.5f);
            InitializeBarrelHarness(harness);

            int completedCount = 0;
            harness.DeathHandler.OnCompleted += delegate
            {
                completedCount++;
            };

            harness.Enemy.TakeFullHpDamage();

            // Act 1 — Just before 5.0s timeout -> does not trigger watchdog
            SetPrivateField(typeof(BarrelEnemyDeathHandler), harness.DeathHandler, "_deathStartedAt", Time.time - 4.99f);
            InvokeLifecycleMethod(harness.DeathHandler, "LateUpdate");
            int beforeTimeoutCount = completedCount;

            // Act 2 — Strictly past 5.0s timeout (margin avoids float cancellation error at large Editor Time.time) -> logs expected error and completes
            SetPrivateField(typeof(BarrelEnemyDeathHandler), harness.DeathHandler, "_deathStartedAt", Time.time - 5.1f);
            LogAssert.Expect(LogType.Error, "Barrel death presentation exceeded its watchdog; completing release.");
            InvokeLifecycleMethod(harness.DeathHandler, "LateUpdate");

            // Assert
            Assert.AreEqual(0, beforeTimeoutCount);
            Assert.AreEqual(1, completedCount);
        }

        [Test]
        public void BarrelDeathHandler_AwakeValidation_ThrowsOnMissingRequiredFieldsOrInvalidTimeout()
        {
            // Arrange
            GameObject go = new GameObject("InvalidBarrelDeath");
            go.SetActive(false);
            _createdObjects.Add(go);
            go.AddComponent<Enemy>();
            go.AddComponent<Health>();
            go.AddComponent<EnemyMovementController>();
            go.AddComponent<Rigidbody>();
            go.AddComponent<BoxCollider>();
            BarrelEnemyDeathHandler handler = go.AddComponent<BarrelEnemyDeathHandler>();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                InvokeLifecycleMethod(handler, "Awake");
            });
        }

        [Test]
        public void BarrelAttackFeedback_SamplesColorAndEmissionTimelinePreservesShineAndRestoresOriginals()
        {
            // Arrange — MeshRenderer with Tier2Shiny shader + emission enabled, and shine enabled via EnemyTierShineController
            Shader shinyShader = Shader.Find(TIER2_SHINY_SHADER_NAME);
            Assert.IsNotNull(shinyShader, $"Missing shader '{TIER2_SHINY_SHADER_NAME}'");
            Material mat = new Material(shinyShader);
            mat.EnableKeyword("_EMISSION");
            Color originalBase = new Color(0.2f, 0.4f, 0.8f, 1f);
            Color originalEmission = new Color(0.1f, 0.1f, 0.1f, 1f);
            mat.SetColor(BarrelFeedbackShaderConstants.BASE_COLOR_ID, originalBase);
            mat.SetColor(BarrelFeedbackShaderConstants.EMISSION_COLOR_ID, originalEmission);
            _createdObjects.Add(mat);

            FeedbackHarness harness = CreateFeedbackHarness(mat, withValidClip: true, withValidState: true);
            harness.ShineController.SetShineEnabled(true);

            harness.Root.SetActive(true);
            harness.UnityAnimator.Rebind();
            harness.UnityAnimator.Update(0f);
            InvokeLifecycleMethod(harness.Feedback, "Awake");
            InvokeLifecycleMethod(harness.Feedback, "OnEnable");
            harness.UnityAnimator.Play("StandingLayer.Attack", 0, 0f);
            harness.UnityAnimator.Update(0f);

            // Act 1 — BeginFeedback + CompleteFeedback drives colors to target while preserving _ShineEnabled = 1
            harness.Feedback.BeginFeedback();
            harness.Feedback.CompleteFeedback();

            MaterialPropertyBlock peakBlock = new MaterialPropertyBlock();
            harness.Renderer.GetPropertyBlock(peakBlock, 0);
            Color peakBase = peakBlock.GetColor(BarrelFeedbackShaderConstants.BASE_COLOR_ID);
            Color peakEmission = peakBlock.GetColor(BarrelFeedbackShaderConstants.EMISSION_COLOR_ID);
            float peakShine = peakBlock.GetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID);

            // Act 2 — ResetFeedback restores original colors and pivot rotation while preserving _ShineEnabled = 1
            harness.Feedback.ResetFeedback();

            MaterialPropertyBlock restoredBlock = new MaterialPropertyBlock();
            harness.Renderer.GetPropertyBlock(restoredBlock, 0);
            Color restoredBase = restoredBlock.GetColor(BarrelFeedbackShaderConstants.BASE_COLOR_ID);
            Color restoredEmission = restoredBlock.GetColor(BarrelFeedbackShaderConstants.EMISSION_COLOR_ID);
            float restoredShine = restoredBlock.GetFloat(EnemyVisualConstants.SHINE_ENABLED_PROPERTY_ID);

            // Assert
            AssertColorEqual(Color.red, peakBase);
            AssertColorEqual(Color.red, peakEmission);
            Assert.AreEqual(1f, peakShine, TOLERANCE);

            AssertColorEqual(originalBase, restoredBase);
            AssertColorEqual(originalEmission, restoredEmission);
            Assert.AreEqual(1f, restoredShine, TOLERANCE);
            Assert.AreEqual(Quaternion.identity, harness.VisualPivot.localRotation);
        }

        [Test]
        public void BarrelAttackFeedback_InvalidConfigOrMissingAnimatorState_LogsExpectedErrorsWithoutThrowing()
        {
            // Arrange 1 — Unassigned pivot/clip/renderers logs configuration error
            GameObject emptyGo = new GameObject("UnconfiguredFeedback");
            emptyGo.SetActive(false);
            _createdObjects.Add(emptyGo);
            BarrelEnemyAttackFeedback unconfigured = emptyGo.AddComponent<BarrelEnemyAttackFeedback>();
            LogAssert.Expect(LogType.Error, "Barrel attack feedback requires authored pivot, Animator, Attack clip, renderers and valid tuning.");
            InvokeLifecycleMethod(unconfigured, "Awake");

            // Arrange 2 — Valid configuration but missing _attackState logs state error on BeginFeedback
            Shader litShader = Shader.Find(URP_LIT_SHADER_NAME);
            Material mat = new Material(litShader);
            _createdObjects.Add(mat);

            FeedbackHarness missingStateHarness = CreateFeedbackHarness(mat, withValidClip: true, withValidState: false);
            missingStateHarness.Root.SetActive(true);
            missingStateHarness.UnityAnimator.Rebind();
            missingStateHarness.UnityAnimator.Update(0f);
            InvokeLifecycleMethod(missingStateHarness.Feedback, "Awake");
            InvokeLifecycleMethod(missingStateHarness.Feedback, "OnEnable");

            // Act & Assert
            LogAssert.Expect(LogType.Error, "Barrel feedback Attack state is missing from its Animator.");
            Assert.DoesNotThrow(() =>
            {
                missingStateHarness.Feedback.BeginFeedback();
            });
        }

        private BarrelPrefabHarness InstantiateBarrelPrefabHarness(Vector3 position, GameObject playerObject, bool withFeedback)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BARREL_PREFAB_PATH);
            Assert.IsNotNull(prefab, $"Missing Barrel prefab at {BARREL_PREFAB_PATH}");

            GameObject inactiveHost = new GameObject("TempInactiveBarrelHost");
            inactiveHost.SetActive(false);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity, inactiveHost.transform);
            instance.SetActive(false);
            instance.transform.SetParent(null, true);
            UnityEngine.Object.DestroyImmediate(inactiveHost);
            _createdObjects.Add(instance);

            Enemy enemy = instance.GetComponent<Enemy>();
            Health health = instance.GetComponent<Health>();
            BoxCollider collider = instance.GetComponent<BoxCollider>();
            EnemyCollisionsController collisions = instance.GetComponent<EnemyCollisionsController>();
            EnemyMovementController movement = instance.GetComponent<EnemyMovementController>();
            BarrelEnemyExplosionController explosion = instance.GetComponent<BarrelEnemyExplosionController>();
            BarrelEnemyDeathHandler deathHandler = instance.GetComponent<BarrelEnemyDeathHandler>();
            BarrelEnemyAttackFeedback feedback = instance.GetComponent<BarrelEnemyAttackFeedback>();
            EnemyAnimator enemyAnimator = instance.GetComponentInChildren<EnemyAnimator>(true);
            Animator unityAnimator = enemyAnimator.GetComponent<Animator>();
            CircularTelegraphIndicator warning = instance.GetComponentInChildren<CircularTelegraphIndicator>(true);
            AudioClipPlayer audioClipPlayer = instance.GetComponentInChildren<AudioClipPlayer>(true);
            AudioSource audioSource = audioClipPlayer.GetComponent<AudioSource>();
            VFXPlayer deathVfxPlayer = GetPrivateField<VFXPlayer>(typeof(BarrelEnemyDeathHandler), deathHandler, "_deathVfxPlayer");
            GameObject visual = GetPrivateField<GameObject>(typeof(Enemy), enemy, "_visual");

            if (!withFeedback)
            {
                SetPrivateField(typeof(BarrelEnemyExplosionController), explosion, "_attackFeedback", null);
            }

            FakePlayerManager playerManager = new FakePlayerManager(playerObject);
            FakeExplosionVfxPool explosionPool = new FakeExplosionVfxPool();
            FakeExpParticleSpawner expSpawner = new FakeExpParticleSpawner();
            FakeTimeFreezeState timeFreeze = new FakeTimeFreezeState();
            DropAnimationConfiguration dropConfig = ScriptableObject.CreateInstance<DropAnimationConfiguration>();
            _createdObjects.Add(dropConfig);

            Container container = new ContainerBuilder()
                .AddSingleton(playerManager, typeof(IPlayerManager))
                .AddSingleton(explosionPool, typeof(IExplosionVfxPool))
                .AddSingleton(expSpawner, typeof(IInWorldSpaceSpawner<ExpParticleSpawner, float>))
                .AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>))
                .AddSingleton(timeFreeze, typeof(ITimeFreezeState))
                .AddSingleton(new FakeGridManager(), typeof(IGridManager))
                .AddSingleton(new FakeCollectibleDropNotifier(), typeof(ICollectibleDropNotifier))
                .AddSingleton(new FakeCollectibleDropSuppressor(), typeof(ICollectibleDropSuppressor))
                .AddSingleton(dropConfig)
                .Build();
            _createdContainers.Add(container);
            GameObjectInjector.InjectRecursive(instance, container);

            return new BarrelPrefabHarness(
                instance,
                enemy,
                health,
                collider,
                collisions,
                movement,
                explosion,
                deathHandler,
                feedback,
                enemyAnimator,
                unityAnimator,
                warning,
                audioClipPlayer,
                audioSource,
                deathVfxPlayer,
                visual,
                explosionPool,
                expSpawner,
                timeFreeze);
        }

        private void InitializeBarrelHarness(BarrelPrefabHarness harness)
        {
            harness.Root.SetActive(true);
            harness.UnityAnimator.Rebind();
            harness.UnityAnimator.Update(0f);

            MonoBehaviour[] behaviours = harness.Root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].transform != harness.Root.transform)
                {
                    InvokeOptionalLifecycleMethod(behaviours[i], "Awake");
                }
            }

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].transform == harness.Root.transform)
                {
                    InvokeOptionalLifecycleMethod(behaviours[i], "Awake");
                }
            }

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].enabled && behaviours[i].gameObject.activeInHierarchy)
                {
                    InvokeOptionalLifecycleMethod(behaviours[i], "OnEnable");
                }
            }

            InvokeOptionalLifecycleMethod(harness.Explosion, "Start");
            Physics.SyncTransforms();
        }

        private FeedbackHarness CreateFeedbackHarness(Material material, bool withValidClip, bool withValidState)
        {
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BARREL_ANIM_CONTROLLER_PATH);
            AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BARREL_ATTACK_CLIP_PATH);
            Assert.IsNotNull(controller);
            Assert.IsNotNull(attackClip);

            GameObject root = new GameObject("FeedbackTestRoot");
            root.SetActive(false);
            _createdObjects.Add(root);

            GameObject pivot = new GameObject("VisualPivot");
            pivot.transform.SetParent(root.transform, false);

            GameObject meshGo = new GameObject("Model");
            meshGo.transform.SetParent(pivot.transform, false);
            MeshRenderer renderer = meshGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            Animator animator = pivot.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            pivot.AddComponent<EnemyAnimator>();

            EnemyTierShineController shine = root.AddComponent<EnemyTierShineController>();
            SetPrivateField(typeof(EnemyTierShineController), shine, "_targetRenderers", new Renderer[] { renderer });

            BarrelEnemyAttackFeedback feedback = root.AddComponent<BarrelEnemyAttackFeedback>();
            SetPrivateField(typeof(BarrelEnemyAttackFeedback), feedback, "_visualPivot", pivot.transform);
            SetPrivateField(typeof(BarrelEnemyAttackFeedback), feedback, "_animator", animator);
            SetPrivateField(typeof(BarrelEnemyAttackFeedback), feedback, "_attackClip", withValidClip ? attackClip : null);
            SetPrivateField(typeof(BarrelEnemyAttackFeedback), feedback, "_attackState", withValidState ? "StandingLayer.Attack" : "StandingLayer.DoesNotExist");
            SetPrivateField(typeof(BarrelEnemyAttackFeedback), feedback, "_renderers", new Renderer[] { renderer });

            return new FeedbackHarness(root, pivot.transform, renderer, animator, feedback, shine);
        }

        private AudioClip ConfigureDeathClip(AudioClipPlayer audioPlayer)
        {
            AudioClip clip = AudioClip.Create("BarrelTestDeath", 4410, 1, 44100, false);
            _createdObjects.Add(clip);

            AudioClipConfig clipConfig = new AudioClipConfig();
            SetPrivateField(typeof(AudioClipConfig), clipConfig, "_audioClip", clip);
            SetPrivateField(typeof(AudioClipConfig), clipConfig, "_volume", 1f);
            SetPrivateField(typeof(AudioClipConfig), clipConfig, "_pitch", 1f);
            SetPrivateField(typeof(AudioClipConfig), clipConfig, "_loop", false);

            AudioClipPlayer.AudioClipPlayerConfig playerConfig = new AudioClipPlayer.AudioClipPlayerConfig();
            SetPrivateField(typeof(AudioClipPlayer.AudioClipPlayerConfig), playerConfig, "_name", BarrelEnemyConstants.DEATH_AUDIO);
            SetPrivateField(typeof(AudioClipPlayer.AudioClipPlayerConfig), playerConfig, "_clipVariants", new AudioClipConfig[] { clipConfig });
            SetPrivateField(typeof(AudioClipPlayer), audioPlayer, "_audioClipPlayerConfigs", new AudioClipPlayer.AudioClipPlayerConfig[] { playerConfig });
            return clip;
        }

        private static void ConfigureVfxDuration(VFXPlayer vfxPlayer, float duration)
        {
            ParticleSystem ps = vfxPlayer.GetComponentInChildren<ParticleSystem>(true);
            if (ps == null)
            {
                ps = vfxPlayer.gameObject.AddComponent<ParticleSystem>();
            }

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.duration = duration;
        }

        private BarrelExplosionConfigSO CreateExplosionConfig(float explosionRadius, float activationRange)
        {
            BarrelExplosionConfigSO config = ScriptableObject.CreateInstance<BarrelExplosionConfigSO>();
            SetPrivateField(typeof(BarrelExplosionConfigSO), config, "_explosionRadius", explosionRadius);
            SetPrivateField(typeof(BarrelExplosionConfigSO), config, "_activationRange", activationRange);
            _createdObjects.Add(config);
            return config;
        }

        private GameObject CreatePlainObject(string name)
        {
            GameObject go = new GameObject(name);
            _createdObjects.Add(go);
            return go;
        }

        private FakePlayerTarget CreatePlayerTarget(Vector3 position, Vector3 size)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            Assert.GreaterOrEqual(playerLayer, 0);

            GameObject go = new GameObject("BarrelTestPlayer");
            go.layer = playerLayer;
            go.transform.position = position;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = size;
            FakePlayerTarget target = go.AddComponent<FakePlayerTarget>();
            target.BodyCollider = box;
            _createdObjects.Add(go);
            Physics.SyncTransforms();
            return target;
        }

        private GameObject CreatePlayerChildCollider(GameObject parent, Vector3 size)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            GameObject child = new GameObject("PlayerChildCollider");
            child.layer = playerLayer;
            child.transform.SetParent(parent.transform, false);
            BoxCollider box = child.AddComponent<BoxCollider>();
            box.size = size;
            Physics.SyncTransforms();
            return child;
        }

        private static void AssertColorEqual(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, TOLERANCE);
            Assert.AreEqual(expected.g, actual.g, TOLERANCE);
            Assert.AreEqual(expected.b, actual.b, TOLERANCE);
            Assert.AreEqual(expected.a, actual.a, TOLERANCE);
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
            if (!TryInvokeLifecycleMethod(target, methodName))
            {
                Assert.Fail($"Method '{methodName}' not found on {target.GetType().Name}.");
            }
        }

        private static void InvokeOptionalLifecycleMethod(object target, string methodName)
        {
            TryInvokeLifecycleMethod(target, methodName);
        }

        private static bool TryInvokeLifecycleMethod(object target, string methodName)
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
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static void InvokeLifecycleMethodWithCollider(object target, string methodName, Collider collider)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}");
            method.Invoke(target, new object[] { target, new CollisionEventArgs(collider) });
        }

        private readonly struct BarrelPrefabHarness
        {
            public readonly GameObject Root;
            public readonly Enemy Enemy;
            public readonly Health Health;
            public readonly BoxCollider Collider;
            public readonly EnemyCollisionsController Collisions;
            public readonly EnemyMovementController Movement;
            public readonly BarrelEnemyExplosionController Explosion;
            public readonly BarrelEnemyDeathHandler DeathHandler;
            public readonly BarrelEnemyAttackFeedback Feedback;
            public readonly EnemyAnimator EnemyAnimator;
            public readonly Animator UnityAnimator;
            public readonly CircularTelegraphIndicator Warning;
            public readonly AudioClipPlayer AudioClipPlayer;
            public readonly AudioSource AudioSource;
            public readonly VFXPlayer DeathVfxPlayer;
            public readonly GameObject Visual;
            public readonly FakeExplosionVfxPool ExplosionVfxPool;
            public readonly FakeExpParticleSpawner ExpSpawner;
            public readonly FakeTimeFreezeState TimeFreeze;

            public BarrelPrefabHarness(
                GameObject root,
                Enemy enemy,
                Health health,
                BoxCollider collider,
                EnemyCollisionsController collisions,
                EnemyMovementController movement,
                BarrelEnemyExplosionController explosion,
                BarrelEnemyDeathHandler deathHandler,
                BarrelEnemyAttackFeedback feedback,
                EnemyAnimator enemyAnimator,
                Animator unityAnimator,
                CircularTelegraphIndicator warning,
                AudioClipPlayer audioClipPlayer,
                AudioSource audioSource,
                VFXPlayer deathVfxPlayer,
                GameObject visual,
                FakeExplosionVfxPool explosionVfxPool,
                FakeExpParticleSpawner expSpawner,
                FakeTimeFreezeState timeFreeze)
            {
                Root = root;
                Enemy = enemy;
                Health = health;
                Collider = collider;
                Collisions = collisions;
                Movement = movement;
                Explosion = explosion;
                DeathHandler = deathHandler;
                Feedback = feedback;
                EnemyAnimator = enemyAnimator;
                UnityAnimator = unityAnimator;
                Warning = warning;
                AudioClipPlayer = audioClipPlayer;
                AudioSource = audioSource;
                DeathVfxPlayer = deathVfxPlayer;
                Visual = visual;
                ExplosionVfxPool = explosionVfxPool;
                ExpSpawner = expSpawner;
                TimeFreeze = timeFreeze;
            }
        }

        private readonly struct FeedbackHarness
        {
            public readonly GameObject Root;
            public readonly Transform VisualPivot;
            public readonly MeshRenderer Renderer;
            public readonly Animator UnityAnimator;
            public readonly BarrelEnemyAttackFeedback Feedback;
            public readonly EnemyTierShineController ShineController;

            public FeedbackHarness(
                GameObject root,
                Transform visualPivot,
                MeshRenderer renderer,
                Animator unityAnimator,
                BarrelEnemyAttackFeedback feedback,
                EnemyTierShineController shineController)
            {
                Root = root;
                VisualPivot = visualPivot;
                Renderer = renderer;
                UnityAnimator = unityAnimator;
                Feedback = feedback;
                ShineController = shineController;
            }
        }

        private sealed class FakePlayerTarget : MonoBehaviour, IDamageable, IHealth
        {
            public BoxCollider BodyCollider { get; set; }
            public float TotalDamage { get; private set; }
            public float CurrentHealth { get { return 100f; } }
            public float MaxHealth { get { return 100f; } set { } }

            public event EventHandler OnHealthChanged { add { } remove { } }
            public event EventHandler OnHealthDecreased { add { } remove { } }
            public event EventHandler OnHealthIncreased { add { } remove { } }
            public event EventHandler OnNoHealth { add { } remove { } }

            public void TakeDamage(float damage)
            {
                TotalDamage += damage;
            }

            public void TakeFullHpDamage()
            {
            }

            public void DecreaseHealth(float value)
            {
            }

            public void IncreaseHealth(float value)
            {
            }

            public bool IsAlive()
            {
                return true;
            }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            public GameObject GameObject { get; }
            public IHealth Health { get; }
            public IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public ICarController CarController { get { return null; } }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get { return null; } }
            public ISkillsRegistry SkillsRegistry { get { return null; } }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }

            public FakePlayerManager(GameObject gameObject)
            {
                GameObject = gameObject;
                Health = gameObject.GetComponent<IHealth>();
            }
        }

        private sealed class FakeExplosionVfxPool : IExplosionVfxPool
        {
            public List<RecordedExplosionPlay> PlayCalls { get; } = new List<RecordedExplosionPlay>();

            public void Play(Vector3 position, float radius)
            {
                PlayCalls.Add(new RecordedExplosionPlay(position, radius));
            }
        }

        private readonly struct RecordedExplosionPlay
        {
            public readonly Vector3 Position;
            public readonly float Radius;

            public RecordedExplosionPlay(Vector3 position, float radius)
            {
                Position = position;
                Radius = radius;
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

        private sealed class FakeGridManager : IGridManager
        {
            public Cell DestinationCell { get { return null; } }
            public Grid GridPlayerChunk { get { return null; } }
            public Grid WorldGrid { get { return null; } }
        }

        private sealed class FakeCollectibleDropNotifier : ICollectibleDropNotifier
        {
            public event EventHandler OnSkillUpgradeCollectibleCollected { add { } remove { } }
            public event Action<Assets.Scripts.PowerUps.PowerUpType> OnPowerUpCollected { add { } remove { } }

            public void SpawnCollectible(GameObject prefab, Vector3 spawnPos, Vector3 targetPos)
            {
            }
        }

        private sealed class FakeCollectibleDropSuppressor : ICollectibleDropSuppressor
        {
            public bool AreDropsSuppressed { get { return true; } }

            public void BeginDropSuppression()
            {
            }

            public void EndDropSuppression()
            {
            }
        }
    }
}
