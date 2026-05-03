# Migrationspfad

#architektur #migration

Wo das Projekt jetzt steht, wohin es geht, und in welcher Reihenfolge die Migration abläuft.

Primärquelle im Repo: `PROJECT_CONTEXT.md`

---

## Stand April 2026

### Bereits vorhanden ✓

- Backend (ASP.NET Core 8), Frontend (React 19), PostgreSQL als tragfähige Basis
- Workflow-Instanzen, Tasks, Kommentare, Deadlines, Audit, Admin-Konfiguration
- Directory-/Identity-Integration mit Entra
- Mehrere Prozessarten und konfigurierbare Formular-/Task-Bausteine
- Erste phasenbasierte Definitionen für `onboarding`, `offboarding`, `department_change` im Definition Layer
- Rotation/Abteilungsdurchlauf (Phase 1–8 abgeschlossen)
- Automation Layer Kern — Jobs, Versuche, Logs
- Mitarbeiterzentrierter Lifecycle-Schnitt: `people` als fachlicher Anker
- Konfigurierbare Mail-Vorlagen (`notification_templates`)
- Zentrales System-Event-Log

### Noch nicht im Zielbild ✗

- Expliziter Definition Layer mit vollständiger Versionierung (teilweise da, nicht vollständig genutzt)
- Eigenständige Runtime-/Orchestrierungsschicht (Runtime-Events existieren, aber noch kein echter Node-Lifecycle-Loop)
- Echter Automation Layer mit produktiven Handlern (derzeit simuliert: `CreateAdUser`, `SendWelcomeMail`)
- Guided Builder für neue Workflow-Definitionen (Canvas-UI existiert, aber Nutzbarkeit unklar)
- Saubere Repository-/Service-Grenzen (Monolith-Repository als Hauptproblem)

---

## Migrationspfad (Reihenfolge)

| Schritt | Inhalt | Status |
|---------|--------|--------|
| 1 | Artefakt- und Secret-Hygiene | ✓ |
| 2 | Produktkern ent-onboarden | laufend |
| 3 | Zielarchitektur dokumentieren, Begriffe harmonisieren | ✓ |
| 4 | Definition Layer einführen | ✓ teilweise |
| 5 | Runtime parallel einführen | in Arbeit |
| 6 | Bestehende Workflows mappen | ✓ für Kern-3 |
| 7 | Task-System an Node-Runtime anbinden | offen |
| 8 | Generische Validierung einführen | ✓ erledigt (CLA-4, DAG-Erreichbarkeitscheck + Publish-Guard) |
| 9 | Automation Layer bauen | Basis da, echte Handler fehlen |
| 10 | Guided Builder ausbauen | Basis da |
| 11 | Altwelt gezielt zurückbauen | nach Parität |

---

## Offene technische Schulden (aus Code Review)

### Kritisch (Stand April 2026)

| ID | Problem | Status |
|----|---------|--------|
| C1 | Transaktionsgrenzen Rotation-Regenerierung | ✓ erledigt |
| C2 | Rotation-Template ohne Zuständigkeit → Aufgaben unsichtbar | ✓ erledigt (2026-04-24) |
| C3 | Entra-gelöschte User werden nicht deaktiviert | ✓ erledigt |
| C4 | Task-Filter in-memory statt SQL | ✓ erledigt (Rotation) |

### Hoch

| ID | Problem | Status |
|----|---------|--------|
| H1 | Monolithisches Repository | ✓ teilweise erledigt — Rotation-Slice herausgeschnitten (CLA-3); weitere Schnitte offen |
| H5 | Keine DAG-Validierung vor Workflow-Definition-Publish | ✓ erledigt (CLA-4, 2026-04-24) |

→ Details: [[Code-Review-Status]]

---

## Erledigter Repository-Schnitt: CLA-3 (2026-04-23)

Der Rotation-Slice wurde aus dem Monolithen herausgeschnitten:

- `PostgresRotationRepository` (neu) ← aus `RotationTaskGenerationOperations` + `RotationOperations` + weiteren Rotation-Partials
- `PostgresRepositorySharedHelpers` (neu) ← geteilte Helfer (Assignment/Audit/Notification/MapTargetPerson)
- `PostgresWorkflowRepository` delegiert Rotation-Task-Routing über ctor-injiziertes `IRotationRepository`

