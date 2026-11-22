using System.Collections.Generic;
using Assets.Scripts.Skills;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using Assets.Scripts.Skills.PlayerSkills.LandmineTrap;
using Assets.Scripts.Skills.PlayerSkills.Lasergun;
using Assets.Scripts.Skills.PlayerSkills.Minigun;
using Assets.Scripts.Skills.PlayerSkills.Saw;
using Assets.Scripts.UI.Skills;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class SkillAssetsIntegrityTests
    {
        private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/Player/Player.prefab";
        private const string SAW_PREFAB_PATH = "Assets/Prefabs/Skills/Saw/Saw.prefab";
        private const string MINIGUN_TURRET_PREFAB_PATH = "Assets/Prefabs/Skills/Minigun/MinigunTurret.prefab";
        private const string LASERGUN_TURRET_PREFAB_PATH = "Assets/Prefabs/Skills/Lasergun/LasergunTurret.prefab";
        private const string LANDMINE_PREFAB_PATH = "Assets/Prefabs/Skills/Landmine/Landmine.prefab";
        private const string SKILL_CRATE_PREFAB_PATH = "Assets/Prefabs/Collectibles/SkilCrate.prefab";
        private const string SKILL_UPGRADE_PRESENTER_PREFAB_PATH = "Assets/Prefabs/UI/SkillsUpgrader/SkillUpgradePresenter.prefab";
        private const string UPGRADE_BUTTON_PREFAB_PATH = "Assets/Prefabs/UI/SkillsUpgrader/UpgradeButton.prefab";
        private const string SKILL_STAT_ROW_PREFAB_PATH = "Assets/Prefabs/UI/SkillStatsPresenter/SkillStatRow.prefab";

        [Test]
        public void PlayerPrefab_SkillsRegistry_HasFourDirectUpgradeableSkillChildrenWithConfigsAndIcons()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            Assert.IsNotNull(playerPrefab, $"Expected prefab at '{PLAYER_PREFAB_PATH}'.");

            SkillsRegistry registry = playerPrefab.GetComponentInChildren<SkillsRegistry>(true);
            Assert.IsNotNull(registry, "Player prefab must contain SkillsRegistry.");

            List<ISkillBase> directSkills = new List<ISkillBase>();
            Transform registryTransform = registry.transform;
            for (int i = 0; i < registryTransform.childCount; i++)
            {
                Transform child = registryTransform.GetChild(i);
                if (child.TryGetComponent(out ISkillBase skill))
                {
                    directSkills.Add(skill);
                }
            }

            Assert.AreEqual(4, directSkills.Count, "Expected 4 direct child skills under SkillsRegistry.");

            bool hasSaw = false;
            bool hasMinigun = false;
            bool hasLandmine = false;
            bool hasLasergun = false;

            for (int i = 0; i < directSkills.Count; i++)
            {
                ISkillBase skill = directSkills[i];
                Assert.IsNotNull(skill.SkillInfo, $"Skill at index {i} is missing SkillInfoSO.");
                Assert.IsFalse(string.IsNullOrEmpty(skill.SkillInfo.Name), $"Skill at index {i} is missing SkillInfo.Name.");
                Assert.IsNotNull(skill.SkillInfo.Icon, $"Skill '{skill.SkillInfo.Name}' is missing SkillInfo.Icon.");

                IUpgradeableSkill upgradeable = skill as IUpgradeableSkill;
                Assert.IsNotNull(upgradeable, $"Skill '{skill.SkillInfo.Name}' must implement IUpgradeableSkill.");
                Assert.IsNotNull(upgradeable.Config, $"Skill '{skill.SkillInfo.Name}' is missing Config.");

                if (skill is SawSkill)
                {
                    hasSaw = true;
                }
                else if (skill is MinigunSkill)
                {
                    hasMinigun = true;
                }
                else if (skill is LandmineSkill)
                {
                    hasLandmine = true;
                }
                else if (skill is LasergunSkill)
                {
                    hasLasergun = true;
                }
            }

            Assert.IsTrue(hasSaw, "Player SkillsRegistry is missing SawSkill.");
            Assert.IsTrue(hasMinigun, "Player SkillsRegistry is missing MinigunSkill.");
            Assert.IsTrue(hasLandmine, "Player SkillsRegistry is missing LandmineSkill.");
            Assert.IsTrue(hasLasergun, "Player SkillsRegistry is missing LasergunSkill.");
        }

        [Test]
        public void AuthoredSkillAndUiPrefabs_LoadWithExpectedComponentsAndZeroMissingScripts()
        {
            AssertPrefabHasComponent<SawBlade>(SAW_PREFAB_PATH);
            AssertPrefabHasComponent<MinigunTurret>(MINIGUN_TURRET_PREFAB_PATH);
            AssertPrefabHasComponent<LasergunTurret>(LASERGUN_TURRET_PREFAB_PATH);
            AssertPrefabHasComponent<Landmine>(LANDMINE_PREFAB_PATH);
            AssertPrefabHasComponent<SkillCrate>(SKILL_CRATE_PREFAB_PATH);
            AssertPrefabHasComponent<SkillUpgradePresenter>(SKILL_UPGRADE_PRESENTER_PREFAB_PATH);
            AssertPrefabHasComponent<SkillUpgradeButton>(UPGRADE_BUTTON_PREFAB_PATH);
            AssertPrefabHasComponent<SkillStatRowView>(SKILL_STAT_ROW_PREFAB_PATH);
        }

        private static void AssertPrefabHasComponent<TComponent>(string prefabPath) where TComponent : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, $"Expected prefab at '{prefabPath}'.");

            TComponent component = prefab.GetComponentInChildren<TComponent>(true);
            Assert.IsNotNull(component, $"Prefab '{prefabPath}' is missing component '{typeof(TComponent).Name}'.");

            int missingScripts = CountMissingScriptsRecursive(prefab.transform);
            Assert.AreEqual(0, missingScripts, $"Prefab '{prefabPath}' contains missing MonoBehaviour script references.");
        }

        private static int CountMissingScriptsRecursive(Transform root)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root.gameObject);
            for (int i = 0; i < root.childCount; i++)
            {
                count += CountMissingScriptsRecursive(root.GetChild(i));
            }

            return count;
        }
    }
}
