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

- **Kein aktiver Zyklus.** Zyklus 10 formal abgeschlossen (2026-05-05) — reiner Review-/Planungszyklus, alle drei Slices done. Naechster Schritt: Eroeffnung Umsetzungszyklus (vorgeschlagen Z11) auf Basis von `CODE_REVIEW.md` § Z10-1.3.
- **Z11 Slice-Reihenfolge (Vorschlag):** **F1** P1-Hull einfuehren + B Master-Data/Lookups (`/departments`, `/roles`, `/admin/master-data/*`) → **F2** P2-Hull einfuehren + Audit-Streams (`/admin/auth/audit`, `/admin/directory/audit`) → **F3** P1 ausrollen + D Builder-Tabs (sieben scoped Endpunkte). Bewusst noch nicht in Z11: A Identity-Listen, C Identities, C Gaps/Pending Split, E/F/G/H — Begruendung in `CODE_REVIEW.md` § Z10-1.3.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.
- **Vorher lesen:** `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `TODO.md`, `CODEX_SYNC.md`, `CLAUDE_CONTROL.md`

## Active Risks / Watchouts

- `WorkflowLifecycleService` ist nach Z7 die Commit-Grenze fuer Create/Form/Approval/Task; neue Lastarbeit darf diese Grenze nicht wieder aufweichen.
- Rotation-Task-Routing bleibt geteilt: `rot:*` geht direkt ueber `_rotationRepository`, `wf:*` ueber den Lifecycle-Service.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen Builds und Tests blockieren.
- FE-Folgen aus Z10-Vertrags-Skizze erst eintragen, wenn aus Z11-Umsetzung konkrete API-Vertragsaenderungen folgen. Kein praeventives FE-TODO.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
