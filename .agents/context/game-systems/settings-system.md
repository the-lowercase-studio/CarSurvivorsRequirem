# Settings System Documentation

## Purpose

The Settings system owns player-configurable option persistence and application for audio volume, graphics quality, fullscreen mode, resolution, and damage-number visibility. It stores values through Assets/Scripts/Storage/AppStorage.cs, exposes typed setting contracts through Reflex, and lets menu UI components save and immediately apply changes.

The Settings system is not responsible for owning UI layout, audio mixer implementation details, damage-number spawning behavior, graphics quality asset configuration, or Unity screen API behavior. Those systems consume applied setting values through their own contracts or Unity APIs.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Settings/ISetting.cs
  - Assets/Scripts/Settings/ISettingLoader.cs
  - Assets/Scripts/Settings/UserSettingsLoader.cs
  - Assets/Scripts/Settings/AudioVolumeSetting.cs
  - Assets/Scripts/Settings/GraphicSetting.cs
  - Assets/Scripts/Settings/FullScreenSetting.cs
  - Assets/Scripts/Settings/Resolution/ResolutionSetting.cs
  - Assets/Scripts/Settings/DamageNumbersSetting.cs
  - Assets/Scripts/Settings/Resolution/ScreenSerializableResolutionHelper.cs
- Primary UI Option components:
  - Assets/Scripts/UI/Settings/AudioVolumeOption.cs
  - Assets/Scripts/UI/Settings/DamageNumbersOption.cs
  - Assets/Scripts/UI/Settings/FullScreenOption.cs
  - Assets/Scripts/UI/Settings/GraphicOption.cs
  - Assets/Scripts/UI/Settings/ResolutionOption.cs
  - Assets/Scripts/UI/Settings/IOptionComponent.cs
- Related runtime setup:
  - Assets/Scripts/ReflexDI/MainMenuInstaller.cs
  - Assets/Scripts/ReflexDI/BootLoader.cs
- Related docs:
  - .agents/context/game-systems/ui-system.md
  - .agents/context/game-systems/audio-system.md
  - .agents/context/game-systems/damage-numbers-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/technology-documentation.md

## Architecture and Data Flow

- Core components:
  - Assets/Scripts/Settings/ISetting.cs (`ISetting<TSelf, TRepresentedBy>`) combines the storage value contract from Assets/Scripts/Storage/IAppStorageValue.cs with `ISettingLoader.Load`.
  - Assets/Scripts/Settings/ISettingLoader.cs (`ISettingLoader`) allows Assets/Scripts/Settings/UserSettingsLoader.cs to apply all bound settings without knowing their concrete value types.
  - Assets/Scripts/Settings/UserSettingsLoader.cs receives `IEnumerable<ISettingLoader>` and calls `Load` for each setting in `Awake`.
  - Assets/Scripts/Settings/AudioVolumeSetting.cs stores `"Volume"` as a `float` mixer value and applies it through `IAudioMixersManager.SetMixerVolume`.
  - Assets/Scripts/Settings/GraphicSetting.cs stores `"GraphicsQuality"` as a quality-name string and applies it with `QualitySettings.SetQualityLevel`.
  - Assets/Scripts/Settings/FullScreenSetting.cs stores `"FullScreenMode"` as a Unity `FullScreenMode` and applies it to `Screen.fullScreenMode`.
  - Assets/Scripts/Settings/Resolution/ResolutionSetting.cs stores `"Resolution"` as `SerializableResolution`, validates it against available screen resolutions, and applies it through Assets/Scripts/Settings/Resolution/ScreenSerializableResolutionHelper.cs.
  - Assets/Scripts/Settings/DamageNumbersSetting.cs stores `"DamageNumbersEnabled"` as a `bool` and applies it through `IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>`.
  - Assets/Scripts/Storage/AppStorage.cs serializes values into Assets/Data/AppStorage.Editor.json in the Unity Editor and Data/AppStorage.json under the build root resolved from `Directory.GetParent(Application.dataPath)`, falling back to `AppDomain.CurrentDomain.BaseDirectory` only if that parent is unavailable.
  - Settings option UI components under Assets/Scripts/UI/Settings/ bind Unity controls to typed `ISetting<TSelf, TRepresentedBy>` instances. Assets/Scripts/UI/Settings/GraphicOption.cs rebuilds the graphics dropdown options from its hard-coded quality map in `Awake`.
