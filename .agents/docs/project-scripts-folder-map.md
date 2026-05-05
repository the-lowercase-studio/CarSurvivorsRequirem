# Project Scripts Folder Map

## Purpose

Use this document when adding, moving, or extracting C# code under `Assets/Scripts/`.

This map describes what each scripts folder should own and where code should be extracted during refactors. It is placement guidance, not a request to reorganize the whole project at once. Before moving code, inspect current references, serialized fields, prefab or scene usage, namespaces, and Reflex bindings.

## Refactor Placement Rules

1. Prefer the folder that owns the gameplay domain before using a generic folder.
2. Keep MonoBehaviours close to the scene, prefab, or system that owns their behavior.
3. Keep narrow interfaces colocated above their primary implementation when they are owned by one implementation.
4. Put reusable interfaces in a shared folder only when multiple unrelated systems depend on them.
5. Put constants in a `Constants` folder under the owning system root when new constants are introduced.
6. Do not move `.prefab`, `.unity`, `.asset`, or `.meta` files by hand unless the user explicitly asks.
7. Do not create broad catch-all folders for new behavior. If a script has a clear domain, use that domain folder.

## Top-Level Folder Map

### `Activators/`

Use for components that enable, activate, or initialize groups of scene or prefab items based on configuration.

Extract here when logic coordinates activation of configured objects but does not own the objects' domain behavior.

### `AnimationPlayers/`

Use for animation playback contracts or adapters that wrap animator/tween playback behind a small API.

Extract here when multiple systems need a shared animation player contract. Keep animation behavior that is specific to one domain inside that domain folder.

### `Audio/`

Use for audio clip configuration, playback services, mixer management, and background music behavior.

Extract here when code controls sound playback, volume routing, mixer values, or audio settings integration. UI controls for audio options belong under `UI/Settings/`; persisted setting models belong under `Settings/`.

### `Car/`

Use for the controllable car entity, car-specific input/movement coordination, and car visual effects.

Extract here when behavior is owned by the player car as a vehicle. Generic player lifecycle logic belongs under `Player/`; generic movement interfaces belong under `Movement/`.

### `Collectibles/`

Use for collectible item contracts and behavior.

Extract here when code describes pickup interaction, collection eligibility, or collectible item behavior independent of a specific reward system. Skill crates and skill-specific rewards should stay under `Skills/ObjectsImpactingSkills/` unless they become generic collectibles.

### `Collisions/`

Use for collision abstractions and event data shared by gameplay systems.

Extract here when collision reporting is reusable across enemies, projectiles, player, or interactables. Keep domain-specific collision responses in the owning domain folder.

### `CustomEventArgs/`

Use for reusable event argument types that are not owned by one domain.

Extract here only when event args are shared across unrelated systems. If event args are owned by one feature, colocate them in that feature folder.

### `CustomTypes/`

Use for small reusable value types that model generic data shapes.

Extract here when a type is not Unity-specific and is reused across unrelated domains, such as ranges or simple typed values.

### `DamageNumbers/`

Use for damage number entities, appearance animation, spawning, and display behavior.

Extract here when code creates, configures, animates, pools, or presents floating damage feedback. User setting models for enabling/disabling damage numbers belong under `Settings/`; UI controls belong under `UI/Settings/`.

### `Editor/`

Use for Unity Editor-only scripts, custom inspectors, editor windows, and project tooling.

Extract here when code references `UnityEditor` or should not be compiled into runtime builds. Keep editor GUI under `Editor/GUI/` and one-off tooling under `Editor/Tools/`.

### `Effects/`

Use for visual effect MonoBehaviours that are generic enough to attach to many objects.

Extract here when behavior is presentation-only, such as rotation loops, face-camera helpers, scale animations, or UI element effects. Domain-specific effects should remain with their owner, for example car VFX in `Car/`.

### `Enemies/`

Use for enemy entity behavior, enemy spawning policy, enemy movement, attacks, death handling, collisions, and enemy-specific animation.

Extract here when behavior is enemy-owned or depends on enemy lifecycle. Generic spawn interfaces belong under `Spawners/`; shared health or status behavior belongs under `HealthSystem/` or `StatusAffectables/`.

### `EventHandlers/`

Use for small reusable Unity event bridge components.

Extract here when a component adapts Unity events into C# events or callbacks and is not owned by a specific UI or gameplay domain.

### `Extensions/`

Use for extension methods on existing types.

Extract here when the primary purpose is extending an existing C#, Unity, DOTween, or project type without owning state. Prefer domain folders for behavior with lifecycle, dependencies, or serialized configuration.

### `FlowFieldSystem/`

