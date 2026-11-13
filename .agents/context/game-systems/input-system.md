# Input System Documentation

## Purpose

The Input system owns centralized runtime input capture, action map gating, context precedence, prioritized command dispatch, active device/gamepad family detection, and device-aware UI glyph/text prompt resolution across the Boot, MainMenu, and RuinedBloodCity scenes.

It is responsible for:
- Owning the project Action Asset (Assets/InputSystem/InputSystem_Actions.inputactions) under the exclusive New Input System backend (activeInputHandler = 1).
- Providing continuous gameplay driving and interaction state (IGameplayInput), ordered discrete commands (IInputCommands), stack-like modal context gating (IInputContexts), active device/family tracking (IInputDeviceContext), shared UI action source (IInputActionSource), and lifecycle control (IInputRuntime) via Assets/Scripts/Input/GameInputService.cs.
- Resolving device-aware glyph sprites and fallback text labels via Assets/Scripts/Input/InputPromptService.cs (IInputPromptProvider) backed by Assets/ScriptableObjects/Input/InputGlyphCatalog.cs and Assets/ScriptableObjects/Input/InputRuntimeConfig.cs.
- Persisting across scene loads via Assets/Scripts/Input/InputRuntimeDriver.cs, bootstrapped by Assets/Scripts/ReflexDI/ProjectInstaller.cs on container build (and guarded by IInputRuntime.TryAcquireDriver so Boot-scene instances yield when a container-spawned driver is active).
- Coordinating UI focus, dropdown keyboard/gamepad navigation and cancel focus retention, text-field cancel handling, child panel stacking, and prompt binding through Assets/Scripts/UI/Input/UiInputScope.cs, MenuInputCoordinator.cs, GameplayInputCoordinator.cs, and InputPromptView.cs.
- Synchronizing OS cursor visibility with the active input scheme and normalizing gamepad face-button layouts (including Switch Pro printed-letter overrides) via GameInputService.
- Validating input action maps, prompt assets, project installers, and UI wiring in Editor via Assets/Scripts/Editor/Tools/InputConfigurationValidator.cs.

It is not responsible for:
- Local multiplayer, PlayerInputManager, custom raw HID Joystick layouts, touch/XR input, virtual cursor simulation, haptics, or runtime control remapping UI.
- Owning gameplay physics (CarController), UI panel animations, pause state (GameTime), or upgrade selection logic.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Input/Constants/InputConstants.cs
  - Assets/Scripts/Input/InputCommand.cs
  - Assets/Scripts/Input/GameInputService.cs
  - Assets/Scripts/Input/InputPromptService.cs
  - Assets/Scripts/Input/InputRuntimeDriver.cs
  - Assets/ScriptableObjects/Input/InputGlyphCatalog.cs
  - Assets/ScriptableObjects/Input/InputRuntimeConfig.cs
  - Assets/Scripts/UI/Input/InputPromptView.cs
  - Assets/Scripts/UI/Input/UiInputScope.cs
  - Assets/Scripts/UI/Input/MenuInputCoordinator.cs
  - Assets/Scripts/UI/Input/GameplayInputCoordinator.cs
- Editor validation tool:
  - Assets/Scripts/Editor/Tools/InputConfigurationValidator.cs
- Action & configuration assets:
  - Assets/InputSystem/InputSystem_Actions.inputactions
  - Assets/ScriptableObjects/Input/InputGlyphCatalog.asset
  - Assets/ScriptableObjects/Input/InputRuntimeConfig.asset
- DI Installers:
  - Assets/Scripts/ReflexDI/ProjectInstaller.cs
  - Assets/Scripts/ReflexDI/MainMenuInstaller.cs
  - Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs
- Primary consumers:
  - Assets/Scripts/Player/Car/CarController.cs
  - Assets/Scripts/Interactables/IncreaseDifficultyTotem.cs
  - Assets/Scripts/UI/Pause/PausePresenter.cs
  - Assets/Scripts/UI/Death/PlayerDeathPresenter.cs
  - Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs
  - Assets/Scripts/UI/Skills/SkillUpgradeButton.cs
  - Assets/Scripts/UI/DevConsole/DevConsolePresenter.cs
  - Assets/Scripts/Effects/UIElementEffects.cs
  - Assets/Scripts/PowerUps/Presentation/ScreenOverlayPresenter.cs

## Architecture and Data Flow

