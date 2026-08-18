# Pre-Commit Gate Checklist

**Date:** [YYYY-MM-DD]  
**Branch / Scope:** [Scope / Files]  
**Verdict:** [PASS | FAIL]  

---

## 1. Scope & Changed Files
- Files Audited:
  - `Assets/Scripts/...`

---

## 2. Gate Results

| Gate | Status | Details |
| :--- | :---: | :--- |
| **1. Compilation & Warnings** | [PASS / FAIL] | `dotnet build` exit code 0, 0 errors, 0 warnings. |
| **2. Serialized Data Safety** | [PASS / FAIL] | `[SerializeField] private` used, `_camelCase` names, no broken inspector fields. |
| **3. Reflex DI & Field Order** | [PASS / FAIL] | Order: `[Inject]` -> `[SerializeField]` -> private. All bindings registered in installers. |
| **4. Coding Standards** | [PASS / FAIL] | Constants in `Constants/`, `OnX` events, no GC allocations in hot loops, no singletons. |
| **5. Git & Asset Consistency** | [PASS / FAIL] | Clean worktree in touched scope, no orphaned meta files. |

---

## 3. Findings & Remediations Applied
- Fixed issues:
  - (e.g. Fixed field ordering in `...`, corrected warning in `...`)
- Issues requiring user confirmation (if any):
  - (None / listed items)

---

## 4. Final Verdict & Next Actions
- [ ] Ready to commit / merge.
- [ ] Remaining Unity Editor manual checks needed:
