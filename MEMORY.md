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
- **Naechster Schritt:** Z9-2.1 Pre-Cleanup + `SyncAllAsync`-Phasen-Strukturierung gemaess `CODE_REVIEW.md` § Z9-1.2. Konkret in 2.1: `SyncDepartmentLeadAssignmentsFromDirectory` + 5 Sub-Helfer + zugehoeriger Reflection-Test (`PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs:103-105`) loeschen; tote Single-Row-Helfer `UpsertDirectoryIdentity` (1469) und `InsertGroupMembership` (1511) loeschen (in Z9-1.2 als ohne Aufrufer im Repo verifiziert); `SyncAllAsync` in `RunGroupSyncAsync` / `RunDirectoryProjectionAsync` / `RunActivationAsync` zerlegen, **noch in derselben Datei**. Kein File-Move in 2.1.
- **Geplante Slices:** Z9-1.1 Inventur (done) → Z9-1.2 Extract-Plan (done) → Z9-2.1 Pre-Cleanup + Phasen-Strukturierung → Z9-2.2 Graph-Adapter unter `api/API/Services/Directory/` → Z9-2.3 DB-Sync-Operations-Modul unter `api/API/Services/Directory/` → Z9-3 Coverage (Integration-Tests Batch-Helfer + Unit-Tests Orchestrator gegen Graph-/Ops-Stubs).
- **Z9-1.2 Kernentscheidungen:** DepartmentLead-Resolver wird **geloescht**, nicht isoliert (kein Konservieren von totem Code in neuem Modul). Single-Row-Helfer ebenfalls geloescht (verifiziert dead). Admin-Read/Write- und Import-Pfad bleiben in der alten Datei. Kein neuer EventLog-Wrapper, kein Repository/Connection-Factory in Z9. Coverage erst in Z9-3, nicht pro Slice.
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
