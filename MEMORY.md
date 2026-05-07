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

- **Aktiver Zyklus 14** (Stand 2026-05-07) — Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung. Reiner Review-/Planungszyklus, keine Umsetzung. Naechster Schritt: Z14-1.1 Inventur. Detail in `CODE_REVIEW.md` § „Aktiver Zyklus 14".
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- Z14 ist Doku-only — kein Code an `web/src/auth/roleModel.ts`, `web/src/navigation/useRoleAwareNavigation.ts` oder Dashboard-Bloecken anfassen, bevor Z14-1.2 die Vertrags-/UX-Entscheidung skizziert hat.
- Begriffstrennung Pflicht: **Rolle** (Berechtigung) ≠ **Persona** (abgeleitete Standard-Sicht) ≠ **aktive Ansicht** (was der Nutzer aktuell sehen will). Diese Trennung wird in Z14-1.2 definiert; bis dahin keine Begriffe vermischen.
- `WorkflowLifecycleService` ist nach Z7 die Commit-Grenze fuer Create/Form/Approval/Task; neue Lastarbeit darf diese Grenze nicht aufweichen.
- DB-getriebene Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- P2-Cursor (Z11-F2) ist opaque — FE darf den String nie zerlegen.
- `limit` Default 50/Max 200 mit Server-Clamp gilt fuer P1 und P2 gleichermassen.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
