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

- **Aktiver Zyklus:** Zyklus 10 (eroeffnet 2026-05-05) — Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege, Query-Kontrakt-Risiken. Reiner Review-/Planungszyklus, keine Umsetzung in Z10.
- **Z10 Bedeutung:** Admin- und Master-Data-Listen laufen heute zu grossen Teilen ohne Pagination, Server-Suche und stabilen Sort-Vertrag (sichtbar an Hotspot #6 aus Z8). Das ist heute noch nicht akut, wird aber mit wachsendem Bestand spuerbar — Listen langsam, Suche unvollstaendig, FE faengt das im Browser ab. Z10 zieht den Vertrag vor das Wachstum.
- **Naechster Schritt:** Z10-1.1 Inventur (HIGH/high/opus) — alle Admin-/Master-Data-/Directory-Read-Endpunkte ohne Pagination/Suche/Sort-Vertrag dokumentieren. Vor jeder Umsetzung erst Z10-1.2 (Vertrags-Skizze) und Z10-1.3 (Slice-Plan).
- **Z10 Leitplanke:** keine Code-Umsetzung in Z10; kein praeventives FE-TODO. FE-Folgen werden erst beim sichtbar gewordenen Trigger eingetragen.
- **Schreibregel (verbindlich, neu in Z10):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.
- **Z9 Endzustand (Referenz):** `EntraDirectorySyncService` 2591 → 1563 Z., Graph- und DB-Sync-Operations hinter `IEntraGraphClient`/`IEntraDirectorySyncOperations` unter `api/API/Services/Directory/`. Coverage gesetzt; Connection-Factory-Schnitt bewusst ausserhalb Z9.
- **Vorher lesen:** `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `TODO.md`, `CODEX_SYNC.md`, `CLAUDE_CONTROL.md`

## Active Risks / Watchouts

- Z10 ist Planungszyklus — keine Code-Umsetzung als done markieren. Vertrags-Skizze nicht implementieren, sondern aufschreiben.
- FE-Folgen aus Z10 erst eintragen, wenn die Inventur sie sichtbar macht. Kein praeventives FE-TODO.
- `WorkflowLifecycleService` ist nach Z7 die Commit-Grenze fuer Create/Form/Approval/Task; neue Lastarbeit darf diese Grenze nicht wieder aufweichen.
- Rotation-Task-Routing bleibt geteilt: `rot:*` geht direkt ueber `_rotationRepository`, `wf:*` ueber den Lifecycle-Service.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen Builds und Tests blockieren.

## Temporary Notes

- Frontend-Stand nach FE-25..FE-31 ist stabil; Z10 erzeugt erst dann FE-Arbeit, wenn aus der Vertrags-Skizze konkrete API-Vertragsaenderungen folgen.
- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
