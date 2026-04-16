# CLAUDE.md

This file provides guidance to Claude Code when working in this repository.

---

## Collaboration Workflow

This project uses a split-AI workflow:

- **Claude**: analysis, architecture, migration slicing, risk assessment
- **Codex**: implementation, test adaptation, compile fixes, incremental rollout

When working on non-trivial tasks, read these first:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`

For rotation / department-rotation work also read:
- `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`
- `TODO.md`

For architecture, migration, runtime, or data-model work also read:
- `PRODUCTIVE_TARGET_ARCHITECTURE.md`
- `DECISIONS.md`

After reading, explicitly keep track of which docs must be updated in the same pass if assumptions, structure, behavior, setup, or scope changed.

Documentation and task descriptions are primarily written in German.

---

## Project Overview

Internal workflow platform in migration.

Current reality:
- existing lifecycle-oriented workflow core
- tasks, comments, deadlines, audit, responsibilities
- directory sync, Entra-based auth direction, admin configuration

Target direction:
- versioned workflow definitions
- dedicated runtime/orchestration layer
- controlled automation layer
- guided admin builder

Do not treat onboarding as the hidden product core.

---

## Stack

- Backend: ASP.NET Core 8, Minimal API, PostgreSQL
- Frontend: React 19, TypeScript, Vite, TanStack Query, React Router 7, Tailwind CSS 4
- Auth: `AUTH_MODE=dev-sim` locally or `AUTH_MODE=entra` for production-style usage
- Proxy: Caddy in production-style deployment

---

## Working Rules

- Prefer incremental migration over large rewrites
- Keep legacy and new architecture parallel until parity exists
- Do not add new workflow special cases that narrow the core model
- Do not introduce free-form technical automation for admins
- Update matching docs in the same pass when architecture assumptions change

---

## Practical Orientation

Important current code areas:
- `api/API/Endpoints`
- `api/API/Repositories`
- `api/API/Services`
- `web/src/pages`
- `web/src/components/admin-config`
- `web/src/services`

Important architecture docs:
- `DOCS_CONTROL.md`
- `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`
- `PRODUCTIVE_TARGET_ARCHITECTURE.md`
- `TODO.md`

The codebase still reflects the older task-/process-type-driven model in many places.
Any larger change should explicitly state how it fits the migration toward definition layer, runtime, and controlled automation.
