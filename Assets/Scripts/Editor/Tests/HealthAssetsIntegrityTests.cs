using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Player;
using Assets.Scripts.PowerUps;
using Assets.Scripts.PowerUps.Collectibles;
using Assets.Scripts.PowerUps.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Editor.Tests
{
    public class HealthAssetsIntegrityTests
    {
        private const string ARMOR_CONFIG_PATH = "Assets/ScriptableObjects/Collectibles/PowerUps/ArmorConfig.asset";
        private const string ARMOR_PREFAB_PATH = "Assets/Prefabs/Collectibles/Armor.prefab";
        private const string HEALTH_BAR_CANVAS_PREFAB_PATH = "Assets/Prefabs/UI/HealthBarCanvas.prefab";
        private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/Player/Player.prefab";
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
        public void ArmorConfigAsset_IsValidWithExpectedPlateCapacities()
        {
            // Arrange
            ArmorConfigSO config = AssetDatabase.LoadAssetAtPath<ArmorConfigSO>(ARMOR_CONFIG_PATH);
            Assert.IsNotNull(config);

            // Act & Assert
            Assert.DoesNotThrow(() => config.ValidateConfiguration());
            Assert.AreEqual(3, config.MaxLevel);
            Assert.AreEqual(20f, config.GetPlateCapacity(0), TOLERANCE);
            Assert.AreEqual(40f, config.GetPlateCapacity(1), TOLERANCE);
            Assert.AreEqual(60f, config.GetPlateCapacity(2), TOLERANCE);
            Assert.AreEqual(1.5f, config.ReferenceBaseHealthWidth, TOLERANCE);
            Assert.AreEqual(100f, config.ReferenceBaseMaxHealth, TOLERANCE);
            Assert.AreEqual(0.2f, config.HorizontalOverlapRatio, TOLERANCE);
            Assert.AreEqual(0.05f, config.PlateColor.r, TOLERANCE);
            Assert.AreEqual(0.15f, config.PlateColor.g, TOLERANCE);
            Assert.AreEqual(0.6f, config.PlateColor.b, TOLERANCE);
            Assert.AreEqual(1f, config.PlateColor.a, TOLERANCE);
        }

        [Test]
        public void ArmorPrefab_HasTriggerColliderAndArmorType()
        {
            // Arrange
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ARMOR_PREFAB_PATH);
            Assert.IsNotNull(prefab);

            // Act
            Collider triggerCollider = prefab.GetComponent<Collider>();
            PowerUpCollectible collectible = prefab.GetComponent<PowerUpCollectible>();

            // Assert
            Assert.IsNotNull(triggerCollider);
            Assert.IsTrue(triggerCollider.isTrigger);
            Assert.IsNotNull(collectible);
            Assert.AreEqual(PowerUpType.Armor, collectible.Type);
        }

        [Test]
        public void HealthBarCanvasPrefab_HasValidHealthBarAndThreeArmorPlateViews()
        {
            // Arrange
            ArmorConfigSO config = AssetDatabase.LoadAssetAtPath<ArmorConfigSO>(ARMOR_CONFIG_PATH);
            Assert.IsNotNull(config);
            GameObject canvasInstance = InstantiatePrefabInactive(HEALTH_BAR_CANVAS_PREFAB_PATH);

            // Act
            HealthBar healthBar = canvasInstance.GetComponentInChildren<HealthBar>(true);
            ArmorPlatesView platesView = canvasInstance.GetComponent<ArmorPlatesView>();

            // Assert
            Assert.IsNotNull(healthBar);
            Assert.IsNotNull(healthBar.GetComponent<Slider>());
            Assert.IsNotNull(GetPrivateField<Image>(typeof(HealthBar), healthBar, "_fillImage"));
            Assert.IsNotNull(GetPrivateField<Gradient>(typeof(HealthBar), healthBar, "_gradient"));

            Assert.IsNotNull(platesView);
            Assert.DoesNotThrow(() => InvokeLifecycleMethod(platesView, "Awake"));
            AssertArmorPlatesWiringAndInitialize(platesView, config);
        }

        [Test]
        public void PlayerPrefab_HasValidRegenativeHealthHandlersAndHealthBarBinding()
        {
            // Arrange
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            Assert.IsNotNull(playerPrefab);

            // Act
            PlayerManager playerManager = playerPrefab.GetComponent<PlayerManager>();
            RegenativeHealth regenHealth = playerPrefab.GetComponent<RegenativeHealth>();
            PlayerDamagedHandler damagedHandler = playerPrefab.GetComponent<PlayerDamagedHandler>();
            PlayerDeathHandler deathHandler = playerPrefab.GetComponent<PlayerDeathHandler>();
            HealthBar healthBar = playerPrefab.GetComponentInChildren<HealthBar>(true);

            // Assert
            Assert.IsNotNull(playerManager);
            Assert.IsNotNull(GetPrivateField<UnityEngine.Object>(typeof(PlayerManager), playerManager, "_audioClipPlayer"));

            Assert.IsNotNull(regenHealth);
            Assert.AreEqual(100f, regenHealth.MaxHealth, TOLERANCE);
            Assert.AreEqual(2f, regenHealth.MaxRegenerationAmount, TOLERANCE);
            Assert.AreEqual(1f, regenHealth.StartRegenerationDelay, TOLERANCE);

            Assert.IsNotNull(damagedHandler);
            Assert.IsNotNull(GetPrivateField<GameObject>(typeof(PlayerDamagedHandler), damagedHandler, "_carVisual"));
            Assert.IsNotNull(GetPrivateField<UnityEngine.Object>(typeof(PlayerDamagedHandler), damagedHandler, "_damageVfxPlayer"));

            Assert.IsNotNull(deathHandler);
            Assert.IsNotNull(GetPrivateField<GameObject>(typeof(PlayerDeathHandler), deathHandler, "_visual"));
            Assert.IsNotNull(GetPrivateField<UnityEngine.Object>(typeof(PlayerDeathHandler), deathHandler, "_deathVfxPlayer"));

            Assert.IsNotNull(healthBar);
            Health boundHealth = GetPrivateField<Health>(typeof(HealthBar), healthBar, "_health");
            Assert.AreSame(regenHealth, boundHealth);
        }

        private GameObject InstantiatePrefabInactive(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab);

            GameObject inactiveParent = new GameObject("TempInactiveAssetHost");
            inactiveParent.SetActive(false);

            GameObject instance = UnityEngine.Object.Instantiate(prefab, inactiveParent.transform);
            instance.SetActive(false);
            instance.transform.SetParent(null, true);
            UnityEngine.Object.DestroyImmediate(inactiveParent);

            _createdObjects.Add(instance);
            return instance;
        }

        private static void AssertArmorPlatesWiringAndInitialize(ArmorPlatesView platesView, ArmorConfigSO config)
        {
            RectTransform baseRect = GetPrivateField<RectTransform>(typeof(ArmorPlatesView), platesView, "_baseHealthBarRect");
            ArmorPlateItemView[] plateViews = GetPrivateField<ArmorPlateItemView[]>(typeof(ArmorPlatesView), platesView, "_plateViews");

            Assert.IsNotNull(baseRect);
            Assert.IsNotNull(plateViews);
            Assert.AreEqual(3, plateViews.Length);
            Assert.AreEqual(0, baseRect.GetSiblingIndex());

            for (int i = 0; i < plateViews.Length; i++)
            {
                Assert.IsNotNull(plateViews[i]);
                Assert.IsNotNull(GetPrivateField<Image>(typeof(ArmorPlateItemView), plateViews[i], "_backgroundImage"));
                Assert.IsNotNull(GetPrivateField<Image>(typeof(ArmorPlateItemView), plateViews[i], "_fillImage"));
                Assert.AreEqual(i + 1, plateViews[i].transform.GetSiblingIndex());
            }

            platesView.Initialize(config);

            for (int i = 0; i < plateViews.Length; i++)
            {
                Assert.IsFalse(plateViews[i].gameObject.activeSelf);
            }
        }

        private static T GetPrivateField<T>(Type ownerType, object target, string fieldName) where T : class
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field.GetValue(target) as T;
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