Use for flow field generation, debug visualization, and movement controllers that consume flow field data.

Extract here when code calculates or follows flow fields. Grid storage and cell state belong under `GridSystem/`; enemy-specific use of flow fields belongs in `Enemies/` unless it is reusable movement infrastructure.

### `GameManipulators/`

Use for global game manipulation services such as time control and scene loading.

Extract here when code changes broad game state or scene flow. Register service dependencies through `ReflexDI/` when the service is injected.

### `GameWindow/`

Use for platform or window-display behavior.

Extract here when code manages resolution/window placement or platform display quirks. User-selectable graphics settings belong under `Settings/` and `UI/Settings/`.

### `GridSystem/`

Use for grid data structures, cells, grid queries, grid debug tools, camera visibility checks, and coordinate conversion.

Extract here when code owns grid state, cell lookup, walkability, edges, or grid-space queries. Spawning contracts that use grid positions belong under `Spawners/GridSpace/`; flow field pathing belongs under `FlowFieldSystem/`.

### `HealthSystem/`

Use for health values, health bars, regeneration, and health presentation that is shared across entities.

Extract here when code changes hit points, regeneration, health bar display, or health events. Damage application interfaces may belong under `StatusAffectables/` when they represent entity capability rather than health storage.

### `Helpers/`

Use for narrow static helpers that simplify Unity or project operations.

Extract here only when the helper has a clear reusable purpose and does not fit a stronger domain folder. Prefer `Extensions/` for extension methods and `Utils/` for domain-neutral pure utilities.

### `Initializers/`

Use for initialization contracts used by configured or pooled objects.

Extract here when multiple systems need a shared initialize method or scriptable-config initialization contract. Keep concrete initialization behavior in the owning domain folder.

### `LayerMasks/`

Use for typed access to project layer masks or layer categories.

Extract here when code centralizes layer definitions, terrain layers, entity layers, or layer mask helpers. Do not hide balance or scene-specific filtering decisions here.

### `LevelSystem/`

Use for player level progression, experience collection, level-up flow, and related particles.

Extract here when code manages experience, level thresholds, level-up events, or level progression visuals. UI display for level state belongs under `UI/Level/`.

### `Movement/`

Use for movement contracts shared by multiple moving entities.

Extract here when an interface or reusable movement abstraction has more than one unrelated owner. Keep concrete movement controllers in the owning domain folder unless they are truly generic.

### `ObjectLifeCycle/`

Use for object enable/disable lifecycle helpers and persistence behavior.

Extract here when code coordinates pooled object lifecycle, disable prerequisites, or scene persistence. Pool ownership and pool contracts belong under `Pooling/`.

### `Player/`

Use for player lifecycle, damage/death handling, player manager behavior, and player-owned game-state transitions.

Extract here when behavior is about the player as a gameplay actor rather than the car vehicle. Car physics and car visuals belong under `Car/`; player UI belongs under `UI/`.

### `Pooling/`

Use for pool contracts and pooled object release notification.

Extract here when code defines or implements reusable object pooling infrastructure. Domain-specific pool usage should remain in the domain folder that spawns or owns the objects.

### `Projectiles/`

Use for projectile behavior, projectile spawn configuration, and shared projectile lifecycle.

Extract here when code controls projectile movement, impact, initialization, or projectile configuration. Skill-specific aiming and firing decisions should stay under the owning skill folder.

### `Providers/`

Use for small provider interfaces that abstract object access.

Extract here only when a provider is shared across unrelated domains. Prefer explicit domain services in the owning folder when a provider has one clear owner.

### `ReflexDI/`

Use for Reflex installers, boot loading, and dependency registration.

Extract here when code configures project, scene, or gameplay dependency bindings. Do not place gameplay behavior here; installers should wire services that live in their owning folders.

### `ScoreBoard/`

Use for score entries, score storage coordination, score presentation, and best-score queries.

Extract here when code owns score data, persistence orchestration, or scoreboard display behavior. Generic storage primitives belong under `Storage/`.

### `Settings/`

Use for setting models, setting loaders, persisted setting values, and non-UI setting application.

Extract here when code defines a setting, loads/saves setting state, or applies setting values to runtime systems. Option UI components belong under `UI/Settings/`.

### `Shapes/`

Use for shape-related enums or shared shape definitions.

Extract here when code describes reusable geometric or targeting shapes. Skill-specific targeting shapes should stay with the skill until reused broadly.

### `Skills/`

Use for skill definitions, skill registry logic, upgradeable skills, skill stats units, player skills, turrets, and objects that affect skills.

