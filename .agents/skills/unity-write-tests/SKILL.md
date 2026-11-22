---
name: unity-write-tests
description: "Use when: designing, writing, extending, or repairing thorough EditMode or PlayMode automated tests in Car Survivors. Orchestrates unity-testability, code-testing, writing-unit-tests, unity-testing, test-anti-patterns, and verification-before-completion under Car Survivors NUnit, Reflex DI, and coding standards. Triggers: write tests, add unit tests, test suite, EditMode tests, PlayMode tests, improve test coverage, testability review."
---

# Unity Write Tests (Meta-Skill)

Orchestrate thorough, deterministic EditMode and PlayMode automated tests in Car Survivors by chaining the repository's installed testing skills under Car Survivors architecture, Reflex DI, and coding-standard guardrails.

## Required Sources

Before designing or writing tests, load context from:

- AGENTS.md
- .agents/README.md
- .agents/context/project-coding-standards.md
- .agents/context/ai-game-dev-best-practices.md
- .agents/context/technology-documentation.md
- .github/workflows/ci.yml
- Assets/Scripts/ReflexDI/
- Assets/Scripts/Editor/Tests/
- Relevant system documentation under .agents/context/game-systems/
- Chained companion skills:
  - .agents/skills/unity-testability/SKILL.md
  - .agents/skills/code-testing/SKILL.md
  - .agents/skills/code-testing/unit-test-generation.prompt.md
  - .agents/skills/writing-unit-tests/SKILL.md
  - .agents/skills/unity-testing/SKILL.md
  - .agents/skills/unity-testing/references/test-framework.md
  - .agents/skills/test-anti-patterns/SKILL.md
  - .agents/skills/verification-before-completion/SKILL.md
- Local Car Survivors workflow skills:
  - .agents/skills/di-integration/SKILL.md
  - .agents/skills/unity-cli/SKILL.md
  - .agents/skills/unity-pre-commit-gate/SKILL.md
- Local test harness quick-reference:
  - .agents/skills/unity-write-tests/references/car-survivors-test-harness.md

## Precedence & Inline Execution Rules

1. **Repository Rules Win:** AGENTS.md, .agents/context/project-coding-standards.md, ADR-004, ADR-005, and the project overrides in this file supersede conflicting generic advice in imported skills.
2. **Inline Pipeline Ownership:** Execute the entire pipeline inside the current conversation or delegated Car Survivors subagents. Do NOT search for external custom agents or absent reference bundles named inside imported skills (`test-engineer`, `code-testing-extensions`, `test-analysis-extensions`, `principal-engineering`, `testing-changes`, `handling-failures`, `test-gap-analysis`, `assertion-quality`).
3. **No Silent Runtime or Asset Edits:** Never mutate runtime gameplay behavior, `.prefab`, `.unity`, `.asset`, or `.asmdef` files as a side effect of writing tests unless the user explicitly approves that scope in the plan gate.

## Gate 0: Authority, Lifecycle & Scope Check

1. Check .agents/context/implementations/plans/ to see whether an approved plan or specification already authorizes the test suite (for example *-tests-spec.md or *-plan.md).
2. For non-trivial new test suites or any change that introduces a runtime testability seam, follow the AGENTS.md Implementation Lifecycle:
   - Draft or update the plan/spec under .agents/context/implementations/plans/ before editing C# files.
   - Surface consequential Open Questions (testing existing behavior as-is vs fixing discovered runtime defects, adding a minimal behavior-preserving seam, or creating a PlayMode assembly) and wait for user approval before writing code.
   - Upon completion, create the summary under .agents/context/implementations/summaries/.
3. Inspect the target production types, their Reflex DI bindings under Assets/Scripts/ReflexDI/, and representative existing tests under Assets/Scripts/Editor/Tests/ (such as UpgradeableStatTests.cs, SkillUpgradeFlowTests.cs, and ScoreBoardDualSortTests.cs).

## Chained Workflow

### Step 1: Testability & Seam Assessment (Conditional — unity-testability + di-integration)

Run when the target system embeds pure domain rules inside `MonoBehaviour` callbacks, `Update()`, static state, or scene lookups:

