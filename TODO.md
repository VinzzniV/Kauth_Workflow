# TODO.md

## 🔴 MUST FIX BEFORE DEMO / HANDOFF

### [D1] Demo-Seed nutzt weiterhin reale Notification-Mailadresse
- Problem:
  In `db/02_seed.sql` verwenden alle Demo-Benutzer weiterhin dieselbe echte `notification_email`.

- Warum kritisch:
  Sobald Mailversand aktiviert oder getestet wird, können Demo-Aktionen reale E-Mails verschicken.

- Evidence:
  `db/02_seed.sql`
  - `notification_email = 'vinzent.niederwieser@kauth.de'`

- Fix direction:
  Alle Demo-Benutzer auf sichere Demo-Adressen umstellen, z. B. `user@demo.local`.

- Status:
  OPEN

---

### [D2] Export-/ZIP-Hygiene ist noch nicht sauber
- Problem:
  Im ZIP/Handoff liegen weiterhin nicht-produktive oder lokale Dateien:
  - `.git/`
  - `.claude/settings.local.json`
  - `web/.env.local`
  - `web/dist/`
  - `api/API.Tests/bin/`
  - `api/API.Tests/obj/`

- Warum kritisch:
  Unsauberer Lieferstand, unnötige Artefakte, potenzielle Leaks lokaler Einstellungen.

- Fix direction:
  Vor Handoff/Archivierung den Projektstand bereinigen und ZIP neu erstellen.

- Status:
  OPEN

---

### [D3] Workflow-Status bei `cancelled`-Tasks fachlich final klären
- Problem:
  `cancelled`-Tasks werden aktuell weder wie `done`/`skipped` als workflow-abschließend behandelt noch sauber in einen terminalen Workflowstatus überführt.

- Warum kritisch:
  Ein Workflow kann in einem nicht-terminalen Status hängen bleiben, obwohl keine aktive Arbeit mehr offen ist.

- Evidence:
  `api/API/Repositories/PostgresWorkflowRepository.StatusCalculationOperations.cs`

- Fix direction:
  Fachlich entscheiden:
  - `cancelled` wie `skipped` behandeln
  - oder klaren Workflow-Abbruchmechanismus definieren
  Danach Statusberechnung entsprechend anpassen.

- Status:
  OPEN

---

## 🟠 SHOULD FIX SOON

### [D4] Multi-Role-Navigation ist noch zu prioritätsbasiert
- Problem:
  Benutzer mit mehreren Rollen erhalten nicht immer alle sinnvollen Navigationseinträge.
  Beispiel: HR + Manager wird aktuell vor allem als HR behandelt.

- Evidence:
  `web/src/auth/roleModel.ts`
  `web/src/navigation/useRoleAwareNavigation.ts`

- Warum wichtig:
  Mehrrollen-Benutzer sehen unter Umständen nicht alle für sie relevanten Arbeitsbereiche.

- Fix direction:
  Navigation additiv aufbauen statt über eine einzige dominante Persona zu priorisieren.

- Status:
  OPEN

---

### [D5] Kleiner Frontend-Bug in `workflowDetailModel.ts`
- Problem:
  In `buildProcessSteps()` ist `key: "hr-start"` doppelt im gleichen Objekt eingetragen.

- Warum wichtig:
  Zeichen dafür, dass der Frontend-Stand noch nicht sauber durch Lint/Build geprüft wurde.

- Evidence:
  `web/src/components/workflow-detail/workflowDetailModel.ts`

- Fix direction:
  Doppelten Eintrag entfernen und Frontend-Lint/Build sauber laufen lassen.

- Status:
  OPEN

---

### [D6] Frontend-Lint/Build in sauberem Zustand wirklich ausführen
- Problem:
  Der aktuelle ZIP-Stand belegt nicht, dass der Frontend-Stand wirklich lint-sauber und build-sauber ist.

- Warum wichtig:
  Kleinere Probleme wie der doppelte `key` können sonst bis zur Demo unbemerkt bleiben.

- Fix direction:
  Sauberen Projektstand herstellen, Dependencies frisch installieren, `npm run lint` und `npm run build` wirklich laufen lassen.

- Status:
  OPEN

---

## 🟡 TECH DEBT

### [D7] `task_template_conditions.answer_key` ist weiterhin nicht relational abgesichert
- Problem:
  `answer_key` ist ein loses String-Feld ohne FK auf die Definitionslogik.

- Risiko:
  Tippfehler oder Key-Renames können Task-Generierung still brechen.

- Evidence:
  `db/01_schema.sql`

- Fix direction:
  Mittelfristig relational härten oder eine robustere Definitionsreferenz einführen.

- Status:
  OPEN

---

### [D8] `selected_option_id` ist nicht gegen die Answer-Definition abgesichert
- Problem:
  Eine `selected_option_id` kann theoretisch auf eine Option einer anderen Definition zeigen.

- Risiko:
  Datenintegrität ist nicht vollständig garantiert.

- Evidence:
  `db/01_schema.sql`

- Fix direction:
  Relationale Absicherung oder validierende Persistenzlogik ergänzen.

- Status:
  OPEN

---

### [D9] `app_users.department_id` und `people.department_id` bleiben doppelt
- Problem:
  Zwei Tabellen halten potenziell dieselbe Department-Wahrheit.

- Risiko:
  Langfristige Divergenz zwischen Stammdaten und Nutzerkontext.

- Evidence:
  `db/01_schema.sql`

- Fix direction:
  Fachlich entscheiden, ob beide Spalten wirklich gebraucht werden. Falls nein: langfristig konsolidieren.

- Status:
  OPEN

---

### [D10] Kleinere Frontend-Duplikate bereinigen
- Problem:
  `formatDate` und ähnliche Präsentationshilfen existieren mehrfach.

- Evidence:
  `WorkflowListPage.tsx`
  `SupervisorStepPage.tsx`
  `WorkflowCard.tsx`
  `MyTasksPage.tsx`
  `workflowDetailModel.ts`

- Fix direction:
  Kleine gemeinsame Date/Formatting-Utilities einführen.

- Status:
  OPEN

---

## 🟢 DONE / LARGELY SOLVED

### [R-A1] Requirement behavior is no longer primarily frontend-hardcoded
- Status:
  DONE

### [R-A2] Task assignment type is enforced correctly
- Status:
  DONE

### [R-A3] Workflow list N+1 loading pattern removed
- Status:
  DONE

### [R-A4] Repository structure was meaningfully modularized
- Status:
  DONE

### [R-A5] Backend test coverage for core workflow/authorization logic exists
- Status:
  DONE

### [R-A6] Mail configuration is now admin-manageable and safer than before
- Status:
  DONE

---

## 🧪 FUTURE / IDEAS

### [F1] Add minimal frontend smoke tests
- Focus:
  Workflow list, supervisor step, my tasks visibility

- Status:
  FUTURE

---

### [F2] Continue reducing presentation-side hardcoding
- Focus:
  Remaining status/task display derivations in frontend utilities

- Status:
  FUTURE

---

### [F3] Improve demo/dev/prod separation
- Focus:
  Seeds, local env files, archive hygiene, config boundaries

- Status:
  FUTURE

---

### [F4] Move toward a cleaner migration strategy
- Focus:
  Long-term DB evolution beyond sequential SQL resets/backfills

- Status:
  FUTURE