- Key interfaces:
  - Assets/Scripts/Storage/IAppStorageValue.cs defines `DefaultValue`, `GetKey`, `GetValueOrStoredDefault`, and `SaveValue`.
  - Assets/Scripts/Settings/ISetting.cs (`ISetting<TSelf, TRepresentedBy>`) is the main typed setting contract injected into UI components.
  - Assets/Scripts/Settings/ISettingLoader.cs is the untyped load/apply contract used for batch loading.
  - Assets/Scripts/UI/Settings/IOptionComponent.cs (`IOptionComponent<T>`) is the UI-side option contract with `LoadComponent` and `PerformValueChange`.
- Runtime flow:
  - Assets/Scripts/ReflexDI/MainMenuInstaller.cs registers each concrete setting as scoped under its concrete type, typed `ISetting<...>` contract, and `ISettingLoader`.
  - Assets/Scripts/Settings/UserSettingsLoader.cs's `Awake` applies every bound `ISettingLoader`, so menu startup pushes stored/default settings into Unity systems and gameplay service toggles.
  - When a settings panel option enables, its UI component loads the stored/default value into its control without notifying where supported, then subscribes to value-change events.
  - When the player changes a setting, the option component saves the typed value through `SaveValue`, then calls `Load` to apply it immediately.
  - Assets/Scripts/Settings/Resolution/ResolutionSetting.cs's `Load` reads the fullscreen setting's stored/default value when applying a resolution, so resolution changes preserve the current fullscreen preference.

## Rules and Invariants

- Critical behavior rules:
  - Keep setting implementations registered in Assets/Scripts/ReflexDI/MainMenuInstaller.cs under both their typed `ISetting<...>` contract and `ISettingLoader`; otherwise option injection or batch loading can break.
  - Keep persistent keys stable unless intentionally migrating player settings: `"Volume"`, `"GraphicsQuality"`, `"FullScreenMode"`, `"Resolution"`, and `"DamageNumbersEnabled"`.
  - Keep setting application inside `Load`. UI components should save values and call `Load`, not duplicate application logic.
  - Keep settings dependencies explicit through Reflex. Do not replace `IAudioMixersManager` or `IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>` with scene searches or singleton access.
  - Preserve Assets/Scripts/Settings/Resolution/ResolutionSetting.cs dependency on `ISetting<FullScreenSetting, FullScreenMode>` so resolution application uses the stored fullscreen mode.
- Ordering or sequencing guarantees:
  - Assets/Scripts/Settings/UserSettingsLoader.cs's `Awake` loads every setting available from the active Reflex container.
  - UI option components load their current value before subscribing to value-change callbacks in `OnEnable`.
  - UI option components unsubscribe from Unity UI callbacks in `OnDisable`.
  - Value changes persist before application: option components call `SaveValue` before `Load`.
- Constraints contributors must preserve:
  - Do not edit scene, prefab, asset, or meta files directly for settings wiring unless explicitly requested and the text change is safe to review.
  - Do not change setting default values as incidental cleanup; defaults are player-facing behavior.
  - Do not rename graphics quality labels without aligning Unity quality settings and Assets/Scripts/UI/Settings/GraphicOption.cs's lookup.
  - Keep Assets/Scripts/UI/Settings/GraphicOption.cs's dropdown option setup aligned with its quality map; `LoadComponent` indexes the same map by stored/default quality name.
  - Treat audio volume values as mixer decibel values after slider conversion, not normalized slider values.

## Extension Points

- Safe extension areas:
  - Add a new persisted setting by implementing Assets/Scripts/Settings/ISetting.cs, choosing a stable storage key and default value, implementing `Load`, and registering it in Assets/Scripts/ReflexDI/MainMenuInstaller.cs as the concrete type, typed setting contract, and `ISettingLoader`.
  - Add a matching UI option by implementing Assets/Scripts/UI/Settings/IOptionComponent.cs, injecting the typed setting, loading the current value into the control without firing change events where possible, and subscribing/unsubscribing in `OnEnable`/`OnDisable`.
  - Add new screen-display behavior through Assets/Scripts/Settings/Resolution/ScreenSerializableResolutionHelper.cs only when it preserves stored resolution compatibility.
  - Add additional audio setting dimensions by extending audio mixer contracts and binding them through Reflex before creating setting classes.
- Required dependencies and contracts:
  - Assets/Scripts/Settings/AudioVolumeSetting.cs requires `IAudioMixersManager`, currently provided by Assets/Scripts/ReflexDI/BootLoader.cs.
  - Assets/Scripts/Settings/DamageNumbersSetting.cs requires `IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>`, currently provided by Assets/Scripts/ReflexDI/BootLoader.cs.
  - Assets/Scripts/Settings/Resolution/ResolutionSetting.cs requires `ISetting<FullScreenSetting, FullScreenMode>`.
  - Settings UI components require scene-wired TMP, slider, toggle, and dropdown references.
