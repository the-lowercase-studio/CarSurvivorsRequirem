# Root Cause Report: [Bug / Issue Summary]

**Date:** [YYYY-MM-DD]

**Status:** [Confirmed cause | Working hypothesis | Insufficient evidence] (Read-Only)

**Defect Category:** [C# Logic | Reflex DI | Unity Lifecycle | Pooling Leak | Asset/Serialization | Event Leak]

---

## 1. Symptom & Reproduction
- **Observed Behavior:** What happens vs. what was expected.
- **Error / Stack Trace:** (If available)
- **Reproduction Conditions:** When and how the defect triggers.

---

## 2. Root Cause Analysis
- **Defect Location:** Assets/Scripts/... (Class, method, line range)
- **Mechanism of Failure:** Explain the causal path with evidence (e.g. lifecycle ordering, missing required binding, dirty pooled state). Preserve fail-fast behavior instead of masking a required dependency with a null guard.
- **Domain Boundaries Involved:** How the defect crosses systems (e.g. Event triggered in Enemy passing invalid data to DamageNumbers).

---

## 3. Minimal Proposed Fix
- **Proposed Approach:** The smallest, safest change that fixes the defect without side effects.
- **Target Files to Modify:**
  - Assets/Scripts/...
- **Concrete Code Snippet / Fix Concept:**
  ```csharp
  // Suggested fix snippet
  ```

---

## 4. Regression Risks & Verification Plan

The steps below are proposed post-fix checks. They do not certify an unimplemented fix.
- **Potential Side Effects:** Any related systems that could be affected by this fix.
- **Verification Steps:**
  1. Automated Compilation: `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`
  2. Unity Editor Playmode Verification: Step-by-step instructions to verify the fix in the scene.

## 5. Investigation Checks Actually Performed

- Inspected evidence / executed command and result:
- Reproduction performed: [Yes with evidence | No]
- Baseline failures, unavailable checks, and remaining hypotheses:
