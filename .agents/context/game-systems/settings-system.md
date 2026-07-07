# Settings System Documentation

## Purpose

The Settings system owns player-configurable option persistence and application for audio volume, graphics quality, fullscreen mode, resolution, and damage-number visibility. It stores values through [AppStorage.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Storage/AppStorage.cs), exposes typed setting contracts through Reflex, and lets menu UI components save and immediately apply changes.

The Settings system is not responsible for owning UI layout, audio mixer implementation details, damage-number spawning behavior, graphics quality asset configuration, or Unity screen API behavior. Those systems consume applied setting values through their own contracts or Unity APIs.

## Reading Map

- Primary code locations:
  - [ISetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/ISetting.cs)
  - [ISettingLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/ISettingLoader.cs)
  - [UserSettingsLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/UserSettingsLoader.cs)
  - [AudioVolumeSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/AudioVolumeSetting.cs)
  - [GraphicSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/GraphicSetting.cs)
  - [FullScreenSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/FullScreenSetting.cs)
  - [ResolutionSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/Resolution/ResolutionSetting.cs)
  - [DamageNumbersSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/DamageNumbersSetting.cs)
  - [ScreenSerializableResolutionHelper.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/Resolution/ScreenSerializableResolutionHelper.cs)
- Primary UI Option components:
  - [AudioVolumeOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/AudioVolumeOption.cs)
  - [DamageNumbersOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/DamageNumbersOption.cs)
  - [FullScreenOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/FullScreenOption.cs)
  - [GraphicOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/GraphicOption.cs)
  - [ResolutionOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/ResolutionOption.cs)
  - [IOptionComponent.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/IOptionComponent.cs)
- Related runtime setup:
  - [MainMenuInstaller.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/ReflexDI/MainMenuInstaller.cs)
  - [BootLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/ReflexDI/BootLoader.cs)
- Related docs:
  - [ui-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/ui-system.md)
  - [audio-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/audio-system.md)
  - [damage-numbers-system.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/game-systems/damage-numbers-system.md)
  - [project-coding-standards.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/project-coding-standards.md)
  - [technology-documentation.md](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/.agents/context/technology-documentation.md)

## Architecture and Data Flow

- Core components:
  - [ISetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/ISetting.cs) (`ISetting<TSelf, TRepresentedBy>`) combines the storage value contract from [IAppStorageValue.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Storage/IAppStorageValue.cs) with `ISettingLoader.Load`.
  - [ISettingLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/ISettingLoader.cs) (`ISettingLoader`) allows [UserSettingsLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/UserSettingsLoader.cs) to apply all bound settings without knowing their concrete value types.
  - [UserSettingsLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/UserSettingsLoader.cs) receives `IEnumerable<ISettingLoader>` and calls `Load` for each setting in `Awake`.
  - [AudioVolumeSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/AudioVolumeSetting.cs) stores `"Volume"` as a `float` mixer value and applies it through `IAudioMixersManager.SetMixerVolume`.
  - [GraphicSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/GraphicSetting.cs) stores `"GraphicsQuality"` as a quality-name string and applies it with `QualitySettings.SetQualityLevel`.
  - [FullScreenSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/FullScreenSetting.cs) stores `"FullScreenMode"` as a Unity `FullScreenMode` and applies it to `Screen.fullScreenMode`.
  - [ResolutionSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/Resolution/ResolutionSetting.cs) stores `"Resolution"` as `SerializableResolution`, validates it against available screen resolutions, and applies it through [ScreenSerializableResolutionHelper.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/Resolution/ScreenSerializableResolutionHelper.cs).
  - [DamageNumbersSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/DamageNumbersSetting.cs) stores `"DamageNumbersEnabled"` as a `bool` and applies it through `IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>`.
  - [AppStorage.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Storage/AppStorage.cs) serializes values into `Assets/Data/AppStorage.Editor.json` in the Unity Editor and `Data/AppStorage.json` under the build root resolved from `Directory.GetParent(Application.dataPath)`, falling back to `AppDomain.CurrentDomain.BaseDirectory` only if that parent is unavailable.
  - Settings option UI components under `Assets/Scripts/UI/Settings/` bind Unity controls to typed `ISetting<TSelf, TRepresentedBy>` instances. [GraphicOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/GraphicOption.cs) rebuilds the graphics dropdown options from its hard-coded quality map in `Awake`.
