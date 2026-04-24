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

| Legacy | Neu |
|--------|-----|
| `process_types` | `workflow_definitions` |
| `processTypeKey` | `workflowDefinitionKey` |
| In-Memory Task-Filter (Rotation) | SQL-seitiger Task-Filter ✓ |
| Monolith-Repository | Rotation-Repository herausgeschnitten ✓; weitere Schnitte offen |

---

## Verwandte Notizen

- [[Zielarchitektur]] — Was das Ziel ist
- [[Entscheidungen]] — Warum Migration statt Big Bang
- [[Code-Review-Status]] — Was konkret aussteht
