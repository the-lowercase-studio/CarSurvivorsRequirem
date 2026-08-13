# ADR-002: UI Animation and Motion Standardization via DOTween

- Status: Accepted
- Date: 2026-08-12
- Decision Makers: Game Development Team & AI Agents

## Context

UI views, damage numbers, feedback animations, and temporary visual effects require smooth animations (scaling, fading, movement, punching). Writing custom coroutines or Update-loop interpolation logic creates unnecessary boilerplate, garbage allocations, and cleanup bugs when GameObjects are disabled or destroyed mid-animation.

## Decision

We standardize UI animations and transform motion using **DOTween** and project extension helpers:

1. **Transform Extensions:** Reuse existing extension methods under `Assets/Scripts/Extensions/TransformTweenExtensions.cs` for common UI and object tweens (pop-in, scale bounce, smooth fade, movement).
2. **Lifecycle Safety:** Always kill or complete running tweens when components are disabled or destroyed (`DOKill()`) to avoid operating on destroyed Unity GameObjects.
3. **No Garbage Update Loops:** Avoid manual `Time.deltaTime` interpolation loops in `Update()` for UI transitions and visual effects when a tween sequence can handle it.

## Consequences

### Positive
- Consistent animation visual feel and easing curves across all UI windows and HUD elements.
- Clean unsubscription and garbage-free tween pooling managed by DOTween.
- Simplifies UI presenter code by replacing multiline Update logic with expressive single-line extension calls.

### Negative / Trade-offs
- Developers and agents must remember to call `.DOKill()` in `OnDisable()` / `OnDestroy()` to prevent tween leak warnings.
