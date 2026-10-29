# Specification: Unified Unity Input System and Controller Support

**Date:** 2026-10-11  
**Author:** Codex  
**Status:** Complete specification; control-design questions resolved. Runtime implementation requires a separate request and the before-code review gate in AGENTS.md.  
**Target Systems:** Input, Player/Car, UI, Interactables, Dev Console, ReflexDI, Settings.

## 1. Overview and Player Experience

Complete the existing Unity Input System adoption, support controller-only gameplay and menu navigation, and present accurate binding hints for the active device through one shared, designer-authored glyph catalog.

The current implementation already uses the new package. The migration concerns action-based access, centralized ownership, missing controller bindings, menu focus, dynamic prompts, and removal of the legacy backend after verification.

### Authorized Scope

- Specify complete adoption of the new Unity Input System.
- Specify gamepad support for gameplay and player menus.
- Specify global button-glyph mappings using the existing graphics pack.
- Include design recommendations and an implementation breakdown.
- Write documentation only; do not modify runtime code or Unity assets.

### Delivery Boundaries

The implementation target is the existing single-player desktop prototype, with Windows Editor/Standalone as the initial hardware-validation environment. Support keyboards, mice, and controllers recognized by the installed package as Gamepad devices. Xbox-compatible, supported PlayStation, and supported Switch Pro devices have dedicated presentation families. Available Switch graphics do not imply a Nintendo console port or support for every connection mode.

Exclude local multiplayer, arbitrary Joystick/HID layout authoring, touch/XR development, virtual mouse navigation, vibration, trigger-based driving, input remapping screens, and binding persistence. Keep the installed Unity/package versions. Existing unused touch/XR bindings are not a reason to broaden this migration or delete unrelated data.

## 2. Open Questions and Resolved Decisions

### Resolved Decisions

- The user requested a specification, not runtime implementation.
- **Q1 resolved by the user:** Preserve left-stick driving and the existing brake binding. X controls steering; Y controls forward/reverse acceleration. Preserve WASD, arrow keys, Space, analog behavior, drift, and vehicle tuning.
- **Q2 resolved by the user:** Assign direct controller buttons to the displayed reward cards. Preserve keyboard 1/2/3 and numpad equivalents. General menus use normal navigation/Submit; reward cards use their dedicated selection actions.
- **Q3 resolved by the user:** Defer remapping UI and saved binding overrides. Resolve hints from effective bindings so a later remapping feature does not require replacing the glyph system.
- Use the installed Input System package and preserve Reflex dependency injection.
- Reuse existing keyboard and controller graphics instead of generating replacements.
- Use a shared ScriptableObject catalog for device/control-to-sprite mappings; resolve action bindings before choosing graphics.
- Preserve existing keyboard shortcuts and gameplay balance unless the user approves a control-design change.
- Maintain English repository documentation as required by AGENTS.md.

### Open Questions

None for this specification. The new bindings, routing rules, service contracts, and editor assignments below are concrete implementation proposals within the resolved design. Physical controller verification remains implementation work, not an unresolved requirement.

### User Review Before Runtime Work

Review this complete specification before a separately requested implementation begins. It explicitly proposes new action maps, a glyph catalog and runtime configuration asset, scene/prefab assignments, and input suppression rules. The present request authorizes the documents only. Unity serialized assets must be authored through the Editor; this specification does not authorize direct YAML/meta edits.

## 3. Verified Current State

- Packages/manifest.json declares com.unity.inputsystem 1.19.0.
- ProjectSettings/ProjectVersion.txt declares Unity 6000.4.5f1.
- ProjectSettings/ProjectSettings.asset contains activeInputHandler: 2, corresponding to Both. The eventual target is Input System Package (New), changed through Unity Editor after dependency verification.
- Assets/InputSystem/InputSystem_Actions.inputactions already defines Player and UI action maps.
- Player/Move includes keyboard direction composites and the gamepad left stick. Player/Brake includes keyboard Space and gamepad buttonWest. Player/Pause currently has only keyboard Escape.
- Assets/Scripts/Player/Car/CarController.cs resolves Move and Brake from InputSystem.actions directly.
- Assets/Scripts/UI/Pause/PausePresenter.cs resolves Pause directly and controls GameTime through its existing UI flow.
- Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs polls Keyboard.current for 1/2/3 and numpad equivalents, with a same-frame selection guard and a queued reward flow.
- SkillUpgradePresenter does not pause GameTime or suppress Player input while a reward is visible. Vehicle physics and gameplay continue. Changing this behavior would be a separate player-facing design decision.
- Assets/Scripts/Interactables/IncreaseDifficultyTotem.cs polls the E key directly.
- Assets/Scripts/UI/DevConsole/DevConsolePresenter.cs polls keyboard commands and directly disables/re-enables the Player action map.
- Assets/ScriptableObjects/UI/SkillUpgradeKeyboardIconMapping.cs and its asset currently map numbers 1/2/3 to sprites for SkillUpgradeButton.
- Assets/Addons/Xelu_Free_Controller&Key_Prompts/ contains Keyboard Dark, Keyboard Light, Xbox, Playstation, and Switch artwork.
- Assets/Scripts/ReflexDI/ProjectInstaller.cs supplies project-wide plain C# services; BootLoader supplies persistent component references to scene containers. The final service lifetime must follow the inspected setup.
- Source searches found no legacy UnityEngine.Input polling or KeyCode usage in C# files under Assets/. Device-specific Keyboard.current polling is already part of the new package; it is an abstraction and controller-coverage problem, not an old-backend API.
- Serialized searches found no StandaloneInputModule references in project scenes/prefabs/assets. The checked scenes already use InputSystemUIInputModule.

### UI Asset Mismatch and Focus

