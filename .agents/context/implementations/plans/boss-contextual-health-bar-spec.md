# Specification: Contextual Boss Health Bars

Date: 2026-10-17
Author: Codex
Status: Implemented on 2026-10-17 following the user's explicit implementation request. See the implementation summary for validation evidence and remaining manual checks.
Target systems: Boss HUD, boss encounter coordination, Golem phase configuration/runtime consumers, and Mortar Tower enrage configuration.

## 1. Overview and Player Experience

Replace the shared legacy boss Slider visual with separate Tower and Golem artwork layouts. Reuse the existing presenter, encounter ownership, display names, and health events. Each layout contains a main background, normal and enraged fills, and matching permanent backgrounds underneath the fills.

Normal health drains first. Both bosses enter enraged at 30% HP, with a repeating DOTween punch on the enraged segment as it starts draining. Golem has exactly two phases: normal above 30% HP and enraged at or below 30%, using its former phase 3 settings as the new phase 2. Preserve the Golem's angled joins with authored overlap; the Tower uses an adjacent join without a deliberate overlap.

## 2. Resolved Decisions and Open Questions

### User Requirements

- Tower encounters use Assets/Imported/Images/UI/Boss/TOWER_HP_BAR_SPRITESHEET.png.
- Golem encounters use Assets/Imported/Images/UI/Boss/BOSS_HP_BAR_SPRITESHEET.png.
- Replace existing visual components partially or completely as required by the artwork.
- Preserve the Golem overlap, separate fill backgrounds, and enraged punch feedback.
- User revision: unify Tower and Golem enrage thresholds at 0.3 (30% HP), including authored configuration and new-config defaults.
- User revision: remove Golem's intermediate phase at 60% HP. Its former phase 3 becomes phase 2 with the same enrage behavior, multipliers, and barrage cycle settings. Expose only two phases in the Inspector and runtime contracts.
- The original request authorized specification only; the subsequent explicit implementation request authorized this migration.

### Source-Grounded Design Constraints

- Target thresholds are Tower Config.EnrageHealthPercent = 0.3 and Golem Config.Phase2HealthPercent = 0.3 after the two-phase migration. Preserve configuration ownership in their current ScriptableObjects.
- The user explicitly approves the resulting balance changes in this plan: Tower enrage begins later, and Golem retains phase 1 behavior until 30% HP. Preserve all other balance values and existing enrage behavior.
- Use boss-owned state rather than inferring boss type from display names or recreating combat state in UI.
- Keep both source textures and their existing semantic slice identities. Tower has two tiny unrelated automatic slices; do not bind them.
- Preserve the current single-visible-boss HUD scope, health smoothing intent, title, encounter reset, death, and swarm ordering.
- Retain the current scene presenter and Reflex binding; author child layouts and new serialized references through Unity Editor during a later authorized implementation.

### Open Questions

None requiring a gameplay or data-ownership decision for this specification. Visual measurements, punch strength, and timing are reversible implementation tuning and will be specified as initial values subject to Editor verification. The subsequent explicit implementation request satisfied the authorization gate; implementation evidence is recorded in the runtime summary.

### Review Before Code Changes

The proposed migration removes the legacy Slider, adds two authored child layouts, replaces the presenter's three legacy serialized visual fields, updates Tower's threshold, and reduces Golem's configuration/runtime phase count to two. Keep _visual and _bossTitleText assigned to their existing scene objects. New layout references require Unity Editor assignment; do not rename serialized fields, use FormerlySerializedAs, or silently recover missing assignments. Before removing Golem phase 3 fields, copy each asset's phase 3 values into its retained phase 2 fields through Unity Editor as detailed below. The user resolved the threshold/phase requirements and subsequently authorized implementation. This document does not authorize direct scene/prefab/meta YAML edits.

## 3. Verified Current State Before Implementation

The following describes the existing source, not the target behavior. The approved migration in section 4 replaces its 40% Tower threshold and three-phase Golem configuration.

