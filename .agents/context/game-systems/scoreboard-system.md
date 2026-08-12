# Scoreboard System Documentation

## Purpose

The Scoreboard system stores the player's best survival times, exposes the best score for death-screen messaging, saves new qualifying scores, and renders stored scores in the scoreboard UI.

It is not responsible for measuring elapsed gameplay time, deciding when the player dies, formatting unrelated UI, scene loading, or storage infrastructure. The timer, death flow, UI panels, and app storage own those responsibilities; scoreboard code consumes them through narrow services or injected storage.

## Reading Map

- Primary code locations:
  - Assets/Scripts/ScoreBoard/StoredScoreBoard.cs
  - Assets/Scripts/ScoreBoard/ScoreBoardNewScoreSaver.cs
  - Assets/Scripts/ScoreBoard/ScoreBoardBestScoreGetter.cs
  - Assets/Scripts/ScoreBoard/ScoreBoardPresenter.cs
  - Assets/Scripts/ScoreBoard/ScoreBoardEntry.cs
- Related runtime integration:
  - Assets/Scripts/ReflexDI/ProjectInstaller.cs
  - Assets/Scripts/UI/Death/PlayerDeathPresenter.cs
  - Assets/Scripts/UI/HUD/TimerPresenter.cs
  - Assets/Scripts/Storage/AppStorage.cs
- Related docs:
  - .agents/context/game-systems/storage-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/game-systems/level-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/technology-documentation.md
- Related agents or instructions:
  - Root AGENTS.md
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md

## Architecture and Data Flow

- Core components:
  - `StoredScoreBoard` is the storage adapter for `List<uint>` scores under the `ScoreBoard` app-storage key. Its default value is an empty list and its current maximum saved score count is 6.
  - `IScoreBoardNewScoreSaver` / `ScoreBoardNewScoreSaver` accepts a survival-time score in seconds, merges it into stored scores when it qualifies, keeps scores sorted descending, and trims to the storage limit before saving.
  - `IScoreBoardBestScoreGetter` / `ScoreBoardBestScoreGetter` reads stored scores and returns the maximum score, or `0` when no score exists.
  - `ScoreBoardPresenter` is a scene UI presenter. On enable, it instantiates one `ScoreBoardEntry` prefab per stored score under a configured parent transform. On disable, it destroys those generated entries.
  - `ScoreBoardEntry` displays one rank number and one formatted time string using `TimeConversionUtility.FormatSecondsToTimeString`.
- Key interfaces:
  - `IAppStorageValue<List<uint>>` is implemented by `StoredScoreBoard`, but `StoredScoreBoard` itself is what the scoreboard services and presenter currently receive.
  - `IScoreBoardNewScoreSaver` is consumed by `PlayerDeathPresenter` to save the current timer value.
  - `IScoreBoardBestScoreGetter` is consumed by `PlayerDeathPresenter` to detect whether the displayed death-screen time is the best stored score.
- Runtime flow:
  1. `ProjectInstaller` registers `StoredScoreBoard`, `ScoreBoardNewScoreSaver` as `IScoreBoardNewScoreSaver`, and `ScoreBoardBestScoreGetter` as `IScoreBoardBestScoreGetter` as project-level singletons.
  2. During the death-screen flow, `PlayerDeathPresenter.EnableDeathScreen` calls `IScoreBoardNewScoreSaver.Save(_timerPresenter.TimerValue)` before setting level and time text.
  3. `ScoreBoardNewScoreSaver` builds a descending `SortedSet<uint>` from stored values, rejects non-qualifying scores, removes low scores when the list is full, then persists the updated list through `StoredScoreBoard.SaveValue`.
  4. `PlayerDeathPresenter` compares `IScoreBoardBestScoreGetter.GetBestScore()` with the current timer value. If they match, the death-screen time text appends the new-best marker.
  5. When the scoreboard UI is enabled, `ScoreBoardPresenter` reads stored scores in their saved order and creates numbered entries from first to last.

## Rules and Invariants

- Critical behavior rules:
  - Scores represent elapsed survival time in seconds as `uint`; higher values are better.
  - Stored scoreboard values must never exceed `StoredScoreBoard.MAX_SAVED_SCORES_COUNT`; `StoredScoreBoard.SaveValue` throws if given too many values.
  - Saved values are expected to be sorted descending by `ScoreBoardNewScoreSaver`; `ScoreBoardPresenter` trusts stored order when assigning rank numbers.
  - Death-screen score saving must happen before best-score comparison so a newly saved best score can be announced immediately.
  - Score text formatting belongs in `ScoreBoardEntry` and `PlayerDeathPresenter` through `TimeConversionUtility`; persistence keeps raw seconds.
