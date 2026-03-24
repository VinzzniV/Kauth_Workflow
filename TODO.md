# TODO.md

## AI Rules

Before any task:
- read `PROJECT_CONTEXT.md` and `DECISIONS.md`
- summarize key rules
- list affected files before changing anything
- change only what is required
- avoid unrelated refactors
- verify outcomes instead of assuming success
- keep backend as source of truth
- do not reintroduce `skipped`

For every task:
- show what you checked
- show verification via commands or clear reasoning
- do not silently change business semantics

---
## Must Fix

### [M1] Newly added user doesent show up in Demo Auth

**Goal:** Newly Added Users should show up in Demo Auth when added via "Verwaltung"

**Note:**  This is only for Demo Purposes and Demo Auth gets substituted by Normal Auth via Azure AD or similar

---

## 🟡 FUTURE

### [F1] Audit trail improvements

**Goal:** Better visibility into who did what and when.

**Reasoning Effort:** High
**Tool:** Claude + Codex

---

### [F2] Improve task comments UX

**Reasoning Effort:** Medium
**Tool:** Codex

---

### [F3] Improve due dates and SLA handling

**Reasoning Effort:** Medium
**Tool:** Codex

---

### [F4] Clean up notification system

**Reasoning Effort:** High
**Tool:** Claude + Codex

---

### [F5] Define a DB migration strategy

**Reasoning Effort:** High
**Tool:** Claude

---

### [F6] Add real authentication

**Constraint:** late only.

**Reasoning Effort:** Very High
**Tool:** Claude

---

### [F7] Expand from onboarding to employee lifecycle

**Direction:** onboarding to broader platform support.

**Reasoning Effort:** High
**Tool:** Claude

---

### [F8] Build admin configuration system

**Note:** big step.

**Reasoning Effort:** Very High
**Tool:** Claude

---

### [F9] Define data lifecycle handling

**Direction:** archive vs. delete.

**Reasoning Effort:** High
**Tool:** Claude

---

### [F10] Add a visual dependency editor

**Reasoning Effort:** Very High
**Tool:** Claude
