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

- **Kein aktiver Zyklus offen.** Z18 (Frontend Full Review) ist am 2026-05-08 vollstaendig abgeschlossen worden; alle 9 Findings sind umgesetzt. FE-8 (`approval_task_template_key` → `approval_spec_key`) ist ebenfalls abgeschlossen. `FRONTEND_TODO.md` hat aktuell keine offenen Items mehr. Naechster Schritt: Codex priorisiert den naechsten Zyklus aus den verbleibenden repo-weiten Review-/Architekturthemen.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- **Rolle ≠ Persona ≠ aktive Ansicht** (aus Z15): bleibt verbindlich — Rechte, Capabilities, Header-Navigation und Routen-Guards nicht mit Persona-Steuerung vermengen.
- **Person ≠ Identity**: `personId` = fachlicher Anker, `directoryIdentityId` = Entra-Objekt-Anker — nie vermischen (in `KauthWorkflow/Domäne/Identity.md` verankert).
- `WorkflowLifecycleService` bleibt Commit-Grenze fuer Create/Form/Approval/Task — nicht aufweichen.
- DB-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- P2-Cursor (Z11-F2) opaque — FE nie zerlegen.
