using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.UI.HUD;
using DG.Tweening;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Assets.Scripts.Editor.Tests
{
    public class SwarmNotificationPresenterTests
    {
        private const float TOLERANCE = 0.0001f;
        private static readonly Vector3 DEFAULT_TEXT_SCALE = new Vector3(1.25f, 1.25f, 1.25f);

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();
        private readonly List<Volume> _createdVolumes = new();
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

            for (int i = 0; i < _createdVolumes.Count; i++)
            {
                Volume volume = _createdVolumes[i];
                if (volume != null && volume.HasInstantiatedProfile())
                {
                    UnityEngine.Object.DestroyImmediate(volume.profile);
                }
            }

            _createdVolumes.Clear();

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
        public void Awake_CapturesDefaultScaleAndHidesVisualAndText()
        {
            // Arrange
            PresenterHarness harness = CreateHarness(
                targetGamma: 0.7f,
                withVolume: false,
                withLiftGammaGain: false,
                initialGamma: Vector4.zero,
                runStart: false);

            // Assert
            Assert.IsFalse(harness.Visual.activeSelf);
            Assert.IsFalse(harness.SwarmText.gameObject.activeSelf);

            // Verify captured scale is applied on ShowOngoing without running ShowIncoming first
            harness.Presenter.ShowOngoing();
            AssertVector3Equal(DEFAULT_TEXT_SCALE, harness.SwarmText.rectTransform.localScale);
        }

        [TestCase(0.0f, -1.0f, TestName = "Start_TargetGamma_0_0_MapsToMinus1_0Offset")]
        [TestCase(0.7f, -0.3f, TestName = "Start_TargetGamma_0_7_MapsToMinus0_3Offset")]
        [TestCase(1.0f, 0f, TestName = "Start_TargetGamma_1_0_MapsToZeroOffset")]
        [TestCase(1.5f, 0.5f, TestName = "Start_TargetGamma_1_5_MapsToPlus0_5Offset")]
        [TestCase(2.0f, 1.0f, TestName = "Start_TargetGamma_2_0_MapsToPlus1_0Offset")]
        [TestCase(-0.7f, -0.7f, TestName = "Start_LegacyNegativeOffset_PreservesNegativeOffset")]
        [TestCase(2.5f, -2.5f, TestName = "Start_LegacyPositiveOffsetAboveTwo_NegatesValue")]
        public void Start_TargetGammaModes_MapsToExpectedLiftGammaOffset(float targetGamma, float expectedOffset)
        {
            // Arrange
            Vector4 initialGamma = new Vector4(0.1f, 0.2f, 0.3f, 0.15f);
            PresenterHarness harness = CreateHarness(
                targetGamma: targetGamma,
                withVolume: true,
                withLiftGammaGain: true,
                initialGamma: initialGamma,
                runStart: true);

            // Act
            harness.Presenter.ShowIncoming(10);
            DOTween.CompleteAll(withCallbacks: false);

            // Assert
            Assert.IsTrue(harness.RuntimeLiftGammaGain.gamma.overrideState);
            Vector4 actual = harness.RuntimeLiftGammaGain.gamma.value;
            Assert.AreEqual(0.1f, actual.x, TOLERANCE);
            Assert.AreEqual(0.2f, actual.y, TOLERANCE);
            Assert.AreEqual(0.3f, actual.z, TOLERANCE);
            Assert.AreEqual(expectedOffset, actual.w, TOLERANCE);
        }

        [TestCase(false, false, true, TestName = "Notifications_NullVolume_WorksWithoutGamma")]
        [TestCase(true, false, true, TestName = "Notifications_VolumeWithoutLiftGammaGain_WorksWithoutGamma")]
        [TestCase(true, false, false, TestName = "Notifications_VolumeWithNullProfile_WorksWithoutGamma")]
        public void Notifications_WithoutLiftGammaGain_StillFormatShowAndHideText(bool withVolume, bool withLiftGammaGain, bool withProfile)
        {
            // Arrange
            PresenterHarness harness = CreateHarness(
                targetGamma: 0.7f,
                withVolume: withVolume,
                withLiftGammaGain: withLiftGammaGain,
                initialGamma: Vector4.zero,
                runStart: true,
                withProfile: withProfile);

            // Act & Assert — Incoming
            harness.Presenter.ShowIncoming(8);
            Assert.IsTrue(harness.Visual.activeSelf);
            Assert.IsTrue(harness.SwarmText.gameObject.activeSelf);
            Assert.AreEqual("Swarm Incoming in 8s", harness.SwarmText.text);

            // Act & Assert — Ongoing
            harness.Presenter.ShowOngoing();
            Assert.AreEqual("Swarm ongoing", harness.SwarmText.text);
            AssertVector3Equal(DEFAULT_TEXT_SCALE, harness.SwarmText.rectTransform.localScale);

            // Act & Assert — Hide
            harness.Presenter.Hide();
            CompleteTweensOnTarget(harness.SwarmText.rectTransform);
            Assert.IsFalse(harness.Visual.activeSelf);
            Assert.IsFalse(harness.SwarmText.gameObject.activeSelf);
        }

        [Test]
        public void ShowIncoming_FromHidden_ActivatesFormatsStartsGrowFromZeroAndCompletesToDefaultScale()
        {
            // Arrange
            PresenterHarness harness = CreateHarness(
                targetGamma: 0.7f,
                withVolume: true,
                withLiftGammaGain: true,
                initialGamma: new Vector4(0f, 0f, 0f, 0f),
                runStart: true);

            // Act
            harness.Presenter.ShowIncoming(10);

            // Assert — Initial activation before tween completion
            Assert.IsTrue(harness.Visual.activeSelf);
            Assert.IsTrue(harness.SwarmText.gameObject.activeSelf);
            Assert.AreEqual("Swarm Incoming in 10s", harness.SwarmText.text);
            AssertVector3Equal(Vector3.zero, harness.SwarmText.rectTransform.localScale);
            Assert.AreEqual(1, GetActiveTweenCount(harness.SwarmText.rectTransform));

            // Act — Complete initial grow tween
            Tween growTween = GetFirstActiveTween(harness.SwarmText.rectTransform);
            growTween.Complete(withCallbacks: true);

            // Assert — Scaled to default and chained into punch tween
            AssertVector3Equal(DEFAULT_TEXT_SCALE, harness.SwarmText.rectTransform.localScale);
            Assert.AreEqual(1, GetActiveTweenCount(harness.SwarmText.rectTransform));
            Assert.AreNotSame(growTween, GetFirstActiveTween(harness.SwarmText.rectTransform));
        }

        [Test]
        public void ShowIncoming_RepeatedCalls_PreservesInFlightGrowThenReplacesPunchAfterGrow()
        {
            // Arrange
            PresenterHarness harness = CreateHarness(
                targetGamma: 0.7f,
                withVolume: false,
                withLiftGammaGain: false,
                initialGamma: Vector4.zero,
                runStart: true);

            harness.Presenter.ShowIncoming(10);
            Tween initialGrowTween = GetFirstActiveTween(harness.SwarmText.rectTransform);

            // Act — Update countdown while grow tween is still running
            harness.Presenter.ShowIncoming(9);

            // Assert — Text updates without killing or replacing active grow tween
            Assert.AreEqual("Swarm Incoming in 9s", harness.SwarmText.text);
            Assert.IsTrue(initialGrowTween.IsActive());
            Assert.AreSame(initialGrowTween, GetFirstActiveTween(harness.SwarmText.rectTransform));

            // Act — Complete grow tween (starts first punch), then emit another countdown tick
            initialGrowTween.Complete(withCallbacks: true);
            Tween firstPunchTween = GetFirstActiveTween(harness.SwarmText.rectTransform);
            harness.Presenter.ShowIncoming(8);

            // Assert — New punch tween replaces previous punch tween
            Assert.AreEqual("Swarm Incoming in 8s", harness.SwarmText.text);
            Assert.IsFalse(firstPunchTween.IsActive());
            Assert.AreEqual(1, GetActiveTweenCount(harness.SwarmText.rectTransform));
            Assert.AreNotSame(firstPunchTween, GetFirstActiveTween(harness.SwarmText.rectTransform));
        }

        [Test]
        public void ShowOngoing_WhileGrowInFlight_KillsGrowRestoresDefaultScaleAndPunches()
        {
            // Arrange
            PresenterHarness harness = CreateHarness(
                targetGamma: 0.7f,
                withVolume: false,
                withLiftGammaGain: false,
                initialGamma: Vector4.zero,
                runStart: true);

            harness.Presenter.ShowIncoming(1);
            Tween growTween = GetFirstActiveTween(harness.SwarmText.rectTransform);
            AssertVector3Equal(Vector3.zero, harness.SwarmText.rectTransform.localScale);

            // Act
            harness.Presenter.ShowOngoing();

            // Assert
            Assert.AreEqual("Swarm ongoing", harness.SwarmText.text);
            Assert.IsTrue(harness.Visual.activeSelf);
            Assert.IsTrue(harness.SwarmText.gameObject.activeSelf);
            Assert.IsFalse(growTween.IsActive());
            AssertVector3Equal(DEFAULT_TEXT_SCALE, harness.SwarmText.rectTransform.localScale);
            Assert.AreEqual(1, GetActiveTweenCount(harness.SwarmText.rectTransform));
        }

        [Test]
        public void Hide_RestoresOriginalGammaAndDeactivatesVisualAndTextOnlyAfterExitCompletes()
        {
            // Arrange
            Vector4 originalGamma = new Vector4(0.2f, 0.4f, 0.6f, 0.25f);
            PresenterHarness harness = CreateHarness(
                targetGamma: 0.7f,
                withVolume: true,
                withLiftGammaGain: true,
                initialGamma: originalGamma,
                runStart: true);

            harness.Presenter.ShowIncoming(3);
            DOTween.CompleteAll(withCallbacks: true);
            Assert.AreEqual(-0.3f, harness.RuntimeLiftGammaGain.gamma.value.w, TOLERANCE);

            // Act — Hide begins exit tweens; objects remain active until exit tween completes
            harness.Presenter.Hide();
            Assert.IsTrue(harness.Visual.activeSelf);
            Assert.IsTrue(harness.SwarmText.gameObject.activeSelf);

            DOTween.CompleteAll(withCallbacks: true);

            // Assert
            Assert.IsFalse(harness.SwarmText.gameObject.activeSelf);
            Assert.IsFalse(harness.Visual.activeSelf);
            Assert.AreEqual(0.25f, harness.RuntimeLiftGammaGain.gamma.value.w, TOLERANCE);
        }

        [Test]
        public void OnDisable_KillsTextAndGammaTweensAndRestoresOriginalGamma()
        {
            // Arrange
            Vector4 originalGamma = new Vector4(0.1f, -0.2f, 0.3f, 0.4f);
            PresenterHarness harness = CreateHarness(
                targetGamma: 0.7f,
                withVolume: true,
                withLiftGammaGain: true,
                initialGamma: originalGamma,
                runStart: true);

            harness.Presenter.ShowIncoming(5);
            harness.Presenter.ShowOngoing();
            harness.RuntimeLiftGammaGain.gamma.value = new Vector4(0.1f, -0.2f, 0.3f, -0.25f);

            // Act
            InvokeLifecycleMethod(harness.Presenter, "OnDisable");

            // Assert
            Assert.AreEqual(0, GetActiveTweenCount(harness.SwarmText.rectTransform));
            Assert.AreEqual(0, GetTotalPlayingTweensCount());
            Vector4 restored = harness.RuntimeLiftGammaGain.gamma.value;
            Assert.AreEqual(originalGamma.x, restored.x, TOLERANCE);
            Assert.AreEqual(originalGamma.y, restored.y, TOLERANCE);
            Assert.AreEqual(originalGamma.z, restored.z, TOLERANCE);
            Assert.AreEqual(originalGamma.w, restored.w, TOLERANCE);
        }

        private PresenterHarness CreateHarness(
            float targetGamma,
            bool withVolume,
            bool withLiftGammaGain,
            Vector4 initialGamma,
            bool runStart,
            bool withProfile = true)
        {
            GameObject root = new GameObject("TestSwarmNotificationPresenter", typeof(RectTransform));
            root.SetActive(false);
            _createdObjects.Add(root);

            GameObject visual = new GameObject("Visual", typeof(RectTransform));
            visual.transform.SetParent(root.transform, false);
            visual.SetActive(true);

            GameObject textGo = new GameObject("SwarmText", typeof(RectTransform));
            textGo.transform.SetParent(visual.transform, false);
            textGo.SetActive(true);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.localScale = DEFAULT_TEXT_SCALE;
            TextMeshProUGUI tmpText = textGo.AddComponent<TextMeshProUGUI>();

            Volume volume = null;
            if (withVolume)
            {
                GameObject volumeGo = new GameObject("TestVolume");
                _createdObjects.Add(volumeGo);
                volume = volumeGo.AddComponent<Volume>();
                _createdVolumes.Add(volume);

                if (withProfile)
                {
                    VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    _createdObjects.Add(profile);
                    if (withLiftGammaGain)
                    {
                        LiftGammaGain authoredLiftGamma = profile.Add<LiftGammaGain>(true);
                        authoredLiftGamma.gamma.overrideState = true;
                        authoredLiftGamma.gamma.value = initialGamma;
                    }

                    volume.sharedProfile = profile;
                }
            }

            ContainerBuilder builder = new ContainerBuilder().SetName("SwarmNotificationPresenterTestContainer");
            if (volume != null)
            {
                builder.AddSingleton(volume);
            }
            else
            {
                builder.AddSingleton<Volume>(delegate { return null; });
            }

            Container container = builder.Build();
            _createdContainers.Add(container);

            SwarmNotificationPresenter presenter = root.AddComponent<SwarmNotificationPresenter>();
            SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_visual", visual);
            SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_swarmText", tmpText);
            SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_targetGamma", targetGamma);
            SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_growDuration", 0.4f);
            SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_punchScale", 0.15f);
            SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_punchDuration", 0.6f);
            SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_exitDuration", 0.3f);
            SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_gammaEnterDuration", 1f);
            SetPrivateField(typeof(SwarmNotificationPresenter), presenter, "_gammaExitDuration", 1f);

            GameObjectInjector.InjectRecursive(root, container);
            root.SetActive(true);
            InvokeLifecycleMethod(presenter, "Awake");
            if (runStart)
            {
                InvokeLifecycleMethod(presenter, "Start");
            }

            LiftGammaGain runtimeLiftGammaGain = null;
            if (withVolume && withProfile && withLiftGammaGain)
            {
                volume.profile.TryGet(out runtimeLiftGammaGain);
            }

            return new PresenterHarness(presenter, visual, tmpText, runtimeLiftGammaGain);
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

        private static void CompleteTweensOnTarget(Transform target)
        {
            List<Tween> tweens = DOTween.TweensByTarget(target);
            if (tweens == null)
            {
                return;
            }

            for (int i = 0; i < tweens.Count; i++)
            {
                if (tweens[i] != null && tweens[i].IsActive())
                {
                    tweens[i].Complete(withCallbacks: true);
                }
            }
        }

        private static int GetTotalPlayingTweensCount()
        {
            List<Tween> playing = DOTween.PlayingTweens();
            return playing != null ? playing.Count : 0;
        }

        private static void AssertVector3Equal(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, TOLERANCE);
            Assert.AreEqual(expected.y, actual.y, TOLERANCE);
            Assert.AreEqual(expected.z, actual.z, TOLERANCE);
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
        }

        private readonly struct PresenterHarness
        {
            public readonly SwarmNotificationPresenter Presenter;
            public readonly GameObject Visual;
            public readonly TextMeshProUGUI SwarmText;
            public readonly LiftGammaGain RuntimeLiftGammaGain;

            public PresenterHarness(
                SwarmNotificationPresenter presenter,
                GameObject visual,
                TextMeshProUGUI swarmText,
                LiftGammaGain runtimeLiftGammaGain)
            {
                Presenter = presenter;
                Visual = visual;
                SwarmText = swarmText;
                RuntimeLiftGammaGain = runtimeLiftGammaGain;
            }
        }
    }
}
