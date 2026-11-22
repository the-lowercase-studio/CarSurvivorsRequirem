# Scoreboard System Documentation

## Purpose

The Scoreboard system stores the player's top run records (elapsed time in seconds and survival outcome), migrates legacy stored score lists, applies dual-mode leaderboard sorting (survived runs ranked first by shortest completion time ascending, followed by defeated runs ranked by longest survival time descending), exposes outcome-specific best times for end-of-run and death-screen messaging, and renders stored records with status labels in the Main Menu scoreboard UI.

It is not responsible for measuring elapsed gameplay time, deciding when the player dies or completes a run, formatting unrelated UI, scene loading, or storage infrastructure. The timer, death flow, portal/end-of-run flow, UI panels, and app storage own those responsibilities; scoreboard code consumes them through narrow services or injected storage.

## Reading Map

- Primary code locations:
  - Assets/Scripts/ScoreBoard/StoredScoreBoard.cs
  - Assets/Scripts/ScoreBoard/ScoreBoardNewScoreSaver.cs
  - Assets/Scripts/ScoreBoard/ScoreBoardBestScoreGetter.cs
  - Assets/Scripts/ScoreBoard/ScoreBoardPresenter.cs
  - Assets/Scripts/ScoreBoard/ScoreBoardEntry.cs
  - Assets/Scripts/ScoreBoard/Constants/ScoreBoardConstants.cs
- Prefabs:
  - Assets/Prefabs/UI/ScoreBoard/ScoreBoardEntry.prefab
- Related runtime integration:
  - Assets/Scripts/ReflexDI/ProjectInstaller.cs
  - Assets/Scripts/UI/Death/PlayerDeathPresenter.cs
  - Assets/Scripts/UI/EndOfRun/DemoThankYouPresenter.cs
  - Assets/Scripts/UI/HUD/TimerPresenter.cs
  - Assets/Scripts/Storage/AppStorage.cs
- Automated tests:
  - Assets/Scripts/Editor/Tests/ScoreBoardDualSortTests.cs
- Related docs:
  - .agents/context/game-systems/storage-system.md
  - .agents/context/game-systems/ui-system.md
  - .agents/context/game-systems/game-flow-system.md
  - .agents/context/game-systems/level-system.md
  - .agents/context/project-coding-standards.md
  - .agents/context/technology-documentation.md
- Related agents or instructions:
  - Root AGENTS.md
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/di-integration/SKILL.md

## Architecture and Data Flow

- Core components:
  - ScoreBoardRecord: Serializable value struct implementing IEquatable<ScoreBoardRecord> with uint TimeSeconds and bool HasSurvived.
  - StoredScoreBoard: Storage adapter implementing IAppStorageValue<List<ScoreBoardRecord>> under ScoreBoardConstants.SCORE_BOARD_STORAGE_KEY ("ScoreBoard"). Its default value is an empty list and its maximum saved record count is ScoreBoardConstants.MAX_SAVED_SCORES_COUNT (6). Reads raw JSON via AppStorage.TryGetRawToken and parses records through ParseRecords(JToken), which seamlessly migrates legacy List<uint> integer arrays into ScoreBoardRecord(timeSeconds, hasSurvived: false) alongside modern object records.
  - IScoreBoardNewScoreSaver / ScoreBoardNewScoreSaver: Accepts Save(uint timeSeconds, bool hasSurvived = false), merges the new ScoreBoardRecord into stored records via InsertAndSort when it qualifies and is not already present, sorts records using CompareRecords (HasSurvived == true first ordered by shortest TimeSeconds ascending; HasSurvived == false second ordered by longest TimeSeconds descending), trims to ScoreBoardConstants.MAX_SAVED_SCORES_COUNT (6), and persists via StoredScoreBoard.SaveValue.
  - IScoreBoardBestScoreGetter / ScoreBoardBestScoreGetter: Exposes TryGetBestTime(bool hasSurvived, out uint bestTime) (returning the shortest TimeSeconds among survived records when hasSurvived is true, or the longest TimeSeconds among died records when hasSurvived is false).
  - ScoreBoardPresenter: Main Menu UI presenter. On enable, reads stored records, sorts them with ScoreBoardNewScoreSaver.CompareRecords, and instantiates one ScoreBoardEntry prefab per record under _scoreBoardEntriesParent. On disable, destroys generated child entries.
  - ScoreBoardEntry: Validates _orderNumberText, _scoreText, and _statusText fail-fast in Awake(), formats rank number (SetOrderNumber), formats time string via TimeConversionUtility.FormatSecondsToTimeString (SetScore), and formats survival outcome via SetStatus(bool hasSurvived) using ScoreBoardConstants.SURVIVED_STATUS_LABEL ("SURVIVED") with _survivedStatusColor or ScoreBoardConstants.DIED_STATUS_LABEL ("DIED") with _diedStatusColor. On Assets/Prefabs/UI/ScoreBoard/ScoreBoardEntry.prefab, Content (HorizontalLayoutGroup with padding Left = 16, Right = 16, spacing = 16, MiddleLeft alignment, ChildControlWidth/Height = true, ChildForceExpandWidth = false, ChildForceExpandHeight = true) orders columns left-to-right as OrderLabel (LayoutElement min/preferred width = 44, flexible = 0) -> StatusLabel (LayoutElement min/preferred width = 164, flexible = 0) -> ScoreLabel (LayoutElement min = 160, preferred = 173, flexible = 1) with no vertical separator bars, fitting strictly inside the 445.22 x 90 row background up to the 9-hour time budget ("9h 59m 59s"). OrderText (Center), StatusText (Left), and ScoreText (Right) share unified 30pt TextMeshProUGUI typography with AutoSizing (20..30), NoWrap, Ellipsis overflow, and zero margins.
