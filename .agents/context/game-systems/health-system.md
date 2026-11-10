# Health System Documentation

## Purpose

The Health system owns mutable health state, alive/dead state, health change events, optional player regeneration, slider-based world-space health presentation, and cascading armor plate UI presentation attached to the player health bar.

It does not own damage calculation, target selection, enemy/player death presentation, armor state or absorption math, damage numbers, experience payout, VFX, audio, or pooling. Those systems consume IHealth, IHealthy, or IDamageable contracts and react to health events.

## Reading Map

- Primary code locations:
  - Assets/Scripts/HealthSystem/Health.cs
  - Assets/Scripts/HealthSystem/RegenativeHealth.cs
  - Assets/Scripts/HealthSystem/HealthBar.cs
  - Assets/Scripts/HealthSystem/ArmorPlatesView.cs
  - Assets/Scripts/HealthSystem/ArmorPlateItemView.cs
  - Assets/Scripts/HealthSystem/Constants/HealthConstants.cs
- Prefabs and authored assets:
  - Assets/Prefabs/UI/HealthBarCanvas.prefab
- Main integration points:
  - Assets/Scripts/Enemies/Base/Enemy.cs
  - Assets/Scripts/Enemies/Base/EnemyDeathHandler.cs
  - Assets/Scripts/Player/PlayerManager.cs
  - Assets/Scripts/Player/PlayerDamagedHandler.cs
  - Assets/Scripts/Player/PlayerDeathHandler.cs
  - Assets/Scripts/PowerUps/Shield/BubbleShieldService.cs
  - Assets/Scripts/PowerUps/Armor/ArmorService.cs
  - Assets/Scripts/PowerUps/Config/ArmorConfigSO.cs
  - Assets/Scripts/StatusEffects/IDamageable.cs
- Related systems:
  - Damage numbers: Assets/Scripts/DamageNumbers/
  - Power-ups (Bubble Shield and Armor): Assets/Scripts/PowerUps/
  - VFX: Assets/Scripts/VFX/
  - Experience payout: Assets/Scripts/LevelSystem/Exp/
  - Player death UI: Assets/Scripts/UI/Death/
- Related docs:
  - .agents/context/project-coding-standards.md
  - .agents/context/ai-game-dev-best-practices.md
  - .agents/context/game-systems/enemies-system.md
  - .agents/context/game-systems/damage-numbers-system.md
  - .agents/context/game-systems/player-system.md
  - .agents/context/game-systems/power-ups-system.md
- Related agents or instructions:
  - .agents/skills/document-system/SKILL.md
  - .agents/skills/architecture-review/SKILL.md
  - .agents/skills/check-optimalization/SKILL.md

## Architecture and Data Flow

- Core components:
  - Health is the base MonoBehaviour implementation of IHealth. It stores MaxHealth, CurrentHealth, alive state (_isAlive), and the events used by gameplay and UI.
  - RegenativeHealth (note the spelling in filename and class name) inherits Health and adds timed automatic healing through Update when current health is below max health. It heals only base player IHealth and never regenerates Armor plates.
  - HealthBar requires a Unity Slider, reads a serialized Health reference, updates the slider and fill color on health changes, supports external fill color overrides (SetColorOverride and ClearColorOverride used by BubbleShieldService), and shakes on health decrease through DOTween using HealthConstants.
  - ArmorPlatesView is attached to the player's world-space HealthBarCanvas alongside HealthBar. It validates a serialized _baseHealthBarRect and an array of 3 ArmorPlateItemView elements in Awake(), computes proportional plate widths from ArmorConfigSO, positions plates on the screen-right side of the health bar at the same vertical center height, enforces Canvas sibling indices so newer plates render on top of older plates, updates plate visibility and fill ratios, and triggers DOTween position shake feedback on armor hits.
  - ArmorPlateItemView represents an individual armor plate mini-bar with a background Image and a horizontally filled foreground Image (_fillImage.fillOrigin = Image.OriginHorizontal.Right) that depletes from the outer right edge inward toward the base health bar.
  - IHealthy is a narrow provider interface for objects that expose an IHealth reference.
  - IDamageable is the status-affectable contract used by attackers and helpers. Its implementations decide how incoming damage maps to shields, armor, health, VFX, audio, and presentation.
