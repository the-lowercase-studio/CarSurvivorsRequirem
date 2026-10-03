# Python Reporting and Reproducibility

Read when preparing data/charts. Python 3.10+ and local files suffice; the helper has no agent-vendor dependency.

## Libraries

- NumPy: calculations, controlled sampling, quantiles. Official documentation: https://numpy.org/doc/stable/reference/random/generator.html
- pandas: normalize and aggregate configuration, model, and measurement tables. Official documentation: https://pandas.pydata.org/docs/getting_started/intro_tutorials/index.html
- Plotly: standalone interactive HTML with embedded JS for offline viewing. Official documentation: https://plotly.com/python/interactive-html-export/
- Matplotlib: PNG/SVG and percentile bands. Official documentation: https://matplotlib.org/stable/api/_as_gen/matplotlib.pyplot.savefig.html and https://matplotlib.org/stable/api/_as_gen/matplotlib.pyplot.fill_between.html
- SciPy, optional: bounded fitting to measurements. Official documentation: https://docs.scipy.org/doc/scipy/reference/generated/scipy.optimize.least_squares.html

Inspect Python availability. Reuse an adequate environment or create one under .agents/context/tmp/. Install only necessary dependencies and record exact versions. Requirements ranges are compatibility bounds, not a reproducibility lock. Do not modify global Python environments.

## Data Contract

The renderer consumes UTF-8 CSV and JSON. It summarizes samples; it does not invent gameplay values or duration.

CSV columns:

| Column | Meaning |
| --- | --- |
| variant | Baseline/candidate identifier |
| profile | Named player/build/route profile |
| sample_id | Stable run/observation identifier; not necessarily a Unity RNG seed |
| time_s | Finite nonnegative active gameplay seconds |
| metric | Identifier declared in the manifest |
| value | Finite numeric value in the metric's unit |

Each variant/profile/sample_id/time_s/metric key is unique. Each variant/profile/metric keeps the same samples at every time. The renderer rejects disappearing cohorts to prevent silent survivor bias. Define death/completion handling in the model. Align irregular telemetry explicitly or use dedicated cohort-aware charts; never forward-fill post-death activity.

Required manifest fields:

| Field | Contract |
| --- | --- |
| run_id | Nonempty descriptive identifier |
| title | Nonempty English report title |
| clock | active_gameplay_seconds |
| model_version | Model identifier; configuration-audit for exact static analysis |
| source_revision | Revision plus dirty-state note, or explicit unavailable value |
| assumptions | English statements including profiles and cohort/death policy |
| metrics | Metric IDs mapped to label, unit, kind, optional style |
| events | Optional time_s/label entries, optionally naming variant |

Metric kind is configured, modelled, measured, or target. It describes the plotted output, not merely its inputs. Units include EXP, level, enemies, HP/s, damage/s, or probability. Style is line or step; levels usually use step.

Add provenance: date, input snapshot paths/hashes and source members, parameter deltas, numerical profiles, RNG/seeds/sampled streams, convergence checks, and calibration revision. Additional fields are retained; the helper adds its environment versions.

Use separate metric IDs or dedicated charts for mixed measured/modelled provenance. Event markers are shared illustrative annotations; stochastic encounter-time distributions need separate charts.

## Commands

From the project root, replace ANALYSIS_ID and keep provisional work under the temporary folder:

```powershell
python -m venv .agents/context/tmp/game-balance-analysis/ANALYSIS_ID/venv
.agents/context/tmp/game-balance-analysis/ANALYSIS_ID/venv/Scripts/python.exe -m pip install -r .agents/skills/game-balance-analysis/scripts/requirements.txt
.agents/context/tmp/game-balance-analysis/ANALYSIS_ID/venv/Scripts/python.exe -B .agents/skills/game-balance-analysis/scripts/render_report.py --series .agents/context/tmp/game-balance-analysis/ANALYSIS_ID/series.csv --manifest .agents/context/tmp/game-balance-analysis/ANALYSIS_ID/manifest.json --output .agents/context/tmp/game-balance-analysis/ANALYSIS_ID/rendered
```

Use bin/python on POSIX. Keep custom-script bytecode/caches under .agents/context/tmp/. The helper uses Matplotlib's headless backend and an output-local cache. Existing output directories are rejected to protect previous results.

Outputs: report.html embedding Plotly JS, charts/*.png and charts/*.svg, series.csv, summary.csv with P10/P50/P90/sample_count, and manifest.json with environment versions and SHA-256 hashes of the helper and input files. Single-sample curves have no uncertainty band. Charts use gameplay minutes, evidence kinds/units, event labels, and consistent variant/profile colors.

Inspect representative progression and pressure/boss charts, legend, and units using any available viewer. Verify HTML embeds JS rather than a CDN dependency. Add report.md from the template with findings, proposals, gaps, and playtests.

## Final Bundle and Repeated Rebalancing

Keep exploratory scripts/raw data in .agents/context/tmp/game-balance-analysis/ANALYSIS_ID/. Promote finalized deliverables to .agents/context/balance-reports/ANALYSIS_ID/:

- Narrative, interactive HTML, charts, series, summary.
- Exact extracted inputs/hashes; exclude unrelated assets and secrets.
- Manifest with model/source/environment/assumptions.
- Executable extraction/model scripts and resolved dependency lock needed to regenerate the series, where applicable.

Final manifest paths are relative to the bundle or project root. Preserve prior reports and explain source/model/profile/clock/calibration changes. A final report does not authorize changing Unity assets. A synthetic validation fixture is a tooling check, never a game forecast.
