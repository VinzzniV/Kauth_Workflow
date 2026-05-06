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

- **Aktiver Zyklus 12** (eroeffnet 2026-05-06) — Admin-Dashboard-Betriebsblock fuer Runtime-/System-Health-Signale. Z12 adressiert **zuerst App-/Runtime-Health** (API/DB/Directory/Mail + einfache Runtime-Metriken wie Prozess-Speicher, Uptime, Storage), **nicht** vollwertige Host-/VM-Metrik. Detail in `CODE_REVIEW.md` § „Aktiver Zyklus 12".
- **Slice-Reihenfolge (streng sequenziell):** ✅ Z12-1.1 done → Z12-1.2 Vertrags-Skizze (DTO + Schwellwerte + Abgrenzung App/Container/Host) → Z12-2.1 Backend Runtime-Health Endpoint + Service → Z12-2.2 Frontend Admin-Dashboard-Betriebsblock. Echte Host-/VM-Metrik (CPU/RAM/Disk Server) bleibt bewusst optionaler Folgeschritt nach Z12-2.2.
- **Z12-1.1 Ergebnis (2026-05-06):** Inventur in `CODE_REVIEW.md` § Z12-1.1 — heutige Health-Signale (`/health/*` + `admin-health-panel` mit Directory/Mail/Warnungen) sind heute nicht verbunden; App-/Runtime-Signale (Prozess-Uptime, Managed Heap, Working Set, Storage-Auslastung App-Schreibpfade) fehlen komplett; Drei-Domaenen-Modell App / Container / Host als Pflicht-Begriffsraster fuer Z12-1.2 fixiert; UI-Wording („Betriebsstatus", „API-Prozess: Managed Heap", „Schreibpfad der Anwendung") empfohlen, kein „Server"/„RAM frei"/„Disk frei" im App-Block.
- **Naechster Schritt:** Z12-1.2 beauftragen (Modell `opus`, Effort `high`) — Vertrags-Skizze DTO + Severity-Stufen + Schwellwerte + Domaenen-Tags pro Feld, FE-Andockpunkt am `admin-health-panel`. Reine Doku-Slice.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.
- **Vorher lesen:** `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `TODO.md`, `CODEX_SYNC.md`, `CLAUDE_CONTROL.md`, `web/README.md`, `KauthWorkflow/Betrieb/Setup.md`

## Active Risks / Watchouts

- **Z12-Watchout (zentral):** App-/Container-/Host-Semantik nicht vermischen. Wer in Z12 Host-Metriken (CPU/RAM/Disk Server, Container-Stats) mitnimmt, weicht den Scope auf. Drei-Domaenen-Modell aus Z12-1.1 (App / Container / Host) ist Pflicht-Raster fuer Z12-1.2 DTO-Felder.
- **Z12-Watchout:** Bestehenden Admin-Health-Begriff (`admin-health-panel` in `web/src/components/admin-config/AdminOverviewWorkspaceSection.tsx` plus `/health/live`, `/health/ready`, `/health` aus `api/API/Extensions/LifecycleApplicationExtensions.cs`) als Anker nutzen — keine konkurrierende zweite Betriebslogik erfinden. Bestehende Kacheln (Verzeichnis-Sync, Mail, Warnungen) bleiben semantisch wo sie sind; Z12 ergaenzt Anwendung/Abhaengigkeiten.
- **Z12-Watchout:** UI-Wording strikt App-bezogen: „API-Prozess: Managed Heap" / „Schreibpfad der Anwendung" — **nicht** „RAM frei" / „Disk frei" / „Server-Status". Ein „Server"-Block kommt erst im optionalen Host-/VM-Folgeschritt nach Z12-2.2.
- **Z12-Watchout:** Schwellwerte und Severity-Stufen (z. B. ok/warn/crit) gehoeren in Z12-1.2 (deklarativ), **nicht** in Z12-2.1 frei erfunden.
- **Z12-Watchout:** Z12-1.x sind reine Doku-Slices — kein Code, kein API-Vertrag, keine DB-Aenderung in der Vorbereitung.
- `WorkflowLifecycleService` ist nach Z7 die Commit-Grenze fuer Create/Form/Approval/Task; neue Lastarbeit darf diese Grenze nicht wieder aufweichen.
- Rotation-Task-Routing bleibt geteilt: `rot:*` geht direkt ueber `_rotationRepository`, `wf:*` ueber den Lifecycle-Service.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen Builds und Tests blockieren.
- P2-Cursor (Z11-F2) ist opaque (Base64 ueber `(occurredAt, id)`); FE darf den String nie zerlegen.
- `limit` Default 50/Max 200 mit Server-Clamp gilt fuer P1 und P2 gleichermassen — bei Endpunkt-Anpassung mitziehen.
- Sichtbare Paging-/Filter-UI fuer Builder-Inspector (Filterzustand in URL-Query) ist bewusste Restgrenze von Z11-F3; bleibt eigenstaendiger UI-Slice ausserhalb Z11/Z12.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
