# Pre-Commit Gate Checklist

Date: [YYYY-MM-DD]
Changed scope: [Files / domains]
Mode: [Verification only | Authorized scoped repairs]
Verdict: [PASS | FAIL | PENDING]

## Scope

- Files inspected and authorized untracked additions:
- Pre-existing user changes:
- Repair authority, if applicable:

## Gate Results

| Gate | Status | Evidence / reason |
| --- | --- | --- |
| 1. Compilation and warnings | [PASS / FAIL / PENDING / N/A] | [Executed command, exit code, errors, warnings; no prefilled success] |
| 2. Serialized data | [PASS / FAIL / PENDING / N/A] | [Canonical permitted inspector patterns, identities, migration authority/checks] |
| 3. DI and field order | [PASS / FAIL / PENDING / N/A] | [Inspected bindings/lifetimes; pending live checks] |
| 4. Standards and architecture | [PASS / FAIL / PENDING / N/A] | [Changed-scope evidence; source risks versus measured allocations] |
| 5. Git and assets | [PASS / FAIL / PENDING / N/A] | [Touched files accounted for, user changes protected, metadata/references checked] |
| 6. Lifecycle artifacts | [PASS / FAIL / PENDING / N/A] | [Durable plan, requirements state, summary paths] |

N/A requires a scope-based reason. Unavailable checks are PENDING. Overall PASS requires every applicable gate to pass.

## Findings and Repairs

- Findings reported without edits:
- Authorized corrections actually applied:
- Baseline versus introduced diagnostics:
- Remaining blockers and missing authority:

## Next Actions

- [ ] All applicable gates passed with evidence.
- [ ] Required Unity Editor/manual checks completed, or explicitly pending.
- Commit/merge authority and action taken: [Not requested / Authorized / Executed with reference]
