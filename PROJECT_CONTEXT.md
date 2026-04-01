# PROJECT_CONTEXT.md

## Goal

Internal onboarding workflow system → evolving into a configurable employee lifecycle platform.

Current focus:
- onboarding
Future:
- offboarding
- employee changes (name, department, access, hardware, etc.)

Productive direction:
- app hosted on-prem in Docker
- identity comes from on-prem AD via Entra sync
- app maps external identities and groups instead of owning them

Current implementation direction:
- multiple process types exist beyond pure onboarding
- local development uses `dev-sim` with synced directory identities
- server-near and productive auth run via Microsoft Entra ID
- admin configuration already includes directory sync, group mapping, permission management and runtime system settings

---

## Core Model

Workflow contains:
- person data
- selected requirements
- generated tasks
- status
- assignments
- notifications

Flow:
1. HR creates workflow
2. Supervisor selects requirements
3. Tasks are generated
4. Departments process tasks
5. Workflow completes when all relevant tasks are done

---

## Critical Rules

### Backend is truth
- All business logic must be correct in backend
- Frontend must not redefine logic

### Roles vs Responsibilities
- Roles = access (Admin, HR, Manager, Worker, Reader)
- Responsibilities = domain (IT, QS, AV, QMB)
- NEVER merge these

### Assignment
- `user` = only that user
- `responsibility` = shared domain
- No leaking between them

### Workflow completion
- Only when ALL relevant tasks are done
- No shortcuts

### Status consistency
- `completed` and `cancelled` must not be mixed or mapped incorrectly

### Task relevance
- Once generated → must be treated as required for workflow
- Tasks that should not exist must not be generated later just to be skipped

### Hardware logic
- requested=false → no tasks
- requested=true + available=true → setup + handover
- requested=true + available=false → procure + setup + handover

---

## Architecture

Frontend → API → DB

- React + TS + Vite
- ASP.NET Core API
- PostgreSQL

Frontend NEVER talks to DB.

Identity and access target:
- authentication via Microsoft Entra ID
- backend validates tokens and stays source of truth for authorization
- app-specific responsibilities remain local
- employee records must be separated from technical identities

---

## Direction

Move toward:
- less frontend logic
- more data-driven behavior
- configurable workflows/templates
- admin-managed data via UI (safe, validated)
- reuse for multiple process types
- productive auth and group-based authorization
- external identity mapping instead of manual in-app user lifecycle
- admin UI for sync status, group mapping, responsibilities and exceptions

---

## Do NOT

- merge roles/responsibilities
- break assignment_type logic
- complete workflows early
- move business logic into frontend
- hardcode new logic unnecessarily
- introduce large refactors casually
- treat demo auth as a productive fallback
- use the app as the primary user directory
- couple employee identity to mutable display-name matching

---

## AI Rules

- read context before coding
- change only what is needed
- keep backend truth
- avoid refactors
- explain changes

## Working Docs

- `DOCS_CONTROL.md` = which docs to read first, when to update which file, and where temporary vs stable knowledge belongs
- `PROJECT_CONTEXT.md` = stable project truth and guardrails
- `MEMORY.md` = current working memory for next session, active findings, open risks and immediate next steps
- `TODO.md` = larger production-readiness backlog and prioritised work packages
