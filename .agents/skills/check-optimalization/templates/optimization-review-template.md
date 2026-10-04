# Performance and Optimization Review

Target scope: [System / files]
Date: [YYYY-MM-DD]
Review mode: [Source inspection | Profiler-assisted]
Implementation authority: [Review only | Existing explicit authority and scope]
Overall assessment: [Source risks identified | Measured bottleneck | No inspected risk | Insufficient evidence]

## Evidence and Scope

- Files and invocation paths inspected:
  - Assets/Scripts/[Domain]/[File].cs
- Profiler measurements: [Not collected | Environment, workload, samples, capture path]
- Evidence limits and unverified paths:

## Findings and Reviewable Proposals

| Severity | File / method / line | Evidence type | Risk or measurement | Proposed change / affected scope | Expected impact and uncertainty |
| --- | --- | --- | --- | --- | --- |
| [Blocker / Major / Minor / Nit] | [Location] | [Source inference / measured] | [Finding] | [Concrete proposal] | [Qualitative expectation; measured values only with evidence] |

## Invariants and Safety

Check only after verification and cite evidence; unchecked items are requirements or pending checks.

- [ ] Reflex DI boundaries preserved.
- [ ] Gameplay determinism and event order preserved.
- [ ] Serialized identities and inspector authoring preserved.
- [ ] Required mechanical dependencies still fail fast.

## Verification and Action State

- Executed checks and results: [Command / exit code / diagnostics, or none]
- Proposed compilation after implementation: dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
- Pending profiler comparison: [Baseline and changed workload; no claimed gains before measurement]
- Implementation state: [Not requested / Authorized / Applied / Blocked]
- Remaining authority: [None needed / Specific missing approval for listed reviewable proposals]

Ask for implementation approval only when the concrete proposal lacks existing authority. Do not add a routine approval request to already authorized work or claim proposed checks were executed.
