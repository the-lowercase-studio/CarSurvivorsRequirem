using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Assets.ScriptableObjects.Player.Skills;
using Assets.ScriptableObjects.Skills;
using Assets.ScriptableObjects.UI;
using Assets.Scripts.Audio;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.Common.Types;
using Assets.Scripts.Enemies;
using Assets.Scripts.GameFlow;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Input;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.PowerUps;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.Constants;
using Assets.Scripts.Skills.UpgradeFlow;
using Assets.Scripts.Stats;
using Assets.Scripts.UI.Constants;
using Assets.Scripts.UI.HUD;
using Assets.Scripts.UI.Input;
using Assets.Scripts.UI.Level;
using Assets.Scripts.UI.Skills;
using DG.Tweening;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Assets.Scripts.Editor.Tests
{
    public class SkillsUITests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            DOTween.Clear(true);

            for (int i = 0; i < _createdContainers.Count; i++)
            {
                if (_createdContainers[i] != null)
                {
                    _createdContainers[i].Dispose();
                }
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
        public void SkillStatValueFormatter_FormatsIntegersFloatsNegativeZeroUnitsAndRejectsInvalid()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                SkillStatValueFormatter.Format(null);
            });

            FloatUpgradeableStat nanStat = CreateFloatStat(1f, 5f, 0.5f, 1f, StatsUnits.Seconds);
            nanStat.RestoreRuntimeState(double.NaN, true);
            Assert.Throws<InvalidOperationException>(() =>
            {
                SkillStatValueFormatter.Format(nanStat);
            });

            FloatUpgradeableStat infStat = CreateFloatStat(1f, 5f, 0.5f, 1f, StatsUnits.Seconds);
            infStat.RestoreRuntimeState(double.PositiveInfinity, true);
            Assert.Throws<InvalidOperationException>(() =>
            {
                SkillStatValueFormatter.Format(infStat);
            });

            IntUpgradeableStat intNone = CreateIntStat(5, 20, 2, 4, StatsUnits.None);
            Assert.AreEqual("5", SkillStatValueFormatter.Format(intNone));

            IntUpgradeableStat intPercent = CreateIntStat(25, 100, 5, 10, StatsUnits.Percentage);
            Assert.AreEqual("25%", SkillStatValueFormatter.Format(intPercent));

            FloatUpgradeableStat floatSeconds = CreateFloatStat(1.256f, 5f, 0.2f, 0.5f, StatsUnits.Seconds);
            Assert.AreEqual("1.26 s", SkillStatValueFormatter.Format(floatSeconds));

            FloatUpgradeableStat floatMeters = CreateFloatStat(8f, 20f, 1f, 2f, StatsUnits.Meters);
            Assert.AreEqual("8 m", SkillStatValueFormatter.Format(floatMeters));

            FloatUpgradeableStat negativeZero = CreateFloatStat(1f, 5f, 0.5f, 1f, StatsUnits.None);
            negativeZero.RestoreRuntimeState(-0.001d, true);
            Assert.AreEqual("0", SkillStatValueFormatter.Format(negativeZero));
        }

        [Test]
        public void PlayerSkillsHUDPresenter_PopulatesInitialAndNewSkillsHidesEmptyFramesAndUnsubscribesOnDestroy()
        {
            Sprite sawSprite = CreateTestSprite();
            Sprite minigunSprite = CreateTestSprite();

            FakeSkillsRegistry registry = new FakeSkillsRegistry();
            FakeSkill sawSkill = CreateFakeSkill("Saw", sawSprite, initialized: true);
            FakeSkill minigunSkill = CreateFakeSkill("Minigun", minigunSprite, initialized: false);
            FakeSkill thirdSkill = CreateFakeSkill("Landmine", sawSprite, initialized: false);
            FakeSkill overflowSkill = CreateFakeSkill("Lasergun", minigunSprite, initialized: false);
            registry.SetSkills(sawSkill, minigunSkill, thirdSkill, overflowSkill);

            GameObject hudGo = TrackGameObject("PlayerSkillsHUD", active: true);
            PlayerSkillsHUDPresenter presenter = hudGo.AddComponent<PlayerSkillsHUDPresenter>();

            Image[] holders = new Image[3];
            Image[] icons = new Image[3];
            GameObject[] emptyFrames = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject holderGo = CreateChild(hudGo.transform, $"Slot{i}");
                holders[i] = holderGo.AddComponent<Image>();

                GameObject iconGo = CreateChild(holderGo.transform, $"Icon{i}");
                icons[i] = iconGo.AddComponent<Image>();

                emptyFrames[i] = CreateChild(holderGo.transform, $"EmptyFrame{i}");
            }

            SetPrivateField(typeof(PlayerSkillsHUDPresenter), presenter, "_skillIconHolders", holders);
            SetPrivateField(typeof(PlayerSkillsHUDPresenter), presenter, "_iconImages", icons);
            SetPrivateField(typeof(PlayerSkillsHUDPresenter), presenter, "_emptySlotFrames", emptyFrames);

            FakePlayerManager playerManager = new FakePlayerManager(registry);
            Container container = BuildContainer((builder) =>
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
            });
            GameObjectInjector.InjectRecursive(hudGo, container);

            InvokeMethod(presenter, "Start");

            Assert.AreSame(sawSprite, icons[0].sprite);
            Assert.IsTrue(icons[0].enabled);
            Assert.IsFalse(emptyFrames[0].activeSelf);
            Assert.IsFalse(icons[1].enabled);
            Assert.IsTrue(emptyFrames[1].activeSelf);

            // Duplicate initialization of already-assigned skill must not consume slot 1.
            registry.RaiseInitialized(sawSkill);
            Assert.IsFalse(icons[1].enabled);

            // Null skill ignored safely.
            registry.RaiseInitialized(null);
            Assert.IsFalse(icons[1].enabled);

            // Second and third skills populate slots 1 and 2.
            registry.InitializeSkill(minigunSkill);
            Assert.AreSame(minigunSprite, icons[1].sprite);
            Assert.IsTrue(icons[1].enabled);
            Assert.IsFalse(emptyFrames[1].activeSelf);

            registry.InitializeSkill(thirdSkill);
            Assert.IsTrue(icons[2].enabled);
            Assert.IsFalse(emptyFrames[2].activeSelf);

            // Fourth skill beyond 3-slot capacity is ignored and leaves slot count capped at 3.
            registry.InitializeSkill(overflowSkill);
            Assert.AreEqual(3, GetPrivateField<int>(typeof(PlayerSkillsHUDPresenter), presenter, "_assignedSlotCount"));
            Assert.AreSame(sawSprite, icons[0].sprite);
            Assert.AreSame(minigunSprite, icons[1].sprite);

            // OnDestroy unsubscribes from OnSkillInitialized.
            InvokeMethod(presenter, "OnDestroy");
            Assert.AreEqual(0, registry.ListenerCount);
        }

        [Test]
        public void SkillsVisualPresenter_SetsContextColorsActivatesNamedVisualsAndHidesAll()
        {
            GameObject presenterGo = TrackGameObject("SkillsVisualPresenter", active: true);
            SkillsVisualPresenter presenter = presenterGo.AddComponent<SkillsVisualPresenter>();

            Camera primaryCam = CreateChild(presenterGo.transform, "PrimaryCam").AddComponent<Camera>();
            Camera secondaryCam = CreateChild(presenterGo.transform, "SecondaryCam").AddComponent<Camera>();
            GameObject primarySaw = CreateChild(presenterGo.transform, "Saw");
            GameObject secondarySaw = CreateChild(presenterGo.transform, "Saw");
            primarySaw.SetActive(false);
            secondarySaw.SetActive(false);

            SetPrivateField(typeof(SkillsVisualPresenter), presenter, "_primaryCamera", primaryCam);
            SetPrivateField(typeof(SkillsVisualPresenter), presenter, "_secondaryCamera", secondaryCam);
            SetPrivateField(typeof(SkillsVisualPresenter), presenter, "_statUpgradeBackgroundColor", Color.blue);
            SetPrivateField(typeof(SkillsVisualPresenter), presenter, "_newSkillUnlockedBackgroundColor", Color.green);
            SetPrivateField(typeof(SkillsVisualPresenter), presenter, "_skillsVisuals", new[] { primarySaw });
            SetPrivateField(typeof(SkillsVisualPresenter), presenter, "_secondarySkillsVisuals", new[] { secondarySaw });

            presenter.SetContext(SkillVisualContext.StatUpgrade);
            Assert.AreEqual(CameraClearFlags.SolidColor, primaryCam.clearFlags);
            Assert.AreEqual(Color.blue, primaryCam.backgroundColor);
            Assert.AreEqual(Color.blue, secondaryCam.backgroundColor);

            presenter.SetContext(SkillVisualContext.NewSkillUnlocked);
            Assert.AreEqual(Color.green, primaryCam.backgroundColor);
            Assert.AreEqual(Color.green, secondaryCam.backgroundColor);

            SkillInfoSO sawInfo = CreateSkillInfo("Saw", "Saw desc", null);
            presenter.ShowSkillVisual(sawInfo, 0);
            presenter.ShowSkillVisual(sawInfo, 1);
            Assert.IsTrue(primarySaw.activeSelf);
            Assert.IsTrue(secondarySaw.activeSelf);

            SkillInfoSO missingInfo = CreateSkillInfo("MissingSkill", "Missing", null);
            LogAssert.Expect(LogType.Warning, "Skill visual for MissingSkill at slot 0 was not found.");
            presenter.ShowSkillVisual(missingInfo, 0);

            presenter.HideAll();
            Assert.IsFalse(primarySaw.activeSelf);
            Assert.IsFalse(secondarySaw.activeSelf);
        }

        [Test]
        public void SkillStatRowView_AndSkillStatsGroupView_ValidateInitializeBindRefreshFreezeAndClear()
        {
            GameObject invalidRowGo = TrackGameObject("InvalidRow", active: false);
            SkillStatRowView invalidRow = invalidRowGo.AddComponent<SkillStatRowView>();
            TargetInvocationException awakeEx = Assert.Throws<TargetInvocationException>(() =>
            {
                InvokeMethod(invalidRow, "Awake");
            });
            Assert.IsInstanceOf<InvalidOperationException>(awakeEx.InnerException);

            SkillStatRowView rowPrefab = CreateStatRowPrefab();
            GameObject groupGo = TrackGameObject("GroupView", active: true);
            Image skillIcon = CreateChild(groupGo.transform, "SkillIcon").AddComponent<Image>();
            TextMeshProUGUI skillName = CreateChild(groupGo.transform, "SkillName").AddComponent<TextMeshProUGUI>();
            RectTransform holder = CreateChild(groupGo.transform, "RowsHolder").AddComponent<RectTransform>();

            SkillStatsGroupView group = groupGo.AddComponent<SkillStatsGroupView>();
            SetPrivateField(typeof(SkillStatsGroupView), group, "_skillIcon", skillIcon);
            SetPrivateField(typeof(SkillStatsGroupView), group, "_skillNameText", skillName);
            SetPrivateField(typeof(SkillStatsGroupView), group, "_rowsHolder", holder);
            SetPrivateField(typeof(SkillStatsGroupView), group, "_statRowPrefab", rowPrefab);

            InvokeMethod(group, "Awake");
            group.Initialize();
            Assert.AreEqual(SkillsStatsConstants.MAX_STATS_PER_SKILL, holder.childCount);

            Sprite iconSprite = CreateTestSprite();
            SkillInfoSO info = CreateSkillInfo("Saw", "Desc", iconSprite);
            IntUpgradeableStat damageStat = CreateIntStat(10, 50, 5, 5, StatsUnits.None);
            List<NameUpgradableStatPair> stats = new List<NameUpgradableStatPair>
            {
                new NameUpgradableStatPair("Damage", damageStat, null)
            };

            group.Bind(info, stats);
            Assert.AreEqual("Saw", skillName.text);
            Assert.AreSame(iconSprite, skillIcon.sprite);
            SkillStatRowView firstRow = holder.GetChild(0).GetComponent<SkillStatRowView>();
            SkillStatRowView secondRow = holder.GetChild(1).GetComponent<SkillStatRowView>();
            Assert.IsTrue(firstRow.gameObject.activeSelf);
            Assert.IsFalse(secondRow.gameObject.activeSelf);
            Assert.AreEqual("10", firstRow.GetComponentInChildren<TextMeshProUGUI>().text);

            damageStat.Upgrade(5f);
            group.RefreshValues();
            Assert.AreEqual("15", firstRow.GetComponentInChildren<TextMeshProUGUI>().text);

            // FreezeSnapshot detaches stat reference; Clear hides header and rows.
            group.FreezeSnapshot();
            group.Clear();
            Assert.AreEqual(string.Empty, skillName.text);
            Assert.IsFalse(firstRow.gameObject.activeSelf);

            // Exceeding MAX_STATS_PER_SKILL throws.
            List<NameUpgradableStatPair> tooMany = new List<NameUpgradableStatPair>();
            for (int i = 0; i <= SkillsStatsConstants.MAX_STATS_PER_SKILL; i++)
            {
                tooMany.Add(new NameUpgradableStatPair($"Stat{i}", damageStat, null));
            }

            Assert.Throws<InvalidOperationException>(() =>
            {
                group.Bind(info, tooMany);
            });
        }

        [Test]
        public void SkillsStatsPresenter_PauseRefreshesLive_DeathFreezesSnapshot_AndSceneLoadResets()
        {
            FakeSkillsRegistry registry = new FakeSkillsRegistry();
            Sprite icon = CreateTestSprite();
            FakeSkill sawSkill = CreateFakeSkill("Saw", icon, initialized: true);
            FakeSkill minigunSkill = CreateFakeSkill("Minigun", icon, initialized: false);
            registry.SetSkills(sawSkill, minigunSkill);

            FakePlayerManager playerManager = new FakePlayerManager(registry);
            FakeGameSceneLoader sceneLoader = new FakeGameSceneLoader();

            GameObject presenterGo = TrackGameObject("SkillsStatsPresenter", active: true);
            GameObject visualGo = CreateChild(presenterGo.transform, "Visual");
            SkillStatRowView rowPrefab = CreateStatRowPrefab();
            SkillStatsGroupView[] slots = new SkillStatsGroupView[SkillConstants.MAX_ACTIVE_SKILLS];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = CreateGroupView(visualGo.transform, $"Slot{i}", rowPrefab);
            }

            SkillsStatsPresenter presenter = presenterGo.AddComponent<SkillsStatsPresenter>();
            SetPrivateField(typeof(SkillsStatsPresenter), presenter, "_visual", visualGo);
            SetPrivateField(typeof(SkillsStatsPresenter), presenter, "_skillSlots", slots);

            Container container = BuildContainer((builder) =>
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
                builder.AddSingleton(sceneLoader, typeof(IGameSceneLoader));
            });
            GameObjectInjector.InjectRecursive(presenterGo, container);

            InvokeMethod(presenter, "Awake");
            Assert.IsFalse(visualGo.activeSelf);

            SetPrivateField(typeof(SkillsStatsPresenter), presenter, "_hasStarted", true);
            InvokeMethod(presenter, "ConnectSceneLoading");
            StepCoroutineToCompletion(presenter, "InitializeAfterSceneStart");

            // Pause visible shows owned skill and reacts live to stat upgrades and new skill acquisitions.
            presenter.SetPauseVisible(true);
            Assert.IsTrue(visualGo.activeSelf);
            TextMeshProUGUI slot0Text = slots[0].GetComponentInChildren<SkillStatRowView>(true).GetComponentInChildren<TextMeshProUGUI>(true);
            Assert.AreEqual("10", slot0Text.text);

            sawSkill.TestConfig.Damage.Upgrade(4f);
            Assert.AreEqual("14", slot0Text.text);

            registry.InitializeSkill(minigunSkill);
            TextMeshProUGUI slot1Name = GetPrivateField<TextMeshProUGUI>(typeof(SkillStatsGroupView), slots[1], "_skillNameText");
            Assert.AreEqual("Minigun", slot1Name.text);

            // Death mode freezes snapshot and ignores subsequent upgrades and SetPauseVisible(false).
            presenter.ShowDeathStats();
            sawSkill.TestConfig.Damage.Upgrade(6f);
            presenter.SetPauseVisible(false);
            Assert.IsTrue(visualGo.activeSelf);
            Assert.AreEqual("14", slot0Text.text);

            // Scene load resets state, hides visual, and clears groups.
            sceneLoader.RaiseStartLoadingScene(GameScene.MainMenu);
            Assert.IsFalse(visualGo.activeSelf);
            Assert.AreEqual(string.Empty, slot1Name.text);
        }

        [Test]
        public void SkillUpgradeButton_Initialize_SetsTextBindsPromptAppliesRarityAndInvokesCallbacks()
        {
            Sprite statIcon = CreateTestSprite();
            Sprite raritySprite = CreateTestSprite();
            SkillUpgradeRaritySpriteMapping mapping = TrackScriptableObject<SkillUpgradeRaritySpriteMapping>();
            SetPrivateField(typeof(SkillUpgradeRaritySpriteMapping), mapping, "_outlinedSprite", raritySprite);
            SetPrivateField(typeof(SkillUpgradeRaritySpriteMapping), mapping, "_rareColor", Color.cyan);

            GameObject buttonRoot = TrackGameObject("UpgradeButton", active: true);
            Button button = buttonRoot.AddComponent<Button>();
            Image bgImage = buttonRoot.AddComponent<Image>();
            button.targetGraphic = bgImage;

            TextMeshProUGUI label = CreateChild(buttonRoot.transform, "Text").AddComponent<TextMeshProUGUI>();
            Image iconImage = CreateChild(buttonRoot.transform, "StatIcon").AddComponent<Image>();

            InputPromptView promptView = CreateInputPromptView(buttonRoot.transform, "Prompt");
            FakeInputPromptProvider promptProvider = new FakeInputPromptProvider("1");

            SkillUpgradeButton upgradeButton = buttonRoot.AddComponent<SkillUpgradeButton>();
            SetPrivateField(typeof(SkillUpgradeButton), upgradeButton, "_button", button);
            SetPrivateField(typeof(SkillUpgradeButton), upgradeButton, "_text", label);
            SetPrivateField(typeof(SkillUpgradeButton), upgradeButton, "_statIconImage", iconImage);
            SetPrivateField(typeof(SkillUpgradeButton), upgradeButton, "_inputPromptView", promptView);
            SetPrivateField(typeof(SkillUpgradeButton), upgradeButton, "_rarityBackgroundImage", bgImage);
            SetPrivateField(typeof(SkillUpgradeButton), upgradeButton, "_raritySpriteMapping", mapping);

            int clicked = 0;
            int hovered = 0;
            upgradeButton.Initialize(
                "Increase Damage by 5",
                promptProvider,
                InputActionId.UpgradeSelectChoice1,
                SkillUpgradeRarity.Rare,
                () =>
                {
                    clicked++;
                },
                () =>
                {
                    hovered++;
                },
                statIcon);

            Assert.AreEqual("Increase Damage by 5", label.text);
            Assert.AreSame(statIcon, iconImage.sprite);
            Assert.IsTrue(iconImage.enabled);
            Assert.AreSame(raritySprite, bgImage.sprite);
            Assert.AreEqual(Color.cyan, bgImage.color);
            Assert.IsTrue(promptView.HasBinding);

            upgradeButton.Invoke();
            Assert.AreEqual(1, clicked);

            PointerEnterHandler pointerHandler = button.GetComponent<PointerEnterHandler>();
            pointerHandler.OnPointerEnter(null);
            Assert.AreEqual(1, hovered);
        }

        [Test]
        public void SkillUpgradePresenter_NewSkillChoiceAndCardSelection_InitializesSkillAndReleasesInputScope()
        {
            SkillUpgradePresenterRig rig = CreateUpgradePresenterRig();
            FakeSkill saw = CreateFakeSkill("Saw", CreateTestSprite(), initialized: true);
            FakeSkill minigun = CreateFakeSkill("Minigun", CreateTestSprite(), initialized: false);
            FakeSkill landmine = CreateFakeSkill("Landmine", CreateTestSprite(), initialized: false);
            rig.Registry.SetSkills(saw, minigun, landmine);

            InvokeMethod(rig.Presenter, "Awake");
            InvokeMethod(rig.Presenter, "OnEnable");
            InvokeMethod(rig.Presenter, "Start");

            // Level 4 matches (4 - 1) % 3 == 0 -> queues NewSkillChoice.
            rig.LevelPresenter.RaiseLevelVisualReached(new LevelData(4, 0f, 100f));

            Assert.IsTrue(rig.NewSkillSection.activeSelf);
            Assert.IsFalse(rig.UpgradeSkillSection.activeSelf);
            Assert.AreEqual(InputContextScopeKind.LiveReward, rig.InputContexts.EffectiveContext);
            Assert.AreEqual(2, rig.InputContexts.EligibleSlots);
            Assert.AreEqual(SkillVisualContext.NewSkillUnlocked, rig.VisualPresenter.LastContext);
            Assert.IsTrue(rig.SecondSkillCard.activeSelf);

            // Click first card -> initializes chosen skill, closes section, releases reward scope.
            rig.FirstCardButton.onClick.Invoke();

            Assert.AreEqual(2, rig.Registry.InitializedSkillsCount);
            Assert.IsFalse(rig.NewSkillSection.activeSelf);
            Assert.AreEqual(InputContextScopeKind.None, rig.InputContexts.EffectiveContext);
            Assert.AreEqual(1, rig.InputContexts.ConsumeCount);
        }

        [Test]
        public void SkillUpgradePresenter_StatUpgradeAndAutoResolve_AppliesUpgradeAndCleansUpOnDestroy()
        {
            SkillUpgradePresenterRig rig = CreateUpgradePresenterRig();
            FakeSkill saw = CreateFakeSkill("Saw", CreateTestSprite(), initialized: true);
            rig.Registry.SetSkills(saw);

            InvokeMethod(rig.Presenter, "Awake");
            InvokeMethod(rig.Presenter, "OnEnable");
            InvokeMethod(rig.Presenter, "Start");

            // Collect skill crate -> queues UpgradeSkill and renders button.
            rig.DropNotifier.RaiseSkillCrateCollected();

            Assert.IsTrue(rig.UpgradeSkillSection.activeSelf);
            Assert.AreEqual(1, rig.ButtonsHolder.childCount);
            Assert.AreEqual(SkillVisualContext.StatUpgrade, rig.VisualPresenter.LastContext);

            // Input command selects slot 0 -> applies upgrade and closes section.
            rig.InputCommands.RaiseCommand(new InputCommand(
                InputCommandKind.UpgradeSelectChoice,
                InputActionId.UpgradeSelectChoice1,
                0,
                0,
                string.Empty,
                1u,
                Time.frameCount,
                1u,
                1u));

            Assert.Greater(saw.TestConfig.Damage.Value, 10);
            Assert.IsFalse(rig.UpgradeSkillSection.activeSelf);
            Assert.AreEqual(InputContextScopeKind.None, rig.InputContexts.EffectiveContext);

            // Auto-upgrade mode resolves queued rewards immediately without showing UI.
            int beforeAutoDamage = saw.TestConfig.Damage.Value;
            rig.Presenter.IsAutoUpgradeEnabled = true;
            rig.DropNotifier.RaiseSkillCrateCollected();
            Assert.Greater(saw.TestConfig.Damage.Value, beforeAutoDamage);
            Assert.IsFalse(rig.UpgradeSkillSection.activeSelf);

            InvokeMethod(rig.Presenter, "OnDisable");
            InvokeMethod(rig.Presenter, "OnDestroy");
            Assert.AreEqual(0, rig.DropNotifier.ListenerCount);
            Assert.AreEqual(0, rig.LevelPresenter.ListenerCount);
            Assert.AreEqual(0, rig.InputCommands.ListenerCount);
        }

        [Test]
        public void SkillUpgradePresenter_ConsecutiveStatUpgrades_DestroysPreviousButtonsCleanlyInEditMode()
        {
            SkillUpgradePresenterRig rig = CreateUpgradePresenterRig();
            FakeSkill saw = CreateFakeSkill("Saw", CreateTestSprite(), initialized: true);
            rig.Registry.SetSkills(saw);

            InvokeMethod(rig.Presenter, "Awake");
            InvokeMethod(rig.Presenter, "OnEnable");
            InvokeMethod(rig.Presenter, "Start");

            // Queue two stat upgrade popups and click through the first so DestroyAllButtons runs in EditMode.
            rig.DropNotifier.RaiseSkillCrateCollected();
            rig.DropNotifier.RaiseSkillCrateCollected();
            Assert.AreEqual(1, rig.ButtonsHolder.childCount);

            Button firstButton = rig.ButtonsHolder.GetChild(0).GetComponent<Button>();
            firstButton.onClick.Invoke();

            Assert.IsTrue(rig.UpgradeSkillSection.activeSelf);
            Assert.AreEqual(1, rig.ButtonsHolder.childCount);
            Assert.IsTrue(firstButton == null);

            SetPrivateField(typeof(SkillUpgradePresenter), rig.Presenter, "_lastHandledInputFrame", -1);
            Button secondButton = rig.ButtonsHolder.GetChild(0).GetComponent<Button>();
            secondButton.onClick.Invoke();
            Assert.IsFalse(rig.UpgradeSkillSection.activeSelf);
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

        private Container BuildContainer(Action<ContainerBuilder> configure)
        {
            ContainerBuilder builder = new ContainerBuilder().SetName("SkillsUITestContainer");
            configure?.Invoke(builder);
            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private Sprite CreateTestSprite()
        {
            Texture2D texture = new Texture2D(2, 2);
            _createdObjects.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            _createdObjects.Add(sprite);
            return sprite;
        }

        private SkillInfoSO CreateSkillInfo(string skillName, string description, Sprite icon)
        {
            SkillInfoSO info = TrackScriptableObject<SkillInfoSO>();
            SetPrivateField(typeof(SkillInfoSO), info, "<Name>k__BackingField", skillName);
            SetPrivateField(typeof(SkillInfoSO), info, "<Description>k__BackingField", description);
            SetPrivateField(typeof(SkillInfoSO), info, "<Icon>k__BackingField", icon);
            return info;
        }

        private SkillStatRowView CreateStatRowPrefab()
        {
            GameObject rowGo = TrackGameObject("StatRowPrefab", active: false);
            Image icon = CreateChild(rowGo.transform, "Icon").AddComponent<Image>();
            TextMeshProUGUI text = CreateChild(rowGo.transform, "Value").AddComponent<TextMeshProUGUI>();
            SkillStatRowView row = rowGo.AddComponent<SkillStatRowView>();
            SetPrivateField(typeof(SkillStatRowView), row, "_statIcon", icon);
            SetPrivateField(typeof(SkillStatRowView), row, "_valueText", text);
            return row;
        }

        private SkillStatsGroupView CreateGroupView(Transform parent, string name, SkillStatRowView rowPrefab)
        {
            GameObject groupGo = CreateChild(parent, name);
            Image skillIcon = CreateChild(groupGo.transform, "SkillIcon").AddComponent<Image>();
            TextMeshProUGUI skillName = CreateChild(groupGo.transform, "SkillName").AddComponent<TextMeshProUGUI>();
            RectTransform holder = CreateChild(groupGo.transform, "Holder").AddComponent<RectTransform>();
            SkillStatsGroupView group = groupGo.AddComponent<SkillStatsGroupView>();
            SetPrivateField(typeof(SkillStatsGroupView), group, "_skillIcon", skillIcon);
            SetPrivateField(typeof(SkillStatsGroupView), group, "_skillNameText", skillName);
            SetPrivateField(typeof(SkillStatsGroupView), group, "_rowsHolder", holder);
            SetPrivateField(typeof(SkillStatsGroupView), group, "_statRowPrefab", rowPrefab);
            InvokeMethod(group, "Awake");
            return group;
        }

        private InputPromptView CreateInputPromptView(Transform parent, string name)
        {
            GameObject promptGo = CreateChild(parent, name);
            Image icon = CreateChild(promptGo.transform, "Icon").AddComponent<Image>();
            TextMeshProUGUI fallback = CreateChild(promptGo.transform, "Fallback").AddComponent<TextMeshProUGUI>();
            InputPromptView view = promptGo.AddComponent<InputPromptView>();
            SetPrivateField(typeof(InputPromptView), view, "_iconImage", icon);
            SetPrivateField(typeof(InputPromptView), view, "_fallbackLabel", fallback);
            return view;
        }

        private SkillUpgradePresenterRig CreateUpgradePresenterRig()
        {
            GameObject root = TrackGameObject("SkillUpgradePresenter", active: true);
            GameObject upgradeSection = CreateChild(root.transform, "UpgradeSection");
            upgradeSection.SetActive(false);
            Transform buttonsHolder = CreateChild(upgradeSection.transform, "ButtonsHolder").transform;

            GameObject buttonPrefab = TrackGameObject("UpgradeButtonPrefab", active: false);
            Button btnComp = buttonPrefab.AddComponent<Button>();
            Image btnBg = buttonPrefab.AddComponent<Image>();
            btnComp.targetGraphic = btnBg;
            TextMeshProUGUI btnText = CreateChild(buttonPrefab.transform, "Label").AddComponent<TextMeshProUGUI>();
            Image btnIcon = CreateChild(buttonPrefab.transform, "StatIcon").AddComponent<Image>();
            InputPromptView btnPrompt = CreateInputPromptView(buttonPrefab.transform, "Prompt");
            SkillUpgradeButton upgradeButtonComp = buttonPrefab.AddComponent<SkillUpgradeButton>();
            SetPrivateField(typeof(SkillUpgradeButton), upgradeButtonComp, "_button", btnComp);
            SetPrivateField(typeof(SkillUpgradeButton), upgradeButtonComp, "_text", btnText);
            SetPrivateField(typeof(SkillUpgradeButton), upgradeButtonComp, "_statIconImage", btnIcon);
            SetPrivateField(typeof(SkillUpgradeButton), upgradeButtonComp, "_inputPromptView", btnPrompt);

            GameObject newSkillSection = CreateChild(root.transform, "NewSkillSection");
            newSkillSection.SetActive(false);
            GameObject firstCard = CreateChild(newSkillSection.transform, "FirstCard");
            Button firstButton = firstCard.AddComponent<Button>();
            InputPromptView firstPrompt = CreateInputPromptView(firstCard.transform, "FirstPrompt");
            TextMeshProUGUI firstName = CreateChild(firstCard.transform, "FirstName").AddComponent<TextMeshProUGUI>();
            TextMeshProUGUI firstDesc = CreateChild(firstCard.transform, "FirstDesc").AddComponent<TextMeshProUGUI>();

            GameObject secondCard = CreateChild(newSkillSection.transform, "SecondCard");
            Button secondButton = secondCard.AddComponent<Button>();
            InputPromptView secondPrompt = CreateInputPromptView(secondCard.transform, "SecondPrompt");
            TextMeshProUGUI secondName = CreateChild(secondCard.transform, "SecondName").AddComponent<TextMeshProUGUI>();
            TextMeshProUGUI secondDesc = CreateChild(secondCard.transform, "SecondDesc").AddComponent<TextMeshProUGUI>();

            SkillUpgradePresenter presenter = root.AddComponent<SkillUpgradePresenter>();
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_upgradeSkillSection", upgradeSection);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_upgradeButtonPrefab", buttonPrefab);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_buttonsHolder", buttonsHolder);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_newSkillSection", newSkillSection);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_firstSkillCard", firstCard);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_firstSkillCardButton", firstButton);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_firstSkillPromptView", firstPrompt);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_newSkillName", firstName);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_newSkillDescription", firstDesc);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_secondSkillCard", secondCard);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_secondSkillCardButton", secondButton);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_secondSkillPromptView", secondPrompt);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_secondSkillName", secondName);
            SetPrivateField(typeof(SkillUpgradePresenter), presenter, "_secondSkillDescription", secondDesc);

            FakeSkillsRegistry registry = new FakeSkillsRegistry();
            FakePlayerManager playerManager = new FakePlayerManager(registry);
            FakePlayerLevelPresenter levelPresenter = new FakePlayerLevelPresenter();
            FakeCollectibleDropNotifier dropNotifier = new FakeCollectibleDropNotifier();
            SkillUpgradeFlow flow = new SkillUpgradeFlow();
            FakeSkillsVisualPresenter visualPresenter = new FakeSkillsVisualPresenter();
            FakeInputCommands inputCommands = new FakeInputCommands();
            FakeInputContexts inputContexts = new FakeInputContexts();
            FakeInputPromptProvider promptProvider = new FakeInputPromptProvider("1");

            Container container = BuildContainer((builder) =>
            {
                builder.AddSingleton(playerManager, typeof(IPlayerManager));
                builder.AddSingleton(levelPresenter, typeof(IPlayerLevelPresenter));
                builder.AddSingleton(dropNotifier, typeof(ICollectibleDropNotifier));
                builder.AddSingleton(flow, typeof(ISkillUpgradeFlow));
                builder.AddSingleton(visualPresenter, typeof(ISkillsVisualPresenter));
                builder.AddSingleton(inputCommands, typeof(IInputCommands));
                builder.AddSingleton(inputContexts, typeof(IInputContexts));
                builder.AddSingleton(promptProvider, typeof(IInputPromptProvider));
            });
            GameObjectInjector.InjectRecursive(root, container);

            return new SkillUpgradePresenterRig
            {
                Presenter = presenter,
                Registry = registry,
                LevelPresenter = levelPresenter,
                DropNotifier = dropNotifier,
                VisualPresenter = visualPresenter,
                InputCommands = inputCommands,
                InputContexts = inputContexts,
                NewSkillSection = newSkillSection,
                UpgradeSkillSection = upgradeSection,
                ButtonsHolder = buttonsHolder,
                FirstCardButton = firstButton,
                SecondSkillCard = secondCard
            };
        }

        private FakeSkill CreateFakeSkill(string skillName, Sprite icon, bool initialized)
        {
            GameObject go = TrackGameObject($"Skill_{skillName}", active: initialized);
            FakeSkill skill = go.AddComponent<FakeSkill>();
            FakeSkillConfig config = TrackScriptableObject<FakeSkillConfig>();
            config.Damage = CreateIntStat(10, 50, 5, 5, StatsUnits.None);
            SkillInfoSO info = CreateSkillInfo(skillName, $"{skillName} description", icon);
            skill.Setup(info, config, initialized);
            return skill;
        }

        private static FloatUpgradeableStat CreateFloatStat(float min, float max, float upgradeMin, float upgradeMax, StatsUnits unit)
        {
            FloatUpgradeableStat stat = new FloatUpgradeableStat(
                min,
                max,
                new FloatValueRange(min, max),
                new FloatValueRange(upgradeMin, upgradeMax),
                false);
            stat.RestoreRuntimeState(min, true);
            typeof(UpgradeableStat<float>).GetProperty(nameof(FloatUpgradeableStat.Unit)).SetValue(stat, unit);
            return stat;
        }

        private static IntUpgradeableStat CreateIntStat(int min, int max, int upgradeMin, int upgradeMax, StatsUnits unit)
        {
            IntUpgradeableStat stat = new IntUpgradeableStat(
                min,
                max,
                new IntValueRange(min, max),
                new IntValueRange(upgradeMin, upgradeMax),
                false);
            stat.RestoreRuntimeState(min, true);
            typeof(UpgradeableStat<int>).GetProperty(nameof(IntUpgradeableStat.Unit)).SetValue(stat, unit);
            return stat;
        }

        private static void StepCoroutineToCompletion(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            IEnumerator routine = (IEnumerator)method.Invoke(target, null);
            while (routine.MoveNext())
            {
            }
        }

        private static void InvokeMethod(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            method.Invoke(target, args);
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

        private sealed class SkillUpgradePresenterRig
        {
            public SkillUpgradePresenter Presenter { get; set; }
            public FakeSkillsRegistry Registry { get; set; }
            public FakePlayerLevelPresenter LevelPresenter { get; set; }
            public FakeCollectibleDropNotifier DropNotifier { get; set; }
            public FakeSkillsVisualPresenter VisualPresenter { get; set; }
            public FakeInputCommands InputCommands { get; set; }
            public FakeInputContexts InputContexts { get; set; }
            public GameObject NewSkillSection { get; set; }
            public GameObject UpgradeSkillSection { get; set; }
            public Transform ButtonsHolder { get; set; }
            public Button FirstCardButton { get; set; }
            public GameObject SecondSkillCard { get; set; }
        }

        private sealed class FakeSkillConfig : SkillUpgradeableStatsConfig
        {
            public IntUpgradeableStat Damage { get; set; }

            public override void ResetRuntimeState()
            {
            }

            public override void AppendStatsForDisplay(List<NameUpgradableStatPair> destination)
            {
                if (Damage != null)
                {
                    destination.Add(new NameUpgradableStatPair(nameof(Damage), Damage, null));
                }
            }
        }

        private sealed class FakeSkill : MonoBehaviour, IUpgradeableSkill
        {
            private bool _initialized;

            public SkillInfoSO SkillInfo { get; private set; }
            public ISkillUpgradeableStatsConfig Config { get { return TestConfig; } }
            public FakeSkillConfig TestConfig { get; private set; }

            public void Setup(SkillInfoSO info, FakeSkillConfig config, bool initialized)
            {
                SkillInfo = info;
                TestConfig = config;
                _initialized = initialized;
            }

            public void Initialize()
            {
                _initialized = true;
            }

            public bool IsInitialized()
            {
                return _initialized;
            }

            public bool CanBeUpgraded()
            {
                return TestConfig != null && TestConfig.Damage != null && TestConfig.Damage.CanBeUpgraded;
            }
        }

        private sealed class FakeSkillsRegistry : ISkillsRegistry
        {
            private readonly List<ISkillBase> _skills = new();
            private event Action<ISkillBase> _onSkillInitialized;

            public int ListenerCount { get; private set; }
            public IReadOnlyList<ISkillBase> Skills { get { return _skills; } }
            public int UninitializedSkillsCount { get { return GetUninitializedSkills().Count; } }
            public int InitializedSkillsCount { get { return GetInitializedSkills().Count; } }

            public event Action<ISkillBase> OnSkillInitialized
            {
                add
                {
                    _onSkillInitialized += value;
                    ListenerCount++;
                }
                remove
                {
                    _onSkillInitialized -= value;
                    if (ListenerCount > 0)
                    {
                        ListenerCount--;
                    }
                }
            }

            public void SetSkills(params ISkillBase[] skills)
            {
                _skills.Clear();
                for (int i = 0; i < skills.Length; i++)
                {
                    _skills.Add(skills[i]);
                }
            }

            public IReadOnlyList<ISkillBase> GetInitializedSkills()
            {
                List<ISkillBase> result = new();
                for (int i = 0; i < _skills.Count; i++)
                {
                    if (_skills[i].IsInitialized())
                    {
                        result.Add(_skills[i]);
                    }
                }

                return result;
            }

            public IReadOnlyList<ISkillBase> GetUninitializedSkills()
            {
                List<ISkillBase> result = new();
                for (int i = 0; i < _skills.Count; i++)
                {
                    if (!_skills[i].IsInitialized())
                    {
                        result.Add(_skills[i]);
                    }
                }

                return result;
            }

            public ISkillBase InitializeSkill(ISkillBase skill)
            {
                if (skill != null && !skill.IsInitialized())
                {
                    skill.Initialize();
                    _onSkillInitialized?.Invoke(skill);
                    return skill;
                }

                return null;
            }

            public void RaiseInitialized(ISkillBase skill)
            {
                _onSkillInitialized?.Invoke(skill);
            }
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            public FakePlayerManager(ISkillsRegistry skillsRegistry)
            {
                SkillsRegistry = skillsRegistry;
            }

            public IAudioClipPlayer AudioClipPlayer { get { return null; } }
            public ICarController CarController { get { return null; } }
            public ICarVfxEffectsController CarVfxEffectsController { get { return null; } }
            public ILevelController LevelController { get { return null; } }
            public ISkillsRegistry SkillsRegistry { get; }
            public IPlayerDamagedHandler DamagedHandler { get { return null; } }
            public IHealth Health { get { return null; } }
            public GameObject GameObject { get { return null; } }
        }

        private sealed class FakeGameSceneLoader : IGameSceneLoader
        {
            public bool IsLoading { get { return false; } }
            public GameScene CurrentLoadedScene { get { return GameScene.RuinedBloodCity; } }

            public event EventHandler<ValueEventArgs<GameScene>> OnSceneLoaded { add { } remove { } }
            public event EventHandler<ValueEventArgs<GameScene>> OnStartLoadingScene;

            public void RaiseStartLoadingScene(GameScene scene)
            {
                OnStartLoadingScene?.Invoke(this, new ValueEventArgs<GameScene>(scene));
            }

            public AsyncOperation LoadNewSceneAsync(GameScene scene)
            {
                return null;
            }

            public AsyncOperation ReloadCurrentSceneAsync()
            {
                return null;
            }
        }

        private sealed class FakePlayerLevelPresenter : IPlayerLevelPresenter
        {
            private event EventHandler<ValueEventArgs<LevelData>> _onReached;
            public int ListenerCount { get; private set; }

            public event EventHandler<ValueEventArgs<LevelData>> OnExpSliderVisualEndValueReached
            {
                add
                {
                    _onReached += value;
                    ListenerCount++;
                }
                remove
                {
                    _onReached -= value;
                    if (ListenerCount > 0)
                    {
                        ListenerCount--;
                    }
                }
            }

            public void RaiseLevelVisualReached(LevelData data)
            {
                _onReached?.Invoke(this, new ValueEventArgs<LevelData>(data));
            }

            public void FastForward()
            {
            }
        }

        private sealed class FakeCollectibleDropNotifier : ICollectibleDropNotifier
        {
            private event EventHandler _onCollected;
            public int ListenerCount { get; private set; }

            public event EventHandler OnSkillUpgradeCollectibleCollected
            {
                add
                {
                    _onCollected += value;
                    ListenerCount++;
                }
                remove
                {
                    _onCollected -= value;
                    if (ListenerCount > 0)
                    {
                        ListenerCount--;
                    }
                }
            }

            public event Action<PowerUpType> OnPowerUpCollected { add { } remove { } }

            public void RaiseSkillCrateCollected()
            {
                _onCollected?.Invoke(this, EventArgs.Empty);
            }

            public void SpawnCollectible(GameObject prefab, Vector3 spawnPos, Vector3 targetPos)
            {
            }
        }

        private sealed class FakeSkillsVisualPresenter : ISkillsVisualPresenter
        {
            public SkillVisualContext LastContext { get; private set; }

            public void SetContext(SkillVisualContext context)
            {
                LastContext = context;
            }

            public void ShowSkillVisual(SkillInfoSO skillInfoSO, int slotIndex = 0)
            {
            }

            public void HideAll()
            {
            }
        }

        private sealed class FakeInputCommands : IInputCommands
        {
            private event Action<InputCommand> _onCommand;
            public int ListenerCount { get; private set; }

            public event Action<InputCommand> OnCommand
            {
                add
                {
                    _onCommand += value;
                    ListenerCount++;
                }
                remove
                {
                    _onCommand -= value;
                    if (ListenerCount > 0)
                    {
                        ListenerCount--;
                    }
                }
            }

            public void RaiseCommand(InputCommand command)
            {
                _onCommand?.Invoke(command);
            }
        }

        private sealed class FakeInputContexts : IInputContexts
        {
            private InputScopeToken _activeToken;

            public InputContextScopeKind EffectiveContext { get; private set; }
            public uint SceneEpoch { get { return 1u; } }
            public uint ContextGeneration { get { return 1u; } }
            public bool IsUiNavigationEligible { get { return false; } }
            public bool IsRewardChoiceEligible { get { return EffectiveContext == InputContextScopeKind.LiveReward; } }
            public bool IsBackEligible { get { return false; } }
            public bool IsGameplayEligible { get { return true; } }
            public bool IsInteractEligible { get { return true; } }
            public int EligibleSlots { get; private set; }
            public int ConsumeCount { get; private set; }

            public event Action OnContextChanged { add { } remove { } }

            public void RegisterSceneReady()
            {
            }

            public InputScopeToken AcquireScope(InputContextScopeKind kind, int ownerId)
            {
                EffectiveContext = kind;
                _activeToken = new InputScopeToken(1u, 1u, kind, ownerId);
                return _activeToken;
            }

            public void ReleaseScope(InputScopeToken token)
            {
                if (token.Equals(_activeToken))
                {
                    EffectiveContext = InputContextScopeKind.None;
                    _activeToken = default;
                }
            }

            public void SetLiveRewardEligibleSlots(int slotCount)
            {
                EligibleSlots = slotCount;
            }

            public bool IsSlotEligible(int slotIndex)
            {
                return slotIndex >= 0 && slotIndex < EligibleSlots;
            }

            public void ConsumeActiveControlsUntilRelease()
            {
                ConsumeCount++;
            }

            public void ReapplyPolicy()
            {
            }
        }

        private sealed class FakeInputPromptProvider : IInputPromptProvider
        {
            private readonly string _label;

            public FakeInputPromptProvider(string label)
            {
                _label = label;
            }

            public event Action OnPromptsChanged { add { } remove { } }

            public InputPromptDescriptor GetPrompt(InputPromptRequest request)
            {
                return new InputPromptDescriptor(
                    isValid: true,
                    isBound: true,
                    isAvailable: true,
                    family: InputDeviceFamily.KeyboardMouse,
                    primarySprite: null,
                    primaryLabel: _label,
                    controlPath: "<Keyboard>/1");
            }
        }
    }
}
