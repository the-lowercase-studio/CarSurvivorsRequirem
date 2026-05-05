# Health System Documentation

## Purpose

The Health system owns mutable health state, alive/dead state, health change events, optional player regeneration, and slider-based health presentation.

It does not own damage calculation, target selection, enemy/player death presentation, damage numbers, experience payout, VFX, audio, or pooling. Those systems consume `IHealth`, `IHealthy`, or `IDamageable` contracts and react to health events.

## Reading Map

- Primary code locations:
  - `Assets/Scripts/HealthSystem/Health.cs`
  - `Assets/Scripts/HealthSystem/RegenativeHealth.cs`
  - `Assets/Scripts/HealthSystem/HealthBar.cs`
- Main integration points:
  - `Assets/Scripts/Enemies/Enemy.cs`
  - `Assets/Scripts/Enemies/EnemyDeathHandler.cs`
  - `Assets/Scripts/Player/PlayerManager.cs`
  - `Assets/Scripts/Player/PlayerDamagedHandler.cs`
  - `Assets/Scripts/Player/PlayerDeathHandler.cs`
  - `Assets/Scripts/StatusAffectables/IDamageable.cs`
- Related systems:
  - Damage numbers: `Assets/Scripts/DamageNumbers/`
  - VFX: `Assets/Scripts/VFX/`
  - Experience payout: `Assets/Scripts/LevelSystem/Exp/`
  - Player death UI: `Assets/Scripts/UI/Death/`
- Related docs:
  - `.agents/docs/project-coding-standards.md`
  - `.agents/docs/ai-game-dev-best-practices.md`
  - `.agents/docs/enemies-system.md`
  - `.agents/docs/damage-numbers-system.md`
- Related agents or instructions:
  - `.agents/skills/document-system/SKILL.md`
  - `.agents/skills/architecture-review/SKILL.md`
  - `.agents/skills/check-optimalization/SKILL.md`

## Architecture and Data Flow

- Core components:
  - `Health` is the base `MonoBehaviour` implementation of `IHealth`. It stores `MaxHealth`, `CurrentHealth`, alive state, and the events used by gameplay and UI.
  - `RegenativeHealth` inherits `Health` and adds timed automatic healing through `Update` when current health is below max health.
  - `HealthBar` requires a Unity `Slider`, reads a serialized `Health` reference, updates the slider and fill color on health changes, and can shake on health decrease through DOTween.
  - `IHealthy` is a narrow provider interface for objects that expose an `IHealth` reference.
  - `IDamageable` is the status-affectable contract used by attackers and helpers. Its implementations decide how incoming damage maps to health, VFX, audio, and presentation.
- Key interfaces:
  - `IHealth` exposes `CurrentHealth`, mutable `MaxHealth`, health change events, `DecreaseHealth`, `IncreaseHealth`, and `IsAlive`.
  - `IRegenativeHealth` extends `IHealth` with regeneration amount and delay state.
  - `IHealthy` exposes an `IHealth Health` property and is implemented by `Enemy`, `PlayerManager`, and `IPlayerManager`.
  - `IDamageable` exposes `TakeDamage` and `TakeFullHpDamage`; current player and enemy implementations reduce health through their owned `IHealth`.
- Runtime flow:
  - `Health.OnEnable` subscribes internal handlers so decreased, increased, and no-health events raise the aggregate `OnHealthChange` event. It also marks the instance alive and resets `CurrentHealth` to `MaxHealth`.
  - `Health.DecreaseHealth` exits when already dead. If remaining health is greater than incoming damage, it subtracts the value and raises `OnHealthDecreased`; otherwise it sets health to zero, marks dead, and raises `OnNoHealth`.
  - `Health.IncreaseHealth` exits when dead. If the increase stays below max health, it adds health and raises `OnHealthIncreased`; otherwise it clamps to `MaxHealth` without raising a health event.
  - `RegenativeHealth.OnEnable` resets regeneration amount and delay from serialized values after base health reset.
  - `RegenativeHealth.Update` runs regeneration while current health is below max health and both regeneration amount and delay are positive. Each completed delay calls `IncreaseHealth(MaxRegenerationAmount)` and resets the delay.
  - `Enemy.OnGet` sets `Health.MaxHealth` from `EnemyConfigSO.MaxHealth`. Because pooled enemies are activated after this setup, `Health.OnEnable` then resets `CurrentHealth` to the configured max health.
  - `Enemy.TakeDamage` spawns a damage number, decreases health, and only plays blood VFX when `Health.IsAlive()` remains true.
  - `EnemyDeathHandler` listens to `Health.OnNoHealth`, disables physical interaction and visuals, plays death feedback, spawns experience, and completes the pool-release sequence after VFX and audio finish.
  - `PlayerManager.Awake` caches `IHealth` from the player object. Player damage and death handlers use the injected `IPlayerManager.Health` reference.
  - `PlayerDamagedHandler.TakeDamage` decreases player health, plays damage audio/VFX, and shakes the car visual.
  - `PlayerDeathHandler` listens to `Health.OnNoHealth`, hides the player visual, disables non-wheel colliders, plays death VFX, then enables the death screen after the VFX finishes.

## Rules and Invariants

- Critical behavior rules:
  - `Health.OnEnable` is the reset point for current health and alive state. Pooled or re-enabled objects come back alive at `MaxHealth`.
  - `OnNoHealth` is raised once per alive lifetime because `_isAlive` is set false before the event is invoked.
  - `OnHealthChange` is derived from `OnHealthDecreased`, `OnHealthIncreased`, and `OnNoHealth`; consumers should subscribe to the narrow event when they need a specific semantic.
  - Dead health cannot be increased through `IncreaseHealth`; resurrection is not supported by the current contract.
  - Enemy health max is configured from `EnemyConfigSO.MaxHealth` on pool get. Player health and regeneration values are configured on the player `RegenativeHealth` component.
  - `HealthBar` depends on a concrete serialized `Health` reference, not `IHealth`, because Unity serializes the component reference.