- ProjectSettings/EditorBuildSettings.asset registers the project action asset with GUID 2bcd2660ca9b64942af0de543d8d7100. Its authored path is Assets/InputSystem/InputSystem_Actions.inputactions.
- Assets/Scenes/Boot.unity, Assets/Scenes/MainMenu.unity, and Assets/Scenes/RuinedBloodCity.unity reference a different action asset in their UI modules: GUID ca9f5fa95ffab41fb9a615ab714db018.
- Resolving that GUID against the installed package identifies Unity's package-owned DefaultInputActions.inputactions. The package cache was inspected only to identify framework behavior and this dependency; it is not an authored project source.
- All three checked EventSystems have no first selected object and enable background-click deselection. Bindings alone therefore do not establish usable controller focus.
- The project UI map already contains gamepad navigation and wildcard Submit/Cancel usage bindings. Its existence does not establish that scene UI uses that map.
- New-skill cards require explicit activation wiring for controller actions; the presenter currently selects them through keyboard shortcuts. The resolved direct-card model does not require stick navigation between these cards.

### Existing Prompt Surfaces

| Surface | Current authored data | Migration concern |
| --- | --- | --- |
| Generated stat-upgrade buttons | Assets/Prefabs/UI/SkillsUpgrader/UpgradeButton.prefab; _selectKeyImage and _keyboardIconMapping | Replace number-only sprite resolution with active-action prompts without renaming unrelated serialized fields. |
| New-skill choice cards | Assets/Prefabs/UI/SkillsUpgrader/SkillUpgradePresenter.prefab; two static digit sprites | Add shared prompt views and direct action activation for slots 1 and 2. |
| Difficulty totem | Assets/Prefabs/Interactables/IncreaseDifficultyTotem.prefab; KeyE_Image | Bind the displayed glyph to the same Interact action that performs the interaction. |
| Main menu and settings | Assets/Prefabs/UI/Menus/MainMenu.prefab and Assets/Prefabs/UI/Controlls/ | Provide deterministic focus, navigation, Submit/Cancel hints, and dropdown/slider support. |
| Pause/death | Assets/Prefabs/UI/PausePresenter.prefab and Assets/Prefabs/UI/PlayerDeathPresenter.prefab; gameplay-scene instances and nested controls | Restore focus on modal changes and inspect scene overrides; preserve existing UnityEvent callbacks. |

The graphics folders currently contain 83 Keyboard Dark PNGs, 83 Keyboard Light PNGs, 20 Xbox PNGs, 22 Playstation PNGs, and 25 Switch PNGs. The pack README describes a larger upstream collection; do not claim all upstream images are present. Controller filenames are numbered and do not define their semantic meaning. Assign mappings by visual inspection and actual Sprite references in the Unity Editor, not filename-number guesses. A checked Xbox texture is already imported as a Sprite; validate the complete selected set during implementation.

## 4. Data Model, Actions, and Serialization

### 4.1 One Authored Action Asset

Extend Assets/InputSystem/InputSystem_Actions.inputactions through Unity's Input Actions editor. Keep its asset GUID, existing Move/Brake action IDs, keyboard alternatives, movement composite behavior, processors, and brake interactions. The project-wide setting, runtime owner, native UI module, and prompt resolver must use this same asset instance.

Use Keyboard&Mouse and Gamepad as the supported binding groups. Group new bindings explicitly; do not use controller product names as gameplay control schemes. Separate action ownership from presentation families. In particular, do not set the whole asset's binding mask to Gamepad merely because pad graphics are visible: keyboard and mouse must remain usable.

Retain existing unused Look/touch/XR data unless a dependency audit justifies removal. Replace wildcard UI Submit/Cancel bindings with explicit supported-device bindings so the prompt resolver can identify the actual control. Retire Player/Pause only after its consumer has migrated to Global/Pause and serialized references have been audited.

| Action | Type | Keyboard/mouse | Gamepad | Owner |
| --- | --- | --- | --- | --- |
| Player/Move | Value, Vector2 | Existing WASD and arrows | leftStick | Car input state; preserve current X/Y meaning. |
| Player/Brake | Button | Space | buttonWest | Car input state and existing brake events. |
| Player/Interact | Button | E | buttonSouth | Eligible world interactions during ordinary gameplay. |
| Global/Pause | Button | Escape | start | Open pause during gameplay or a live reward. |
| Global/ToggleConsole | Button | backquote | None | Existing development-only console availability. |
| UI/Navigate | PassThrough, Vector2 | WASD and arrows | leftStick and dpad | General menus only; rewards do not use navigation. |
| UI/Submit | Button | Enter and numpad Enter | buttonSouth | Native selectable activation in general menus. |
| UI/Cancel | Button | Escape | buttonEast and start | Central back routing in general menus, including pause. |
| UI/Point | PassThrough, Vector2 | Mouse position | None | Native pointer interaction. |
| UI/Click | PassThrough, Button | Mouse left button | None | Native pointer interaction. |
| UI/ScrollWheel | PassThrough, Vector2 | Mouse scroll | None | Existing scroll controls. |
| Upgrade/SelectChoice1 | Button | 1 and numpad 1 | buttonSouth | Visible reward slot 1. |
| Upgrade/SelectChoice2 | Button | 2 and numpad 2 | buttonNorth | Visible reward slot 2. |
| Upgrade/SelectChoice3 | Button | 3 and numpad 3 | buttonEast | Visible reward slot 3. |
| DevConsole/Close | Button | Escape | buttonEast | Close the supported console. |
| DevConsole/Submit | Button | Enter and numpad Enter | None | Submit keyboard text once. |
| DevConsole/HistoryPrevious | Button | Up arrow | None | Existing command history. |
| DevConsole/HistoryNext | Button | Down arrow | None | Existing command history. |

Use press-only command activation with initial-state checks disabled for selection, Pause, Cancel, Interact, and console commands. Preserve Brake's existing started/canceled semantics and Move's Value initial-state behavior. UI RightClick/MiddleClick and any retained pointer references continue to point to this asset; they do not gain new gameplay behavior.

