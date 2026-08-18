# Implementation Summary - [Feature or Task Name]

Date: YYYY-MM-DD

## Overview

[Brief summary of what was accomplished, why it was done, and the final state of the implementation.]

## Key Changes

### [Component / Domain 1]
- Assets/Scripts/[Path/To/File.cs]: [Key changes made and rationale.]

### [Component / Domain 2]
- Assets/Scripts/[Path/To/File.cs]: [Key changes made and rationale.]

## Documentation & Standards

- Implementation Plan: .agents/context/implementations/plans/[plan-name].md
- Coding Standards: Verified compliance with .agents/context/project-coding-standards.md (naming conventions, LINQ ban, block syntax, field ordering).

## Verification Performed

### Automated Tests & Compilation
- Clean build verified:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- Status: Build succeeded with 0 errors and 0 warnings.

### Manual Verification
- [List of manual test steps executed in Unity Editor play mode or inspector.]
- [Observations and confirmed behavior.]

## Follow-up / Unity Editor Steps

1. [Any inspector assignments, prefab connections, or asset configurations needed in the editor.]
2. [Or state "No additional manual inspector setup required."]