1. Apply .agents/skills/unity-testability/SKILL.md review questions alongside .agents/skills/di-integration/SKILL.md.
2. Separate candidate pure-C# rules (fast EditMode tests) from Unity-facing lifecycle/physics/DOTween behavior.
3. If an existing `MonoBehaviour` lacks a deterministic EditMode step seam (for example `Time.deltaTime` accumulation inside `Update()`), propose a minimal, behavior-preserving seam in the plan (such as extracting a pure collaborator or an explicit step method invoked by `Update()`) rather than writing timing-fragile sleeps.

### Step 2: Requirement Coverage Matrix (code-testing)

Follow .agents/skills/code-testing/SKILL.md and .agents/skills/code-testing/unit-test-generation.prompt.md inline:

1. Classify scope (`Focused` vs `Broad`). Treat completeness as a requirement-coverage matrix, never as a raw test-count target.
2. Enumerate every observable contract:
   - Happy paths and state transitions.
   - Exact boundaries (`<`, `==`, `>` around caps, thresholds, and capacities) plus immediately adjacent values.
   - Invalid and degenerate inputs (`<= 0`, `float.NaN`, `float.PositiveInfinity`, `null`, empty collections, already-dead or uninitialized states).
   - Pooling and lifecycle reuse (`OnDisable` -> `OnEnable` resets, event unsubscription without duplicate handler invocations).
   - Multi-axis property intersections and event firing order invariants.
3. Apply mutation thinking from unit-test-generation.prompt.md: ensure every assertion would fail under plausible defects (`>` vs `>=`, missing clamp, inverted condition, skipped cleanup).
4. **Car Survivors Override:**
   - Ignore `.csproj` `<Compile Include>`, `.sln`, or `dotnet test` instructions in code-testing — Unity automatically generates `CarSurvivors.Editor.Tests.csproj` (Assets/Scripts/Editor/Tests/) and `CarSurvivors.PlayMode.Tests.csproj` (Assets/Tests/PlayMode/).
   - Ignore `<TESTAGENT_DIR>/research.md`, `plan.md`, and `status.md` state-file instructions (and `.git/testagent` paths) in code-testing — use the repository lifecycle under .agents/context/implementations/plans/ and .agents/context/implementations/summaries/ (and .agents/context/tmp/ only when temporary scratch files are needed).

### Step 3: Test Implementation (writing-unit-tests + unity-testing + Car Survivors Standards)

Combine test-craft rules from .agents/skills/writing-unit-tests/SKILL.md with Unity Test Framework guidance from .agents/skills/unity-testing/SKILL.md and .agents/skills/unity-testing/references/test-framework.md (and boilerplate templates in .agents/skills/unity-write-tests/references/car-survivors-test-harness.md) under these mandatory Car Survivors overrides:

1. **Test Location & Assemblies:**
   - Place EditMode unit and integration tests in Assets/Scripts/Editor/Tests/ (`CarSurvivors.Editor.Tests.asmdef`).
   - Place PlayMode tests in Assets/Tests/PlayMode/ (`CarSurvivors.PlayMode.Tests.asmdef`).
   - Do NOT create additional `.asmdef` files unless the user explicitly approves.
   - Keep Unity `.meta` pairing consistent for all created C# files (verify via .agents/scripts/verify-change.ps1 or Unity Editor refresh).
2. **Framework Baseline (UTF 1.6) & Test Doubles (No Moq / NSubstitute / FluentAssertions):**
   - Target Unity Test Framework `com.unity.test-framework` `1.6.0` (NUnit 3.5): use `NUnit.Framework` (`[Test]`, `[TestCase]`, `[TestCaseSource]`, `[SetUp]`, `[TearDown]`, `Assert.*`, `LogAssert`). Ignore UTF 2.0-only attributes referenced in unity-testing (`[PreservedValues]`, `[RequirePlatformSupport]`).
   - Do NOT reference `Moq`, `NSubstitute`, or `FluentAssertions` (none are installed in Assets/packages.config).
   - Use lightweight private hand-written fake classes implementing project interfaces, `ScriptableObject.CreateInstance<T>()`, and scoped Reflex `ContainerBuilder` + `GameObjectInjector.InjectRecursive(...)` harnesses for `[Inject]` components.
3. **Contract-First & Independent Oracles (from writing-unit-tests):**
   - Test through public contracts and DI interfaces, not private call sequences.
   - Compute expected values independently from design rules or arithmetic literals — never copy production formulas or call production calculation helpers to derive expected values.
   - Enforce strict Arrange-Act-Assert separation with one behavioral claim per test and no branching (`if`/`switch`) or loops inside test bodies (move fixture generation into private helper methods).
   - **Naming Override:** Use Car Survivors PascalCase NUnit method names (`SubjectOrMethod_Scenario_ExpectedOutcome`, matching Assets/Scripts/Editor/Tests/), overriding snake_case examples in writing-unit-tests.