### 4.2 Reward Slots and Controller Labels

| Semantic control | Xbox | PlayStation | Switch positional convention | Gameplay use |
| --- | --- | --- | --- | --- |
| buttonSouth | A | Cross | B | Slot 1; Submit/Interact in their eligible contexts. |
| buttonNorth | Y | Triangle | X | Slot 2. |
| buttonEast | B | Circle | A | Slot 3; Cancel outside reward selection. |
| buttonWest | X | Square | Y | Existing brake; never a reward-slot shortcut. |
| start | Menu/Start | Options | Plus | Pause or menu back, according to context. |

Bindings use these canonical controls, not printed letters. Verify the physical positions and chosen graphics on each supported controller. A translated device may expose another layout and require a presentation override; do not reinterpret its action bindings based only on its physical branding.

New-skill selection uses slots 1 and 2. Stat upgrades use the existing ordered slots 1 through 3. Show a prompt only for an available, visible, enabled option. If a future reward exposes more than three options, extend the selection contract deliberately; do not wrap indices or assign duplicate shortcuts.

### 4.3 Shared Glyph Catalog

New type: Assets/ScriptableObjects/Input/InputGlyphCatalog.cs. New asset: Assets/ScriptableObjects/Input/InputGlyphCatalog.asset.

The catalog contains serialized entries with presentation family, canonical control path, keyboard theme where relevant, Sprite reference, and readable/printed label. Supported families are KeyboardMouse, Xbox, PlayStation, Switch, and GenericGamepad. Keyboard Light/Dark are themes, not separate input schemes. Example entries pair Xbox + buttonSouth with an A sprite and PlayStation + buttonSouth with a Cross sprite; no entry hard-codes an action such as SelectChoice1 to A.

Store canonical controls such as e, digit1, numpad1, space, escape, enter, leftButton, leftStick, dpad, buttonSouth, and start. Composite-part requests use their actual part controls. Cache lookup keys and indexes once at initialization; inspector arrays remain the editable source. Fail validation on duplicate family/theme/control keys or malformed required entries. A missing decorative Sprite may use its readable label; a missing required catalog reference is an initialization error.

Initial coverage includes all new bindings above, keyboard movement hints, and pad navigation hints. GenericGamepad must provide neutral positional labels such as South Button and West Button instead of incorrectly claiming Xbox lettering. Unknown keyboard keys or layout-label mismatches use the package's actual display label. Missing artwork never hides a required mechanical action.

### 4.4 Runtime Configuration and State

New type/asset: Assets/ScriptableObjects/Input/InputRuntimeConfig.cs and Assets/ScriptableObjects/Input/InputRuntimeConfig.asset.

Keep presentation thresholds, keyboard theme, and an optional controller-family override in this designer-authored configuration. Initial proposed activity thresholds are 0.35 stick magnitude to enter meaningful activity, 0.20 to rearm, 0.10 stick-vector change for renewed analog activity, and 4 screen pixels of net mouse displacement within an unscaled 0.10-second window. Opposing tiny mouse movements must not accumulate into artificial activity. These thresholds affect device-hint switching only; they must not replace the existing gameplay stick deadzone or steering/acceleration values. Buttons and explicit navigation switch hints immediately. No mandatory debounce delays commands.

Runtime state belongs to service instances: active presentation scheme/device/family, last meaningful activity, current scene epoch, modal/context generation, held-control rearm state, and transient accepted commands. The catalog/configuration assets are never used as mutable global runtime state. No new persistent setting or AppStorage key is required for this migration.

### 4.5 Serialization Rules

Use _camelCase private serialized fields, English labels/tooltips, and injected fields before serialized fields. Preserve existing unrelated field names and UnityEvent callback names. Keep _selectKeyImage available for the current image; migrate its renderer to a prompt view without renaming the field as incidental cleanup.

Do not use FormerlySerializedAs. Keep _keyboardIconMapping and its old asset during staged editor migration, then retire them only after every authored reference is replaced and verified. Do not maintain the old mapping as a runtime fallback. Assign new ProjectScope, scene, card, and totem references through the Editor. Any script/asset deletion must include the corresponding Unity-managed metadata lifecycle and a reference audit.

## 5. Architecture and Reflex DI Contracts

### 5.1 Runtime Owner

New primary implementation: Assets/Scripts/Input/GameInputService.cs. Colocate its narrow interfaces above the implementation:

- IGameplayInput: MoveInput, IsBrakePressed, WasInteractPressedThisFrame, OnBrakeStarted, and OnBrakeCanceled. Consumers receive neutral values when their context is ineligible.
- IInputCommands: OnCommand carrying an immutable InputCommand value with command kind/slot, source device/control identity, input-update/frame identity, scene epoch, and context generation. Do not retain InputAction.CallbackContext values.
- IInputContexts: register scene readiness, acquire/release modal scopes, inspect eligibility, and emit OnContextChanged. A scope token identifies its scene epoch and owner; releasing one scope cannot release another.
- IInputDeviceContext: read-only presentation state and OnDeviceChanged. Prompt consumers receive this narrow contract rather than map-mutating APIs.
- IInputActionSource: cached access to the authoritative actions for the UI adapter and prompt resolver. Gameplay/UI presenters do not enable/disable returned actions.
- IInputRuntime: Initialize, DispatchPendingCommands, SetApplicationFocus, and Suspend; used only by the Boot driver. Initialization is idempotent, while final unsubscription/disposal belongs to the service's IDisposable implementation.

New supporting value types: Assets/Scripts/Input/InputCommand.cs. Use stable explicit enum values for serialized action IDs and keep the fully qualified action-name constants under Assets/Scripts/Input/Constants/. Resolve each required action once, with validation of expected type/control type. No string lookups in Update/FixedUpdate.

