# Implementation Plan - Boss HUD Child Activation

Date: 2026-08-23

Refactor `BossHUDPresenter` to toggle visibility by enabling/disabling a child container GameObject (`_contentRoot`) rather than fading/toggling a `CanvasGroup`. The parent GameObject with `BossHUDPresenter` stays active at all times in the scene.

## User Review Required

> [!NOTE]
> The parent GameObject with `BossHUDPresenter` remains permanently active. When creating or configuring the Boss HUD UI in the Unity Hierarchy, ensure all visual components (slider, text, background, icons) are placed inside a child GameObject (e.g. `Content` or `Container`) and assigned to the `_contentRoot` field.

## Open Questions

- None.

## Proposed Changes

### UI / HUD

#### [MODIFY] Assets/Scripts/UI/HUD/BossHUDPresenter.cs
- Remove `CanvasGroup _canvasGroup`, `_fadeInDuration`, `_fadeOutDuration`, and `_fadeTween`.
- Add `[SerializeField] private GameObject _contentRoot`.
- In `Awake()`, if `_contentRoot` is null and `transform.childCount > 0`, resolve `transform.GetChild(0).gameObject` as fallback, then set `_contentRoot.SetActive(false)`.
- In `Show()`, activate `_contentRoot` immediately via `_contentRoot.SetActive(true)`, bind health slider and title text, and subscribe to health events.
- In `Hide()`, deactivate `_contentRoot` via `_contentRoot.SetActive(false)`, unsubscribe from health events, and kill active slider tweens.

---

## Verification Plan

### Automated Checks
- Project compilation check:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```

### Manual Verification
1. Verify in Unity Editor that the `BossHUDPresenter` parent GameObject can stay active in the Canvas hierarchy.
2. Verify that assigning the child container to `_contentRoot` (or relying on the first child fallback) keeps the HUD hidden on start.
3. Trigger boss spawn (or call `Show(...)`) and verify the HUD child is activated and reflects boss health/name.
4. Defeat the boss (or call `Hide()`) and verify the HUD child is deactivated.