- Key interfaces:
  - IHealth exposes CurrentHealth, mutable MaxHealth, health change events (OnHealthChanged, OnHealthDecreased, OnHealthIncreased, OnNoHealth), DecreaseHealth, IncreaseHealth, and IsAlive.
  - IRegenativeHealth extends IHealth with regeneration amount and delay state.
  - IHealthy exposes an IHealth Health property and is implemented by Enemy, PlayerManager, and IPlayerManager.
  - IDamageable exposes TakeDamage and TakeFullHpDamage; current player and enemy implementations reduce health through their owned IHealth.
- Centralized constants:
  - HealthConstants (Assets/Scripts/HealthSystem/Constants/HealthConstants.cs):
    - HEALTH_BAR_SHAKE_DURATION = 0.1f
    - HEALTH_BAR_SHAKE_STRENGTH = 0.14f
    - HEALTH_BAR_SHAKE_RANDOMNESS = 90f
    - HEALTH_BAR_SHAKE_VIBRATO = 3
    - HEALTH_BAR_SHAKE_SNAPPING = false
    - HEALTH_BAR_SHAKE_FADE_OUT = true
- Runtime flow:
  - Aggregate Event Forwarding Chain: Health.OnEnable subscribes InvokeOnHealthChanged to its own narrow events: OnHealthDecreased, OnHealthIncreased, and OnNoHealth. Raising any of these narrow events automatically invokes OnHealthChanged to notify general observers (such as HealthBar). These subscriptions are unsubscribed in OnDisable to avoid duplicate forwarding across re-enable cycles of pooled objects.
  - Health.DecreaseHealth exits when already dead. If remaining health is greater than incoming damage, it subtracts the value and raises OnHealthDecreased; otherwise it sets health to zero, marks dead (_isAlive = false), and raises OnNoHealth.
  - Health.IncreaseHealth exits when dead. If the increase stays below max health, it adds health and raises OnHealthIncreased; otherwise it clamps to MaxHealth without raising OnHealthIncreased or OnHealthChanged.
  - RegenativeHealth.OnEnable resets regeneration amount and delay from serialized values after base health reset.
  - RegenativeHealth.Update runs regeneration every frame when CurrentHealth < MaxHealth and both regeneration amount and delay configs are positive.
  - Regeneration Process: RegenativeHealth.RegenerationProcess checks if CurrentRegenerationDelay is greater than 0. If so, it decrements it by Time.deltaTime. Otherwise, if CurrentHealth > 0 (preventing regeneration from dead state), it calls IncreaseHealth(MaxRegenerationAmount) and resets the delay to StartRegenerationDelay.
  - Enemy.OnGet sets Health.MaxHealth from EnemyConfigSO.MaxHealth. Because pooled enemies are activated after this setup, Health.OnEnable then resets CurrentHealth to the configured max health.
  - Enemy.TakeDamage spawns a damage number, decreases health, and only plays blood VFX when Health.IsAlive() remains true.
  - EnemyDeathHandler listens to Health.OnNoHealth, disables physical interaction and visuals, plays death feedback, spawns experience, and completes the pool-release sequence after VFX and audio finish.
  - PlayerManager.Awake caches IHealth from the player object. Player damage and death handlers use the injected IPlayerManager.Health reference.
  - PlayerDamagedHandler.TakeDamage evaluates combat protection in strict priority order:
    1. If IBubbleShield.IsActive is true, calls _bubbleShield.NotifyDamageAbsorbed() and returns immediately.
    2. If IArmorService.HasArmor is true, calls damage = _armorService.AbsorbDamage(damage), which depletes active armor plates from highest level downward and shakes ArmorPlatesView. If damage is fully absorbed (damage <= 0f), returns before reducing base health.
    3. If remaining damage > 0f, decreases base player health via _playerManager.Health.DecreaseHealth(damage), plays damage audio/VFX, and shakes the car visual scale.
  - PlayerDamagedHandler.TakeFullHpDamage (used for environmental out-of-bounds kills) calls _armorService.ClearArmorImmediate() and then decreases base health by MaxHealth, bypassing Bubble Shield.
  - ArmorPlatesView Layout and Layering Flow:
    1. Because HealthBarCanvas uses FaceMainCameraDirection with local +Z facing the camera, local +X maps to screen-left and local -X maps to screen-right.
    2. ArmorPlatesView.Initialize(ArmorConfigSO) computes baseVisualRightEdge = _baseHealthBarRect.anchoredPosition.x - (baseWidth * _baseHealthBarRect.pivot.x) and baseCenterY.
    3. Each plate width is scaled proportionally to its capacity: plateWidth = (capacity / ReferenceBaseMaxHealth) * ReferenceBaseHealthWidth (yielding widths 0.30, 0.60, and 0.90 for 20, 40, and 60 HP capacities against a 1.5-unit 100 HP bar).
    4. Plates step toward screen-right (local -X) with pivot (1f, 0.5f), overlapping the previous element by plateWidth * HorizontalOverlapRatio at the exact same vertical height (posY = baseCenterY).
    5. Sibling order is enforced via SetSiblingIndex(baseSiblingIndex + 1 + i) so Plate 1 renders on top of HealthBarSlider, Plate 2 renders on top of Plate 1, and Plate 3 renders on top of Plate 2.
    6. ArmorPlateItemView sets _fillImage.fillOrigin = (int)Image.OriginHorizontal.Right so damage depletes from the outer right edge inward.
  - PlayerDeathHandler listens to Health.OnNoHealth, hides the player visual, disables non-wheel colliders, plays death VFX, then enables the death screen after the VFX finishes.