GameInputService is a project-container service. It borrows the configured project-wide InputActionAsset; it does not instantiate PlayerInput, create a second action asset for production, or destroy/dispose the authored asset. Initialize explicitly before scene loading because project-wide actions start enabled by default. It owns action eligibility, command admission, brake reset, and presentation-device detection. The native UI adapter is its controlled framework bridge, not a competing owner.

### 5.2 Prompt Resolver

New implementation/contract: Assets/Scripts/Input/InputPromptService.cs with IInputPromptProvider colocated above it.

The provider accepts an action ID and presentation request, returns a cached immutable descriptor/sequence, and emits OnPromptsChanged. A request can select a composite part or primary binding. It depends on IInputActionSource, IInputDeviceContext, and InputGlyphCatalog. It does not own gameplay, modal state, or time scale.

### 5.3 Registration and Startup

Assets/Scripts/ReflexDI/ProjectInstaller.cs gains serialized references to the action asset, catalog, and runtime configuration. Register these values, then one GameInputService instance under its contracts and one InputPromptService under IInputPromptProvider using the installed Reflex 12.0.1 AddSingleton APIs. Type registration handles constructor dependencies; do not create separate owners for each interface.

Assign those references in Assets/Resources/ProjectScope.prefab. New Assets/Scripts/Input/InputRuntimeDriver.cs is a Boot-scene component injected with IInputRuntime. Its Awake initializes the service before BootLoader.Start begins asynchronous loading. Its Update dispatches the transient command batch after Dynamic input processing and before scene UI processing; use explicit execution ordering and verify it in Play Mode. It forwards application-focus changes and suspends input on shutdown. IDisposable on project services performs final cleanup.

New scene adapters: Assets/Scripts/UI/Input/UiInputScope.cs, Assets/Scripts/UI/Input/MenuInputCoordinator.cs, and Assets/Scripts/UI/Input/GameplayInputCoordinator.cs. Serialize their EventSystem, InputSystemUIInputModule, screen roots, default selectables, and native widget references. Menu/gameplay coordinators supply their own scene context; the gameplay coordinator observes the injected IPlayerManager health lifecycle. New adapter instance bindings, where needed by presenters, belong in MainMenuInstaller.cs or DefaultGameplaySceneInstaller.cs. Do not inject gameplay-only dependencies into the menu scene or bind scene-owned UI at project lifetime.

Existing BootLoader audio and damage-number registration remains intact. Use IGameSceneLoader.OnStartLoadingScene and OnSceneLoaded for transition suppression/readiness; verify the actual GameSceneLoader ordering rather than outdated documentation filenames. Direct gameplay-scene launch without Boot is not added as a new workflow by this feature.

### 5.4 Command Dispatch and Ownership

Capture eligibility and scene/context generation at action performance. Copy only value data into a reused transient batch. Dispatch commands to eligible subscribed presenters without letting a later map/context change reinterpret an earlier press. Any command stamped for a released context or old scene is discarded.

Pause/Cancel/console transitions have priority over card activation in the same batch. Preserve the existing slot order 1, then 2, then 3 for simultaneous choice shortcuts. A frame containing such a modal transition must not also resolve a reward or world interaction. Keep the existing presenter frame guard for mixed mouse/keyboard/controller activation, and additionally require release before a consumed control can activate a new reward/context.

Native Submit belongs exclusively to InputSystemUIInputModule. Central Cancel belongs exclusively to the router: deliberately leave the module's cancel reference unassigned and handle UI/Cancel in the scoped coordinator. If a registered dropdown is expanded, close that widget first; if a registered text field is editing, finish/cancel that edit first; otherwise send Back to the top eligible screen. Use explicit widget references and the installed TMP contracts; do not depend on cancel events bubbling from a selected button to a panel.

During the native module's OnEnable, it may enable referenced actions. UiInputScope must bind only the currently eligible navigation/Submit references, keep the shared action asset, and reapply the owner's action policy after module initialization. Detach general-menu navigation/Submit while live rewards are active. Never wire a second action asset or call AssignDefaultActions as a fallback. Clear/rebind pointers and selection safely during context changes; a consumed Submit/Click must remain blocked across a transition until released.

## 6. Prompt Resolution and UI Integration

### 6.1 Resolution Pipeline

1. Resolve the stable action ID against the authoritative action source.
2. Choose a binding for the active presentation scheme using binding groups and the binding's effective path. Prefer the binding producing meaningful input when it is applicable; otherwise use authored binding order as the primary preference.
3. Resolve generic controls/usages to the actual active device when available. Derive presentation family from the actual device layout, not from a generic Gamepad binding string.
4. Normalize the semantic control path and look it up in the shared catalog for family/theme.
5. Return the Sprite plus readable label; if artwork is unavailable or inaccurate, render the correct label. Keep explicit Unbound/Unavailable state distinct from an unknown cosmetic glyph.

Use GetBindingDisplayString and the package's control-path information for display, honoring overrides even though no remapping UI is delivered. Composite roots are not individual buttons: expand their parts, selecting the first authored control per part for a compact movement hint, with alternatives available for a controls-description row. Preserve existing WASD/arrow alternatives; do not flatten their composite into one guessed sprite.

Refresh on initialization, meaningful device/family changes, keyboard configuration/layout changes, theme/override changes, and bound-control changes. Refresh on enable/bind so a view reopened after a device switch is correct immediately. Allocate/format on those changes, then serve cached descriptors; do not recalculate every visible hint in Update.

### 6.2 Device Presentation

Detect recognized PlayStation layout inheritance, SwitchProControllerHID, Xbox/XInput families, then GenericGamepad. Treat translated virtual devices according to the layout actually exposed. Default to keyboard/mouse when present; otherwise select an available supported gamepad's family without invoking an action. Connecting a controller while playing with a keyboard does not itself switch hints.

