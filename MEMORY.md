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

- **Aktiver Zyklus: Z19 — Backend Full Review / Holistic Audit.** **Z19-S1 abgeschlossen 2026-05-11**: 3 HIGH-, 4 MEDIUM-, 4 LOW-Findings dokumentiert in `CODE_REVIEW.md` § „Aktiver Zyklus 19 — S1 Ergebnis". **Naechster Slice: Z19-S2** — Sweep-Timeout fuer `DirectorySyncHostedService` analog `RotationNotificationHostedService.SweepTimeout` (2h-`CancelAfter`). Empfehlung Codex-CLI: `--model claude-opus-4-7 --effort high`. Danach Z19-S3 (Schema-Paritaet `db/01_schema.sql` vs `db/manual/`) und Z19-S4 (`CancellationToken`-Propagation in Lifecycle/Runtime).
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- **Z19-Scope-Abgrenzung:** kein breiter Architektur-Umbau am Definition-/Runtime-/Automation-Layer, keine neuen FE-Findings (Z18 abgeschlossen), keine Berechtigungsmodell-Aenderungen ohne konkretes Risiko — sonst Slice-Drift.
- **DB-Drift-Pfad:** schemaaendernde Slices erzeugen weiterhin manuelle SQL-Helfer in `db/manual/`; Z19-S3 ist der priorisierte Folge-Slice, der einen Paritaets-Check gegen `db/01_schema.sql` einfuehrt. Bis dahin neue Schema-Aenderungen weiterhin explizit als `db/manual/<datum>_*.sql` ablegen.
- **Person ≠ Identity:** `personId` = fachlicher Anker, `directoryIdentityId` = Entra-Objekt-Anker — nie vermischen (in `KauthWorkflow/Domäne/Identity.md` verankert).
- `WorkflowLifecycleService` bleibt Commit-Grenze fuer Create/Form/Approval/Task — nicht aufweichen.
- DB-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- P2-Cursor (Z11-F2) opaque — FE nie zerlegen.
