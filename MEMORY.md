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

- **Zyklus 16 aktiv** (Stand 2026-05-08) — Z16-S1 + Z16-S2 + Z16-S3 abgeschlossen. Z16 im produktiven Kern fertig. Z16-S4 deferred. Naechster Zyklus: Codex priorisiert.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- **Begriffstrennung verbindlich:** Person = fachlicher Anker, `directoryIdentityId` = Entra-technische Identity — nie vermischen (Z16-Guardrail).
- **Rolle ≠ Persona ≠ aktive Ansicht** (aus Z15): bleibt verbindlich; Z16 ergaenzt `peopleDirectory`-Feature ohne das Z15-Modell zu beruehren.
- `WorkflowLifecycleService` bleibt Commit-Grenze fuer Create/Form/Approval/Task — nicht aufweichen.
- DB-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- P2-Cursor (Z11-F2) opaque — FE nie zerlegen.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
- Z16-S4 (Automation-Snapshot `snapshotAt`) ist deferred bis Produkt klaert, ob Snapshot in `workflow_automation_jobs` persistiert werden soll.
