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

- **Aktiver Zyklus 14** (Stand 2026-05-07) — Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung. Reiner Review-/Planungszyklus, keine Umsetzung. Z14-1.1 Inventur abgeschlossen (Override-Punkte `useRoleAwareNavigation.ts:253` und `roleModel.ts:210`; vier Sicht-Konsumenten betroffen; Header/Aktionen/Routen-Guards capability-getrieben und nicht betroffen). Z14-1.2 Vertrags-/UX-Entscheidung abgeschlossen (Begriffsraster Rolle/Persona/aktive Ansicht; Vorzugsrichtung: Persona-Switcher mit Vorrangs-Default + Persistenz, Admin-Vorrang als Default-Regel, Fallback `generic`; Vertragspflichten Default/Persistenz/Fallback/Schalter/Login-Routing getrennt; Capability-Schicht unberuehrt; Andock an `loadDashboardInsights`, `queryKeys.dashboard.insights` und drei `DashboardOverview`-Schalter beschrieben). Naechster Schritt: Z14-1.3 Slice-Plan Folgezyklus. Detail in `CODE_REVIEW.md` § „Aktiver Zyklus 14" und §§ Z14-1.1 / Z14-1.2 Kernergebnis.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- Z14 ist Doku-only — kein Code an `web/src/auth/roleModel.ts`, `web/src/navigation/useRoleAwareNavigation.ts` oder Dashboard-Bloecken anfassen, bevor Z14-1.3 den Slice-Plan geschnitten hat.
- Begriffstrennung verbindlich (Z14-1.2): **Rolle** (Berechtigung, getrennt) ≠ **Persona** (deterministischer Default aus Vorrangskette `admin > hr > manager > worker > reader > generic`) ≠ **aktive Ansicht** (vom Nutzer beeinflusste, persistierte Praeferenz mit Default + Fallback). Nicht vermischen.
- Z14 betrifft **Sicht**, nicht **Rechte**: Capabilities, Header-Navigation, Schnellaktionen und Routen-Guards bleiben unangetastet — der `generic`-Kollaps sitzt nur im Dashboard-Body, im Insight-Lader, im Seitenkopf und im Login-Routing.
- Z14-1.3 muss die zwei Override-Stellen aus Z14-1.1 **getrennt** versorgen (Dashboard-Sicht *und* Login-Routing aus derselben Quelle „aktive Ansicht"). Ein Persona-Switcher allein reicht nicht automatisch fuer das Routing.
- Z14-1.3 Andockpunkte ohne Logik-Aenderung: `loadDashboardInsights(persona, options)`, `queryKeys.dashboard.insights(persona, definitionKey)`, drei `DashboardOverview`-Schalter (`isAdminDashboard`, `supportsProcessTypeFilter`, Manager-Liste). Quelle wechselt, Logik in den Loadern bleibt; `loadGenericInsights` bleibt als Fallback erhalten.
- Persistenz der aktiven Ansicht laut Vertrag in `localStorage` mit person-/user-gebundenem Schluessel; Hebung nach Backend ist ausserhalb Z14.
- `WorkflowLifecycleService` ist nach Z7 die Commit-Grenze fuer Create/Form/Approval/Task; neue Lastarbeit darf diese Grenze nicht aufweichen.
- DB-getriebene Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- P2-Cursor (Z11-F2) ist opaque — FE darf den String nie zerlegen.
- `limit` Default 50/Max 200 mit Server-Clamp gilt fuer P1 und P2 gleichermassen.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