| Area | Verified implementation and implication |
| --- | --- |
| HUD | Assets/Scripts/UI/HUD/BossHUDPresenter.cs defines IBossHUDPresenter.Show(IHealth, string), Hide(), one Slider, one fill Image, a Gradient, and a TMP title. Health changes use a 0.2-second Ease.OutQuad Slider tween. The view hides immediately on zero HP. |
| Scene | Assets/Scenes/RuinedBloodCity.unity contains MainCanvas and BossHealthBarPresenter, with an active presenter root and inactive Visual child. The presenter root has bottom anchors in the serialized scene. Preserve that authored placement; older brainstorm references to a top-screen bar are not current scene truth. |
| DI | Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs binds its serialized BossHUDPresenter as IBossHUDPresenter and BossEncounterService as IBossEncounterService when assigned. Retain those instances and registrations. |
| Encounter | Assets/Scripts/Enemies/Bosses/BossEncounterService.cs forwards health/name to the HUD, suppresses swarms, tracks towers, spawns the Golem, and handles the progression portal. It does not receive boss kind or enrage state today. |
| Tower entry | Assets/Scripts/Enemies/Bosses/Towers/Arena/EncounterArenaController.cs starts the tower, calls NotifyEncounterEngaged, and then updates the camera/perimeter. A legacy no-boss path passes null health but still marks the encounter engaged. Reset hides the HUD before resetting tower health/state. |
| Tower enrage | Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs latches IsEnraged at HP ratio <= Config.EnrageHealthPercent. Assets/ScriptableObjects/Enemy/Towers/MortarTowerConfig.asset authors 3500 max HP and threshold 0.4. ResetEncounter clears the latch. |
| Golem enrage | Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs enters phase 2 at Config.Phase2HealthPercent and phase 3/enraged at Config.Phase3HealthPercent. Assets/ScriptableObjects/Enemy/Bosses/Golem/GolemBossConfig.asset authors 5500 max HP, phase 2 at 0.6, phase 3 at 0.3. |
| Health events | Assets/Scripts/HealthSystem/Health.cs forwards OnHealthDecreased, OnHealthIncreased, and OnNoHealth into OnHealthChanged. Filling health exactly to max does not raise an increase/change event. Preserve this behavior; Show must always take a fresh snapshot, including after tower reset. |
| Tween helpers | Assets/Scripts/Extensions/TransformTweenExtensions.cs has scale/rotation loops and shrink feedback, but no anchored-position punch helper. Assets/Plugins/Demigiant/DOTween/Modules/DOTweenModuleUI.cs supplies DOPunchAnchorPos. A local view use is sufficient. |
| Environment | ProjectSettings/ProjectVersion.txt declares Unity 6000.4.5f1. Packages/manifest.json declares uGUI 2.0.0 and Reflex 12.0.1. No package upgrade is required. |

Initial worktree inspection found the two new textures/metas, deleted BOSS_BAR_EMPTY/FRAME/FULL textures/metas, and unrelated user modifications. Those are pre-existing user work; preserve their current state during later implementation. A search of current scene/prefab/asset text found no references to the three deleted texture GUIDs; do not assume those deleted files are the current scene's assigned sprites.

### Source Artwork Inventory

Read-only inspection covered both PNGs and their existing .png.meta sprite rectangles. Both source images are 2560 x 1440, imported as multiple sprites. Their default and Standalone max texture size is 2048, so source-pixel measurements are authoring references rather than guaranteed imported texel dimensions.

| Role | Golem sprite | Tower sprite |
| --- | --- | --- |
| Main background | GOLEM_BOSS_HP_BAR_MAIN_BACKGROUND | TOWER_HP_BAR_MAIN_BACKGROUND |
| Enraged fill | GOLEM_BOSS_HP_BAR_FILL_PART_ENRAGED | TOWER_HP_BAR_FILL_PART_ENRAGED |
| Normal fill | GOLEM_BOSS_HP_BAR_FILL_PART_NORMAL | TOWER_HP_BAR_FILL_PART_NORMAL |
| Enraged fill background | GOLEM_BOSS_HP_BAR_FILL_BACKGROUND_PART_ENRAGED | TOWER_HP_BAR_FILL_BACKGROUND_PART_ENRAGED |
| Normal fill background | GOLEM_BOSS_HP_BAR_FILL_BACKGROUND_PART_NORMAL | TOWER_HP_BAR_FILL_BACKGROUND_PART_NORMAL |

- Golem main background: 2469 x 278. Enraged fill: 781 x 75; normal fill: 1671 x 75. Backgrounds: 781 x 75 and 1673 x 75. Fill rectangles overlap in source X by 76 pixels; background rectangles overlap by 78 pixels. These rectangle intersections demonstrate why edge-to-edge packing is unsuitable; they are not final assembled overlap measurements because the rectangles contain transparent margins.
- Tower main background: 2467 x 241. Enraged fill: 708 x 100; normal fill: 1671 x 100. Backgrounds: 707 x 100 and 1670 x 100. The artwork includes a stepped interlocking detail; follow the user's non-overlapping Tower layout requirement and align the actual visible join, not the arbitrary spacing between rows on the source sheet.
- Do not bind TOWER_HP_BAR_SPRITESHEET_0 (6 x 7) or TOWER_HP_BAR_SPRITESHEET_2 (6 x 6). They are tiny automatic slices outside the five semantic roles.
- Preserve semantic sprite names, GUIDs, local IDs, alpha, and existing slice boundaries. Re-slicing, repainting, or changing texture import quality is outside this migration unless Editor verification identifies a concrete issue requiring review.

## 4. Health Mapping and State Rules

Keep the artwork's short enraged segment on the left and long normal segment on the right. The bar drains from right to left, normal first. Backgrounds are fully visible layers beneath the fills, not delayed damage trails or additional HP pools.

Let h = Clamp01(CurrentHealth / MaxHealth), t = the owning boss's configured enrage threshold, and e = the boss's actual latched IsEnraged state. Require MaxHealth > 0 and 0 < t < 1; reject invalid source configuration explicitly.

```text
normalTarget  = Clamp01((h - t) / (1 - t))
enragedTarget = e ? Clamp01(h / t) : 1
```

The two fills represent separate HP ranges: normal covers (t, 1], enraged covers [0, t], with t = 0.3 in both target configurations. They are presentation mappings of one health value, not independent health values. Preserve the authored art proportions rather than deriving combat thresholds from sprite rectangles; visible segment widths need not represent exact HP percentages. The approved balance changes are limited to the shared 30% threshold and removal of Golem's intermediate phase.