4. **Determinism & Unity Lifecycle Cleanup (from unity-testing & .github/workflows/ci.yml):**
   - Prefer synchronous `[Test]` over `[UnityTest]` unless coroutine/frame yields are strictly required.
   - Track every instantiated `GameObject`, component, `Texture2D`, `Sprite`, `AudioClip`, and `ScriptableObject` and destroy them in `[TearDown]` via `UnityEngine.Object.DestroyImmediate(...)`; dispose every temporary Reflex `Container` in `[TearDown]`.
   - When testing components that spawn DOTween animations or shakes (such as `HealthBar` or `ArmorPlatesView`), call `DG.Tweening.DOTween.KillAll()` and `DG.Tweening.DOTween.Clear(true)` in `[TearDown]` so no hidden `[DOTween]` manager GameObject leaks in the Editor scene.
   - Never rely on wall-clock sleeps, unseeded randomness, or audio hardware (CI executes EditMode and PlayMode runners with `-disable-audio`).
   - Load real assets read-only via `AssetDatabase.LoadAssetAtPath` when validating authored prefab/asset integrity; never modify `.prefab`, `.asset`, or `.unity` files on disk.
5. **Project Coding Standards Override (.agents/context/project-coding-standards.md):**
   - Strictly NO `using System.Linq;` or LINQ extension calls in test files or helpers.
   - Always use block-bodied methods with `{}` and explicit `return` statements (override `=>` method examples from unity-testing).
   - Follow `_camelCase` private fields, `UPPER_SNAKE_CASE` constants (if any), English-only identifiers/comments, and zero compiler warnings.

### Step 4: Anti-Pattern Audit (test-anti-patterns)

Apply .agents/skills/test-anti-patterns/SKILL.md Steps 3–4 inline to every newly written or modified test file:

1. Account for every test method and class-level fixture in a private completeness check.
2. Eliminate Critical and High findings:
   - Missing assertions, coverage-touching facades, self-referential assertions, swallowed exceptions, or `try/catch` instead of `Assert.Throws<T>`.
   - Weak transformation oracles (`actual != oldValue` instead of exact expected values).
   - Shared mutable static/fixture state creating execution-order coupling.
   - Unseeded randomness, wall-clock calls, or leaked `UnityEngine.Object` / Reflex containers missing `[TearDown]` cleanup.

### Step 5: Compile, Execute & Evidence Gate (unity-cli + unity-pre-commit-gate + verification-before-completion)

Follow .agents/skills/verification-before-completion/SKILL.md alongside .agents/skills/unity-cli/SKILL.md and .agents/skills/unity-pre-commit-gate/SKILL.md:

1. **Compile affected assemblies with zero errors and zero warnings:**
   ```powershell
   dotnet build CarSurvivors.Runtime.csproj -p:BuildProjectReferences=false
   dotnet build CarSurvivors.Editor.csproj -p:BuildProjectReferences=false
   dotnet build CarSurvivors.Editor.Tests.csproj -p:BuildProjectReferences=false
   dotnet build CarSurvivors.PlayMode.Tests.csproj -p:BuildProjectReferences=false
   ```
2. **Run architecture and coding-standards linter on touched C# files:**
   ```powershell
   powershell -ExecutionPolicy Bypass -File .agents/scripts/check-architecture.ps1 -Files "<comma-separated-cs-files>" -StrictWarnings
   ```
3. **Run live Unity Test Runner when Editor is connected:**
   - Check connection via `unity status --format json` and execute tests via `unity command test run --json` (or `unity test` writing reports strictly under .agents/context/tmp/unity-cli/, never at repo root).
   - Inspect exit code, total tests discovered, and failed/skipped counts directly from the fresh output.
4. **Honest Evidence Reporting:**
   - Never claim tests pass without fresh passing runner output after the final edit.
   - If Unity Editor is unavailable or disconnected, explicitly report compilation and linter results as `PASS` (if clean) and live Unity test execution as `PENDING`.
   - Include a `Requirement | Evidence` table (for multi-requirement suites) or **Requirement coverage** bullet list (for 1–3 focused requirements) mapping each behavior to exact test method names.
