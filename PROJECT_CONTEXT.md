# PROJECT_CONTEXT.md

## Project goal

This project is an internal onboarding workflow system.

It replaces a manual process based on Word documents and email with a structured digital workflow.

Main goals:
- clear process status
- role-based steps
- traceable task ownership
- department-specific work
- later automation potential
- maintainable admin/master-data management

This is currently a prototype, but changes should already respect long-term maintainability.

---

## Core business model

The onboarding system is centered around workflows.

A workflow contains:
- person / onboarding master data
- selected requirements
- generated tasks
- workflow status
- notifications

The main process is:

1. HR creates a workflow
2. Manager / supervisor selects requirements
3. Tasks are generated for departments
4. Departments process their tasks
5. Workflow is completed only when all relevant tasks are done

---

## Important conceptual separation

There are TWO different concepts and they must never be merged.

### 1. Roles
Roles define what a user is allowed to access in the application.

Examples:
- Admin
- HR
- Manager
- Worker
- Reader

### 2. Responsibilities
Responsibilities define which department or domain a user is responsible for.

Examples:
- IT
- QS
- AV
- QMB

IMPORTANT:
Roles != Responsibilities

A user may have a role that grants UI/API access, and a responsibility that determines business ownership of tasks.

Do not collapse these concepts into one.

---

## Workflow phases

The workflow has business phases.

Target understanding:
- draft
- waiting_for_supervisor
- waiting_for_department / in_progress
- completed
- cancelled

Key rule:
A workflow must only reach completed when all relevant generated tasks are done.

The system must not complete a workflow too early just because one department finished first.

Parallel department work is valid and expected.

---

## Requirement and task model

Requirements are selected in the supervisor step.

Tasks are then generated from requirements through task templates and related conditions/dependencies.

Important rule:
- the requirement can be optional
- but once a task is generated and is relevant for the workflow, it must be treated as workflow-relevant

Task generation must stay data-driven as much as possible.

---

## Assignment model

Tasks use an assignment model with assignment_type.

Supported concepts:
- user assignment
- responsibility assignment

Critical rules:
- if assignment_type = user, the task must stay personal
- user assignments must NOT leak through shared responsibility matching
- if assignment_type = responsibility, the task may be shared within the responsibility/domain
- admin may see more, but must not become the default regular actor

Assignment logic is one of the most sensitive parts of the system.

---

## Department ownership model

Typical department ownership examples:
- IT handles AD, mailbox, hardware, LN, Habel-related tasks
- QS handles quality-related systems such as CAQ/Babtec
- AV handles AV-related systems such as Gewatec / Provis
- QMB handles Consense-related work

These mappings may evolve, but current behavior must remain consistent unless explicitly changed.

---

## Hardware logic

Hardware behavior is important and must stay correct.

Definitions:
- hardware_requested = hardware is needed
- hardware_available = suitable hardware already exists

Target logic:
- if hardware_requested = false -> no hardware tasks
- if hardware_requested = true and hardware_available = true:
  - hardware_setup
  - hardware_handover
- if hardware_requested = true and hardware_available = false:
  - hardware_procure
  - hardware_setup
  - hardware_handover

Do not simplify this into a single unconditional hardware path.

---

## Notification and mail configuration

The system includes notification email configuration.

Important expectations:
- mail sending can be enabled or disabled by admin
- disabled mail sending must not look like a technical failure
- person email addresses must be maintainable in admin/master data
- notification links must make sense for the recipient role

Prototype rule:
Mail handling should be reliable and understandable, but does not need full enterprise-grade secret infrastructure yet.

---

## Architecture

Frontend:
- React
- TypeScript
- Vite

Backend:
- ASP.NET Core Web API
- C#

Database:
- PostgreSQL

Flow:
Frontend -> API -> Database

Important:
The frontend must never talk directly to the database.

---

## Current architectural direction

The project should move toward:
- clearer modular structure
- less duplicated frontend logic
- more data-driven behavior
- less hardcoding
- cleaner separation of bootstrap, seed, cleanup, and backfill logic
- safer admin configuration

However:
Do not introduce large rewrites unless explicitly requested.

---

## What should not be changed casually

Do NOT casually:
- merge roles and responsibilities
- break assignment_type logic
- complete workflows early
- move business logic from backend into inconsistent frontend-only logic
- replace data-driven behavior with hardcoded shortcuts
- redesign the whole workflow model during small fixes
- rename business concepts unless explicitly requested

---

## Development rules for AI-assisted changes

When modifying the project:
- prefer minimal targeted changes
- preserve existing working behavior outside the requested scope
- avoid broad speculative refactors
- explain what changed and why
- check for impact on roles, workflow status, tasks, and notifications
- respect current naming unless a rename is explicitly part of the task
- if a decision is unclear, call it out instead of guessing silently

---

## Priority when reviewing changes

When analyzing or changing this project, prioritize:
1. correctness of workflow logic
2. correctness of role/responsibility/assignment handling
3. consistency of status calculation
4. task visibility and editability
5. maintainability and structure
6. UI polish

Function and business correctness are more important than cosmetic refactoring.