Observe meaningful supported controls through action activity and, where necessary while a map is disabled, package device events. Raw device observation is restricted to presentation activity; gameplay still reads actions. Ignore gyro/sensor noise, unchanged held reports, stick motion within the activity deadzone, sub-threshold mouse jitter, reset/cancellation events, focus restoration, and synthetic initial-state checks. Button presses and meaningful analog changes from another supported device may switch presentation; mere continued holding must not repeatedly steal the hint family.

Single-player actions may be actuated by supported connected devices, using Unity action conflict resolution. This feature does not add exclusive player pairing or multiplayer ownership. A second idle/noisy controller must not flip the hints. If hardware is exposed only as a generic Joystick, use clear unsupported-device diagnostics rather than guessed button mappings.

### 6.3 Reusable Views and Consumers

New Assets/Scripts/UI/Input/InputPromptView.cs renders a Sprite/readable label and owns only its display subscription. Mandatory Image/label references are explicit. Bind it to IInputPromptProvider and an action ID before its first visible render; binding can be passed explicitly by an injected presenter/coordinator. Detach the subscription on disable/destruction, retain binding identity across ordinary disable/enable, and resubscribe/refresh on enable; an explicit Unbind clears the identity when an owner retires the view. Do not make dynamically instantiated views depend on injection occurring before Instantiate invokes Awake/OnEnable.

- SkillUpgradePresenter receives IInputCommands, IInputContexts, and IInputPromptProvider. Replace Keyboard.current polling with admitted SelectChoice commands, retaining _lastHandledInputFrame and existing reward queue/Apply/InitializeSkill order.
- Pass the prompt provider and slot action to SkillUpgradeButton.Initialize. Its existing dynamic Instantiate path can remain; explicit initialization supplies dependencies. Keep rarity/stat graphics separate from the input prompt. Do not add scene searches or runtime AddComponent as the prompt wiring strategy.
- Bind static new-skill card views to SelectChoice1/2. Route pad, keyboard, and any card click through the same validated selection method. Unavailable cards cannot receive commands or show misleading prompts.
- IncreaseDifficultyTotem receives IGameplayInput and a prompt provider. Keep distance, one-use behavior, configured difficulty amount, and VFX sequencing; replace E polling and KeyE_Image with Player/Interact state/prompt. Preserve the existing handling of multiple eligible totem instances; do not add a nearest-target arbitration mechanic.
- CarController consumes IGameplayInput and forwards brake started/canceled to its existing OnBrakePress/OnBrakeRelease events. Keep Update sampling and FixedUpdate physics. No new normalization, acceleration curve, brake strength, or drift tuning.
- PausePresenter consumes admitted Pause/Back commands and modal scope ownership. Preserve living-player checks, HideForDeath, stats visibility, and all serialized forwarding callbacks.
- MenuInputCoordinator/UiInputScope and MenuButtonsFunctionality preserve existing button business actions while providing Back/focus routing. Settings values, storage keys, and scoreboard logic do not change.
- DevConsolePresenter replaces polling with console actions and relinquishes direct Player-map enable/disable. Preserve development-build gating and keyboard text entry through TMP_InputField. Use one command-submission path: remove the competing onSubmit execution listener, queue action submission, and read finalized TMP text in LateUpdate after field processing. Do not execute the command twice.

### 6.4 General Menu Focus and Visual Feedback

Provide explicit first selectable per main menu, settings, scoreboard/back control, pause, pause child panel, and death screen. Store/restore the last valid selectable when returning to a parent screen; use the authored default if the stored one was hidden, destroyed, or disabled. This validity check is state handling, not a fallback dependency search. Ignore Back at the root main menu; do not quit the application on an unrequested Cancel.

Set deselectOnBackgroundClick to false and ensure visible navigation cannot reach hidden modal controls. Rebuild navigation for dynamic/native dropdown lists, scroll the selected item into view, and verify sliders/toggles/dropdowns with a controller. On switching from mouse to pad, restore valid focus if necessary without activating a control; on switching to mouse, retain valid selection and pointer behavior.

Reward selection deliberately disables general navigation/Submit/Cancel. The three direct card prompts remain visible for the active family. buttonEast selects slot 3 here; it never cancels an obligatory reward. Global/Pause can temporarily cover a pending reward; resuming reveals the same request and rearms selection.

Use an existing visible selection treatment or add selection/deselection callbacks to the touched UI effects so pad focus gets the same readable feedback as pointer hover. Reuse existing button audio and TransformTweenExtensions helpers where appropriate. Do not add VFX, sounds, or haptics as required dependencies. Any new UI tween must work at timeScale zero and be killed on disable/destruction.

## 7. Context Routing and Lifecycle Invariants

### 7.1 Eligibility Matrix

| Effective context | Move/Brake | World Interact | Direct choices | Native UI navigation/Submit | Back/Pause | Console |
| --- | --- | --- | --- | --- | --- | --- |
| Boot/loading | Neutral | Disabled | Disabled | Disabled | Disabled | Disabled |
| Main menu/settings/scoreboard | Neutral | Disabled | Disabled | Enabled for top screen | UI/Cancel handles widget/parent back | Unavailable in these scenes |
| Live gameplay | Enabled | Enabled | Disabled | Disabled; no hidden-menu activation | Global/Pause opens pause | Toggle if supported |
| Live reward | Enabled | Disabled | Visible slots only | Disabled; card pointer clicks use the same selection guard | Global/Pause opens pause; Cancel disabled | Toggle if supported |
| Pause/pause child panel | Neutral | Disabled | Disabled | Enabled for top screen | UI/Cancel backs out or resumes; Global/Pause disabled | Toggle if supported |
| Dying before death UI | Neutral | Disabled | Disabled | Disabled | Disabled | Development console may overlay |
| Death UI | Neutral | Disabled | Disabled | Enabled for death controls | Cannot resume gameplay; use visible navigation buttons | Development console may overlay |
| Development console overlay | Neutral | Disabled | Disabled | Disabled; TMP keyboard text entry remains available | DevConsole/Close only | Toggle, Submit, history |

