# Specification: Dev Console Command: [Command Name]

Date: [YYYY-MM-DD]  
Author: [Agent / Author]  
Status: Draft  
Target Systems: Assets/Scripts/UI/DevConsole/, Assets/Scripts/ReflexDI/, [Target Domain Systems, e.g. Assets/Scripts/Player/]  

---

## 1. Overview & Command Intent

- Command Syntax: [e.g. command_name <param1> [optional_param2] [--flag]]
- Description: [One-line summary of what the command does, as displayed in help output]
- Purpose & Developer Value: [Why this command is needed and how developers/designers will use it during testing and balancing]
- Target Domain: [The affected gameplay or engine subsystem, e.g. Player Health, Enemies Spawner, Waves, GameTime]
- Example Invocations:
  - [e.g. command_name 10]
  - [e.g. command_name 50 --auto]

---

## 2. Resolved Decisions & Open Questions

### Resolved Decisions
- Decision 1 (Command Syntax & Aliases): [Confirmed command name, parameter format, and any shortcuts or aliases]
- Decision 2 (Registrar Placement): [Confirmed registrar class, e.g. GameplayDevCommandsRegistrar vs dedicated domain registrar]
- Decision 3 (Domain Integration): [Confirmed methods and services invoked on the target system]
- Decision 4 (Build Stripping): [Confirmed release build stripping strategy]

### Open Questions (Consequential Unknowns Only)

Reuse resolved choices. State None when no consequential unknown remains; routine reversible technical choices do not require repeated approval. Save the skeleton at its canonical plan path before asking questions. Runtime implementation still follows the requirements gate in AGENTS.md.
- [ ] Q1: [Unresolved critical decision, parameter boundary, or dependency requirement]

---

## 3. Command Syntax, Tokens & Validation

### Token Structure
- Token 0 (Command Name): [normalized string, case-insensitive]
- Token 1..N (Arguments):
  - Argument 1: [Name, type (int/float/string), range/bounds, default value, required vs optional]
  - Argument 2 / Flags: [Flag name (e.g. --flag / -f), effect, default value]

### Validation & Error Handling
- Argument Count Check: [Behavior when too few or too many arguments are provided]
- Parsing Logic: [int.TryParse, float.TryParse with CultureInfo.InvariantCulture, enum matching, or string comparison]
- Error Feedback Messages:
  - Invalid argument count: IDevConsoleService.LogError("Usage: [syntax]")
  - Out of range / invalid format: IDevConsoleService.LogError("[specific error message]")
- Success Feedback:
  - IDevConsoleService.Log("[success confirmation message]")

### Constants Declaration
Constants to declare in Assets/Scripts/UI/DevConsole/Constants/DevConsoleConstants.cs (or domain Constants):
- COMMAND_NAME: [string]
- COMMAND_SYNTAX: [string]
- COMMAND_DESCRIPTION: [string]
- [Default values, limits, or flag tokens]

---

## 4. Architecture & Reflex DI Integration

### Registrar Component
- Target Class: [Assets/Scripts/UI/DevConsole/GameplayDevCommandsRegistrar.cs or new dedicated registrar]
- Interface: [IGameplayDevCommandsRegistrar or new domain registrar interface]
- Injection Order:
  - [Inject] private readonly IDevConsoleService _devConsoleService = null;
  - [Inject] private readonly [ITargetService] _targetService = null;
  - [SerializeField] private fields (if any)
  - Private fields

### Lifecycle Registration
- Start():
  - Check IsConsoleSupported.
  - _devConsoleService.RegisterCommand(COMMAND_NAME, COMMAND_SYNTAX, COMMAND_DESCRIPTION, HandleCommand);
- OnDestroy():
  - Check IsConsoleSupported.
  - _devConsoleService.UnregisterCommand(COMMAND_NAME);

### Reflex DI Container Wiring
- Installer: Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs (or target scene installer)
- Binding requirements: [Confirm target domain service is already bound or add binding if missing]

---

## 5. Security & Build Configuration Invariants

- Compile-Time & Runtime Stripping:
  - Code must be protected by #if UNITY_EDITOR || DEVELOPMENT_BUILD.
  - Runtime check must evaluate Debug.isDebugBuild.
  - In release builds, registrar must disable itself in Awake() and register zero commands.
  - IDevConsoleService remains inert in release builds.
- Zero Release Overhead:
  - No heap allocations, no update polling, and no UI rendering in production release builds.

---

## 6. Edge Cases, Safety & Lifecycle Invariants

- Null Dependency Handling:
  - If target domain service is null (e.g. running in an isolated test scene without the full gameplay harness), log a diagnostic error via _devConsoleService.LogError and return gracefully without throwing NullReferenceException.
- Simulation State & Pause:
  - Dev console pauses gameplay time via GameTime.Pause() while open. Ensure any time-dependent manipulations (e.g. time scale or delay) account for GameTime pause state.
- Scene Lifecycle & Teardown:
  - Ensure command unregistration in OnDestroy() prevents stale delegate invocation after scene transitions.

---

## 7. Implementation Plan (Phases & Steps)

### Phase 1: Constants & Registration Wiring
- [ ] Step 1.1: Declare command constants (name, syntax, description, limits) in Assets/Scripts/UI/DevConsole/Constants/DevConsoleConstants.cs.
  - Verification: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
- [ ] Step 1.2: Add command registration in Start() and unregistration in OnDestroy() within the registrar.
  - Verification: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false

### Phase 2: Domain Logic & Parameter Parsing
- [ ] Step 2.1: Implement handler method with argument validation, parsing, error logging, and domain execution.
  - Verification: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
- [ ] Step 2.2: Add unit tests or integration checks for parsing logic and boundary validation.
  - Files: Assets/Scripts/Editor/Tests/DevConsoleServiceTests.cs (or dedicated test file)
  - Verification: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false

### Phase 3: Verification & Polish
- [ ] Step 3.1: Run pre-commit verification gate (compilation, zero warnings, standards audit).
- [ ] Step 3.2: Perform Unity Editor playmode verification:
  - Open console (`~`).
  - Verify command appears in `help`.
  - Execute valid command and observe domain effect and log output.
  - Execute invalid arguments and verify helpful error messages.
  - Close console and verify game resumes properly.

---

## 8. Verification & Acceptance Criteria

- [ ] Project compiles cleanly with zero warnings: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false.
- [ ] Command appears in help list with correct syntax and description.
- [ ] Valid invocation triggers intended gameplay behavior with positive feedback in console.
- [ ] Invalid syntax logs clear error message without throwing unhandled exceptions.
- [ ] Command is unregistered cleanly on scene unload.
- [ ] Release build stripping verified: zero overhead and inactive in non-development builds.
- [ ] Coding standards strictly followed ([Inject] field order, _camelCase, UPPER_SNAKE_CASE constants).
