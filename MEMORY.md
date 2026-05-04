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

- **Schritt 7 Slice 2 ist vollstaendig abgeschlossen** (2026-05-04). 2.0–2.6 done. 409 Tests gruen, 0 failed.
- `WorkflowLifecycleService` besitzt jetzt Conn+Tx fuer alle Mutationspfade: Task-Status, Approval, Automation, Definition-Runtime-Delegierung.
- `WorkflowAutomationService` und `WorkflowDefinitionRuntimeService` routen durch `IWorkflowLifecycleService`.
- Naechster Schritt: kein weiterer Schritt 7 offen. Naechster Zyklus aus `CODE_REVIEW.md` lesen.
- `DOCS_CONTROL.md` bleibt zentraler Einstieg; pro Aufgabe mitdenken, welche Doku im selben Arbeitsgang aktualisiert wird.
- Frontend-Review vom 2026-05-04 ist als umsetzbare Roadmap in `FRONTEND_TODO.md` abgebildet.
- `FE-25` bis `FE-29` sind erledigt: Builder-Dialoge sind gehärtet, wiederkehrende Inline-Layouts in gemeinsame CSS-Bausteine überführt, Haupt-Filterflächen sind vereinheitlicht, operative Listen haben Karten-/Tabellenmodus und `Meine Aufgaben`/`Laufende Vorgänge` nutzen Split-Views für weniger Navigationssprünge.
- Naechster FE-Schritt ist `FE-30` (Builder konsequent canvas-first machen).

## Active Risks / Watchouts

- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen lokale Builds und Tests blockieren.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal weiter an einer verfuegbaren PostgreSQL-Instanz auf `127.0.0.1:26432` — relevant fuer Slice 2.3+ (Verhaltens-Paritaets-Beweis nach Brücken-Migration).
- Repo-Methoden `CompleteRuntimeApprovalNode` / `CompleteRuntimeTaskNode` / `CreateWorkflowDefinitionInstance` / `CompleteRuntimeFormNode` in `PostgresWorkflowRuntimeRepository` existieren noch — sie werden nur noch via `lifecycleService.*` aufgerufen (Slice 2.5 Routing). Vollstaendige Verschiebung der Logik in den Service ist defer.
- Rotation-Task-Routing bleibt im Repo (`UpdateTaskStatusByRef` / `DecideTaskApprovalByRef`): RotationTaskRef → `_rotationRepository`, WorkflowTaskRef → Lifecycle-Service.
- Permission-Schema ist seit Slice 6.3d-iv vollstaendig definitionsgetrieben (`workflows.create.<definition_key>`).
- LA5 done: Task-Specs liegen am `workflow_node_id`. `workflow_definitions.approval_task_template_key` heisst nominell noch `_template_key` — Naming-Cleanup als FE-8 backlog.
- FE-9 done: Spec-Carry-Over via `WorkflowDefinitionNodeDto.Specs`. AdminTaskTemplate-Editor schreibt weiter auf published Version — Cross-Version-Leak bleibt Watch-Item (FE-11/13).

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` beschreibt das stabile Plattform-Zielbild.
- `PROJECT_STRUCTURE.md` und `web/README.md` muessen bei sichtbaren Admin-/UI-Verschiebungen mitgezogen werden.
- Diese Datei enthaelt nur noch Hinweise fuer die naechsten Sessions, keine laengere Historie.

## Cleanup Rule

- Eintraege entfernen, sobald sie fuer die naechsten Sessions nicht mehr helfen.
- Keine historischen Zusammenfassungen hier sammeln.
