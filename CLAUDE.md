# CLAUDE.md

This file provides guidance to Claude Code when working in this repository.

For Codex-orchestrated Claude work, also read `CLAUDE_CONTROL.md`.
For vault navigation and stable knowledge, start with `KauthWorkflow/00 Start.md` when needed.

---

## Collaboration Workflow

This project uses a split-AI workflow:

- **Claude**: analysis, architecture, migration slicing, risk assessment
- **Codex**: implementation, test adaptation, compile fixes, incremental rollout

When working on non-trivial tasks, read these first:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`
- `CLAUDE_CONTROL.md` when Codex is orchestrating the workflow

For rotation / department-rotation work also read:
- `CODE_REVIEW.md`
- `TODO.md`

For product / UX / production-readiness slices from the active Z21 review also read:
- `PROD_TODO.md` (Slice-Reihenfolge, Aufwand, Reasoning-/Modell-/Plan-Mode-Empfehlung pro Slice)

For architecture, migration, runtime, or data-model work also read:
- `KauthWorkflow/Architektur/Zielarchitektur.md`
- `KauthWorkflow/Architektur/Entscheidungen.md`

After reading, explicitly keep track of which docs must be updated in the same pass if assumptions, structure, behavior, setup, or scope changed.

Documentation and task descriptions are primarily written in German.

---

## Obsidian Vault

There is a knowledge base at `KauthWorkflow/` (Obsidian vault, lives inside the repo).

**Read from the vault when:**
- Getting the human-readable map of the vault → `KauthWorkflow/00 Start.md`
- Planning architecture changes or migration steps → `KauthWorkflow/Architektur/`
- Clarifying domain concepts (Rotation, Identity, Automation, Workflow) → `KauthWorkflow/Domäne/`
- Checking what is open vs. done in the code review → `CODE_REVIEW.md` and `CODE_REVIEW_ARCHIVE.md`
- Understanding how Claude and Codex are supposed to work together → `KauthWorkflow/Arbeit/KI-Workflow.md`
- Start at `KauthWorkflow/00 Start.md` for the full index

**Write to the vault when:**
- A new architecture decision was made → update `KauthWorkflow/Architektur/Entscheidungen.md`
- A migration step was completed → update `KauthWorkflow/Architektur/Migrationspfad.md`
- A new domain concept was introduced → update or create the relevant file in `KauthWorkflow/Domäne/`
- Review status changed significantly → update `CODE_REVIEW.md` and move deep history to `CODE_REVIEW_ARCHIVE.md`

**Do not write to the vault for:**
- Short-term session context → use `MEMORY.md`
- Task tracking → use `TODO.md`
- Code snippets or diffs → stay in the repo

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
- `CODE_REVIEW.md`
- `KauthWorkflow/Architektur/Zielarchitektur.md`
- `TODO.md`

The codebase still reflects the older task-/process-type-driven model in many places.
Any larger change should explicitly state how it fits the migration toward definition layer, runtime, and controlled automation.
