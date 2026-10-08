using Assets.Scripts.ReflexDI;
using Assets.Scripts.UI.DevConsole;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Editor.Tools
{
    public static class DevConsolePrefabBuilder
    {
        private const string PREFAB_DIR = "Assets/Prefabs/UI/DevConsole";
        private const string PREFAB_PATH = "Assets/Prefabs/UI/DevConsole/DevConsolePresenter.prefab";

        [MenuItem("Tools/Dev Console/Create Dev Console Prefab")]
        public static void CreateDevConsolePrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                {
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                }

                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }

            if (!AssetDatabase.IsValidFolder(PREFAB_DIR))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs/UI", "DevConsole");
            }

            GameObject rootGo = new GameObject("DevConsolePresenter");
            Canvas canvas = rootGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            CanvasScaler scaler = rootGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            rootGo.AddComponent<GraphicRaycaster>();
            DevConsolePresenter presenter = rootGo.AddComponent<DevConsolePresenter>();

            // RootVisual Panel (covers top ~45% of screen)
            GameObject rootVisualGo = new GameObject("RootVisual");
            rootVisualGo.transform.SetParent(rootGo.transform, false);
            RectTransform rootVisualRect = rootVisualGo.AddComponent<RectTransform>();
            rootVisualRect.anchorMin = new Vector2(0f, 0.55f);
            rootVisualRect.anchorMax = new Vector2(1f, 1f);
            rootVisualRect.offsetMin = Vector2.zero;
            rootVisualRect.offsetMax = Vector2.zero;

            Image rootBg = rootVisualGo.AddComponent<Image>();
            rootBg.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);

            // Output ScrollView
            GameObject scrollViewGo = new GameObject("OutputScrollView");
            scrollViewGo.transform.SetParent(rootVisualGo.transform, false);
            RectTransform scrollRectTransform = scrollViewGo.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0f, 0.12f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.offsetMin = new Vector2(10f, 5f);
            scrollRectTransform.offsetMax = new Vector2(-10f, -5f);

            ScrollRect scrollRect = scrollViewGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Viewport
            GameObject viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollViewGo.transform, false);
            RectTransform viewportRect = viewportGo.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();

            // LogContent
            GameObject logContentGo = new GameObject("LogContent");
            logContentGo.transform.SetParent(viewportGo.transform, false);
            RectTransform logContentRect = logContentGo.AddComponent<RectTransform>();
            logContentRect.anchorMin = new Vector2(0f, 0f);
            logContentRect.anchorMax = new Vector2(1f, 1f);
            logContentRect.pivot = new Vector2(0f, 0f);
            logContentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentLayout = logContentGo.AddComponent<VerticalLayoutGroup>();
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            ContentSizeFitter contentFitter = logContentGo.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // LogText
            GameObject logTextGo = new GameObject("LogText");
            logTextGo.transform.SetParent(logContentGo.transform, false);
            RectTransform logTextRect = logTextGo.AddComponent<RectTransform>();
            TextMeshProUGUI logTmp = logTextGo.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                logTmp.font = TMP_Settings.defaultFontAsset;
            }

            logTmp.fontSize = 16f;
            logTmp.color = Color.white;
            logTmp.textWrappingMode = TextWrappingModes.Normal;
            logTmp.richText = true;
            logTmp.text = "Developer Console ready. Type 'help' for available commands.\n";

            scrollRect.content = logContentRect;
            scrollRect.viewport = viewportRect;

            // InputRow
            GameObject inputRowGo = new GameObject("InputRow");
            inputRowGo.transform.SetParent(rootVisualGo.transform, false);
            RectTransform inputRowRect = inputRowGo.AddComponent<RectTransform>();
            inputRowRect.anchorMin = new Vector2(0f, 0f);
            inputRowRect.anchorMax = new Vector2(1f, 0.12f);
            inputRowRect.offsetMin = new Vector2(10f, 5f);
            inputRowRect.offsetMax = new Vector2(-10f, -5f);

            HorizontalLayoutGroup inputLayout = inputRowGo.AddComponent<HorizontalLayoutGroup>();
            inputLayout.childControlHeight = true;
            inputLayout.childControlWidth = true;
            inputLayout.childForceExpandHeight = true;
            inputLayout.childForceExpandWidth = false;
            inputLayout.spacing = 8f;

            // PromptLabel
            GameObject promptLabelGo = new GameObject("PromptLabel");
            promptLabelGo.transform.SetParent(inputRowGo.transform, false);
            RectTransform promptRect = promptLabelGo.AddComponent<RectTransform>();
            promptRect.sizeDelta = new Vector2(20f, 0f);
            LayoutElement promptLayout = promptLabelGo.AddComponent<LayoutElement>();
            promptLayout.minWidth = 20f;
            promptLayout.preferredWidth = 20f;
            promptLayout.flexibleWidth = 0f;

            TextMeshProUGUI promptTmp = promptLabelGo.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                promptTmp.font = TMP_Settings.defaultFontAsset;
            }

            promptTmp.fontSize = 18f;
            promptTmp.color = new Color(0.2f, 0.8f, 1f);
            promptTmp.alignment = TextAlignmentOptions.MidlineLeft;
            promptTmp.text = ">";

            // InputField
            GameObject inputFieldGo = new GameObject("InputField");
            inputFieldGo.transform.SetParent(inputRowGo.transform, false);
            RectTransform inputFieldRect = inputFieldGo.AddComponent<RectTransform>();
            LayoutElement inputLayoutElement = inputFieldGo.AddComponent<LayoutElement>();
            inputLayoutElement.flexibleWidth = 1f;
            inputLayoutElement.minWidth = 100f;

            Image inputBg = inputFieldGo.AddComponent<Image>();
            inputBg.color = new Color(0.12f, 0.12f, 0.16f, 0.8f);

            // Text Area (Viewport with RectMask2D)
            GameObject textAreaGo = new GameObject("Text Area");
            textAreaGo.transform.SetParent(inputFieldGo.transform, false);
            RectTransform textAreaRect = textAreaGo.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.sizeDelta = Vector2.zero;
            textAreaRect.offsetMin = new Vector2(6f, 2f);
            textAreaRect.offsetMax = new Vector2(-6f, -2f);
            textAreaGo.AddComponent<RectMask2D>();

            // InputField Text component
            GameObject inputTextGo = new GameObject("Text");
            inputTextGo.transform.SetParent(textAreaGo.transform, false);
            RectTransform inputTextRect = inputTextGo.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.sizeDelta = Vector2.zero;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;
            TextMeshProUGUI inputFieldTmp = inputTextGo.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                inputFieldTmp.font = TMP_Settings.defaultFontAsset;
            }

            inputFieldTmp.fontSize = 18f;
            inputFieldTmp.color = Color.white;
            inputFieldTmp.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField tmpInputField = inputFieldGo.AddComponent<TMP_InputField>();
            tmpInputField.textViewport = textAreaRect;
            tmpInputField.textComponent = inputFieldTmp;
            tmpInputField.caretBlinkRate = 0.85f;
            tmpInputField.caretWidth = 2;
            tmpInputField.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };

            // Wire Serialized Properties on DevConsolePresenter
            SerializedObject serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_rootVisual").objectReferenceValue = rootVisualGo;
            serializedPresenter.FindProperty("_inputField").objectReferenceValue = tmpInputField;
            serializedPresenter.FindProperty("_logText").objectReferenceValue = logTmp;
            serializedPresenter.FindProperty("_scrollRect").objectReferenceValue = scrollRect;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            rootVisualGo.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(rootGo, PREFAB_PATH);
            Object.DestroyImmediate(rootGo);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DevConsolePrefabBuilder] Created and saved Dev Console prefab to {PREFAB_PATH}");
        }

        [MenuItem("Tools/Dev Console/Setup Dev Console in Active Scene")]
        public static void SetupInActiveScene()
        {
            DefaultGameplaySceneInstaller installer = Object.FindAnyObjectByType<DefaultGameplaySceneInstaller>();
            if (installer == null)
            {
                Debug.LogError("[DevConsolePrefabBuilder] DefaultGameplaySceneInstaller not found in active scene.");
                return;
            }

            CreateDevConsolePrefab();
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);

            DevConsolePresenter presenter = Object.FindAnyObjectByType<DevConsolePresenter>();
            if (presenter != null)
            {
                Undo.DestroyObjectImmediate(presenter.gameObject);
            }

            GameObject uiRoot = GameObject.Find("====UI====");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, uiRoot != null ? uiRoot.transform : null);
            instance.name = "DevConsolePresenter";
            presenter = instance.GetComponent<DevConsolePresenter>();
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate DevConsolePresenter");

            GameplayDevCommandsRegistrar registrar = presenter.gameObject.AddComponent<GameplayDevCommandsRegistrar>();
            Undo.RegisterCreatedObjectUndo(registrar, "Add GameplayDevCommandsRegistrar");

            SerializedObject serializedInstaller = new SerializedObject(installer);
            serializedInstaller.FindProperty("_devConsolePresenter").objectReferenceValue = presenter;
            serializedInstaller.FindProperty("_devCommandsRegistrar").objectReferenceValue = registrar;
            serializedInstaller.ApplyModifiedProperties();

            EditorUtility.SetDirty(installer);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(installer.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(installer.gameObject.scene);

            Debug.Log("[DevConsolePrefabBuilder] Successfully set up Dev Console in active scene.");
        }

        [MenuItem("Tools/Dev Console/Setup Dev Console in Ruined Blood City")]
        public static void SetupInRuinedBloodCity()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/RuinedBloodCity.unity");
            SetupInActiveScene();
        }
    }
}
