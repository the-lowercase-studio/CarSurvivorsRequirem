#!/usr/bin/env python3
"""Render validated balance-analysis samples; gameplay simulation is external."""

import argparse
import hashlib
import html
import importlib.metadata
import importlib.util
import json
import math
import os
import re
import shutil
import sys
from pathlib import Path


HELPER_VERSION = "1.0"
PACKAGES = ("numpy", "pandas", "plotly", "matplotlib")
KEY_COLUMNS = ("variant", "profile", "sample_id", "time_s", "metric")
GROUP_COLUMNS = ("variant", "profile", "metric", "time_s")
KINDS = {"configured", "modelled", "measured", "target"}


def nonempty_string(value, field):
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field} must be a nonempty string.")


def load_inputs(series_path, manifest_path):
    import numpy as np
    import pandas as pd

    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    if not isinstance(manifest, dict):
        raise ValueError("Manifest must be an object.")
    for field in ("run_id", "title", "model_version", "source_revision"):
        nonempty_string(manifest.get(field), field)
    if manifest.get("clock") != "active_gameplay_seconds":
        raise ValueError("clock must be active_gameplay_seconds.")
    assumptions = manifest.get("assumptions")
    if not isinstance(assumptions, list):
        raise ValueError("assumptions must be a list of strings.")
    for assumption in assumptions:
        nonempty_string(assumption, "assumption")
    metrics = manifest.get("metrics")
    if not isinstance(metrics, dict) or not metrics:
        raise ValueError("metrics must be a nonempty mapping.")
    for metric, info in metrics.items():
        if not re.fullmatch(r"[a-z][a-z0-9_]*", metric):
            raise ValueError(f"Unsafe metric identifier: {metric!r}.")
        if not isinstance(info, dict):
            raise ValueError(f"Metric {metric} must be an object.")
        for field in ("label", "unit"):
            nonempty_string(info.get(field), f"{metric}.{field}")
        if info.get("kind") not in KINDS:
            raise ValueError(f"Invalid evidence kind for {metric}.")
        if info.get("style", "line") not in {"line", "step"}:
            raise ValueError(f"Invalid chart style for {metric}.")

    frame = pd.read_csv(
        series_path,
        encoding="utf-8-sig",
        keep_default_na=False,
        dtype={column: str for column in ("variant", "profile", "sample_id", "metric")},
    )
    missing = set(KEY_COLUMNS + ("value",)) - set(frame.columns)
    if missing or frame.empty:
        raise ValueError(f"Series must be nonempty and contain all columns; missing: {sorted(missing)}.")
    for column in ("variant", "profile", "sample_id", "metric"):
        if frame[column].str.strip().eq("").any():
            raise ValueError(f"Blank identifier in {column}.")
    for column in ("time_s", "value"):
        frame[column] = pd.to_numeric(frame[column], errors="raise")
        if not np.isfinite(frame[column].to_numpy(dtype=float)).all():
            raise ValueError(f"Nonfinite number in {column}.")
    if frame["time_s"].lt(0).any():
        raise ValueError("time_s must be nonnegative.")
    if frame.duplicated(list(KEY_COLUMNS)).any():
        raise ValueError("Duplicate variant/profile/sample/time/metric observations.")
    unknown = set(frame["metric"]) - set(metrics)
    if unknown:
        raise ValueError(f"Undeclared metrics: {sorted(unknown)}.")

    for key, group in frame.groupby(["variant", "profile", "metric"], sort=False):
        cohort = None
        for _, instant in group.groupby("time_s", sort=True):
            samples = set(instant["sample_id"])
            if cohort is None:
                cohort = samples
            elif samples != cohort:
                raise ValueError(f"Changing sample cohort for {key}; declare death/completion handling upstream.")

    events = manifest.get("events", [])
    if not isinstance(events, list):
        raise ValueError("events must be a list.")
    for event in events:
        if not isinstance(event, dict):
            raise ValueError("Each event must be an object.")
        nonempty_string(event.get("label"), "event.label")
        time = event.get("time_s")
        if isinstance(time, bool) or not isinstance(time, (int, float)) or not math.isfinite(time) or time < 0:
            raise ValueError("Event time_s must be finite and nonnegative.")
        if "variant" in event and event["variant"] not in set(frame["variant"]):
            raise ValueError("Event variant is absent from the series.")

    grouped = frame.groupby(list(GROUP_COLUMNS), sort=True)["value"]
    summary = grouped.quantile([0.1, 0.5, 0.9]).unstack()
    summary.columns = ["p10", "p50", "p90"]
    summary["sample_count"] = grouped.count()
    return frame, manifest, summary.reset_index()


