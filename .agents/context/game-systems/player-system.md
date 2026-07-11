# Player System Documentation

## Purpose

The Player system is responsible for aggregating and exposing player state, handling player damage mechanics, and orchestrating the player death and UI transition flow.

It is not responsible for enemy behavior, wave mechanics, scoreboard database storage, or individual skill execution logic.

## Reading Map

- Primary code locations:
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Player/PlayerDamagedHandler.cs
  - Assets/Scripts/Player/PlayerDeathHandler.cs
  - Assets/Scripts/Player/Car/CarController.cs
  - Assets/Scripts/HealthSystem/RegenativeHealth.cs
  - Assets/Scripts/LevelSystem/LevelController.cs
  - Assets/Scripts/Skills/SkillsRegistry.cs
- Related docs:
  - .agents/context/game-systems/car-system.md
  - .agents/context/game-systems/health-system.md
  - .agents/context/game-systems/skills-system.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/architecture-review/SKILL.md

## Architecture and Data Flow

- Core components:
  - `PlayerManager`: The primary interface wrapper and aggregation root. It accesses and exposes Health, Level, Skill Registry, and Car Controller components attached to the player GameObject.
  - `PlayerDamagedHandler`: Handles incoming damage (`TakeDamage`), reduces health, triggers the damage VFX, plays damage SFX, and triggers a DOTween scale shake on the car's visual GameObject.
  - `PlayerDeathHandler`: Subscribes to the health component's `OnNoHealth` event to initiate player disablement, hide the car visuals, play the death VFX, and trigger the death UI presenter upon VFX completion.
- Key interfaces:
  - `IPlayerManager`: Inherits from `IHealthy` (exposing `IHealth Health`) and `IGameObjectProvider` (exposing `GameObject`). Exposes properties: `AudioClipPlayer`, `CarController`, `LevelController`, and `SkillsRegistry`.
- Runtime flow:
  - **Setup**: `PlayerManager` caches core references on `Awake` and is registered as a scene-scoped singleton of type `IPlayerManager` via the Reflex installer. `PlayerDeathHandler` caches all active colliders.
  - **Damage Processing**: When `TakeDamage` is called on the `PlayerDamagedHandler`, it decreases health on `IPlayerManager.Health`, plays the "Damaged" sound, spawns a damage VFX, and shakes the car's visual scale.
  - **Death Processing**: When health drops to 0, `OnNoHealth` fires. `PlayerDeathHandler` hides the car visual, disables all colliders except `_wheelColliders`, and plays the death VFX. Once completed, the presenter enables the game over screen, saves the score, changes the audio mode, and pauses the game time.

## Rules and Invariants

- Critical behavior rules:
  - `PlayerManager` requires both `RegenativeHealth` and `LevelController` components to be attached to the same GameObject.
  - `PlayerDamagedHandler` and `PlayerDeathHandler` both require the `PlayerManager` component to be attached to the same GameObject.
  - The main colliders must be disabled upon death to prevent further enemy collisions, while the wheels remain interactive or decoupled to avoid physics instability.
- Ordering or sequencing guarantees:
  - The game over screen is enabled only after the death VFX completes its animation and raises `OnVFXFinished`.
- Constraints contributors must preserve:
  - Do not introduce singleton accessors to the Player. Always inject `IPlayerManager` via Reflex.
  - Keep player damage routing unified through the `IDamageable` interface on `PlayerDamagedHandler`.

## Extension Points

- Safe extension areas:
  - Expose additional player stats by expanding `IPlayerManager` and delegates in `PlayerManager`.
  - Add visual cues, overlays, or buffs by subscribing to health (`OnHealthChange`) or level (`OnLvlUp`) events.
- Required dependencies and contracts:
  - `PlayerManager` acts as the aggregate root, providing direct access to nested dependencies.
  - DOTween is used for visual effects like the scale shake on damage.
- Testing implications:
  - Verify changes by running `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Play-test in the Unity Editor to inspect the damage shake, death sequence, and UI transition.

## Integration Notes

- Upstream dependencies:
  - Reflex DI framework for injection of `IPlayerManager` and UI presenters.
  - DOTween for scale animations.
- Downstream consumers:
  - `GridManager` reads the player's position and velocity to update pathfinding flow fields and target prediction.
  - `IncreaseDifficultyTotem` checks player distance for keyboard interaction.
  - `ExpParticle` rewards experience to the player's level controller when collected.
  - `SawBlade` queries `GetMovementSpeed()` to scale knockback force.
  - UI presenters (`PlayerLevelPresenter`, `PlayerDeathPresenter`, `SkillUpgradePresenter`) display player progress and death screens.
- Cross-system coupling risks:
  - Ensure references are cleared or handled gracefully if the player is destroyed to avoid downstream NullReferenceExceptions.

## Known Risks and Open Questions

- Known limitations:
  - `PlayerDeathHandler` uses `GetComponentsInChildren<Collider>` on `Awake`. If colliders are dynamically attached to the player (e.g. from skills) after initialization, they will not be disabled on death.
  - `PlayerDamagedHandler` does not check for negative damage values in `TakeDamage`.
- Open design questions:
  - Should the player damage feedback (shake and VFX) be modularized rather than hardcoded in `PlayerDamagedHandler`?
- Suggested follow-up tasks:
  - Verify if newly added skills with dynamic colliders need explicit exclusion or inclusion in the death collider-disabling list.
