# Storage System Documentation

## Purpose

The Storage system provides a small JSON-backed persistence wrapper for app-level values such as user settings and scoreboard entries. It centralizes file read/write behavior in Assets/Scripts/Storage/AppStorage.cs and gives persisted value owners a shared Assets/Scripts/Storage/IAppStorageValue.cs contract.

It is not responsible for applying settings to Unity systems, deciding scoreboard ranking rules, presenting stored values in UI, or validating every domain-specific value. Those responsibilities stay in Settings/, ScoreBoard/, UI/Settings/, and their owning runtime systems.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Storage/AppStorage.cs
  - Assets/Scripts/Storage/IAppStorageValue.cs
  - Assets/Scripts/Storage/Constants/StorageConstants.cs
- Main consumers:
  - Assets/Scripts/ScoreBoard/StoredScoreBoard.cs
  - Assets/Scripts/ScoreBoard/ScoreBoardNewScoreSaver.cs
  - Assets/Scripts/ScoreBoard/ScoreBoardBestScoreGetter.cs
  - Assets/Scripts/Settings/UserSettingsLoader.cs
- Related runtime integration:
  - Assets/Scripts/ReflexDI/MainMenuInstaller.cs
  - Assets/Scripts/ReflexDI/ProjectInstaller.cs
- Related docs:
  - .agents/context/game-systems/scoreboard-system.md
  - .agents/context/game-systems/settings-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/game-systems/audio-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/technology-documentation.md
- Related agents or instructions:
  - Root AGENTS.md
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md

## Architecture and Data Flow

- Core components:
  - Assets/Scripts/Storage/AppStorage.cs is a static key/value store backed by Assets/Data/AppStorage.Editor.json in the Unity Editor and Data/AppStorage.json under the build root resolved from Directory.GetParent(Application.dataPath) in builds, with AppDomain.CurrentDomain.BaseDirectory only as a fallback when that parent is unavailable.
  - Assets/Scripts/Storage/AppStorage.cs keeps an in-memory Dictionary<string, JToken> cache initialized by its static constructor. It exposes TryGetValue<T>(string key, out T value), TryGetRawToken(string key, out JToken token) for schema-migration inspection, and SetValue<T>(string key, T value).
  - Assets/Scripts/Storage/IAppStorageValue.cs defines the contract for domain-owned persisted values: DefaultValue, GetKey(), GetValueOrStoredDefault(), and SaveValue(T value).
  - Settings extend the storage contract through ISetting<TSelf, TRepresentedBy>, which combines IAppStorageValue<TRepresentedBy> with ISettingLoader.
  - Assets/Scripts/ScoreBoard/StoredScoreBoard.cs implements IAppStorageValue<List<ScoreBoardRecord>> directly (using AppStorage.TryGetRawToken to migrate legacy List<uint> arrays into ScoreBoardRecord lists) and is registered as a project-level singleton.
- Key storage keys:
  - "Volume" stores the audio mixer volume value.
  - "DamageNumbersEnabled" stores whether damage numbers are enabled.
  - "FullScreenMode" stores the Unity FullScreenMode.
  - "GraphicsQuality" stores the selected quality level name.
  - "Resolution" stores SerializableResolution.
  - "ScoreBoard" (ScoreBoardConstants.SCORE_BOARD_STORAGE_KEY) stores saved scoreboard records.
- Runtime flow:
  1. The first reference to Assets/Scripts/Storage/AppStorage.cs runs the static constructor and calls Load().
  2. Load() reads the active target's storage JSON when present, deserializes it into Dictionary<string, JToken>, and falls back to an empty dictionary (logging an error if an exception occurs) when the file is missing, unreadable, or deserializes to null.
  3. Consumers call TryGetValue<T>(key, out value) (or TryGetRawToken(key, out JToken token) when migrating legacy token shapes such as StoredScoreBoard) through their own GetValueOrStoredDefault() method.
  4. If a key is missing or conversion to T throws, the consumer returns its domain-owned DefaultValue.
  5. Consumers call SetValue<T>(key, value) through SaveValue(T value).
  6. SetValue creates the active Data directory when needed, updates the cache, serializes the full dictionary with indented JSON, and overwrites the active storage JSON (catching and logging any IO/serialization exception via Debug.LogError).
  7. Assets/Scripts/Settings/UserSettingsLoader.cs Awake iterates injected ISettingLoader instances so settings can load stored/default values and apply them to Unity systems.
  8. Scoreboard services read and write Assets/Scripts/ScoreBoard/StoredScoreBoard.cs from project-level services and menu/end-of-run presenters.

## Rules and Invariants

- Critical behavior rules:
  - Keep Assets/Scripts/Storage/AppStorage.cs generic and domain-neutral. Domain keys, defaults, validation, migration, and apply behavior belong to the owning setting or scoreboard class.
  - Keep persisted values accessed through Assets/Scripts/Storage/IAppStorageValue.cs or a more specific setting/scoreboard service instead of scattering raw AppStorage calls across UI and gameplay classes.
  - Preserve the current storage file locations unless the user explicitly approves a migration plan. Existing build persisted data is tied to Data/AppStorage.json under the build root adjacent to the player data folder, while Editor persisted data is tied to Assets/Data/AppStorage.Editor.json.
  - Preserve the fallback behavior where missing or non-convertible values return domain defaults instead of failing callers.
  - Keep settings binding in Assets/Scripts/ReflexDI/MainMenuInstaller.cs when adding a new ISetting<TSelf, TRepresentedBy> implementation.