Native pointer interaction is allowed only for the top visible interactive screen. Gameplay-only views do not expose hidden clickable menus. Loading/dying suppress pointer business actions; console and reward overlays block covered UI even when lower screens remain authored active.

Loading overrides all scopes. The supported development console may overlay other gameplay contexts; death/dying supersede ordinary pause/reward scopes underneath it. Pause covers a live reward without consuming it. Modal release restores the remaining valid context instead of unconditionally enabling Player or selecting a generic Gameplay state.

### 7.2 Required Safety Behavior

- Keep GameTime ownership in existing presenters/scene flow. A reward remains live; input routing must not pause it. Pause/console/death retain their existing time semantics, and GameSceneLoader resumes time after loading completes.
- Suppressing Interact during live rewards and paused/console/terminal contexts is an intentional input-safety correction: buttonSouth cannot select a card and increase world difficulty simultaneously. It does not change the difficulty amount, interaction radius, or reward rules.
- World actions are suppressed while menus own their controls. Gameplay resumes through context eligibility, never through an unconditional Player-map Enable in a Hide callback.
- Preserve the death VFX/display sequence. Mark gameplay input ineligible when health reaches zero without saving score, enabling the death screen early, or altering audio/VFX timing. Enter DeathUI only when the existing presenter shows it.
- Clear cached movement/brake when ineligible, focus is lost, or the relevant device is reset/disconnected. Emit a brake cancellation/release once if braking was active so CarVfxEffectsController cannot retain a lit brake state. Repeated suppression must not emit duplicate release events.
- Preserve analog Move initial-state behavior on normal context resume. Digital commands and Brake require safe release/rearm after suppression so menu presses do not create unintended actions. Do not reset Rigidbody velocity as part of input reset.
- On supported-pad loss, neutralize its active input, update hints to an available meaningful device/neutral disconnected label, and preserve the current screen/focus. Do not add automatic GameTime pause; that gameplay policy is outside this migration. Reconnection changes availability and rearms commands without selecting a card or resuming a game automatically.
- On application focus loss, clear transient commands and release input. On regain, suppress fresh digital actions until rearmed; do not rely only on platform background defaults. UI repeat/timing uses unscaled time and Dynamic input updates so menus remain operable while paused.
- Starting a scene load increments the scene epoch, invalidates scene/modal tokens, clears focus/commands, and neutralizes gameplay. New scene adapters register readiness before the service activates the loaded context. Destroyed old adapters cannot release a new scene's scope or resume time.
- Console closing uses the existing alive/previously-paused checks. Closing over pause/death restores that context and never resumes it. Console Submit executes once, history arrows do not steer the car, and Escape does not also toggle pause.
- AutoResolvePendingUpgrades bypasses input and releases reward prompt/scopes without changing reward queue order. Reentrant OnRequestQueued processing must not expose two active reward command owners.

## 8. Performance and Resource Ownership

- Zero managed allocations in steady-state input sampling, device-activity filtering, command dispatch, and Update/FixedUpdate. Cache actions, control identities, dictionary keys, and delegates. Reuse bounded command storage; do not allocate new event arguments or call LINQ/ToString while sampling.
- Context changes, device attachment, and prompt refresh may rebuild small caches outside the steady-state path. Glyph lookup does not use Resources.Load, scene searches, or dictionary construction per frame.
- Unchanged analog input must not publish repeated device/prompt events. Only active visible views subscribe, and newly enabled views read the cached descriptor immediately.
- Subscribe/unsubscribe action, device, scene-loader, and view events in matched lifecycles. Dispose project services once; never dispose the borrowed authored action asset. Restore any runtime masks/references on shutdown, including repeated Play Mode with domain reload disabled.
- Preserve the existing reward button creation cadence. This migration does not create per-input UI objects or require a new projectile/gameplay pool. If an added repeated visual element needs runtime spawning, reuse/pool it; do not introduce per-frame allocation as polish.
- Missing required actions/configuration/UI references fail fast. Unsupported devices and missing optional artwork produce bounded diagnostics plus correct label behavior; they do not trigger GetComponentInChildren/FindAnyObjectByType dependency fallbacks.
- Follow the LINQ ban, method block syntax, event OnX naming, private-field order, and domain Constants folder rule in all later code changes.

## 9. Implementation Plan

Every atomic implementation step must finish compiling and leave the active prototype functional. Supporting types/assets remain inactive until the runtime cutover is ready. Do not leave a temporary state where only one consumer has handed over map ownership while the console still toggles maps. C# compilation alone does not validate Unity asset assignments.

### Phase 1 - Add Inactive Foundations

- **Step 1.1 - Contracts and data.** Create the input value types/contracts, InputGlyphCatalog, InputRuntimeConfig, and constants. Add no active gameplay subscriptions yet. Create/populate assets through the Editor and visually identify the selected numbered graphics. Verification: runtime compile, duplicate/key/type validation, and required Sprite/label coverage.
- **Step 1.2 - Actions.** Add Global, Upgrade, DevConsole, and Interact actions/bindings to the existing asset. Keep the old Pause action available until cutover. Preserve existing Move/Brake action IDs and behavior. Verification: inspect action/group/composite inventory and confirm the existing keyboard/gamepad car still works; new unconsumed commands do not change behavior.
- **Step 1.3 - Services and views.** Implement the passive runtime owner, prompt resolver, driver, and UI adapters/views without activating driver/routing. Add pure policy/resolver tests and an editor validator. Verification: compile runtime/editor code and tests; borrowed-asset ownership, override-aware glyph resolution, scope invalidation, and press-consumption rules.

### Phase 2 - Prepare Explicit Editor Wiring

