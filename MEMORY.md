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

- **Aktiver Zyklus:** Z9 — `EntraDirectorySyncService`-Split / Testbarkeit (LQ2-Z3 aktiviert), eroeffnet 2026-05-05.
- **Trigger:** Z8 hat die Lastpfade in `SyncAllAsync` gehaertet (Z8-2.3 Batch-Helfer). Offene Grenze ist die fehlende Test-Isolation: `UpsertDirectoryIdentitiesBatch` / `InsertGroupMembershipsBatch` sind `private` hinter dem 2.4k-Z. `SyncAllAsync` mit Live-Graph + DB. File-Split bringt sowohl Wartbarkeit als auch testbare Abgrenzung.
- **Naechster Schritt:** Z9-1.1 Boundary-/Split-Inventur (oeffentliche API, Aufrufer, interne Achsen Graph-Zugriff/DB-Batch/Orchestrierung/DepartmentLead/Import). Reine Inventur, kein Code-Change.
- **Geplante Slices:** Z9-1.1 Inventur → Z9-1.2 Extract-Plan → Z9-2.1 `SyncAllAsync`-Zuschnitt → Z9-2.2 Graph-Adapter → Z9-2.3 DB-Batch-Modul → Z9-3 Coverage.
- **Z8 Abschluss-Stand:** #1/#2/#3/#4/#7 gepushed; #5 false positive; #8 deferred; Z8-4 Coverage abgeschlossen; EntraDirectorySync-Batch-Helfer-Coverage formal nach Z9-3 verschoben.
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
