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

- **Aktiver Zyklus 11** (eroeffnet 2026-05-05) — Admin-/Master-Data-Listen-Vertraege in Umsetzung. Reiner Umsetzungszyklus auf Basis Z10-1.3. Detail in `CODE_REVIEW.md` § „Aktiver Zyklus 11".
- **Slice-Reihenfolge (verbindlich):** **Z11-F1** P1-Hull `AdminListPageDto<T>` einfuehren + B Master-Data/Lookups (`/departments`, `/roles`, `/admin/master-data/{departments,positions,responsibilities}`) — opus/high → **Z11-F2** P2-Hull `CursorPageDto<T>` einfuehren + Audit-Streams (`/admin/auth/audit`, `/admin/directory/audit`) — sonnet/medium..high → **Z11-F3** P1 ausrollen + D Builder-Tabs (sieben scoped Endpunkte) — opus/medium..high. Streng sequenziell; F3 setzt P1 aus F1 voraus.
- **Pro Slice eine Hull-Familie**; kein Mischen P1/P2; kein Schreibpfad; keine Composite-DTO-Umbauten. FE-Folgen sind Konsequenz, kein praeventives FE-TODO — Eintrag in `FRONTEND_TODO.md` mit Trigger F1/F2/F3 erst beim Start des jeweiligen Slices.
- **Bewusst nicht in Z11:** A Identity-Listen, C `/admin/directory/identities`, C Gaps/Pending Split, E Notification-Templates, F Rotation, G Runtime-Sub-Resources, H Startable — Begruendungen in `CODE_REVIEW.md` § Z10-1.3.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.
- **Vorher lesen:** `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `TODO.md`, `CODEX_SYNC.md`, `CLAUDE_CONTROL.md`

## Active Risks / Watchouts

- `WorkflowLifecycleService` ist nach Z7 die Commit-Grenze fuer Create/Form/Approval/Task; neue Lastarbeit darf diese Grenze nicht wieder aufweichen.
- Rotation-Task-Routing bleibt geteilt: `rot:*` geht direkt ueber `_rotationRepository`, `wf:*` ueber den Lifecycle-Service.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen Builds und Tests blockieren.
- FE-Folgen aus Z10-Vertrags-Skizze erst beim Start des jeweiligen Z11-Slices in `FRONTEND_TODO.md` eintragen (Trigger F1/F2/F3), nicht praeventiv und nicht als Sammeleintrag.
- P2-Cursor ist opaque (Base64 ueber `(occurredAt, id)`); FE darf den String nie zerlegen.
- `limit` Default 50/Max 200 mit Server-Clamp gilt fuer P1 und P2 gleichermassen — bei Endpunkt-Anpassung mitziehen.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
