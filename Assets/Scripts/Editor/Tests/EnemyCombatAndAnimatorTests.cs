using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Collisions;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Enemies.Constants;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.StatusEffects;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class EnemyCombatAndAnimatorTests
    {
        private const float TOLERANCE = 0.0001f;
        private const string ZOMBIE_CONTROLLER_PATH = "Assets/Animations/Enemies/Basic/Zombie/ZombieAnimController.controller";
        private static readonly Vector3 ISOLATED_ORIGIN = new Vector3(32000f, 1200f, 32000f);

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
            Physics.SyncTransforms();
        }

        [Test]
        public void EnemyCollisionsController_DetectsOnlyExternalPlayerCollidersWithinRadiusAndIgnoresSelfTriggers()
        {
            // Arrange — Enemy at origin with _collisionRadius = 1.0f and a self-trigger on Player layer (must be excluded)
            CollisionHarness harness = CreateCollisionHarness(ISOLATED_ORIGIN, collisionRadius: 1.0f, withSelfPlayerTrigger: true);
            BoxCollider inRangePlayer = CreateBoxCollider("InRangePlayer", ISOLATED_ORIGIN + new Vector3(0f, 0f, 1.2f), new Vector3(0.5f, 0.5f, 0.5f), "Player", isTrigger: false).GetComponent<BoxCollider>();
            CreateBoxCollider("OutOfRangePlayer", ISOLATED_ORIGIN + new Vector3(0f, 0f, 1.6f), new Vector3(0.5f, 0.5f, 0.5f), "Player", isTrigger: false);
            CreateBoxCollider("NonPlayerEnemy", ISOLATED_ORIGIN + new Vector3(0.2f, 0f, 0f), new Vector3(0.5f, 0.5f, 0.5f), "Enemy", isTrigger: false);

            List<Collider> detected = new List<Collider>();
            harness.Collisions.OnCollisionWithPlayer += delegate (object sender, CollisionEventArgs args)
            {
                detected.Add(args.Collider);
            };

            // Act
            InvokeLifecycleMethod(harness.Collisions, "HandleCollisionsCheck");

            // Assert
            Assert.IsTrue(harness.Collisions.IsInvoking("HandleCollisionsCheck"));
            Assert.AreEqual(1, detected.Count);
            Assert.AreSame(inRangePlayer, detected[0]);

            // Act & Assert — OnDisable cancels InvokeRepeating
            InvokeLifecycleMethod(harness.Collisions, "OnDisable");
            harness.Root.SetActive(false);
            Assert.IsFalse(harness.Collisions.IsInvoking("HandleCollisionsCheck"));
        }

        [Test]
        public void EnemyAttackController_WhenFrozenOrAlreadyAttacking_IgnoresPlayerContact()
        {
            // Arrange
            AttackHarness harness = CreateAttackHarness(ISOLATED_ORIGIN, attackRange: 1.5f, attackArcAngle: 60f, damage: 25f);
            FakeDamageableTarget player = CreateDamageablePlayer("Player", ISOLATED_ORIGIN + new Vector3(0f, 0f, 1.0f), new Vector3(0.5f, 0.5f, 0.5f));

            // Act 1 — Frozen enemy ignores contact
            harness.TimeFreeze.SetFrozen(true, unfrozenTime: 0f);
            harness.FakeCollisions.EmitCollision(player.Collider);
            bool triggerWhileFrozen = harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_TRIGGER_ATTACK);

            // Act 2 — Unfrozen while already playing attack animation ignores new contact
            harness.TimeFreeze.SetFrozen(false, unfrozenTime: 0f);
            harness.EnemyAnimator.Call_OnAttackAnimationStart();
            harness.FakeCollisions.EmitCollision(player.Collider);
            bool triggerWhileAttacking = harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_TRIGGER_ATTACK);

            // Assert
            Assert.IsFalse(triggerWhileFrozen);
            Assert.IsFalse(triggerWhileAttacking);
        }

        [TestCase(0f, 1.0f, true, TestName = "EnemyAttackController_TargetDeadAheadInRange_TriggersAttackAndDealsDamageOnHitFrame")]
        [TestCase(25f, 1.0f, true, TestName = "EnemyAttackController_TargetInsideHalfArc_TriggersAttackAndDealsDamageOnHitFrame")]
        [TestCase(35f, 1.0f, false, TestName = "EnemyAttackController_TargetOutsideHalfArc_DoesNotAttack")]
        [TestCase(0f, 2.0f, false, TestName = "EnemyAttackController_TargetOutOfAttackRange_DoesNotAttack")]
        public void EnemyAttackController_RangeAndArcConeBoundaries_ControlAttackTriggerAndHitFrameDamage(
            float angleDegrees,
            float zDistance,
            bool shouldAttack)
        {
            // Arrange — Enemy faces +Z with 60 deg arc (half angle = 30 deg) and 1.25m range
            AttackHarness harness = CreateAttackHarness(ISOLATED_ORIGIN, attackRange: 1.25f, attackArcAngle: 60f, damage: 30f);
            Vector3 offset = Quaternion.Euler(0f, angleDegrees, 0f) * (Vector3.forward * zDistance);
            FakeDamageableTarget player = CreateDamageablePlayer("Player", ISOLATED_ORIGIN + offset, new Vector3(0.4f, 0.4f, 0.4f));

            // Act — Contact triggers animation only, NOT immediate damage
            harness.FakeCollisions.EmitCollision(player.Collider);
            bool attackTriggered = harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_TRIGGER_ATTACK);
            float damageBeforeHitFrame = player.TotalDamageReceived;

            // Act — Fire hit frame
            harness.EnemyAnimator.Call_OnAttackHitFrame();
            // Stray second hit frame must not apply duplicate damage
            harness.EnemyAnimator.Call_OnAttackHitFrame();

            // Assert
            Assert.AreEqual(shouldAttack, attackTriggered);
            Assert.AreEqual(0f, damageBeforeHitFrame, TOLERANCE);
            Assert.AreEqual(shouldAttack ? 30f : 0f, player.TotalDamageReceived, TOLERANCE);
            Assert.AreEqual(shouldAttack ? 1 : 0, player.HitCount);
        }

        [Test]
        public void EnemyAttackController_OverlappingTargetBehindEnemy_AllowsAttackViaCloseOverlapShortcut()
        {
            // Arrange — Enemy faces +Z; player collider encloses enemy origin so ClosestPoint - pos has sqrMagnitude < 0.001
            AttackHarness harness = CreateAttackHarness(ISOLATED_ORIGIN, attackRange: 1.0f, attackArcAngle: 30f, damage: 22f);
            FakeDamageableTarget player = CreateDamageablePlayer("Player", ISOLATED_ORIGIN + new Vector3(0f, 0f, -0.2f), new Vector3(1.5f, 1.0f, 1.5f));

            // Act
            harness.FakeCollisions.EmitCollision(player.Collider);
            bool attackTriggered = harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_TRIGGER_ATTACK);
            harness.EnemyAnimator.Call_OnAttackHitFrame();

            // Assert
            Assert.IsTrue(attackTriggered);
            Assert.AreEqual(22f, player.TotalDamageReceived, TOLERANCE);
        }

        [Test]
        public void EnemyAttackController_ImpassableTerrainBetweenEnemyAndPlayer_BlocksAttackLineOfSight()
        {
            // Arrange — Impassable wall sits between enemy at z=0 and player at z=1.1
            AttackHarness harness = CreateAttackHarness(ISOLATED_ORIGIN, attackRange: 1.5f, attackArcAngle: 60f, damage: 30f);
            FakeDamageableTarget player = CreateDamageablePlayer("Player", ISOLATED_ORIGIN + new Vector3(0f, 0f, 1.1f), new Vector3(0.4f, 0.4f, 0.4f));
            CreateBoxCollider("BlockingWall", ISOLATED_ORIGIN + new Vector3(0f, 0f, 0.5f), new Vector3(2f, 2f, 0.2f), "Impassable", isTrigger: false);

            // Act
            harness.FakeCollisions.EmitCollision(player.Collider);
            bool attackTriggered = harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_TRIGGER_ATTACK);
            harness.EnemyAnimator.Call_OnAttackHitFrame();

            // Assert
            Assert.IsFalse(attackTriggered);
            Assert.AreEqual(0f, player.TotalDamageReceived, TOLERANCE);
        }

        [Test]
        public void EnemyAttackController_PlayerMovesOutOfRangeBeforeHitFrame_DealsZeroDamage()
        {
            // Arrange
            AttackHarness harness = CreateAttackHarness(ISOLATED_ORIGIN, attackRange: 1.25f, attackArcAngle: 60f, damage: 30f);
            FakeDamageableTarget player = CreateDamageablePlayer("Player", ISOLATED_ORIGIN + new Vector3(0f, 0f, 1.0f), new Vector3(0.4f, 0.4f, 0.4f));

            harness.FakeCollisions.EmitCollision(player.Collider);
            Assert.IsTrue(harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_TRIGGER_ATTACK));

            // Act — Player retreats out of range before hit frame
            player.transform.position = ISOLATED_ORIGIN + new Vector3(0f, 0f, 4.0f);
            Physics.SyncTransforms();
            harness.EnemyAnimator.Call_OnAttackHitFrame();

            // Assert
            Assert.AreEqual(0f, player.TotalDamageReceived, TOLERANCE);
            Assert.AreEqual(0, player.HitCount);
        }

        [Test]
        public void EnemyAttackController_OnDisable_ClearsTargetAndPreventsStaleHitFrameDamage()
        {
            // Arrange
            AttackHarness harness = CreateAttackHarness(ISOLATED_ORIGIN, attackRange: 1.5f, attackArcAngle: 60f, damage: 30f);
            FakeDamageableTarget player = CreateDamageablePlayer("Player", ISOLATED_ORIGIN + new Vector3(0f, 0f, 1.0f), new Vector3(0.4f, 0.4f, 0.4f));

            harness.FakeCollisions.EmitCollision(player.Collider);

            // Act — Disable clears _currentAttackedTarget and unsubscribes, then re-enable and fire hit frame
            InvokeLifecycleMethod(harness.Attack, "OnDisable");
            harness.Attack.enabled = false;
            harness.Attack.enabled = true;
            InvokeLifecycleMethod(harness.Attack, "OnEnable");
            harness.EnemyAnimator.Call_OnAttackHitFrame();

            // Assert
            Assert.AreEqual(0f, player.TotalDamageReceived, TOLERANCE);
        }

        [Test]
        public void EnemyAttackController_RejectedCollisionDoesNotCacheTarget_AndMultiPlayerOverlapMatchesTarget()
        {
            // Arrange — Secondary Player-layer trigger overlaps enemy first while real target sits ahead
            AttackHarness harness = CreateAttackHarness(ISOLATED_ORIGIN, attackRange: 1.5f, attackArcAngle: 60f, damage: 30f);
            FakeDamageableTarget secondaryTrigger = CreateDamageablePlayer("SecondaryTrigger", ISOLATED_ORIGIN + new Vector3(0.4f, 0f, -0.4f), new Vector3(0.4f, 0.4f, 0.4f));
            FakeDamageableTarget frontPlayer = CreateDamageablePlayer("FrontPlayer", ISOLATED_ORIGIN + new Vector3(0f, 0f, 1.0f), new Vector3(0.4f, 0.4f, 0.4f));

            // Act 1 — Rejected out-of-arc collision must NOT cache target or hit on a stray hit frame
            harness.FakeCollisions.EmitCollision(secondaryTrigger.Collider);
            harness.EnemyAnimator.Call_OnAttackHitFrame();
            Assert.AreEqual(0f, secondaryTrigger.TotalDamageReceived, TOLERANCE);
            Assert.IsNull(typeof(EnemyAttackController).GetField("_currentAttackedTarget", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(harness.Attack));

            // Act 2 — Valid front player attack succeeds despite secondary Player-layer collider inside range
            harness.FakeCollisions.EmitCollision(frontPlayer.Collider);
            bool attackTriggered = harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_TRIGGER_ATTACK);
            harness.EnemyAnimator.Call_OnAttackHitFrame();

            // Assert
            Assert.IsTrue(attackTriggered);
            Assert.AreEqual(30f, frontPlayer.TotalDamageReceived, TOLERANCE);
        }

        [Test]
        public void EnemyAnimator_SyncsCrawlingLayerWeightsSpeedIsOnGroundAndFreezeAnimatorSpeed()
        {
            // Arrange
            AnimatorHarness harness = CreateAnimatorHarness(isMovingByCrawling: false);
            harness.Movement.Speed = 3.75f;
            harness.Movement.Grounded = false;

            // Act 1 — Standing properties
            InvokeLifecycleMethod(harness.EnemyAnimator, "HandleTransitionPropertiesChanges");

            float standingLayer0 = harness.UnityAnimator.GetLayerWeight(0);
            float standingLayer1 = harness.UnityAnimator.GetLayerWeight(1);
            bool crawlingParamStanding = harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_PARAM_IS_MOVING_BY_CRAWLING);
            float speedParam = harness.UnityAnimator.GetFloat(EnemyAnimationConstants.ANIM_PARAM_SPEED);
            bool groundedParam = harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_PARAM_IS_ON_GROUND);

            // Act 2 — Switch config to crawling and freeze
            harness.Enemy.Config.IsMovingByCrawling = true;
            harness.TimeFreeze.SetFrozen(true, unfrozenTime: 0f);
            InvokeLifecycleMethod(harness.EnemyAnimator, "HandleTransitionPropertiesChanges");

            float crawlingLayer0 = harness.UnityAnimator.GetLayerWeight(0);
            float crawlingLayer1 = harness.UnityAnimator.GetLayerWeight(1);
            bool crawlingParamActive = harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_PARAM_IS_MOVING_BY_CRAWLING);
            float speedWhileFrozen = harness.UnityAnimator.speed;

            // Act 3 — Disable restores animator speed to 1
            InvokeLifecycleMethod(harness.EnemyAnimator, "OnDisable");
            InvokeLifecycleMethod(harness.Enemy, "OnDisable");
            harness.Root.SetActive(false);
            float speedAfterDisable = harness.UnityAnimator.speed;

            // Assert
            Assert.AreEqual(1f, standingLayer0, TOLERANCE);
            Assert.AreEqual(0f, standingLayer1, TOLERANCE);
            Assert.IsFalse(crawlingParamStanding);
            Assert.AreEqual(3.75f, speedParam, TOLERANCE);
            Assert.IsFalse(groundedParam);

            Assert.AreEqual(1f, crawlingLayer0, TOLERANCE);
            Assert.AreEqual(1f, crawlingLayer1, TOLERANCE);
            Assert.IsTrue(crawlingParamActive);
            Assert.AreEqual(0f, speedWhileFrozen, TOLERANCE);
            Assert.AreEqual(1f, speedAfterDisable, TOLERANCE);
        }

        [Test]
        public void EnemyAnimator_AttackCallbacksAndRestoreInitialState_MaintainIsPlayingFlagAndIdempotentEnd()
        {
            // Arrange
            AnimatorHarness harness = CreateAnimatorHarness(isMovingByCrawling: false);
            int startCount = 0;
            int hitCount = 0;
            int endCount = 0;
            harness.EnemyAnimator.OnAttackAnimationStart += delegate { startCount++; };
            harness.EnemyAnimator.OnAttackHitFrame += delegate { hitCount++; };
            harness.EnemyAnimator.OnAttackAnimationEnd += delegate { endCount++; };

            // Act 1 — Call_OnAttackAnimationEnd while idle is ignored
            harness.EnemyAnimator.Call_OnAttackAnimationEnd();
            int endBeforeStart = endCount;

            // Act 2 — Full attack event cycle
            harness.EnemyAnimator.PlayAttackAnimation();
            bool triggerSet = harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_TRIGGER_ATTACK);
            harness.EnemyAnimator.Call_OnAttackAnimationStart();
            bool playingAfterStart = harness.EnemyAnimator.IsPlayingAttackAnimation;
            harness.EnemyAnimator.Call_OnAttackHitFrame();
            harness.EnemyAnimator.Call_OnAttackAnimationEnd();
            harness.EnemyAnimator.Call_OnAttackAnimationEnd();

            // Act 3 — RestoreInitialState clears trigger, Speed, and active attack flag
            harness.EnemyAnimator.PlayAttackAnimation();
            harness.EnemyAnimator.Call_OnAttackAnimationStart();
            harness.EnemyAnimator.RestoreInitialState("StandingLayer.Zombie Idle Standing");

            // Assert
            Assert.AreEqual(0, endBeforeStart);
            Assert.IsTrue(triggerSet);
            Assert.IsTrue(playingAfterStart);
            Assert.AreEqual(2, startCount);
            Assert.AreEqual(1, hitCount);
            Assert.AreEqual(1, endCount);
            Assert.IsFalse(harness.EnemyAnimator.IsPlayingAttackAnimation);
            Assert.IsFalse(harness.UnityAnimator.GetBool(EnemyAnimationConstants.ANIM_TRIGGER_ATTACK));
            Assert.AreEqual(0f, harness.UnityAnimator.GetFloat(EnemyAnimationConstants.ANIM_PARAM_SPEED), TOLERANCE);
        }

        [Test]
        public void EnemyAnimator_SynchronizeAttackAnimationState_CancelsAfterMaxDurationUnfrozenSeconds()
        {
            // Arrange — Enter attack state at UnfrozenTime = 5.0s; watchdog fires at >= 5.0 + 3.5 = 8.5s
            AnimatorHarness harness = CreateAnimatorHarness(isMovingByCrawling: false);
            harness.UnityAnimator.Play(EnemyAnimationConstants.ANIM_STATE_ZOMBIE_ATTACK_STANDING, 0, 0f);
            harness.UnityAnimator.Update(0f);

            int endCount = 0;
            harness.EnemyAnimator.OnAttackAnimationEnd += delegate { endCount++; };

            harness.TimeFreeze.SetFrozen(false, unfrozenTime: 5.0f);
            harness.EnemyAnimator.Call_OnAttackAnimationStart();

            // Act 1 — Just before 3.5s unfrozen duration -> still playing
            harness.TimeFreeze.SetFrozen(false, unfrozenTime: 5.0f + EnemyAnimationConstants.MAX_ATTACK_ANIMATION_DURATION - 0.01f);
            InvokeLifecycleMethod(harness.EnemyAnimator, "HandleTransitionPropertiesChanges");
            bool playingBeforeDeadline = harness.EnemyAnimator.IsPlayingAttackAnimation;

            // Act 2 — At exact 3.5s unfrozen boundary -> watchdog cancels attack
            harness.TimeFreeze.SetFrozen(false, unfrozenTime: 5.0f + EnemyAnimationConstants.MAX_ATTACK_ANIMATION_DURATION);
            InvokeLifecycleMethod(harness.EnemyAnimator, "HandleTransitionPropertiesChanges");

            // Assert
            Assert.IsTrue(playingBeforeDeadline);
            Assert.IsFalse(harness.EnemyAnimator.IsPlayingAttackAnimation);
            Assert.AreEqual(1, endCount);
        }

        [Test]
        public void EnemyAnimator_SynchronizeAttackAnimationState_CancelsWhenAnimatorLeavesAttackState()
        {
            // Arrange — Animator sits in non-attack state (Zombie Idle Standing) while IsPlayingAttackAnimation is true
            AnimatorHarness harness = CreateAnimatorHarness(isMovingByCrawling: false);
            harness.EnemyAnimator.RestoreInitialState("StandingLayer.Zombie Idle Standing");

            int endCount = 0;
            harness.EnemyAnimator.OnAttackAnimationEnd += delegate { endCount++; };

            harness.TimeFreeze.SetFrozen(false, unfrozenTime: 1.0f);
            harness.EnemyAnimator.Call_OnAttackAnimationStart();
            Assert.IsTrue(harness.EnemyAnimator.IsPlayingAttackAnimation);

            // Act
            InvokeLifecycleMethod(harness.EnemyAnimator, "HandleTransitionPropertiesChanges");

            // Assert
            Assert.IsFalse(harness.EnemyAnimator.IsPlayingAttackAnimation);
            Assert.AreEqual(1, endCount);
        }

        private GameObject CreateBoxCollider(string name, Vector3 center, Vector3 size, string layerName, bool isTrigger)
        {
            int layer = LayerMask.NameToLayer(layerName);
            Assert.GreaterOrEqual(layer, 0, $"Missing layer '{layerName}'");

            GameObject obj = new GameObject(name);
            obj.layer = layer;
            obj.transform.position = center;
            BoxCollider collider = obj.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = isTrigger;
            _createdObjects.Add(obj);
            Physics.SyncTransforms();
            return obj;
        }

        private FakeDamageableTarget CreateDamageablePlayer(string name, Vector3 position, Vector3 colliderSize)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            Assert.GreaterOrEqual(playerLayer, 0);

            GameObject obj = new GameObject(name);
            obj.layer = playerLayer;
            obj.transform.position = position;
            BoxCollider collider = obj.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            FakeDamageableTarget target = obj.AddComponent<FakeDamageableTarget>();
            target.Collider = collider;
            _createdObjects.Add(obj);
            Physics.SyncTransforms();
            return target;
        }

        private CollisionHarness CreateCollisionHarness(Vector3 position, float collisionRadius, bool withSelfPlayerTrigger)
        {
            GameObject root = new GameObject("CollisionTestEnemy");
            root.SetActive(false);
            root.transform.position = position;
            _createdObjects.Add(root);

            if (withSelfPlayerTrigger)
            {
                int playerLayer = LayerMask.NameToLayer("Player");
                GameObject triggerChild = new GameObject("SelfTrigger");
                triggerChild.layer = playerLayer;
                triggerChild.transform.SetParent(root.transform, false);
                BoxCollider trigger = triggerChild.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(0.5f, 0.5f, 0.5f);
            }

            root.AddComponent<Health>();
            root.AddComponent<FakeMovementController>();
            root.AddComponent<FakeDeathSequence>();
            EnemyCollisionsController collisions = root.AddComponent<EnemyCollisionsController>();
            Enemy enemy = root.AddComponent<Enemy>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<Animator>();
            EnemyAnimator animator = visual.AddComponent<EnemyAnimator>();

            SetPrivateField(typeof(EnemyCollisionsController), collisions, "_collisionRadius", collisionRadius);
            SetPrivateField(typeof(Enemy), enemy, "_visual", visual);
            SetPrivateField(typeof(EnemyAnimator), animator, "_enemy", enemy);

            Container container = new ContainerBuilder()
                .AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>))
                .AddSingleton(new FakeTimeFreezeState(), typeof(ITimeFreezeState))
                .Build();
            _createdContainers.Add(container);
            GameObjectInjector.InjectRecursive(root, container);

            root.SetActive(true);
            InvokeLifecycleMethod(animator, "Awake");
            InvokeLifecycleMethod(enemy, "Awake");
            InvokeLifecycleMethod(collisions, "Awake");
            InvokeLifecycleMethod(collisions, "OnEnable");
            Physics.SyncTransforms();

            return new CollisionHarness(root, collisions);
        }

        private AttackHarness CreateAttackHarness(Vector3 position, float attackRange, float attackArcAngle, float damage)
        {
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ZOMBIE_CONTROLLER_PATH);
            Assert.IsNotNull(controller, $"Missing controller at {ZOMBIE_CONTROLLER_PATH}");

            GameObject root = new GameObject("AttackTestEnemy");
            root.SetActive(false);
            root.transform.position = position;
            _createdObjects.Add(root);

            root.AddComponent<Health>();
            FakeCollisionsController fakeCollisions = root.AddComponent<FakeCollisionsController>();
            root.AddComponent<FakeMovementController>();
            root.AddComponent<FakeDeathSequence>();
            Enemy enemy = root.AddComponent<Enemy>();
            EnemyAttackController attack = root.AddComponent<EnemyAttackController>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            Animator unityAnimator = visual.AddComponent<Animator>();
            unityAnimator.runtimeAnimatorController = controller;
            EnemyAnimator enemyAnimator = visual.AddComponent<EnemyAnimator>();

            EnemyConfigSO config = ScriptableObject.CreateInstance<EnemyConfigSO>();
            config.MaxHealth = 100f;
            config.Damage = damage;
            _createdObjects.Add(config);

            SetPrivateField(typeof(Enemy), enemy, "<Config>k__BackingField", config);
            SetPrivateField(typeof(Enemy), enemy, "_visual", visual);
            SetPrivateField(typeof(EnemyAnimator), enemyAnimator, "_enemy", enemy);
            SetPrivateField(typeof(EnemyAttackController), attack, "_attackRange", attackRange);
            SetPrivateField(typeof(EnemyAttackController), attack, "_attackArcAngle", attackArcAngle);

            FakeTimeFreezeState timeFreeze = new FakeTimeFreezeState();
            Container container = new ContainerBuilder()
                .AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>))
                .AddSingleton(timeFreeze, typeof(ITimeFreezeState))
                .Build();
            _createdContainers.Add(container);
            GameObjectInjector.InjectRecursive(root, container);

            root.SetActive(true);
            unityAnimator.Rebind();
            unityAnimator.Update(0f);

            InvokeLifecycleMethod(enemyAnimator, "Awake");
            InvokeLifecycleMethod(enemy, "Awake");
            InvokeLifecycleMethod(attack, "Awake");

            InvokeLifecycleMethod(enemy, "OnEnable");
            InvokeLifecycleMethod(enemyAnimator, "OnEnable");
            InvokeLifecycleMethod(attack, "OnEnable");

            return new AttackHarness(root, enemy, attack, enemyAnimator, unityAnimator, fakeCollisions, timeFreeze);
        }

        private AnimatorHarness CreateAnimatorHarness(bool isMovingByCrawling)
        {
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ZOMBIE_CONTROLLER_PATH);
            Assert.IsNotNull(controller, $"Missing controller at {ZOMBIE_CONTROLLER_PATH}");

            GameObject root = new GameObject("AnimatorTestEnemy");
            root.SetActive(false);
            _createdObjects.Add(root);

            root.AddComponent<Health>();
            root.AddComponent<FakeCollisionsController>();
            FakeMovementController movement = root.AddComponent<FakeMovementController>();
            root.AddComponent<FakeDeathSequence>();
            Enemy enemy = root.AddComponent<Enemy>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            Animator unityAnimator = visual.AddComponent<Animator>();
            unityAnimator.runtimeAnimatorController = controller;
            EnemyAnimator enemyAnimator = visual.AddComponent<EnemyAnimator>();

            EnemyConfigSO config = ScriptableObject.CreateInstance<EnemyConfigSO>();
            config.MaxHealth = 100f;
            config.IsMovingByCrawling = isMovingByCrawling;
            _createdObjects.Add(config);

            SetPrivateField(typeof(Enemy), enemy, "<Config>k__BackingField", config);
            SetPrivateField(typeof(Enemy), enemy, "_visual", visual);
            SetPrivateField(typeof(EnemyAnimator), enemyAnimator, "_enemy", enemy);

            FakeTimeFreezeState timeFreeze = new FakeTimeFreezeState();
            Container container = new ContainerBuilder()
                .AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>))
                .AddSingleton(timeFreeze, typeof(ITimeFreezeState))
                .Build();
            _createdContainers.Add(container);
            GameObjectInjector.InjectRecursive(root, container);

            root.SetActive(true);
            unityAnimator.Rebind();
            unityAnimator.Update(0f);

            InvokeLifecycleMethod(enemyAnimator, "Awake");
            InvokeLifecycleMethod(enemy, "Awake");

            InvokeLifecycleMethod(enemy, "OnEnable");
            InvokeLifecycleMethod(enemyAnimator, "OnEnable");

            return new AnimatorHarness(root, enemy, enemyAnimator, unityAnimator, movement, timeFreeze);
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

        private readonly struct CollisionHarness
        {
            public readonly GameObject Root;
            public readonly EnemyCollisionsController Collisions;

            public CollisionHarness(GameObject root, EnemyCollisionsController collisions)
            {
                Root = root;
                Collisions = collisions;
            }
        }

        private readonly struct AttackHarness
        {
            public readonly GameObject Root;
            public readonly Enemy Enemy;
            public readonly EnemyAttackController Attack;
            public readonly EnemyAnimator EnemyAnimator;
            public readonly Animator UnityAnimator;
            public readonly FakeCollisionsController FakeCollisions;
            public readonly FakeTimeFreezeState TimeFreeze;

            public AttackHarness(
                GameObject root,
                Enemy enemy,
                EnemyAttackController attack,
                EnemyAnimator enemyAnimator,
                Animator unityAnimator,
                FakeCollisionsController fakeCollisions,
                FakeTimeFreezeState timeFreeze)
            {
                Root = root;
                Enemy = enemy;
                Attack = attack;
                EnemyAnimator = enemyAnimator;
                UnityAnimator = unityAnimator;
                FakeCollisions = fakeCollisions;
                TimeFreeze = timeFreeze;
            }
        }

        private readonly struct AnimatorHarness
        {
            public readonly GameObject Root;
            public readonly Enemy Enemy;
            public readonly EnemyAnimator EnemyAnimator;
            public readonly Animator UnityAnimator;
            public readonly FakeMovementController Movement;
            public readonly FakeTimeFreezeState TimeFreeze;

            public AnimatorHarness(
                GameObject root,
                Enemy enemy,
                EnemyAnimator enemyAnimator,
                Animator unityAnimator,
                FakeMovementController movement,
                FakeTimeFreezeState timeFreeze)
            {
                Root = root;
                Enemy = enemy;
                EnemyAnimator = enemyAnimator;
                UnityAnimator = unityAnimator;
                Movement = movement;
                TimeFreeze = timeFreeze;
            }
        }

        private sealed class FakeDamageableTarget : MonoBehaviour, IDamageable
        {
            public Collider Collider { get; set; }
            public float TotalDamageReceived { get; private set; }
            public int HitCount { get; private set; }

            public void TakeDamage(float damage)
            {
                TotalDamageReceived += damage;
                HitCount++;
            }

            public void TakeFullHpDamage()
            {
            }
        }

        private sealed class FakeCollisionsController : MonoBehaviour, ICollisionsController
        {
            public event EventHandler<CollisionEventArgs> OnCollisionWithPlayer;

            public void EmitCollision(Collider collider)
            {
                OnCollisionWithPlayer?.Invoke(this, new CollisionEventArgs(collider));
            }
        }

        private sealed class FakeMovementController : MonoBehaviour, IMovementController
        {
            public float Speed { get; set; }
            public bool Grounded { get; set; } = true;
            public bool CanBeTeleported { get { return true; } }

            public void ResetAfterRelocation()
            {
            }

            public float GetCurrentMovementSpeed()
            {
                return Speed;
            }

            public bool IsOnGround()
            {
                return Grounded;
            }

            public void MoveToPositionInTimeIgnoringSpeed(Vector3 pos, float time)
            {
            }

            public void SetMovementSpeedMultiplier(float multiplier)
            {
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
    }
}
