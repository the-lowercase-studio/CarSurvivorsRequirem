# Storage System Documentation

## Purpose

The Storage system provides a small JSON-backed persistence wrapper for app-level values such as user settings and scoreboard entries. It centralizes file read/write behavior in `AppStorage` and gives persisted value owners a shared `IAppStorageValue<T>` contract.

It is not responsible for applying settings to Unity systems, deciding scoreboard ranking rules, presenting stored values in UI, or validating every domain-specific value. Those responsibilities stay in `Settings/`, `ScoreBoard/`, `UI/Settings/`, and their owning runtime systems.

## Reading Map

- Primary code locations:
  - `Assets/Scripts/Storage/AppStorage.cs`
  - `Assets/Scripts/Storage/IAppStorageValue.cs`
- Main consumers:
  - `Assets/Scripts/Settings/`
  - `Assets/Scripts/Settings/Resolution/`
  - `Assets/Scripts/ScoreBoard/`
  - `Assets/Scripts/UI/Settings/`
- Related runtime integration:
  - `Assets/Scripts/ReflexDI/MainMenuInstaller.cs`
  - `Assets/Scripts/ReflexDI/ProjectInstaller.cs`
- Related docs:
  - `.agents/context/ui-system.md`
  - `.agents/context/audio-system.md`
  - `.agents/context/project-scripts-folder-map.md`
  - `.agents/context/project-coding-standards.md`
- Related skills:
  - `.agents/skills/di-integration/SKILL.md` for adding or reviewing Reflex-bound persisted setting services.
  - `.agents/skills/preserve-coding-standards/SKILL.md` for cleanup in touched storage, settings, or scoreboard files.
  - `.agents/skills/unity-refactor-suggestions/SKILL.md` for behavior-preserving persistence refactors.

## Architecture and Data Flow

- Core components:
  - `AppStorage` is a static key/value store backed by `Assets/Data/AppStorage.Editor.json` in the Unity Editor and `Data/AppStorage.json` under `AppDomain.CurrentDomain.BaseDirectory` in builds.
  - `AppStorage` keeps an in-memory `Dictionary<string, JToken>` cache initialized by its static constructor.
  - `IAppStorageValue<T>` defines the contract for domain-owned persisted values: `DefaultValue`, `GetKey()`, `GetValueOrStoredDefault()`, and `SaveValue(T value)`.
  - Settings extend the storage contract through `ISetting<TSelf, TRepresentedBy>`, which combines `IAppStorageValue<TRepresentedBy>` with `ISettingLoader`.
  - `StoredScoreBoard` implements `IAppStorageValue<List<uint>>` directly and is registered as a project-level singleton.
- Key storage keys:
  - `"Volume"` stores the audio mixer volume value.
  - `"DamageNumbersEnabled"` stores whether damage numbers are enabled.
  - `"FullScreenMode"` stores the Unity `FullScreenMode`.
  - `"GraphicsQuality"` stores the selected quality level name.
  - `"Resolution"` stores `SerializableResolution`.
  - `"ScoreBoard"` stores saved scoreboard values.
- Runtime flow:
  1. The first reference to `AppStorage` runs the static constructor and calls `Load()`.
  2. `Load()` reads the active target's storage JSON when present, deserializes it into `Dictionary<string, JToken>`, and falls back to an empty dictionary when the file is missing or the JSON root deserializes to null.
  3. Consumers call `TryGetValue<T>(key, out value)` through their own `GetValueOrStoredDefault()` method.
  4. If a key is missing or conversion to `T` throws, the consumer returns its domain-owned `DefaultValue`.
  5. Consumers call `SetValue<T>(key, value)` through `SaveValue(T value)`.
  6. `SetValue` creates the active `Data` directory when needed, updates the cache, serializes the full dictionary with indented JSON, and overwrites the active storage JSON.
  7. `UserSettingsLoader.Awake` iterates injected `ISettingLoader` instances so settings can load stored/default values and apply them to Unity systems.
  8. Scoreboard services read and write `StoredScoreBoard` from project-level services and menu presenters.

## Rules and Invariants

- Critical behavior rules:
  - Keep `AppStorage` generic and domain-neutral. Domain keys, defaults, validation, and apply behavior belong to the owning setting or scoreboard class.
  - Keep persisted values accessed through `IAppStorageValue<T>` or a more specific setting/scoreboard service instead of scattering raw `AppStorage` calls across UI and gameplay classes.
  - Preserve the current storage file locations unless the user explicitly approves a migration plan. Existing build persisted data is tied to `Data/AppStorage.json` under the application base directory, while Editor persisted data is tied to `Assets/Data/AppStorage.Editor.json`.
  - Preserve the fallback behavior where missing or non-convertible values return domain defaults instead of failing callers.
  - Keep settings binding in `MainMenuInstaller` when adding a new `ISetting<TSelf, TRepresentedBy>` implementation.
- Ordering or sequencing guarantees:
  - `AppStorage.Load()` runs before any public `TryGetValue` or `SetValue` call because it is invoked by the static constructor.
  - `SetValue` writes immediately; there is no deferred save queue.
  - Settings are applied when `UserSettingsLoader.Awake` runs and when setting UI options save and reload their setting.
