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

- **Aktiver Zyklus:** keiner. Z9 abgeschlossen 2026-05-05 (Split + Coverage). Nachfolge-Zyklus offen — Trigger-getrieben.
- **Z9 Endzustand:** `EntraDirectorySyncService` 2591 → 1563 Z., Graph- und DB-Sync-Operations hinter `IEntraGraphClient`/`IEntraDirectorySyncOperations` unter `api/API/Services/Directory/`. Coverage: Unit-Tests `EntraDirectorySyncServiceTests` (Stub-Pfade ConnectionString/MissingCredentials/GraphFailed, 3/3 gruen) + Integration-Tests `EntraDirectorySyncOperationsIntegrationTests` (Upsert-Insert/Update+Dedup, Membership-Insert+Idempotenz, Empty-Array-No-Op; Fixture-Gating wie Z8-4.1).
- **Naechster Schritt:** offen — entweder anlassgetrieben neuer Zyklus (z. B. #6 Pagination wenn FE-Trigger, #8 Rotation-Regeneration wenn Last-Trigger) oder Codex-Priorisierung.
- **Z9-Leitplanke fuer Folge:** `EntraDirectorySyncService` oeffnet weiterhin direkt `NpgsqlConnection`. Tiefere Unit-Coverage der `RunGroupSyncAsync`/`RunDirectoryProjectionAsync`-Pfade verlangt einen Connection-Factory-Schnitt — bewusst nicht in Z9.
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