Extract here when behavior belongs to unlockable/upgradable player abilities or skill-impacting world objects. Put each player skill in `Skills/PlayerSkills/<SkillName>/`. Put skill-affecting world objects in `Skills/ObjectsImpactingSkills/<ObjectName>/`.

### `Spawners/`

Use for shared spawning contracts, spawn chance data, and generic grid-space or world-space spawner abstractions.

Extract here when code defines where or how objects are spawned independent of a specific domain. Concrete enemy spawning belongs under `Enemies/`; damage number spawning belongs under `DamageNumbers/`; experience particle spawning belongs under `LevelSystem/Exp/`.

### `Stats/`

Use for generic upgradeable stat types.

Extract here when code models reusable stat values, stat upgrade rules, or stat value types used by multiple gameplay systems. Skill-only stat behavior can stay under `Skills/` until reused elsewhere.

### `StatusAffectables/`

Use for entity capability interfaces and controllers for status or combat effects, such as damage, stun, and knockback.

Extract here when code describes what effects an entity can receive or shared effect controllers. Health storage belongs under `HealthSystem/`; effect application owned by a skill stays under the skill folder.

### `Storage/`

Use for generic app storage wrappers and persisted value contracts.

Extract here when code abstracts PlayerPrefs or another persistence backend. Domain-specific persistence orchestration belongs in the domain folder, such as `ScoreBoard/` or `Settings/`.

### `UI/`

Use for presenters, UI components, menu behavior, pause/death screens, settings options, level display, and skill display.

Extract here when code owns Unity UI, player-facing presentation, or UI event handling. Keep non-UI setting state in `Settings/`, score state in `ScoreBoard/`, and gameplay logic in the owning gameplay folder.

### `Utils/`

Use for domain-neutral pure utility functions.

Extract here when code performs generic operations such as random selection, deep copy, time conversion, or easing lookup without Unity scene ownership. Prefer `Helpers/` for Unity-oriented helper operations and `Extensions/` for extension methods.

### `VFX/`

Use for reusable Visual Effect Graph playback or visual effect controllers.

Extract here when code controls VFX playback generically. Entity-specific VFX orchestration should stay under the owning domain, such as `Car/` or `Enemies/`.

### `Volumes/`

Use for trigger volumes and world volumes with reusable scene behavior.

Extract here when code represents area-based world behavior such as death zones. Domain-specific trigger volumes should stay with the owning domain if they are not reusable.

### `Waves/`

Use for wave timing, wave progression, and wave manager behavior.

Extract here when code controls enemy wave schedules, wave transitions, or wave-level gameplay pacing. Enemy spawn implementation remains under `Enemies/` unless it is truly wave-owned.

## Subfolder Conventions

- `Editor/GUI/`: custom inspectors and editor UI.
- `Editor/Tools/`: editor utilities and menu tools.
- `LevelSystem/Exp/`: experience particles and experience pickup/spawn behavior.
- `ObjectLifeCycle/Actions/`: contracts or actions that must complete before disabling pooled or lifecycle-managed objects.
- `Settings/Resolution/`: resolution data and resolution setting behavior.
- `Skills/ObjectsImpactingSkills/<ObjectName>/`: world objects that modify, grant, or interact with skills.
- `Skills/PlayerSkills/<SkillName>/`: concrete player skill implementation, related spawned objects, and skill-specific helpers.
- `Spawners/GridSpace/`: spawning abstractions that place objects using grid positions.
- `Spawners/WorldSpace/`: spawning abstractions that place objects in world coordinates.
- `UI/Death/`: player death screen presentation.
- `UI/Level/`: level and experience UI presentation.
- `UI/Settings/`: option components and settings screen UI.
- `UI/Skills/`: skill upgrade and skill visual presentation.

## Before Moving Existing Scripts

1. Search references with `rg` and Unity serialized references before moving.
2. Check whether namespaces or assembly definitions require updates.
3. Check Reflex installers in `Assets/Scripts/ReflexDI/` for bound concrete types and interfaces.
4. Preserve serialized field names and public API compatibility unless the user approves a breaking change.
5. Move files through Unity Editor when `.meta` GUID preservation matters.
6. Run `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false` after C# moves or namespace changes.
7. Perform Unity Editor checks for prefab, scene, inspector, audio, VFX, and UI wiring after any physical file move.

## When To Ask The User

Ask before:

1. Changing player-facing mechanics or balance while extracting code.
2. Moving serialized MonoBehaviours that are attached to prefabs or scenes.
3. Renaming public types, serialized fields, assets, prefabs, scenes, or folders.
4. Introducing a new top-level `Assets/Scripts/` folder.
5. Collapsing existing folders into a new architecture.

