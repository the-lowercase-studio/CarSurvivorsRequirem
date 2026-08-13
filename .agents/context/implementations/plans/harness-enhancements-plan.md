# Implementation Plan - Agent Harness Enhancements (ADRs, .editorconfig, Architecture Linter)

Date: 2026-08-12

## Goal
Implement three core enhancements to the project harness for AI agents:
1. Rejestr Decyzji Architektonicznych (ADR) under `.agents/context/adr/`.
2. Uniform C# code style enforcement via root `.editorconfig`.
3. Automated static architecture linter for Unity/C# via `.agents/scripts/check-architecture.ps1`.

## Changes

### 1. ADRs (`.agents/context/adr/`)
- `ADR-001-reflex-di-architecture.md`: Explicit DI via Reflex vs singletons and scene lookups.
- `ADR-002-dotween-and-transform-extensions.md`: DOTween transform extensions for UI/animations.
- `ADR-003-flowfield-navigation-and-grid.md`: FlowField grid performance and job/memory allocation model.
- `ADR-004-designer-authored-data-and-prefabs.md`: Inspector data safety and ScriptableObject workflows.

### 2. `.editorconfig`
- Root `.editorconfig` setting `_camelCase` for private fields, `PascalCase` for public members, `UPPER_SNAKE_CASE` for constants, and C# warning diagnostics.

### 3. Architecture Linter (`.agents/scripts/check-architecture.ps1`)
- PowerShell script scanning `Assets/Scripts/` for `FindObjectOfType`, `FindAnyObjectByType`, `GameObject.Find`, static singletons, `DontDestroyOnLoad`, public mutable fields, and event leak risks.

## Verification
- Run `powershell -ExecutionPolicy Bypass -File .agents/scripts/check-architecture.ps1`
- Run `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`
