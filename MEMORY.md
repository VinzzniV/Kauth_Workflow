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

- **Zyklus 8 ist aktiv** (2026-05-05). Thema: Skalierbarkeits- & Last-Haertung. Detail in `CODE_REVIEW.md` § "Aktiver Zyklus 8" und `TODO.md`.
- Zyklus 7 ist abgeschlossen — Lifecycle-Service ist Commit-Grenze fuer Create/Form/Approval/Task; Validation-Service ist in Draft-/Snapshot-/Helper-/Catalog-Slices aufgeteilt.
- Naechster sinnvoller Schritt: **Z8-1.1 Inventur** (unbegrenztes Laden, In-Memory-Filter/-Sort, N+1) als reiner Analyse-Slice ohne Code-Aenderung.
- `DOCS_CONTROL.md` bleibt zentraler Einstieg; pro Aufgabe mitdenken, welche Doku im selben Arbeitsgang aktualisiert wird.

## Active Risks / Watchouts

- `WorkflowLifecycleService` ist nach Z7-1.5b vollstaendig die Conn+Tx-Grenze fuer Create/Form/Approval/Task und orchestriert die `*InScope`-Logik direkt. `PostgresWorkflowRuntimeRepository` enthaelt nur noch read- und shared-static-Helfer.
- Rotation-Task-Routing bleibt im Repo (`UpdateTaskStatusByRef` / `DecideTaskApprovalByRef`): RotationTaskRef → `_rotationRepository`, WorkflowTaskRef → Lifecycle-Service.
- Haupt-Risiko fuer den naechsten Zyklus sind Lastpfade mit unbeschraenktem Laden, In-Memory-Filter/-Sortierung oder N+1, nicht weitere reine Hygiene-Splits.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal weiter an einer verfuegbaren PostgreSQL-Instanz auf `127.0.0.1:26432`.
- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen lokale Builds und Tests blockieren.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` beschreibt das stabile Plattform-Zielbild.
- `PROJECT_STRUCTURE.md` und `web/README.md` muessen bei sichtbaren Admin-/UI-Verschiebungen mitgezogen werden.
- Frontend-Stand nach FE-25..FE-31 ist stabil; aus Zyklus 8 entstehen erst dann FE-Items, wenn API-Vertraege fuer Pagination/Sortierung brechen.

## Cleanup Rule

- Eintraege entfernen, sobald sie fuer die naechsten Sessions nicht mehr helfen.
- Keine historischen Zusammenfassungen hier sammeln.