Noch ausstehend: `WorkflowRuntimeRepository`, `AutomationRepository`, `AuditRepository`, `NotificationRepository` — `PostgresWorkflowRepository` ist weiterhin groß, aber Rotation-Bloat ist weg.

---

## Parallelzustand: Legacy + Neu

Beide existieren bewusst nebeneinander, bis Parität erreicht ist:

| Legacy | Neu | Status |
|--------|-----|--------|
| `process_types`-Tabelle + `/process-types`-Endpunkt | `workflow_definitions` + `/workflow-definitions/startable` | aktiv parallel — beide Pfade haben Konsumenten |
| `processTypeKey` in `WorkflowCreateRequest` | `workflowDefinitionKey` | beide werden parallel gesendet/akzeptiert |
| `PrimaryLegacyProcessTypeKey` in Definition-DTOs | direkter Definition-Key | aktiv für Mapping bei Publish |
| `setup`-Node-Type | `measure_provision` / `_deprovision` / `_change` / `_rename` | aktiv defensiv für Production-Daten; kein neuer `setup` mehr im Seed |
| `WorkflowLegacyStatus`-String-Feld | typsicheres `WorkflowStatus` | beide werden geliefert; Frontend konvertiert weg vom Legacy |
| `workflows.create.onboarding` etc. (process-type-Permissions) | `workflows.create.<definition_key>` | ✓ Code-Umstellung (Schritt 5, 2026-04-30) — Suffix kommt jetzt aus `workflow_definitions.key`; Permission-Strings unverändert, weil definition_key == legacy process_type_key. Legacy-Lookup gegen `PrimaryLegacyProcessTypeKey` bleibt als Brücke bis Schritt 6 |
| `/workflows/completed-onboardings`, `/rotation/completed-onboardings`, `CompletedOnboardingSearchResultDto`, `CompletedOnboardingSearchResult` | `/workflow-target-person-sources`, `/people/rotation-eligible`, `WorkflowTargetPersonSourceDto`, `RotationEligiblePerson` | ✓ Code-Umstellung (Schritt 7A, 2026-05-01) |
| Responsibility `hr_onboarding` | `hr_workflow_initiator` | ✓ Code-Umstellung (Schritt 7B, 2026-05-01). Seeds + Backend-Fallback in `NotificationOperations.cs` umgestellt |
| Hardcoded `WorkflowCreatePermissions[]`-Array | dynamisch via `WorkflowCreate(definitionKey)` | ✓ Code-Cleanup (Schritt 7C, 2026-05-01). Permission-Schema ist seit 6.3d-iv vollstaendig definitionsgetrieben — Array war ungenutzt |
| `task_templates` + `legacyTemplateKey`-Konfig pro Node | `workflow_node_task_specs` + Sub-Tabellen pro Maßnahmen-/task/approval-Node | ✓ LA5 (2026-05-03). Specs haengen am `workflow_node_id` der published Version. Generator + Runtime-Resolver + Admin-CRUD lesen/schreiben aus den neuen Tabellen. Alte Tabellen + `legacyTemplateKey`-Validation droppt. Detail in [[LA5-TaskSpezifikation-Skizze]]. |
| Specs nur am published `workflow_node_id`, kein Carry-Over zwischen Versionen | Specs reisen mit der Version-DTO; `EnsureWorkingDraft` + `Replace` schreiben sie atomic | ✓ FE-9 (2026-05-03). `WorkflowDefinitionNodeDto.Specs` ist Pflichtfeld; Validierung deckt Spec-Key-Eindeutigkeit, Same-Node-Dependencies, Node-Type-Compat ab. Builder round-trippt Specs durch Save. AdminTaskTemplate-Editor unveraendert (schreibt weiter auf published) — Cross-Version-Leak ist Watch-Item. Detail in [[FE9-Spec-Carry-Over-Skizze]]. |
| In-Memory Task-Filter (Rotation) | SQL-seitiger Task-Filter | ✓ erledigt |
| Monolith-Repository | Slice-Repositories (`PostgresRotationRepository` + Helpers) | Rotation ✓ erledigt; Runtime/Automation/Audit/Notification offen |

Konkrete Roadmap zum Abbau: siehe [[Legacy-Abbau-Plan]].

---

## Verwandte Notizen

- [[Zielarchitektur]] — Was das Ziel ist
- [[Entscheidungen]] — Warum Migration statt Big Bang
- [[Code-Review-Status]] — Was konkret aussteht
