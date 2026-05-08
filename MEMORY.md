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

- **Zyklus 15 aktiv** (Stand 2026-05-08) — Implementierungszyklus Mehrrollen-Persona. Z15-S1 abgeschlossen: `useActiveView`-Hook live (`web/src/hooks/useActiveView.ts`), 24 Tests gruen. Naechster Schritt: Z15-S2 (zwei Override-Stellen `useRoleAwareNavigation.ts:253` + `roleModel.ts:210` gemeinsam umstellen). Z15-S3 (Switcher) danach.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- **Z15-S2 Pflicht:** die zwei Override-Stellen (`useRoleAwareNavigation.ts:253` + `roleModel.ts:210`) **gemeinsam** auf `useActiveView` umstellen — nicht einzeln, sonst laufen Sicht und Routing auseinander.
- **Begriffstrennung verbindlich:** **Rolle** (Berechtigung) ≠ **Persona** (deterministischer Default aus Vorrangskette `admin > hr > manager > worker > reader > generic`) ≠ **aktive Ansicht** (persistierte Praeferenz, steuert nur Sicht). Nicht vermischen.
- **Sicht, nicht Rechte:** Capabilities, Header-Navigation, Schnellaktionen und Routen-Guards bleiben in allen Z15-Slices unangetastet.
- **`useActiveView` erwartet einen stabilen `capabilities`-Ref:** Im `useEffect`-Dependency-Array koennte das Objekt auf jeder Render-Runde neu erstellt werden. Im ProductionProvider ist `capabilities` per `useMemo` stabil — bei Z15-S2 Provider-Tests beachten.
- Persistenz-Schluessel: `kauth.activeView.<personId>`; Cross-Device-Hebung bleibt ausserhalb Z15.
- `WorkflowLifecycleService` bleibt Commit-Grenze fuer Create/Form/Approval/Task — nicht aufweichen.
- DB-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- P2-Cursor (Z11-F2) opaque — FE nie zerlegen.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
