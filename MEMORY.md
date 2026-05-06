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

- **Zyklus 11 abgeschlossen** (2026-05-05..06) — Admin-/Master-Data-Listen-Vertraege vollstaendig umgesetzt. **Kein aktiver Zyklus.** Detail in `CODE_REVIEW.md` § „Abgeschlossener Zyklus 11".
- **Stand:** F1, F2, F3 alle done — `AdminListPageDto<T>` (P1) live fuer 5 Master-Data/Lookup-Endpunkte (F1) plus 7 scoped Builder-Endpunkte (F3); `CursorPageDto<T>` (P2) + Keyset-Pagination live fuer beide Audit-Streams (F2). Typed FE-Wrapper `services/api/adminList.ts` und `services/api/cursorPage.ts`; Builder-Hooks lesen pragmatisch `page.items` mit `limit: 200`.
- **Naechster Schritt:** Codex entscheidet, welcher der nach Z11 vorgesehenen Folgekandidaten als naechster aktiver Zyklus eroeffnet wird (A Identity-Listen, C `/admin/directory/identities`, C Gaps/Pending Split, E Notification-Templates, F Rotation, G Runtime-Sub-Resources, H Startable, oder ein eigenstaendiger Builder-UI-Slice fuer URL-Filterzustand). **Aus diesem Slice heraus keinen neuen Zyklus eroeffnen.**
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.
- **Vorher lesen:** `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `TODO.md`, `CODEX_SYNC.md`, `CLAUDE_CONTROL.md`

## Active Risks / Watchouts

- `WorkflowLifecycleService` ist nach Z7 die Commit-Grenze fuer Create/Form/Approval/Task; neue Lastarbeit darf diese Grenze nicht wieder aufweichen.
- Rotation-Task-Routing bleibt geteilt: `rot:*` geht direkt ueber `_rotationRepository`, `wf:*` ueber den Lifecycle-Service.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen Builds und Tests blockieren.
- Z11-F1, Z11-F2, Z11-F3 sind als abgeschlossene FE-Trigger in `FRONTEND_TODO.md` vermerkt — kein offener Z11-Trigger mehr.
- P2-Cursor ist opaque (Base64 ueber `(occurredAt, id)`); FE darf den String nie zerlegen.
- `limit` Default 50/Max 200 mit Server-Clamp gilt fuer P1 und P2 gleichermassen — bei Endpunkt-Anpassung mitziehen.
- Sichtbare Paging-/Filter-UI fuer Builder-Inspector (Filterzustand in URL-Query) ist bewusste Restgrenze von Z11-F3; bleibt eigenstaendiger UI-Slice ausserhalb Z11.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