- Key interfaces:
  - IAppStorageValue<List<ScoreBoardRecord>> is implemented by StoredScoreBoard.
  - IScoreBoardNewScoreSaver is consumed by PlayerDeathPresenter (hasSurvived: false) and DemoThankYouPresenter (hasSurvived: true).
  - IScoreBoardBestScoreGetter is consumed by PlayerDeathPresenter (TryGetBestTime(hasSurvived: false, ...)) and DemoThankYouPresenter (TryGetBestTime(hasSurvived: true, ...)) to detect whether the current run achieved a new best for its outcome category.
- Centralized constants:
  - ScoreBoardConstants (Assets/Scripts/ScoreBoard/Constants/ScoreBoardConstants.cs):
    - SCORE_BOARD_STORAGE_KEY = "ScoreBoard"
    - MAX_SAVED_SCORES_COUNT = 6
    - SURVIVED_STATUS_LABEL = "SURVIVED"
    - DIED_STATUS_LABEL = "DIED"
- Runtime flow:
  1. ProjectInstaller registers StoredScoreBoard, ScoreBoardNewScoreSaver as IScoreBoardNewScoreSaver, and ScoreBoardBestScoreGetter as IScoreBoardBestScoreGetter as project-level singletons.
  2. On player death, PlayerDeathPresenter.EnableDeathScreen calls IScoreBoardNewScoreSaver.Save(_timerPresenter.TimerValue, hasSurvived: false), then checks IScoreBoardBestScoreGetter.TryGetBestTime(hasSurvived: false, out uint bestTime) && bestTime == _timerPresenter.TimerValue to append "(New Best!)" to the "Time Alive:" label.
  3. On final-tier portal completion, DemoThankYouPresenter.EnableThankYouScreen calls IScoreBoardNewScoreSaver.Save(_timerPresenter.TimerValue, hasSurvived: true), then checks IScoreBoardBestScoreGetter.TryGetBestTime(hasSurvived: true, out uint bestTime) && bestTime == _timerPresenter.TimerValue to append "(New Best!)" to the "Completion Time:" label.
  4. When the Main Menu ScoreBoard panel is enabled, ScoreBoardPresenter reads stored ScoreBoardRecord entries, sorts them via ScoreBoardNewScoreSaver.CompareRecords, and populates rank, formatted time, and SURVIVED / DIED status on each instantiated ScoreBoardEntry.

## Rules and Invariants

