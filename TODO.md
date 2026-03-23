# TODO.md

## Working Rules for AI

Before starting any task, always read:

1. `PROJECT_CONTEXT.md`
2. `DECISIONS.md`
3. `TODO.md`

Then summarize these rules before changing code:

- Roles and Responsibilities must stay strictly separated.
- Backend is the source of truth for workflow/task business logic.
- `assignment_type` must remain strict:
  - `user` = only the assigned user
  - `responsibility` = responsible area
- Tasks must not leak to the wrong users or responsibilities.
- A workflow must not be completed too early.
- Generated tasks that are relevant to a workflow must be treated as workflow-relevant.
- Do not introduce broad refactors unless explicitly requested.
- Fix one task at a time.
- Prefer minimal, targeted changes.
- Keep demo/prototype pragmatism, but do not weaken core business correctness.
- When in doubt, preserve backend truth over frontend-derived behavior.

For every task:
- first list the files you plan to change
- keep the change set small
- avoid unrelated renames/reformats
- explain the reasoning briefly
- run review after implementation
- do not silently change business semantics

---

## Legend

### Priority
- 🔴 Must
- 🟠 Should
- 🟡 Future / Tech Debt / Product Direction

### Reasoning Effort
- **Low** = Codex can usually solve directly
- **Medium** = Codex can solve, but review carefully
- **High** = Claude analysis is helpful first, then Codex implements
- **Very High** = do not let AI change this without a prior plan

---

# 🔴 MUST FIX BEFORE DEMO / HANDOFF

---

## [D1] Replace real notification email addresses in demo seed

Status: Open

### Problem
The demo seed still uses a real notification email address for demo users.

### Why it matters
If email sending is enabled or tested, demo actions could send real emails.

### Evidence
- `db/02_seed.sql`
- current value example:
  - `notification_email = 'vinzent.niederwieser@kauth.de'`

### Goal
Replace all real/demo-unsafe notification addresses with safe demo-only addresses.

### Required behavior after fix
- No demo seed record contains a real personal/company email address.
- Demo addresses are clearly fake and safe, for example:
  - `hr@demo.local`
  - `manager@demo.local`
  - `it@demo.local`
- No functional behavior depends on those addresses being real.

### Constraints
- Change only demo/seed data.
- Do not refactor mail sending.
- Do not change unrelated SQL.

### Suggested file scope
- `db/02_seed.sql`

### AI execution plan
1. Read context files.
2. Inspect `db/02_seed.sql`.
3. Replace unsafe addresses with safe demo-local addresses.
4. Verify no real notification emails remain in seed/demo data.

### Acceptance criteria
- `db/02_seed.sql` contains no real personal/company email addresses.
- Demo seed still works.
- No app logic changes.

### Reasoning effort
**Low**

### Best tool
**Codex**

---

# 🟡 FUTURE / IDEAS / PRODUCT DIRECTION

---

## [F5] Move toward a cleaner migration strategy

Status: Open

### Problem
Long-term database evolution should not rely only on sequential SQL resets/backfills.

### Goal
Move gradually toward a cleaner migration workflow appropriate for the project size.

### Constraints
- Do not overengineer too early
- Match the current prototype maturity

### Reasoning effort
**High**

### Best tool
**Claude for options, Codex for implementation**

---

## [F6] Introduce real authentication/identity later

Status: Open

### Goal
Move from demo-oriented auth toward real identity integration when the prototype is stable enough.

### Examples
- OIDC
- Entra ID
- AD/SSO integration

### Constraints
- Not needed before core workflow quality is stable
- Do not let auth complexity derail workflow correctness

### Reasoning effort
**Very High**

### Best tool
**Claude first**

---

## [F7] Generalize from onboarding tool to employee lifecycle platform

Status: Open

### Problem / Long-term direction
The project should not stay hardcoded around onboarding only.

### Goal
Evolve toward a platform that can support:
- onboarding
- offboarding
- employee change processes
  - name change
  - department change
  - responsibility change
  - role/access change
  - hardware/software change

### Constraints
- Do not force this generalization into small bugfixes
- Keep current onboarding behavior stable while evolving the model

### Acceptance criteria
- New future designs avoid unnecessary onboarding-only hardcoding
- Product direction is documented and reflected in future architecture choices

### Reasoning effort
**High**

### Best tool
**Claude for direction, Codex for incremental implementation**

---

## [F8] Build admin-safe configuration management via UI

Status: Open

### Problem / Long-term direction
Admins should eventually maintain important master/configuration data without direct database editing or code changes.

### Goal
Provide safe UI-based management for:
- persons/users
- responsibilities/departments
- workflow templates
- task templates
- dependencies
- assignment mappings
- notification settings
- process-specific configuration

### Constraints
- Do not expose raw database structure directly
- Do not allow unsafe free-form editing without validation
- Prefer domain-safe admin screens over generic CRUD for everything

### Acceptance criteria
- Admin workflows become less dependent on DB/code knowledge
- Config changes are understandable and validated
- Dangerous misconfiguration paths are reduced

### Reasoning effort
**Very High**

### Best tool
**Claude first**

---

## [F9] Introduce lifecycle-safe data management

Status: Open

### Problem / Long-term direction
Long-lived systems usually cannot hard-delete everything safely once workflows reference those records.

### Goal
Move toward safer data lifecycle handling where appropriate:
- active/inactive
- archived
- soft delete
- valid-from / valid-to where needed

### Constraints
- Do not turn every entity into a heavy archival system immediately
- Apply where historical workflow integrity requires it

### Reasoning effort
**High**

### Best tool
**Claude for design, Codex for focused implementation**

---

## [F10] Add a domain-friendly dependency/configuration editor

Status: Open

### Problem / Long-term direction
Admins should understand relationships and dependencies without needing database knowledge.

### Goal
Create an admin-friendly representation of:
- process steps
- dependencies
- assignment targets
- triggers/conditions
- required vs optional behavior

### Constraints
- Do not expose raw relational DB concepts directly
- Show domain relationships in a human-friendly way
- Prefer validation and preview over unrestricted editing

### Reasoning effort
**Very High**

### Best tool
**Claude first**