- Ordering or sequencing guarantees:
  - `PlayerDeathPresenter.EnableDeathScreen` saves score, sets level text, sets time text, enables the visual, switches death audio, then pauses `GameTime`.
  - `ScoreBoardPresenter.OnDisable` clears generated entries, so repeated enable/disable cycles should not duplicate visible rows.
- Constraints contributors must preserve:
  - Keep scoreboard services registered in `ProjectInstaller` unless the lifetime model intentionally changes across scenes.
  - Do not move elapsed-time ownership into the scoreboard. Continue to pass in timer values from `ITimerPresenter` or an equivalent explicit timing service.
  - Do not change storage key, score type, max count, sorting order, or displayed time format without checking migration and UI implications.
  - Do not edit scoreboard scene/prefab references directly in text unless the user explicitly requests it and the change is safe to review.

## Extension Points

- Safe extension areas:
  - Add read-only scoreboard consumers through `IScoreBoardBestScoreGetter` or a new narrow query interface if callers need more than the best value.
  - Add UI-only row visuals by changing `ScoreBoardEntry` or its prefab wiring while preserving the raw stored score contract.
  - Add explicit clear/reset tooling for development if it is kept separate from normal gameplay flow and uses the storage adapter deliberately.
- Required dependencies and contracts:
  - Runtime consumers that save scores should depend on `IScoreBoardNewScoreSaver`, not `StoredScoreBoard`.
  - Runtime consumers that only need the best score should depend on `IScoreBoardBestScoreGetter`.
  - Scoreboard UI requires a `ScoreBoardEntry` prefab, an entries parent transform, and Reflex injection for `StoredScoreBoard`.
  - Storage depends on `AppStorage`, which persists JSON under Assets/Data/AppStorage.Editor.json in the Unity Editor and Data/AppStorage.json under the build root resolved from `Directory.GetParent(Application.dataPath)` in builds.
- Testing implications:
  - Focused edit-mode tests for `ScoreBoardNewScoreSaver` should cover empty storage, duplicate scores, full storage, lower-than-minimum rejection, higher-than-current-best insertion, and max-count enforcement.
  - Play-mode or manual UI validation is needed for scoreboard row instantiation, repeated panel enable/disable cycles, and death-screen "New Best!" messaging.

## Integration Notes

- Upstream dependencies:
  - `ITimerPresenter.TimerValue` provides the score saved on death.
  - `AppStorage` provides JSON-backed persistence and cache loading.
  - Reflex project-level bindings provide scoreboard services across scenes.
- Downstream consumers:
  - `PlayerDeathPresenter` saves new scores and reads the best score for death-screen copy.
  - `ScoreBoardPresenter` renders stored values in the scoreboard UI.
- Cross-system coupling risks:
  - Changing `TimerPresenter` units or lifetime changes persisted score meaning and display.
  - Changing `AppStorage` serialization or file location can affect saved scoreboard migration.
  - Changing death-screen ordering can break immediate new-best detection.
  - Changing score uniqueness behavior affects whether equal survival times can appear as separate leaderboard rows.

## Known Risks and Open Questions

- Known limitations:
  - `ScoreBoardNewScoreSaver` uses `SortedSet<uint>`, so duplicate score values collapse into one entry.
  - When the scoreboard is empty, `Save` compares the new score with `LastOrDefault()`; a score of `0` is rejected and never stored.
  - `StoredScoreBoard.MAX_SAVED_SCORES_COUNT` is a public mutable field rather than an immutable constant or property.
  - `StoredScoreBoard.GetValueOrStoredDefault` returns the stored list reference from `AppStorage` when deserialization succeeds, so callers should avoid mutating it directly without saving.
  - Existing stored data with more than six scores is not trimmed when read; the exception only occurs when saving too many values.
- Open design questions:
  - Should tied survival times be shown as separate runs or intentionally collapsed?
  - Should the scoreboard support player names, run metadata, difficulty, or build-version migration before broader persistence work?
  - Should max saved score count become a private constant under a scoreboard constants folder during a dedicated coding-standards cleanup?
- Suggested follow-up tasks:
  - Add tests around `ScoreBoardNewScoreSaver` qualification, ordering, trimming, and duplicate handling.
  - Decide whether duplicate scores and zero-second scores should be valid leaderboard entries.
  - Consider exposing a read-only all-scores interface if more UI or analytics consumers need scoreboard data without depending on `StoredScoreBoard`.