def render(frame, manifest, summary, output):
    os.environ["MPLCONFIGDIR"] = str(output / "_cache")
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    import plotly.graph_objects as go
    import plotly.io as pio
    from plotly.colors import qualitative

    chart_directory = output / "charts"
    chart_directory.mkdir()
    pairs = sorted(set(zip(frame["variant"], frame["profile"])))
    colors = {pair: qualitative.Plotly[index % len(qualitative.Plotly)] for index, pair in enumerate(pairs)}
    max_minutes = max(float(frame["time_s"].max()) / 60, 1 / 60)
    title = html.escape(manifest["title"])
    assumptions = "".join(f"<li>{html.escape(item)}</li>" for item in manifest["assumptions"])
    sections = [
        "<!doctype html><html lang='en'><head><meta charset='utf-8'>",
        f"<title>{title}</title>",
        "<style>body{font:16px system-ui;margin:24px auto;max-width:1200px;padding:0 16px}",
        "pre{white-space:pre-wrap;overflow-wrap:anywhere}section{margin:32px 0}</style></head><body>",
        f"<h1>{title}</h1><p>Run: {html.escape(manifest['run_id'])}</p>",
        "<p>P10-P90 describes sample variation under the declared model or measurement cohort. ",
        "Modelled bands do not establish forecast accuracy. Single samples have no band.</p>",
        f"<h2>Assumptions and cohort policy</h2><ul>{assumptions}</ul>",
    ]
    for metric_index, metric in enumerate(sorted(set(frame["metric"]))):
        info = manifest["metrics"][metric]
        label = info["label"]
        heading = f"{label} [{info['kind']}]"
        axis_label = f"{label} ({info['unit']})"
        step = info.get("style", "line") == "step"
        interactive = go.Figure()
        static, axis = plt.subplots(figsize=(11, 4.8))
        selected = summary[summary["metric"].eq(metric)]
        for (variant, profile), group in selected.groupby(["variant", "profile"], sort=True):
            group = group.sort_values("time_s")
            x = group["time_s"].to_numpy(dtype=float) / 60
            low = group["p10"].to_numpy(dtype=float)
            median = group["p50"].to_numpy(dtype=float)
            high = group["p90"].to_numpy(dtype=float)
            sample_count = int(group["sample_count"].iloc[0])
            color = colors[(variant, profile)]
            red, green, blue = (int(color[index:index + 2], 16) for index in (1, 3, 5))
            legend = f"{variant} / {profile} (n={sample_count})"
            shape = "hv" if step else "linear"
            if sample_count > 1:
                interactive.add_trace(go.Scatter(
                    x=x, y=low, mode="lines", line={"width": 0, "shape": shape},
                    legendgroup=legend, showlegend=False, hoverinfo="skip",
                ))
                interactive.add_trace(go.Scatter(
                    x=x, y=high, mode="lines", line={"width": 0, "shape": shape},
                    fill="tonexty", fillcolor=f"rgba({red},{green},{blue},0.15)",
                    legendgroup=legend, showlegend=False, hoverinfo="skip",
                ))
                axis.fill_between(x, low, high, color=color, alpha=0.15, step="post" if step else None)
            interactive.add_trace(go.Scatter(
                x=x, y=median, name=html.escape(legend), legendgroup=legend,
                mode="lines", line={"color": color, "shape": shape},
                customdata=group[["p10", "p90", "sample_count"]].to_numpy(),
                hovertemplate="Time: %{x:.2f} min<br>Median: %{y:.3g}<br>P10: %{customdata[0]:.3g}"
                              "<br>P90: %{customdata[1]:.3g}<br>Samples: %{customdata[2]}<extra>%{fullData.name}</extra>",
            ))
            axis.plot(x, median, color=color, label=legend, drawstyle="steps-post" if step else "default")

        for event in manifest.get("events", []):
            event_label = event["label"]
            if "variant" in event:
                event_label = f"{event['variant']}: {event_label}"
            time = event["time_s"] / 60
            interactive.add_vline(x=time, line_dash="dot", line_color="#777777",
                                  annotation_text=html.escape(event_label))
            axis.axvline(time, color="#777777", linestyle=":", alpha=0.6, label=event_label)
        interactive.update_layout(
            title=html.escape(heading), xaxis_title="Active gameplay time (min)",
            yaxis_title=html.escape(axis_label), template="plotly_white", height=480,
            xaxis={"range": [0, max_minutes]}, legend={"groupclick": "togglegroup"},
        )
        axis.set(title=heading, xlabel="Active gameplay time (min)", ylabel=axis_label, xlim=(0, max_minutes))
        axis.grid(alpha=0.2)
        axis.legend(fontsize=8)
        static.tight_layout()
        static.savefig(chart_directory / f"{metric}.png", dpi=160)
        static.savefig(chart_directory / f"{metric}.svg")
        plt.close(static)
        sections.append("<section>")
        sections.append(pio.to_html(interactive, full_html=False, include_plotlyjs=metric_index == 0))
        sections.append("</section>")
    sections.append("<h2>Reproduction manifest</h2><pre>")
    sections.append(html.escape(json.dumps(manifest, indent=2, ensure_ascii=False)))
    sections.append("</pre></body></html>")
    (output / "report.html").write_text("\n".join(sections), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--series", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        missing = [package for package in PACKAGES if importlib.util.find_spec(package) is None]
        if missing:
            raise ValueError(f"Missing dependencies: {', '.join(missing)}. Install scripts/requirements.txt in an isolated environment.")
        frame, manifest, summary = load_inputs(args.series, args.manifest)
        manifest["renderer_environment"] = {
            "helper_version": HELPER_VERSION,
            "helper_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            "series_sha256": hashlib.sha256(args.series.read_bytes()).hexdigest(),
            "input_manifest_sha256": hashlib.sha256(args.manifest.read_bytes()).hexdigest(),
            "python": sys.version.split()[0],
            "packages": {package: importlib.metadata.version(package) for package in PACKAGES},
        }
        output = args.output.resolve()
        output.mkdir(parents=True, exist_ok=False)
        render(frame, manifest, summary, output)
        shutil.copyfile(args.series, output / "series.csv")
        summary.to_csv(output / "summary.csv", index=False)
        (output / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print(f"Report created: {output / 'report.html'}")
    except (ValueError, TypeError, KeyError, OSError, ImportError) as error:
        parser.exit(2, f"Error: {error}\n")


if __name__ == "__main__":
    main()