- Constraints contributors must preserve:
  - Do not move setting application logic into `AppStorage`; it should only persist and retrieve values.
  - Do not change key strings casually. A renamed key is a persisted-data breaking change unless a migration path reads the previous key.
  - Do not directly edit generated local storage files as project source. `Data/AppStorage.json` and `Assets/Data/AppStorage.Editor.json` are runtime output, not authoritative project assets.
  - Do not edit `.prefab`, `.unity`, `.asset`, or `.meta` files directly for storage integration unless the user explicitly requests it and the change is safe to review as text.

## Extension Points

- Safe extension areas:
  - Add a new persisted user setting by implementing `ISetting<TSelf, TRepresentedBy>`, defining a stable key and default, binding it in `MainMenuInstaller`, and wiring the matching UI option to call `SaveValue` and `Load`.
  - Add a non-setting persisted value by implementing `IAppStorageValue<T>` in the owning domain and registering that domain service through the appropriate Reflex installer.
  - Add domain validation inside the owning persisted-value class before calling `AppStorage.SetValue`.
  - Add migration logic in the owning persisted-value class when changing key names or serialized data shapes.
- Required dependencies and contracts:
  - Persisted value types must be serializable by Newtonsoft Json.NET and convertible back from `JToken.ToObject<T>()`.
  - Settings that apply runtime behavior should implement `ISettingLoader` through `ISetting<TSelf, TRepresentedBy>` and be available to `UserSettingsLoader`.
  - Scoreboard consumers expect `StoredScoreBoard` to be a project-level singleton from `ProjectInstaller`.
- Testing implications:
  - Documentation-only storage changes only need path/link review.
  - C# changes should be compiled with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Persistence behavior needs edit-mode or play-mode coverage for missing files, missing keys, invalid JSON or invalid value shapes, enum round-trips, `SerializableResolution` round-trips, and scoreboard list saves.
  - Manual validation is needed for settings UI controls because saved values must both persist and apply to Unity systems such as `Screen`, `QualitySettings`, audio mixers, and damage number enablement.

## Integration Notes

- Upstream dependencies:
  - `AppStorage` depends on `System.IO`, `AppDomain.CurrentDomain.BaseDirectory`, Unity `Application.dataPath`, Newtonsoft `JsonConvert`, and `JToken`.
  - Settings depend on storage for persisted values and on their runtime systems for apply behavior.
  - `ResolutionSetting` depends on `FullScreenSetting` and available Unity screen resolutions when applying stored resolution data.
  - Scoreboard services depend on `StoredScoreBoard` for persisted score lists.
- Downstream consumers:
  - `AudioVolumeOption`, `DamageNumbersOption`, `FullScreenOption`, `GraphicOption`, and `ResolutionOption` call their injected settings from the settings UI.
  - `UserSettingsLoader` loads all Reflex-bound `ISettingLoader` instances.
  - `ScoreBoardBestScoreGetter`, `ScoreBoardNewScoreSaver`, and `ScoreBoardPresenter` consume `StoredScoreBoard`.
  - `PlayerDeathPresenter` indirectly writes the scoreboard through `IScoreBoardNewScoreSaver`.
- Cross-system coupling risks:
  - Changing `AppStorage` serialization affects all settings and scoreboard data at once.
  - Changing a setting key can silently reset user preferences to defaults.
  - Changing fallback behavior can turn corrupt or stale persisted data into runtime errors in settings and scoreboard consumers.
  - Changing when settings load can affect startup visuals, audio volume, fullscreen mode, resolution, and damage number availability.

## Known Risks and Open Questions

- Known limitations:
  - `AppStorage.Load()` does not catch file read or JSON parse exceptions. A corrupt `AppStorage.json` can fail static initialization.
  - `TryGetValue<T>` swallows conversion exceptions without logging, so invalid stored values silently fall back to defaults.
  - `AppStorage` writes the full JSON file on every `SetValue` call and has no batching or concurrency protection.
  - Storage is static and not DI-bound, which keeps usage simple but makes isolated tests harder.
  - `SettingsFilePath` is selected with `UNITY_EDITOR`, so Editor and build runs do not share the same JSON file.
  - `StoredScoreBoard.MAX_SAVED_SCORES_COUNT` is a mutable public field even though it behaves like a constant.
- Open design questions:
  - Should storage move behind an injectable interface to support tests, alternate backends, or platform-specific persistence?
  - Should invalid JSON reset to an empty store, back up the corrupt file, or surface an explicit recovery error?
  - Should persisted key strings move to domain constants before adding more stored values?
  - Should score lists preserve duplicate equal scores, or is the current `SortedSet<uint>` behavior intentionally unique by value?
- Suggested follow-up tasks:
  - Add focused persistence tests around missing/corrupt files, enum conversion, and complex value round-trips.
  - Review whether `AppStorage` should log conversion failures with the key name to make bad persisted data diagnosable.
  - Convert `StoredScoreBoard.MAX_SAVED_SCORES_COUNT` to a private constant in a scoped cleanup if behavior compatibility allows.
