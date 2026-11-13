using System;
using System.Collections.Generic;
using Assets.ScriptableObjects.Input;
using Assets.Scripts.Effects;
using Assets.Scripts.Input;
using Assets.Scripts.Input.Constants;
using Assets.Scripts.Interactables;
using Assets.Scripts.PowerUps.Presentation;
using Assets.Scripts.ReflexDI;
using Assets.Scripts.UI.Death;
using Assets.Scripts.UI.DevConsole;
using Assets.Scripts.UI.Input;
using Assets.Scripts.UI.Pause;
using Assets.Scripts.UI.Skills;
using Reflex.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.Editor.Tools
{
    public static class InputConfigurationValidator
    {
        public sealed class ValidationReport
        {
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
            public bool Passed
            {
                get { return Errors.Count == 0; }
            }
        }

        private const string ACTION_ASSET_PATH = "Assets/InputSystem/InputSystem_Actions.inputactions";
        private const string CATALOG_ASSET_PATH = "Assets/ScriptableObjects/Input/InputGlyphCatalog.asset";
        private const string CONFIG_ASSET_PATH = "Assets/ScriptableObjects/Input/InputRuntimeConfig.asset";
        private const string PROJECT_SCOPE_PREFAB_PATH = "Assets/Resources/ProjectScope.prefab";
        private const string BOOT_SCENE_PATH = "Assets/Scenes/Boot.unity";
        private const string MAIN_MENU_SCENE_PATH = "Assets/Scenes/MainMenu.unity";
        private const string GAMEPLAY_SCENE_PATH = "Assets/Scenes/RuinedBloodCity.unity";
        private const string UPGRADE_BUTTON_PREFAB_PATH = "Assets/Prefabs/UI/SkillsUpgrader/UpgradeButton.prefab";
        private const string SKILL_UPGRADE_PRESENTER_PREFAB_PATH = "Assets/Prefabs/UI/SkillsUpgrader/SkillUpgradePresenter.prefab";
        private const string TOTEM_PREFAB_PATH = "Assets/Prefabs/Interactables/IncreaseDifficultyTotem.prefab";

        private static readonly string[] REQUIRED_CONTROL_PATHS_ALL_PADS = new string[]
        {
            "buttonSouth",
            "buttonNorth",
            "buttonEast",
            "buttonWest",
            "leftStick",
            "dpad",
            "start",
            "rightTrigger"
        };

        private static readonly string[] REQUIRED_CONTROL_PATHS_KM = new string[]
        {
            "e",
            "1",
            "2",
            "3",
            "escape",
            "enter",
            "space",
            "backquote"
        };

        [MenuItem("Tools/Input/Validate Input Configuration")]
        public static void ValidateFromMenu()
        {
            ValidationReport report = RunValidation();
            for (int i = 0; i < report.Warnings.Count; i++)
            {
                Debug.LogWarning($"[InputConfigurationValidator] Warning: {report.Warnings[i]}");
            }

            for (int i = 0; i < report.Errors.Count; i++)
            {
                Debug.LogError($"[InputConfigurationValidator] Error: {report.Errors[i]}");
            }

            Debug.Log($"[InputConfigurationValidator] Completed with {report.Errors.Count} error(s) and {report.Warnings.Count} warning(s). Passed={report.Passed}");
        }

        public static ValidationReport RunValidation()
        {
            ValidationReport report = new ValidationReport();

            InputActionAsset actionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ACTION_ASSET_PATH);
            InputGlyphCatalog catalog = AssetDatabase.LoadAssetAtPath<InputGlyphCatalog>(CATALOG_ASSET_PATH);
            InputRuntimeConfig runtimeConfig = AssetDatabase.LoadAssetAtPath<InputRuntimeConfig>(CONFIG_ASSET_PATH);

            ValidateActionAsset(actionAsset, report);
            ValidateCatalog(catalog, report);
            ValidateRuntimeConfig(runtimeConfig, report);
            ValidateProjectScope(actionAsset, catalog, runtimeConfig, report);
            ValidatePrefabs(report);
            ValidateScenes(actionAsset, report);

            return report;
        }

        private static void ValidateActionAsset(InputActionAsset asset, ValidationReport report)
        {
            if (asset == null)
            {
                report.Errors.Add($"Missing InputActionAsset at '{ACTION_ASSET_PATH}'.");
                return;
            }

            VerifyAction(asset, InputConstants.PLAYER_MOVE_ACTION, InputConstants.EXPECTED_VECTOR2_CONTROL_TYPE, true, true, report);
            VerifyAction(asset, InputConstants.PLAYER_BRAKE_ACTION, null, true, true, report);
            VerifyAction(asset, InputConstants.PLAYER_REVERSE_ACTION, InputConstants.EXPECTED_AXIS_CONTROL_TYPE, false, true, report);
            VerifyAction(asset, InputConstants.PLAYER_INTERACT_ACTION, null, true, true, report);
            VerifyAction(asset, InputConstants.GLOBAL_PAUSE_ACTION, null, true, true, report);
            VerifyAction(asset, InputConstants.GLOBAL_TOGGLE_CONSOLE_ACTION, null, true, false, report);
            VerifyAction(asset, InputConstants.UI_NAVIGATE_ACTION, InputConstants.EXPECTED_VECTOR2_CONTROL_TYPE, true, true, report);
            VerifyAction(asset, InputConstants.UI_SUBMIT_ACTION, null, true, true, report);
            VerifyAction(asset, InputConstants.UI_CANCEL_ACTION, null, true, true, report);
            VerifyAction(asset, InputConstants.UI_POINT_ACTION, InputConstants.EXPECTED_VECTOR2_CONTROL_TYPE, true, false, report);
            VerifyAction(asset, InputConstants.UI_CLICK_ACTION, null, true, false, report);
            VerifyAction(asset, InputConstants.UI_SCROLL_WHEEL_ACTION, InputConstants.EXPECTED_VECTOR2_CONTROL_TYPE, true, false, report);
            VerifyAction(asset, InputConstants.UPGRADE_SELECT_CHOICE_1_ACTION, null, true, true, report);
            VerifyAction(asset, InputConstants.UPGRADE_SELECT_CHOICE_2_ACTION, null, true, true, report);
            VerifyAction(asset, InputConstants.UPGRADE_SELECT_CHOICE_3_ACTION, null, true, true, report);
            VerifyAction(asset, InputConstants.DEV_CONSOLE_CLOSE_ACTION, null, true, true, report);
            VerifyAction(asset, InputConstants.DEV_CONSOLE_SUBMIT_ACTION, null, true, false, report);
            VerifyAction(asset, InputConstants.DEV_CONSOLE_HISTORY_PREVIOUS_ACTION, null, true, false, report);
            VerifyAction(asset, InputConstants.DEV_CONSOLE_HISTORY_NEXT_ACTION, null, true, false, report);
        }

        private static void VerifyAction(
            InputActionAsset asset,
            string actionPath,
            string expectedControlType,
            bool requireKeyboardMouse,
            bool requireGamepad,
            ValidationReport report)
        {
            InputAction action = asset.FindAction(actionPath, throwIfNotFound: false);
            if (action == null)
            {
                report.Errors.Add($"Action '{actionPath}' missing from '{ACTION_ASSET_PATH}'.");
                return;
            }

            if (!string.IsNullOrEmpty(expectedControlType)
                && !string.Equals(action.expectedControlType, expectedControlType, StringComparison.Ordinal))
            {
                report.Errors.Add($"Action '{actionPath}' expectedControlType is '{action.expectedControlType}', expected '{expectedControlType}'.");
            }

            if (requireKeyboardMouse && !HasGroupBinding(action, InputConstants.KEYBOARD_MOUSE_GROUP))
            {
                report.Errors.Add($"Action '{actionPath}' has no '{InputConstants.KEYBOARD_MOUSE_GROUP}' binding.");
            }

            if (requireGamepad && !HasGroupBinding(action, InputConstants.GAMEPAD_GROUP))
            {
                report.Errors.Add($"Action '{actionPath}' has no '{InputConstants.GAMEPAD_GROUP}' binding.");
            }
        }

        private static bool HasGroupBinding(InputAction action, string groupName)
        {
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (binding.isComposite)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(binding.groups) && binding.groups.IndexOf(groupName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateCatalog(InputGlyphCatalog catalog, ValidationReport report)
        {
            if (catalog == null)
            {
                report.Errors.Add($"Missing InputGlyphCatalog at '{CATALOG_ASSET_PATH}'.");
                return;
            }

            catalog.Validate(report.Errors, report.Warnings);

            for (int i = 0; i < REQUIRED_CONTROL_PATHS_KM.Length; i++)
            {
                string path = REQUIRED_CONTROL_PATHS_KM[i];
                if (!catalog.TryGetGlyph(InputDeviceFamily.KeyboardMouse, KeyboardGlyphTheme.Light, path, out _))
                {
                    report.Errors.Add($"InputGlyphCatalog missing KeyboardMouse Light entry for '{path}'.");
                }
            }

            InputDeviceFamily[] families = new InputDeviceFamily[]
            {
                InputDeviceFamily.Xbox,
                InputDeviceFamily.PlayStation,
                InputDeviceFamily.Switch,
                InputDeviceFamily.GenericGamepad
            };

            for (int f = 0; f < families.Length; f++)
            {
                InputDeviceFamily family = families[f];
                for (int i = 0; i < REQUIRED_CONTROL_PATHS_ALL_PADS.Length; i++)
                {
                    string path = REQUIRED_CONTROL_PATHS_ALL_PADS[i];
                    if (!catalog.TryGetGlyph(family, KeyboardGlyphTheme.Light, path, out InputGlyphEntry entry))
                    {
                        report.Errors.Add($"InputGlyphCatalog missing {family} entry for '{path}'.");
                    }
                    else if (family != InputDeviceFamily.GenericGamepad && entry.Sprite == null && !entry.IsSpriteOptional)
                    {
                        report.Warnings.Add($"InputGlyphCatalog {family} entry '{path}' has no Sprite assigned.");
                    }
                }
            }
        }

        private static void ValidateRuntimeConfig(InputRuntimeConfig config, ValidationReport report)
        {
            if (config == null)
            {
                report.Errors.Add($"Missing InputRuntimeConfig at '{CONFIG_ASSET_PATH}'.");
                return;
            }

            if (config.StickActivityRearmThreshold > config.StickActivityEnterThreshold)
            {
                report.Warnings.Add("InputRuntimeConfig _stickActivityRearmThreshold exceeds _stickActivityEnterThreshold and will be clamped at runtime.");
            }
        }

        private static void ValidateProjectScope(
            InputActionAsset actionAsset,
            InputGlyphCatalog catalog,
            InputRuntimeConfig runtimeConfig,
            ValidationReport report)
        {
            GameObject projectScope = AssetDatabase.LoadAssetAtPath<GameObject>(PROJECT_SCOPE_PREFAB_PATH);
            if (projectScope == null)
            {
                report.Errors.Add($"Missing ProjectScope prefab at '{PROJECT_SCOPE_PREFAB_PATH}'.");
                return;
            }

            ProjectInstaller installer = projectScope.GetComponent<ProjectInstaller>();
            if (installer == null)
            {
                report.Errors.Add($"ProjectScope prefab lacks ProjectInstaller component.");
                return;
            }

            SerializedObject so = new SerializedObject(installer);
            UnityEngine.Object assignedActions = so.FindProperty("_inputActions")?.objectReferenceValue;
            UnityEngine.Object assignedCatalog = so.FindProperty("_inputGlyphCatalog")?.objectReferenceValue;
            UnityEngine.Object assignedConfig = so.FindProperty("_inputRuntimeConfig")?.objectReferenceValue;

            if (assignedActions != actionAsset)
            {
                report.Errors.Add("ProjectInstaller._inputActions is not assigned to InputSystem_Actions.");
            }

            if (assignedCatalog != catalog)
            {
                report.Errors.Add("ProjectInstaller._inputGlyphCatalog is not assigned to InputGlyphCatalog.");
            }

            if (assignedConfig != runtimeConfig)
            {
                report.Errors.Add("ProjectInstaller._inputRuntimeConfig is not assigned to InputRuntimeConfig.");
            }
        }

        private static void ValidatePrefabs(ValidationReport report)
        {
            GameObject upgradeButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UPGRADE_BUTTON_PREFAB_PATH);
            if (upgradeButtonPrefab == null)
            {
                report.Errors.Add($"Missing UpgradeButton prefab at '{UPGRADE_BUTTON_PREFAB_PATH}'.");
            }
            else
            {
                SkillUpgradeButton button = upgradeButtonPrefab.GetComponent<SkillUpgradeButton>();
                if (button == null)
                {
                    report.Errors.Add("UpgradeButton prefab lacks SkillUpgradeButton.");
                }
                else
                {
                    SerializedObject so = new SerializedObject(button);
                    InputPromptView promptView = so.FindProperty("_inputPromptView")?.objectReferenceValue as InputPromptView;
                    if (promptView == null)
                    {
                        report.Errors.Add("UpgradeButton._inputPromptView is unassigned.");
                    }
                    else
                    {
                        ValidatePromptView(promptView, "UpgradeButton", report);
                    }
                }
            }

            GameObject presenterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SKILL_UPGRADE_PRESENTER_PREFAB_PATH);
            if (presenterPrefab == null)
            {
                report.Errors.Add($"Missing SkillUpgradePresenter prefab at '{SKILL_UPGRADE_PRESENTER_PREFAB_PATH}'.");
            }
            else
            {
                SkillUpgradePresenter presenter = presenterPrefab.GetComponent<SkillUpgradePresenter>();
                if (presenter == null)
                {
                    report.Errors.Add("SkillUpgradePresenter prefab lacks SkillUpgradePresenter component.");
                }
                else
                {
                    SerializedObject so = new SerializedObject(presenter);
                    InputPromptView firstView = so.FindProperty("_firstSkillPromptView")?.objectReferenceValue as InputPromptView;
                    InputPromptView secondView = so.FindProperty("_secondSkillPromptView")?.objectReferenceValue as InputPromptView;
                    ValidatePromptView(firstView, "SkillUpgradePresenter/FirstCard", report);
                    ValidatePromptView(secondView, "SkillUpgradePresenter/SecondCard", report);
                }
            }

            GameObject totemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TOTEM_PREFAB_PATH);
            if (totemPrefab == null)
            {
                report.Errors.Add($"Missing IncreaseDifficultyTotem prefab at '{TOTEM_PREFAB_PATH}'.");
            }
            else
            {
                IncreaseDifficultyTotem totem = totemPrefab.GetComponent<IncreaseDifficultyTotem>();
                if (totem == null)
                {
                    report.Errors.Add("IncreaseDifficultyTotem prefab lacks IncreaseDifficultyTotem component.");
                }
                else
                {
                    SerializedObject so = new SerializedObject(totem);
                    InputPromptView promptView = so.FindProperty("_interactPromptView")?.objectReferenceValue as InputPromptView;
                    if (promptView == null)
                    {
                        report.Errors.Add("IncreaseDifficultyTotem._interactPromptView is unassigned.");
                    }
                    else
                    {
                        ValidatePromptView(promptView, "IncreaseDifficultyTotem", report);
                    }
                }
            }
        }

        private static void ValidatePromptView(InputPromptView view, string ownerLabel, ValidationReport report)
        {
            if (view == null)
            {
                return;
            }

            if (view.IconImage == null)
            {
                report.Errors.Add($"InputPromptView on '{ownerLabel}/{view.name}' is missing _iconImage.");
            }

            if (view.FallbackLabel == null)
            {
                report.Errors.Add($"InputPromptView on '{ownerLabel}/{view.name}' is missing _fallbackLabel.");
            }

            if (view.IconImage != null
                && view.FallbackLabel != null
                && view.IconImage.gameObject.layer != view.FallbackLabel.gameObject.layer)
            {
                report.Errors.Add(
                    $"InputPromptView on '{ownerLabel}/{view.name}' has mismatched layers between _iconImage ({view.IconImage.gameObject.layer}) and _fallbackLabel ({view.FallbackLabel.gameObject.layer}).");
            }
        }

        private static void ValidateScenes(InputActionAsset expectedAsset, ValidationReport report)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                report.Warnings.Add($"Active scene '{activeScene.path}' has unsaved changes; skipping scene open validation.");
                return;
            }

            string previousScenePath = activeScene.path;

            try
            {
                ValidateBootScene(expectedAsset, report);
                ValidateMainMenuScene(expectedAsset, report);
                ValidateGameplayScene(expectedAsset, report);
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousScenePath))
                {
                    EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
                }
            }
        }

        private static void ValidateBootScene(InputActionAsset expectedAsset, ValidationReport report)
        {
            Scene scene = EditorSceneManager.OpenScene(BOOT_SCENE_PATH, OpenSceneMode.Single);
            ValidateSceneEventSystem(scene, expectedAsset, report);

            List<InputRuntimeDriver> drivers = FindSceneComponents<InputRuntimeDriver>(scene);
            if (drivers.Count != 1)
            {
                report.Errors.Add($"'{BOOT_SCENE_PATH}' expected exactly 1 InputRuntimeDriver, found {drivers.Count}.");
            }
        }

        private static void ValidateMainMenuScene(InputActionAsset expectedAsset, ValidationReport report)
        {
            Scene scene = EditorSceneManager.OpenScene(MAIN_MENU_SCENE_PATH, OpenSceneMode.Single);
            ValidateSceneEventSystem(scene, expectedAsset, report);

            List<UiInputScope> scopes = FindSceneComponents<UiInputScope>(scene);
            if (scopes.Count != 1)
            {
                report.Errors.Add($"'{MAIN_MENU_SCENE_PATH}' expected exactly 1 UiInputScope, found {scopes.Count}.");
            }
            else
            {
                ValidateUiInputScope(scopes[0], MAIN_MENU_SCENE_PATH, report);
            }

            List<MenuInputCoordinator> coordinators = FindSceneComponents<MenuInputCoordinator>(scene);
            if (coordinators.Count != 1)
            {
                report.Errors.Add($"'{MAIN_MENU_SCENE_PATH}' expected exactly 1 MenuInputCoordinator, found {coordinators.Count}.");
            }
            else
            {
                MenuInputCoordinator coord = coordinators[0];
                if (coord.UiInputScope == null || coord.MainMenuRoot == null || coord.MainMenuFirstSelectable == null)
                {
                    report.Errors.Add($"MenuInputCoordinator in '{MAIN_MENU_SCENE_PATH}' has unassigned core references.");
                }

                if (coord.ChildPanels == null || coord.ChildPanels.Length < 2)
                {
                    report.Warnings.Add($"MenuInputCoordinator in '{MAIN_MENU_SCENE_PATH}' has fewer than 2 child panels configured.");
                }
                else
                {
                    for (int i = 0; i < coord.ChildPanels.Length; i++)
                    {
                        var panel = coord.ChildPanels[i];
                        if (panel.PanelRoot == null || panel.DefaultSelectable == null)
                        {
                            report.Errors.Add($"MenuInputCoordinator child panel index {i} is missing PanelRoot or DefaultSelectable.");
                        }
                    }
                }
            }

            List<MainMenuInstaller> installers = FindSceneComponents<MainMenuInstaller>(scene);
            if (installers.Count != 1)
            {
                report.Errors.Add($"'{MAIN_MENU_SCENE_PATH}' expected exactly 1 MainMenuInstaller, found {installers.Count}.");
            }
            else
            {
                SerializedObject so = new SerializedObject(installers[0]);
                if (so.FindProperty("_uiInputScope")?.objectReferenceValue == null)
                {
                    report.Errors.Add($"MainMenuInstaller._uiInputScope is unassigned in '{MAIN_MENU_SCENE_PATH}'.");
                }
            }
        }

        private static void ValidateGameplayScene(InputActionAsset expectedAsset, ValidationReport report)
        {
            Scene scene = EditorSceneManager.OpenScene(GAMEPLAY_SCENE_PATH, OpenSceneMode.Single);
            ValidateSceneEventSystem(scene, expectedAsset, report);

            List<UiInputScope> scopes = FindSceneComponents<UiInputScope>(scene);
            if (scopes.Count != 1)
            {
                report.Errors.Add($"'{GAMEPLAY_SCENE_PATH}' expected exactly 1 UiInputScope, found {scopes.Count}.");
            }
            else
            {
                ValidateUiInputScope(scopes[0], GAMEPLAY_SCENE_PATH, report);
            }

            List<GameplayInputCoordinator> coordinators = FindSceneComponents<GameplayInputCoordinator>(scene);
            if (coordinators.Count != 1)
            {
                report.Errors.Add($"'{GAMEPLAY_SCENE_PATH}' expected exactly 1 GameplayInputCoordinator, found {coordinators.Count}.");
            }
            else
            {
                GameplayInputCoordinator coord = coordinators[0];
                if (coord.UiInputScope == null || coord.PauseFirstSelectable == null || coord.DeathFirstSelectable == null)
                {
                    report.Errors.Add($"GameplayInputCoordinator in '{GAMEPLAY_SCENE_PATH}' has unassigned required references.");
                }
            }

            List<DefaultGameplaySceneInstaller> installers = FindSceneComponents<DefaultGameplaySceneInstaller>(scene);
            if (installers.Count != 1)
            {
                report.Errors.Add($"'{GAMEPLAY_SCENE_PATH}' expected exactly 1 DefaultGameplaySceneInstaller, found {installers.Count}.");
            }
            else
            {
                SerializedObject so = new SerializedObject(installers[0]);
                if (so.FindProperty("_uiInputScope")?.objectReferenceValue == null)
                {
                    report.Errors.Add($"DefaultGameplaySceneInstaller._uiInputScope is unassigned in '{GAMEPLAY_SCENE_PATH}'.");
                }
            }

            List<SkillUpgradePresenter> upgradePresenters = FindSceneComponents<SkillUpgradePresenter>(scene);
            for (int i = 0; i < upgradePresenters.Count; i++)
            {
                SerializedObject so = new SerializedObject(upgradePresenters[i]);
                ValidateSerializedRef<Button>(so, "_firstSkillCardButton", GAMEPLAY_SCENE_PATH, "SkillUpgradePresenter", report);
                ValidateSerializedRef<Button>(so, "_secondSkillCardButton", GAMEPLAY_SCENE_PATH, "SkillUpgradePresenter", report);
                InputPromptView p1 = ValidateSerializedRef<InputPromptView>(so, "_firstSkillPromptView", GAMEPLAY_SCENE_PATH, "SkillUpgradePresenter", report);
                InputPromptView p2 = ValidateSerializedRef<InputPromptView>(so, "_secondSkillPromptView", GAMEPLAY_SCENE_PATH, "SkillUpgradePresenter", report);
                ValidatePromptView(p1, "SkillUpgradePresenter/FirstCard", report);
                ValidatePromptView(p2, "SkillUpgradePresenter/SecondCard", report);
            }

            List<IncreaseDifficultyTotem> totems = FindSceneComponents<IncreaseDifficultyTotem>(scene);
            for (int i = 0; i < totems.Count; i++)
            {
                SerializedObject so = new SerializedObject(totems[i]);
                InputPromptView view = ValidateSerializedRef<InputPromptView>(so, "_interactPromptView", GAMEPLAY_SCENE_PATH, $"IncreaseDifficultyTotem({totems[i].name})", report);
                ValidatePromptView(view, totems[i].name, report);
            }

            List<ScreenOverlayPresenter> overlays = FindSceneComponents<ScreenOverlayPresenter>(scene);
            for (int i = 0; i < overlays.Count; i++)
            {
                ScreenOverlayPresenter overlay = overlays[i];
                if (overlay.OverlayImage != null && overlay.OverlayImage.raycastTarget)
                {
                    report.Errors.Add($"ScreenOverlayPresenter '{overlay.name}' in '{GAMEPLAY_SCENE_PATH}' has raycastTarget enabled on _overlayImage.");
                }

                CanvasGroup group = overlay.GetComponent<CanvasGroup>();
                if (group != null && group.blocksRaycasts)
                {
                    report.Errors.Add($"ScreenOverlayPresenter '{overlay.name}' in '{GAMEPLAY_SCENE_PATH}' has CanvasGroup.blocksRaycasts enabled.");
                }
            }

            List<InputPromptView> sceneViews = FindSceneComponents<InputPromptView>(scene);
            for (int i = 0; i < sceneViews.Count; i++)
            {
                ValidatePromptView(sceneViews[i], GAMEPLAY_SCENE_PATH, report);
            }
        }

        private static T ValidateSerializedRef<T>(
            SerializedObject so,
            string propName,
            string scenePath,
            string ownerName,
            ValidationReport report) where T : UnityEngine.Object
        {
            T value = so.FindProperty(propName)?.objectReferenceValue as T;
            if (value == null)
            {
                report.Errors.Add($"{ownerName}.{propName} is unassigned in '{scenePath}'.");
            }

            return value;
        }

        private static void ValidateSceneEventSystem(Scene scene, InputActionAsset expectedAsset, ValidationReport report)
        {
            List<EventSystem> eventSystems = FindSceneComponents<EventSystem>(scene);
            if (eventSystems.Count != 1)
            {
                report.Errors.Add($"'{scene.path}' expected exactly 1 EventSystem, found {eventSystems.Count}.");
                return;
            }

            EventSystem eventSystem = eventSystems[0];
            if (eventSystem.GetComponent<StandaloneInputModule>() != null)
            {
                report.Errors.Add($"'{scene.path}' still contains legacy StandaloneInputModule.");
            }

            InputSystemUIInputModule uiModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null)
            {
                report.Errors.Add($"'{scene.path}' EventSystem is missing InputSystemUIInputModule.");
                return;
            }

            if (expectedAsset != null && uiModule.actionsAsset != expectedAsset)
            {
                report.Errors.Add($"'{scene.path}' InputSystemUIInputModule.actionsAsset is not set to InputSystem_Actions.");
            }

            if (uiModule.cancel != null && uiModule.cancel.action != null)
            {
                report.Errors.Add($"'{scene.path}' InputSystemUIInputModule.cancel must be unassigned (null).");
            }

            if (uiModule.deselectOnBackgroundClick)
            {
                report.Errors.Add($"'{scene.path}' InputSystemUIInputModule.deselectOnBackgroundClick must be false.");
            }

            if (uiModule.move == null || uiModule.submit == null || uiModule.point == null || uiModule.leftClick == null || uiModule.scrollWheel == null)
            {
                report.Errors.Add($"'{scene.path}' InputSystemUIInputModule is missing one or more required action references (move/submit/point/leftClick/scrollWheel).");
            }
        }

        private static void ValidateUiInputScope(UiInputScope scope, string scenePath, ValidationReport report)
        {
            if (scope.EventSystem == null)
            {
                report.Errors.Add($"UiInputScope._eventSystem is unassigned in '{scenePath}'.");
            }

            if (scope.UiInputModule == null)
            {
                report.Errors.Add($"UiInputScope._uiInputModule is unassigned in '{scenePath}'.");
            }
        }

        private static List<T> FindSceneComponents<T>(Scene scene) where T : Component
        {
            List<T> results = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T[] components = roots[i].GetComponentsInChildren<T>(includeInactive: true);
                for (int c = 0; c < components.Length; c++)
                {
                    results.Add(components[c]);
                }
            }

            return results;
        }
    }
}
