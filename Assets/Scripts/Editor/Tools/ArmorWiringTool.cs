using System;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.Player;
using Assets.Scripts.PowerUps.Armor;
using Assets.Scripts.PowerUps.Config;
using Assets.Scripts.PowerUps.Shield;
using Assets.Scripts.ReflexDI;
using Assets.Scripts.UI.DevConsole;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Editor.Tools
{
    public static class ArmorWiringTool
    {
        private const string HEALTH_BAR_CANVAS_PREFAB_PATH = "Assets/Prefabs/UI/HealthBarCanvas.prefab";
        private const string ARMOR_CONFIG_PATH = "Assets/ScriptableObjects/Collectibles/PowerUps/ArmorConfig.asset";
        private const string RUINED_BLOOD_CITY_SCENE_PATH = "Assets/Scenes/RuinedBloodCity.unity";

        [MenuItem("Tools/Armor/Wire Armor Power-Up")]
        public static void WireArmor()
        {
            WireHealthBarCanvasPrefab();
            WireRuinedBloodCityScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Armor Power-Up wiring completed successfully.");
        }

        [MenuItem("Tools/Armor/Play From Boot")]
        public static void PlayFromBoot()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/Armor/Open Gameplay Scene")]
        public static void OpenGameplayScene()
        {
            EditorSceneManager.OpenScene(RUINED_BLOOD_CITY_SCENE_PATH);
        }

        public static void WireHealthBarCanvasPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HEALTH_BAR_CANVAS_PREFAB_PATH);
            try
            {
                ArmorPlatesView armorPlatesView = root.GetComponent<ArmorPlatesView>();
                if (armorPlatesView == null)
                {
                    armorPlatesView = root.AddComponent<ArmorPlatesView>();
                }

                Transform sliderTransform = root.transform.Find("HealthBarSlider");
                if (sliderTransform == null)
                {
                    throw new InvalidOperationException("HealthBarSlider child not found on HealthBarCanvas prefab.");
                }

                RectTransform sliderRect = sliderTransform.GetComponent<RectTransform>();
                Transform bgFillTransform = sliderTransform.Find("Background/Fill");
                Sprite baseSprite = null;
                if (bgFillTransform != null)
                {
                    Image bgImage = bgFillTransform.GetComponent<Image>();
                    if (bgImage != null)
                    {
                        baseSprite = bgImage.sprite;
                    }
                }

                ArmorPlateItemView[] plateViews = new ArmorPlateItemView[3];
                for (int i = 0; i < 3; i++)
                {
                    string plateName = $"ArmorPlate_{i + 1}";
                    Transform plateTransform = root.transform.Find(plateName);
                    GameObject plateGo;
                    if (plateTransform == null)
                    {
                        plateGo = new GameObject(plateName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ArmorPlateItemView));
                        plateGo.transform.SetParent(root.transform, false);
                    }
                    else
                    {
                        plateGo = plateTransform.gameObject;
                    }

                    plateGo.layer = root.layer;

                    Image bgImage = plateGo.GetComponent<Image>();
                    bgImage.color = new Color(0f, 0f, 0f, 0.85f);
                    bgImage.sprite = baseSprite;
                    bgImage.type = Image.Type.Sliced;

                    Transform fillTransform = plateGo.transform.Find("Fill");
                    GameObject fillGo;
                    if (fillTransform == null)
                    {
                        fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        fillGo.transform.SetParent(plateGo.transform, false);
                    }
                    else
                    {
                        fillGo = fillTransform.gameObject;
                    }

                    fillGo.layer = root.layer;
                    RectTransform fillRect = fillGo.GetComponent<RectTransform>();
                    fillRect.anchorMin = Vector2.zero;
                    fillRect.anchorMax = Vector2.one;
                    fillRect.offsetMin = new Vector2(0.015f, 0.015f);
                    fillRect.offsetMax = new Vector2(-0.015f, -0.015f);

                    Image fillImage = fillGo.GetComponent<Image>();
                    fillImage.color = new Color(0.05f, 0.15f, 0.6f, 1f);
                    fillImage.sprite = baseSprite;
                    fillImage.type = Image.Type.Filled;
                    fillImage.fillMethod = Image.FillMethod.Horizontal;
                    fillImage.fillOrigin = (int)Image.OriginHorizontal.Right;
                    fillImage.fillAmount = 1f;

                    ArmorPlateItemView plateItemView = plateGo.GetComponent<ArmorPlateItemView>();
                    SerializedObject plateSo = new SerializedObject(plateItemView);
                    plateSo.FindProperty("_backgroundImage").objectReferenceValue = bgImage;
                    plateSo.FindProperty("_fillImage").objectReferenceValue = fillImage;
                    plateSo.ApplyModifiedPropertiesWithoutUndo();

                    plateViews[i] = plateItemView;
                }

                SerializedObject apvSo = new SerializedObject(armorPlatesView);
                apvSo.FindProperty("_baseHealthBarRect").objectReferenceValue = sliderRect;
                SerializedProperty plateViewsProp = apvSo.FindProperty("_plateViews");
                plateViewsProp.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    plateViewsProp.GetArrayElementAtIndex(i).objectReferenceValue = plateViews[i];
                }

                apvSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, HEALTH_BAR_CANVAS_PREFAB_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void WireRuinedBloodCityScene()
        {
            var scene = EditorSceneManager.OpenScene(RUINED_BLOOD_CITY_SCENE_PATH, OpenSceneMode.Single);

            BubbleShieldService bubbleShieldService = UnityEngine.Object.FindAnyObjectByType<BubbleShieldService>();
            ArmorService armorService = UnityEngine.Object.FindAnyObjectByType<ArmorService>();
            if (armorService == null)
            {
                GameObject asGo = new GameObject("ArmorService");
                armorService = asGo.AddComponent<ArmorService>();

                if (bubbleShieldService != null)
                {
                    asGo.transform.SetParent(bubbleShieldService.transform.parent, false);
                    asGo.transform.SetSiblingIndex(bubbleShieldService.transform.GetSiblingIndex() + 1);
                }
            }

            ArmorConfigSO configAsset = AssetDatabase.LoadAssetAtPath<ArmorConfigSO>(ARMOR_CONFIG_PATH);
            if (configAsset == null)
            {
                throw new InvalidOperationException($"ArmorConfig asset not found at {ARMOR_CONFIG_PATH}");
            }

            PlayerManager player = UnityEngine.Object.FindAnyObjectByType<PlayerManager>();
            if (player == null)
            {
                throw new InvalidOperationException("PlayerManager not found in RuinedBloodCity scene.");
            }

            ArmorPlatesView platesView = player.GetComponentInChildren<ArmorPlatesView>(true);
            if (platesView == null)
            {
                throw new InvalidOperationException("ArmorPlatesView not found in player children in RuinedBloodCity scene.");
            }

            SerializedObject asSo = new SerializedObject(armorService);
            asSo.FindProperty("_config").objectReferenceValue = configAsset;
            asSo.FindProperty("_armorPlatesView").objectReferenceValue = platesView;
            asSo.ApplyModifiedPropertiesWithoutUndo();

            DefaultGameplaySceneInstaller installer = UnityEngine.Object.FindAnyObjectByType<DefaultGameplaySceneInstaller>();
            if (installer == null)
            {
                throw new InvalidOperationException("DefaultGameplaySceneInstaller not found in RuinedBloodCity scene.");
            }

            SerializedObject instSo = new SerializedObject(installer);
            instSo.FindProperty("_armorService").objectReferenceValue = armorService;
            instSo.ApplyModifiedPropertiesWithoutUndo();

            GameplayDevCommandsRegistrar devCommands = UnityEngine.Object.FindAnyObjectByType<GameplayDevCommandsRegistrar>();
            if (devCommands != null)
            {
                GameObject armorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Collectibles/Armor.prefab");
                SerializedObject devSo = new SerializedObject(devCommands);
                devSo.FindProperty("_armorPrefab").objectReferenceValue = armorPrefab;
                devSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
