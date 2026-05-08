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

- **Kein aktiver Zyklus offen.** Z18, FE-8 und der komplette Entra-Retrofit-Block aus `TODO.md` (A1, A2, A3, B, C) sind am 2026-05-08 abgeschlossen worden. Praktisch bedeutet das: bestehende Entra-Mitarbeitende lassen sich jetzt retroaktiv importieren, direkt im Verzeichnis sehen, auf der Mitarbeiterkarte nachpflegen und mit Entra-Stellen sauber in Abteilungen uebernehmen. Naechster Schritt: Codex priorisiert den naechsten Zyklus aus den verbleibenden repo-weiten Review-/Architekturthemen.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- **Rolle ≠ Persona ≠ aktive Ansicht** (aus Z15): bleibt verbindlich — Rechte, Capabilities, Header-Navigation und Routen-Guards nicht mit Persona-Steuerung vermengen.
- **Person ≠ Identity**: `personId` = fachlicher Anker, `directoryIdentityId` = Entra-Objekt-Anker — nie vermischen (in `KauthWorkflow/Domäne/Identity.md` verankert).
- `WorkflowLifecycleService` bleibt Commit-Grenze fuer Create/Form/Approval/Task — nicht aufweichen.
- DB-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- P2-Cursor (Z11-F2) opaque — FE nie zerlegen.
