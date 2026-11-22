using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.ScriptableObjects;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.Constants;
using Assets.Scripts.Skills.PlayerSkills.Lasergun;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.VFX;
using NUnit.Framework;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class LasergunTurretTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
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
        public void SetNumberOfTargets_ClampsBelowMinimumAndResizesLineRenderersAndTargetBuffers()
        {
            LasergunTurretRig rig = CreateTurretRig(Vector3.zero, range: 15f, damage: 5, rotationSpeed: 360f);

            rig.Turret.SetNumberOfTargets(0);
            Assert.AreEqual(SkillConstants.MIN_NUMBER_OF_TARGETS, GetPrivateField<int>(typeof(LasergunTurret), rig.Turret, "_numberOfTargets"));
            Assert.AreEqual(1, GetPrivateField<LineRenderer[]>(typeof(LasergunTurret), rig.Turret, "_laserLineRenderers").Length);

            rig.Turret.SetNumberOfTargets(3);
            Assert.AreEqual(3, GetPrivateField<int>(typeof(LasergunTurret), rig.Turret, "_numberOfTargets"));
            Assert.AreEqual(3, GetPrivateField<LineRenderer[]>(typeof(LasergunTurret), rig.Turret, "_laserLineRenderers").Length);
            Assert.AreEqual(3, GetPrivateField<Collider[]>(typeof(LasergunTurret), rig.Turret, "_trackedTargets").Length);
        }

        [Test]
        public void AssignNewTargets_SortsClosestVisibleTargetsExcludesOccludedDisabledAndOutOfRange()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int wallLayer = LayerMask.NameToLayer("Impassable");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");
            Assert.GreaterOrEqual(wallLayer, 0, "Impassable layer must exist.");

            Vector3 origin = new Vector3(1600f, 0f, 0f);
            LasergunTurretRig rig = CreateTurretRig(origin, range: 12f, damage: 6, rotationSpeed: 360f);
            rig.Turret.SetNumberOfTargets(2);

            TestLaserEnemy farVisible = CreateEnemy("FarVisible", enemyLayer, origin + new Vector3(0f, 0f, 10f));
            TestLaserEnemy closestVisible = CreateEnemy("ClosestVisible", enemyLayer, origin + new Vector3(0f, 0f, 4f));
            TestLaserEnemy thirdVisible = CreateEnemy("ThirdVisible", enemyLayer, origin + new Vector3(0f, 0f, 11f));

            // Disabled collider enemy at 2m must be ignored.
            GameObject disabledGo = TrackGameObject("DisabledEnemy", active: true);
            disabledGo.layer = enemyLayer;
            disabledGo.transform.position = origin + new Vector3(0f, 0f, 2f);
            BoxCollider disabledCollider = disabledGo.AddComponent<BoxCollider>();
            disabledCollider.enabled = false;
            disabledGo.AddComponent<TestLaserEnemy>();

            // Out-of-range enemy at 15m (> 12m) must be ignored.
            CreateEnemy("OutOfRange", enemyLayer, origin + new Vector3(0f, 0f, 15f));

            // Occluded enemy behind terrain wall at 3m must be ignored.
            CreateWall("Wall", wallLayer, origin + new Vector3(3f, 0f, 0f), new Vector3(1f, 2f, 2f));
            CreateEnemy("Occluded", enemyLayer, origin + new Vector3(6f, 0f, 0f));

            Physics.SyncTransforms();

            InvokeMethod(rig.Turret, "AssignNewTargets");

            Collider primary = GetPrivateField<Collider>(typeof(LasergunTurret), rig.Turret, "_primaryTarget");
            Collider[] tracked = GetPrivateField<Collider[]>(typeof(LasergunTurret), rig.Turret, "_trackedTargets");
            int trackedCount = GetPrivateField<int>(typeof(LasergunTurret), rig.Turret, "_trackedTargetsCount");

            Assert.AreEqual(2, trackedCount);
            Assert.AreSame(closestVisible.GetComponent<Collider>(), primary);
            Assert.AreSame(closestVisible.GetComponent<Collider>(), tracked[0]);
            Assert.AreSame(farVisible.GetComponent<Collider>(), tracked[1]);
            Assert.AreEqual(0f, thirdVisible.TotalDamageTaken, 0.001f);
        }

        [Test]
        public void HandleRotation_SetsLookingAtTargetBasedOnAngleThresholdAndRotatesTowardPrimaryTarget()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");

            Vector3 origin = new Vector3(1800f, 0f, 0f);
            LasergunTurretRig rig = CreateTurretRig(origin, range: 15f, damage: 5, rotationSpeed: 100f);

            // Place enemy 90 degrees to the right.
            CreateEnemy("RightEnemy", enemyLayer, origin + new Vector3(5f, 0f, 0f));
            Physics.SyncTransforms();

            InvokeMethod(rig.Turret, "AssignNewTargets");
            InvokeMethod(rig.Turret, "HandleRotation");

            Assert.IsFalse(GetPrivateField<bool>(typeof(LasergunTurret), rig.Turret, "_isLookingAtTarget"));
            Assert.Greater(rig.Turret.transform.eulerAngles.y, 0.1f);

            // Aim turret within 2 degrees (< SMALLEST_ANGLE_QUALIFYING_AS_LOOKING_AT_TARGET = 4f) of target.
            rig.Turret.transform.rotation = Quaternion.Euler(0f, 88f, 0f);
            InvokeMethod(rig.Turret, "HandleRotation");
            Assert.IsTrue(GetPrivateField<bool>(typeof(LasergunTurret), rig.Turret, "_isLookingAtTarget"));
        }

        [Test]
        public void Shoot_DamagesAllValidTrackedTargetsSkipsOutOfRangeAndCleansUpOnDisable()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.GreaterOrEqual(enemyLayer, 0, "Enemy layer must exist.");

            Vector3 origin = new Vector3(2000f, 0f, 0f);
            LasergunTurretRig rig = CreateTurretRig(origin, range: 10f, damage: 9, rotationSpeed: 360f);
            rig.Turret.SetNumberOfTargets(3);

            TestLaserEnemy target1 = CreateEnemy("Target1", enemyLayer, origin + new Vector3(0f, 0f, 4f));
            TestLaserEnemy target2 = CreateEnemy("Target2", enemyLayer, origin + new Vector3(0f, 0f, 7f));
            TestLaserEnemy target3 = CreateEnemy("Target3", enemyLayer, origin + new Vector3(0f, 0f, 9f));
            Physics.SyncTransforms();

            InvokeMethod(rig.Turret, "AssignNewTargets");

            // Not looking at target -> Shoot must do nothing.
            SetPrivateField(typeof(LasergunTurret), rig.Turret, "_isLookingAtTarget", false);
            rig.Turret.Shoot(1f);
            Assert.AreEqual(0f, target1.TotalDamageTaken, 0.001f);

            // Move target3 out of range before firing; target1 and target2 remain valid.
            target3.transform.position = origin + new Vector3(0f, 0f, 20f);
            Physics.SyncTransforms();

            SetPrivateField(typeof(LasergunTurret), rig.Turret, "_isLookingAtTarget", true);
            rig.Turret.Shoot(1f);

            Assert.AreEqual(9f, target1.TotalDamageTaken, 0.001f);
            Assert.AreEqual(9f, target2.TotalDamageTaken, 0.001f);
            Assert.AreEqual(0f, target3.TotalDamageTaken, 0.001f);
            Assert.AreEqual(1, target1.PositionalHitCount);
            Assert.AreEqual(2, rig.PrimaryLineRenderer.positionCount);

            // OnDisable must reset showing/charging flags, clear primary target, and reset all line renderers.
            InvokeMethod(rig.Turret, "OnDisable");
            Assert.IsFalse(GetPrivateField<bool>(typeof(LasergunTurret), rig.Turret, "_isShowingLaser"));
            Assert.IsFalse(GetPrivateField<bool>(typeof(LasergunTurret), rig.Turret, "_isChargingLaser"));
            Assert.IsNull(GetPrivateField<Collider>(typeof(LasergunTurret), rig.Turret, "_primaryTarget"));
            Assert.AreEqual(0, rig.PrimaryLineRenderer.positionCount);
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

        private LasergunTurretRig CreateTurretRig(Vector3 position, float range, int damage, float rotationSpeed)
        {
            TurretConfigSO turretConfig = TrackScriptableObject<TurretConfigSO>();
            ProjectileConfigSO projectileConfig = TrackScriptableObject<ProjectileConfigSO>();
            projectileConfig.Damage = damage;
            projectileConfig.Range = range;
            turretConfig.Range = range;
            turretConfig.ProjectileStatsSO = projectileConfig;
            SetProperty(typeof(TurretConfigSO), turretConfig, nameof(TurretConfigSO.RotationSpeed), rotationSpeed);
            SetProperty(typeof(TurretConfigSO), turretConfig, nameof(TurretConfigSO.SearchForTargetInterval), 0.1f);

            GameObject turretGo = TrackGameObject("LasergunTurret", active: true);
            turretGo.transform.position = position;

            GameObject gunTip = new GameObject("GunTip");
            gunTip.transform.SetParent(turretGo.transform, false);
            LineRenderer lineRenderer = gunTip.AddComponent<LineRenderer>();

            LasergunTurret turret = turretGo.AddComponent<LasergunTurret>();
            SetPrivateField(typeof(Turret<TurretConfigSO>), turret, "_gunTip", gunTip.transform);
            SetPrivateField(typeof(LasergunTurret), turret, "_laserLineRenderer", lineRenderer);
            SetPrivateField(typeof(LasergunTurret), turret, "_laserCumulatingVFX", null);

            InvokeMethod(turret, "Awake");
            turret.Initialize(turretConfig);

            return new LasergunTurretRig
            {
                Turret = turret,
                PrimaryLineRenderer = lineRenderer
            };
        }

        private TestLaserEnemy CreateEnemy(string name, int layer, Vector3 position)
        {
            GameObject go = TrackGameObject(name, active: true);
            go.layer = layer;
            go.transform.position = position;
            go.AddComponent<BoxCollider>();
            return go.AddComponent<TestLaserEnemy>();
        }

        private void CreateWall(string name, int layer, Vector3 position, Vector3 size)
        {
            GameObject go = TrackGameObject(name, active: true);
            go.layer = layer;
            go.transform.position = position;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
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

        private sealed class LasergunTurretRig
        {
            public LasergunTurret Turret { get; set; }
            public LineRenderer PrimaryLineRenderer { get; set; }
        }

        private sealed class TestLaserEnemy : MonoBehaviour, IPositionalDamageable
        {
            public float TotalDamageTaken { get; private set; }
            public int PositionalHitCount { get; private set; }

            public void TakeDamage(float damage)
            {
                TotalDamageTaken += damage;
            }

            public void TakeDamage(float damage, Vector3 hitWorldPosition)
            {
                PositionalHitCount++;
                TotalDamageTaken += damage;
            }

            public void TakeFullHpDamage()
            {
                TotalDamageTaken += 1000f;
            }
        }
    }
}
