using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Audio;
using Assets.Scripts.Enemies;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.Player.Constants;
using Assets.Scripts.PowerUps;
using Assets.Scripts.PowerUps.Armor;
using Assets.Scripts.PowerUps.Config;
using Assets.Scripts.PowerUps.Shield;
using Assets.Scripts.Skills;
using Assets.Scripts.UI.Death;
using Assets.Scripts.VFX;
using DG.Tweening;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Editor.Tests
{
    public class ArmorPowerUpAndDamagePipelineTests
    {
        private const float DEFAULT_MAX_HEALTH = 100f;
        private const float TOLERANCE = 0.0001f;

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();

        public enum ArmorConfigInvalidKind
        {
            WrongCapacitiesLength,
            ZeroCapacity,
            NegativeCapacity,
            NaNCapacity,
            NegativeOverlap,
            OverlapAboveOne,
            NonPositiveRefWidth,
            NonPositiveRefMaxHealth
        }

        public enum ArmorServiceMisconfiguration
        {
            MissingConfig,
            MissingPlatesView,
            InvalidConfig
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            DOTween.Clear(true);

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
        public void ArmorConfig_ValidateConfiguration_AcceptsDefaults()
        {
            // Arrange
            ArmorConfigSO config = CreateTrackedScriptableObject<ArmorConfigSO>();

            // Act & Assert
            Assert.DoesNotThrow(() => config.ValidateConfiguration());
            Assert.AreEqual(3, config.MaxLevel);
        }

        [TestCase(ArmorConfigInvalidKind.WrongCapacitiesLength, TestName = "ArmorConfig_Validate_WrongCapacitiesLength_ThrowsInvalidOperation")]
        [TestCase(ArmorConfigInvalidKind.ZeroCapacity, TestName = "ArmorConfig_Validate_ZeroCapacity_ThrowsInvalidOperation")]
        [TestCase(ArmorConfigInvalidKind.NegativeCapacity, TestName = "ArmorConfig_Validate_NegativeCapacity_ThrowsInvalidOperation")]
        [TestCase(ArmorConfigInvalidKind.NaNCapacity, TestName = "ArmorConfig_Validate_NaNCapacity_ThrowsInvalidOperation")]
        [TestCase(ArmorConfigInvalidKind.NegativeOverlap, TestName = "ArmorConfig_Validate_NegativeOverlap_ThrowsInvalidOperation")]
        [TestCase(ArmorConfigInvalidKind.OverlapAboveOne, TestName = "ArmorConfig_Validate_OverlapAboveOne_ThrowsInvalidOperation")]
        [TestCase(ArmorConfigInvalidKind.NonPositiveRefWidth, TestName = "ArmorConfig_Validate_NonPositiveRefWidth_ThrowsInvalidOperation")]
        [TestCase(ArmorConfigInvalidKind.NonPositiveRefMaxHealth, TestName = "ArmorConfig_Validate_NonPositiveRefMaxHealth_ThrowsInvalidOperation")]
        public void ArmorConfig_ValidateConfiguration_RejectsInvalidValues(ArmorConfigInvalidKind invalidKind)
        {
            // Arrange
            ArmorConfigSO config = CreateInvalidArmorConfig(invalidKind);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => config.ValidateConfiguration());
        }

        [Test]
        public void ArmorConfig_GetPlateCapacity_ReturnsCapacityOrZeroOutOfRange()
        {
            // Arrange
            ArmorConfigSO config = CreateTrackedScriptableObject<ArmorConfigSO>();

            // Act & Assert
            Assert.AreEqual(0f, config.GetPlateCapacity(-1), TOLERANCE);
            Assert.AreEqual(20f, config.GetPlateCapacity(0), TOLERANCE);
            Assert.AreEqual(40f, config.GetPlateCapacity(1), TOLERANCE);
            Assert.AreEqual(60f, config.GetPlateCapacity(2), TOLERANCE);
            Assert.AreEqual(0f, config.GetPlateCapacity(3), TOLERANCE);
        }

        [TestCase(ArmorServiceMisconfiguration.MissingConfig, TestName = "ArmorService_Awake_MissingConfig_ThrowsInvalidOperation")]
        [TestCase(ArmorServiceMisconfiguration.MissingPlatesView, TestName = "ArmorService_Awake_MissingView_ThrowsInvalidOperation")]
        [TestCase(ArmorServiceMisconfiguration.InvalidConfig, TestName = "ArmorService_Awake_InvalidConfig_ThrowsInvalidOperation")]
        public void ArmorService_Awake_ThrowsWhenConfigOrViewMissing(ArmorServiceMisconfiguration misconfiguration)
        {
            // Arrange
            ArmorService service = CreateMisconfiguredArmorService(misconfiguration);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => InvokeLifecycleMethod(service, "Awake"));
        }

        [Test]
        public void ArmorService_Pickup_ProgressesToMaxLevelRefillsDamagedPlatesAndIgnoresDeadPlayer()
        {
            // Arrange
            ArmorServiceHarness harness = CreateArmorServiceHarness();

            // Act & Assert — Non-Armor pickup ignored
            harness.DropNotifier.RaisePowerUpCollected(PowerUpType.Nuke);
            Assert.AreEqual(0, harness.Service.CurrentLevel);
            Assert.IsFalse(harness.Service.HasArmor);
            Assert.AreEqual(0f, harness.Service.CurrentArmorHealth, TOLERANCE);

            // Act & Assert — Level progression 1 -> 2 -> 3
            harness.DropNotifier.RaisePowerUpCollected(PowerUpType.Armor);
            AssertArmorState(harness, expectedLevel: 1, expectedTotalArmor: 20f, expectedPlateVisibility: new[] { true, false, false });

            harness.DropNotifier.RaisePowerUpCollected(PowerUpType.Armor);
            AssertArmorState(harness, expectedLevel: 2, expectedTotalArmor: 60f, expectedPlateVisibility: new[] { true, true, false });

            harness.DropNotifier.RaisePowerUpCollected(PowerUpType.Armor);
            AssertArmorState(harness, expectedLevel: 3, expectedTotalArmor: 120f, expectedPlateVisibility: new[] { true, true, true });

            // Act & Assert — Pickup while damaged at max level refills all 3 plates (70 -> 120)
            harness.Service.AbsorbDamage(50f);
            Assert.AreEqual(70f, harness.Service.CurrentArmorHealth, TOLERANCE);
            harness.DropNotifier.RaisePowerUpCollected(PowerUpType.Armor);
            AssertArmorState(harness, expectedLevel: 3, expectedTotalArmor: 120f, expectedPlateVisibility: new[] { true, true, true });

            // Act & Assert — Dead player ignores Armor pickup
            harness.Health.DecreaseHealth(100f);
            harness.DropNotifier.RaisePowerUpCollected(PowerUpType.Armor);
            AssertArmorState(harness, expectedLevel: 0, expectedTotalArmor: 0f, expectedPlateVisibility: new[] { false, false, false });
        }

        [Test]
        public void ArmorService_AbsorbDamage_HandlesPartialExactBreakCascadeAndOverflow()
        {
            // Arrange
            ArmorServiceHarness harness = CreateArmorServiceHarness();
            GrantArmorLevels(harness.DropNotifier, 3);

            // Act & Assert — Non-positive damage passes through unchanged
            Assert.AreEqual(0f, harness.Service.AbsorbDamage(0f), TOLERANCE);
            Assert.AreEqual(-5f, harness.Service.AbsorbDamage(-5f), TOLERANCE);
            Assert.AreEqual(120f, harness.Service.CurrentArmorHealth, TOLERANCE);

            // Act & Assert — Partial hit on Plate 3 (60 - 25 = 35 => total 95)
            float partialOverflow = harness.Service.AbsorbDamage(25f);
            Assert.AreEqual(0f, partialOverflow, TOLERANCE);
            AssertArmorState(harness, expectedLevel: 3, expectedTotalArmor: 95f, expectedPlateVisibility: new[] { true, true, true });
            Assert.AreEqual(35f / 60f, harness.PlateFillImages[2].fillAmount, TOLERANCE);

            // Act & Assert — Refill then exact break of Plate 3 (60 => Level 2, 60 HP)
            harness.DropNotifier.RaisePowerUpCollected(PowerUpType.Armor);
            float exactOverflow = harness.Service.AbsorbDamage(60f);
            Assert.AreEqual(0f, exactOverflow, TOLERANCE);
            AssertArmorState(harness, expectedLevel: 2, expectedTotalArmor: 60f, expectedPlateVisibility: new[] { true, true, false });

            // Act & Assert — Refill to 120 HP then cascade overflow (145 => 0 armor, 25 HP overflow)
            harness.DropNotifier.RaisePowerUpCollected(PowerUpType.Armor);
            float cascadeOverflow = harness.Service.AbsorbDamage(145f);
            Assert.AreEqual(25f, cascadeOverflow, TOLERANCE);
            AssertArmorState(harness, expectedLevel: 0, expectedTotalArmor: 0f, expectedPlateVisibility: new[] { false, false, false });

            // Act & Assert — No armor passes full damage through
            Assert.AreEqual(15f, harness.Service.AbsorbDamage(15f), TOLERANCE);
        }

        [Test]
        public void ArmorService_ClearAndDeathAndDisable_ResetAllPlatesImmediately()
        {
            // Arrange
            ArmorServiceHarness harness = CreateArmorServiceHarness();

            // Act & Assert — Explicit ClearArmorImmediate
            GrantArmorLevels(harness.DropNotifier, 3);
            harness.Service.ClearArmorImmediate();
            AssertArmorState(harness, expectedLevel: 0, expectedTotalArmor: 0f, expectedPlateVisibility: new[] { false, false, false });

            // Act & Assert — Player OnNoHealth clears armor
            GrantArmorLevels(harness.DropNotifier, 2);
            harness.Health.DecreaseHealth(100f);
            AssertArmorState(harness, expectedLevel: 0, expectedTotalArmor: 0f, expectedPlateVisibility: new[] { false, false, false });

            // Act & Assert — OnDisable clears armor
            SetActiveAndEnable(harness.Health);
            GrantArmorLevels(harness.DropNotifier, 1);
            InvokeLifecycleMethod(harness.Service, "OnDisable");
            AssertArmorState(harness, expectedLevel: 0, expectedTotalArmor: 0f, expectedPlateVisibility: new[] { false, false, false });

            // Act & Assert — OnDestroy clears armor and unsubscribes
            GrantArmorLevels(harness.DropNotifier, 1);
            InvokeLifecycleMethod(harness.Service, "OnDestroy");
            AssertArmorState(harness, expectedLevel: 0, expectedTotalArmor: 0f, expectedPlateVisibility: new[] { false, false, false });

            harness.DropNotifier.RaisePowerUpCollected(PowerUpType.Armor);
            Assert.AreEqual(0, harness.Service.CurrentLevel);
        }

        [Test]
        public void PlayerDamagedHandler_EnforcesPriorityPortalThenShieldThenArmorThenHealth()
        {
            // Arrange
            DamagePipelineHarness harness = CreateDamagePipelineHarness();
            GrantArmorLevels(harness.DropNotifier, 1);
            harness.BubbleShield.IsActive = true;
            harness.DamagedHandler.SetPortalTransitionInvulnerable(true);

            // Act & Assert — Priority 0: Portal transition invulnerability blocks all
            harness.DamagedHandler.TakeDamage(50f);
            Assert.AreEqual(0, harness.BubbleShield.AbsorbedCount);
            Assert.AreEqual(20f, harness.ArmorService.CurrentArmorHealth, TOLERANCE);
            Assert.AreEqual(100f, harness.Health.CurrentHealth, TOLERANCE);
            Assert.AreEqual(0, harness.AudioPlayer.OneShotKeys.Count);

            // Act & Assert — Priority 1: BubbleShield absorbs without touching Armor or Health
            harness.DamagedHandler.SetPortalTransitionInvulnerable(false);
            harness.DamagedHandler.TakeDamage(50f);
            Assert.AreEqual(1, harness.BubbleShield.AbsorbedCount);
            Assert.AreEqual(20f, harness.ArmorService.CurrentArmorHealth, TOLERANCE);
            Assert.AreEqual(100f, harness.Health.CurrentHealth, TOLERANCE);
            Assert.AreEqual(0, harness.AudioPlayer.OneShotKeys.Count);

            // Act & Assert — Priority 2: Armor absorbs completely when damage <= armor
            harness.BubbleShield.IsActive = false;
            harness.DamagedHandler.TakeDamage(15f);
            Assert.AreEqual(5f, harness.ArmorService.CurrentArmorHealth, TOLERANCE);
            Assert.AreEqual(100f, harness.Health.CurrentHealth, TOLERANCE);
            Assert.AreEqual(0, harness.AudioPlayer.OneShotKeys.Count);

            // Act & Assert — Priority 3: Armor breaks and forwards only 25f overflow to base Health
            harness.DamagedHandler.TakeDamage(30f);
            Assert.AreEqual(0f, harness.ArmorService.CurrentArmorHealth, TOLERANCE);
            Assert.IsFalse(harness.ArmorService.HasArmor);
            Assert.AreEqual(75f, harness.Health.CurrentHealth, TOLERANCE);
            Assert.AreEqual(1, harness.AudioPlayer.OneShotKeys.Count);
            Assert.AreEqual(PlayerAudioConstants.DAMAGED_AUDIO_KEY, harness.AudioPlayer.OneShotKeys[0]);
        }

        [Test]
        public void PlayerDamagedHandler_TakeFullHpDamage_BypassesShieldClearsArmorAndKillsPlayer()
        {
            // Arrange
            DamagePipelineHarness harness = CreateDamagePipelineHarness();
            GrantArmorLevels(harness.DropNotifier, 2);
            harness.BubbleShield.IsActive = true;
            harness.DamagedHandler.SetPortalTransitionInvulnerable(true);

            // Act & Assert — Portal invulnerability still blocks TakeFullHpDamage
            harness.DamagedHandler.TakeFullHpDamage();
            Assert.AreEqual(100f, harness.Health.CurrentHealth, TOLERANCE);
            Assert.IsTrue(harness.Health.IsAlive());
            Assert.AreEqual(60f, harness.ArmorService.CurrentArmorHealth, TOLERANCE);

            // Act — Full-HP damage outside portal transition
            harness.DamagedHandler.SetPortalTransitionInvulnerable(false);
            harness.DamagedHandler.TakeFullHpDamage();

            // Assert — Shield bypassed, armor cleared, health reduced to 0
            Assert.AreEqual(0, harness.BubbleShield.AbsorbedCount);
            Assert.AreEqual(0, harness.ArmorService.CurrentLevel);
            Assert.AreEqual(0f, harness.ArmorService.CurrentArmorHealth, TOLERANCE);
            Assert.IsFalse(harness.ArmorService.HasArmor);
            Assert.AreEqual(0f, harness.Health.CurrentHealth, TOLERANCE);
            Assert.IsFalse(harness.Health.IsAlive());
        }

        [Test]
        public void PlayerDeathHandler_OnNoHealth_HidesVisualDisablesNonWheelCollidersAndOpensDeathScreen()
        {
            // Arrange
            DeathPipelineHarness harness = CreateDeathPipelineHarness();

            // Act
            harness.Health.DecreaseHealth(100f);

            // Assert
            Assert.IsFalse(harness.Visual.activeSelf);
            Assert.IsFalse(harness.BodyCollider.enabled);
            Assert.IsTrue(harness.WheelColliders[0].enabled);
            Assert.IsTrue(harness.WheelColliders[1].enabled);
            Assert.AreEqual(1, harness.DeathPresenter.EnableCount);
        }

        private ArmorConfigSO CreateInvalidArmorConfig(ArmorConfigInvalidKind invalidKind)
        {
            ArmorConfigSO config = CreateTrackedScriptableObject<ArmorConfigSO>();

            if (invalidKind == ArmorConfigInvalidKind.WrongCapacitiesLength)
            {
                SetPrivateField(typeof(ArmorConfigSO), config, "_plateCapacities", new[] { 20f, 40f });
            }
            else if (invalidKind == ArmorConfigInvalidKind.ZeroCapacity)
            {
                SetPrivateField(typeof(ArmorConfigSO), config, "_plateCapacities", new[] { 20f, 0f, 60f });
            }
            else if (invalidKind == ArmorConfigInvalidKind.NegativeCapacity)
            {
                SetPrivateField(typeof(ArmorConfigSO), config, "_plateCapacities", new[] { 20f, -5f, 60f });
            }
            else if (invalidKind == ArmorConfigInvalidKind.NaNCapacity)
            {
                SetPrivateField(typeof(ArmorConfigSO), config, "_plateCapacities", new[] { 20f, float.NaN, 60f });
            }
            else if (invalidKind == ArmorConfigInvalidKind.NegativeOverlap)
            {
                SetPrivateField(typeof(ArmorConfigSO), config, "_horizontalOverlapRatio", -0.1f);
            }
            else if (invalidKind == ArmorConfigInvalidKind.OverlapAboveOne)
            {
                SetPrivateField(typeof(ArmorConfigSO), config, "_horizontalOverlapRatio", 1.1f);
            }
            else if (invalidKind == ArmorConfigInvalidKind.NonPositiveRefWidth)
            {
                SetPrivateField(typeof(ArmorConfigSO), config, "_referenceBaseHealthWidth", 0f);
            }
            else
            {
                SetPrivateField(typeof(ArmorConfigSO), config, "_referenceBaseMaxHealth", 0f);
            }

            return config;
        }

        private ArmorService CreateMisconfiguredArmorService(ArmorServiceMisconfiguration misconfiguration)
        {
            GameObject serviceGo = CreateTrackedGameObject("MisconfiguredArmorService", active: false);
            ArmorService service = serviceGo.AddComponent<ArmorService>();

            if (misconfiguration == ArmorServiceMisconfiguration.MissingConfig)
            {
                ArmorPlatesView validView = CreateArmorPlatesView(out _);
                SetPrivateField(typeof(ArmorService), service, "_config", null);
                SetPrivateField(typeof(ArmorService), service, "_armorPlatesView", validView);
            }
            else if (misconfiguration == ArmorServiceMisconfiguration.MissingPlatesView)
            {
                ArmorConfigSO validConfig = CreateTrackedScriptableObject<ArmorConfigSO>();
                SetPrivateField(typeof(ArmorService), service, "_config", validConfig);
                SetPrivateField(typeof(ArmorService), service, "_armorPlatesView", null);
            }
            else
            {
                ArmorConfigSO invalidConfig = CreateInvalidArmorConfig(ArmorConfigInvalidKind.ZeroCapacity);
                ArmorPlatesView validView = CreateArmorPlatesView(out _);
                SetPrivateField(typeof(ArmorService), service, "_config", invalidConfig);
                SetPrivateField(typeof(ArmorService), service, "_armorPlatesView", validView);
            }

            return service;
        }

        private ArmorServiceHarness CreateArmorServiceHarness()
        {
            Health health = CreateEnabledHealth(DEFAULT_MAX_HEALTH);
            FakeAudioClipPlayer audioPlayer = new FakeAudioClipPlayer();
            FakePlayerManager playerManager = new FakePlayerManager(health.gameObject, health, audioPlayer);
            FakeCollectibleDropNotifier dropNotifier = new FakeCollectibleDropNotifier();

            ArmorConfigSO config = CreateTrackedScriptableObject<ArmorConfigSO>();
            ArmorPlatesView platesView = CreateArmorPlatesView(out Image[] plateFillImages);

            GameObject serviceGo = CreateTrackedGameObject("ArmorService", active: false);
            ArmorService service = serviceGo.AddComponent<ArmorService>();
            SetPrivateField(typeof(ArmorService), service, "_config", config);
            SetPrivateField(typeof(ArmorService), service, "_armorPlatesView", platesView);

            Container container = BuildContainer(builder =>
            {
                builder.AddSingleton(dropNotifier, typeof(ICollectibleDropNotifier));
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
            });

            GameObjectInjector.InjectRecursive(serviceGo, container);
            serviceGo.SetActive(true);
            InvokeLifecycleMethod(platesView, "Awake");
            InvokeLifecycleMethod(service, "Awake");
            InvokeLifecycleMethod(service, "Start");

            return new ArmorServiceHarness(service, health, dropNotifier, platesView, plateFillImages);
        }

        private DamagePipelineHarness CreateDamagePipelineHarness()
        {
            Health health = CreateEnabledHealth(DEFAULT_MAX_HEALTH);
            FakeAudioClipPlayer audioPlayer = new FakeAudioClipPlayer();
            FakeCollectibleDropNotifier dropNotifier = new FakeCollectibleDropNotifier();
            FakeBubbleShield bubbleShield = new FakeBubbleShield();
            FakeRunSessionService runSession = new FakeRunSessionService();

            ArmorConfigSO config = CreateTrackedScriptableObject<ArmorConfigSO>();
            ArmorPlatesView platesView = CreateArmorPlatesView(out _);

            GameObject serviceGo = CreateTrackedGameObject("ArmorService", active: false);
            ArmorService armorService = serviceGo.AddComponent<ArmorService>();
            SetPrivateField(typeof(ArmorService), armorService, "_config", config);
            SetPrivateField(typeof(ArmorService), armorService, "_armorPlatesView", platesView);

            GameObject playerHost = CreateTrackedGameObject("PlayerHost", active: false);
            GameObject carVisual = new GameObject("CarVisual");
            carVisual.transform.SetParent(playerHost.transform, false);
            PlayerDamagedHandler damagedHandler = playerHost.AddComponent<PlayerDamagedHandler>();
            SetPrivateField(typeof(PlayerDamagedHandler), damagedHandler, "_carVisual", carVisual);

            FakePlayerManager playerManager = new FakePlayerManager(playerHost, health, audioPlayer);

            Container container = BuildContainer(builder =>
            {
                builder.AddSingleton(dropNotifier, typeof(ICollectibleDropNotifier));
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
                builder.AddSingleton(bubbleShield, typeof(IBubbleShield));
                builder.AddSingleton(armorService, typeof(IArmorService));
                builder.AddSingleton(runSession, typeof(IRunSessionService));
            });

            GameObjectInjector.InjectRecursive(serviceGo, container);
            GameObjectInjector.InjectRecursive(playerHost, container);

            serviceGo.SetActive(true);
            InvokeLifecycleMethod(platesView, "Awake");
            InvokeLifecycleMethod(armorService, "Awake");
            InvokeLifecycleMethod(armorService, "Start");

            playerHost.SetActive(true);
            InvokeLifecycleMethod(damagedHandler, "Awake");

            return new DamagePipelineHarness(damagedHandler, armorService, health, dropNotifier, bubbleShield, audioPlayer);
        }

        private DeathPipelineHarness CreateDeathPipelineHarness()
        {
            Health health = CreateEnabledHealth(DEFAULT_MAX_HEALTH);
            FakeAudioClipPlayer audioPlayer = new FakeAudioClipPlayer();
            FakePlayerDeathPresenter deathPresenter = new FakePlayerDeathPresenter();
            FakeRunSessionService runSession = new FakeRunSessionService();

            GameObject playerHost = CreateTrackedGameObject("PlayerDeathHost", active: false);
            PlayerDeathHandler deathHandler = playerHost.AddComponent<PlayerDeathHandler>();

            GameObject visual = new GameObject("CarVisual");
            visual.transform.SetParent(playerHost.transform, false);
            visual.SetActive(true);

            GameObject vfxGo = new GameObject("DeathVFX");
            vfxGo.transform.SetParent(playerHost.transform, false);
            VFXPlayer deathVfxPlayer = vfxGo.AddComponent<VFXPlayer>();

            BoxCollider bodyCollider = playerHost.AddComponent<BoxCollider>();
            bodyCollider.enabled = true;

            GameObject wheel1Go = new GameObject("Wheel1");
            wheel1Go.transform.SetParent(playerHost.transform, false);
            SphereCollider wheel1 = wheel1Go.AddComponent<SphereCollider>();
            wheel1.enabled = true;

            GameObject wheel2Go = new GameObject("Wheel2");
            wheel2Go.transform.SetParent(playerHost.transform, false);
            SphereCollider wheel2 = wheel2Go.AddComponent<SphereCollider>();
            wheel2.enabled = true;

            Collider[] wheelColliders = new Collider[] { wheel1, wheel2 };
            SetPrivateField(typeof(PlayerDeathHandler), deathHandler, "_visual", visual);
            SetPrivateField(typeof(PlayerDeathHandler), deathHandler, "_deathVfxPlayer", deathVfxPlayer);
            SetPrivateField(typeof(PlayerDeathHandler), deathHandler, "_wheelColliders", wheelColliders);

            FakePlayerManager playerManager = new FakePlayerManager(playerHost, health, audioPlayer);

            Container container = BuildContainer(builder =>
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
                builder.AddSingleton(deathPresenter, typeof(IPlayerDeathPresenter));
                builder.AddSingleton(new FakeBubbleShield(), typeof(IBubbleShield));
                builder.AddSingleton(new FakeArmorService(), typeof(IArmorService));
                builder.AddSingleton(runSession, typeof(IRunSessionService));
            });

            GameObjectInjector.InjectRecursive(playerHost, container);

            playerHost.SetActive(true);
            InvokeLifecycleMethod(deathVfxPlayer, "Awake");
            InvokeLifecycleMethod(deathHandler, "Awake");
            InvokeLifecycleMethod(deathHandler, "OnEnable");

            return new DeathPipelineHarness(deathHandler, health, visual, bodyCollider, wheelColliders, deathPresenter);
        }

        private ArmorPlatesView CreateArmorPlatesView(out Image[] plateFillImages)
        {
            GameObject canvasGo = CreateTrackedGameObject("HealthBarCanvas", active: true);
            RectTransform canvasRect = canvasGo.AddComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1.5f, 0.2f);

            GameObject baseBarGo = new GameObject("HealthBarSlider", typeof(RectTransform));
            baseBarGo.transform.SetParent(canvasGo.transform, false);
            RectTransform baseRect = baseBarGo.GetComponent<RectTransform>();
            baseRect.sizeDelta = new Vector2(1.5f, 0.2f);
            baseRect.pivot = new Vector2(0.5f, 0.5f);

            ArmorPlateItemView[] plateViews = new ArmorPlateItemView[3];
            plateFillImages = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject plateGo = new GameObject($"ArmorPlate_{i + 1}", typeof(RectTransform), typeof(Image));
                plateGo.transform.SetParent(canvasGo.transform, false);
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

                plateViews[i] = itemView;
                plateFillImages[i] = fillImage;
            }

            ArmorPlatesView platesView = canvasGo.AddComponent<ArmorPlatesView>();
            SetPrivateField(typeof(ArmorPlatesView), platesView, "_baseHealthBarRect", baseRect);
            SetPrivateField(typeof(ArmorPlatesView), platesView, "_plateViews", plateViews);
            return platesView;
        }

        private Health CreateEnabledHealth(float maxHealth)
        {
            GameObject go = CreateTrackedGameObject("TestPlayerHealth", active: false);
            Health health = go.AddComponent<Health>();
            health.MaxHealth = maxHealth;
            SetActiveAndEnable(health);
            return health;
        }

        private GameObject CreateTrackedGameObject(string name, bool active)
        {
            GameObject go = new GameObject(name);
            go.SetActive(active);
            _createdObjects.Add(go);
            return go;
        }

        private T CreateTrackedScriptableObject<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private Container BuildContainer(Action<ContainerBuilder> configure)
        {
            ContainerBuilder builder = new ContainerBuilder().SetName("HealthPipelineTestContainer");
            configure(builder);
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private static void GrantArmorLevels(FakeCollectibleDropNotifier notifier, int count)
        {
            for (int i = 0; i < count; i++)
            {
                notifier.RaisePowerUpCollected(PowerUpType.Armor);
            }
        }

        private static void AssertArmorState(
            ArmorServiceHarness harness,
            int expectedLevel,
            float expectedTotalArmor,
            bool[] expectedPlateVisibility)
        {
            Assert.AreEqual(expectedLevel, harness.Service.CurrentLevel);
            Assert.AreEqual(expectedLevel > 0, harness.Service.HasArmor);
            Assert.AreEqual(expectedTotalArmor, harness.Service.CurrentArmorHealth, TOLERANCE);

            for (int i = 0; i < expectedPlateVisibility.Length; i++)
            {
                Assert.AreEqual(expectedPlateVisibility[i], harness.PlateFillImages[i].transform.parent.gameObject.activeSelf);
            }
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

        private readonly struct ArmorServiceHarness
        {
            public readonly ArmorService Service;
            public readonly Health Health;
            public readonly FakeCollectibleDropNotifier DropNotifier;
            public readonly ArmorPlatesView PlatesView;
            public readonly Image[] PlateFillImages;

            public ArmorServiceHarness(
                ArmorService service,
                Health health,
                FakeCollectibleDropNotifier dropNotifier,
                ArmorPlatesView platesView,
                Image[] plateFillImages)
            {
                Service = service;
                Health = health;
                DropNotifier = dropNotifier;
                PlatesView = platesView;
                PlateFillImages = plateFillImages;
            }
        }

        private readonly struct DamagePipelineHarness
        {
            public readonly PlayerDamagedHandler DamagedHandler;
            public readonly ArmorService ArmorService;
            public readonly Health Health;
            public readonly FakeCollectibleDropNotifier DropNotifier;
            public readonly FakeBubbleShield BubbleShield;
            public readonly FakeAudioClipPlayer AudioPlayer;

            public DamagePipelineHarness(
                PlayerDamagedHandler damagedHandler,
                ArmorService armorService,
                Health health,
                FakeCollectibleDropNotifier dropNotifier,
                FakeBubbleShield bubbleShield,
                FakeAudioClipPlayer audioPlayer)
            {
                DamagedHandler = damagedHandler;
                ArmorService = armorService;
                Health = health;
                DropNotifier = dropNotifier;
                BubbleShield = bubbleShield;
                AudioPlayer = audioPlayer;
            }
        }

        private readonly struct DeathPipelineHarness
        {
            public readonly PlayerDeathHandler DeathHandler;
            public readonly Health Health;
            public readonly GameObject Visual;
            public readonly Collider BodyCollider;
            public readonly Collider[] WheelColliders;
            public readonly FakePlayerDeathPresenter DeathPresenter;

            public DeathPipelineHarness(
                PlayerDeathHandler deathHandler,
                Health health,
                GameObject visual,
                Collider bodyCollider,
                Collider[] wheelColliders,
                FakePlayerDeathPresenter deathPresenter)
            {
                DeathHandler = deathHandler;
                Health = health;
                Visual = visual;
                BodyCollider = bodyCollider;
                WheelColliders = wheelColliders;
                DeathPresenter = deathPresenter;
            }
        }

        private sealed class FakeCollectibleDropNotifier : ICollectibleDropNotifier
        {
            public event EventHandler OnSkillUpgradeCollectibleCollected
            {
                add { }
                remove { }
            }

            public event Action<PowerUpType> OnPowerUpCollected;

            public void RaisePowerUpCollected(PowerUpType type)
            {
                OnPowerUpCollected?.Invoke(type);
            }

            public void SpawnCollectible(GameObject prefab, Vector3 spawnPos, Vector3 targetPos)
            {
            }
        }

        private sealed class FakeBubbleShield : IBubbleShield
        {
            public bool IsActive { get; set; }
            public int AbsorbedCount { get; private set; }

            public void NotifyDamageAbsorbed()
            {
                AbsorbedCount++;
            }
        }

        private sealed class FakeArmorService : IArmorService
        {
            public int CurrentLevel { get { return 0; } }
            public float CurrentArmorHealth { get { return 0f; } }
            public bool HasArmor { get { return false; } }

            public float AbsorbDamage(float incomingDamage)
            {
                return incomingDamage;
            }

            public void ClearArmorImmediate()
            {
            }
        }

        private sealed class FakeAudioClipPlayer : IAudioClipPlayer
        {
            public List<string> OneShotKeys { get; } = new List<string>();

            public event EventHandler OnAudioClipFinished
            {
                add { }
                remove { }
            }

            public void Play(string name)
            {
            }

            public void PlayOneShot(string name)
            {
                OneShotKeys.Add(name);
            }
        }

        private sealed class FakePlayerDeathPresenter : IPlayerDeathPresenter
        {
            public int EnableCount { get; private set; }

            public void EnableDeathScreen()
            {
                EnableCount++;
            }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            public GameObject GameObject { get; }
            public IHealth Health { get; }
            public IAudioClipPlayer AudioClipPlayer { get; }
            public ICarController CarController { get { return null; } }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get { return null; } }
            public ISkillsRegistry SkillsRegistry { get { return null; } }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }

            public FakePlayerManager(GameObject gameObject, IHealth health, IAudioClipPlayer audioClipPlayer)
            {
                GameObject = gameObject;
                Health = health;
                AudioClipPlayer = audioClipPlayer;
            }
        }

        private sealed class FakeRunSessionService : IRunSessionService
        {
            private bool _isGodModeEnabled;

            public int CurrentTierIndex { get { return 0; } }
            public int TotalTiersCount { get { return 1; } }
            public bool IsFinalTier { get { return true; } }
            public LoopTierConfig ActiveTierConfig { get { return null; } }
            public GameLoopProgressionConfigSO ProgressionConfig { get { return null; } }
            public bool HasPendingRestorationSnapshot { get { return false; } }
            public uint AccumulatedRunSeconds { get { return 0; } }
            public bool IsGodModeEnabled { get { return _isGodModeEnabled; } }

            public void CaptureSnapshotAndAdvanceTier(IPlayerManager playerManager, uint elapsedSeconds)
            {
            }

            public void CaptureSnapshotAndSetTier(int tierIndex, IPlayerManager playerManager, uint elapsedSeconds)
            {
            }

            public void CaptureFinalRunTimer(uint elapsedSeconds)
            {
            }

            public bool TryGetRestoredLevel(out byte level, out float exp)
            {
                level = 1;
                exp = 0f;
                return false;
            }

            public bool TryGetRestoredSkills(out IReadOnlyList<SavedSkillSnapshot> skills)
            {
                skills = null;
                return false;
            }

            public void SetGodModeEnabled(bool isEnabled)
            {
                _isGodModeEnabled = isEnabled;
            }

            public void ResetSession()
            {
                _isGodModeEnabled = false;
            }

            public void ResetSessionToTier(int tierIndex)
            {
                _isGodModeEnabled = false;
            }
        }
    }
}
