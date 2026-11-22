using System;
using System.Reflection;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Base;
using Assets.Scripts.Pooling;
using Assets.Scripts.PowerUps;
using Assets.Scripts.PowerUps.Collectibles;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class CollectiblesAssetsIntegrityTests
    {
        private const string SKILL_CRATE_PREFAB_PATH = "Assets/Prefabs/Collectibles/SkilCrate.prefab";
        private const string DROP_ANIMATION_CONFIG_PATH = "Assets/ScriptableObjects/Collectibles/DropAnimationConfiguration.asset";
        private const string COLLECTIBLES_LAYER_NAME = "Collectibles";
        private const float TOLERANCE = 0.0001f;

        [Test]
        public void SkillCratePrefab_HasTriggerColliderCollectiblesLayerSkillCrateComponentAndNoSelfInjector()
        {
            // Arrange
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SKILL_CRATE_PREFAB_PATH);

            // Assert
            AssertCommonCollectiblePrefabContracts(prefab);
            SkillCrate crate = prefab.GetComponent<SkillCrate>();
            Assert.IsNotNull(crate);
            Assert.IsInstanceOf<ISkillUpgradeCollectible>(crate);
            Assert.IsInstanceOf<IPoolable>(crate);
        }

        [TestCase("Assets/Prefabs/Collectibles/Nuke.prefab", PowerUpType.Nuke, TestName = "PowerUpPrefab_Nuke_HasValidWiringAndType")]
        [TestCase("Assets/Prefabs/Collectibles/FreezeTimer.prefab", PowerUpType.FreezeTimer, TestName = "PowerUpPrefab_FreezeTimer_HasValidWiringAndType")]
        [TestCase("Assets/Prefabs/Collectibles/BubbleShield.prefab", PowerUpType.BubbleShield, TestName = "PowerUpPrefab_BubbleShield_HasValidWiringAndType")]
        [TestCase("Assets/Prefabs/Collectibles/Armor.prefab", PowerUpType.Armor, TestName = "PowerUpPrefab_Armor_HasValidWiringAndType")]
        [TestCase("Assets/Prefabs/Collectibles/FireTails.prefab", PowerUpType.FireTails, TestName = "PowerUpPrefab_FireTails_HasValidWiringAndType")]
        public void PowerUpPrefab_HasTriggerColliderCollectiblesLayerAndExpectedPowerUpType(
            string prefabPath,
            PowerUpType expectedType)
        {
            // Arrange
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            // Assert
            AssertCommonCollectiblePrefabContracts(prefab);
            PowerUpCollectible collectible = prefab.GetComponent<PowerUpCollectible>();
            Assert.IsNotNull(collectible);
            Assert.IsInstanceOf<IPowerUpCollectible>(collectible);
            Assert.IsInstanceOf<IPoolable>(collectible);
            Assert.AreEqual(expectedType, collectible.Type);
        }

        [Test]
        public void DropAnimationConfigurationAsset_ExistsWithValidOrderedRanges()
        {
            // Arrange
            DropAnimationConfiguration config = AssetDatabase.LoadAssetAtPath<DropAnimationConfiguration>(DROP_ANIMATION_CONFIG_PATH);

            // Assert
            Assert.IsNotNull(config);
            Assert.AreEqual(2f, config.ScatterRadius, TOLERANCE);
            Assert.AreEqual(0.6f, config.ScaleDuration, TOLERANCE);
            Assert.AreEqual(0.8f, config.ScatterDuration, TOLERANCE);
            Assert.AreEqual(1f, config.MinJumpPower, TOLERANCE);
            Assert.AreEqual(1.22f, config.MaxJumpPower, TOLERANCE);
            Assert.AreEqual(1.2f, config.MinDurationMultiplier, TOLERANCE);
            Assert.AreEqual(1.4f, config.MaxDurationMultiplier, TOLERANCE);

            Assert.Greater(config.ScatterRadius, 0f);
            Assert.Greater(config.ScaleDuration, 0f);
            Assert.Greater(config.ScatterDuration, 0f);
            Assert.LessOrEqual(config.MinJumpPower, config.MaxJumpPower);
            Assert.LessOrEqual(config.MinDurationMultiplier, config.MaxDurationMultiplier);
        }

        [TestCase("Assets/Prefabs/Enemies/Basic/Zombies/Crawling/CrawlingZombie.prefab", TestName = "EnemyPrefab_CrawlingZombie_HasValidDropTableAndComponentOrder")]
        [TestCase("Assets/Prefabs/Enemies/Basic/Zombies/Standing/StandingZombie.prefab", TestName = "EnemyPrefab_StandingZombie_HasValidDropTableAndComponentOrder")]
        [TestCase("Assets/Prefabs/Enemies/Basic/BarrelEnemy.prefab", TestName = "EnemyPrefab_BarrelEnemy_HasValidDropTableAndComponentOrder")]
        public void EnemyPrefab_HasValidEnemyDropHandlerTableAndComponentOrder(string enemyPrefabPath)
        {
            // Arrange
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPrefabPath);

            // Assert
            AssertEnemyDropTableContracts(enemyPrefab);
        }

        private static void AssertCommonCollectiblePrefabContracts(GameObject prefab)
        {
            Assert.IsNotNull(prefab);
            Assert.IsTrue(prefab.activeSelf);

            int expectedLayer = LayerMask.NameToLayer(COLLECTIBLES_LAYER_NAME);
            Assert.GreaterOrEqual(expectedLayer, 0);
            Assert.AreEqual(expectedLayer, prefab.layer);

            Collider triggerCollider = prefab.GetComponent<Collider>();
            Assert.IsNotNull(triggerCollider);
            Assert.IsTrue(triggerCollider.isTrigger);

            MonoBehaviour[] allBehaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                Assert.IsNotNull(allBehaviours[i]);
                Assert.AreNotEqual("GameObjectSelfInjector", allBehaviours[i].GetType().Name);
            }
        }

        private static void AssertEnemyDropTableContracts(GameObject enemyPrefab)
        {
            Assert.IsNotNull(enemyPrefab);

            Enemy enemy = enemyPrefab.GetComponent<Enemy>();
            EnemyDropHandler dropHandler = enemyPrefab.GetComponent<EnemyDropHandler>();
            Assert.IsNotNull(enemy);
            Assert.IsNotNull(dropHandler);

            Component[] components = enemyPrefab.GetComponents<Component>();
            int enemyIndex = Array.IndexOf(components, enemy);
            int dropHandlerIndex = Array.IndexOf(components, dropHandler);
            Assert.GreaterOrEqual(enemyIndex, 0);
            Assert.Greater(dropHandlerIndex, enemyIndex);

            FieldInfo entriesField = typeof(EnemyDropHandler).GetField("_dropEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            CollectibleDropEntry[] entries = entriesField.GetValue(dropHandler) as CollectibleDropEntry[];
            Assert.IsNotNull(entries);
            Assert.Greater(entries.Length, 0);

            for (int i = 0; i < entries.Length; i++)
            {
                CollectibleDropEntry entry = entries[i];
                Assert.IsNotNull(entry.Prefab);
                Assert.IsNotNull(entry.Prefab.GetComponent<ICollectible>());
                Assert.IsNotNull(entry.Prefab.GetComponent<IPoolable>());
                Assert.Greater(entry.DropChancePercent, 0f);
                Assert.LessOrEqual(entry.DropChancePercent, 100f);
                Assert.Greater(entry.YOffset, 0f);
            }
        }
    }
}