- **Step 2.1 - Project data.** Wire ProjectInstaller references in ProjectScope and add project service bindings. Ensure service construction alone does not enable routing; the driver remains inactive. Prepare its Boot scene reference. Verification: configuration resolves without a second service/asset instance; existing Boot-to-menu-to-game flow works.
- **Step 2.2 - Native menus.** Assign the project action asset and explicit action references to UI modules in Boot, MainMenu, and RuinedBloodCity. Preserve their native Cancel handling until the routed adapter is activated at cutover. Prepare focus/adapter references, widget lists, prompt views, and selection feedback without enabling duplicate command routing. Verification: current mouse/keyboard menu actions, settings persistence, and pad native navigation still function; no package-default action asset remains assigned.
- **Step 2.3 - Prompt surfaces.** Prepare disabled shared prompt views/adapters for dynamic upgrades, new-skill cards, totem, menu, pause/death hints. Keep the old renderer active until each surface can switch atomically with its initialization path. Verification: required image/label/provider assignments, scene overrides, slot ordering, and no visual field renames.

### Phase 3 - Atomic Runtime Cutover

- **Step 3.1 - Transfer all ownership together.** Switch CarController, PausePresenter, SkillUpgradePresenter/Button, IncreaseDifficultyTotem, DevConsolePresenter, and menu/scene adapters to the injected contracts. Activate the Boot input driver, centrally owned map/action policy, prepared prompt renderers, Back/focus routing, and activation guards in this same functional checkpoint. Remove console map mutation, raw key polling, old prompt rendering, and duplicate TMP submit execution together. Verification: compile and Play Mode covering every context in section 7; no mixed owner or misleading old prompt remains.
- **Step 3.2 - Complete prompt/focus coverage.** Check shared view refresh everywhere, family-specific controller slot graphics, nested widget focus, modal click blocking, and consumed-press rearming; complete selection visual/audio parity. Verification: live driving plus A/Y/B selection, unavailable slots, switching devices while a menu stays open, and one press per reward.
- **Step 3.3 - Lifecycle regression pass.** Validate focus changes, unplug/replug, brake releases, pause over rewards, console over pause/death, auto upgrades, death VFX completion, restart, and stale scope destruction. Verification: meaningful automated policy tests and the manual matrix below; no time/score/reward ordering regression.

### Phase 4 - Remove Legacy Dependencies and Accept

- **Step 4.1 - Dependency retirement.** Audit all project C# and serialized references again. Retire unused Player/Pause and SkillUpgradeKeyboardIconMapping only after all consumers/references migrate; delete old Unity assets through the Editor. Existing compatibility UnityEvent methods remain unless separately migrated. Verification: no legacy polling, old prompt mapping use, package-default UI asset assignment, or direct map mutation outside the input subsystem.
- **Step 4.2 - Backend switch last.** In Unity Player settings choose Input System Package (New), restart the Editor as required, and retain Input System 1.19.0. Do not hand-edit ProjectSettings YAML or delete InputManager.asset as a substitute. Verification: clean Unity compilation, a targeted project build, Boot Play Mode, and a Windows development build running the acceptance matrix with only the new backend.
- **Step 4.3 - Record implementation.** Update the touched input/UI/player/interactable/console/DI system documentation and add an input-system document under .agents/context/game-systems/. Create the runtime implementation summary under .agents/context/implementations/summaries/ with exact assets/wiring, tested hardware/connection modes, and remaining manual checks. Verification: references and scope accurately match shipped code; no unfinished runtime feature is reported as validated.

## 10. Verification and Acceptance Criteria

### 10.1 Automated Validation to Implement

Use the project's existing predefined editor-test assembly for pure routing and resolver tests under Assets/Scripts/Editor/Tests/. Proposed files are GameInputContextTests.cs, InputPromptResolverTests.cs, and InputCommandConsumptionTests.cs. Test scene/context stamps, modal precedence, held-control rearm, same-frame transitions, visible-slot bounds, family/control resolution, keyboard label mismatches, composites, and effective binding overrides. These tests exercise observable policy, not private implementation structure.

Add Assets/Scripts/Editor/Tools/InputConfigurationValidator.cs to inspect authored action types/groups, catalog keys/coverage, duplicate owners/EventSystems, UI asset/action references, prompt initialization references, and required selectable/widget assignments. Distinguish optional missing sprites from required missing references.

Do not introduce a test asmdef that attempts to reference Assembly-CSharp. InputTestFixture is not currently available through a configured package test assembly; enabling it would require manifest testables plus a deliberate compatible assembly plan. Use pure policy tests/editor asset validation for this scope and actual Unity/hardware scenarios for integration. A future dedicated input-test assembly is optional follow-up, not a hidden runtime assembly migration.

Compile later C# changes with:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

Compile the existing Assembly-CSharp-Editor project after Unity refreshes its project files, and execute the added Edit Mode tests in Unity. Require zero introduced warnings and errors; record any pre-existing validation problem explicitly. A generated dotnet build does not replace the Editor/backend/build test.

### 10.2 Manual Integration Matrix

