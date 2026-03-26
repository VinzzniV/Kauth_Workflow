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

## 🟡 ROADMAP

---

### [F5] Define a DB migration strategy

**Problem:** SQL files are numbered manually, no runner (Flyway, Liquibase, etc.). Risk of wrong order or missed migrations on new environments. `db/13_remove_skipped_task_status.sql` is untracked and still needs to be run on existing DBs.

**Reasoning Effort:** High
**Tool:** Claude

---

### [F6] Add real authentication

**Constraint:** Late only. Demo auth is sufficient for now.

**Reasoning Effort:** Very High
**Tool:** Claude

---

### [F8] Build admin configuration system

**Goal:** Manage task templates, conditions, dependencies, answer definitions, role answer defaults via UI. Currently requires direct DB changes.

**Note:** `AdminProcessTypeSection` (process type activation/deactivation) already done.

**Relation to F10:** F10 (visual dependency editor) is an optional UI upgrade for the dependency part of F8. F8 must be done first. F10 replaces the dropdown-based dependency editor with a drag & drop graph view.

**Existing patterns to follow:**
- Hub-spoke architecture: hook manages state + API calls, component renders forms/tables
- Draft-based editing (see `useAdminOrganizationManagement.ts` for pattern)
- All endpoints behind `CanManageAdminConfiguration` policy
- Filter by `process_type_id` — admin selects process type first, then edits its templates/answers
- Integration into `AdminWorkspaceNavigation` as new workspace section(s)

**Existing admin files for reference:**
- `api/API/Endpoints/AdminEndpoints.cs` — all admin routes
- `web/src/pages/AdminConfigPage.tsx` — admin page container
- `web/src/components/admin-config/AdminWorkspaceNavigation.tsx` — workspace nav
- `web/src/components/admin-config/adminWorkspaceModel.ts` — workspace model/types
- `web/src/hooks/useAdminOrganizationManagement.ts` — best pattern example for new hooks
- `web/src/components/admin-config/AdminProcessTypeSection.tsx` — process type UI (already done)

**DB tables already exist (no migrations needed):**
- `task_templates` — id, process_type_id, template_key, title, category, description, icon_key, owning_department_id, default_responsibility_id, process_area_label, is_department_phase_task, is_required, due_in_days, sort_order, is_active
- `task_template_conditions` — id, task_template_id, condition_group, answer_key, operator (eq/neq/is_true/is_false/is_null/is_not_null), expected_value_text, expected_value_boolean, expected_value_number
- `task_template_dependencies` — id, task_template_id, depends_on_task_template_id, required_status (open/ready/in_progress/blocked/done)
- `workflow_answer_definitions` — id, process_type_id, answer_key, title, category, description, icon_key, input_type (boolean/text/select/multi_select), is_required, sort_order, is_active
- `app_role_answer_defaults` — id, process_type_id, app_role_id, answer_key, default_value_text, default_value_boolean

**Status:** Fertig. Alle Subtasks (F8.1–F8.11) abgeschlossen. Review und 18 Integration-Tests in `api/API.Tests/PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs`.

---

### [F9] Define data lifecycle handling

**Direction:** Archive vs. delete for completed/old workflows.

**Reasoning Effort:** High
**Tool:** Claude

---

### [F10] Add a visual dependency editor

**Goal:** Admin UI to configure task template dependencies visually.

**Status:** Fertig. Alle Subtasks (F10.1–F10.9) abgeschlossen. Fix: `GetAdminDependencyGraph` Stub in `api/API.Tests/WorkflowEndpointsTests.cs` ergänzt. Backend + Frontend Build: 0 Errors.