## Rules and Invariants

- Critical behavior rules:
  - Health.OnEnable is the reset point for current health and alive state. Pooled or re-enabled objects come back alive at MaxHealth.
  - OnNoHealth is raised once per alive lifetime because _isAlive is set false before the event is invoked.
  - OnHealthChanged is derived from OnHealthDecreased, OnHealthIncreased, and OnNoHealth; consumers should subscribe to the narrow event when they need a specific semantic.
  - Dead health cannot be increased through IncreaseHealth; resurrection is not supported by the current contract.
  - Enemy health max is configured from EnemyConfigSO.MaxHealth on pool get. Player health and regeneration values are configured on the player RegenativeHealth component.
  - HealthBar depends on a concrete serialized Health reference, not IHealth, because Unity serializes the component reference.
  - ArmorPlatesView must fail fast in Awake() with InvalidOperationException if _baseHealthBarRect is null or if _plateViews does not contain exactly ArmorConstants.MAX_ARMOR_LEVEL (3) non-null ArmorPlateItemView references.
  - Both HealthBar and ArmorPlatesView must kill active DOTween shake tweens in OnDisable() and OnDestroy().
- Ordering or sequencing guarantees:
  - For enemy damage, damage numbers are spawned before health is reduced, and blood VFX plays only if the enemy survives the hit.
  - For player combat damage, Bubble Shield is checked before Armor, and Armor absorbs damage before base IHealth is reduced.
  - Death handlers are event subscribers to OnNoHealth; they do not poll health.
  - OnNoHealth also raises OnHealthChanged through the internal subscription created in Health.OnEnable.
  - Health.OnDisable removes internal event forwarding subscriptions to avoid duplicate forwarding across re-enable cycles.
  - Regeneration starts after StartRegenerationDelay, then repeats after each delay while health remains below max.
- Constraints contributors must preserve:
  - Preserve OnNoHealth as the authoritative death trigger for enemies and players unless the death flow is intentionally redesigned.
  - Do not bypass health events by mutating CurrentHealth directly; it has a protected setter for this reason.
  - Treat changes to clamping, event firing, death state, regeneration timing, or max-health reset as player-facing gameplay changes.
  - Preserve serialized fields on health, health bar, and armor plate view components so existing prefabs and scene references remain intact.
  - Keep damage application through explicit contracts such as IDamageable, IHealthy, and IHealth; do not add singleton access or broad scene searches.

## Extension Points

