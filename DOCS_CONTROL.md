# DOCS_CONTROL.md

## Purpose

This file defines how the repository documentation should be read and maintained.
It is the control file for documentation flow, not the place for business rules or implementation detail.

---

## Read First

Always read these before making non-trivial changes:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`

Read these when they match the task:
- `PROJECT_STRUCTURE.md` for file ownership, entry points and module layout
- `SETUP.md` for local runtime, deployment and environment handling
- `PRODUCTIVE_TARGET_ARCHITECTURE.md` for target state and migration direction
- `DECISIONS.md` for stable architectural decisions
- `ENGINEERING_RULES.md` for implementation and handoff constraints
- `FRONTEND_TODO.md` for UI guardrails and frontend review criteria
- `TODO.md` for larger backlog and production-readiness work
- `web/README.md` for frontend-specific orientation

---

## Write Here

Use the following write targets deliberately:

- stable project truth and guardrails -> `PROJECT_CONTEXT.md`
- architecture or product decisions that should remain valid long-term -> `DECISIONS.md`
- temporary findings, current risks, next steps for the next session -> `MEMORY.md`
- local setup, deployment steps, env handling, smoke tests -> `SETUP.md`
- repository/module structure, important entry points, relevant folders -> `PROJECT_STRUCTURE.md`
- target architecture and migration direction -> `PRODUCTIVE_TARGET_ARCHITECTURE.md`
- broader backlog and release-readiness work packages -> `TODO.md`
- frontend-specific open work and UI guardrails -> `FRONTEND_TODO.md`
- frontend-oriented quick orientation -> `web/README.md`

---

## Minimum Doc Updates After Changes

When code changes affect these areas, update the matching docs in the same work pass:

- auth mode, env vars, compose, deployment, URLs -> `SETUP.md`
- added/removed folders, new entry points, renamed modules -> `PROJECT_STRUCTURE.md`
- changed stable rules or product direction -> `PROJECT_CONTEXT.md` and possibly `DECISIONS.md`
- changed frontend module boundaries or frontend workflows -> `web/README.md`
- changed target-state assumptions -> `PRODUCTIVE_TARGET_ARCHITECTURE.md`
- new ongoing risks or unfinished follow-ups -> `MEMORY.md`

---

## Practical Reading Order By Task

For backend or full-stack feature work:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `MEMORY.md`
4. `PROJECT_STRUCTURE.md`
5. task-specific docs

For frontend work:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `FRONTEND_TODO.md`
4. `web/README.md`
5. `MEMORY.md`

For infra, auth or deployment work:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `SETUP.md`
4. `PRODUCTIVE_TARGET_ARCHITECTURE.md`
5. `.env.prod.example` and compose files

---

## Hygiene Rules

- Do not duplicate the same truth across multiple files without a reason.
- Stable information should not live only in `MEMORY.md`.
- Temporary or uncertain notes should not be promoted into `PROJECT_CONTEXT.md` too early.
- If a file stops matching its real purpose, either rewrite its description or move the content.
- Prefer short, explicit status notes over vague "mostly done" wording.
