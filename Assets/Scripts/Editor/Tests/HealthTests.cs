using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.HealthSystem;
using NUnit.Framework;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class HealthTests
    {
        private const float DEFAULT_MAX_HEALTH = 100f;
        private const float TOLERANCE = 0.0001f;

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
        public void OnEnable_InitializesCurrentHealthToMaxAndAlive()
        {
            // Arrange
            Health health = CreateHealth(DEFAULT_MAX_HEALTH, enable: false);

            // Act
            SetActiveAndEnable(health);

            // Assert
            Assert.AreEqual(100f, health.CurrentHealth, TOLERANCE);
            Assert.IsTrue(health.IsAlive());
        }

        [Test]
        public void OnDisableThenOnEnable_ResetsHealthAndDoesNotDuplicateHealthChangedEvents()
        {
            // Arrange
            Health health = CreateHealth(DEFAULT_MAX_HEALTH, enable: true);
            int changedCount = 0;
            health.OnHealthChanged += delegate { changedCount++; };
            health.DecreaseHealth(100f);
            Assert.AreEqual(1, changedCount);

            // Act — Cycle 1 pooling reset and single sub-lethal hit
            SetInactiveAndDisable(health);
            SetActiveAndEnable(health);
            Assert.AreEqual(100f, health.CurrentHealth, TOLERANCE);
            Assert.IsTrue(health.IsAlive());

            changedCount = 0;
            health.DecreaseHealth(25f);
            Assert.AreEqual(75f, health.CurrentHealth, TOLERANCE);
            Assert.AreEqual(1, changedCount);

            // Act — Cycle 2 pooling reset and single sub-lethal hit
            SetInactiveAndDisable(health);
            SetActiveAndEnable(health);
            changedCount = 0;
            health.DecreaseHealth(10f);

            // Assert
            Assert.AreEqual(90f, health.CurrentHealth, TOLERANCE);
            Assert.AreEqual(1, changedCount);
        }

        [TestCase(30f, 70f, TestName = "DecreaseHealth_SubLethal_30Of100_ReducesHealthAndRaisesDecreasedThenChanged")]
        [TestCase(99.9f, 0.1f, TestName = "DecreaseHealth_SubLethal_99_9Of100_ReducesHealthAndRaisesDecreasedThenChanged")]
        public void DecreaseHealth_SubLethal_ReducesHealthAndRaisesDecreasedThenChanged(float damage, float expectedHealth)
        {
            // Arrange
            Health health = CreateHealth(DEFAULT_MAX_HEALTH, enable: false);
            List<string> events = new();
            int decreasedCount = 0;
            int changedCount = 0;
            int noHealthCount = 0;

            health.OnHealthDecreased += delegate
            {
                decreasedCount++;
                events.Add("Decreased");
            };
            health.OnHealthChanged += delegate
            {
                changedCount++;
                events.Add("Changed");
            };
            health.OnNoHealth += delegate { noHealthCount++; };
            SetActiveAndEnable(health);

            // Act
            health.DecreaseHealth(damage);

            // Assert
            Assert.AreEqual(expectedHealth, health.CurrentHealth, TOLERANCE);
            Assert.IsTrue(health.IsAlive());
            Assert.AreEqual(1, decreasedCount);
            Assert.AreEqual(1, changedCount);
            Assert.AreEqual(0, noHealthCount);
            CollectionAssert.AreEqual(new[] { "Decreased", "Changed" }, events);
        }

        [TestCase(100f, TestName = "DecreaseHealth_ExactLethal_100Of100_ClampsToZeroAndRaisesNoHealth")]
        [TestCase(150f, TestName = "DecreaseHealth_Overkill_150Of100_ClampsToZeroAndRaisesNoHealth")]
        public void DecreaseHealth_ExactLethalAndOverkill_ClampsToZeroMarksDeadBeforeEventAndRaisesNoHealth(float damage)
        {
            // Arrange
            Health health = CreateHealth(DEFAULT_MAX_HEALTH, enable: true);
            int decreasedCount = 0;
            int changedCount = 0;
            int noHealthCount = 0;
            bool aliveInsideHandler = true;
            float healthInsideHandler = -1f;

            health.OnHealthDecreased += delegate { decreasedCount++; };
            health.OnHealthChanged += delegate { changedCount++; };
            health.OnNoHealth += delegate
            {
                noHealthCount++;
                aliveInsideHandler = health.IsAlive();
                healthInsideHandler = health.CurrentHealth;
            };

            // Act
            health.DecreaseHealth(damage);

            // Assert
            Assert.AreEqual(0f, health.CurrentHealth, TOLERANCE);
            Assert.IsFalse(health.IsAlive());
            Assert.IsFalse(aliveInsideHandler);
            Assert.AreEqual(0f, healthInsideHandler, TOLERANCE);
            Assert.AreEqual(1, noHealthCount);
            Assert.AreEqual(1, changedCount);
            Assert.AreEqual(0, decreasedCount);
        }

        [Test]
        public void DecreaseHealth_WhenAlreadyDead_DoesNotChangeHealthOrRaiseEvents()
        {
            // Arrange
            Health health = CreateHealth(DEFAULT_MAX_HEALTH, enable: true);
            health.DecreaseHealth(100f);

            int decreasedCount = 0;
            int changedCount = 0;
            int noHealthCount = 0;
            health.OnHealthDecreased += delegate { decreasedCount++; };
            health.OnHealthChanged += delegate { changedCount++; };
            health.OnNoHealth += delegate { noHealthCount++; };

            // Act
            health.DecreaseHealth(10f);
            health.DecreaseHealth(25f);

            // Assert
            Assert.AreEqual(0f, health.CurrentHealth, TOLERANCE);
            Assert.IsFalse(health.IsAlive());
            Assert.AreEqual(0, decreasedCount);
            Assert.AreEqual(0, changedCount);
            Assert.AreEqual(0, noHealthCount);
        }

        [Test]
        public void IncreaseHealth_WhenDead_DoesNotResurrectOrRaiseEvents()
        {
            // Arrange
            Health health = CreateHealth(DEFAULT_MAX_HEALTH, enable: true);
            health.DecreaseHealth(100f);

            int increasedCount = 0;
            int changedCount = 0;
            health.OnHealthIncreased += delegate { increasedCount++; };
            health.OnHealthChanged += delegate { changedCount++; };

            // Act
            health.IncreaseHealth(20f);

            // Assert
            Assert.AreEqual(0f, health.CurrentHealth, TOLERANCE);
            Assert.IsFalse(health.IsAlive());
            Assert.AreEqual(0, increasedCount);
            Assert.AreEqual(0, changedCount);
        }

        [Test]
        public void IncreaseHealth_BelowMax_AddsHealthAndRaisesIncreasedThenChanged()
        {
            // Arrange
            Health health = CreateHealth(DEFAULT_MAX_HEALTH, enable: false);
            List<string> events = new();
            int increasedCount = 0;
            int changedCount = 0;

            health.OnHealthIncreased += delegate
            {
                increasedCount++;
                events.Add("Increased");
            };
            health.OnHealthChanged += delegate
            {
                changedCount++;
                events.Add("Changed");
            };

            SetActiveAndEnable(health);
            health.DecreaseHealth(50f);
            changedCount = 0;
            events.Clear();

            // Act
            health.IncreaseHealth(20f);

            // Assert
            Assert.AreEqual(70f, health.CurrentHealth, TOLERANCE);
            Assert.IsTrue(health.IsAlive());
            Assert.AreEqual(1, increasedCount);
            Assert.AreEqual(1, changedCount);
            CollectionAssert.AreEqual(new[] { "Increased", "Changed" }, events);
        }

        [Test]
        public void IncreaseHealth_WhenAlreadyAtMax_DoesNotRaiseEvents()
        {
            // Arrange
            Health health = CreateHealth(DEFAULT_MAX_HEALTH, enable: true);
            int increasedCount = 0;
            int changedCount = 0;
            health.OnHealthIncreased += delegate { increasedCount++; };
            health.OnHealthChanged += delegate { changedCount++; };

            // Act
            health.IncreaseHealth(10f);

            // Assert
            Assert.AreEqual(100f, health.CurrentHealth, TOLERANCE);
            Assert.AreEqual(0, increasedCount);
            Assert.AreEqual(0, changedCount);
        }

        [TestCase(20f, 20f, TestName = "IncreaseHealth_ReachingMax_From80Plus20_ClampsAndNotifiesObservers")]
        [TestCase(20f, 40f, TestName = "IncreaseHealth_ExceedingMax_From80Plus40_ClampsAndNotifiesObservers")]
        public void IncreaseHealth_ReachingOrExceedingMaxFromDamaged_ClampsAndNotifiesObservers(float initialDamage, float healAmount)
        {
            // Arrange
            Health health = CreateHealth(DEFAULT_MAX_HEALTH, enable: true);
            health.DecreaseHealth(initialDamage);

            int increasedCount = 0;
            int changedCount = 0;
            health.OnHealthIncreased += delegate { increasedCount++; };
            health.OnHealthChanged += delegate { changedCount++; };

            // Act
            health.IncreaseHealth(healAmount);

            // Assert
            Assert.AreEqual(100f, health.CurrentHealth, TOLERANCE);
            Assert.IsTrue(health.IsAlive());
            Assert.AreEqual(1, increasedCount);
            Assert.AreEqual(1, changedCount);
        }

        [TestCase(0f, TestName = "DecreaseHealth_ZeroInput_IsIgnoredWithoutEvents")]
        [TestCase(-10f, TestName = "DecreaseHealth_NegativeInput_IsIgnoredWithoutEvents")]
        [TestCase(float.NaN, TestName = "DecreaseHealth_NaNInput_IsIgnoredWithoutEvents")]
        [TestCase(float.PositiveInfinity, TestName = "DecreaseHealth_PositiveInfinityInput_IsIgnoredWithoutEvents")]
        [TestCase(float.NegativeInfinity, TestName = "DecreaseHealth_NegativeInfinityInput_IsIgnoredWithoutEvents")]
        public void DecreaseHealth_InvalidOrNonPositiveInput_IsIgnoredWithoutEvents(float invalidDamage)
        {
            // Arrange
            Health health = CreateHealth(DEFAULT_MAX_HEALTH, enable: true);
            health.DecreaseHealth(50f);

            int decreasedCount = 0;
            int changedCount = 0;
            int noHealthCount = 0;
            health.OnHealthDecreased += delegate { decreasedCount++; };
            health.OnHealthChanged += delegate { changedCount++; };
            health.OnNoHealth += delegate { noHealthCount++; };

            // Act
            health.DecreaseHealth(invalidDamage);

            // Assert
            Assert.AreEqual(50f, health.CurrentHealth, TOLERANCE);
            Assert.IsTrue(health.IsAlive());
            Assert.AreEqual(0, decreasedCount);
            Assert.AreEqual(0, changedCount);
            Assert.AreEqual(0, noHealthCount);
        }

        [TestCase(0f, TestName = "IncreaseHealth_ZeroInput_IsIgnoredWithoutEvents")]
        [TestCase(-10f, TestName = "IncreaseHealth_NegativeInput_IsIgnoredWithoutEvents")]
        [TestCase(float.NaN, TestName = "IncreaseHealth_NaNInput_IsIgnoredWithoutEvents")]
        [TestCase(float.PositiveInfinity, TestName = "IncreaseHealth_PositiveInfinityInput_IsIgnoredWithoutEvents")]
        [TestCase(float.NegativeInfinity, TestName = "IncreaseHealth_NegativeInfinityInput_IsIgnoredWithoutEvents")]
        public void IncreaseHealth_InvalidOrNonPositiveInput_IsIgnoredWithoutEvents(float invalidHeal)
        {
            // Arrange
            Health health = CreateHealth(DEFAULT_MAX_HEALTH, enable: true);
            health.DecreaseHealth(50f);

            int increasedCount = 0;
            int changedCount = 0;
            health.OnHealthIncreased += delegate { increasedCount++; };
            health.OnHealthChanged += delegate { changedCount++; };

            // Act
            health.IncreaseHealth(invalidHeal);

            // Assert
            Assert.AreEqual(50f, health.CurrentHealth, TOLERANCE);
            Assert.IsTrue(health.IsAlive());
            Assert.AreEqual(0, increasedCount);
            Assert.AreEqual(0, changedCount);
        }

        private Health CreateHealth(float maxHealth, bool enable)
        {
            GameObject go = new GameObject("TestHealth");
            go.SetActive(false);
            _createdObjects.Add(go);

            Health health = go.AddComponent<Health>();
            health.MaxHealth = maxHealth;

            if (enable)
            {
                SetActiveAndEnable(health);
            }

            return health;
        }

        private static void SetActiveAndEnable(MonoBehaviour behaviour)
        {
            behaviour.gameObject.SetActive(true);
            InvokeLifecycleMethod(behaviour, "OnEnable");
        }

        private static void SetInactiveAndDisable(MonoBehaviour behaviour)
        {
            behaviour.gameObject.SetActive(false);
            InvokeLifecycleMethod(behaviour, "OnDisable");
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
