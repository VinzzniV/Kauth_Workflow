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
- **Slice-Reihenfolge (streng sequenziell):** ✅ Z12-1.1 done → ✅ Z12-1.2 done → Z12-2.1 Backend Runtime-Health Endpoint + Service → Z12-2.2 Frontend Admin-Dashboard-Betriebsblock. Echte Host-/VM-Metrik (CPU/RAM/Disk Server) bleibt bewusst optionaler Folgeschritt nach Z12-2.2.
- **Z12-1.2 Ergebnis (2026-05-06):** Vertrags-Skizze in `CODE_REVIEW.md` § Z12-1.2 — `GET /admin/runtime-health` admin-only liefert `AdminRuntimeHealthDto` mit Sub-DTOs `application` (`processStartedAt`/`uptimeSeconds`/`managedHeapBytes`/`managedHeapHighThresholdBytes`/`workingSetBytes`/`threadPool?`), `dependencies` (`database`/`auth`/`mail` jeweils mit eigener Severity), `directory` (Sync-Status + `nextScheduledSyncAt` + `pendingImportsCount`) und `storage[]` (nur explizit konfigurierte App-Schreibpfade); pro Feld Domaenen-Tag `app|container|host|external`; Severity vier Stufen `ok/warning/critical/unknown`, `overallSeverity` = max ueber Sub-DTOs mit `unknown`-Neutralisierung fuer dev-sim/none-Auth und leere Storage-Liste; Schwellwerte deklarativ in § 3 (DB <1000 ms ok / 1000-3000 warn / >3000 crit; Entra OIDC analog mit 2000/5000; Mail aus mode+configurationStatus; Directory-Sync ueber 2x/5x Intervall; Managed Heap 75/90 %; Storage 80/90 %); FE-Andock im bestehenden `admin-health-panel` (`AdminOverviewWorkspaceSection.tsx`), kein zweiter Block, neue Kacheln Anwendung+Abhaengigkeiten+Schreibpfade vor/zwischen den Bestandskacheln.
- **Naechster Schritt:** Z12-2.1 beauftragen (Modell `sonnet`, Effort `medium..high`) — Backend Runtime-Health Endpoint + Service entlang § Z12-1.2. Schwellwerte 1:1 uebernehmen, nicht frei waehlen. Bestehende `/health/*`-Endpunkte unangetastet. Storage-Pfade kommen aus expliziter Konfiguration, nicht aus generischer Drive-Enumeration.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.
- **Vorher lesen:** `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `TODO.md`, `CODEX_SYNC.md`, `CLAUDE_CONTROL.md`, `web/README.md`, `KauthWorkflow/Betrieb/Setup.md`

## Active Risks / Watchouts

- **Z12-Watchout (zentral):** App-/Container-/Host-Semantik nicht vermischen. Wer in Z12 Host-Metriken (CPU/RAM/Disk Server, Container-Stats) mitnimmt, weicht den Scope auf. Drei-Domaenen-Modell aus Z12-1.1 (App / Container / Host) ist Pflicht-Raster fuer Z12-1.2 DTO-Felder.
- **Z12-Watchout:** Bestehenden Admin-Health-Begriff (`admin-health-panel` in `web/src/components/admin-config/AdminOverviewWorkspaceSection.tsx` plus `/health/live`, `/health/ready`, `/health` aus `api/API/Extensions/LifecycleApplicationExtensions.cs`) als Anker nutzen — keine konkurrierende zweite Betriebslogik erfinden. Bestehende Kacheln (Verzeichnis-Sync, Mail, Warnungen) bleiben semantisch wo sie sind; Z12 ergaenzt Anwendung/Abhaengigkeiten.
- **Z12-Watchout:** UI-Wording strikt App-bezogen: „API-Prozess: Managed Heap" / „Schreibpfad der Anwendung" — **nicht** „RAM frei" / „Disk frei" / „Server-Status". Ein „Server"-Block kommt erst im optionalen Host-/VM-Folgeschritt nach Z12-2.2.
- **Z12-Watchout:** Schwellwerte und Severity-Stufen sind in `CODE_REVIEW.md` § Z12-1.2 § 3 deklarativ fixiert (DB 1000/3000 ms; Entra 2000/5000 ms; Mail aus mode+configurationStatus; Directory 2x/5x Intervall; Managed Heap 75/90 %; Storage 80/90 %). Z12-2.1 darf sie **nicht** veraendern, nicht „erweitern" und nicht frei interpretieren — Aenderung ist ein Vertrags-Update in § Z12-1.2.
- **Z12-Watchout:** `application.workingSetBytes` und `threadPool.*` sind in Z12 nur informativ (Severity bleibt `ok`/`unknown`); kein Schwellwert, weil ohne Container-Sizing keine seriose Heuristik moeglich ist.
- **Z12-Watchout:** Storage-Pfade in `storage[]` kommen aus **expliziter Konfiguration** (Settings/Env), nicht aus generischer `DriveInfo.GetDrives()`-Enumeration — sonst rutscht „Disk-Free der VM" still in den App-Vertrag.
- **Z12-Watchout:** `unknown`-Aggregationsregel: `auth.severity = unknown` bei `mode = dev-sim`/`none` und leere `storage[]`-Liste duerfen `overallSeverity` nicht auf `unknown` ziehen — sonst zeigt Dev-Sim-Lokal staendig „unbekannt".
- **Z12-Watchout:** bestehende offene `/health/live`/`/health/ready`/`/health` bleiben in Z12-2.1 unveraendert (Caddy-/Container-Probes haengen daran). Der neue Endpoint ist ein **zweiter** Pfad fuer Admin-UI, kein Ersatz.
- **Z12-Watchout:** ausserhalb Z12-2.1/2.2: keine Host-/VM-Metrik, keine Prometheus/Grafana/`node_exporter`-Pipeline, keine Historisierung/Trends, keine Severity-Alerts/Notifications, kein aktiver Mail-Send-Probe.
- `WorkflowLifecycleService` ist nach Z7 die Commit-Grenze fuer Create/Form/Approval/Task; neue Lastarbeit darf diese Grenze nicht wieder aufweichen.
- Rotation-Task-Routing bleibt geteilt: `rot:*` geht direkt ueber `_rotationRepository`, `wf:*` ueber den Lifecycle-Service.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal an PostgreSQL auf `127.0.0.1:26432`.
- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen Builds und Tests blockieren.
- P2-Cursor (Z11-F2) ist opaque (Base64 ueber `(occurredAt, id)`); FE darf den String nie zerlegen.
- `limit` Default 50/Max 200 mit Server-Clamp gilt fuer P1 und P2 gleichermassen — bei Endpunkt-Anpassung mitziehen.
- Sichtbare Paging-/Filter-UI fuer Builder-Inspector (Filterzustand in URL-Query) ist bewusste Restgrenze von Z11-F3; bleibt eigenstaendiger UI-Slice ausserhalb Z11/Z12.

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` bleibt das stabile Plattform-Zielbild.