| Scenario | Required outcome |
| --- | --- |
| Keyboard/mouse Boot -> menu -> settings/scoreboard -> gameplay | Existing shortcuts, pointer actions, values, and panel callbacks work; no controller required. |
| Controller-only Boot -> main menu -> settings -> gameplay -> pause -> restart/menu/exit -> death screen | Every player-facing action is reachable through selection/navigation or direct reward actions; focus and hints remain visible. |
| Existing analog driving and brake | Same steering/acceleration/drift behavior; west face button brakes; no choice shortcut applies the brake. |
| Two new-skill cards and up to three stat options | Keyboard 1/2/3 and pad south/north/east select their actual slots; absent slots do nothing and show no prompt. |
| Reward visible while driving | Left stick/brake remain available; no new pause; south does not also activate a nearby totem. |
| Rapid/mixed clicks and held selection across queued rewards | One option applies once; the next request needs a fresh press/click; simultaneous shortcuts preserve slot priority. |
| Pause over pending reward, then Back/Continue | Pause takes ownership; reward does not change; resume restores the same request and safe input state. |
| Console open/close, Enter, history; console over pause/death | Text submits once; no steering/reward input leaks; Escape closes only the console; paused/dead game does not resume. |
| Dropdown list, slider, toggle, scrollbar, and background click | Correct widget navigation/back behavior, selected items remain visible, valid focus survives empty clicks. |
| Switch keyboard/mouse <-> Xbox/PlayStation/Switch while a prompt is visible | Action hints update immediately after meaningful activity, including static cards/totem; numbered graphics match the physical buttons. |
| Second idle/noisy controller, mouse jitter, stick/gyro drift | Hints remain stable and no command is produced. |
| Hold brake, lose focus/unplug, reconnect/regain focus | Input neutralizes and brake visual releases once; digital commands do not fire from held/reset states. |
| Death during a reward/console, then death VFX completion | Gameplay input stops; existing score/audio/death UI ordering is preserved; rewards cannot apply after death. |
| Scene reload while holding buttons or with console/reward open | Old commands/tokens do not reach the new scene; time resumes through existing loader behavior; subscriptions do not multiply. |
| Missing optional sprite, unknown pad family, nonmatching keyboard layout | A correct readable control label appears; required missing configuration fails validation. |
| New-backend-only Windows build | No UnityEngine.Input backend exception or missing UI input; all core flows above work outside the Editor. |

Record actual device model, reported Unity layout, USB/Bluetooth/translation mode, and result for each tested family. Simulated policy tests and installed graphics do not establish physical-device support. Switch Pro and PlayStation connection support must match the installed package documentation and actual hardware results.

### 10.3 Definition of Done

- [ ] Only Input System Package (New) is active after final migration and restart.
- [ ] One project action asset/runtime owner serves gameplay, UI, and prompt resolution.
- [ ] No project gameplay/menu/console command polls a hard-coded device key directly.
- [ ] Direct card bindings, preserved driving/brake, menu Back/Submit/focus, and world Interact work on supported controllers.
- [ ] All known prompt surfaces use the shared catalog and effective bindings, with accurate labels as fallback.
- [ ] Modal/scene/focus/device transitions satisfy section 7 and the manual matrix.
- [ ] Required references and DI contracts validate; no static mutable application service or scene-search shortcut is introduced.
- [ ] Targeted compilation/editor tests pass; steady-state paths add no managed allocations.
- [ ] Hardware/build verification and remaining limitations are recorded in the runtime summary.

No runtime criterion above is claimed as executed by this documentation-only task.

## 11. Architectural Self-Review and Optional Follow-ups

| Severity | Risk | Design resolution and verification |
| --- | --- | --- |
| Blocker | Implementing code/assets before the reviewed requirements gate | This is a complete document only; runtime work needs a separate request/review. No consequential specification question remains. |
| Major | Shared UI/gameplay controls or action assets produce mismatched behavior/hints | One asset; direct south/north/east slots; west remains brake; navigation/Submit/Interact eligibility is context-specific. Check mixed-input and near-totem scenarios. |
| Major | Native UI module and routing service both process Cancel or re-enable actions | Central Cancel is intentionally detached from the module; UiInputScope reapplies owner policy and verifies explicit references after OnEnable. |
| Major | Scope release/scene reload resurrects old gameplay or consumes another reward | Epoch/generation-stamped commands, owner tokens, release gating, and retained presenter frame guards. |
| Major | New prompt injection happens too late for dynamically spawned UI | Explicit provider binding in existing initialization paths; required refs validate; no Awake fallback lookup. |
| Major | Input migration silently alters pause/death/reward mechanics | Preserve live rewards and existing time/score/VFX order; document input suppression corrections; regression matrix required. |
| Minor | Device noise, translation, or keyboard layout yields wrong artwork | Meaningful activity filtering, actual layout family, neutral/text fallback, and optional presentation override. |
| Minor | Proposed input tests require an incompatible asmdef migration | Keep pure tests in existing editor assembly; separately plan any future InputTestFixture assembly. |
| Nit | Existing docs use an obsolete loader filename or unsupported shortcut claims | Use inspected GameSceneLoader.cs/presenter behavior and update touched docs during runtime implementation. |

Optional follow-ups, excluded from acceptance: a player setting for automatic/forced controller prompt style, a controls-reference screen generated from the same provider, full remapping/persistence, exclusive controller pairing, configurable rumble, and auto-pause on controller loss. The latter needs a separate gameplay/time-ownership decision. None requires a second glyph database.

## 12. Official Documentation Checked

API facts below were verified against Input System 1.19 documentation. The architecture, binding table, thresholds, and routing rules above are project-specific proposals based on inspected code and the user's resolved choices.

- Backend selection belongs to Unity's Active Input Handling settings; both backends can be enabled during migration. [Migration documentation](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/Migration.html).
- Project-wide actions are enabled by default and the chosen asset is preloaded. [Project-wide actions](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/ProjectWideActions.html).
- The native UI module supports action references and selection navigation. Background-click deselection can interfere with pad navigation. [UI support](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/UISupport.html).
- Binding display can supply layout/control-path information; effective bindings, composites, keyboard layouts, and initial-state checks matter to hints and rearming. [Action bindings](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/ActionBindings.html).
- Gamepad controls use positional semantics. Generic HID devices may appear as Joystick; supported device/connection combinations vary. [Gamepad support](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/Gamepad.html).
- Dynamic updates, focus behavior, and noise affect input sampling and current-device APIs. [Input settings](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/Settings.html).
- Device events can be observed without replacing action-driven gameplay. [Input events](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/Events.html).
- InputTestFixture setup requires package testability and compatible test-assembly references. [Input testing](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/Testing.html).
- Use the installed Reflex 12.0.1 AddSingleton/AddScoped and disposal implementation, not newer upstream registration examples. [Official Reflex repository](https://github.com/gustavopsantos/Reflex).