| Boss/state | HP ratio | Normal target | Enraged target | Punch |
| --- | --- | --- | --- | --- |
| Either, fresh engagement | 1 | 1 | 1 | Off |
| Either, normal phase | 0.65 | 0.5 | 1 | Off |
| Either, normal phase | 0.6 | ~0.4286 | 1 | Off |
| Either, normal phase | 0.4 | ~0.1429 | 1 | Off |
| Either, threshold committed; Golem phase 2 | 0.3 | 0 | 1 | On |
| Either, enraged; Golem phase 2 | 0.15 | 0 | 0.5 | On |
| Either, dead | 0 | 0 | 0 | Off; HUD hidden immediately |

### Approved Two-Phase Golem Configuration Migration

Keep CurrentPhase as the existing public integer contract with only values 1 and 2. Phase 1 uses its existing baseline stats and multipliers of 1. Phase 2 is the sole enraged phase. On a living hit reaching HP ratio <= Config.Phase2HealthPercent, set CurrentPhase = 2 and IsEnraged = true, run the existing enrage feedback once, then publish the presentation change. Remove the 60% intermediate transition and every runtime branch selecting phase 3. Healing does not lower the phase; enable/reset retains the existing phase 1 initialization.

Retain the serialized _phase2* and _skyBarrageCyclesPhase2 field names and their Phase2* public accessors, but replace their values with the former phase 3 values. After migrating data, remove _phase3* fields/accessors and _skyBarrageCyclesPhase3/SkyBarrageCyclesPhase3. Do not leave hidden or unused third-phase settings in the Inspector, and do not merely set both old thresholds to 30%.

| Setting | Current GolemBossConfig.asset | Target GolemBossConfig.asset | New-config C# default |
| --- | --- | --- | --- |
| _phase2HealthPercent | 0.6 | 0.3, from former _phase3HealthPercent | 0.3 |
| _phase2CooldownMultiplier | 0.7 | 0.4, from former _phase3CooldownMultiplier | 0.4 |
| _phase2SpeedMultiplier | 1.15 | 1.35, from former _phase3SpeedMultiplier | 1.35 |
| _phase2ArmSpeedMultiplier | 1.25 | 1.6, from former _phase3ArmSpeedMultiplier | 1.6 |
| _skyBarrageCyclesPhase2 | 3 | 5, from former _skyBarrageCyclesPhase3 | 3, the former phase 3 C# default |
| _skyBarrageCyclesPhase1 | 2 | 2, unchanged | 2, unchanged |

The authored asset's five enraged barrage cycles are intentional; do not replace them with the C# default of three. Inventory all GolemBossConfigSO assets and any serialized overrides before implementation, copy each asset's own former phase 3 values, and enforce the user-requested 0.3 threshold. Inspection currently found one such Golem asset. Save a pre-migration value snapshot under .agents/context/tmp/ before changing/removing fields, then verify the migrated values after Editor serialization. Do not rely on changing class defaults to update an existing asset.

Update GolemBoss.CurrentCooldownMultiplier, CurrentSpeedMultiplier, and CurrentArmSpeedMultiplier to select only baseline or the migrated phase 2 multipliers. Update Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemSkyBarrageState.cs so GetTotalCyclesForCurrentPhase selects only phase 1 or phase 2, with no fallback to removed phase 3 data. Preserve its existing attack-entry cycle snapshot: a barrage already in progress is not restarted or given additional cycles on enrage. Other attack timing and transitions keep their current behavior.

For Tower, set _enrageHealthPercent to 0.3 in Assets/ScriptableObjects/Enemies/Bosses/MortarTowerConfigSO.cs and in every relevant authored Tower config, including Assets/ScriptableObjects/Enemy/Towers/MortarTowerConfig.asset. Retain its existing two-state normal/enraged logic and all attack settings. Tower at 40% is now normal; it enters enraged only at or below 30%. Do not add a shared mutable threshold service or a HUD override: the two config values remain explicitly aligned, and the UI reads its owning boss's config.

### Snapshot and Smoothing

- Show renders current health and state immediately, including a boss already enraged. It never animates from the previous boss or from full HP.
- Preserve the existing 0.2-second Ease.OutQuad health response with one interpolated displayed HP ratio. Derive both fill amounts from that same displayed ratio and the committed source state on each tween update. Do not tween the two fill amounts independently: a large hit must drain normal before visibly crossing into enraged.
- On a threshold-crossing hit, gameplay commits IsEnraged and publishes its presentation event; the punch starts immediately. Health smoothing may finish over the existing 0.2-second response. The enraged fill never decreases before the source has committed enrage.
- Health and presentation events may both arrive for one hit. Snapshot deduplication prevents identical retargeting and repeated punch restarts. Never assume delegate subscription order makes the boss's state current during every health callback.
- Rapid hits retarget from the currently displayed ratio to the latest ratio. There is no queued backlog. Clamp values to [0, 1], and use the current MaxHealth on every refresh.
- Enrage stays latched when health rises above t, as in current gameplay. The fills follow health upward; the punch continues while the living source remains enraged. Do not invent de-enrage on healing.
- Dead/invalidated sources terminate presentation immediately, with no death-bar animation delaying encounter progression. Zero HP never starts a new punch, including a lethal hit that skips every threshold.