- Core components and colocated interfaces:
  - GameInputService (Assets/Scripts/Input/GameInputService.cs) implements:
    - IGameplayInput: exposes MoveInput, IsBrakePressed, ReverseInput (analog float from Player/Reverse), WasInteractPressedThisFrame, OnBrakeStarted, and OnBrakeCanceled.
    - IInputCommands: exposes OnCommand ordered dispatch.
    - IInputContexts: manages scene epoch, context generation, modal scope tokens (AcquireScope, ReleaseScope), live-reward slot count (SetLiveRewardEligibleSlots, IsSlotEligible), eligibility queries (IsUiNavigationEligible, IsRewardChoiceEligible, IsBackEligible, IsGameplayEligible, IsInteractEligible), ConsumeActiveControlsUntilRelease, RegisterSceneReady, and ReapplyPolicy.
    - IInputDeviceContext: exposes ActiveScheme (KeyboardMouse or Gamepad), EffectiveFamily (KeyboardMouse, Xbox, PlayStation, Switch, GenericGamepad, Unavailable), KeyboardTheme (Light or Dark), availability flags, ActiveGamepad, and ActiveKeyboard.
    - IInputActionSource: exposes ActionAsset, TryGetAction, and GetRequiredAction for UI modules, settings controls, and prompt resolution.
    - IInputRuntime: TryAcquireDriver, Initialize, SampleDeviceActivity, DispatchPendingCommands, SetApplicationFocus, and Suspend.
  - InputPromptService (Assets/Scripts/Input/InputPromptService.cs) implements IInputPromptProvider and raises OnPromptsChanged whenever device family, keyboard theme, or active bindings change. Resolves InputPromptDescriptor (IsValid, IsBound, IsAvailable, Family, PrimarySprite, PrimaryLabel, ControlPath, Parts, Alternatives) with descriptor caching.
  - InputRuntimeDriver (Assets/Scripts/Input/InputRuntimeDriver.cs, DefaultExecutionOrder(-500)) is spawned and injected by ProjectInstaller.OnContainerBuilt (DontDestroyOnLoad) so direct non-Boot scene launches initialize IInputRuntime before scene Awake; Boot.unity's scene-placed InputRuntimeDriver checks TryAcquireDriver() in Awake and destroys itself if a driver is already active. Calls Initialize on Awake, runs SampleDeviceActivity and DispatchPendingCommands each Update, forwards OnApplicationFocus, and calls Suspend on OnDestroy.
  - UiInputScope (Assets/Scripts/UI/Input/UiInputScope.cs) configures InputSystemUIInputModule with cancel unassigned (null) and deselectOnBackgroundClick = false, updates directional navigation for dynamic TMP_Dropdown lists, scrolls ScrollRect containers into view, restores focus when switching from mouse to gamepad without unintended activation, retains focus on a closed TMP_Dropdown after selection/collapse, and handles Cancel on expanded dropdowns or focused input fields via TryHandleWidgetCancel. When cancelling an expanded TMP_Dropdown, PrepareDropdownListForImmediateCancel immediately hides/disables the spawned Dropdown List and tracks _closingDropdown until !IsExpanded so TMP_Dropdown.Hide()'s fade coroutine cannot bounce selection back into the dying list.
  - MenuInputCoordinator and GameplayInputCoordinator acquire Menu and Dying/scene scopes, manage child panel focus stacks (Settings, ScoreBoard, Pause child panels), restore last valid parent selectables, and bind Back InputPromptView indicators.

- Action maps & control schemes (Assets/InputSystem/InputSystem_Actions.inputactions):
  - Maps: Player (Move with stickDeadzone(min=0.125,max=0.925) on Gamepad leftStick, Brake on Space / Gamepad buttonEast, Reverse as Value/Axis on Gamepad rightTrigger, Interact on E / Gamepad buttonSouth, Look), Global (Pause, ToggleConsole), UI (Navigate, Submit, Cancel, Point, Click, RightClick, MiddleClick, ScrollWheel), Upgrade (SelectChoice1..3 on 1/2/3, Numpad1..3, and Gamepad buttonNorth, buttonWest, buttonSouth [Y, X, A top-to-bottom]), DevConsole (Close, Submit, HistoryPrevious, HistoryNext).
  - Control schemes: Keyboard&Mouse, Gamepad, Touch, Joystick, XR.