- Key interfaces:
  - [IAppStorageValue.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Storage/IAppStorageValue.cs) defines `DefaultValue`, `GetKey`, `GetValueOrStoredDefault`, and `SaveValue`.
  - [ISetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/ISetting.cs) (`ISetting<TSelf, TRepresentedBy>`) is the main typed setting contract injected into UI components.
  - [ISettingLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/ISettingLoader.cs) is the untyped load/apply contract used for batch loading.
  - [IOptionComponent.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/IOptionComponent.cs) (`IOptionComponent<T>`) is the UI-side option contract with `LoadComponent` and `PerformValueChange`.
- Runtime flow:
  - [MainMenuInstaller.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/ReflexDI/MainMenuInstaller.cs) registers each concrete setting as scoped under its concrete type, typed `ISetting<...>` contract, and `ISettingLoader`.
  - [UserSettingsLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/UserSettingsLoader.cs)'s `Awake` applies every bound `ISettingLoader`, so menu startup pushes stored/default settings into Unity systems and gameplay service toggles.
  - When a settings panel option enables, its UI component loads the stored/default value into its control without notifying where supported, then subscribes to value-change events.
  - When the player changes a setting, the option component saves the typed value through `SaveValue`, then calls `Load` to apply it immediately.
  - [ResolutionSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/Resolution/ResolutionSetting.cs)'s `Load` reads the fullscreen setting's stored/default value when applying a resolution, so resolution changes preserve the current fullscreen preference.

## Rules and Invariants

- Critical behavior rules:
  - Keep setting implementations registered in [MainMenuInstaller.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/ReflexDI/MainMenuInstaller.cs) under both their typed `ISetting<...>` contract and `ISettingLoader`; otherwise option injection or batch loading can break.
  - Keep persistent keys stable unless intentionally migrating player settings: `"Volume"`, `"GraphicsQuality"`, `"FullScreenMode"`, `"Resolution"`, and `"DamageNumbersEnabled"`.
  - Keep setting application inside `Load`. UI components should save values and call `Load`, not duplicate application logic.
  - Keep settings dependencies explicit through Reflex. Do not replace `IAudioMixersManager` or `IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>` with scene searches or singleton access.
  - Preserve [ResolutionSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/Resolution/ResolutionSetting.cs) dependency on `ISetting<FullScreenSetting, FullScreenMode>` so resolution application uses the stored fullscreen mode.
- Ordering or sequencing guarantees:
  - [UserSettingsLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/UserSettingsLoader.cs)'s `Awake` loads every setting available from the active Reflex container.
  - UI option components load their current value before subscribing to value-change callbacks in `OnEnable`.
  - UI option components unsubscribe from Unity UI callbacks in `OnDisable`.
  - Value changes persist before application: option components call `SaveValue` before `Load`.
- Constraints contributors must preserve:
  - Do not edit scene, prefab, asset, or meta files directly for settings wiring unless explicitly requested and the text change is safe to review.
  - Do not change setting default values as incidental cleanup; defaults are player-facing behavior.
  - Do not rename graphics quality labels without aligning Unity quality settings and [GraphicOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/GraphicOption.cs)'s lookup.
  - Keep [GraphicOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/GraphicOption.cs)'s dropdown option setup aligned with its quality map; `LoadComponent` indexes the same map by stored/default quality name.
  - Treat audio volume values as mixer decibel values after slider conversion, not normalized slider values.

## Extension Points

- Safe extension areas:
  - Add a new persisted setting by implementing [ISetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/ISetting.cs), choosing a stable storage key and default value, implementing `Load`, and registering it in [MainMenuInstaller.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/ReflexDI/MainMenuInstaller.cs) as the concrete type, typed setting contract, and `ISettingLoader`.
  - Add a matching UI option by implementing [IOptionComponent.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/IOptionComponent.cs), injecting the typed setting, loading the current value into the control without firing change events where possible, and subscribing/unsubscribing in `OnEnable`/`OnDisable`.
  - Add new screen-display behavior through [ScreenSerializableResolutionHelper.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/Resolution/ScreenSerializableResolutionHelper.cs) only when it preserves stored resolution compatibility.
  - Add additional audio setting dimensions by extending audio mixer contracts and binding them through Reflex before creating setting classes.
- Required dependencies and contracts:
  - [AudioVolumeSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/AudioVolumeSetting.cs) requires `IAudioMixersManager`, currently provided by [BootLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/ReflexDI/BootLoader.cs).
  - [DamageNumbersSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/DamageNumbersSetting.cs) requires `IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>`, currently provided by [BootLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/ReflexDI/BootLoader.cs).
  - [ResolutionSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/Resolution/ResolutionSetting.cs) requires `ISetting<FullScreenSetting, FullScreenMode>`.
  - Settings UI components require scene-wired TMP, slider, toggle, and dropdown references.
