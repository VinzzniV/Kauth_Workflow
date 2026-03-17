# DECISIONS.md

## Purpose of this file

This file documents long-term architectural and business-logic decisions that should remain stable over time.

It is NOT a changelog.
It is NOT a task list.
It should only contain decisions that help prevent future regressions, confusion, or bad refactors.

---

## 1. Roles and responsibilities are separate concepts

Decision:
Application roles and business responsibilities are intentionally separate.

Why:
- roles define access rights in the UI/API
- responsibilities define domain/task ownership
- merging them would make the system harder to reason about and less flexible

Consequence:
Any future implementation must preserve this distinction.

---

## 2. The system is workflow-based, not just form-based

Decision:
The project is a real workflow system, not just a data-entry form.

Why:
- the onboarding process has phases
- departments act at different times
- tasks are generated and tracked
- completion depends on business progress, not just form submission

Consequence:
Future changes must respect workflow state, generated tasks, and multi-step ownership.

---

## 3. Tasks are generated from requirements through templates

Decision:
Requirements selected in the supervisor step generate tasks through database-driven task templates, conditions, and dependencies.

Why:
- keeps the system more maintainable
- allows requirement/task behavior to be configured without hardcoding everything
- supports future extensibility

Consequence:
Do not replace this with manual hardcoded task creation flows unless explicitly necessary.

---

## 4. Assignment type must be respected strictly

Decision:
Task assignment_type is a core rule and must be enforced consistently.

Meaning:
- user assignment = personal task
- responsibility assignment = shared/domain-based task

Why:
Earlier versions leaked personal tasks through responsibility matching.

Consequence:
Any future authorization or task-visibility logic must explicitly respect assignment_type.

---

## 5. Workflow completion depends on all relevant generated tasks

Decision:
A workflow is completed only when all relevant generated tasks are done.

Why:
Earlier prototype behavior risked completing a workflow too early when one department finished before others.

Consequence:
Future changes must not reintroduce completion logic that ignores still-open relevant department tasks.

---

## 6. Parallel department work is valid

Decision:
Multiple departments may have open tasks at the same time.

Why:
The real onboarding process is not strictly single-threaded.
IT, QS, AV, and QMB can all be active in parallel depending on requirements.

Consequence:
Status logic, task visibility, and detail views must support parallel open work correctly.

---

## 7. Hardware uses a dedicated branching model

Decision:
Hardware is modeled with two distinct questions:
- hardware_requested
- hardware_available

Why:
Whether hardware is needed and whether it is already available are different business facts.

Consequence:
Future changes must keep the branching logic:
- no hardware if not requested
- setup/handover if requested and available
- procure/setup/handover if requested and not available

---

## 8. Frontend must not become the source of truth for workflow business rules

Decision:
The backend remains the main source of truth for workflow authorization, completion logic, and task correctness.

Why:
Frontend-only business logic easily drifts and causes inconsistent behavior.

Consequence:
Frontend may mirror behavior for UX, but must not silently redefine core workflow rules.

---

## 9. Admin is a management/override role, not the normal default actor

Decision:
Admin can see and manage more than other roles, but should not automatically be treated as the normal business actor for every workflow step.

Why:
Earlier prototype states made admin effectively HR + manager + worker at once, which hid real role problems.

Consequence:
Admin visibility and override are acceptable, but normal business ownership should stay with the correct roles/responsibilities.

---

## 10. Configuration should move out of hardcoded values over time

Decision:
System-relevant configuration should increasingly become manageable through admin/configuration rather than hidden hardcoded values.

Examples:
- notification email settings
- master data
- people/email addresses
- responsibility mappings where practical

Why:
The project goal includes maintainability and lower dependence on code changes for operational updates.

Consequence:
Prefer configurable behavior over hardcoded values when making future changes, but do this incrementally and safely.

---

## 11. Data-driven direction is preferred, but not at the cost of prototype stability

Decision:
The long-term direction is more data-driven behavior, but the prototype must stay stable.

Why:
Over-abstracting too early can make the prototype harder to reason about and more fragile.

Consequence:
Prefer pragmatic improvements.
Do not rewrite stable parts just to chase theoretical purity.

---

## 12. Large refactors must be justified by structure gain

Decision:
Large refactors are only acceptable when they clearly improve maintainability, readability, or correctness.

Why:
The project is complex enough that broad refactors can easily break working business logic.

Consequence:
Future AI-assisted refactors should be targeted, incremental, and justified.