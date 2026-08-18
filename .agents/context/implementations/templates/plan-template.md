# Implementation Plan - [Feature or Task Name]

Date: YYYY-MM-DD

[Brief description of the problem, background context, and what the change accomplishes.]

## User Review Required

> [!IMPORTANT]
> [Critical architectural decisions, breaking changes, or user-visible behaviors requiring explicit user confirmation before proceeding.]

## Open Questions

- [Question 1 for the user regarding balance, data ownership, or UI flow]
- [Question 2, or state "None."]

## Proposed Changes

### [Component / Feature Area 1]

#### [NEW] Assets/Scripts/[Path/To/NewFile.cs]
- [Description of new file, interface, or class responsibilities]

#### [MODIFY] Assets/Scripts/[Path/To/ModifiedFile.cs]
- [Description of modifications, method signatures, or field changes]

#### [DELETE] Assets/Scripts/[Path/To/DeletedFile.cs]
- [Description of rationale for removal]

---

### [Component / Feature Area 2]

#### [MODIFY] Assets/Scripts/[Path/To/AnotherFile.cs]
- [Description of modifications]

---

## Verification Plan

### Automated Checks
- Project compilation check:
```powershell
dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false
```
- [Any additional automated tests or architecture linting scripts]

### Manual Verification
1. [Step 1 for play mode or editor verification]
2. [Step 2 verifying specific edge cases, inspector bindings, or UI flows]
