# ENGINEERING_RULES.md

## Core Principles

- Backend is Source of Truth
- No business logic drift into frontend
- Roles and Responsibilities must stay separated
- assignment_type must always be respected
- `skipped` is not allowed

---

## Change Rules

- Only change what is required for the task
- No unrelated refactors
- No guessing → call out uncertainty
- Do not silently change behavior

---

## Documentation Rules

- Read `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md` and `MEMORY.md` before larger changes
- Update the relevant docs in the same pass when code, structure or runtime behavior changes
- Keep stable truth in `PROJECT_CONTEXT.md`, not in ad-hoc notes
- Keep temporary findings in `MEMORY.md`, not in long-term architecture files
- Do not duplicate setup, structure and decision content across multiple docs without need

---

## File Size Rules

Avoid large files:
- Frontend components > 400–500 lines → split
- Backend classes > 500–700 lines → split

Never:
- add new logic to already large files without evaluating split

---

## API Rules

- No in-memory filtering/pagination
- Always query at DB level
- One endpoint = one response shape

---

## Frontend Rules

- No duplicate business logic
- Avoid multiple requests for same data
- Cache static data (e.g. process types)
- Debounce search inputs

---

## Security Rules

- No open demo endpoints without explicit flag
- No secrets in plain text (DB or config)
- Validate all input

---

## Handoff Rules

- No node_modules
- No dist
- No bin/obj
- No .git
- Build must work from source only
