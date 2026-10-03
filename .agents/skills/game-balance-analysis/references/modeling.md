# Balance Modeling

Read for forecasts, comparison, or calibration. The model approximates verified runtime behavior; it does not replace Unity physics.

## State and Clock

Use active gameplay seconds for the timeline. Verify pause/time-scale behavior and keep wall-clock time separate. Represent waves, swarm warnings/spawn, boss engagement/phases, pickups, upgrades, and death as events or explicit states. Engagement can depend on objectives and movement; expose route assumptions.

Discrete events suit sparse scheduling; small timesteps suit combat pressure/collection. Either approach must preserve relevant ordering. Repeat with a smaller timestep if milestone conclusions depend on discretization.

Maintain the requested subset of live enemies/remaining HP, requested/actual spawns, kills/releases, generated/pending/collected EXP, levels, applied upgrades, effective power, player HP/death/completion, timers/freezes, engaged boss/phase, and objective progress.

## Population and Combat

Account for population as existing enemies plus actual spawns minus kills/verified despawns. Engagement availability differs from total population when offscreen enemies take time to arrive.

HP / effective DPS approximates single-target TTK only for approximately continuous damage against an attackable target. Discrete hits, overkill, reloads, uptime, and phases can invalidate it. Allocate damage to available targets; kills cannot exceed population, and HP damage cannot exceed capacity without a verified multiplier such as multi-target AoE.

AoE, piercing, mines, saw contact, and crowd control need density-dependent coverage and uptime. Keep per-target damage separate from aggregate output. Approximate movement/range/car handling through declared parameters until spatial measurements exist.

Incoming damage requires attack opportunities, hit severity/cadence, and exposure/avoidance. Multiplying enemy damage by the entire map population is not a player-intake estimate. Include verified damage cooldowns, immunity, regeneration delays, and simultaneous-hit semantics where relevant. Any compound pressure index must expose components/weights and supplement actual population/HP/damage metrics.

## EXP and Upgrades

Track kill -> actual reward/particles -> collection delay -> thresholds/carryover -> offered reward -> applied upgrade. Generated and collected EXP are separate series. Preserve runtime multiplication, truncation, caps, and delays in the baseline; suspected defects are separate findings.

Read required EXP at the actual evaluated level inputs. Check finite thresholds and suspicious non-positive values; do not silently replace authored progression with a smooth curve.

Reward availability differs from applied power. Include configured offer intervals, unlock pool, choice policy, and timing. A predetermined build is a scenario, not typical-player evidence. A fixed DPS multiplier per level needs supporting evidence.

## Waves, Swarms, and Bosses

Preserve wave-size integer conversions/bounds. Apply composition redistribution at its actual trigger, often per wave rather than per second. Include immediate clear behavior and release/VFX timing when the scheduling counter tracks pooled entities.

Distinguish freezing timers, suppressing new events, canceling ongoing events, and deleting enemies. Preserve warnings, tick batching, type progression, placement success, activation delays, and timer restart timing.

Bosses require objective gates, route/engagement policy, attack action plus recovery/cooldown, phase thresholds, damage opportunities, and rewards. Cooldown alone may not equal a complete attack cycle. Include leash/reset behavior where relevant and separate background enemies from boss damage.

## Scenarios and Uncertainty

Beginner/typical/skilled profiles are analyst-defined until calibrated. Record numerical assumptions/ranges for damage uptime, hits, avoidance/contact, pickups, route, build choices, and encounter resets. Avoid one accuracy scalar controlling unrelated mechanics.

Sample randomness and uncertain inputs with preserved correlations, such as density affecting AoE and incoming contact. Record sample count, seed mapping/RNG, versions, and sampled inputs. A shared seed is not paired evidence when changed control flow consumes random draws differently; pre-sample comparable streams or state the limitation.

Show median/P10/P90 and sample counts. These are conditional model outcome quantiles, not empirical confidence bounds. Chosen extreme scenarios form an envelope, not a sampled percentile band.

Use fixed cohorts for survival and report denominators. Do not silently drop dead players from later curves. Retained final-state level curves need a declared policy; post-death DPS/kills stop. Unfinished boss fights are censored outcomes, not zero-duration fights. Report failures alongside successful-fight durations.

## Calibration and Checks

Fit parameters only where measurements constrain them; bounded SciPy least squares can help. Avoid fitting many parameters to one final level. Separate fitting/validation runs where possible and match build/configuration/clock.

Before delivery, check observable invariants:

- Finite values, nonnegative populations, HP bounds, probability validity, and kills bounded by availability.
- Population/damage accounting, emitted/collected EXP, carryover, and verified caps.
- No actor activity after death except explicitly retained final-state metrics.
- Frozen timers, phase transitions, and rewards follow current semantics/order.
- Boundaries: zero DPS, no targets, zero collection, exact EXP thresholds, multiple levels, minimal wave sizes, failed placement, interrupted encounters.
- Candidate effects are explainable; investigate unexpected non-monotonic behavior rather than forcing intuitive results.

Express error in useful units: milestone seconds, swarm-peak enemies, collected EXP, boss seconds, and survival percentage points. Separate exact configuration-derived outputs, conditional predictions, and measurements.
