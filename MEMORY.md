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

- **Aktiver Zyklus 13** (eroeffnet 2026-05-06) — Echte Linux-Host-/VM-Metriken im Admin-Runtime-Health-Block. Z13-1 done (Zykluseroeffnung/Scope). Z13-2 offen (Implementierung). Detail in `CODE_REVIEW.md` § „Aktiver Zyklus 13".
- **Naechster Schritt:** Z13-2 implementieren: `HostHealthDto` + `AdminRuntimeHealthService`-Erweiterung + FE-Host-Kachel + `LifecycleRuntimeSettings` (3 neue Env-Variablen) + `compose.prod.yml` Volumes + `scripts/start-vm.sh dev` Aktivierung + Tests.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- **Z13-Watchout:** `host: null` wenn nicht aktiviert oder nicht Linux — kein Fehlerwerfen, sauberes Fehlen. Load Average ehrlich als `loadAverage1m` benennen, nie als `cpuPercent`.
- **Z13-Watchout:** App-Block bleibt unveraendert — kein Umbenennen, kein Verschieben bestehender Kacheln. Nur neuen Host-Bereich ergaenzen.
- **Z13-Watchout:** `HOST_RUNTIME_HEALTH_ENABLED` nicht setzen = Host-Block bleibt leer. Windows-`start.ps1 dev` darf dadurch nicht kaputtgehen.
- **Z13-Watchout:** Volume-Mounts in `compose.prod.yml` sind readonly — kein Schreiben in den Host-FS. Nur aktiviert wenn Env-Var gesetzt.
- `WorkflowLifecycleService` ist nach Z7 die Commit-Grenze fuer Create/Form/Approval/Task; neue Lastarbeit darf diese Grenze nicht aufweichen.
- DB-getriebene Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- P2-Cursor (Z11-F2) ist opaque — FE darf den String nie zerlegen.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
- Z13-2 darf `ComputeOverallSeverity` anpassen um Host-Severity einzubeziehen — aber bestehende Tests duerfen dabei nicht brechen; `host`-Parameter wird als nullable hinzugefuegt.