- Testing implications:
  - Compile C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Manual Unity play-mode checks are required for settings panel wiring, immediate application, persisted reload after returning to the menu, fullscreen/resolution behavior on the target platform, and damage-number enable/disable behavior.
  - Repeatedly enable and disable settings panels after UI changes to verify listeners do not duplicate.

## Integration Notes

- Upstream dependencies:
  - Assets/Scripts/ReflexDI/MainMenuInstaller.cs provides setting bindings in the Main Menu scene container.
  - Assets/Scripts/ReflexDI/BootLoader.cs provides global audio mixer and damage-number functionality services consumed by setting implementations.
  - Assets/Scripts/Storage/AppStorage.cs provides JSON-backed persistence for all setting values.
  - Assets/Scripts/Settings/Resolution/ScreenSerializableResolutionHelper.cs adapts Unity `Screen.resolutions` and `Screen.SetResolution` to `SerializableResolution`.
- Downstream consumers:
  - UI option components (Assets/Scripts/UI/Settings/AudioVolumeOption.cs, Assets/Scripts/UI/Settings/GraphicOption.cs, Assets/Scripts/UI/Settings/FullScreenOption.cs, Assets/Scripts/UI/Settings/ResolutionOption.cs, Assets/Scripts/UI/Settings/DamageNumbersOption.cs) consume typed setting contracts.
  - Assets/Scripts/Audio/AudioMixersManager.cs, Unity `QualitySettings`, Unity `Screen`, and Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs receive applied setting effects through each setting's `Load` method.
  - Other docs treat settings as the owner of persisted preferences for UI, audio, and damage-number visibility.
- Cross-system coupling risks:
  - Moving global service bindings out of Assets/Scripts/ReflexDI/BootLoader.cs can break setting application and unrelated runtime consumers together.
  - Changing Assets/Scripts/Storage/AppStorage.cs path or serialization affects every persisted setting, not only Settings UI.
  - Changing fullscreen mode semantics can affect resolution application because Assets/Scripts/Settings/Resolution/ResolutionSetting.cs reads the fullscreen setting.
  - Changing damage-number setting behavior affects enemy hit feedback through the shared spawner enable/disable contract.

## Known Risks and Open Questions

- Known limitations:
  - Assets/Scripts/Storage/AppStorage.cs's `TryGetValue` swallows deserialization exceptions and falls back to default values without diagnostics.
  - Assets/Scripts/Storage/AppStorage.cs caches values statically and does not support external file changes after its static load.
  - Assets/Scripts/Settings/GraphicSetting.cs's `Load` throws when the stored/default quality name is not present in `QualitySettings.names`.
  - Assets/Scripts/UI/Settings/GraphicOption.cs hard-codes `Low`, `Medium`, `High`, and `Ultra`, rebuilds dropdown options from that map in `Awake`, and still requires stored/default names to exist in the map and Unity quality settings.
  - Assets/Scripts/UI/Settings/AudioVolumeOption.cs uses `Mathf.Log10(_slider.value)`, so the slider minimum must stay above zero.
  - Assets/Scripts/UI/Settings/ResolutionOption.cs's `LoadComponent` can set `-1` if the stored/default resolution is not in the current available-resolution list, while Assets/Scripts/Settings/Resolution/ResolutionSetting.cs's `Load` has its own fallback path.
  - Assets/Scripts/Settings/Resolution/ScreenSerializableResolutionHelper.cs caches available resolutions for the process lifetime.
- Open design questions:
  - Should setting storage keys move to domain constants with migration safeguards?
  - Should settings load failures log diagnostics for corrupt or incompatible `AppStorage.json` values?
  - Should display settings distinguish windowed, exclusive fullscreen, and maximized window as explicit UI options instead of a single boolean toggle?
  - Should Assets/Scripts/UI/Settings/GraphicOption.cs derive dropdown options from `QualitySettings.names` instead of maintaining a separate hard-coded map?
- Suggested follow-up tasks:
  - Add a focused validation helper for settings UI controls to catch zero-volume sliders, mismatched graphics names, and invalid resolution indices.
  - Review Assets/Scripts/Storage/AppStorage.cs error handling before adding more player-facing preferences.
  - Consider a settings migration plan before changing existing keys, default values, or serialized value shapes.