## 5. Data Model, Contracts, and Reflex DI

### Proposed Shared Boss Presentation Contract

NEW: Assets/Scripts/Enemies/Bosses/IBossHUDSource.cs.

Define one narrow shared contract, IBossHUDSource : IHealthy, plus its supporting BossHealthBarKind enum (Tower, Golem). This is a shared contract used by two primary implementations, so a shared file is appropriate. Do not introduce a duplicate health service or expose either concrete boss class to the presenter.

| Member | Responsibility |
| --- | --- |
| Health, inherited from IHealthy | Existing health instance; never a copied HP store. |
| HealthBarKind | Explicit Tower/Golem identity, supplied by the owning implementation. |
| EnrageHealthPercent | Reads the target config: Tower.EnrageHealthPercent or Golem.Phase2HealthPercent, both authored at 0.3 after migration. |
| IsEnraged | Existing boss-owned latch. |
| IsActive | Owner is enabled/active and its health is alive. Used to invalidate a disabled source. |
| event Action OnPresentationChanged | Announces a committed enrage/reset/activation/disable change; consumer re-reads the snapshot. |

MODIFY: Assets/Scripts/Enemies/Bosses/Golem/IGolemBoss.cs and Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs.

MODIFY: Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs, including its colocated IMortarTowerBoss.

- Have both boss interfaces inherit IBossHUDSource. Remove redundant inherited declarations of Health/IsEnraged rather than hiding members and introducing compiler warnings. Reuse existing implementation properties.
- Add kind, threshold, active-state projection, and the presentation event without adding serialized boss fields. Config assets and Golem's existing serialized phase fields change only as specified in the approved migration in section 4.
- Publish after TriggerEnrage/TriggerEnragePhase has committed its existing state and visual/audio side effects; preserve relative order of existing side effects. Publish activation/reset only after state initialization/reset completes. Publish disable invalidation during cleanup, after the owner is no longer active.
- Golem phase 2 is its sole enraged phase after migration. UI must not own phase progression or invoke combat transitions.
- Keep the existing health event subscriptions, death handlers, health mutation, and boss state machine logic in their owners.

### Encounter-to-Presenter Contract

- Change IBossHUDPresenter.Show to Show(IBossHUDSource source, string bossName). Keep Hide().
- Change IBossEncounterService.NotifyEncounterEngaged to NotifyEncounterEngaged(IBossHUDSource source, string displayName).
- EncounterArenaController passes _boss; BossEncounterService's Golem spawn path passes _activeGolemInstance. Display names remain their current designer-authored strings; never select a skin by comparing those strings.
- Preserve the legacy null-source arena path: it still records an engaged encounter and suppresses swarms, but does not call Show with a null source. Do not make this existing optional arena path fail validation or introduce a fake default skin.
- Preserve StartEncounter -> encounter notification -> camera/perimeter ordering; preserve disengage/HUD-hide -> tower reset ordering and existing boss defeat/portal/swarm ordering.
- BossEncounterService retains coordination only. BossHUDPresenter owns health/presentation subscriptions; views own rendering and punches. Do not add event forwarding through the encounter service.

### DI and Serialized Ownership

- Retain IBossHUDPresenter and IBossEncounterService bindings in Assets/Scripts/ReflexDI/DefaultGameplaySceneInstaller.cs. No new service registrations, project singletons, static mutable state, factories, or scene searches are needed.
- IBossHUDSource is passed with an encounter, not resolved as a global current-boss binding.
- Sprite assignments, overlap offsets, and punch settings belong to two scene-authored view components. Do not create a ScriptableObject skin catalog for two fixed layouts.
- Keep existing GolemBossConfigSO and MortarTowerConfigSO as the single source of truth for thresholds. Do not copy threshold values into the HUD Inspector.

## 6. Visual Layout, Serialization, and Tweening

NEW: Assets/Scripts/UI/HUD/BossHealthBarView.cs. Use the same component type for the two authored view instances. No DI, health subscriptions, or boss-type branching inside the view.

Proposed hierarchy under the retained presenter:

```text
BossHealthBarPresenter                  existing active presenter root
  Visual                               existing visibility container
    TowerHealthBar                     BossHealthBarView; initially inactive
      MainBackground                   Simple Image
      EnragedFillBackground            Simple Image
      NormalFillBackground             Simple Image
      NormalFill                       Filled Image
      EnragedFillMotion                RectTransform, punch target
        EnragedFill                    Filled Image
    GolemHealthBar                     same roles; separate authored geometry
    BossText                           existing TMP title, retained, draws last
```

This hierarchy states the intended sibling draw order: main background first, both fill backgrounds underneath both fills, then fills. In the Golem overlap region the enraged fill draws above the normal fill; no normal background may obscure an enraged fill. Keep the title above the artwork, and leave space for its crown/tower crest without moving the global HUD anchor.

### Image and Geometry Rules

- Fill Images use Type Filled, Horizontal, Left origin. Decreasing fillAmount removes the right side. Background Images remain full-size Simple Images. Keep Image transforms fixed during health drain; do not scale the artwork or drive fill width through a Slider.
- Default Image tint is white so the supplied white fills and colored backgrounds retain their authored colors. Retire the old Gradient treatment and inherited red/dark Image tints when replacing the visual subtree.
- Use full-rect Image rendering and intentional rect dimensions matching the slice aspect ratios. The current slices have zero nine-slice borders; do not select Sliced or invent borders to fit the layout.
- Align each fill with its matching background by visible contour, accounting for the 1-2 pixel slice differences and source-row offsets. Matching centers or copying all native sizes alone is insufficient.
- Golem: deliberately overlap the normal/enraged fill rectangles and the corresponding background rectangles until the angled seams assemble correctly. Treat the 76/78 source-X intersections as inspection references, then record the actual authored local offsets. Verify at full health, just above/at/below t, partial enraged fill, and during punch motion.
- Tower: align adjacent segment contours without an intentional overlap. Remove source-sheet spacing through authored placement; do not copy the gap between the exported rectangles into the live bar. Its stepped details must remain legible at the join.
- Scale each entire artwork assembly uniformly into the existing HUD width budget, with its own height derived from its art. Do not force the Golem and Tower backgrounds into the same width-and-height rectangle. Keep crests, title, fill edges, and neighboring HUD controls within the screen at 16:9, 16:10, and 21:9.
- Disable raycastTarget on all bar Images and the title. Do not let this read-only HUD take selectable focus or intercept player UI.
- Use the existing UI material. No custom shader, stencil mask, Canvas rebuild loop, new texture, or runtime sprite slicing is required. Keep punch small enough to remain within the authored interior margin.

### Proposed Serialized Fields

| Owner | Fields and migration |
| --- | --- |
| BossHUDPresenter | Keep _visual and _bossTitleText. Add _towerHealthBar and _golemHealthBar as BossHealthBarView references; add _healthTweenDuration with initial 0.2 seconds. Remove _healthSlider, _fillImage, and _healthGradient together with their retired scene components at cutover. |
| BossHealthBarView | Required Image refs _mainBackground, _normalFillBackground, _enragedFillBackground, _normalFill, _enragedFill; required RectTransform _enragedFillMotion. Assign the five sprites directly on those scene Images. |
| BossHealthBarView tuning | _enragePunchOffset (initial Vector2(0, 2) in reference Canvas units), _enragePunchDuration (0.28 seconds), _enragePunchInterval (0.18 seconds of rest after a punch), _enragePunchVibrato (8), _enragePunchElasticity (0.35). These are initial visual tuning values, not balance changes. |

Use private serialized fields with English tooltips for non-obvious settings. No serialized field rename is needed. Removal/replacement is deliberate: scene Inspector wiring is required during the same cutover, and the implementation must report those assignments to the user. Validate the required presenter/view references and sprite roles explicitly, including inactive authored views; do not depend on an inactive child's Awake having already run. Reject missing/incorrect required content with a useful development error rather than a fallback sprite or hierarchy search.

### Enraged Punch

- Start one repeating DOTween Sequence when the active living source becomes enraged, including Show on an already enraged boss. Append a DOPunchAnchorPos on _enragedFillMotion and then the configured rest interval; loop the outer Sequence with Restart. Nested punch tween has no infinite loop of its own.
- Animate only the enraged fill's motion wrapper vertically. Keep its background, normal fill, main background, and title stationary. This preserves HP width readability and prevents the entire HUD from shaking.
- The wrapper's authored baseline position is cached before motion. Killing a punch must restore that exact baseline immediately; a kill does not imply automatic transform restoration. Do not accumulate offsets or scale drift across loops or boss switches.
- Keep the punch independent from HP interpolation: additional damage does not restart the stress rhythm. Apply timing settings on the outer Sequence and keep it on scaled game time, consistent with GameTime.Pause/Resume. Pause freezes the fill transition and punch; resume continues them.
- Start/stop operations are idempotent. Enraged events arriving twice create only one Sequence. Stop on Hide, death, invalidation, source switch, view disable, presenter disable/destroy, and scene unload.
- Use the existing DOTween UI extension directly. TransformTweenExtensions has no matching punch helper, so do not introduce unrelated helper refactors. No new audio/VFX or changes to boss enrage sounds are planned.

## 7. Lifecycle, Edge Cases, and Performance

1. Presenter Awake validates both authored view references and their content, then hides Visual and both views. The presenter root stays active so Hide never disables its own service component.
2. Show detaches any old source and kills its HP tween/punch, resets both views, binds the new valid source, sets the title, subscribes to health/presentation events, and snapshots current state. Activate exactly the matching view only after the snapshot is applied.
3. Health callbacks read current source state and retarget the displayed ratio. Presentation callbacks read the same snapshot and idempotently update punch/state. A no-longer-active or dead source hides instead. Handle stale/in-flight callbacks from an old source with source identity or a binding generation check.
4. Hide removes OnHealthChanged, OnNoHealth, and OnPresentationChanged handlers, kills owned tweens without completing stale callbacks, restores view baselines, clears the binding, and deactivates both views and Visual. Hide is safe more than once.
5. Presenter/view OnDisable and OnDestroy clean up their respective subscriptions/tweens. For a disabled presenter, clear the binding; re-enabling it alone remains hidden until an explicit Show. Normal UI hiding only toggles Visual, not the presenter root.
6. Tower leash countdown keeps the HUD visible until the existing reset; a successful return leaves it bound. On actual reset the HUD hides before max-health restoration and enrage clearing. The next Show reads full health even though a max-heal event was not emitted.
7. Golem DeathVolume recovery retains health, phase, source identity, and the HUD. It does not restart punches or switch skins. Recovery is not encounter disengagement.
8. Single-HUD ownership remains last explicit Show wins; source replacement is deterministic. Do not add simultaneous boss bars or arena arbitration. Existing overlapping encounter coordination risks remain outside this visual migration.
9. No polling Update/FixedUpdate, LINQ, runtime layout creation, per-frame sprite search, forced layout rebuild, or per-frame managed allocation. Author both views once; retain at most one HP tween and one active punch Sequence. Tween setup may allocate at binding/retarget time; the specification does not claim whole-combat zero allocation without profiling. Reuse the HP tweener/delegates where practical; never recreate a Sequence on every punch cycle.
10. No spawned HUD objects or VFX need pooling. Existing boss projectile/telegraph pooling and gameplay timing remain untouched.

## 8. Implementation Plan: Phases and Atomic Steps

Every step must leave the project compiling and preserve a functional HUD. Runtime steps begin only after separate authorization. Do not finish a step with required references unassigned or a changed signature with unresolved callers.

### Phase 1: Approved Phase Migration and Source Contract

- [x] Step 1.1: Inventory all affected Golem/Tower configuration assets and serialized overrides; snapshot old Golem phase 2/3 values under .agents/context/tmp/. While the old fields still exist, copy former phase 3 values into retained phase 2 fields through Unity Editor and set both bosses' thresholds to 0.3. Then atomically remove Golem phase 3 fields/accessors/branches, make enrage commit phase 2, update all multiplier and barrage consumers, and update C# defaults. Save/reimport config assets through Unity Editor and confirm only two Golem phases remain exposed. The old HUD remains functional during this step.
  - Files: Assets/ScriptableObjects/Enemy/Bosses/Golem/GolemBossConfigSO.cs; Assets/ScriptableObjects/Enemy/Bosses/Golem/GolemBossConfig.asset; Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs; Assets/Scripts/Enemies/Bosses/Golem/StateMachine/States/GolemSkyBarrageState.cs; Assets/ScriptableObjects/Enemies/Bosses/MortarTowerConfigSO.cs; Assets/ScriptableObjects/Enemy/Towers/MortarTowerConfig.asset; any additional discovered config instances/overrides.
  - Verify: targeted compile, configuration value comparison before/after, no remaining runtime phase 3 consumers, no transition at 60% or Tower enrage at 40%, exactly one living transition at <= 30%, migrated phase 2 multipliers and five barrage cycles in the authored Golem asset, fresh-config defaults, unchanged phase 1 behavior and active-attack snapshot semantics. This step is complete only when code and saved configuration agree.

- [x] Step 1.2: Add IBossHUDSource/BossHealthBarKind and implement source projections/events in both bosses and their existing interfaces. Keep the existing HUD API and visual path operating while this contract is unused. Use the migrated Golem phase 2 threshold and preserve the approved combat order and configuration from Step 1.1.
  - Files: proposed Assets/Scripts/Enemies/Bosses/IBossHUDSource.cs; existing Assets/Scripts/Enemies/Bosses/Golem/IGolemBoss.cs; Assets/Scripts/Enemies/Bosses/Golem/GolemBoss.cs; Assets/Scripts/Enemies/Bosses/Towers/MortarTower/MortarTowerBoss.cs.
  - Verify: targeted compile; living threshold crossing for both bosses at 30%, Golem phase 2 enrage, no transition/event at 60%, reset, disable, lethal hit, and functional legacy HUD behavior.

### Phase 2: Author Both Visuals Without Cutover

- [x] Step 2.1: Add BossHealthBarView and author two initially inactive child layouts through Unity Editor under the existing Visual. Assign all semantic sprites, required references, aspect ratios, sibling order, Golem overlap, Tower join, punch baselines, and initial settings. The current presenter continues using its old Slider until cutover.
  - Files: proposed Assets/Scripts/UI/HUD/BossHealthBarView.cs; Assets/Scenes/RuinedBloodCity.unity saved through Unity Editor, with generated script .meta files managed by Unity.
  - Verify: targeted compile; Editor preview of each layout at normal/enraged fill amounts 1, 0.5, and 0; inspect seams and backgrounds. Verify a punch stays inside the chosen art's interior and restores its baseline. No permanent preview/scratch files outside .agents/context/tmp/.

### Phase 3: Atomic Presenter and Call-Site Cutover