- Testing implications:
  - Compile C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Manual Unity play-mode checks are required for settings panel wiring, immediate application, persisted reload after returning to the menu, fullscreen/resolution behavior on the target platform, and damage-number enable/disable behavior.
  - Repeatedly enable and disable settings panels after UI changes to verify listeners do not duplicate.

## Integration Notes

- Upstream dependencies:
  - [MainMenuInstaller.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/ReflexDI/MainMenuInstaller.cs) provides setting bindings in the Main Menu scene container.
  - [BootLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/ReflexDI/BootLoader.cs) provides global audio mixer and damage-number functionality services consumed by setting implementations.
  - [AppStorage.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Storage/AppStorage.cs) provides JSON-backed persistence for all setting values.
  - [ScreenSerializableResolutionHelper.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/Resolution/ScreenSerializableResolutionHelper.cs) adapts Unity `Screen.resolutions` and `Screen.SetResolution` to `SerializableResolution`.
- Downstream consumers:
  - UI option components ([AudioVolumeOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/AudioVolumeOption.cs), [GraphicOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/GraphicOption.cs), [FullScreenOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/FullScreenOption.cs), [ResolutionOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/ResolutionOption.cs), [DamageNumbersOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/DamageNumbersOption.cs)) consume typed setting contracts.
  - [AudioMixersManager.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Audio/AudioMixersManager.cs), Unity `QualitySettings`, Unity `Screen`, and [DamageNumbersSpawner.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs) receive applied setting effects through each setting's `Load` method.
  - Other docs treat settings as the owner of persisted preferences for UI, audio, and damage-number visibility.
- Cross-system coupling risks:
  - Moving global service bindings out of [BootLoader.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/ReflexDI/BootLoader.cs) can break setting application and unrelated runtime consumers together.
  - Changing [AppStorage.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Storage/AppStorage.cs) path or serialization affects every persisted setting, not only Settings UI.
  - Changing fullscreen mode semantics can affect resolution application because [ResolutionSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/Resolution/ResolutionSetting.cs) reads the fullscreen setting.
  - Changing damage-number setting behavior affects enemy hit feedback through the shared spawner enable/disable contract.

## Known Risks and Open Questions

- Known limitations:
  - [AppStorage.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Storage/AppStorage.cs)'s `TryGetValue` swallows deserialization exceptions and falls back to default values without diagnostics.
  - [AppStorage.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Storage/AppStorage.cs) caches values statically and does not support external file changes after its static load.
  - [GraphicSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/GraphicSetting.cs)'s `Load` throws when the stored/default quality name is not present in `QualitySettings.names`.
  - [GraphicOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/GraphicOption.cs) hard-codes `Low`, `Medium`, `High`, and `Ultra`, rebuilds dropdown options from that map in `Awake`, and still requires stored/default names to exist in the map and Unity quality settings.
  - [AudioVolumeOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/AudioVolumeOption.cs) uses `Mathf.Log10(_slider.value)`, so the slider minimum must stay above zero.
  - [ResolutionOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/ResolutionOption.cs)'s `LoadComponent` can set `-1` if the stored/default resolution is not in the current available-resolution list, while [ResolutionSetting.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/Resolution/ResolutionSetting.cs)'s `Load` has its own fallback path.
  - [ScreenSerializableResolutionHelper.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Settings/Resolution/ScreenSerializableResolutionHelper.cs) caches available resolutions for the process lifetime.
- Open design questions:
  - Should setting storage keys move to domain constants with migration safeguards?
  - Should settings load failures log diagnostics for corrupt or incompatible `AppStorage.json` values?
  - Should display settings distinguish windowed, exclusive fullscreen, and maximized window as explicit UI options instead of a single boolean toggle?
  - Should [GraphicOption.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/UI/Settings/GraphicOption.cs) derive dropdown options from `QualitySettings.names` instead of maintaining a separate hard-coded map?
- Suggested follow-up tasks:
  - Add a focused validation helper for settings UI controls to catch zero-volume sliders, mismatched graphics names, and invalid resolution indices.
  - Review [AppStorage.cs](file:///d:/GameDev/Unity/During/CarSurvivorsRequirem/Assets/Scripts/Storage/AppStorage.cs) error handling before adding more player-facing preferences.
  - Consider a settings migration plan before changing existing keys, default values, or serialized value shapes.
