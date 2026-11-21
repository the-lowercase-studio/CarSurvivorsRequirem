using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.PowerUps.Config;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Editor.Tests
{
    public class HealthBarAndArmorViewsTests
    {
        private const float DEFAULT_MAX_HEALTH = 100f;
        private const float TOLERANCE = 0.0001f;

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private bool _previousDotweenInitialized;

        public enum ArmorPlatesMisconfiguration
        {
            NullBaseRect,
            NullPlateArray,
            TwoPlatesOnly,
            NullElementInArray
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

        [TestCase(0f, 100f, 0f, 1f, 0f, TestName = "HealthBar_OnEnable_FullHealth_InitializesSliderAndGreenColor")]
        [TestCase(50f, 50f, 1f, 1f, 0f, TestName = "HealthBar_OnEnable_DamagedHealth_InitializesSliderAndYellowColor")]
        public void HealthBar_OnEnable_InitializesSliderAndColor(
            float initialDamage,
            float expectedSliderValue,
            float expectedR,
            float expectedG,
            float expectedB)
        {
            // Arrange
            Health health = CreateEnabledHealth(DEFAULT_MAX_HEALTH, initialDamage);
            HealthBarHarness bar = CreateHealthBar(health, shakeOnDecrease: false, enableBar: false);
            bar.FillImage.color = new Color(1f, 0f, 1f, 1f);

            // Act
            SetActiveAndEnable(bar.HealthBar);

            // Assert
            Assert.AreEqual(100f, bar.Slider.maxValue, TOLERANCE);
            Assert.AreEqual(expectedSliderValue, bar.Slider.value, TOLERANCE);
            AssertColorEqual(new Color(expectedR, expectedG, expectedB, 1f), bar.FillImage.color);
        }

        [Test]
        public void HealthBar_HealthChanges_UpdateSliderValueAndGradientColor()
        {
            // Arrange
            Health health = CreateEnabledHealth(DEFAULT_MAX_HEALTH, 0f);
            HealthBarHarness bar = CreateHealthBar(health, shakeOnDecrease: false, enableBar: true);

            // Act & Assert — 50% HP (yellow)
            health.DecreaseHealth(50f);
            Assert.AreEqual(50f, bar.Slider.value, TOLERANCE);
            AssertColorEqual(new Color(1f, 1f, 0f, 1f), bar.FillImage.color);

            // Act & Assert — 75% HP (midpoint between yellow and green)
            health.IncreaseHealth(25f);
            Assert.AreEqual(75f, bar.Slider.value, TOLERANCE);
            AssertColorEqual(new Color(0.5f, 1f, 0f, 1f), bar.FillImage.color);

            // Act & Assert — 0% HP (red)
            health.DecreaseHealth(75f);
            Assert.AreEqual(0f, bar.Slider.value, TOLERANCE);
            AssertColorEqual(new Color(1f, 0f, 0f, 1f), bar.FillImage.color);
        }

        [Test]
        public void HealthBar_OnDisable_UnsubscribesAndClearsOverride()
        {
            // Arrange
            Health health = CreateEnabledHealth(DEFAULT_MAX_HEALTH, 0f);
            HealthBarHarness bar = CreateHealthBar(health, shakeOnDecrease: false, enableBar: true);
            bar.HealthBar.SetColorOverride(Color.blue, 1f);
            AssertColorEqual(Color.blue, bar.FillImage.color);

            // Act
            SetInactiveAndDisable(bar.HealthBar);
            health.DecreaseHealth(40f);

            // Assert
            Assert.AreEqual(100f, bar.Slider.value, TOLERANCE);
            AssertColorEqual(new Color(0f, 1f, 0f, 1f), bar.FillImage.color);
        }

        [Test]
        public void HealthBar_SetColorOverride_ClampsBlendAndRestoresGradientOnClear()
        {
            // Arrange
            Health health = CreateEnabledHealth(DEFAULT_MAX_HEALTH, 0f);
            HealthBarHarness bar = CreateHealthBar(health, shakeOnDecrease: false, enableBar: true);

            // Act & Assert — Negative blend clamps to 0 (pure green)
            bar.HealthBar.SetColorOverride(Color.blue, -0.5f);
            AssertColorEqual(new Color(0f, 1f, 0f, 1f), bar.FillImage.color);

            // Act & Assert — 0.5 blend yields Color.Lerp(green, blue, 0.5)
            bar.HealthBar.SetColorOverride(Color.blue, 0.5f);
            AssertColorEqual(new Color(0f, 0.5f, 0.5f, 1f), bar.FillImage.color);

            // Act & Assert — Over-1 blend clamps to 1 (pure blue)
            bar.HealthBar.SetColorOverride(Color.blue, 1.5f);
            AssertColorEqual(Color.blue, bar.FillImage.color);

            // Act & Assert — Clear restores gradient (pure green)
            bar.HealthBar.ClearColorOverride();
            AssertColorEqual(new Color(0f, 1f, 0f, 1f), bar.FillImage.color);
        }

        [Test]
        public void HealthBar_ShakeOnHealthDecrease_CreatesAndReplacesTweenOnlyWhenEnabled()
        {
            // Arrange
            Health noShakeHealth = CreateEnabledHealth(DEFAULT_MAX_HEALTH, 0f);
            HealthBarHarness noShakeBar = CreateHealthBar(noShakeHealth, shakeOnDecrease: false, enableBar: true);
            Health shakeHealth = CreateEnabledHealth(DEFAULT_MAX_HEALTH, 0f);
            HealthBarHarness shakeBar = CreateHealthBar(shakeHealth, shakeOnDecrease: true, enableBar: true);

            // Act & Assert — Disabled shake creates no tween
            noShakeHealth.DecreaseHealth(20f);
            Assert.AreEqual(0, GetActiveTweenCount(noShakeBar.HealthBar.transform));

            // Act & Assert — Enabled shake creates tween on decrease and replaces on subsequent decrease
            shakeHealth.DecreaseHealth(20f);
            Assert.AreEqual(1, GetActiveTweenCount(shakeBar.HealthBar.transform));
            Tween firstTween = GetFirstActiveTween(shakeBar.HealthBar.transform);

            shakeHealth.DecreaseHealth(20f);
            Assert.IsFalse(firstTween.IsActive());
            Assert.AreEqual(1, GetActiveTweenCount(shakeBar.HealthBar.transform));
            Assert.AreNotSame(firstTween, GetFirstActiveTween(shakeBar.HealthBar.transform));

            // Act & Assert — IncreaseHealth does not spawn a shake tween
            DOTween.Kill(shakeBar.HealthBar.transform);
            shakeHealth.IncreaseHealth(10f);
            Assert.AreEqual(0, GetActiveTweenCount(shakeBar.HealthBar.transform));
        }

        [Test]
        public void HealthBar_OnDisableAndDestroy_KillActiveShakeTween()
        {
            // Arrange
            Health health = CreateEnabledHealth(DEFAULT_MAX_HEALTH, 0f);
            HealthBarHarness bar = CreateHealthBar(health, shakeOnDecrease: true, enableBar: true);

            // Act & Assert — OnDisable kills active shake tween
            health.DecreaseHealth(20f);
            Assert.AreEqual(1, GetActiveTweenCount(bar.HealthBar.transform));
            SetInactiveAndDisable(bar.HealthBar);
            Assert.AreEqual(0, GetActiveTweenCount(bar.HealthBar.transform));

            // Act & Assert — OnDestroy kills active shake tween
            SetActiveAndEnable(bar.HealthBar);
            health.DecreaseHealth(20f);
            Assert.AreEqual(1, GetActiveTweenCount(bar.HealthBar.transform));
            InvokeLifecycleMethod(bar.HealthBar, "OnDestroy");
            Assert.AreEqual(0, GetActiveTweenCount(bar.HealthBar.transform));
        }

        [TestCase(ArmorPlatesMisconfiguration.NullBaseRect, TestName = "ArmorPlatesView_Awake_NullBaseRect_ThrowsInvalidOperation")]
        [TestCase(ArmorPlatesMisconfiguration.NullPlateArray, TestName = "ArmorPlatesView_Awake_NullPlateArray_ThrowsInvalidOperation")]
        [TestCase(ArmorPlatesMisconfiguration.TwoPlatesOnly, TestName = "ArmorPlatesView_Awake_TwoPlateArray_ThrowsInvalidOperation")]
        [TestCase(ArmorPlatesMisconfiguration.NullElementInArray, TestName = "ArmorPlatesView_Awake_NullPlateElement_ThrowsInvalidOperation")]
        public void ArmorPlatesView_Awake_ThrowsInvalidOperationOnMissingReferences(ArmorPlatesMisconfiguration misconfiguration)
        {
            // Arrange
            ArmorPlatesView view = CreateMisconfiguredArmorPlatesView(misconfiguration);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => InvokeLifecycleMethod(view, "Awake"));
        }

        [Test]
        public void ArmorPlatesView_Initialize_SetsSiblingOrderProportionalWidthsAndRightToLeftPositions()
        {
            // Arrange — 1.5f bar centered at (0,0) with pivot (0.5,0.5) => visual right edge = -0.75f
            // Plates [20, 40, 60] with overlap 0.2f:
            //   Plate 1 width = (20/100)*1.5 = 0.30, X = -0.75 + 0.30*0.2 = -0.69, nextEdge = -0.99
            //   Plate 2 width = (40/100)*1.5 = 0.60, X = -0.99 + 0.60*0.2 = -0.87, nextEdge = -1.47
            //   Plate 3 width = (60/100)*1.5 = 0.90, X = -1.47 + 0.90*0.2 = -1.29
            ArmorPlatesHarness harness = CreateArmorPlatesHarness(reverseInitialPlateOrder: true);
            ArmorConfigSO config = CreateDefaultArmorConfig();

            // Act
            harness.View.Initialize(config);

            // Assert — Sibling order
            Assert.AreEqual(0, harness.BaseBarRect.GetSiblingIndex());
            Assert.AreEqual(1, harness.Plates[0].RectTransform.GetSiblingIndex());
            Assert.AreEqual(2, harness.Plates[1].RectTransform.GetSiblingIndex());
            Assert.AreEqual(3, harness.Plates[2].RectTransform.GetSiblingIndex());

            // Assert — Layout positions, sizes, colors, fill, and hidden initial state
            AssertPlateLayoutAndReset(harness.Plates[0], -0.69f, 0f, 0.30f, 0.20f, config.PlateColor);
            AssertPlateLayoutAndReset(harness.Plates[1], -0.87f, 0f, 0.60f, 0.20f, config.PlateColor);
            AssertPlateLayoutAndReset(harness.Plates[2], -1.29f, 0f, 0.90f, 0.20f, config.PlateColor);
        }

        [Test]
        public void ArmorPlatesView_UpdatePlates_SetsVisibilityAndClampedFill()
        {
            // Arrange
            ArmorPlatesHarness harness = CreateArmorPlatesHarness(reverseInitialPlateOrder: false);
            ArmorConfigSO config = CreateDefaultArmorConfig();
            harness.View.Initialize(config);

            // Act — Level 2 active: Plate 1 = 20/20 (1.0), Plate 2 = 20/40 (0.5), Plate 3 hidden
            harness.View.UpdatePlates(2, new[] { 20f, 20f, 60f }, config);

            // Assert
            Assert.IsTrue(harness.Plates[0].GameObject.activeSelf);
            Assert.AreEqual(1f, harness.Plates[0].FillImage.fillAmount, TOLERANCE);
            Assert.IsTrue(harness.Plates[1].GameObject.activeSelf);
            Assert.AreEqual(0.5f, harness.Plates[1].FillImage.fillAmount, TOLERANCE);
            Assert.IsFalse(harness.Plates[2].GameObject.activeSelf);

            // Act — Out-of-bounds HP values clamp into [0, 1]
            harness.View.UpdatePlates(2, new[] { 35f, -10f, 0f }, config);

            // Assert
            Assert.AreEqual(1f, harness.Plates[0].FillImage.fillAmount, TOLERANCE);
            Assert.AreEqual(0f, harness.Plates[1].FillImage.fillAmount, TOLERANCE);
        }

        [Test]
        public void ArmorPlatesView_PlayDamageShake_KillsTweenOnDisableAndDestroy()
        {
            // Arrange
            ArmorPlatesHarness harness = CreateArmorPlatesHarness(reverseInitialPlateOrder: false);

            // Act & Assert — OnDisable kills tween
            harness.View.PlayDamageShake();
            Assert.AreEqual(1, GetActiveTweenCount(harness.View.transform));
            InvokeLifecycleMethod(harness.View, "OnDisable");
            Assert.AreEqual(0, GetActiveTweenCount(harness.View.transform));

            // Act & Assert — OnDestroy kills tween
            harness.View.PlayDamageShake();
            Assert.AreEqual(1, GetActiveTweenCount(harness.View.transform));
            InvokeLifecycleMethod(harness.View, "OnDestroy");
            Assert.AreEqual(0, GetActiveTweenCount(harness.View.transform));
        }

        [Test]
        public void ArmorPlateItemView_SetupLayout_ConfiguresRectAndRightOrigin()
        {
            // Arrange
            PlateItemHarness plate = CreatePlateItemHarness("TestPlate", null);
            plate.FillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

            // Act
            plate.ItemView.SetupLayout(new Vector2(-0.69f, 0.1f), new Vector2(0.30f, 0.20f));

            // Assert
            Assert.AreEqual(new Vector2(0.5f, 0.5f), plate.RectTransform.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), plate.RectTransform.anchorMax);
            Assert.AreEqual(new Vector2(1f, 0.5f), plate.RectTransform.pivot);
            Assert.AreEqual(-0.69f, plate.RectTransform.anchoredPosition.x, TOLERANCE);
            Assert.AreEqual(0.1f, plate.RectTransform.anchoredPosition.y, TOLERANCE);
            Assert.AreEqual(0.30f, plate.RectTransform.sizeDelta.x, TOLERANCE);
            Assert.AreEqual(0.20f, plate.RectTransform.sizeDelta.y, TOLERANCE);
            Assert.AreEqual((int)Image.OriginHorizontal.Right, plate.FillImage.fillOrigin);
        }

        [Test]
        public void ArmorPlateItemView_SetFillAmountColorAndVisibility_UpdatesViewState()
        {
            // Arrange
            PlateItemHarness plate = CreatePlateItemHarness("TestPlate", null);
            Color expectedColor = new Color(0.05f, 0.15f, 0.6f, 1f);

            // Act & Assert — Color
            plate.ItemView.SetColor(expectedColor);
            AssertColorEqual(expectedColor, plate.FillImage.color);

            // Act & Assert — Fill clamping
            plate.ItemView.SetFillAmount(-0.2f);
            Assert.AreEqual(0f, plate.FillImage.fillAmount, TOLERANCE);
            plate.ItemView.SetFillAmount(0.35f);
            Assert.AreEqual(0.35f, plate.FillImage.fillAmount, TOLERANCE);
            plate.ItemView.SetFillAmount(1.4f);
            Assert.AreEqual(1f, plate.FillImage.fillAmount, TOLERANCE);

            // Act & Assert — Visibility
            plate.ItemView.SetVisible(false);
            Assert.IsFalse(plate.GameObject.activeSelf);
            plate.ItemView.SetVisible(true);
            Assert.IsTrue(plate.GameObject.activeSelf);
        }

        private Health CreateEnabledHealth(float maxHealth, float initialDamage)
        {
            GameObject go = new GameObject("TestHealth");
            go.SetActive(false);
            _createdObjects.Add(go);

            Health health = go.AddComponent<Health>();
            health.MaxHealth = maxHealth;
            SetActiveAndEnable(health);

            if (initialDamage > 0f)
            {
                health.DecreaseHealth(initialDamage);
            }

            return health;
        }

        private HealthBarHarness CreateHealthBar(Health health, bool shakeOnDecrease, bool enableBar)
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
            SetPrivateField(typeof(HealthBar), healthBar, "_shakeOnHealthDecrease", shakeOnDecrease);

            InvokeLifecycleMethod(healthBar, "Awake");
            if (enableBar)
            {
                SetActiveAndEnable(healthBar);
            }

            return new HealthBarHarness(healthBar, barGo.GetComponent<Slider>(), fillImage);
        }

        private ArmorPlatesView CreateMisconfiguredArmorPlatesView(ArmorPlatesMisconfiguration misconfiguration)
        {
            GameObject canvasGo = new GameObject("TestArmorPlatesCanvas", typeof(RectTransform));
            _createdObjects.Add(canvasGo);

            GameObject baseBarGo = new GameObject("HealthBarSlider", typeof(RectTransform));
            baseBarGo.transform.SetParent(canvasGo.transform, false);
            RectTransform baseRect = baseBarGo.GetComponent<RectTransform>();

            ArmorPlatesView view = canvasGo.AddComponent<ArmorPlatesView>();
            PlateItemHarness plate1 = CreatePlateItemHarness("ArmorPlate_1", canvasGo.transform);
            PlateItemHarness plate2 = CreatePlateItemHarness("ArmorPlate_2", canvasGo.transform);
            PlateItemHarness plate3 = CreatePlateItemHarness("ArmorPlate_3", canvasGo.transform);

            if (misconfiguration == ArmorPlatesMisconfiguration.NullBaseRect)
            {
                SetPrivateField(typeof(ArmorPlatesView), view, "_baseHealthBarRect", null);
                SetPrivateField(typeof(ArmorPlatesView), view, "_plateViews", new[] { plate1.ItemView, plate2.ItemView, plate3.ItemView });
            }
            else if (misconfiguration == ArmorPlatesMisconfiguration.NullPlateArray)
            {
                SetPrivateField(typeof(ArmorPlatesView), view, "_baseHealthBarRect", baseRect);
                SetPrivateField(typeof(ArmorPlatesView), view, "_plateViews", null);
            }
            else if (misconfiguration == ArmorPlatesMisconfiguration.TwoPlatesOnly)
            {
                SetPrivateField(typeof(ArmorPlatesView), view, "_baseHealthBarRect", baseRect);
                SetPrivateField(typeof(ArmorPlatesView), view, "_plateViews", new[] { plate1.ItemView, plate2.ItemView });
            }
            else
            {
                SetPrivateField(typeof(ArmorPlatesView), view, "_baseHealthBarRect", baseRect);
                SetPrivateField(typeof(ArmorPlatesView), view, "_plateViews", new[] { plate1.ItemView, null, plate3.ItemView });
            }

            return view;
        }

        private ArmorPlatesHarness CreateArmorPlatesHarness(bool reverseInitialPlateOrder)
        {
            GameObject canvasGo = new GameObject("HealthBarCanvas", typeof(RectTransform));
            _createdObjects.Add(canvasGo);

            GameObject baseBarGo = new GameObject("HealthBarSlider", typeof(RectTransform));
            baseBarGo.transform.SetParent(canvasGo.transform, false);
            RectTransform baseRect = baseBarGo.GetComponent<RectTransform>();
            baseRect.anchorMin = new Vector2(0.5f, 0.5f);
            baseRect.anchorMax = new Vector2(0.5f, 0.5f);
            baseRect.pivot = new Vector2(0.5f, 0.5f);
            baseRect.anchoredPosition = Vector2.zero;
            baseRect.sizeDelta = new Vector2(1.5f, 0.20f);

            PlateItemHarness plate1;
            PlateItemHarness plate2;
            PlateItemHarness plate3;

            if (reverseInitialPlateOrder)
            {
                plate3 = CreatePlateItemHarness("ArmorPlate_3", canvasGo.transform);
                plate2 = CreatePlateItemHarness("ArmorPlate_2", canvasGo.transform);
                plate1 = CreatePlateItemHarness("ArmorPlate_1", canvasGo.transform);
            }
            else
            {
                plate1 = CreatePlateItemHarness("ArmorPlate_1", canvasGo.transform);
                plate2 = CreatePlateItemHarness("ArmorPlate_2", canvasGo.transform);
                plate3 = CreatePlateItemHarness("ArmorPlate_3", canvasGo.transform);
            }

            ArmorPlatesView view = canvasGo.AddComponent<ArmorPlatesView>();
            SetPrivateField(typeof(ArmorPlatesView), view, "_baseHealthBarRect", baseRect);
            SetPrivateField(typeof(ArmorPlatesView), view, "_plateViews", new[] { plate1.ItemView, plate2.ItemView, plate3.ItemView });
            InvokeLifecycleMethod(view, "Awake");

            return new ArmorPlatesHarness(view, baseRect, new[] { plate1, plate2, plate3 });
        }

        private PlateItemHarness CreatePlateItemHarness(string name, Transform parent)
        {
            GameObject plateGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            if (parent != null)
            {
                plateGo.transform.SetParent(parent, false);
            }
            else
            {
                _createdObjects.Add(plateGo);
            }

            Image bgImage = plateGo.GetComponent<Image>();

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(plateGo.transform, false);
            Image fillImage = fillGo.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;

            ArmorPlateItemView itemView = plateGo.AddComponent<ArmorPlateItemView>();
            SetPrivateField(typeof(ArmorPlateItemView), itemView, "_backgroundImage", bgImage);
            SetPrivateField(typeof(ArmorPlateItemView), itemView, "_fillImage", fillImage);
            InvokeLifecycleMethod(itemView, "Awake");

            return new PlateItemHarness(plateGo, plateGo.GetComponent<RectTransform>(), itemView, fillImage);
        }

        private ArmorConfigSO CreateDefaultArmorConfig()
        {
            ArmorConfigSO config = ScriptableObject.CreateInstance<ArmorConfigSO>();
            _createdObjects.Add(config);
            return config;
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

        private static Tween GetFirstActiveTween(Transform target)
        {
            List<Tween> tweens = DOTween.TweensByTarget(target);
            if (tweens != null)
            {
                for (int i = 0; i < tweens.Count; i++)
                {
                    if (tweens[i] != null && tweens[i].IsActive())
                    {
                        return tweens[i];
                    }
                }
            }

            Assert.Fail("Expected an active DOTween instance on target transform.");
            return null;
        }

        private static void AssertPlateLayoutAndReset(
            PlateItemHarness plate,
            float expectedX,
            float expectedY,
            float expectedWidth,
            float expectedHeight,
            Color expectedColor)
        {
            Assert.AreEqual(expectedX, plate.RectTransform.anchoredPosition.x, TOLERANCE);
            Assert.AreEqual(expectedY, plate.RectTransform.anchoredPosition.y, TOLERANCE);
            Assert.AreEqual(expectedWidth, plate.RectTransform.sizeDelta.x, TOLERANCE);
            Assert.AreEqual(expectedHeight, plate.RectTransform.sizeDelta.y, TOLERANCE);
            Assert.AreEqual(new Vector2(1f, 0.5f), plate.RectTransform.pivot);
            AssertColorEqual(expectedColor, plate.FillImage.color);
            Assert.AreEqual(1f, plate.FillImage.fillAmount, TOLERANCE);
            Assert.IsFalse(plate.GameObject.activeSelf);
        }

        private static void AssertColorEqual(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, TOLERANCE);
            Assert.AreEqual(expected.g, actual.g, TOLERANCE);
            Assert.AreEqual(expected.b, actual.b, TOLERANCE);
            Assert.AreEqual(expected.a, actual.a, TOLERANCE);
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

        private readonly struct HealthBarHarness
        {
            public readonly HealthBar HealthBar;
            public readonly Slider Slider;
            public readonly Image FillImage;

            public HealthBarHarness(HealthBar healthBar, Slider slider, Image fillImage)
            {
                HealthBar = healthBar;
                Slider = slider;
                FillImage = fillImage;
            }
        }

        private readonly struct ArmorPlatesHarness
        {
            public readonly ArmorPlatesView View;
            public readonly RectTransform BaseBarRect;
            public readonly PlateItemHarness[] Plates;

            public ArmorPlatesHarness(ArmorPlatesView view, RectTransform baseBarRect, PlateItemHarness[] plates)
            {
                View = view;
                BaseBarRect = baseBarRect;
                Plates = plates;
            }
        }

        private readonly struct PlateItemHarness
        {
            public readonly GameObject GameObject;
            public readonly RectTransform RectTransform;
            public readonly ArmorPlateItemView ItemView;
            public readonly Image FillImage;

            public PlateItemHarness(GameObject gameObject, RectTransform rectTransform, ArmorPlateItemView itemView, Image fillImage)
            {
                GameObject = gameObject;
                RectTransform = rectTransform;
                ItemView = itemView;
                FillImage = fillImage;
            }
        }
    }
}
