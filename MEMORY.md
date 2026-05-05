# MEMORY.md

## Zweck

Diese Datei ist nur kurzfristiges Arbeitsgedaechtnis.
Sie ist kein Backlog, kein Changelog und keine zweite Architektur-Doku.
Sollte die Datei Inhalte enthalten, die nicht mehr aktuell sind, dann bereinige diese.

Verwende sie nur fuer:
- aktuellen Fokus
- aktive Stolperfallen
- wenige temporaere Hinweise fuer die naechsten Sessions

---

## Current Focus

- **Zyklus 7 ist abgeschlossen** (2026-05-05). Thema: Lifecycle-Service-Konsolidierung + Validation-Service-Split. Detail in `CODE_REVIEW.md` § "Aktiver Zyklus 7" und `TODO.md`.
- Z7-1.1 (Lifecycle-Inventur) ist **done** — Ergebnis in `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md` § 12.
- Z7-2 ist **done** — `api/API.Tests/WorkflowLifecycleServiceTests.cs` deckt `wf:`/`rot:`-Routing, Rollback im zweiten Lifecycle-Schritt und den Automation-Scope-Pfad ab.
- Z7-1.2/1.3/1.4/1.5a sind **done** (2026-05-05) — Lifecycle-Service besitzt Conn+Tx fuer Create/Form/Approval/Task, statische Runtime-Helfer wandern hinter Scoped-Interfaces, Repo-Wrapper-Dupletten und `*ByRef`-Workflow-Pfade entfernt.
- Z7-1.5b ist **done** (2026-05-05) — alle 5 Sub-Slices (b.i..b.v) erledigt; Inhalte der `*InScope`-Methoden physisch in den Lifecycle-Service gezogen; Scoped-Vertrag `IWorkflowDefinitionRuntimeScopedRepository` komplett entfernt (Datei + DI + Service-Ctor-Parameter + Test-Stubs).
- Z7-3 ist **done** (2026-05-05) — `WorkflowDefinitionValidationService` in `WorkflowDefinitionValidationCatalog`, `WorkflowDefinitionValidationHelpers`, `WorkflowDefinitionSnapshotValidator` und `WorkflowDefinitionDraftValidator` geschnitten; Service ist jetzt duenne Facade.
- Naechster sinnvoller Schritt: neuen Review-Block aus `CODE_REVIEW.md` ziehen, sobald ein neuer Zyklus festgelegt ist.
- `DOCS_CONTROL.md` bleibt zentraler Einstieg; pro Aufgabe mitdenken, welche Doku im selben Arbeitsgang aktualisiert wird.

## Active Risks / Watchouts

- `WorkflowLifecycleService` ist nach Z7-1.5b vollstaendig die Conn+Tx-Grenze fuer Create/Form/Approval/Task und orchestriert die `*InScope`-Logik direkt. `PostgresWorkflowRuntimeRepository` enthaelt nur noch read- und shared-static-Helfer.
- Rotation-Task-Routing bleibt im Repo (`UpdateTaskStatusByRef` / `DecideTaskApprovalByRef`): RotationTaskRef → `_rotationRepository`, WorkflowTaskRef → Lifecycle-Service.
- `WorkflowDefinitionValidationService.cs` ist kein aktiver Monolith-Posten mehr; Zyklus 7 hat den Split abgeschlossen.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal weiter an einer verfuegbaren PostgreSQL-Instanz auf `127.0.0.1:26432`.
- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen lokale Builds und Tests blockieren.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` beschreibt das stabile Plattform-Zielbild.
- `PROJECT_STRUCTURE.md` und `web/README.md` muessen bei sichtbaren Admin-/UI-Verschiebungen mitgezogen werden.
- Frontend-Stand nach FE-25..FE-31 ist stabil; aus Zyklus 7 entstehen keine neuen FE-Items.

## Cleanup Rule

- Eintraege entfernen, sobald sie fuer die naechsten Sessions nicht mehr helfen.
- Keine historischen Zusammenfassungen hier sammeln.
