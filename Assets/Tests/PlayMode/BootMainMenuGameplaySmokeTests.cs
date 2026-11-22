using System;
using System.Collections;
using System.Collections.Generic;
using Assets.ScriptableObjects.Skills.PlayerSkills.LandmineSkill;
using Assets.ScriptableObjects.Skills.PlayerSkills.MinigunSkill;
using Assets.ScriptableObjects.Skills.PlayerSkills.SawSkill;
using Assets.Scripts.Audio;
using Assets.Scripts.DamageNumbers;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Barrel;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Enemies.Bosses;
using Assets.Scripts.GameFlow;
using Assets.Scripts.GameFlow.Config;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Interactables;
using Assets.Scripts.LayerMasks.Constants;
using Assets.Scripts.LevelSystem;
using Assets.Scripts.Navigation.GridSystem;
using Assets.Scripts.Player;
using Assets.Scripts.PowerUps.Freeze;
using Assets.Scripts.Skills;
using Assets.Scripts.Spawners.Enemies;
using Assets.Scripts.Spawners.WorldSpace;
using Assets.Scripts.Stats;
using Assets.Scripts.UI.Level;
using Assets.Scripts.UI.Loading;
using Assets.Scripts.UI.Skills;
using Assets.Scripts.Waves;
using Reflex.Core;
using Reflex.Extensions;
using Reflex.Injectors;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using NavigationGrid = Assets.Scripts.Navigation.GridSystem.Grid;

namespace Assets.Tests.PlayMode
{
    public class BootMainMenuGameplaySmokeTests
    {
        private const float SCENE_TRANSITION_TIMEOUT = 12f;

        private readonly PlayModeTestHarness _harness = new PlayModeTestHarness();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Container.ProjectContainer != null)
            {
                if (Container.ProjectContainer.HasBinding<IRunSessionService>())
                {
                    Container.ProjectContainer.Resolve<IRunSessionService>().ResetSession();
                }

                if (Container.ProjectContainer.HasBinding<IGameTimeService>())
                {
                    Container.ProjectContainer.Resolve<IGameTimeService>().Reset();
                }
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && (active.name == "MainMenu" || active.name == "RuinedBloodCity" || active.name == "Boot"))
            {
                Scene clean = SceneManager.CreateScene("StageBTeardownClean_" + Guid.NewGuid().ToString("N"));
                SceneManager.SetActiveScene(clean);
                AsyncOperation unload = SceneManager.UnloadSceneAsync(active);
                if (unload != null)
                {
                    while (!unload.isDone)
                    {
                        yield return null;
                    }
                }
            }

