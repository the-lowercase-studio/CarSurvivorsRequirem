using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Collisions;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Enemies.Constants;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Navigation.FlowFieldSystem;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.ObjectLifecycle.Actions;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Spawners.WorldSpace;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using Grid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Scripts.Editor.Tests
{
    public class EnemyMovementControllerTests
    {
        private const float TOLERANCE = 0.0001f;
        private const float FIXED_DT = 0.02f;
        private static readonly Vector3 ISOLATED_ORIGIN = new Vector3(500f, 1500f, 500f);

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
        public void IsOnGround_DetectsWalkableGroundWithinRangeAndIgnoresTriggers()
        {
            // Arrange — Floor top surface at ISOLATED_ORIGIN.y (box center y = -0.5, size y = 1)
            CreateBoxCollider("WalkableFloor", ISOLATED_ORIGIN + new Vector3(0f, -0.5f, 0f), new Vector3(10f, 1f, 10f), "Ground", isTrigger: false);
            CreateBoxCollider("TriggerFloor", ISOLATED_ORIGIN + new Vector3(30f, -0.5f, 0f), new Vector3(10f, 1f, 10f), "Ground", isTrigger: true);
            CreateBoxCollider("RoughFloor", ISOLATED_ORIGIN + new Vector3(60f, -0.5f, 0f), new Vector3(10f, 1f, 10f), "RoughTerrain", isTrigger: false);

            MovementHarness harness = CreateHarness(ISOLATED_ORIGIN + new Vector3(0f, 1.99f, 0f), speed: 4f, acceleration: 10f);

            // Act & Assert — Inside 3.5m probe from +1.5m origin (1.99 + 1.5 - 3.5 < 0)
            Assert.IsTrue(harness.Movement.IsOnGround());

            // Just above max probe reach (2.01 + 1.5 - 3.5 = +0.01 > 0) -> false
            harness.Root.transform.position = ISOLATED_ORIGIN + new Vector3(0f, 2.01f, 0f);
            Assert.IsFalse(harness.Movement.IsOnGround());

            // Trigger collider on Ground layer is ignored
            harness.Root.transform.position = ISOLATED_ORIGIN + new Vector3(30f, 0f, 0f);
            Assert.IsFalse(harness.Movement.IsOnGround());

            // RoughTerrain is walkable
            harness.Root.transform.position = ISOLATED_ORIGIN + new Vector3(60f, 0f, 0f);
            Assert.IsTrue(harness.Movement.IsOnGround());
        }

        [Test]
        public void FixedUpdate_GroundedAboveFloor_SnapsDownTowardHitPointAtConfiguredSnapSpeed()
        {
            // Arrange — Floor top at 1500.0f, enemy starts at 1500.5f; snap rate 20 * 0.02 = 0.4 per step
            Assert.AreEqual(FIXED_DT, Time.fixedDeltaTime, TOLERANCE);
            CreateBoxCollider("SnapFloor", ISOLATED_ORIGIN + new Vector3(0f, -0.5f, 0f), new Vector3(10f, 1f, 10f), "Ground", isTrigger: false);
            MovementHarness harness = CreateHarness(ISOLATED_ORIGIN + new Vector3(0f, 0.5f, 0f), speed: 0f, acceleration: 10f);

            // Act 1 — First fixed step moves Y from 1500.5 to 1500.1
            StepFixedFrames(harness.Movement, 1);
            float yAfterFirstStep = harness.Root.transform.position.y;

            // Act 2 — Second fixed step completes snap to 1500.0
            StepFixedFrames(harness.Movement, 1);
            float yAfterSecondStep = harness.Root.transform.position.y;

            // Assert
            Assert.AreEqual(ISOLATED_ORIGIN.y + 0.1f, yAfterFirstStep, TOLERANCE);
            Assert.AreEqual(ISOLATED_ORIGIN.y, yAfterSecondStep, TOLERANCE);
        }

        [Test]
        public void FixedUpdate_UngroundedFall_AccumulatesGravityAndSuppressesHorizontalMovementPastOneUnitDrop()
        {
            // Arrange — No ground below; start grounded Y was 1500.0.
            // Gravity 25 u/s^2 at dt=0.02:
            // Step 1: v=0.5, y=1500 - 0.01 = 1499.99 (drop 0.01 <= 1.0 -> horizontal movement runs)
            // After placing at 1498.99 (< 1500 - 1.0), horizontal pursuit is suppressed while Y still falls.
            Assert.AreEqual(FIXED_DT, Time.fixedDeltaTime, TOLERANCE);
            MovementHarness harness = CreateHarness(ISOLATED_ORIGIN, speed: 5f, acceleration: 1000f);
            harness.Flow.DesiredDirection = Vector3.forward;

            // Act 1 — Initial airborne step before -1m suppression threshold
            StepFixedFrames(harness.Movement, 1);
            float yAfterStep1 = harness.Root.transform.position.y;
            float zAfterStep1 = harness.Root.transform.position.z;

            // Act 2 — Drop past 1m below _lastGroundedY and step again
            harness.Root.transform.position = new Vector3(ISOLATED_ORIGIN.x, ISOLATED_ORIGIN.y - 1.01f, zAfterStep1);
            StepFixedFrames(harness.Movement, 1);

            // Assert
            Assert.AreEqual(ISOLATED_ORIGIN.y - 0.01f, yAfterStep1, TOLERANCE);
            Assert.Greater(zAfterStep1, ISOLATED_ORIGIN.z);
            Assert.Less(harness.Root.transform.position.y, ISOLATED_ORIGIN.y - 1.01f);
            Assert.AreEqual(zAfterStep1, harness.Root.transform.position.z, TOLERANCE);
        }

        [Test]
        public void FixedUpdate_FallBelowVoidThreshold_KillsEnemyStrictlyBelowMinusTenY()
        {
            // Arrange — Step 1 from -9.985 lands at -9.995 (>= -10.0, alive). Step 2 lands at -10.015 (< -10.0, dead).
            Assert.AreEqual(FIXED_DT, Time.fixedDeltaTime, TOLERANCE);
            Vector3 startPos = new Vector3(ISOLATED_ORIGIN.x, -9.985f, ISOLATED_ORIGIN.z);
            MovementHarness harness = CreateHarness(startPos, speed: 0f, acceleration: 10f);

            // Act 1
            StepFixedFrames(harness.Movement, 1);
            bool aliveJustAboveThreshold = harness.Health.IsAlive();

            // Act 2
            StepFixedFrames(harness.Movement, 1);
            bool aliveBelowThreshold = harness.Health.IsAlive();

            // Assert
            Assert.AreEqual(-9.995f, startPos.y - 0.01f, TOLERANCE);
            Assert.IsTrue(aliveJustAboveThreshold);
            Assert.IsFalse(aliveBelowThreshold);
        }

        [Test]
        public void FixedUpdate_FlowFieldAccelerationAndFallbackAcceleration_ComputeExpectedStepAndSpeed()
        {
            // Arrange — Floor at Y = 1500f; explicit acceleration 10 with speed 5 -> step 1: v = 0.2, dz = 0.004
            Assert.AreEqual(FIXED_DT, Time.fixedDeltaTime, TOLERANCE);
            CreateBoxCollider("MoveFloor", ISOLATED_ORIGIN + new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f), "Ground", isTrigger: false);

            MovementHarness explicitHarness = CreateHarness(ISOLATED_ORIGIN, speed: 5f, acceleration: 10f);
            explicitHarness.Flow.DesiredDirection = Vector3.forward;

            // Fallback acceleration (Config.Acceleration = 0): effectiveAccel = 5 * 4 = 20 -> step 1: v = 0.4, dz = 0.008
            MovementHarness fallbackHarness = CreateHarness(ISOLATED_ORIGIN + new Vector3(4f, 0f, 0f), speed: 5f, acceleration: 0f);
            fallbackHarness.Flow.DesiredDirection = Vector3.forward;

            // Act
            StepFixedFrames(explicitHarness.Movement, 1);
            StepFixedFrames(fallbackHarness.Movement, 1);

            // Assert
            Assert.AreEqual(ISOLATED_ORIGIN.z + 0.004f, explicitHarness.Root.transform.position.z, TOLERANCE);
            Assert.AreEqual(0.2f, explicitHarness.Movement.GetCurrentMovementSpeed(), TOLERANCE);
            Assert.AreEqual(ISOLATED_ORIGIN.z + 0.008f, fallbackHarness.Root.transform.position.z, TOLERANCE);
            Assert.AreEqual(0.4f, fallbackHarness.Movement.GetCurrentMovementSpeed(), TOLERANCE);
        }

        [Test]
        public void SetMovementSpeedMultiplier_ScalesSpeedClampsNegativeAndResetsOnLifecycleAndRelocation()
        {
            // Arrange — Floor inside ISOLATED_ORIGIN; speed 5, accel 1000 reaches target velocity immediately on step 1
            Assert.AreEqual(FIXED_DT, Time.fixedDeltaTime, TOLERANCE);
            CreateBoxCollider("MultiplierFloor", ISOLATED_ORIGIN + new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f), "Ground", isTrigger: false);
            MovementHarness harness = CreateHarness(ISOLATED_ORIGIN, speed: 5f, acceleration: 1000f);
            harness.Flow.DesiredDirection = Vector3.forward;

            // Act 1 — 0.6x slow (effective speed = 3 -> dz = 0.06)
            harness.Movement.SetMovementSpeedMultiplier(0.6f);
            StepFixedFrames(harness.Movement, 1);
            float slowSpeed = harness.Movement.GetCurrentMovementSpeed();

            // Act 2 — Negative multiplier clamps to 0
            harness.Movement.SetMovementSpeedMultiplier(-3f);
            StepFixedFrames(harness.Movement, 1);
            float clampedSpeed = harness.Movement.GetCurrentMovementSpeed();

            // Act 3 — ResetAfterRelocation restores 1.0x multiplier
            harness.Movement.ResetAfterRelocation();
            StepFixedFrames(harness.Movement, 1);
            float speedAfterRelocation = harness.Movement.GetCurrentMovementSpeed();

            // Act 4 — Apply slow again then OnDisable -> OnEnable restores 1.0x multiplier
            harness.Movement.SetMovementSpeedMultiplier(0.5f);
            InvokeLifecycleMethod(harness.Movement, "OnDisable");
            InvokeLifecycleMethod(harness.Enemy, "OnDisable");
            InvokeLifecycleMethod(harness.Health, "OnDisable");
            harness.Root.SetActive(false);

            harness.Root.SetActive(true);
            InvokeLifecycleMethod(harness.Health, "OnEnable");
            InvokeLifecycleMethod(harness.Enemy, "OnEnable");
            InvokeLifecycleMethod(harness.Movement, "OnEnable");
            StepFixedFrames(harness.Movement, 1);
            float speedAfterReEnable = harness.Movement.GetCurrentMovementSpeed();

            // Assert
            Assert.AreEqual(3f, slowSpeed, TOLERANCE);
            Assert.AreEqual(0f, clampedSpeed, TOLERANCE);
            Assert.AreEqual(5f, speedAfterRelocation, TOLERANCE);
            Assert.AreEqual(5f, speedAfterReEnable, TOLERANCE);
            Assert.AreEqual(1, harness.Flow.ResetCount);
        }

        [Test]
        public void FixedUpdate_InvalidVoluntaryStepInsideWorldBounds_UsesFlowFallbackOrClampsInPlace()
        {
            // Arrange — WorldGrid [0..8] x [0..8] at Y=1500f so position (2, 1500, 2) is strictly within XZ bounds
            Assert.AreEqual(FIXED_DT, Time.fixedDeltaTime, TOLERANCE);
            Vector3 worldSpaceStart = new Vector3(2f, 1500f, 2f);
            CreateBoxCollider("WorldGridFloor", new Vector3(4f, 1499.5f, 4f), new Vector3(12f, 1f, 12f), "Ground", isTrigger: false);
            Grid worldGrid = new Grid(new GridConfiguration(4, 4, 2f));

            MovementHarness fallbackFlowHarness = CreateHarness(worldSpaceStart, speed: 5f, acceleration: 1000f, worldGrid: worldGrid);
            fallbackFlowHarness.Flow.DesiredDirection = new Vector3(1f, 0f, 1f).normalized;
            fallbackFlowHarness.Flow.FlowDirection = Vector3.forward;
            fallbackFlowHarness.Flow.AllowDiagonalStep = false;
            fallbackFlowHarness.Flow.AllowFlowFallbackStep = true;

            MovementHarness fullyBlockedHarness = CreateHarness(worldSpaceStart + new Vector3(2f, 0f, 0f), speed: 5f, acceleration: 1000f, worldGrid: worldGrid);
            fullyBlockedHarness.Flow.DesiredDirection = new Vector3(1f, 0f, 1f).normalized;
            fullyBlockedHarness.Flow.FlowDirection = Vector3.forward;
            fullyBlockedHarness.Flow.AllowDiagonalStep = false;
            fullyBlockedHarness.Flow.AllowFlowFallbackStep = false;

            // Act
            StepFixedFrames(fallbackFlowHarness.Movement, 1);
            StepFixedFrames(fullyBlockedHarness.Movement, 1);

            // Assert — First takes pure flow fallback along +Z (dz = 5 * 0.02 = 0.1, dx = 0); second stays clamped at x=4, z=2
            Assert.AreEqual(2f, fallbackFlowHarness.Root.transform.position.x, TOLERANCE);
            Assert.AreEqual(2.1f, fallbackFlowHarness.Root.transform.position.z, TOLERANCE);
            Assert.AreEqual(5f, fallbackFlowHarness.Movement.GetCurrentMovementSpeed(), TOLERANCE);

            Assert.AreEqual(4f, fullyBlockedHarness.Root.transform.position.x, TOLERANCE);
            Assert.AreEqual(2f, fullyBlockedHarness.Root.transform.position.z, TOLERANCE);
            Assert.AreEqual(0f, fullyBlockedHarness.Movement.GetCurrentMovementSpeed(), TOLERANCE);
        }

        [Test]
        public void FixedUpdate_FrozenAttackingPostAttackDelayAndPursuitSuppression_GateMovementAndVelocity()
        {
            // Arrange
            Assert.AreEqual(FIXED_DT, Time.fixedDeltaTime, TOLERANCE);
            CreateBoxCollider("GatingFloor", ISOLATED_ORIGIN + new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f), "Ground", isTrigger: false);
            MovementHarness harness = CreateHarness(ISOLATED_ORIGIN, speed: 5f, acceleration: 1000f);
            harness.Flow.DesiredDirection = Vector3.forward;

            // Freeze stops movement and zeroes velocity
            StepFixedFrames(harness.Movement, 1);
            float zMoving = harness.Root.transform.position.z;
            harness.TimeFreeze.IsFrozen = true;
            InvokeLifecycleMethod(harness.Movement, "FixedUpdate");
            Assert.AreEqual(0f, harness.Movement.GetCurrentMovementSpeed(), TOLERANCE);
            Assert.AreEqual(zMoving, harness.Root.transform.position.z, TOLERANCE);
            harness.TimeFreeze.IsFrozen = false;

            // Attack animation active blocks movement; ending attack enforces 0.2s cooldown before resuming
            // (10 fixed frames leave ~7e-9f float residue > 0, so step 11 clears cooldown and step 12 resumes movement).
            harness.Animator.Call_OnAttackAnimationStart();
            StepFixedFrames(harness.Movement, 2);
            Assert.AreEqual(zMoving, harness.Root.transform.position.z, TOLERANCE);

            harness.Animator.Call_OnAttackAnimationEnd();
            StepFixedFrames(harness.Movement, 10);
            float zDuringCooldown = harness.Root.transform.position.z;
            Assert.AreEqual(zMoving, zDuringCooldown, TOLERANCE);

            // Step 11 clears float residue, step 12 resumes pursuit; SetPursuitSuppressed halts again
            StepFixedFrames(harness.Movement, 2);
            float zAfterCooldown = harness.Root.transform.position.z;
            Assert.Greater(zAfterCooldown, zDuringCooldown);

            harness.Movement.SetPursuitSuppressed(true);
            StepFixedFrames(harness.Movement, 2);
            Assert.AreEqual(zAfterCooldown, harness.Root.transform.position.z, TOLERANCE);
        }

        [Test]
        public void FixedUpdate_RotatesTowardVelocityAboveRotationThreshold()
        {
            // Arrange
            Assert.AreEqual(FIXED_DT, Time.fixedDeltaTime, TOLERANCE);
            CreateBoxCollider("RotateFloor", ISOLATED_ORIGIN + new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f), "Ground", isTrigger: false);
            MovementHarness harness = CreateHarness(ISOLATED_ORIGIN, speed: 5f, acceleration: 1000f);
            harness.Root.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            harness.Flow.DesiredDirection = Vector3.right;

            // Act
            StepFixedFrames(harness.Movement, 5);

            // Assert — Enemy rotated toward +X
            Assert.Greater(harness.Root.transform.forward.x, 0.2f);
        }

        [Test]
        public void Knockback_ClearPathAcrossWorldBoundary_AppliesSineEasingAndContinuesGravity()
        {
            // Arrange — Start just inside [0..4] WorldGrid at x=3.5, knock +4m in X (outside grid) over 0.1s with NO floor
            Assert.AreEqual(FIXED_DT, Time.fixedDeltaTime, TOLERANCE);
            Vector3 startPos = new Vector3(3.5f, 1500f, 2f);
            Grid worldGrid = new Grid(new GridConfiguration(2, 2, 2f));
            MovementHarness harness = CreateHarness(startPos, speed: 5f, acceleration: 1000f, worldGrid: worldGrid);
            harness.Flow.AllowDiagonalStep = false;
            harness.Flow.AllowFlowFallbackStep = false;

            // Act 1 — Move 1 fixed step (t = 0.02 / 0.1 = 0.2 -> sin(0.2 * PI * 0.5) = sin(0.1 * PI))
            harness.Movement.MoveToPositionInTimeIgnoringSpeed(startPos + new Vector3(4f, 0f, 0f), 0.1f);
            StepFixedFrames(harness.Movement, 1);

            float expectedFirstX = 3.5f + (4f * Mathf.Sin(0.2f * Mathf.PI * 0.5f));
            float xAfterFirst = harness.Root.transform.position.x;
            float yAfterFirst = harness.Root.transform.position.y;
            float knockSpeed = harness.Movement.GetCurrentMovementSpeed();

            // Act 2 — Complete remaining 4 steps (total 0.1s)
            StepFixedFrames(harness.Movement, 4);

            // Assert — Crossed outside WorldGrid x>=4 without grid clamping, gravity pulled Y down, arrived at 7.5f
            Assert.AreEqual(expectedFirstX, xAfterFirst, TOLERANCE);
            Assert.Less(yAfterFirst, 1500f);
            Assert.Greater(knockSpeed, 0f);
            Assert.AreEqual(7.5f, harness.Root.transform.position.x, TOLERANCE);
        }

        [Test]
        public void Knockback_BlockedByImpassableObstacle_ClampsBeforeWallAndScalesDuration()
        {
            // Arrange — Enemy at ISOLATED_ORIGIN; Impassable box centered at +X=3.0 size X=2.0 -> near face at +2.0.
            // SphereCast radius=0.4 hits at distance 1.6 -> safeDistance = 1.6 - 0.1 = 1.5; adjustedTime = 0.4 * (1.5 / 4.0) = 0.15s.
            Assert.AreEqual(FIXED_DT, Time.fixedDeltaTime, TOLERANCE);
            CreateBoxCollider("KnockFloor", ISOLATED_ORIGIN + new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f), "Ground", isTrigger: false);
            CreateBoxCollider("ImpassableWall", ISOLATED_ORIGIN + new Vector3(3f, 0.5f, 0f), new Vector3(2f, 2f, 4f), "Impassable", isTrigger: false);
            MovementHarness harness = CreateHarness(ISOLATED_ORIGIN, speed: 0f, acceleration: 10f);

            // Act — Request 4.0m knockback through the wall over 0.4s; run 8 steps (0.16s >= 0.15s)
            harness.Movement.MoveToPositionInTimeIgnoringSpeed(ISOLATED_ORIGIN + new Vector3(4f, 0f, 0f), 0.4f);
            StepFixedFrames(harness.Movement, 8);

            // Assert
            Assert.AreEqual(ISOLATED_ORIGIN.x + 1.5f, harness.Root.transform.position.x, 0.01f);
        }

        [Test]
        public void StopMovement_HaltsHorizontalGravityAndIgnoresSubsequentKnockback()
        {
            // Arrange — No ground underneath; StopMovement must freeze all FixedUpdate displacement and reject knockback
            Assert.AreEqual(FIXED_DT, Time.fixedDeltaTime, TOLERANCE);
            MovementHarness harness = CreateHarness(ISOLATED_ORIGIN, speed: 5f, acceleration: 1000f);
            harness.Flow.DesiredDirection = Vector3.forward;

            // Act
            harness.Movement.StopMovement();
            harness.Movement.MoveToPositionInTimeIgnoringSpeed(ISOLATED_ORIGIN + Vector3.right * 5f, 0.2f);
            StepFixedFrames(harness.Movement, 5);

            // Assert
            Assert.AreEqual(ISOLATED_ORIGIN.x, harness.Root.transform.position.x, TOLERANCE);
            Assert.AreEqual(ISOLATED_ORIGIN.y, harness.Root.transform.position.y, TOLERANCE);
            Assert.AreEqual(ISOLATED_ORIGIN.z, harness.Root.transform.position.z, TOLERANCE);
            Assert.AreEqual(0f, harness.Movement.GetCurrentMovementSpeed(), TOLERANCE);
        }

        [Test]
        public void IsMaterializing_HaltsMovementIgnoresKnockbackAndBlocksTeleportation()
        {
            // Arrange
            Assert.AreEqual(FIXED_DT, Time.fixedDeltaTime, TOLERANCE);
            CreateBoxCollider("WalkableFloor", ISOLATED_ORIGIN + new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f), "Ground", isTrigger: false);
            MovementHarness harness = CreateHarness(ISOLATED_ORIGIN, speed: 5f, acceleration: 1000f);
            harness.Root.AddComponent<BoxCollider>();
            InvokeLifecycleMethod(harness.Enemy, "Awake");
            harness.Flow.DesiredDirection = Vector3.forward;

            // Act
            harness.Enemy.BeginMaterialization(2f);
            harness.Movement.MoveToPositionInTimeIgnoringSpeed(ISOLATED_ORIGIN + Vector3.right * 5f, 0.2f);
            StepFixedFrames(harness.Movement, 5);

            // Assert — Paused, immune to knockback, and excluded from chunk teleporter while materializing
            Assert.IsFalse(harness.Movement.CanBeTeleported);
            Assert.AreEqual(ISOLATED_ORIGIN.x, harness.Root.transform.position.x, TOLERANCE);
            Assert.AreEqual(ISOLATED_ORIGIN.y, harness.Root.transform.position.y, TOLERANCE);
            Assert.AreEqual(ISOLATED_ORIGIN.z, harness.Root.transform.position.z, TOLERANCE);
            Assert.AreEqual(0f, harness.Movement.GetCurrentMovementSpeed(), TOLERANCE);

            // Act — Completing materialization resumes movement and resets relocation grace window
            harness.Enemy.CompleteMaterialization();
            StepFixedFrames(harness.Movement, 3);
            Assert.Greater(harness.Root.transform.position.z, ISOLATED_ORIGIN.z);
        }

        private void StepFixedFrames(EnemyMovementController movement, int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
            {
                InvokeLifecycleMethod(movement, "FixedUpdate");
            }
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

        private MovementHarness CreateHarness(Vector3 startPos, float speed, float acceleration, Grid worldGrid = null)
        {
            GameObject root = new GameObject("MovementTestEnemy");
            root.SetActive(false);
            root.transform.position = startPos;
            _createdObjects.Add(root);

            Health health = root.AddComponent<Health>();
            root.AddComponent<FakeCollisionsController>();
            FakeFlowFieldMovementController flow = root.AddComponent<FakeFlowFieldMovementController>();
            root.AddComponent<FakeDeathSequence>();
            Enemy enemy = root.AddComponent<Enemy>();
            EnemyMovementController movement = root.AddComponent<EnemyMovementController>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<Animator>();
            EnemyAnimator animator = visual.AddComponent<EnemyAnimator>();

            EnemyConfigSO config = ScriptableObject.CreateInstance<EnemyConfigSO>();
            config.MaxHealth = 100f;
            config.MovementSpeed = speed;
            config.RotationSpeed = 10f;
            config.Acceleration = acceleration;
            _createdObjects.Add(config);

            SetPrivateField(typeof(Enemy), enemy, "<Config>k__BackingField", config);
            SetPrivateField(typeof(Enemy), enemy, "_visual", visual);
            SetPrivateField(typeof(EnemyAnimator), animator, "_enemy", enemy);

            FakeTimeFreezeState timeFreeze = new FakeTimeFreezeState();
            FakeGridManager gridManager = new FakeGridManager(worldGrid);

            Container container = new ContainerBuilder()
                .AddSingleton(new FakeDamageNumbersSpawner(), typeof(IInWorldSpaceSpawner<DamageNumbersSpawner, DamageNubmersSpawnerConfig>))
                .AddSingleton(timeFreeze, typeof(ITimeFreezeState))
                .AddSingleton(gridManager, typeof(IGridManager))
                .Build();
            _createdContainers.Add(container);
            GameObjectInjector.InjectRecursive(root, container);

            root.SetActive(true);

            InvokeLifecycleMethod(animator, "Awake");
            InvokeLifecycleMethod(enemy, "Awake");
            InvokeLifecycleMethod(movement, "Awake");
            SetPrivateField(typeof(EnemyMovementController), movement, "_flowFieldMovementController", flow);

            InvokeLifecycleMethod(health, "OnEnable");
            InvokeLifecycleMethod(enemy, "OnEnable");
            InvokeLifecycleMethod(movement, "OnEnable");
            InvokeLifecycleMethod(animator, "OnEnable");

            return new MovementHarness(root, enemy, movement, health, animator, flow, timeFreeze);
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

        private readonly struct MovementHarness
        {
            public readonly GameObject Root;
            public readonly Enemy Enemy;
            public readonly EnemyMovementController Movement;
            public readonly Health Health;
            public readonly EnemyAnimator Animator;
            public readonly FakeFlowFieldMovementController Flow;
            public readonly FakeTimeFreezeState TimeFreeze;

            public MovementHarness(
                GameObject root,
                Enemy enemy,
                EnemyMovementController movement,
                Health health,
                EnemyAnimator animator,
                FakeFlowFieldMovementController flow,
                FakeTimeFreezeState timeFreeze)
            {
                Root = root;
                Enemy = enemy;
                Movement = movement;
                Health = health;
                Animator = animator;
                Flow = flow;
                TimeFreeze = timeFreeze;
            }
        }

        private sealed class FakeFlowFieldMovementController : MonoBehaviour, IFlowFieldMovementController
        {
            public Vector3 DesiredDirection { get; set; }
            public Vector3 FlowDirection { get; set; }
            public bool AllowDiagonalStep { get; set; } = true;
            public bool AllowFlowFallbackStep { get; set; } = true;
            public int ResetCount { get; private set; }

            public Vector3 CalculateDesiredMovementDirection()
            {
                return DesiredDirection;
            }

            public Vector3 GetFlowDirection()
            {
                return FlowDirection;
            }

            public bool IsValidVoluntaryStep(Vector3 from, Vector3 to)
            {
                Vector3 delta = to - from;
                if (Mathf.Abs(delta.x) > 0.0001f && Mathf.Abs(delta.z) > 0.0001f)
                {
                    return AllowDiagonalStep;
                }

                return AllowFlowFallbackStep;
            }

            public void ResetAfterRelocation()
            {
                ResetCount++;
            }

            public Vector3 MoveOnFlowFieldGrid(float movementSpeed)
            {
                return Vector3.zero;
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
            public bool IsFrozen { get; set; }
            public float UnfrozenTime { get; set; }

            public event Action<bool> OnFrozenChanged
            {
                add { }
                remove { }
            }
        }

        private sealed class FakeGridManager : IGridManager
        {
            public Cell DestinationCell { get { return null; } }
            public Grid GridPlayerChunk { get { return WorldGrid; } }
            public Grid WorldGrid { get; }

            public FakeGridManager(Grid worldGrid)
            {
                WorldGrid = worldGrid;
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
