using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Enemies;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.Pooling;
using Assets.Scripts.PowerUps;
using Assets.Scripts.PowerUps.Collectibles;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class CollectiblePickupsTests
    {
        private const float TOLERANCE = 0.0001f;

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private bool _previousDotweenInitialized;

        public enum NonPlayerLayerKind
        {
            Default,
            Enemy,
            Boss
        }

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
        public void SkillCrate_Awake_AssignsGameObjectAndSatisfiesContracts()
        {
            // Arrange
            GameObject go = CreateInactiveGameObject("TestSkillCrate");
            SkillCrate crate = go.AddComponent<SkillCrate>();
            Assert.IsNull(crate.GameObject);

            // Act
            InvokeLifecycleMethod(crate, "Awake");

            // Assert
            Assert.AreSame(go, crate.GameObject);
            Assert.IsInstanceOf<ISkillUpgradeCollectible>(crate);
            Assert.IsInstanceOf<ICollectible>(crate);
            Assert.IsInstanceOf<IPoolable>(crate);
        }

        [Test]
        public void SkillCrate_OnTriggerEnter_PlayerLayer_RaisesOnCollectedBeforeRelease()
        {
            // Arrange
            SkillCrate crate = CreateAwakenedSkillCrate();
            Collider playerCollider = CreateColliderOnLayer("PlayerCollider", GetLayerIndexFromMask(EntityLayers.Player.value));
            List<string> eventOrder = new();
            crate.OnCollected += delegate { eventOrder.Add("OnCollected"); };
            crate.OnCanBeReleased += delegate { eventOrder.Add("OnCanBeReleased"); };

            // Act
            InvokeTriggerEnter(crate, playerCollider);

            // Assert
            CollectionAssert.AreEqual(new[] { "OnCollected", "OnCanBeReleased" }, eventOrder);
        }

        [TestCase(NonPlayerLayerKind.Default, TestName = "SkillCrate_OnTriggerEnter_DefaultLayer_IgnoresTrigger")]
        [TestCase(NonPlayerLayerKind.Enemy, TestName = "SkillCrate_OnTriggerEnter_EnemyLayer_IgnoresTrigger")]
        [TestCase(NonPlayerLayerKind.Boss, TestName = "SkillCrate_OnTriggerEnter_BossLayer_IgnoresTrigger")]
        public void SkillCrate_OnTriggerEnter_NonPlayerLayer_IgnoresTrigger(NonPlayerLayerKind layerKind)
        {
            // Arrange
            SkillCrate crate = CreateAwakenedSkillCrate();
            Collider otherCollider = CreateColliderOnLayer("NonPlayerCollider", ResolveNonPlayerLayer(layerKind));
            int collectedCount = 0;
            int releasedCount = 0;
            crate.OnCollected += delegate { collectedCount++; };
            crate.OnCanBeReleased += delegate { releasedCount++; };

            // Act
            InvokeTriggerEnter(crate, otherCollider);

            // Assert
            Assert.AreEqual(0, collectedCount);
            Assert.AreEqual(0, releasedCount);
        }

        [Test]
        public void SkillCrate_OnTriggerEnter_DuplicatePlayerTriggersInSingleCheckout_CollectsOnlyOnce()
        {
            // Arrange
            SkillCrate crate = CreateAwakenedSkillCrate();
            crate.OnGet();
            Collider playerCollider = CreateColliderOnLayer("PlayerCollider", GetLayerIndexFromMask(EntityLayers.Player.value));
            int collectedCount = 0;
            int releasedCount = 0;
            crate.OnCollected += delegate { collectedCount++; };
            crate.OnCanBeReleased += delegate { releasedCount++; };

            // Act — Two Player-layer triggers hit in the same checkout before deactivation
            InvokeTriggerEnter(crate, playerCollider);
            InvokeTriggerEnter(crate, playerCollider);

            // Assert
            Assert.AreEqual(1, collectedCount);
            Assert.AreEqual(1, releasedCount);
        }

        [Test]
        public void SkillCrate_ReturnToPool_WithAndWithoutSubscribers_RaisesOnCanBeReleased()
        {
            // Arrange
            SkillCrate crate = CreateAwakenedSkillCrate();
            Assert.DoesNotThrow(delegate { crate.ReturnToPool(); });

            int releasedCount = 0;
            object eventSender = null;
            crate.OnCanBeReleased += delegate(object sender, EventArgs args)
            {
                releasedCount++;
                eventSender = sender;
            };

            // Act
            crate.ReturnToPool();

            // Assert
            Assert.AreEqual(1, releasedCount);
            Assert.AreSame(crate, eventSender);
        }

        [Test]
        public void SkillCrate_OnRelease_KillsActiveTransformTweens()
        {
            // Arrange
            SkillCrate crate = CreateAwakenedSkillCrate();
            Assert.DoesNotThrow(delegate { crate.OnRelease(); });

            crate.transform.DOMove(new Vector3(4f, 0f, 4f), 1f);
            Assert.AreEqual(1, GetActiveTweenCount(crate.transform));

            // Act
            crate.OnRelease();

            // Assert
            Assert.AreEqual(0, GetActiveTweenCount(crate.transform));
        }

        [TestCase(PowerUpType.Nuke, 0, TestName = "PowerUpCollectible_AwakeAndType_Nuke_MatchesEnumOrdinal")]
        [TestCase(PowerUpType.FreezeTimer, 1, TestName = "PowerUpCollectible_AwakeAndType_FreezeTimer_MatchesEnumOrdinal")]
        [TestCase(PowerUpType.BubbleShield, 2, TestName = "PowerUpCollectible_AwakeAndType_BubbleShield_MatchesEnumOrdinal")]
        [TestCase(PowerUpType.Armor, 3, TestName = "PowerUpCollectible_AwakeAndType_Armor_MatchesEnumOrdinal")]
        [TestCase(PowerUpType.FireTails, 4, TestName = "PowerUpCollectible_AwakeAndType_FireTails_MatchesEnumOrdinal")]
        public void PowerUpCollectible_Awake_AssignsGameObjectAndExposesConfiguredType(
            PowerUpType powerUpType,
            int expectedOrdinal)
        {
            // Arrange
            GameObject go = CreateInactiveGameObject("TestPowerUp");
            PowerUpCollectible collectible = go.AddComponent<PowerUpCollectible>();
            SetPrivateField(typeof(PowerUpCollectible), collectible, "_type", powerUpType);
            Assert.IsNull(collectible.GameObject);

            // Act
            InvokeLifecycleMethod(collectible, "Awake");

            // Assert
            Assert.AreSame(go, collectible.GameObject);
            Assert.AreEqual(powerUpType, collectible.Type);
            Assert.AreEqual(expectedOrdinal, (int)collectible.Type);
            Assert.IsInstanceOf<IPowerUpCollectible>(collectible);
            Assert.IsInstanceOf<ICollectible>(collectible);
            Assert.IsInstanceOf<IPoolable>(collectible);
        }

        [Test]
        public void PowerUpCollectible_OnTriggerEnter_PlayerLayer_RaisesOnCollectedBeforeRelease()
        {
            // Arrange
            PowerUpCollectible collectible = CreateAwakenedPowerUp(PowerUpType.Nuke);
            Collider playerCollider = CreateColliderOnLayer("PlayerCollider", GetLayerIndexFromMask(EntityLayers.Player.value));
            List<string> eventOrder = new();
            collectible.OnCollected += delegate { eventOrder.Add("OnCollected"); };
            collectible.OnCanBeReleased += delegate { eventOrder.Add("OnCanBeReleased"); };

            // Act
            InvokeTriggerEnter(collectible, playerCollider);

            // Assert
            CollectionAssert.AreEqual(new[] { "OnCollected", "OnCanBeReleased" }, eventOrder);
        }

        [TestCase(NonPlayerLayerKind.Default, TestName = "PowerUpCollectible_OnTriggerEnter_DefaultLayer_IgnoresTrigger")]
        [TestCase(NonPlayerLayerKind.Enemy, TestName = "PowerUpCollectible_OnTriggerEnter_EnemyLayer_IgnoresTrigger")]
        [TestCase(NonPlayerLayerKind.Boss, TestName = "PowerUpCollectible_OnTriggerEnter_BossLayer_IgnoresTrigger")]
        public void PowerUpCollectible_OnTriggerEnter_NonPlayerLayer_IgnoresTrigger(NonPlayerLayerKind layerKind)
        {
            // Arrange
            PowerUpCollectible collectible = CreateAwakenedPowerUp(PowerUpType.Armor);
            Collider otherCollider = CreateColliderOnLayer("NonPlayerCollider", ResolveNonPlayerLayer(layerKind));
            int collectedCount = 0;
            int releasedCount = 0;
            collectible.OnCollected += delegate { collectedCount++; };
            collectible.OnCanBeReleased += delegate { releasedCount++; };

            // Act
            InvokeTriggerEnter(collectible, otherCollider);

            // Assert
            Assert.AreEqual(0, collectedCount);
            Assert.AreEqual(0, releasedCount);
        }

        [Test]
        public void PowerUpCollectible_OnTriggerEnter_DuplicatePlayerTriggersInSingleCheckout_CollectsOnlyOnce()
        {
            // Arrange
            PowerUpCollectible collectible = CreateAwakenedPowerUp(PowerUpType.BubbleShield);
            collectible.OnGet();
            Collider playerCollider = CreateColliderOnLayer("PlayerCollider", GetLayerIndexFromMask(EntityLayers.Player.value));
            int collectedCount = 0;
            int releasedCount = 0;
            collectible.OnCollected += delegate { collectedCount++; };
            collectible.OnCanBeReleased += delegate { releasedCount++; };

            // Act — Two Player-layer triggers hit in the same checkout before deactivation
            InvokeTriggerEnter(collectible, playerCollider);
            InvokeTriggerEnter(collectible, playerCollider);

            // Assert
            Assert.AreEqual(1, collectedCount);
            Assert.AreEqual(1, releasedCount);
        }

        [Test]
        public void PowerUpCollectible_OnRelease_KillsActiveTransformTweens()
        {
            // Arrange
            PowerUpCollectible collectible = CreateAwakenedPowerUp(PowerUpType.FireTails);
            Assert.DoesNotThrow(delegate { collectible.OnRelease(); });

            collectible.transform.DOScale(Vector3.one * 2f, 0.5f);
            Assert.AreEqual(1, GetActiveTweenCount(collectible.transform));

            // Act
            collectible.OnRelease();

            // Assert
            Assert.AreEqual(0, GetActiveTweenCount(collectible.transform));
        }

        [Test]
        public void DropAnimationConfiguration_DefaultInstance_ExposesExpectedDefaults()
        {
            // Arrange
            DropAnimationConfiguration config = ScriptableObject.CreateInstance<DropAnimationConfiguration>();
            _createdObjects.Add(config);

            // Assert
            Assert.AreEqual(2f, config.ScatterRadius, TOLERANCE);
            Assert.AreEqual(0.4f, config.ScaleDuration, TOLERANCE);
            Assert.AreEqual(0.5f, config.ScatterDuration, TOLERANCE);
            Assert.AreEqual(1.2f, config.MinJumpPower, TOLERANCE);
            Assert.AreEqual(1.8f, config.MaxJumpPower, TOLERANCE);
            Assert.AreEqual(0.9f, config.MinDurationMultiplier, TOLERANCE);
            Assert.AreEqual(1.1f, config.MaxDurationMultiplier, TOLERANCE);
        }

        private GameObject CreateInactiveGameObject(string name)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false);
            _createdObjects.Add(go);
            return go;
        }

        private SkillCrate CreateAwakenedSkillCrate()
        {
            GameObject go = CreateInactiveGameObject("SkillCrate");
            SkillCrate crate = go.AddComponent<SkillCrate>();
            InvokeLifecycleMethod(crate, "Awake");
            return crate;
        }

        private PowerUpCollectible CreateAwakenedPowerUp(PowerUpType powerUpType)
        {
            GameObject go = CreateInactiveGameObject("PowerUp_" + powerUpType);
            PowerUpCollectible collectible = go.AddComponent<PowerUpCollectible>();
            SetPrivateField(typeof(PowerUpCollectible), collectible, "_type", powerUpType);
            InvokeLifecycleMethod(collectible, "Awake");
            return collectible;
        }

        private Collider CreateColliderOnLayer(string name, int layer)
        {
            GameObject go = new GameObject(name);
            go.layer = layer < 0 ? 0 : layer;
            _createdObjects.Add(go);
            return go.AddComponent<BoxCollider>();
        }

        private static int ResolveNonPlayerLayer(NonPlayerLayerKind layerKind)
        {
            switch (layerKind)
            {
                case NonPlayerLayerKind.Enemy:
                    return GetLayerIndexFromMask(EntityLayers.Enemy.value);
                case NonPlayerLayerKind.Boss:
                    return GetLayerIndexFromMask(EntityLayers.Boss.value);
                default:
                    return 0;
            }
        }

        private static int GetLayerIndexFromMask(int mask)
        {
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int GetActiveTweenCount(Transform target)
        {
            List<Tween> tweens = DOTween.TweensByTarget(target);
            if (tweens == null)
            {
                return 0;
            }

            int active = 0;
            for (int i = 0; i < tweens.Count; i++)
            {
                if (tweens[i] != null && tweens[i].IsActive())
                {
                    active++;
                }
            }

            return active;
        }

        private static void InvokeTriggerEnter(MonoBehaviour behaviour, Collider other)
        {
            MethodInfo method = behaviour.GetType().GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
            Action<Collider> callback = (Action<Collider>)Delegate.CreateDelegate(typeof(Action<Collider>), behaviour, method);
            callback(other);
        }

        private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
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
        }
    }
}
