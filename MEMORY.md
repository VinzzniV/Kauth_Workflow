# MEMORY.md

## Zweck

- kurzfristiges Arbeitsgedaechtnis fuer die naechste Session
- nur aktueller Fokus, aktive Risiken und wenige echte Stolperfallen

## Primaerquelle fuer

- naechsten konkreten Arbeitskontext
- kurzfristige Watchouts

## Nicht verwenden fuer

- Backlog
- Changelog
- zweite Architektur- oder Review-Doku

## Wann aktualisieren

- wenn sich aktiver Zyklus oder naechster Schritt aendert
- wenn eine Stolperfalle nicht mehr aktuell ist

## Verwandte Dateien

- `CODE_REVIEW.md`
- `TODO.md`
- `CODEX_SYNC.md`
- `DOCS_CONTROL.md`

---

## Current Focus

- **Aktiver Zyklus:** keiner — Z8 geschlossen 2026-05-05.
- **Naechster Schritt:** neuen Review-Zyklus eroeffnen, sobald ein klarer Hebel feststeht. Kandidaten: LQ2-Z3 (`EntraDirectorySyncService`-Split, bringt auch Coverage fuer Z8-2.3-Batch-Helfer mit), #6 Pagination fuer Departments/Rollen falls Last-Trigger, oder neue Befunde aus laufender Arbeit.
- **Z8 Abschluss-Stand:** #1/#2/#3/#4/#7 gepushed (Bulk-Lookups, Batch-Sweep+Apply, Entra Group/Member Bulk, Recipient-Bulk); #5 als false positive verifiziert; #8 deferred (admin-getriggert, kein kleiner SQL-Hebel); Z8-4 Coverage mit Z8-4.1 (Bulk-Recipient-Integration-Tests) abgeschlossen, EntraDirectorySync-Batch-Helfer-Coverage nach LQ2-Z3 verschoben.
- **Vorher lesen:** `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `TODO.md`, `CODEX_SYNC.md`

## Active Risks / Watchouts

- `WorkflowLifecycleService` ist nach Z7 die Commit-Grenze fuer Create/Form/Approval/Task; neue Lastarbeit darf diese Grenze nicht wieder aufweichen.
- Rotation-Task-Routing bleibt geteilt: `rot:*` geht direkt ueber `_rotationRepository`, `wf:*` ueber den Lifecycle-Service.
- Groesster aktueller Technikhebel ist Lastverhalten, nicht weiterer Hygiene-Refactor.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen Builds und Tests blockieren.

## Temporary Notes

- Frontend-Stand nach FE-25..FE-31 ist stabil; Z8 erzeugt erst dann FE-Arbeit, wenn API-Vertraege fuer Pagination/Sortierung angepasst werden.
- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
