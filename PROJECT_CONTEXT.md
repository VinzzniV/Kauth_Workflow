# PROJECT_CONTEXT.md

## Goal

Internal onboarding workflow system → evolving into a configurable employee lifecycle platform.

Current focus:
- onboarding
Future:
- offboarding
- employee changes (name, department, access, hardware, etc.)

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
- `completed`, `cancelled`, `skipped` must not be mixed or mapped incorrectly

### Task relevance
- Once generated → must be treated as required for workflow

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

---

## Direction

Move toward:
- less frontend logic
- more data-driven behavior
- configurable workflows/templates
- admin-managed data via UI (safe, validated)
- reuse for multiple process types

---

## Do NOT

- merge roles/responsibilities
- break assignment_type logic
- complete workflows early
- move business logic into frontend
- hardcode new logic unnecessarily
- introduce large refactors casually

---

## AI Rules

- read context before coding
- change only what is needed
- keep backend truth
- avoid refactors
- explain changes