- Ordering or sequencing guarantees:
  - AppStorage.Load() runs before any public TryGetValue, TryGetRawToken, or SetValue call because it is invoked by the static constructor.
  - SetValue writes immediately; there is no deferred save queue.
  - Settings are applied when Assets/Scripts/Settings/UserSettingsLoader.cs Awake runs and when setting UI options save and reload their setting.
- Constraints contributors must preserve:
  - Do not move setting application logic into Assets/Scripts/Storage/AppStorage.cs; it should only persist and retrieve values.
  - Do not change key strings casually. A renamed key is a persisted-data breaking change unless a migration path reads the previous key.
  - Do not directly edit generated local storage files as project source. Data/AppStorage.json and Assets/Data/AppStorage.Editor.json are runtime output, not authoritative project assets.
  - Follow ADR-004 before editing .prefab, .unity, .asset, or .meta files directly.

## Extension Points

- Safe extension areas:
  - Add a new persisted user setting by implementing ISetting<TSelf, TRepresentedBy>, defining a stable key and default, binding it in Assets/Scripts/ReflexDI/MainMenuInstaller.cs, and wiring the matching UI option to call SaveValue and Load.
  - Add a non-setting persisted value by implementing Assets/Scripts/Storage/IAppStorageValue.cs in the owning domain and registering that domain service through the appropriate Reflex installer.
  - Add domain validation inside the owning persisted-value class before calling AppStorage.SetValue.
  - Add migration logic in the owning persisted-value class using AppStorage.TryGetRawToken when changing serialized data shapes (as demonstrated by StoredScoreBoard.ParseRecords).
- Required dependencies and contracts:
  - Persisted value types must be serializable by Newtonsoft Json.NET and convertible back from JToken.ToObject<T>() (or parsed via TryGetRawToken).
  - Settings that apply runtime behavior should implement ISettingLoader through ISetting<TSelf, TRepresentedBy> and be available to Assets/Scripts/Settings/UserSettingsLoader.cs.
  - Scoreboard consumers expect Assets/Scripts/ScoreBoard/StoredScoreBoard.cs to be a project-level singleton from Assets/Scripts/ReflexDI/ProjectInstaller.cs.
- Testing implications:
  - Documentation-only storage changes only need path/link review.
  - C# changes should be compiled with dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false.
  - Scoreboard token parsing and legacy List<uint> migration are covered by EditMode tests in Assets/Scripts/Editor/Tests/ScoreBoardDualSortTests.cs.
  - Manual validation is needed for settings UI controls because saved values must both persist and apply to Unity systems such as Screen, QualitySettings, audio mixers, and damage number enablement.

## Integration Notes

- Upstream dependencies:
  - Assets/Scripts/Storage/AppStorage.cs depends on System.IO, Directory.GetParent(Application.dataPath), fallback AppDomain.CurrentDomain.BaseDirectory, Newtonsoft JsonConvert, and JToken.
  - Settings depend on storage for persisted values and on their runtime systems for apply behavior.
  - ResolutionSetting depends on FullScreenSetting and available Unity screen resolutions when applying stored resolution data.
  - Scoreboard services depend on Assets/Scripts/ScoreBoard/StoredScoreBoard.cs for persisted score record lists.
- Downstream consumers:
  - AudioVolumeOption, DamageNumbersOption, FullScreenOption, GraphicOption, and ResolutionOption call their injected settings from the settings UI.
  - Assets/Scripts/Settings/UserSettingsLoader.cs loads all Reflex-bound ISettingLoader instances.
  - Assets/Scripts/ScoreBoard/ScoreBoardBestScoreGetter.cs, Assets/Scripts/ScoreBoard/ScoreBoardNewScoreSaver.cs, and Assets/Scripts/ScoreBoard/ScoreBoardPresenter.cs consume Assets/Scripts/ScoreBoard/StoredScoreBoard.cs.
  - Assets/Scripts/UI/Death/PlayerDeathPresenter.cs and Assets/Scripts/UI/EndOfRun/DemoThankYouPresenter.cs indirectly write the scoreboard through IScoreBoardNewScoreSaver.
- Cross-system coupling risks:
  - Changing Assets/Scripts/Storage/AppStorage.cs serialization affects all settings and scoreboard data at once.
  - Changing a setting key can silently reset user preferences to defaults.
  - Changing when settings load can affect startup visuals, audio volume, fullscreen mode, resolution, and damage number availability.

## Known Risks and Open Questions

- Known limitations:
  - TryGetValue<T> swallows conversion exceptions without logging, so invalid stored values silently fall back to defaults.
  - Assets/Scripts/Storage/AppStorage.cs writes the full JSON file on every SetValue call and has no batching or concurrency protection.
  - Storage is static and not DI-bound, which keeps usage simple but makes isolated file-level tests harder (domain adapters like StoredScoreBoard.ParseRecords and ScoreBoardNewScoreSaver.InsertAndSort expose static pure methods for EditMode testing).
  - SettingsFilePath is selected with UNITY_EDITOR, so Editor and build runs use separate JSON files.
- Open design questions:
  - Should storage move behind an injectable interface to support isolated storage-file unit tests or alternate backends?
- Suggested follow-up tasks:
  - Add focused persistence tests around missing/corrupt settings files, enum conversion, and SerializableResolution round-trips.
  - Review whether AppStorage.TryGetValue<T> should log conversion warnings with the key name to make malformed persisted data easier to diagnose.
