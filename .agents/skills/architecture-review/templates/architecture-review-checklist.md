# Architecture Review Report

**Target System / Feature:** [System / Feature Name]  
**Date:** [YYYY-MM-DD]  
**Verdict:** [APPROVE | REQUEST CHANGES]  

---

## 1. Scope & Touched Files
- Files Audited:
  - Assets/Scripts/...

---

## 2. Validation & Compilation Gate
- `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`: [PASS / FAIL] (Exit code, warnings count)

---

## 3. Unity Breaking Change Assessment
- [ ] **Serialized Data Safety:** (No unmapped field renames, inspector compatibility intact)
- [ ] **Reflex DI Wiring:** (All `[Inject]` dependencies bound in installers under Assets/Scripts/ReflexDI/)
- [ ] **Lifecycle & Invariants:** (Event order, gameplay flow, death flow, pooling safety preserved)

---

## 4. Findings by Severity

### 🔴 Blocker (Must fix before merge/completion)
- [None / Finding list with file, line, rationale, and concrete fix]

### 🟠 Major (Architecture or lifecycle flaws)
- [None / Finding list with file, line, rationale, and concrete fix]

### 🟡 Minor (Standards & non-blocking structural drift)
- [None / Finding list with file, line, rationale, and concrete fix]

### ⚪ Nit (Stylistic suggestions)
- [None / Finding list with file, line, rationale, and concrete fix]

---

## 5. Summary & Next Actions
- Summary of architecture health:
- Required follow-up actions:
