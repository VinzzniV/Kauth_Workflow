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

- **Zyklus 14 abgeschlossen** (Stand 2026-05-07) — Mehrrollen-Persona-Kollisionen vollstaendig durchgeplant: Z14-1.1 Inventur done, Z14-1.2 Vertrag done, Z14-1.3 Slice-Plan done. Kein aktiver Zyklus. Z14-1.3 Ergebnis (Detail in `CODE_REVIEW.md` § Z14-1.3): drei Umsetzungsslices in fester Reihenfolge — Slice I „aktive Ansicht" als reines Datenmodell + Persistenz + Fallback ohne Sicht-Konsumenten; Slice II zwei Override-Stellen aus Z14-1.1 (`useRoleAwareNavigation.ts:253` + `roleModel.ts:210`) gemeinsam auf die neue Quelle umstellen; Slice III sichtbarer Persona-Switcher nur fuer `hasMultipleRoles === true`. Pro Slice empfohlen `claude-sonnet-4-6` + `--effort medium`. Loader, Query-Key-Aufbau und die drei `DashboardOverview`-Schalter bleiben unangetastet; `loadGenericInsights` bleibt Fallback-Pfad.
- **Naechster Folgezyklus** (Arbeitstitel Zyklus 15): Codex eroeffnet die Implementierung mit Slice I aus Z14-1.3 (Datenmodell-Hook fuer „aktive Ansicht"). Vor Eroeffnung Modell/Effort per CLI-Flag erzwingen.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- Folgezyklus-Implementierung darf erst starten, wenn Codex Slice I beauftragt — nicht aus Z14 vorgreifen. Z14 hat selbst keinen Code geaendert.
- Begriffstrennung verbindlich (Z14-1.2): **Rolle** (Berechtigung) ≠ **Persona** (deterministischer Default aus Vorrangskette `admin > hr > manager > worker > reader > generic`) ≠ **aktive Ansicht** (vom Nutzer beeinflusste, persistierte Praeferenz mit Default + Fallback). Nicht vermischen.
- Folgezyklus betrifft **Sicht**, nicht **Rechte**: Capabilities, Header-Navigation, Schnellaktionen und Routen-Guards bleiben unangetastet — der `generic`-Kollaps sitzt nur im Dashboard-Body, im Insight-Lader, im Seitenkopf und im Login-Routing.
- Slice II muss die zwei Override-Stellen aus Z14-1.1 **gemeinsam** versorgen (Dashboard-Sicht *und* Login-Routing aus derselben Quelle „aktive Ansicht"). Ein Persona-Switcher allein heilt das Routing nicht.
- Slice I ist sicht-konsumentenfrei: `useRoleAwareNavigation.ts:253`, `roleModel.ts:210`, Loader, Query-Key, drei `DashboardOverview`-Schalter und `loadGenericInsights` werden in Slice I **nicht** angefasst.
- SSR-/Hydration-Risiko in Slice I: `localStorage`-Lesen Client-only, Default beim Erststand; Mitigation gehoert in die Slice-Tests.
- Persistenz der aktiven Ansicht in `localStorage` mit person-/user-gebundenem Schluessel `kauth.activeView.<personId>`; Hebung nach Backend (Cross-Device) bleibt ausserhalb des Folgezyklus.
- `WorkflowLifecycleService` ist nach Z7 die Commit-Grenze fuer Create/Form/Approval/Task; neue Lastarbeit darf diese Grenze nicht aufweichen.
- DB-getriebene Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- P2-Cursor (Z11-F2) ist opaque — FE darf den String nie zerlegen.
- `limit` Default 50/Max 200 mit Server-Clamp gilt fuer P1 und P2 gleichermassen.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
