# Settings System Documentation

## Purpose

The Settings system owns player-configurable option persistence and application for audio volume, graphics quality, fullscreen mode, resolution, and damage-number visibility. It stores values through `AppStorage`, exposes typed setting contracts through Reflex, and lets menu UI components save and immediately apply changes.

The Settings system is not responsible for owning UI layout, audio mixer implementation details, damage-number spawning behavior, graphics quality asset configuration, or Unity screen API behavior. Those systems consume applied setting values through their own contracts or Unity APIs.

## Reading Map

- Primary code locations:
  - `Assets/Scripts/Settings/`
  - `Assets/Scripts/Settings/Resolution/`
  - `Assets/Scripts/UI/Settings/`
  - `Assets/Scripts/ReflexDI/MainMenuInstaller.cs`
- Related runtime code:
  - `Assets/Scripts/Storage/AppStorage.cs`
  - `Assets/Scripts/Storage/IAppStorageValue.cs`
  - `Assets/Scripts/Helpers/ScreenSerializableResolutionHelper.cs`
  - `Assets/Scripts/Audio/AudioMixersManager.cs`
  - `Assets/Scripts/DamageNumbers/DamageNumbersSpawner.cs`
  - `Assets/Scripts/ReflexDI/BootLoader.cs`
- Related docs:
  - `.agents/docs/ui-system.md`
  - `.agents/docs/audio-system.md`
  - `.agents/docs/damage-numbers-system.md`
  - `.agents/docs/project-coding-standards.md`
  - `.agents/docs/technology-documentation.md`
- Related agents or instructions:
  - Root `AGENTS.md`
  - `.agents/skills/document-system/SKILL.md`
  - `.agents/skills/di-integration/SKILL.md` when adding setting bindings or injected dependencies

## Architecture and Data Flow

- Core components:
  - `ISetting<TSelf, TRepresentedBy>` combines the storage value contract from `IAppStorageValue<TRepresentedBy>` with `ISettingLoader.Load`.
  - `ISettingLoader` allows `UserSettingsLoader` to apply all bound settings without knowing their concrete value types.
  - `UserSettingsLoader` receives `IEnumerable<ISettingLoader>` and calls `Load` for each setting in `Awake`.
  - `AudioVolumeSetting` stores `"Volume"` as a `float` mixer value and applies it through `IAudioMixersManager.SetMixerVolume`.
  - `GraphicSetting` stores `"GraphicsQuality"` as a quality-name string and applies it with `QualitySettings.SetQualityLevel`.
  - `FullScreenSetting` stores `"FullScreenMode"` as a Unity `FullScreenMode` and applies it to `Screen.fullScreenMode`.
  - `ResolutionSetting` stores `"Resolution"` as `SerializableResolution`, validates it against available screen resolutions, and applies it through `ScreenSerializableResolutionHelper.SetResolution`.
  - `DamageNumbersSetting` stores `"DamageNumbersEnabled"` as a `bool` and applies it through `IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>`.
  - `AppStorage` serializes values into `Data/AppStorage.json` under `AppDomain.CurrentDomain.BaseDirectory`.
  - Settings option UI components under `Assets/Scripts/UI/Settings/` bind Unity controls to typed `ISetting<TSelf, TRepresentedBy>` instances.
- Key interfaces:
  - `IAppStorageValue<T>` defines `DefaultValue`, `GetKey`, `GetValueOrStoredDefault`, and `SaveValue`.
  - `ISetting<TSelf, TRepresentedBy>` is the main typed setting contract injected into UI components.
  - `ISettingLoader` is the untyped load/apply contract used for batch loading.
  - `IOptionComponent<T>` is the UI-side option contract with `LoadComponent` and `PerformValueChange`.
- Runtime flow:
  - `MainMenuInstaller` registers each concrete setting as scoped under its concrete type, typed `ISetting<...>` contract, and `ISettingLoader`.
  - `UserSettingsLoader.Awake` applies every bound `ISettingLoader`, so menu startup pushes stored/default settings into Unity systems and gameplay service toggles.
  - When a settings panel option enables, its UI component loads the stored/default value into its control without notifying where supported, then subscribes to value-change events.
  - When the player changes a setting, the option component saves the typed value through `SaveValue`, then calls `Load` to apply it immediately.
  - `ResolutionSetting.Load` reads the fullscreen setting's stored/default value when applying a resolution, so resolution changes preserve the current fullscreen preference.

## Rules and Invariants

- Critical behavior rules:
  - Keep setting implementations registered in `MainMenuInstaller` under both their typed `ISetting<...>` contract and `ISettingLoader`; otherwise option injection or batch loading can break.
  - Keep persistent keys stable unless intentionally migrating player settings: `"Volume"`, `"GraphicsQuality"`, `"FullScreenMode"`, `"Resolution"`, and `"DamageNumbersEnabled"`.
  - Keep setting application inside `Load`. UI components should save values and call `Load`, not duplicate application logic.
  - Keep settings dependencies explicit through Reflex. Do not replace `IAudioMixersManager` or `IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>` with scene searches or singleton access.
  - Preserve `ResolutionSetting` dependency on `ISetting<FullScreenSetting, FullScreenMode>` so resolution application uses the stored fullscreen mode.
