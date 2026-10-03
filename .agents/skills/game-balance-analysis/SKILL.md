---
name: game-balance-analysis
description: "Use when: auditing or rebalancing Car Survivors gameplay pacing, EXP and leveling, enemy HP/damage/spawn composition, swarms, bosses, player survivability, or build progression using Python forecasts, time-series charts, and baseline comparisons. Triggers: balance analysis, rebalance, balance simulation, difficulty curve, progression forecast, expected EXP over time. Excludes runtime performance optimization and implementing balance changes without authorization."
---

# Game Balance Analysis

Build an evidence-based picture of a run: how progression and effective player power interact with spawning, enemy pressure, swarms, and bosses. Produce reproducible Python charts and specific candidate changes linked to design targets.

## Portability and Scope

This file and its supporting resources are the authoritative workflow. It uses repository inspection, local files, and Python; no particular agent vendor, MCP server, account, or editor connection is required. The optional descriptor in .agents/skills/game-balance-analysis/agents/openai.yaml supplies Codex UI metadata only.

Analyze existing behavior and propose changes within the requested scope. New Unity exporters, telemetry instrumentation, runtime bug fixes, and applying balance values are separate implementation work. Respect authorization already given; do not ask again for an explicitly authorized change.

Keep ScriptableObjects, prefab references, and inspector authoring authoritative. Do not silently correct runtime behavior in the model or substitute field initializers for configured asset values.

## Sources and Modes

Read AGENTS.md and .agents/README.md, then relevant standards, gameplay guardrails, technology documentation, and system documentation. Verify behavior in current source before relying on documentation or an older report.

- For ownership and implementation paths, read .agents/skills/game-balance-analysis/references/project-sources.md.
- For forecasting, uncertainty, or calibration, read .agents/skills/game-balance-analysis/references/modeling.md.
- Before generating charts, read .agents/skills/game-balance-analysis/references/reporting.md.

Select the smallest useful mode:

- **Configuration audit:** exact thresholds, configured stats, schedules, and implementation discrepancies. Do not present this as a player-performance forecast.
- **Forecast and compare:** derive a coupled offline model from verified behavior and configuration; compare baseline and requested/proposed variants.
- **Calibrate:** compare forecasts with existing run measurements and refine behavioral assumptions. Adding a recorder is outside this mode.

## Workflow

### 1. Define the Decision

Identify systems, scene/configuration, horizon, player/build profiles, and design targets. Reuse confirmed inputs when still applicable. Ask a concise question if missing intent materially changes the recommendation, while continuing useful source inspection. Exploratory analysis can use a declared range of assumptions/horizons; do not invent a canonical run duration or difficulty target.

Use measurable goals such as time to a level, boss fight duration, peak population, swarm recovery time, or successful-run fraction. A full-game request still needs individually inspectable metrics rather than an opaque difficulty score.

### 2. Capture Behavior and Inputs

Trace active scene references and prefab overrides read-only. Record project-relative source path, member, value/unit, revision, and file hash. Sample curves at coordinates consumed by runtime code; do not substitute a Python spline for Unity interpolation without labeling it.

Distinguish **configured/derived** values verified against implementation, **modelled** assumptions/results, **measured** observations with build/clock/cohort information, and user-confirmed **targets**.

Report discrepancies affecting forecasts, including EXP accounting, integer rounding, requested/actual spawns, and timer semantics. Preserve current behavior in the baseline. If faithful modeling is impossible, mark affected metrics unsupported and complete unaffected analysis.

### 3. Model the Coupled Run

Connect actual spawning/population, attacks/kills, generated/collected EXP, levels, upgrade selection/application, and effective power. Include wave/swarm/boss transitions, rewards, and player death when relevant.

Use documented profiles for combat effectiveness, pickup delays, avoidance, and choices. Compare relevant builds rather than assuming universal DPS. Pair baseline/candidates with common sampled inputs where possible. Record/pin dependencies: a seed alone does not guarantee reproducibility across versions or changed random-call ordering.

Validate conservation, clock semantics, event ordering, and timestep convergence. Higher spawning must not directly become higher kills without accounting for combat capacity and available enemies.

### 4. Generate and Inspect Charts

Use Python following the reporting reference: NumPy/pandas for calculation/data, Plotly for offline interactive HTML, Matplotlib for PNG/SVG. Add SciPy when measured-data fitting warrants it.

Select charts answering the decision. Full-run coverage includes:

- Generated/collected EXP, level, and intervals between levels.
- Requested/actual spawning, active population, and enemy composition.
- Effective damage output, enemy time to kill, and unresolved combat load.
- Incoming damage, HP/regeneration, and survival/completion fractions when supported.
- Swarm warnings/spawn windows, peaks, clear/recovery time, and EXP yield.
- Boss engagement/duration, phases, hit severity, and rewards.

Use a common gameplay-time axis with units, event markers, and consistent variant colors. Show median and P10-P90 only for multiple samples; these describe model variation, not real-world accuracy. Report sample counts and death/completion handling. Inspect representative exported charts before delivery.

The helper .agents/skills/game-balance-analysis/scripts/render_report.py renders validated output. It does not extract Unity data or simulate gameplay. Write task-specific extraction/model scripts under .agents/context/tmp/ and preserve their exact versions in the final reproduction bundle when needed.

### 5. Compare and Recommend

Keep a baseline and vary a small interpretable parameter set. Report source member, baseline/proposed values, predicted target effect, cross-system effects, and evidence quality. Use sensitivity sweeps to identify influential controls. Do not select a variant from one lucky seed or optimize toward an unconfirmed target.

When telemetry exists, validate on runs not used for fitting when feasible. Check errors around levels, swarms, and bosses rather than final averages alone. Explain model limitations.

### 6. Preserve the Result

Use .agents/skills/game-balance-analysis/assets/report-template.md for the narrative, omitting irrelevant sections instead of inventing values.

- Scratch scripts, intermediate data, environments, caches, and provisional output: .agents/context/tmp/game-balance-analysis/<run-id>/.
- Final report/reproducibility bundle: .agents/context/balance-reports/<run-id>/.
- Plans/summaries for authorized game changes: existing .agents/context/implementations/ locations.

Use descriptive identifiers without date prefixes; put dates in reports/manifests. Preserve previous bundles and account for source/model changes before comparing them. Final bundles contain report.md, report.html, charts, exact inputs, series.csv, summary.csv, manifest.json, and executable model/dependency lock where applicable. Only finalized deliverables leave the temporary directory.

Close with findings, candidate changes, assumptions/unsupported areas, report location, and the smallest playtest that can confirm or falsify the recommendation. Prepare concrete reviewable proposals before asking for any approval required to implement them.
