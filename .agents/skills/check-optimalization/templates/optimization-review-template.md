# Performance & Optimization Review Report

**Target Scope / System:** [System / File Name]  
**Date:** [YYYY-MM-DD]  
**Overall Performance Health:** [CRITICAL | WARNING | GOOD | EXCELLENT]  

---

## 1. Hot-Path & Scope Audit
- **Files Inspected:**
  - `Assets/Scripts/...`
- **Execution Context:** (e.g. `Update` loop, Physics tick, Spawner cycle, Combat event)

---

## 2. Performance Findings by Severity

### 🔴 Blocker (Hot-Path GC Allocations / Search Shortcuts / Leaks)
- [None / Finding: File, line, issue (e.g. LINQ in Update), and concrete zero-allocation replacement]

### 🟠 Major (Scale Bottlenecks / Missing Pooling / Canvas Rebuilds)
- [None / Finding: File, line, issue (e.g. Instantiate without pool), and pooling integration fix]

### 🟡 Minor (Math & Algorithmic Inefficiencies)
- [None / Finding: File, line, issue (e.g. sqrMagnitude vs Distance), and fix]

### ⚪ Nit (Micro-Optimizations)
- [None / Finding: File, line, suggestion]

---

## 3. Preserved Invariants & Safety
- [x] Reflex DI boundaries preserved (no static singletons introduced).
- [x] Gameplay determinism and event order unchanged.
- [x] Serialized fields and inspector authoring intact.

---

## 4. Verification & Implementation Approval
- **Compilation Check:** `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false` [PASS / FAIL]
- **Approval Request:**
  > I can implement these proposed optimizations: [list]. Do you approve all items, or only specific ones?
