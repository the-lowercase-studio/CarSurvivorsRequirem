# ADR-005: Fail-Fast & Explicit Dependency Null-Check Policy

- Status: Accepted
- Date: 2026-08-31
- Decision Makers: Game Development Team & AI Agents

## Context

In Unity development, scripts frequently employ defensive coding patterns to avoid `NullReferenceException`, such as:
1. Fallback hierarchy searches in `Awake()` (e.g., `if (_field == null) _field = GetComponentInChildren<T>();`).
2. Silent null-guards in `Awake()` (e.g., `if (_visual != null) _visual.SetActive(false);`).
3. Downstream null-conditional calls or early returns in gameplay loops (e.g., `_animator?.Play(...)` or `if (_hitbox == null) return;`).

In `Car Survivors`, these defensive patterns masked broken prefab connections, deleted hierarchy nodes, and missing inspector bindings. Systems failed silently or operated in degraded states (e.g. enemies moving in T-pose without animations, attacks dealing zero damage without warnings, missing UI presenters). Furthermore, fallback searches like `GetComponentInChildren` introduced hidden execution costs and fragile assumptions about child GameObject order.

## Decision

We establish a strict **Fail-Fast & Explicit Dependency Policy** across the codebase:

1. **No Defensive Fallback Searches for Serialized Fields:**
   - Serialized dependencies (`[SerializeField] private T _field;`) authored on prefabs or scene instances must be wired in the inspector. Fallback queries in `Awake()` (such as `GetComponentInChildren` or `transform.GetChild(0)`) are prohibited.
   - For mandatory same-GameObject components, use `[RequireComponent(typeof(T))]` and assign directly in `Awake()` without null checks (`_field = GetComponent<T>();`).

2. **No Silent Awake Null-Guards for Required Mechanical Dependencies:**
   - Mechanical initialization must execute directly in `Awake()`. Unassigned required fields must throw immediately or fail fast on access rather than silently skipping setup.

3. **No Downstream Silent Null-Swallowing for Required Mechanical Services:**
   - Core gameplay dependencies (animators, hitboxes, combat triggers, projectiles, configs) must be invoked directly (`_animator.Play(...)`, `_hitbox.Activate()`). Using `?.` or early returns to swallow unassigned mechanical references is prohibited.

4. **Strict Configuration Validation:**
   - Critical configuration masks (e.g. `_groundLayerMask` in `CarController`) must validate in `Awake()`, throwing an explicit `InvalidOperationException` if left unassigned (`mask.value == 0`), rather than silently falling back to defaults.

5. **Authorized Exceptions:**
   Null checks in `Awake()` and runtime execution are permitted strictly in these three scenarios:
   - **Cosmetic & Sensory Components (VFX & Audio):** Components like `VFXPlayer`, `IAudioClipPlayer`, particle systems, and trail renderers are allowed to be optional and guarded with `if (_vfxPlayer != null)` or `_audioClipPlayer?.PlayOneShot(...)` so missing sensory polish does not break core gameplay mechanics.
   - **Genuinely Optional Visual / Polish Elements:** Secondary visual indicators designed explicitly as optional polish (e.g. optional deactivation visual sets).
   - **DI Dependency with Optional Inspector Override:** When a service is injected via Reflex DI (`[Inject] private readonly IService _service;`) but allows an inspector override (`[SerializeField] private CustomService _inspectorOverride;`), checking `if (_inspectorOverride != null)` to override the DI service is permitted.

## Consequences

### Positive
- Broken prefab connections, missing bindings, and deleted nodes fail loudly and immediately in development.
- Eliminates silent degraded states (e.g., animations failing silently, attacks failing to register).
- Eliminates unnecessary hierarchy traversal overhead in `Awake()`.
- Explicit, unambiguous code paths without defensive clutter.

### Negative / Trade-offs
- Missing inspector references will trigger immediate runtime errors (`NullReferenceException` or assertion) during playmode, requiring developers/designers to properly assign fields on prefabs.