            yield return _harness.CleanupAsync();
        }

        [UnityTest]
        [Order(1)]
        public IEnumerator BootScene_LoadsProjectScopeAndTransitionsToMainMenuWithValidBindings()
        {
            yield return EnsureBootedIntoMainMenuCoroutine();

            Scene activeScene = SceneManager.GetActiveScene();
            Assert.AreEqual("MainMenu", activeScene.name);
            Assert.IsNotNull(Container.ProjectContainer);
            Assert.IsTrue(Container.ProjectContainer.HasBinding<IGameSceneLoader>());
            Assert.IsTrue(Container.ProjectContainer.HasBinding<IRunSessionService>());
            Assert.IsTrue(Container.ProjectContainer.HasBinding<IGameTimeService>());
            Container menuContainer = activeScene.GetSceneContainer();
            Assert.IsNotNull(menuContainer);
            Assert.IsTrue(menuContainer.HasBinding<ILoadingOverlayPresenter>());
        }

        [UnityTest]
        [Order(2)]
        public IEnumerator RuinedBloodCity_Tier1InitializationSpawnsWaveAndProcessesPlayerExpCleanly()
        {
            yield return EnsureBootedIntoGameplayCoroutine();

            Scene gameplayScene = SceneManager.GetActiveScene();
            Assert.AreEqual("RuinedBloodCity", gameplayScene.name);
            Container sceneContainer = gameplayScene.GetSceneContainer();
            Assert.IsNotNull(sceneContainer);

            IGridManager gridManager = sceneContainer.Resolve<IGridManager>();
            IPlayerManager playerManager = sceneContainer.Resolve<IPlayerManager>();
            EnemiesSpawner enemiesSpawner = PlayModeTestHarness.FindInScene<EnemiesSpawner>(gameplayScene);
            WaveManager waveManager = PlayModeTestHarness.FindInScene<WaveManager>(gameplayScene);

            Assert.IsNotNull(gridManager);
            Assert.IsNotNull(gridManager.WorldGrid);
            Assert.Greater(gridManager.WorldGrid.Width, 0);
            Assert.Greater(gridManager.WorldGrid.Height, 0);
            Assert.IsNotNull(gridManager.GridPlayerChunk);
            Assert.IsNotNull(playerManager);
            Assert.IsNotNull(enemiesSpawner);
            Assert.IsNotNull(waveManager);

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => enemiesSpawner.CurrentlySpawnedObjectsCount > 0u,
                8f,
                "Expected WaveManager in RuinedBloodCity to spawn at least one wave enemy into EnemiesSpawner.");

            byte startingLevel = playerManager.LevelController.LevelData.Lvl;
            float neededToLevel = playerManager.LevelController.LevelData.MaxExp - playerManager.LevelController.LevelData.Exp;
            playerManager.LevelController.AddExp(neededToLevel + 1f);

            Assert.Greater(playerManager.LevelController.LevelData.Lvl, startingLevel);
        }

        [UnityTest]
        [Order(3)]
        public IEnumerator RuinedBloodCity_Tier1ToTier2PortalTransition_AdvancesTierAndRestoresPlayerProgression()
        {
            yield return EnsureBootedIntoGameplayCoroutine();

            IRunSessionService runSession = Container.ProjectContainer.Resolve<IRunSessionService>();
            Assert.AreEqual(0, runSession.CurrentTierIndex);
            Assert.IsFalse(runSession.IsFinalTier);

            Scene tier1Scene = SceneManager.GetActiveScene();
            Container tier1Container = tier1Scene.GetSceneContainer();
            IPlayerManager tier1Player = tier1Container.Resolve<IPlayerManager>();
            IBossEncounterService bossService = tier1Container.Resolve<IBossEncounterService>();

            float expToLevel = tier1Player.LevelController.LevelData.MaxExp - tier1Player.LevelController.LevelData.Exp;
            tier1Player.LevelController.AddExp(expToLevel + 5f);
            byte expectedRestoredLevel = tier1Player.LevelController.LevelData.Lvl;
            Assert.GreaterOrEqual(expectedRestoredLevel, (byte)2);

            if (tier1Container.HasBinding<IPlayerLevelPresenter>())
            {
                tier1Container.Resolve<IPlayerLevelPresenter>().FastForward();
            }

            if (tier1Container.HasBinding<ISkillUpgradePresenter>())
            {
                tier1Container.Resolve<ISkillUpgradePresenter>().HideForStageTransition();
            }

            Container.ProjectContainer.Resolve<IGameTimeService>().Reset();

            IUpgradeableSkill firstUpgradeableSkill = FindFirstInitializedUpgradeableSkill(tier1Player);
            Assert.IsNotNull(firstUpgradeableSkill, "Expected starting initialized upgradeable skill on Tier 1 player.");

            IUpgradeableStat firstStat = FindFirstUpgradeableStat(firstUpgradeableSkill);
            double expectedStatValue = 0d;
            string expectedStatName = null;
            if (firstStat != null)
            {
                float delta = firstStat.GetUpgradeValueBasedOnUpdateRange();
                if (!Mathf.Approximately(delta, 0f))
                {
                    firstStat.Upgrade(delta);
                }

                expectedStatValue = firstStat.CurrentValue;
                expectedStatName = FindFirstUpgradeableStatName(firstUpgradeableSkill);
            }

            Vector3 spawnPortalAnchor = tier1Player.GameObject.transform.position + Vector3.forward * 12f;
            bossService.SpawnGolemBoss(spawnPortalAnchor);

            IHealth golemHealth = FindActiveGolemHealth(tier1Scene);
            Assert.IsNotNull(golemHealth);
            golemHealth.DecreaseHealth(golemHealth.MaxHealth + 100000f);

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => PlayModeTestHarness.FindInScene<NextStagePortal>(tier1Scene) != null,
                6f,
                "Expected defeating GolemBoss in Tier 1 to spawn NextStagePortal.");

            NextStagePortal portal = PlayModeTestHarness.FindInScene<NextStagePortal>(tier1Scene);
            Rigidbody playerRb = tier1Player.GameObject.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.position = portal.transform.position;
            }
            tier1Player.GameObject.transform.position = portal.transform.position;

            IGameSceneLoader sceneLoader = Container.ProjectContainer.Resolve<IGameSceneLoader>();
            sceneLoader.OnStartLoadingScene += HandleStartLoadingTier2;

            void HandleStartLoadingTier2(object sender, Assets.Scripts.Common.EventArgs.ValueEventArgs<GameScene> args)
            {
                expectedRestoredLevel = tier1Player.LevelController.LevelData.Lvl;
                sceneLoader.OnStartLoadingScene -= HandleStartLoadingTier2;
            }

            try
            {
                yield return PlayModeTestHarness.WaitUntilWithTimeout(
                    () => runSession.CurrentTierIndex == 1 && !sceneLoader.IsLoading && SceneManager.GetActiveScene() != tier1Scene,
                    SCENE_TRANSITION_TIMEOUT,
                    "Expected NextStagePortal vortex and scene transition to load Tier 2 cleanly.");
            }
            finally
            {
                sceneLoader.OnStartLoadingScene -= HandleStartLoadingTier2;
            }

            yield return null;
            yield return null;

            Scene tier2Scene = SceneManager.GetActiveScene();
            Assert.AreEqual("RuinedBloodCity", tier2Scene.name);
            Assert.AreEqual(1, runSession.CurrentTierIndex);
            Container tier2Container = tier2Scene.GetSceneContainer();
            Assert.IsNotNull(tier2Container);

            IPlayerManager tier2Player = tier2Container.Resolve<IPlayerManager>();
            Assert.AreEqual(expectedRestoredLevel, tier2Player.LevelController.LevelData.Lvl,
                "Tier 2 player should restore carried-over level from Tier 1.");

            if (!string.IsNullOrEmpty(expectedStatName))
            {
                IUpgradeableStat restoredStat = FindNamedStatOnSkill(
                    tier2Player,
                    firstUpgradeableSkill.SkillInfo.Name,
                    expectedStatName);
                Assert.IsNotNull(restoredStat);
                Assert.AreEqual(expectedStatValue, restoredStat.CurrentValue, 0.001d);
            }
        }

        private IEnumerator EnsureBootedIntoMainMenuCoroutine()
        {
            if (SceneScope.OnSceneContainerBuilding == null)
            {
                AsyncOperation bootLoad = SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
                Assert.IsNotNull(bootLoad, "Failed to start Boot scene async load.");
                while (!bootLoad.isDone)
                {
                    yield return null;
                }

                yield return PlayModeTestHarness.WaitUntilWithTimeout(
                    () => SceneManager.GetActiveScene().name == "MainMenu",
                    SCENE_TRANSITION_TIMEOUT,
                    "Expected BootLoader to transition automatically from Boot to MainMenu.");

                yield return null;
            }
            else if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                IGameSceneLoader loader = Container.ProjectContainer.Resolve<IGameSceneLoader>();
                loader.LoadNewSceneAsync(GameScene.MainMenu);
                yield return PlayModeTestHarness.WaitUntilWithTimeout(
                    () => !loader.IsLoading && SceneManager.GetActiveScene().name == "MainMenu",
                    SCENE_TRANSITION_TIMEOUT,
                    "Expected GameSceneLoader to load MainMenu.");
                yield return null;
            }
        }

        private IEnumerator EnsureBootedIntoGameplayCoroutine()
        {
            yield return EnsureBootedIntoMainMenuCoroutine();

            IRunSessionService runSession = Container.ProjectContainer.Resolve<IRunSessionService>();
            runSession.ResetSession();
            IGameTimeService timeService = Container.ProjectContainer.Resolve<IGameTimeService>();
            timeService.Reset();

            IGameSceneLoader loader = Container.ProjectContainer.Resolve<IGameSceneLoader>();
            loader.LoadNewSceneAsync(GameScene.RuinedBloodCity);
            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => !loader.IsLoading && SceneManager.GetActiveScene().name == "RuinedBloodCity",
                SCENE_TRANSITION_TIMEOUT,
                "Expected GameSceneLoader to load RuinedBloodCity and bind SceneScope.");

            yield return null;
            yield return null;
        }

        private static IHealth FindActiveGolemHealth(Scene scene)
        {
            List<Assets.Scripts.Enemies.Bosses.Golem.GolemBoss> golems = new List<Assets.Scripts.Enemies.Bosses.Golem.GolemBoss>();
            PlayModeTestHarness.FindAllInScene(scene, golems);
            for (int i = 0; i < golems.Count; i++)
            {
                if (golems[i].IsActive && golems[i].Health != null)
                {
                    return golems[i].Health;
                }
            }

            return null;
        }

        private static IUpgradeableSkill FindFirstInitializedUpgradeableSkill(IPlayerManager player)
        {
            IReadOnlyList<ISkillBase> initialized = player.SkillsRegistry.GetInitializedSkills();
            for (int i = 0; i < initialized.Count; i++)
            {
                if (initialized[i] is IUpgradeableSkill upgradeable)
                {
                    return upgradeable;
                }
            }

            return null;
        }

        private static IUpgradeableStat FindFirstUpgradeableStat(IUpgradeableSkill skill)
        {
            foreach (var pair in skill.Config.GetUpgradeableStatsThatCanBeUpgraded())
            {
                return pair.UpgradeableStat;
            }

            return null;
        }

        private static string FindFirstUpgradeableStatName(IUpgradeableSkill skill)
        {
            foreach (var pair in skill.Config.GetUpgradeableStatsThatCanBeUpgraded())
            {
                return pair.Name;
            }

            return null;
        }

        private static IUpgradeableStat FindNamedStatOnSkill(IPlayerManager player, string skillName, string statName)
        {
            IReadOnlyList<ISkillBase> initialized = player.SkillsRegistry.GetInitializedSkills();
            for (int i = 0; i < initialized.Count; i++)
            {
                if (initialized[i] is IUpgradeableSkill upgradeable
                    && upgradeable.SkillInfo.Name == skillName)
                {
                    var list = new List<Assets.ScriptableObjects.Player.Skills.NameUpgradableStatPair>();
                    upgradeable.Config.AppendStatsForDisplay(list);
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (list[j].Name == statName)
                        {
                            return list[j].UpgradeableStat;
                        }
                    }
                }
            }

            return null;
        }
    }
}