- Safe extension areas:
  - Add new health consumers by depending on IHealthy or IHealth and subscribing to lifecycle-matched events.
  - Add new damage producers through IDamageable.TakeDamage rather than calling concrete player or enemy implementations directly.
  - Add local UI reactions by subscribing to OnHealthChanged, OnHealthDecreased, or OnHealthIncreased.
  - Add specialized health behavior by deriving from Health only when the existing reset and event semantics remain valid.
  - Re-wire or update HealthBarCanvas armor plate hierarchy in the Unity Editor using Tools/Armor/Wire Armor Power-Up in Assets/Scripts/Editor/Tools/ArmorWiringTool.cs.
- Required dependencies and contracts:
  - Objects exposing IHealthy must have a component that implements IHealth available when their aggregate Awake runs.
  - Player setup currently requires RegenativeHealth through PlayerManager.
  - Enemy prefabs are expected to provide an IHealth component so Enemy.Awake can cache it.
  - HealthBar needs a valid Slider, serialized Health, fill Image, and gradient.
  - ArmorPlatesView requires _baseHealthBarRect and 3 ArmorPlateItemView references wired on Assets/Prefabs/UI/HealthBarCanvas.prefab.
  - DOTween is required for health bar and armor plate shake behavior.
- Testing implications:
  - Compile after C# edits with dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false.
  - For health logic changes, test damage smaller than current health, damage equal to current health, overkill damage, dead-state repeated damage, healing below max, healing to max, and healing while dead.
  - For regeneration changes, test delay countdown, repeated regeneration, max-health clamping, zero regeneration amount, and zero delay.
  - In Unity, verify player death screen timing, enemy death VFX/audio completion, experience spawn, pooled enemy health reset, health bar updates, Bubble Shield color override, and Armor plate cascading layout and right-to-left depletion.

## Integration Notes

- Upstream dependencies:
  - Enemy configuration provides EnemyConfigSO.MaxHealth.
  - Enemy attacks, helper utilities, projectiles, skills, and other combat producers reach health through IDamageable implementations.
  - Player and enemy managers cache IHealth from components on their own GameObjects.
  - BubbleShieldService drives HealthBar.SetColorOverride and ClearColorOverride.
  - ArmorService drives ArmorPlatesView.Initialize, UpdatePlates, and PlayDamageShake using ArmorConfigSO.
  - Unity lifecycle order matters for pooled enemies because Enemy.OnGet sets max health before activation resets current health.
- Downstream consumers:
  - EnemyDeathHandler consumes OnNoHealth for death sequence, experience payout, and pool release completion.
  - PlayerDeathHandler and ArmorService consume OnNoHealth for death cleanup.
  - HealthBar consumes OnHealthChanged and optionally OnHealthDecreased for UI updates.
  - Damage numbers are currently produced by Enemy.TakeDamage, not by Health itself.
- Cross-system coupling risks:
  - Changing when OnHealthChanged fires can break UI updates and any future aggregate health observers.
  - Changing death event timing affects enemy pool release, experience payout, player death UI, armor clearing, and VFX/audio sequencing.
  - Moving enemy max-health assignment after activation would prevent pooled enemies from resetting to the intended configured health.
  - Replacing HealthBar's concrete Health serialized reference with an interface would require a Unity-serializable pattern.

## Known Risks and Open Questions

- Known limitations:
  - RegenativeHealth is misspelled in the type and filename; renaming it would require Unity serialization and reference migration care.
  - Health.IncreaseHealth does not raise OnHealthIncreased or OnHealthChanged when it clamps to MaxHealth.
  - HealthBar.OnEnable initializes slider max/value but does not initialize the fill color until the first health event or color override call.
  - DecreaseHealth and IncreaseHealth do not validate negative values, so callers are responsible for passing positive damage or healing.
  - Regeneration delay does not reset when damage is taken; it only resets after a regeneration tick.
- Open design questions:
  - Should healing-to-max fire health change events so UI and observers always receive a notification?
  - Should regeneration be interrupted or its delay reset by taking damage?
  - Should HealthBar depend on a component type that implements IHealth through an adapter-friendly serialized reference pattern?
- Suggested follow-up tasks:
  - Add focused edit-mode tests for Health event semantics and boundary values.
  - Review whether max-health healing should emit events before changing UI assumptions.
  - Consider a dedicated serialization-safe cleanup for the RegenativeHealth spelling if the project can tolerate asset migration.
