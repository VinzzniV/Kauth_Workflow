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

- **Aktiver Zyklus: Z19 — Backend Full Review / Holistic Audit (eroeffnet 2026-05-11).** Doku-/Review-Slice analog zu Z18 (Frontend Full Review). **Naechster Slice: Z19-S1**, Audit-Pass ueber `api/API/Endpoints`, `api/API/Repositories`, `api/API/Services`, `Authorization/`, `Auth/`, `Services/Directory/`, Background-/Sweep-Jobs, Schema-/Migrations-Hygiene (insb. `db/manual/`-Workflow) und Test-Coverage. Liefert priorisierte Findings (HIGH/MEDIUM/LOW). Empfehlung Codex-CLI: `--model claude-opus-4-7 --effort high`. Detail in `CODE_REVIEW.md` § „Aktiver Zyklus 19" und `TODO.md`.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- **Z19-Scope-Abgrenzung:** kein breiter Architektur-Umbau am Definition-/Runtime-/Automation-Layer, keine neuen FE-Findings (Z18 abgeschlossen), keine Berechtigungsmodell-Aenderungen ohne konkretes Risiko — sonst Slice-Drift.
- **DB-Drift-Pfad:** schemaaendernde Slices erzeugen aktuell manuelle SQL-Helfer in `db/manual/` (zuletzt `approval_spec_key` + `directory_identities.job_title`). Z19-S1 muss diesen Workflow explizit benennen, nicht stillschweigend uebersehen.
- **Person ≠ Identity:** `personId` = fachlicher Anker, `directoryIdentityId` = Entra-Objekt-Anker — nie vermischen (in `KauthWorkflow/Domäne/Identity.md` verankert).
- `WorkflowLifecycleService` bleibt Commit-Grenze fuer Create/Form/Approval/Task — nicht aufweichen.
- DB-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- P2-Cursor (Z11-F2) opaque — FE nie zerlegen.
