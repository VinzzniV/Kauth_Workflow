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

- **Aktiver Zyklus:** Z8 — Skalierbarkeits- & Last-Haertung
- **Naechster Schritt:** Z8-3 (Sweep-/Dispatch-Resthebel #5/#7/#8), dann Z8-4 (Coverage).
- **Z8-2.3 done (2026-05-05):** `EntraDirectorySyncService.SyncAllAsync` ersetzt die per-Member-Schleife durch `UpsertDirectoryIdentitiesBatch` (Bulk-Upsert via `unnest(uuid[],text[],...)` + RETURNING fuer Id-Mapping) und `InsertGroupMembershipsBatch` (Bulk-Insert via `unnest(bigint[])` + ON CONFLICT DO NOTHING). Counts (`identitiesSynced`/`membershipsSynced`) zaehlen weiter pro valide-Member-Vorkommen, semantisch wie vorher. LQ2-Z3 (File-Split) bleibt deferred.
- **Z8-2.2 done (2026-05-05):** `RotationNotificationService.ExecuteDailySweepAsync` schleift mit `DispatchBatchSize=200` ueber `GetDispatchableRotationNotifications(limit, excludeIds)`; `ApplyRotationNotificationDispatchResults` macht Bulk-Metadata-SELECT (`id = ANY(@ids)`) und Bulk-UPDATE via `unnest`. Audit weiter pro Result, aber ohne Per-Item-SELECT.
- **Z8-2.1 done (2026-05-05):** `WorkflowCatalogService` ruft jetzt `repository.GetManagerCreatableDefinitionKeys()` einmalig (lazy) statt N×`IsManagerCreatableDefinition`.
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
