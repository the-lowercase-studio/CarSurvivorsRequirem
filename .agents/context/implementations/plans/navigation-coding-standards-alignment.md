# Navigation Coding Standards Alignment Plan

**Date**: 2026-08-13

## Summary
Audyt i dostosowanie kodu w module Navigation (Assets/Scripts/Navigation/) do wytycznych określonych w .agents/context/project-coding-standards.md.

## Scope
Pliki w Assets/Scripts/Navigation/:
- Assets/Scripts/Navigation/Constants/GridConstants.cs
- Assets/Scripts/Navigation/Constants/FlowFieldConstants.cs
- Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs
- Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldDebug.cs
- Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs
- Assets/Scripts/Navigation/GridSystem/Cell.cs
- Assets/Scripts/Navigation/GridSystem/GridDirection.cs
- Assets/Scripts/Navigation/GridSystem/GridCellsNotVisibleByMainCamera.cs
- Assets/Scripts/Navigation/GridSystem/GridEdgeHelper.cs
- Assets/Scripts/Navigation/GridSystem/GridManager.cs
- Assets/Scripts/Navigation/GridSystem/Grid.cs

## Actions
1. Dodanie stałych do GridConstants i FlowFieldConstants (eliminacja magic numbers).
2. Usunięcie FormerlySerializedAs oraz nieużywanych importów UnityEngine.Serialization.
3. Poprawa właściwości GridDirection.Vector z publicznego pola na auto-property { get; }.
4. Ujednolicenie składni blokowej {} dla instrukcji warunkowych w GridCellsNotVisibleByMainCamera oraz GridEdgeHelper.
5. Uporządkowanie kolejności składowych (Inject -> SerializeField -> private fields -> properties -> lifecycle -> methods) w GridManager.
