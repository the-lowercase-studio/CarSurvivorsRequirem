using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.HealthSystem;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Editor.Tests
{
    public class RegenativeHealthTests
    {
        private const float DEFAULT_MAX_HEALTH = 100f;
        private const float DEFAULT_REGEN_AMOUNT = 2f;
        private const float DEFAULT_REGEN_DELAY = 1f;
        private const float TOLERANCE = 0.0001f;

        private readonly List<UnityEngine.Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            DOTween.Clear(true);

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
        public void OnEnable_InitializesRegenerationAmountDelayAndBaseHealth()
        {
            // Arrange
            RegenativeHealth regen = CreateRegenativeHealth(DEFAULT_MAX_HEALTH, DEFAULT_REGEN_AMOUNT, DEFAULT_REGEN_DELAY, enable: false);

            // Act
            SetActiveAndEnable(regen);

            // Assert
            Assert.AreEqual(100f, regen.CurrentHealth, TOLERANCE);
            Assert.IsTrue(regen.IsAlive());
            Assert.AreEqual(2f, regen.CurrentRegenerationAmount, TOLERANCE);
            Assert.AreEqual(1f, regen.CurrentRegenerationDelay, TOLERANCE);
        }

        [Test]
        public void AdvanceRegeneration_CountsDownDelayThenHealsOnNextStepAndResetsDelay()
        {
            // Arrange
            RegenativeHealth regen = CreateRegenativeHealth(DEFAULT_MAX_HEALTH, DEFAULT_REGEN_AMOUNT, DEFAULT_REGEN_DELAY, enable: true);
            regen.DecreaseHealth(5f);

            // Act & Assert — Countdown partial step
            regen.AdvanceRegeneration(0.75f);
            Assert.AreEqual(0.25f, regen.CurrentRegenerationDelay, TOLERANCE);
            Assert.AreEqual(95f, regen.CurrentHealth, TOLERANCE);

            // Act & Assert — Delay hits exact zero without healing on the same step
            regen.AdvanceRegeneration(0.25f);
            Assert.AreEqual(0f, regen.CurrentRegenerationDelay, TOLERANCE);
            Assert.AreEqual(95f, regen.CurrentHealth, TOLERANCE);

            // Act & Assert — Next step triggers +2 heal and resets delay to 1s
            regen.AdvanceRegeneration(0.016f);
            Assert.AreEqual(97f, regen.CurrentHealth, TOLERANCE);
            Assert.AreEqual(1f, regen.CurrentRegenerationDelay, TOLERANCE);
        }

        [Test]
        public void AdvanceRegeneration_RepeatsUntilMaxHealthAndSyncsHealthBar()
        {
            // Arrange
            RegenativeHealth regen = CreateRegenativeHealth(DEFAULT_MAX_HEALTH, DEFAULT_REGEN_AMOUNT, DEFAULT_REGEN_DELAY, enable: true);
            Slider slider = CreateHealthBarSlider(regen);
            regen.DecreaseHealth(5f);
            Assert.AreEqual(95f, slider.value, TOLERANCE);

            // Act & Assert — Cycle 1: 95 -> 97
            AdvanceOneRegenerationCycle(regen, 1f, 0.016f);
            Assert.AreEqual(97f, regen.CurrentHealth, TOLERANCE);
            Assert.AreEqual(97f, slider.value, TOLERANCE);

            // Act & Assert — Cycle 2: 97 -> 99
            AdvanceOneRegenerationCycle(regen, 1f, 0.016f);
            Assert.AreEqual(99f, regen.CurrentHealth, TOLERANCE);
            Assert.AreEqual(99f, slider.value, TOLERANCE);

            // Act & Assert — Cycle 3: 99 -> 100 (clamps to MaxHealth and syncs HealthBar)
            AdvanceOneRegenerationCycle(regen, 1f, 0.016f);
            Assert.AreEqual(100f, regen.CurrentHealth, TOLERANCE);
            Assert.AreEqual(100f, slider.value, TOLERANCE);

            // Act & Assert — Further cycle stays capped at 100
            AdvanceOneRegenerationCycle(regen, 1f, 0.016f);
            Assert.AreEqual(100f, regen.CurrentHealth, TOLERANCE);
        }

        [Test]
        public void AdvanceRegeneration_WhenDead_DoesNotHeal()
        {
            // Arrange
            RegenativeHealth regen = CreateRegenativeHealth(DEFAULT_MAX_HEALTH, DEFAULT_REGEN_AMOUNT, DEFAULT_REGEN_DELAY, enable: true);
            regen.DecreaseHealth(100f);

            int increasedCount = 0;
            regen.OnHealthIncreased += delegate { increasedCount++; };

            // Act
            AdvanceOneRegenerationCycle(regen, 1f, 0.016f);

            // Assert
            Assert.AreEqual(0f, regen.CurrentHealth, TOLERANCE);
            Assert.IsFalse(regen.IsAlive());
            Assert.AreEqual(0, increasedCount);
        }

        [TestCase(0f, 1f, 1f, TestName = "AdvanceRegeneration_ZeroMaxRegenAmount_DoesNotAdvanceOrHeal")]
        [TestCase(2f, 0f, 0f, TestName = "AdvanceRegeneration_ZeroStartDelay_DoesNotAdvanceOrHeal")]
        public void AdvanceRegeneration_ZeroRegenConfig_DoesNotAdvanceOrHeal(float maxRegenAmount, float startDelay, float expectedDelay)
        {
            // Arrange
            RegenativeHealth regen = CreateRegenativeHealth(DEFAULT_MAX_HEALTH, maxRegenAmount, startDelay, enable: true);
            regen.DecreaseHealth(50f);

            int increasedCount = 0;
            regen.OnHealthIncreased += delegate { increasedCount++; };

            // Act
            regen.AdvanceRegeneration(1f);
            regen.AdvanceRegeneration(0.5f);

            // Assert
            Assert.AreEqual(50f, regen.CurrentHealth, TOLERANCE);
            Assert.AreEqual(expectedDelay, regen.CurrentRegenerationDelay, TOLERANCE);
            Assert.AreEqual(0, increasedCount);
        }

        [TestCase(0f, TestName = "AdvanceRegeneration_ZeroDeltaTime_IsIgnored")]
        [TestCase(-0.5f, TestName = "AdvanceRegeneration_NegativeDeltaTime_IsIgnored")]
        [TestCase(float.NaN, TestName = "AdvanceRegeneration_NaNDeltaTime_IsIgnored")]
        [TestCase(float.PositiveInfinity, TestName = "AdvanceRegeneration_PositiveInfinityDeltaTime_IsIgnored")]
        [TestCase(float.NegativeInfinity, TestName = "AdvanceRegeneration_NegativeInfinityDeltaTime_IsIgnored")]
        public void AdvanceRegeneration_NonPositiveOrNonFiniteDeltaTime_IsIgnored(float invalidDeltaTime)
        {
            // Arrange
            RegenativeHealth regen = CreateRegenativeHealth(DEFAULT_MAX_HEALTH, DEFAULT_REGEN_AMOUNT, DEFAULT_REGEN_DELAY, enable: true);
            regen.DecreaseHealth(50f);

            int increasedCount = 0;
            regen.OnHealthIncreased += delegate { increasedCount++; };

            // Act
            regen.AdvanceRegeneration(invalidDeltaTime);

            // Assert
            Assert.AreEqual(50f, regen.CurrentHealth, TOLERANCE);
            Assert.AreEqual(1f, regen.CurrentRegenerationDelay, TOLERANCE);
            Assert.AreEqual(0, increasedCount);
        }

        private RegenativeHealth CreateRegenativeHealth(float maxHealth, float regenAmount, float startDelay, bool enable)
        {
            GameObject go = new GameObject("TestRegenativeHealth");
            go.SetActive(false);
            _createdObjects.Add(go);

            RegenativeHealth regen = go.AddComponent<RegenativeHealth>();
            regen.MaxHealth = maxHealth;
            SetPrivateField(typeof(RegenativeHealth), regen, "<MaxRegenerationAmount>k__BackingField", regenAmount);
            SetPrivateField(typeof(RegenativeHealth), regen, "<StartRegenerationDelay>k__BackingField", startDelay);

            if (enable)
            {
                SetActiveAndEnable(regen);
            }

            return regen;
        }

        private Slider CreateHealthBarSlider(Health health)
        {
            GameObject barGo = new GameObject("TestHealthBar", typeof(RectTransform), typeof(Slider));
            barGo.SetActive(false);
            _createdObjects.Add(barGo);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barGo.transform, false);
            Image fillImage = fillGo.GetComponent<Image>();

            HealthBar healthBar = barGo.AddComponent<HealthBar>();
            SetPrivateField(typeof(HealthBar), healthBar, "_gradient", CreateLinearGradient());
            SetPrivateField(typeof(HealthBar), healthBar, "_health", health);
            SetPrivateField(typeof(HealthBar), healthBar, "_fillImage", fillImage);
            SetPrivateField(typeof(HealthBar), healthBar, "_shakeOnHealthDecrease", false);

            barGo.SetActive(true);
            InvokeLifecycleMethod(healthBar, "Awake");
            InvokeLifecycleMethod(healthBar, "OnEnable");

            return barGo.GetComponent<Slider>();
        }

        private static Gradient CreateLinearGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0f, 0f, 1f), 0f),
                    new GradientColorKey(new Color(1f, 1f, 0f, 1f), 0.5f),
                    new GradientColorKey(new Color(0f, 1f, 0f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static void AdvanceOneRegenerationCycle(RegenativeHealth regen, float delayStep, float tickStep)
        {
            regen.AdvanceRegeneration(delayStep);
            regen.AdvanceRegeneration(tickStep);
        }

        private static void SetActiveAndEnable(MonoBehaviour behaviour)
        {
            behaviour.gameObject.SetActive(true);
            InvokeLifecycleMethod(behaviour, "OnEnable");
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
