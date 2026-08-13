# Navigation Coding Standards Alignment Summary

**Date**: 2026-08-13

## Summary
Przeprowadzono pełny audyt oraz dostosowanie kodu w module Navigation (Assets/Scripts/Navigation/) do standardów projektowych (ProjectLizard Coding Standards).

## Files Changed
- Assets/Scripts/Navigation/Constants/GridConstants.cs: dodano stałą OCCUPANCY_BUFFER_SIZE.
- Assets/Scripts/Navigation/Constants/FlowFieldConstants.cs: dodano stałą EDGES_OFFSET.
- Assets/Scripts/Navigation/FlowFieldSystem/FlowField.cs: zastąpiono lokalną zmienną stałą FlowFieldConstants.EDGES_OFFSET.
- Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldDebug.cs: usunięto atrybuty FormerlySerializedAs oraz nieużywany import UnityEngine.Serialization.
- Assets/Scripts/Navigation/FlowFieldSystem/FlowFieldMovementController.cs: usunięto atrybuty FormerlySerializedAs oraz nieużywany import UnityEngine.Serialization.
- Assets/Scripts/Navigation/GridSystem/Cell.cs: uproszczono warunek IncreaseCost.
- Assets/Scripts/Navigation/GridSystem/GridDirection.cs: zamieniono publiczne pole Vector na właściwość auto-property Vector { get; }.
- Assets/Scripts/Navigation/GridSystem/GridCellsNotVisibleByMainCamera.cs: użyto GridConstants.OCCUPANCY_BUFFER_SIZE i sformatowano instrukcje if do bloków {}.
- Assets/Scripts/Navigation/GridSystem/GridEdgeHelper.cs: sformatowano instrukcje if do czytelnych bloków wielolinijkowych.
- Assets/Scripts/Navigation/GridSystem/Grid.cs: zastąpiono var jawnym typem Vector2Int.
- Assets/Scripts/Navigation/GridSystem/GridManager.cs: uporządkowano strukturę składowych (Inject -> SerializeField -> private fields/constants -> properties -> lifecycle -> methods) oraz zdefiniowano stałe debugowania.

## Verification
- Projekt skompilowany pomyślnie za pomocą `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false` (0 błędów, 0 ostrzeżeń w zmodyfikowanym kodzie).