- [x] Step 3.1: Replace Show/NotifyEncounterEngaged signatures and update all callers together; implement source snapshot handling, one-ratio health smoothing, source switch/deduplication, and punch activation/cleanup. Preserve optional null arena engagement semantics and existing encounter order.
  - Files: Assets/Scripts/UI/HUD/BossHUDPresenter.cs; Assets/Scripts/Enemies/Bosses/BossEncounterService.cs; Assets/Scripts/Enemies/Bosses/Towers/Arena/EncounterArenaController.cs.
  - Within the same step, wire _towerHealthBar and _golemHealthBar through Unity Editor, retain _visual/_bossTitleText, retire the old Slider/fill/background hierarchy and three old visual fields, and clear unused serialized keys by saving the scene. Do not leave two active render paths. Preserve the retained presenter script GUID and installer reference.
  - Verify: targeted compile; search every Show/NotifyEncounterEngaged caller and scene reference; engage Tower then Golem, cross both thresholds, inflict a large nonlethal crossing hit and a direct lethal hit, reset/re-engage Tower, and reload the scene. Confirm no missing script/sprite/reference and no duplicate listeners/tweens.

### Phase 4: Focused Verification and Documentation

- [x] Step 4.1: Add focused regression tests for the two-phase configuration/consumers, phase-range mapping, and source/subscription lifecycle using the existing Editor NUnit test pattern. Cover meaningful cases from section 9, including no Golem intermediate phase, both 30% thresholds, migrated barrage settings, reversed health/presentation callback order, and source replacement. Prefer testing the presenter/view through a controllable source and tween advancement; do not add a separate production service just to host an equation or tests.
  - Proposed file: Assets/Scripts/Editor/Tests/BossHUDPresenterTests.cs.
  - Verify: targeted compile plus relevant Unity Editor assembly compilation; run the new Editor tests. The runtime csproj compile alone does not validate the Editor tests.
- [ ] Step 4.2 (partially verified; see implementation summary for completed smoke checks and remaining full-combat/Profiler review): Complete the Play Mode matrix and visual checks below, inspect the active tween count and profiler for per-frame allocation after initialization, and check the final diff for unintended gameplay/serialized changes. No project-wide audit is needed for this scope.
- [x] Step 4.3: Update the relevant Boss HUD portions of .agents/context/game-systems/golem-boss-system.md, .agents/context/game-systems/tower-boss-system.md, and .agents/context/game-systems/ui-system.md after implementation. Record actual implementation and validation in .agents/context/implementations/summaries/boss-contextual-health-bar-summary.md. Do not claim runtime completion in this specification-delivery summary.

Targeted runtime compile after each C# step:

```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

Compilation must finish with zero errors/warnings. If validation finds existing unrelated failures, record them explicitly; do not hide them or expand cleanup without authorization.

## 9. Verification and Acceptance Criteria

| Check | Acceptance |
| --- | --- |
| Type and title | Each Tower uses only Tower sprites; Golem uses only Golem sprites. Display names remain correct even when renamed in designer data. Exactly one layout is active. |
| Full bar | Both fills completely cover their intended backgrounds, preserving intentional alpha details. Main background/crest and title are legible. |
| Normal damage | Only the normal HP range drains; enraged fill remains full and motionless before actual enrage. |
| Threshold values | Both configs and their new-config defaults use 0.3. Both bosses enter enrage/stress presentation at <= 30%; Tower at 40% and Golem at 60% remain normal. Equality uses the existing <= combat rule. |
| Golem phase count | Only normal phase 1 and enraged phase 2 exist in code and Inspector. No intermediate transition or phase 3 setting/accessor/consumer remains. Healing retains phase 2 once latched. |
| Migrated Golem settings | The authored asset retains baseline values and uses cooldown 0.4, movement 1.35, arm speed 1.6, and five barrage cycles in phase 2. New-config defaults use the former phase 3 defaults, including three barrage cycles. No active barrage restarts at transition. |
| Changed config | A temporary in-memory/test threshold different from the default changes fill mapping and transition consistently; the UI has no hard-coded threshold. Committed Tower/Golem configuration values remain aligned at 0.3. |
| Skipped threshold | A living hit from h > t to h < t animates across the seam and ends at h/t for the enraged fill. Normal ends empty, and only one punch loop runs. |
| Lethal hit | Zero HP hides immediately, even from full health. It never starts a final enrage animation or delays defeat/portal/swarm events. |
| Golem join | No unintended transparent slit, double-height edge, or background painted over a full fill at the angled join, including near threshold and during punch. |
| Tower join | Adjacent segments show no source-sheet gap and require no deliberate overlap. Stepped edge alignment does not obscure either segment. |
| Background reveal | Red/textured backgrounds remain stationary and become visible exactly where white fills drain. Main artwork is not the damage trail. |
| Rapid damage | Retargeting has no backlog or HP increase artifact. Source events do not restart an existing enrage loop. |
| Reversed callback order | Enrage committed before or after the presenter's health callback reaches the same result without premature enraged drain. |
| Show while enraged | Correct skin, immediate current HP, and one active punch; no full-health flash. |
| Source replacement | Tower -> Golem -> Tower removes old source listeners, clears interpolation, restores old offsets, and never lets an old source update the new bar. |
| Leash | Temporary departure retains presentation; reset hides, cancels punch, and re-entry restores full normal state from a fresh snapshot. |
| Pause/death | Pausing freezes both animations; resume continues. Player death retains existing HUD visibility policy while scaled time freezes; boss death still hides. No new player-death subscription is added. |
| Disable/reload/recovery | Source disable, presenter disable, destruction, and scene unload leave no callbacks/tweens. Repeated Hide is harmless; Golem recovery retains the same visible encounter. |
| Healing/config failures | Enrage latch survives healing. Current max HP is used. Invalid thresholds/missing required layout data fail visibly; no silent wrong-skin fallback. |
| Inspector and input | Retained references survive; newly required ones are assigned. All bar graphics/title have raycasts off; other HUD interaction works. |
| Scale and performance | No distortion, clipping, title/crest collision, or seam leakage at the tested aspect ratios. No new per-frame managed allocation after setup; no hidden view punches. |

Automate mapping/lifecycle cases where practical. Visual seam quality, punch intensity, title placement, texture sharpness, and resolution scaling require actual Unity Editor Play Mode review; passing C# compilation is insufficient.

## 10. Architectural Self-Review

| Severity | Risk | Required mitigation |
| --- | --- | --- |
| Blocker | HUD reads a removed phase 3 threshold or Golem retains an intermediate phase. | Migrate all config/runtime consumers atomically; map Golem enrage to phase 2 and both authored thresholds to 0.3. |
| Blocker | Phase 3 fields are removed before their authored values are transferred, losing the intended enraged tuning. | Snapshot every config first, copy its own values into retained phase 2 fields through Editor, then remove obsolete fields and verify saved assets. Preserve five authored barrage cycles versus three in new-config defaults. |
| Blocker | Mixed old/new HUD signatures, removed fields with missing scene wiring, or unresolved sprite references at cutover. | Atomic call-site/scene migration, preserved presenter identity, explicit validation, compile and Editor reference review. |
| Major | Health callback sees stale enrage state or receives duplicate presentation updates. | Committed owner notification plus fresh snapshot/deduplication; test both event orders. |
| Major | Golem slanted seam, Tower join, or moving fill leaks outside artwork. | Separate authored geometry, correct draw order, bounded vertical punch, threshold and aspect-ratio visual checks. |
| Major | Old source/tween survives Hide, reset, disable, or switch. | Symmetric subscription ownership, idempotent cleanup, binding identity, baseline restoration, repeated lifecycle checks. |
| Minor | Sprite width does not exactly equal its represented HP fraction. | Document phase-based mapping; preserve artwork and the approved common 30% threshold without additional rebalance or segment stretching. |
| Minor | 2048 import limit softens a 2560-wide source. | Inspect at gameplay scale; treat quality changes as a separate reviewed asset adjustment if needed. |
| Nit | Legacy names such as MortalTower or misspelled file names tempt unrelated cleanup. | Use current paths and preserve identifiers outside the touched presentation scope. |

Standards check: explicit dependencies; existing Reflex bindings; shared narrow contract; private _camelCase serialized fields; Inject -> SerializeField -> private ordering; PascalCase members and OnX events; block-bodied methods; no LINQ; no singleton/find fallback; no serialized renames. Any new reusable constants belong under the owning system's Constants directory; visual tuning belongs in the Inspector.

## 11. References and Specification Validation

Operational sources: AGENTS.md; .agents/README.md; .agents/context/project-coding-standards.md; .agents/context/ai-game-dev-best-practices.md; .agents/context/technology-documentation.md; ADR-001/002/004/005 under .agents/context/adr/; .agents/context/game-systems/ui-system.md; .agents/context/game-systems/health-system.md; .agents/context/game-systems/golem-boss-system.md; .agents/context/game-systems/tower-boss-system.md; .agents/context/brainstorming-summaries/golem-boss-brainstorm-summary.md; .agents/context/brainstorming-summaries/mortar-tower-miniboss-brainstorm-summary.md. Existing plans were checked for conflicts; no previous canonical boss HUD replacement plan was found.

Official framework references consulted on 2026-10-17:

- [Unity uGUI Image](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-Image.html) and [visual component types](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/UIVisualComponents.html): Simple/Filled rendering and image configuration.
- [Unity horizontal fill API](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/api/UnityEngine.UI.Image.FillMethod.html): horizontal fill support.
- [DOTween documentation](https://dotween.demigiant.com/documentation.php): anchored punch and outer Sequence lifecycle/time settings. The installed UI module was also checked for DOPunchAnchorPos availability.
- [Reflex official repository](https://github.com/gustavopsantos/Reflex): DI reference. Current repository examples differ from the project's pinned registration API; preserve the inspected 12.0.1 installer usage and introduce no registration changes.

Completed for this documentation deliverable: source/caller/installer/scene inspection, both artwork previews, slice metadata inspection, threshold verification against authored assets, source-reference checks, phase equations checked against acceptance examples, and standards/lifecycle self-review. The user's subsequent 30%/two-phase revision was checked against Golem phase accessors, multiplier consumers, barrage cycle selection, and authored values versus C# defaults. No runtime code, Unity asset, scene, importer, or package was modified. Runtime compilation, Editor tests, Play Mode, screenshots, and profiling remain future implementation validation.
