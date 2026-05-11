# Migrationspfad

#architektur #migration

Wo das Projekt jetzt steht, wohin es geht, und in welcher Reihenfolge die Migration abläuft.

Primärquelle im Repo: `PROJECT_CONTEXT.md`

---

## Stand Mai 2026

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
| 7 | Task-System an Node-Runtime anbinden | ✓ erledigt (Zyklus 7, 2026-05-05: Lifecycle-Service als Commit-Grenze + Validation-Split abgeschlossen) |
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

Noch ausstehend: weitere fachlich motivierte Schnitte um `PostgresWorkflowRepository` bzw. verbleibende große Partials. Reine Split-Arbeit ohne Last-, Runtime- oder Wartbarkeits-Trigger ist derzeit nicht der naechste Hebel.

---

## Parallelzustand: Legacy + Neu

Der große Parallelzustand ist deutlich geschrumpft. Offen sind vor allem noch Benennungs- und Runtime-Brücken:

| Legacy | Neu | Status |
|--------|-----|--------|
| `legacyProcessTypeKey` in Form-/Gatekeeper-Config, Requirements- und WorkflowConfig-Pfaden | `workflowDefinitionKey` | noch aktiv in Builder, Publish-Validierung, Runtime-Gatekeeper und Teilen der Master-Data-Leser |
| `LegacyProcessTypeKey` / `ProcessTypeName` in Notification-DTOs und Link-Lookup-Record | `WorkflowDefinitionKey` / `WorkflowDefinitionName` | ✓ erledigt 2026-05-11: `WorkflowNotificationDispatchTarget`, `WorkflowNotificationRenderContext`, `WorkflowLinkLookupRecord` und alle Consumer umbenannt |
| `PrimaryLegacyProcessTypeKey` in Definition-/Runtime-Snapshots | direkter Definition-Key | ✓ erledigt 2026-05-11: Properties in C#-Code und Doku auf `workflowDefinitionKey` / `WorkflowDefinitionKey` umbenannt |
| `WorkflowLegacyStatus` bzw. `LegacyStatus` im Runtime-Pfad | typsicheres `WorkflowStatus` | kein eigener DTO-Schwerpunkt mehr, aber intern noch als Übergangsstatus in Engine/Apply-Pfad vorhanden |
| `WorkflowProcessTypeDto` / `processType`-Lesesurface | definitionsbasierte Workflow-Metadaten | ✓ erledigt 2026-05-11: umbenannt zu `WorkflowDefinitionRefDto` / `workflowDefinition`; FE-Typen und alle Consumer synchron |
| `setup`-Node-Type | `measure_provision` / `_deprovision` / `_change` / `_rename` | ✓ Code/Seeds clean (nur noch `db/_archive/`); Doku bereinigt; einzig offener Rest: manuelle DB-Inventur gegen persistierte Definitionen |
| Monolith-Repository | Slice-Repositories (`PostgresRotationRepository` + Helpers) | Rotation ✓ erledigt; Runtime/Automation/Audit/Notification offen |

Bereits abgebaut sind u. a. `process_types` als Tabelle, die alten Onboarding-Alias-Endpunkte, das harte Permission-Array und der `task_templates`-/`legacyTemplateKey`-Pfad. Konkrete Restinventur: [[Legacy-Abbau-Plan]].

Konkrete Roadmap zum Abbau: siehe [[Legacy-Abbau-Plan]].

---

## Verwandte Notizen

- [[Zielarchitektur]] — Was das Ziel ist
- [[Entscheidungen]] — Warum Migration statt Big Bang
- [[Code-Review-Status]] — Was konkret aussteht