- Ordering or sequencing guarantees:
  - `UserSettingsLoader.Awake` loads every setting available from the active Reflex container.
  - UI option components load their current value before subscribing to value-change callbacks in `OnEnable`.
  - UI option components unsubscribe from Unity UI callbacks in `OnDisable`.
  - Value changes persist before application: option components call `SaveValue` before `Load`.
- Constraints contributors must preserve:
  - Do not edit scene, prefab, asset, or meta files directly for settings wiring unless explicitly requested and the text change is safe to review.
  - Do not change setting default values as incidental cleanup; defaults are player-facing behavior.
  - Do not rename graphics quality labels without aligning Unity quality settings and `GraphicOption`'s lookup.
  - Treat audio volume values as mixer decibel values after slider conversion, not normalized slider values.

## Extension Points

- Safe extension areas:
  - Add a new persisted setting by implementing `ISetting<TSelf, TRepresentedBy>`, choosing a stable storage key and default value, implementing `Load`, and registering it in `MainMenuInstaller` as the concrete type, typed setting contract, and `ISettingLoader`.
  - Add a matching UI option by implementing `IOptionComponent<T>`, injecting the typed setting, loading the current value into the control without firing change events where possible, and subscribing/unsubscribing in `OnEnable`/`OnDisable`.
  - Add new screen-display behavior through `ScreenSerializableResolutionHelper` only when it preserves stored resolution compatibility.
  - Add additional audio setting dimensions by extending audio mixer contracts and binding them through Reflex before creating setting classes.
- Required dependencies and contracts:
  - `AudioVolumeSetting` requires `IAudioMixersManager`, currently provided by `BootLoader`.
  - `DamageNumbersSetting` requires `IEnableDisableFunctionalityTrigger<DamageNumbersSpawner>`, currently provided by `BootLoader`.
  - `ResolutionSetting` requires `ISetting<FullScreenSetting, FullScreenMode>`.
  - Settings UI components require scene-wired TMP, slider, toggle, and dropdown references.
- Testing implications:
  - Compile C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Manual Unity play-mode checks are required for settings panel wiring, immediate application, persisted reload after returning to the menu, fullscreen/resolution behavior on the target platform, and damage-number enable/disable behavior.
  - Repeatedly enable and disable settings panels after UI changes to verify listeners do not duplicate.

## Integration Notes

- Upstream dependencies:
  - `MainMenuInstaller` provides setting bindings in the Main Menu scene container.
  - `BootLoader` provides global audio mixer and damage-number functionality services consumed by setting implementations.
  - `AppStorage` provides JSON-backed persistence for all setting values.
  - `ScreenSerializableResolutionHelper` adapts Unity `Screen.resolutions` and `Screen.SetResolution` to `SerializableResolution`.
- Downstream consumers:
  - `AudioVolumeOption`, `GraphicOption`, `FullScreenOption`, `ResolutionOption`, and `DamageNumbersOption` consume typed setting contracts.
  - `AudioMixersManager`, Unity `QualitySettings`, Unity `Screen`, and `DamageNumbersSpawner` receive applied setting effects through each setting's `Load` method.
  - Other docs treat settings as the owner of persisted preferences for UI, audio, and damage-number visibility.
- Cross-system coupling risks:
  - Moving global service bindings out of `BootLoader` can break setting application and unrelated runtime consumers together.
  - Changing `AppStorage` path or serialization affects every persisted setting, not only Settings UI.
  - Changing fullscreen mode semantics can affect resolution application because `ResolutionSetting.Load` reads the fullscreen setting.
  - Changing damage-number setting behavior affects enemy hit feedback through the shared spawner enable/disable contract.

## Known Risks and Open Questions

- Known limitations:
  - `AppStorage.TryGetValue` swallows deserialization exceptions and falls back to default values without diagnostics.
  - `AppStorage` caches values statically and does not support external file changes after its static load.
  - `GraphicSetting.Load` throws when the stored/default quality name is not present in `QualitySettings.names`.
  - `GraphicOption` hard-codes `Low`, `Medium`, `High`, and `Ultra`; Unity quality settings must match those names and indices.
  - `AudioVolumeOption` uses `Mathf.Log10(_slider.value)`, so the slider minimum must stay above zero.
  - `ResolutionOption.LoadComponent` can set `-1` if the stored/default resolution is not in the current available-resolution list, while `ResolutionSetting.Load` has its own fallback path.
  - `ScreenSerializableResolutionHelper` caches available resolutions for the process lifetime.
- Open design questions:
  - Should setting storage keys move to domain constants with migration safeguards?
  - Should settings load failures log diagnostics for corrupt or incompatible `AppStorage.json` values?
  - Should display settings distinguish windowed, exclusive fullscreen, and maximized window as explicit UI options instead of a single boolean toggle?
  - Should `GraphicOption` derive dropdown options from `QualitySettings.names` instead of maintaining a separate hard-coded map?
- Suggested follow-up tasks:
  - Add a focused validation helper for settings UI controls to catch zero-volume sliders, mismatched graphics names, and invalid resolution indices.
  - Review `AppStorage` error handling before adding more player-facing preferences.
  - Consider a settings migration plan before changing existing keys, default values, or serialized value shapes.
