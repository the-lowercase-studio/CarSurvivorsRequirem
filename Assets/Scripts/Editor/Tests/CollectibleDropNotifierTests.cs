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
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class CollectibleDropNotifierTests
    {
        private const float TOLERANCE = 0.0001f;
        private const int DEFAULT_RANDOM_SEED = 112233;

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();
        private UnityEngine.Random.State _previousRandomState;
        private bool _previousDotweenInitialized;

        [SetUp]
        public void SetUp()
        {
            _previousRandomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(DEFAULT_RANDOM_SEED);

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
            UnityEngine.Random.state = _previousRandomState;
        }

        [Test]
        public void DropSuppression_InitialState_IsNotSuppressed()
        {
            // Arrange
            NotifierHarness harness = CreateHarness();

            // Assert
            Assert.IsFalse(harness.Suppressor.AreDropsSuppressed);
        }

        [Test]
        public void DropSuppression_NestedBeginAndEnd_TracksDepthReferenceCount()
        {
            // Arrange
            NotifierHarness harness = CreateHarness();

            // Act & Assert
            harness.Suppressor.BeginDropSuppression();
            Assert.IsTrue(harness.Suppressor.AreDropsSuppressed);

            harness.Suppressor.BeginDropSuppression();
            Assert.IsTrue(harness.Suppressor.AreDropsSuppressed);

            harness.Suppressor.EndDropSuppression();
            Assert.IsTrue(harness.Suppressor.AreDropsSuppressed);

            harness.Suppressor.EndDropSuppression();
            Assert.IsFalse(harness.Suppressor.AreDropsSuppressed);
        }

        [Test]
        public void DropSuppression_EndAtZeroDepth_ClampsAtZeroSoNextBeginSuppressesImmediately()
        {
            // Arrange
            NotifierHarness harness = CreateHarness();

            // Act
            harness.Suppressor.EndDropSuppression();
            harness.Suppressor.EndDropSuppression();
            Assert.IsFalse(harness.Suppressor.AreDropsSuppressed);

            harness.Suppressor.BeginDropSuppression();

            // Assert
            Assert.IsTrue(harness.Suppressor.AreDropsSuppressed);
        }

        [Test]
        public void SpawnCollectible_ValidPrefab_ActivatesUnderParentAtSpawnPositionWithZeroScaleAndActiveTweens()
        {
            // Arrange
            NotifierHarness harness = CreateHarness();
            GameObject cratePrefab = CreateSkillCratePrefabTemplate("SkillCrateTemplate");
            Vector3 spawnPos = new Vector3(4f, 0.85f, -3f);
            Vector3 targetPos = new Vector3(6f, 0.85f, -3f);

            // Act
            harness.Notifier.SpawnCollectible(cratePrefab, spawnPos, targetPos);

            // Assert
            Assert.AreEqual(1, harness.ItemsParent.childCount);
            GameObject spawned = harness.ItemsParent.GetChild(0).gameObject;
            Assert.IsTrue(spawned.activeSelf);
            AssertVector3Equal(spawnPos, spawned.transform.position);
            AssertVector3Equal(Vector3.zero, spawned.transform.localScale);
            Assert.Greater(GetActiveTweenCount(spawned.transform), 0);
        }

        [Test]
        public void SpawnCollectible_SkillCrateCollected_RaisesSkillUpgradeEventOnly()
        {
            // Arrange
            NotifierHarness harness = CreateHarness();
            GameObject cratePrefab = CreateSkillCratePrefabTemplate("SkillCrateTemplate");
            Collider playerCollider = CreatePlayerCollider();

            int skillEventCount = 0;
            object skillEventSender = null;
            List<PowerUpType> powerUpEvents = new();
            harness.Notifier.OnSkillUpgradeCollectibleCollected += delegate(object sender, EventArgs args)
            {
                skillEventCount++;
                skillEventSender = sender;
            };
            harness.Notifier.OnPowerUpCollected += delegate(PowerUpType type) { powerUpEvents.Add(type); };

            harness.Notifier.SpawnCollectible(cratePrefab, Vector3.zero, new Vector3(2f, 0f, 0f));
            SkillCrate spawnedCrate = harness.ItemsParent.GetChild(0).GetComponent<SkillCrate>();

            // Act
            InvokeTriggerEnter(spawnedCrate, playerCollider);

            // Assert
            Assert.AreEqual(1, skillEventCount);
            Assert.AreSame(spawnedCrate, skillEventSender);
            Assert.AreEqual(0, powerUpEvents.Count);
        }

        [TestCase(PowerUpType.Nuke, TestName = "SpawnCollectible_PowerUpCollected_Nuke_RaisesPowerUpEventWithExactType")]
        [TestCase(PowerUpType.FreezeTimer, TestName = "SpawnCollectible_PowerUpCollected_FreezeTimer_RaisesPowerUpEventWithExactType")]
        [TestCase(PowerUpType.BubbleShield, TestName = "SpawnCollectible_PowerUpCollected_BubbleShield_RaisesPowerUpEventWithExactType")]
        [TestCase(PowerUpType.Armor, TestName = "SpawnCollectible_PowerUpCollected_Armor_RaisesPowerUpEventWithExactType")]
        [TestCase(PowerUpType.FireTails, TestName = "SpawnCollectible_PowerUpCollected_FireTails_RaisesPowerUpEventWithExactType")]
        public void SpawnCollectible_PowerUpCollected_RaisesPowerUpEventWithExpectedTypeOnly(PowerUpType expectedType)
        {
            // Arrange
            NotifierHarness harness = CreateHarness();
            GameObject powerUpPrefab = CreatePowerUpPrefabTemplate("PowerUpTemplate_" + expectedType, expectedType);
            Collider playerCollider = CreatePlayerCollider();

            int skillEventCount = 0;
            List<PowerUpType> powerUpEvents = new();
            harness.Notifier.OnSkillUpgradeCollectibleCollected += delegate { skillEventCount++; };
            harness.Notifier.OnPowerUpCollected += delegate(PowerUpType type) { powerUpEvents.Add(type); };

            harness.Notifier.SpawnCollectible(powerUpPrefab, Vector3.zero, new Vector3(1f, 0f, 1f));
            PowerUpCollectible spawnedPowerUp = harness.ItemsParent.GetChild(0).GetComponent<PowerUpCollectible>();

            // Act
            InvokeTriggerEnter(spawnedPowerUp, playerCollider);

            // Assert
            Assert.AreEqual(0, skillEventCount);
            CollectionAssert.AreEqual(new[] { expectedType }, powerUpEvents);
        }

        [Test]
        public void SpawnCollectible_GenericCollectibleWithoutTagInterface_RaisesNeitherEventAndStillReleases()
        {
            // Arrange
            NotifierHarness harness = CreateHarness();
            GameObject genericPrefab = CreateGenericCollectiblePrefabTemplate("GenericCollectibleTemplate");

            int skillEventCount = 0;
            int powerUpEventCount = 0;
            harness.Notifier.OnSkillUpgradeCollectibleCollected += delegate { skillEventCount++; };
            harness.Notifier.OnPowerUpCollected += delegate { powerUpEventCount++; };

            harness.Notifier.SpawnCollectible(genericPrefab, Vector3.zero, Vector3.right);
            FakeGenericCollectible spawned = harness.ItemsParent.GetChild(0).GetComponent<FakeGenericCollectible>();

            // Act
            spawned.TriggerCollectAndRelease();

            // Assert
            Assert.AreEqual(0, skillEventCount);
            Assert.AreEqual(0, powerUpEventCount);
            Assert.IsFalse(spawned.gameObject.activeSelf);
        }

        [Test]
        public void PoolReleaseAndReuse_KillsTweensCallsLifecycleHooksAndAvoidsDuplicateEventHandlers()
        {
            // Arrange
            NotifierHarness harness = CreateHarness();
            GameObject trackedPrefab = CreateTrackedCollectiblePrefabTemplate("TrackedCollectibleTemplate");

            int skillEventCount = 0;
            harness.Notifier.OnSkillUpgradeCollectibleCollected += delegate { skillEventCount++; };

            // Act 1 — Initial spawn and collection release
            harness.Notifier.SpawnCollectible(trackedPrefab, Vector3.zero, new Vector3(2f, 0f, 0f));
            FakeTrackedSkillCollectible firstInstance = harness.ItemsParent.GetChild(0).GetComponent<FakeTrackedSkillCollectible>();
            Assert.AreEqual(1, firstInstance.OnGetCount);
            Assert.AreEqual(0, firstInstance.OnReleaseCount);
            Assert.Greater(GetActiveTweenCount(firstInstance.transform), 0);

            firstInstance.CollectAndRelease();

            // Assert 1 — Released cleanly: tweens killed, OnRelease invoked, deactivated
            Assert.AreEqual(1, skillEventCount);
            Assert.AreEqual(1, firstInstance.OnReleaseCount);
            Assert.AreEqual(0, GetActiveTweenCount(firstInstance.transform));
            Assert.IsFalse(firstInstance.gameObject.activeSelf);

            // Act 2 — Re-spawn from pool and collect a second time
            harness.Notifier.SpawnCollectible(trackedPrefab, Vector3.one, new Vector3(3f, 1f, 1f));
            Assert.AreEqual(1, harness.ItemsParent.childCount);
            FakeTrackedSkillCollectible reusedInstance = harness.ItemsParent.GetChild(0).GetComponent<FakeTrackedSkillCollectible>();
            Assert.AreSame(firstInstance, reusedInstance);
            Assert.IsTrue(reusedInstance.gameObject.activeSelf);
            Assert.AreEqual(2, reusedInstance.OnGetCount);

            reusedInstance.CollectAndRelease();

            // Assert 2 — Collection fires once for the second checkout (total 2, not 3)
            Assert.AreEqual(2, skillEventCount);
            Assert.AreEqual(2, reusedInstance.OnReleaseCount);
            Assert.IsFalse(reusedInstance.gameObject.activeSelf);
        }

        [Test]
        public void SpawnCollectible_MultiplePrefabs_MaintainsSeparatePoolsAndRoutesEventsIndependently()
        {
            // Arrange
            NotifierHarness harness = CreateHarness();
            GameObject cratePrefab = CreateSkillCratePrefabTemplate("CrateTemplate");
            GameObject nukePrefab = CreatePowerUpPrefabTemplate("NukeTemplate", PowerUpType.Nuke);
            Collider playerCollider = CreatePlayerCollider();

            int skillEventCount = 0;
            List<PowerUpType> powerUpEvents = new();
            harness.Notifier.OnSkillUpgradeCollectibleCollected += delegate { skillEventCount++; };
            harness.Notifier.OnPowerUpCollected += delegate(PowerUpType type) { powerUpEvents.Add(type); };

            // Act 1 — Spawn one of each prefab and release both
            harness.Notifier.SpawnCollectible(cratePrefab, Vector3.zero, Vector3.right);
            harness.Notifier.SpawnCollectible(nukePrefab, Vector3.zero, Vector3.left);

            SkillCrate firstCrate = harness.ItemsParent.GetChild(0).GetComponent<SkillCrate>();
            PowerUpCollectible firstNuke = harness.ItemsParent.GetChild(1).GetComponent<PowerUpCollectible>();

            InvokeTriggerEnter(firstCrate, playerCollider);
            InvokeTriggerEnter(firstNuke, playerCollider);

            // Act 2 — Re-spawn nuke only, verify crate remains pooled/inactive, then collect nuke again
            harness.Notifier.SpawnCollectible(nukePrefab, Vector3.zero, Vector3.forward);
            Assert.AreEqual(2, harness.ItemsParent.childCount);
            Assert.IsTrue(firstNuke.gameObject.activeSelf);
            Assert.IsFalse(firstCrate.gameObject.activeSelf);

            InvokeTriggerEnter(firstNuke, playerCollider);
            harness.Notifier.SpawnCollectible(cratePrefab, Vector3.zero, Vector3.back);

            // Assert
            Assert.AreEqual(2, harness.ItemsParent.childCount);
            Assert.IsTrue(firstCrate.gameObject.activeSelf);
            Assert.IsFalse(firstNuke.gameObject.activeSelf);
            Assert.AreEqual(1, skillEventCount);
            CollectionAssert.AreEqual(new[] { PowerUpType.Nuke, PowerUpType.Nuke }, powerUpEvents);
        }

        [Test]
        public void SpawnCollectible_ReentrantPlayerTriggerDuringOnCollected_DoesNotThrowDuplicateRelease()
        {
            // Arrange
            NotifierHarness harness = CreateHarness();
            GameObject cratePrefab = CreateSkillCratePrefabTemplate("ReentrantCrateTemplate");
            Collider playerCollider = CreatePlayerCollider();

            harness.Notifier.SpawnCollectible(cratePrefab, Vector3.zero, Vector3.right);
            SkillCrate spawnedCrate = harness.ItemsParent.GetChild(0).GetComponent<SkillCrate>();
            ReentrantTriggerRelay relay = new ReentrantTriggerRelay(spawnedCrate, playerCollider);
            harness.Notifier.OnSkillUpgradeCollectibleCollected += relay.HandleSkillUpgradeCollected;

            // Act & Assert
            Assert.DoesNotThrow(delegate { InvokeTriggerEnter(spawnedCrate, playerCollider); });
            Assert.AreEqual(1, relay.SkillEventCount);
            Assert.IsFalse(spawnedCrate.gameObject.activeSelf);
        }

        [Test]
        public void SpawnCollectible_NullPrefab_ThrowsArgumentNullException()
        {
            // Arrange
            NotifierHarness harness = CreateHarness();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(delegate
            {
                harness.Notifier.SpawnCollectible(null, Vector3.zero, Vector3.right);
            });
        }

        private NotifierHarness CreateHarness()
        {
            DropAnimationConfiguration config = ScriptableObject.CreateInstance<DropAnimationConfiguration>();
            _createdObjects.Add(config);

            GameObject itemsParentGo = new GameObject("CollectibleItemsParent");
            _createdObjects.Add(itemsParentGo);

            GameObject notifierGo = new GameObject("TestCollectibleDropNotifier");
            notifierGo.SetActive(false);
            _createdObjects.Add(notifierGo);

            CollectibleDropNotifier notifier = notifierGo.AddComponent<CollectibleDropNotifier>();
            SetPrivateField(typeof(CollectibleDropNotifier), notifier, "_collectibleItemsParent", itemsParentGo.transform);

            Container container = BuildContainer(delegate(ContainerBuilder builder)
            {
                builder.AddSingleton(config);
            });

            GameObjectInjector.InjectObject(notifierGo, container);

            return new NotifierHarness(notifier, notifier, itemsParentGo.transform);
        }

        private GameObject CreateSkillCratePrefabTemplate(string name)
        {
            GameObject prefab = new GameObject(name);
            prefab.SetActive(false);
            _createdObjects.Add(prefab);
            prefab.AddComponent<SkillCrate>();
            return prefab;
        }

        private GameObject CreatePowerUpPrefabTemplate(string name, PowerUpType powerUpType)
        {
            GameObject prefab = new GameObject(name);
            prefab.SetActive(false);
            _createdObjects.Add(prefab);
            PowerUpCollectible collectible = prefab.AddComponent<PowerUpCollectible>();
            SetPrivateField(typeof(PowerUpCollectible), collectible, "_type", powerUpType);
            return prefab;
        }

        private GameObject CreateGenericCollectiblePrefabTemplate(string name)
        {
            GameObject prefab = new GameObject(name);
            prefab.SetActive(false);
            _createdObjects.Add(prefab);
            prefab.AddComponent<FakeGenericCollectible>();
            return prefab;
        }

        private GameObject CreateTrackedCollectiblePrefabTemplate(string name)
        {
            GameObject prefab = new GameObject(name);
            prefab.SetActive(false);
            _createdObjects.Add(prefab);
            prefab.AddComponent<FakeTrackedSkillCollectible>();
            return prefab;
        }

        private Collider CreatePlayerCollider()
        {
            GameObject playerGo = new GameObject("PlayerCollider");
            int layer = GetLayerIndexFromMask(EntityLayers.Player.value);
            playerGo.layer = layer < 0 ? 0 : layer;
            _createdObjects.Add(playerGo);
            return playerGo.AddComponent<BoxCollider>();
        }

        private Container BuildContainer(Action<ContainerBuilder> configure)
        {
            ContainerBuilder builder = new ContainerBuilder().SetName("CollectibleDropNotifierTestContainer");
            configure?.Invoke(builder);
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
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

        private static void AssertVector3Equal(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, TOLERANCE);
            Assert.AreEqual(expected.y, actual.y, TOLERANCE);
            Assert.AreEqual(expected.z, actual.z, TOLERANCE);
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

        private readonly struct NotifierHarness
        {
            public readonly CollectibleDropNotifier Notifier;
            public readonly ICollectibleDropSuppressor Suppressor;
            public readonly Transform ItemsParent;

            public NotifierHarness(
                CollectibleDropNotifier notifier,
                ICollectibleDropSuppressor suppressor,
                Transform itemsParent)
            {
                Notifier = notifier;
                Suppressor = suppressor;
                ItemsParent = itemsParent;
            }
        }

        private sealed class FakeGenericCollectible : MonoBehaviour, ICollectible, IPoolable
        {
            public GameObject GameObject { get { return gameObject; } }
            public event EventHandler OnCollected;
            public event EventHandler OnCanBeReleased;

            public void OnGet()
            {
            }

            public void OnRelease()
            {
            }

            public void ReturnToPool()
            {
                OnCanBeReleased?.Invoke(this, EventArgs.Empty);
            }

            public void TriggerCollectAndRelease()
            {
                OnCollected?.Invoke(this, EventArgs.Empty);
                ReturnToPool();
            }
        }

        private sealed class FakeTrackedSkillCollectible : MonoBehaviour, ISkillUpgradeCollectible, IPoolable
        {
            public int OnGetCount { get; private set; }
            public int OnReleaseCount { get; private set; }
            public GameObject GameObject { get { return gameObject; } }

            public event EventHandler OnCollected;
            public event EventHandler OnCanBeReleased;

            public void OnGet()
            {
                OnGetCount++;
            }

            public void OnRelease()
            {
                OnReleaseCount++;
                transform.DOKill();
            }

            public void ReturnToPool()
            {
                OnCanBeReleased?.Invoke(this, EventArgs.Empty);
            }

            public void CollectAndRelease()
            {
                OnCollected?.Invoke(this, EventArgs.Empty);
                ReturnToPool();
            }
        }

        private sealed class ReentrantTriggerRelay
        {
            private readonly SkillCrate _crate;
            private readonly Collider _playerCollider;
            private bool _hasReentered;

            public int SkillEventCount { get; private set; }

            public ReentrantTriggerRelay(SkillCrate crate, Collider playerCollider)
            {
                _crate = crate;
                _playerCollider = playerCollider;
            }

            public void HandleSkillUpgradeCollected(object sender, EventArgs args)
            {
                SkillEventCount++;
                if (!_hasReentered)
                {
                    _hasReentered = true;
                    InvokeTriggerEnter(_crate, _playerCollider);
                }
            }
        }
    }
}
