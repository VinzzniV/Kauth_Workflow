# DECISIONS.md

## 1. Workflow system, not form app
This is a real workflow engine with tasks, ownership, and phases.
Do not simplify into form submission logic.

---

## 2. Backend is source of truth
All business rules (status, assignment, completion, visibility) must be correct in backend.
Frontend is display only.

---

## 3. Roles ≠ Responsibilities
- Roles = access
- Responsibilities = ownership
Never merge.

---

## 4. assignment_type is strict
- `user` = personal
- `responsibility` = shared
No leakage or shortcuts.

---

## 5. Completion rule
Workflow completes only when all relevant tasks are done.
No UI-based shortcuts.

---

## 6. Parallel work
Multiple departments can work simultaneously.
Do not force linear flows.

---

## 7. Status must stay consistent
- Do not map `cancelled` → `completed`
- Do not hide status differences
- Do not use `skipped` as task-state workaround; non-needed tasks must not be generated

---

## 8. Task generation is data-driven
Use templates, conditions, dependencies.
Avoid hardcoding tasks.

---

## 9. Generated tasks are mandatory
Once generated and relevant → must be respected in workflow.

---

## 10. Hardware logic stays explicit
Do not flatten branching logic.

---

## 11. Admin is not default actor
Admin can override, but not replace real ownership.

---

## 12. Move toward configurability
Future:
- templates
- responsibilities
- mappings
- notifications
configurable via UI

But:
- no overengineering
- no unsafe free editing

---

## 13. Product direction
From onboarding → employee lifecycle system:
- onboarding
- offboarding
- employee changes

Do not hardcode new features to onboarding only.

---

## 14. Prototype rule
Missing polish is ok.
Broken business logic is NOT.

---

## 15. Refactors must be justified
No large refactors during bugfixes.

---

## 16. AI usage
- small scope
- one task at a time
- review diffs
- protect business logic

---

## 17. Hygiene matters
No build artifacts as source of truth.

---

## 18. Prefer archive over delete (long-term)
Protect historical workflows.

---

## 19. Identity source of truth
- The app is NOT the leading user directory
- On-prem AD is the leading source
- Entra ID is the productive auth and integration layer
- Do not build product logic around local demo identities

---

## 20. No manual user CRUD as default product model
- Users should not be created manually in the app as the normal path
- The app may project, map and enrich external identities
- Local manual overrides must stay the exception, not the base model

---

## 21. Identity != Employee
- Technical identity and employee master data are different concepts
- Do not couple employee records 1:1 to local app users
- Do not resolve productive identity via mutable strings like display name

---

## 22. Groups are the default access model
- Standard access should come from AD/Entra groups
- App-specific responsibilities may stay local
- Roles = access, responsibilities = domain ownership, keep the split strict

---

## 23. Productive auth is now a critical path item
- Proper auth is NOT a late nice-to-have anymore
- No production rollout before productive auth, group mapping and secret handling are solved

---

## 24. On-prem hosting still needs modern auth
- Running on-prem does not justify custom auth shortcuts
- Use Entra-based login even if API and web are hosted on an internal Docker server