- Critical behavior rules:
  - Dual-sorting order (ScoreBoardNewScoreSaver.CompareRecords):
    1. Records with HasSurvived == true always rank above records with HasSurvived == false.
    2. Among records with HasSurvived == true, shorter TimeSeconds rank higher (ascending speedrun clear time).
    3. Among records with HasSurvived == false, longer TimeSeconds rank higher (descending survival duration).
  - Stored scoreboard records must never exceed ScoreBoardConstants.MAX_SAVED_SCORES_COUNT (6); StoredScoreBoard.SaveValue throws ArgumentException if given more than 6 entries.
  - Duplicate records with identical (TimeSeconds, HasSurvived) are deduplicated by ContainsRecord so identical scores do not consume multiple leaderboard slots, while the same TimeSeconds with different HasSurvived values are treated as distinct records.
  - Legacy JSON arrays containing raw integer elements under the "ScoreBoard" key must continue to migrate cleanly to ScoreBoardRecord(timeSeconds, hasSurvived: false) via StoredScoreBoard.ParseRecords.
  - Score saving in PlayerDeathPresenter and DemoThankYouPresenter must execute before calling TryGetBestTime so a newly saved best time is recognized immediately.
  - ScoreBoardEntry.Awake() must fail fast with InvalidOperationException if _orderNumberText, _scoreText, or _statusText is unassigned.
- Ordering or sequencing guarantees:
  - PlayerDeathPresenter.EnableDeathScreen and DemoThankYouPresenter.EnableThankYouScreen save the score before formatting time text and comparing against TryGetBestTime.
  - ScoreBoardPresenter.OnDisable destroys generated entries so repeated panel enable/disable cycles never duplicate rows.
- Constraints contributors must preserve:
  - Keep scoreboard services registered in ProjectInstaller so they remain accessible across MainMenu and gameplay scenes.
  - Do not move elapsed-time ownership into the scoreboard; pass timer values from ITimerPresenter.
  - Preserve the "ScoreBoard" storage key and JToken migration path in StoredScoreBoard.ParseRecords to avoid breaking existing player save files.

## Extension Points

- Safe extension areas:
  - Add read-only scoreboard consumers through IScoreBoardBestScoreGetter or a new narrow query interface.
  - Customize row colors or typography on Assets/Prefabs/UI/ScoreBoard/ScoreBoardEntry.prefab while preserving _orderNumberText, _scoreText, and _statusText references.
- Required dependencies and contracts:
  - Runtime consumers that save scores should depend on IScoreBoardNewScoreSaver.
  - Runtime consumers that query best times should depend on IScoreBoardBestScoreGetter.
  - Scoreboard UI requires a ScoreBoardEntry prefab (with wired _orderNumberText, _scoreText, and _statusText), an entries parent transform, and Reflex injection for StoredScoreBoard.
- Testing implications:
  - Compile after C# edits with dotnet build CarSurvivors.Runtime.csproj -p:BuildProjectReferences=false and dotnet build CarSurvivors.Editor.Tests.csproj -p:BuildProjectReferences=false.
  - Run EditMode unit tests in Assets/Scripts/Editor/Tests/ScoreBoardDualSortTests.cs covering dual sorting (SURVIVED ascending above DIED descending), 6-entry trimming, worst-record rejection and deduplication, legacy uint JSON array migration, and outcome-specific TryGetBestTime lookups.
  - Manual UI validation in MainMenu and RuinedBloodCity for row instantiation, SURVIVED/DIED status styling, and "(New Best!)" messaging on both PlayerDeathPresenter and DemoThankYouPresenter.

## Integration Notes

- Upstream dependencies:
  - ITimerPresenter.TimerValue (seeded across tiers from IRunSessionService.AccumulatedRunSeconds) provides the cumulative run time in seconds.
  - AppStorage (TryGetRawToken and SetValue) provides JSON-backed persistence under Assets/Data/AppStorage.Editor.json (Editor) and Data/AppStorage.json (builds).
  - Reflex project-level bindings in ProjectInstaller provide scoreboard services across scenes.
- Downstream consumers:
  - PlayerDeathPresenter saves died records and queries best died time.
  - DemoThankYouPresenter saves survived records and queries best survived time.
  - ScoreBoardPresenter renders sorted records in the Main Menu scoreboard UI.
- Cross-system coupling risks:
  - Changing TimerPresenter units or cross-tier accumulation changes persisted score meaning and display.
  - Changing AppStorage serialization or file location affects saved scoreboard migration.

## Known Risks and Open Questions

- Known limitations:
  - Identical (TimeSeconds, HasSurvived) pairs are collapsed into a single leaderboard entry by ContainsRecord.
- Open design questions:
  - Should tied run times with the same survival outcome be shown as separate rows or remain intentionally collapsed?
  - Should future scoreboard entries record tier reached or active car skills?
- Suggested follow-up tasks:
  - Consider exposing a read-only all-records query method on an interface if additional UI presenters need full leaderboard access without injecting StoredScoreBoard directly.
