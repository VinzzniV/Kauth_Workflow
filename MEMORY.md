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

- **Zyklus 15 abgeschlossen** (Stand 2026-05-08) — Implementierungszyklus Mehrrollen-Persona vollstaendig umgesetzt: Z15-S1 Hook/Persistenz done, Z15-S2 Override-Stellen done, Z15-S3 Persona-Switcher done. Kein aktiver Folgezyklus offen.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- **Begriffstrennung verbindlich:** **Rolle** (Berechtigung) ≠ **Persona** (deterministischer Default) ≠ **aktive Ansicht** (persistierte Praeferenz, steuert nur Sicht). Nicht vermischen.
- **Sicht, nicht Rechte:** Die Z15-Logik aendert nur die sichtbare Persona. Capabilities, Header-Navigation, Schnellaktionen und Routen-Guards bleiben unveraendert.
- **Storage-Schluessel ist `kauth.activeView.<username>`** (kein `personId` im `/me`-Modell; `username` ist die stabilste user-gebundene Identitaet); Cross-Device-Hebung bleibt ausserhalb Z15.
- `WorkflowLifecycleService` bleibt Commit-Grenze fuer Create/Form/Approval/Task — nicht aufweichen.
- DB-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- P2-Cursor (Z11-F2) opaque — FE nie zerlegen.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