- Runtime command priority, cursor sync & Switch overrides:
  - Command priority order per frame: ToggleConsole (0) -> DevConsoleClose (1) -> Pause (2) -> UiCancel (3) -> DevConsoleHistory/Submit (4) -> UpgradeSelectChoice (5).
  - Context precedence (highest to lowest): Loading -> Dying -> Death -> DevConsole -> Menu -> PauseChildPanel -> Pause -> LiveReward -> None.
  - LiveReward keeps Player Move, Brake, and Reverse active alongside Upgrade shortcuts without pausing GameTime, while Pause, PauseChildPanel, DevConsole, Death, and Menu suppress Player Move/Brake/Reverse/Interact and emit one-shot OnBrakeCanceled when transitioning out of gameplay eligibility.
  - Stale commands buffered across scene loads or context transitions are dropped via SceneEpoch and ContextGeneration checks plus per-control awaiting-release rearming (AddControlAwaitingRelease / ConsumeActiveControlsUntilRelease).
  - SyncCursorVisibility() hides Cursor.visible when ActiveScheme == InputScheme.Gamepad and restores it on KeyboardMouse (and on Dispose()) while keeping CursorLockMode.None so mouse delta detection continues working.
  - SyncSwitchFaceButtonOverrides() applies runtime Gamepad binding overrides whenever EffectiveFamily transitions to/from InputDeviceFamily.Switch (before OnDeviceChanged fires) so Nintendo face buttons match printed letters: SelectChoice1..3 -> buttonWest [Y], buttonNorth [X], buttonEast [A]; Brake, Cancel, DevConsole/Close -> buttonSouth [B]; Interact and Submit -> buttonEast [A]. Overrides are cleared on family exit and Dispose().

- Prompt resolution pipeline:
  - Normalizes effective paths using InputControlLayout.FindControlLayout and device family matching: DualShockGamepad or Sony/PlayStation HID metadata (layout/manufacturer/product/VID 0x054c) -> PlayStation, SwitchProControllerHID or Switch metadata -> Switch, XInputController or Xbox metadata -> Xbox, other Gamepad -> GenericGamepad.
  - Unsupported HID Joysticks log a single warning and do not drive Gamepad glyph selection.
  - Composite keyboard bindings (e.g. WASD + Arrow keys) expand into Parts and Alternatives when matched by InputGlyphCatalog, falling back to concise neutral text (such as "Y / X / A" or positional fallback labels on GenericGamepad, and concise key text on non-Latin or unmatched keyboard layouts).

## Rules and Invariants

- Critical behavior rules:
  - Keep ProjectSettings/ProjectSettings.asset activeInputHandler set to 1 (Input System Package (New) only). Never reintroduce legacy UnityEngine.Input calls or InputActionAsset.FindAction lookups in gameplay/UI callers outside GameInputService.
  - Never pause GameTime during LiveReward context; left-stick driving and braking remain live while choosing rewards via buttons or number/gamepad choice shortcuts.
  - Keep InputSystemUIInputModule.cancel unassigned (null) and deselectOnBackgroundClick disabled; Back / Cancel actions flow exclusively through IInputCommands (InputCommandKind.UiCancel / DevConsoleClose) to preserve modal stack order and widget cancellation.
  - Validate runtime input dependencies on spawned world objects (such as IncreaseDifficultyTotem) in Start() rather than Awake() when spawned via Instantiate followed by Reflex GameObjectInjector.InjectRecursive, because Unity executes Awake() synchronously before InjectRecursive runs.
  - Ensure world-space InputPromptView _iconImage and _fallbackLabel GameObjects share the same Unity layer (e.g., Layer 16 WorldUI on SkillUpgradePresenter/UpgradeButton, Layer 17 Interactables on IncreaseDifficultyTotem) and world-space TMP font sizing so camera culling never hides fallback text; enforced by InputConfigurationValidator.ValidatePromptView.
  - Never renumber existing InputActionId enum values (PlayerReverse = 13 is appended after PlayerInteract = 12) so serialized InputPromptView._defaultActionId values remain stable.
  - Suppress ScreenOverlayPresenter CanvasGroup and Image raycasts in Awake() so full-screen visual tint/vignette overlays never intercept UI pointer hits.
  - Keep UIElementEffects scale tween bookkeeping zero-stale across disable/destroy transitions (OnSelect/OnDeselect and pointer hover reset baseline scale cleanly).

## Validation

- Run Editor configuration validation via Tools -> Input -> Validate Input Configuration (Assets/Scripts/Editor/Tools/InputConfigurationValidator.cs) whenever modifying action maps, glyphs, runtime config, DI installers, or UI prompt bindings (verifies Player/Reverse Axis control type, rightTrigger catalog entry, matching icon/fallback layers, and scene/prefab wiring).
- Compile check:
  - dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
  - dotnet build Assembly-CSharp-Editor.csproj -p:BuildProjectReferences=false

## Known Risks and Open Questions

- Known limitations:
  - Assets/Addons/Xelu_Free_Controller&Key_Prompts contains Light/Dark themes only for Keyboard; Xbox and Switch PNGs are dark-styled only and PlayStation face/dpad/system glyphs are light gray with dark sticks/triggers.
  - Physical hardware gamepad verification (Xbox, PlayStation DualShock/DualSense, Switch Pro, GenericGamepad) and non-US keyboard layout testing remain manual Editor/device checks.