- Ordering or sequencing guarantees:
  - For enemy damage, damage numbers are spawned before health is reduced, and blood VFX plays only if the enemy survives the hit.
  - Death handlers are event subscribers to `OnNoHealth`; they do not poll health.
  - `OnNoHealth` also raises `OnHealthChange` through the internal subscription created in `Health.OnEnable`.
  - `Health.OnDisable` removes internal event forwarding subscriptions to avoid duplicate forwarding across re-enable cycles.
  - Regeneration starts after `StartRegenerationDelay`, then repeats after each delay while health remains below max.
- Constraints contributors must preserve:
  - Preserve `OnNoHealth` as the authoritative death trigger for enemies and players unless the death flow is intentionally redesigned.
  - Do not bypass health events by mutating `CurrentHealth` directly; it has a protected setter for this reason.
  - Treat changes to clamping, event firing, death state, regeneration timing, or max-health reset as player-facing gameplay changes.
  - Preserve serialized fields on health and health bar components so existing prefabs and scene references remain intact.
  - Keep damage application through explicit contracts such as `IDamageable`, `IHealthy`, and `IHealth`; do not add singleton access or broad scene searches.

## Extension Points

- Safe extension areas:
  - Add new health consumers by depending on `IHealthy` or `IHealth` and subscribing to lifecycle-matched events.
  - Add new damage producers through `IDamageable.TakeDamage` rather than calling concrete player or enemy implementations directly.
  - Add local UI reactions by subscribing to `OnHealthChange`, `OnHealthDecreased`, or `OnHealthIncreased`.
  - Add specialized health behavior by deriving from `Health` only when the existing reset and event semantics remain valid.
- Required dependencies and contracts:
  - Objects exposing `IHealthy` must have a component that implements `IHealth` available when their aggregate `Awake` runs.
  - Player setup currently requires `RegenativeHealth` through `PlayerManager`.
  - Enemy prefabs are expected to provide an `IHealth` component so `Enemy.Awake` can cache it.
  - `HealthBar` needs a valid `Slider`, serialized `Health`, fill `Image`, and gradient.
  - DOTween is required for health bar shake behavior.
- Testing implications:
  - Compile after C# edits with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - For health logic changes, test damage smaller than current health, damage equal to current health, overkill damage, dead-state repeated damage, healing below max, healing to max, and healing while dead.
  - For regeneration changes, test delay countdown, repeated regeneration, max-health clamping, zero regeneration amount, and zero delay.
  - In Unity, verify player death screen timing, enemy death VFX/audio completion, experience spawn, pooled enemy health reset, and health bar updates.

## Integration Notes

- Upstream dependencies:
  - Enemy configuration provides `EnemyConfigSO.MaxHealth`.
  - Enemy attacks, helper utilities, projectiles, skills, and other combat producers reach health through `IDamageable` implementations.
  - Player and enemy managers cache `IHealth` from components on their own GameObjects.
  - Unity lifecycle order matters for pooled enemies because `Enemy.OnGet` sets max health before activation resets current health.
- Downstream consumers:
  - `EnemyDeathHandler` consumes `OnNoHealth` for death sequence, experience payout, and pool release completion.
  - `PlayerDeathHandler` consumes `OnNoHealth` for visual/collider changes and death UI timing.
  - `HealthBar` consumes `OnHealthChange` and optionally `OnHealthDecreased` for UI updates.
  - Damage numbers are currently produced by `Enemy.TakeDamage`, not by `Health` itself.
- Cross-system coupling risks:
  - Changing when `OnHealthChange` fires can break UI updates and any future aggregate health observers.
  - Changing death event timing affects enemy pool release, experience payout, player death UI, and VFX/audio sequencing.
  - Moving enemy max-health assignment after activation would prevent pooled enemies from resetting to the intended configured health.
  - Replacing `HealthBar`'s concrete `Health` serialized reference with an interface would require a Unity-serializable pattern.

## Known Risks and Open Questions

- Known limitations:
  - `RegenativeHealth` is misspelled in the type and filename; renaming it would require Unity serialization and reference migration care.
  - `Health.IncreaseHealth` does not raise `OnHealthIncreased` or `OnHealthChange` when it clamps to `MaxHealth`.
  - `HealthBar.OnEnable` initializes slider max/value but does not initialize the fill color until the first health event.
  - `HealthBar` does not kill an active DOTween shake on disable.
  - `HealthBar.Health_OnHealthDecreased` has a local variable typo, `vibratio`.
  - `DecreaseHealth` and `IncreaseHealth` do not validate negative values, so callers are responsible for passing positive damage or healing.
  - Regeneration delay does not reset when damage is taken; it only resets after a regeneration tick.
- Open design questions:
  - Should healing-to-max fire health change events so UI and observers always receive a notification?
  - Should regeneration be interrupted or its delay reset by taking damage?
  - Should health support shields, invulnerability, overheal, or resurrection as explicit concepts, or should those remain outside the base health contract?
  - Should `HealthBar` depend on a component type that implements `IHealth` through an adapter-friendly serialized reference pattern?
- Suggested follow-up tasks:
  - Add focused edit-mode tests for `Health` event semantics and boundary values.
  - Review whether max-health healing should emit events before changing UI assumptions.
  - Consider a dedicated serialization-safe cleanup for the `RegenativeHealth` spelling if the project can tolerate asset migration.
