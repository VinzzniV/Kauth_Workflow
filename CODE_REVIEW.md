# Code Review — kauth_workflow

## Zweck

- aktive technische Review-Priorisierung
- Begruendung fuer den naechsten Arbeitszyklus
- kompakter Status fuer Mensch und KI

## Primaerquelle fuer

- aktuellen Review-Fokus
- Reihenfolge der Nacharbeit
- offene zyklusuebergreifende technische Befunde

## Nicht verwenden fuer

- kurzfristige Session-Notizen
- tiefes Slice-fuer-Slice-History-Studium abgeschlossener Zyklen

Dafuer sind `MEMORY.md`, `CODEX_SYNC.md` und `CODE_REVIEW_ARCHIVE.md` zustaendig.

## Wann aktualisieren

- wenn ein neuer Review-Zyklus eroeffnet wird
- wenn sich Priorisierung oder Folge-Slices aendern
- wenn ein aktiver Zyklus abgeschlossen wird

## Verwandte Dateien

- `TODO.md`
- `MEMORY.md`
- `CODE_REVIEW_ARCHIVE.md`
- `KauthWorkflow/Stand/Code-Review-Status.md`
- `KauthWorkflow/Architektur/Migrationspfad.md`

---

## Schreibregel fuer Reviews und Findings (verbindlich)

Jedes Review-Finding und jeder Slice in dieser Datei muss neben dem technischen Befund in kurzen Saetzen erklaeren:

- **Was bedeutet das praktisch?** — was ein normal verstaendlicher Leser im Alltag merkt (z. B. „Listen werden langsam, sobald viele Eintraege da sind", „Admin-UI zeigt nicht alles, was wirklich existiert", „Sync schlaegt still fehl").
- **Warum lohnt es sich, das anzugehen?** — der konkrete Anlass oder das Risiko, nicht nur „technische Schuld".
- **Was wird dadurch besser, sicherer, schneller oder wartbarer?** — der erwartete Nutzen, mit dem die Priorisierung begruendet ist.

Reine Technik-Beschreibung ohne Nutzen-/Bedeutung-Erklaerung ist nicht ausreichend. Die Regel gilt fuer alle neuen Zyklen, fuer einzelne Befunde und fuer den jeweils gefuehrten Slice-Plan. Bei zyklusuebergreifend offenen Befunden reicht ein kurzer Hinweis, warum sie aktuell nicht angegangen werden.

Diese Regel ist auch in `CLAUDE_CONTROL.md` als Arbeits-Pflicht fuer Claude unter Codex-Orchestrierung verankert.

---

**Stand**: 2026-05-06 — **Zyklus 12 abgeschlossen**: Admin-Dashboard-Betriebsblock fuer Runtime-/System-Health-Signale. Alle vier Slices done: Z12-1.1 (Inventur) + Z12-1.2 (Vertrags-Skizze) + Z12-2.1 (Backend Runtime-Health Endpoint) + Z12-2.2 (Frontend Betriebsblock). `GET /admin/runtime-health` backend-seitig implementiert; `AdminOverviewWorkspaceSection` zeigt neuen Betriebsblock (API-Prozess, Abhaengigkeiten, Schreibpfade) mit Severity-Badge + `overallSeverity`. Host-/VM-Metrik bleibt bewusst optionaler Folgeschritt. Zyklus 11/10/9/8 abgeschlossen. Kein aktiver Zyklus.
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..03 Zyklus 2–5; 2026-05-03..04 Zyklus 6; 2026-05-05 Zyklus 7; 2026-05-05 Zyklus 8 abgeschlossen; 2026-05-05 Zyklus 9 abgeschlossen; 2026-05-05 Zyklus 10 abgeschlossen; 2026-05-05 Zyklus 11 eroeffnet) + Codex-Fallback (2026-05-05 Z11-F1 Abschluss waehrend Claude-Rate-Limit) + Claude (2026-05-06 Z11-F2 Abschluss; 2026-05-06 Z11-F3 Abschluss = Z11 vollstaendig geschlossen; 2026-05-06 Z12 eroeffnet; 2026-05-06 Z12-1.1 Abschluss; 2026-05-06 Z12-1.2 Abschluss; 2026-05-06 Z12-2.1 Abschluss; 2026-05-06 Z12-2.2 Abschluss = Z12 vollstaendig geschlossen).

---

## Aktuelle Gesamtbewertung

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| Backend-Architektur | **A-** | Repository-Monolith reduziert; Lifecycle-Service nach Z7 echte Commit-Grenze fuer zentrale Runtime-Mutationen; groesste Resthebel liegen jetzt bei Skalierbarkeit und Lastpfaden |
| Datenbankdesign | **A-** | Solides Schema, gute Constraints |
| Auth & Berechtigungen | **B+** | Permission-Audit hat Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | **B+** | RotationTaskRegenerationEngine als pure Domain-Engine; HQ5-Hooks getestet; Sweep-Timeout |
| Frontend-Architektur | **B+** | Builder + Listen-Workspaces refactored; Split-Views; Karten-/Tabellenmodus; AdminConfig-Bundle-Refactor |
| Testbarkeit | **B** | Testcontainers + Integration-Tests; Lifecycle-Service hat eine eigene Service-Testdatei fuer Routing, Rollback und Automation-Scope |
| Skalierbarkeit | **B-** | Mehrere Listen-, Sweep- und Dispatch-Pfade sind noch Kandidaten fuer SQL-Pushdown, Pagination oder N+1-Abbau |
| Sicherheit | **B+** | `/client/log-events` rate-limited; dev-sim-Guard hard-throw |
| Lesbarkeit | **B+** | Konventionen durchgaengig; grobe Monolithen reduziert, Resthebel liegen weniger in Benennung als in Hotspot-Pfaden unter Last |

---

## Abgeschlossener Zyklus 12 — Admin-Dashboard-Betriebsblock fuer Runtime-/System-Health (2026-05-06)

**Status:** abgeschlossen 2026-05-06. Alle vier Slices done: Z12-1.1 (Inventur), Z12-1.2 (Vertrags-Skizze), Z12-2.1 (Backend Endpoint), Z12-2.2 (Frontend Betriebsblock).

**Thema:** Das Admin-Dashboard soll fuer `admin` kuenftig Signale aus dem laufenden System sichtbar machen, die heute nur vereinzelt im UI auftauchen oder nur als Backend-Health-Endpunkt existieren. Der bestehende Admin-Health-Begriff (`admin-health-panel` in `web/src/components/admin-config/AdminOverviewWorkspaceSection.tsx`, plus `/health/live`, `/health/ready`, `/health` aus `api/API/Extensions/LifecycleApplicationExtensions.cs`) bleibt die Hauptachse — Z12 erweitert ihn, statt eine konkurrierende zweite Betriebslogik zu erfinden.

**Wichtige Produkt-/Scope-Entscheidung:**
- Z12 adressiert zuerst **App-/Runtime-Health** (API erreichbar, DB erreichbar, Directory-Sync-Status, Mail-Konfiguration, einfache Runtime-Metriken wie Prozess-Speicher, Prozess-Uptime, Storage-Auslastung der App-Schreibpfade).
- **Echte Host-/VM-Metrik** (CPU/RAM/Disk des ganzen Servers, Lastdurchschnitt, Container-Health) bleibt bewusst ein **spaeterer optionaler Ausbau** nach Z12. Sie wird in Z12-1 nur als klar abgegrenzter Folgeschritt dokumentiert und bewusst nicht umgesetzt.
- Z12 erfindet keinen zweiten Health-Begriff. Es nutzt den bestehenden Admin-Health-Block (`admin-health-panel` plus `/health`-Endpunkte) als Anker.

**Was bedeutet das praktisch?**
- Admins sehen ohne Server-Login direkt im Dashboard, ob API, DB, Directory-Sync und Mail laufen — heute muss man dafuer mehrere Stellen kombinieren oder auf den Server.
- Einfache Runtime-Signale (Prozess-Speicher, Uptime, Storage-Auslastung der App-Schreibpfade) machen sichtbar, ob die App selbst gesund laeuft, nicht nur ob der Server an ist.
- Die Trennung zwischen App-/Container-/Host-Sicht wird sichtbar: Admins verstehen, ob ein Engpass die App betrifft (relevant fuer Z12) oder den Host (Folgeschritt).

**Warum lohnt es sich, das anzugehen?**
- Heute existieren `/health/live`, `/health/ready`, `/health` als Backend-Endpunkte und es gibt einen `admin-health-panel` im UI — beide Welten sind aber nicht systematisch verbunden. Operative Health-Signale leben verstreut.
- App-/Runtime-Health ist die Stufe mit dem groessten praktischen Hebel pro Zeitaufwand: keine zusaetzliche Infrastruktur, keine OS-/Container-Hooks, keine Privilegien-Diskussion.
- Wenn die Trennung App vs. Host jetzt klar gezogen wird, kann ein spaeterer Host-Metrik-Ausbau sauber andocken, statt App-Signale zu ueberbauen.

**Was wird dadurch besser, sicherer, schneller oder wartbarer?**
- **Besser:** ein konsolidierter Betriebsblock fuer Admins, statt verstreuter Indikatoren in mehreren Sektionen.
- **Sicherer:** sichtbare Health-Signale in Production reduzieren das Risiko stiller Fehlzustaende (Mailversand inaktiv, Directory-Sync laenger nicht gelaufen, Prozess-Speicher faellt unter Schwelle).
- **Wartbarer:** ein expliziter Runtime-Health-Vertrag (DTO + Schwellwerte + klar benannte Domaenen App/Container/Host) verhindert, dass jede Sektion ihren eigenen Mini-Healthcheck baut.
- **Anschlussfaehig:** spaeterer Host-/VM-Metrik-Ausbau bekommt einen sauberen Anker, weil Z12 die App-/Container-/Host-Semantik bereits getrennt hat.

**Begruendung gegen alternative Zuschnitte:**
- *Direkt mit voller Host-Metrik starten*: erzwingt OS-/Container-Hooks (z. B. `cgroup`, `procfs`, Docker-Stats), Privilegien-Klaerung und Plattform-Forks (Linux-VM vs. Dev-Windows). Hoher Aufwand, geringer sichtbarer Nutzen vor App-Health.
- *Nur Backend-Endpoint ohne UI-Block*: bestehende Admins koennen heute `/health/ready` aufrufen — das hat den Schmerzpunkt nicht geloest. Sichtbarkeit im Dashboard ist der eigentliche Hebel.
- *Nur UI-Block ohne neuen Vertrag*: das `admin-health-panel` zeigt heute Directory-Sync, Mail und Warnungen — Runtime-Metriken (Speicher, Uptime, Storage) fehlen ohne neuen Endpoint, weil sie weder im Directory-Status noch im Mail-Config-Endpoint enthalten sind.
- *Beides in einem Slice statt vier*: zu breit fuer einen sauberen Slice; Begriffsklaerung und Vertrag muessen vor der Backend-Implementierung stehen, sonst entsteht ein DTO, das spaeter wieder umgebaut wird.

**Fokus / Slice-Reihenfolge:**
1. **Z12-1.1** Begriffsklaerung / Vertragsinventur Runtime Health — was existiert heute (`/health`-Endpunkte, `admin-health-panel`, Directory-Status, Mail-Konfig, Warnungen)? Welche App-/Runtime-Signale fehlen? Wie heisst was, und wie verhaelt es sich zu Container-/Host-Sicht?
2. **Z12-1.2** Vertrags-Skizze DTO + Schwellwerte + Begriffsabgrenzung App/Container/Host — Skizze des neuen Runtime-Health-DTO, klare Schwellwerte (z. B. Speicher ok/warn/crit), explizite Trennung App vs. Container vs. Host. Kein Code-Change.
3. **Z12-2.1** Backend Runtime-Health Endpoint + Service — Implementierung gemaess Z12-1.2.
4. **Z12-2.2** Frontend Admin-Dashboard-Betriebsblock — sichtbarer Block im Admin-Dashboard, andockend an den bestehenden `admin-health-panel`.
5. *(Folgeschritt nach Z12)* **Optionaler Host-/VM-Metrik-Ausbau** (CPU/RAM/Disk des gesamten Servers) — bewusst klar **ausserhalb** der ersten Umsetzung; eigener Zyklus oder Slice nach Z12-2.2, nur wenn ein konkreter Bedarf entsteht und die Privilegien-/Plattform-Frage geklaert ist.

**Leitplanken Z12:**
- Z12 redet zuerst von App-/Runtime-Health, nicht von Host-/VM-Metrik. Wer im Slice „Host-CPU/Disk/RAM" mitnimmt, weicht den Zuschnitt auf.
- Bestehende `admin-health-panel`-Logik und `/health/*`-Endpunkte bleiben Anker. Keine zweite parallele Betriebslogik.
- Begriffstrennung App vs. Container vs. Host ist Pflicht in Z12-1.1/Z12-1.2 — sonst rutschen Host-Metriken still in den App-Vertrag.
- Reihenfolge streng sequenziell: Z12-1.1 → Z12-1.2 → Z12-2.1 → Z12-2.2.
- Z12-1.x sind reine Doku-Slices (kein Code, kein API-Vertrag, keine DB-Aenderung).
- Schwellwerte und Severity-Begriffe (z. B. ok/warn/crit) werden in Z12-1.2 deklarativ skizziert, nicht in Z12-2.1 frei erfunden.
- Schreibregel anwenden: jedes Slice-Ergebnis erklaert auch fuer normale Leser, was sich praktisch aendert, warum es sich gelohnt hat und welcher Nutzen entsteht.
- Nach jedem Slice: Commit mit klarem Slice-Bezug; Doku (`CODE_REVIEW.md`, `TODO.md`, `MEMORY.md`, `CODEX_SYNC.md`, `KauthWorkflow/Stand/Code-Review-Status.md`) im selben Pass nachziehen.
- Keine stillen Mitnahme-Refactors ausserhalb des beauftragten Slices (`CLAUDE_CONTROL.md`).

**Geplante Slices:**

| ID | Aufgabe | Prio | Reasoning | Modell | Status |
|----|---------|------|-----------|--------|--------|
| Z12-1.1 | Begriffsklaerung / Vertragsinventur Runtime Health: bestehende Health-Signale (Backend-Endpunkte, UI-Block, Directory-Status, Mail-Konfig, Warnungen) inventarisieren; benennen, welche App-/Runtime-Signale heute **fehlen**; klar trennen App / Container / Host | HIGH | high | opus | done (2026-05-06) — Inventur in `CODE_REVIEW.md` § Z12-1.1 (heutige Signale, fehlende App-/Runtime-Signale, App-/Container-/Host-Trennung, UI-Begriffsempfehlung) |
| Z12-1.2 | Vertrags-Skizze DTO + Schwellwerte + Begriffsabgrenzung App/Container/Host: neuen Runtime-Health-Antwortvertrag (Felder, Severity-Stufen, Schwellwerte, Domaenen-Tags) auf Papier ziehen; klar markieren, was App ist und was Host bleibt; FE-Andockpunkt am bestehenden `admin-health-panel` benennen | HIGH | high | opus | done (2026-05-06) — Vertrags-Skizze in `CODE_REVIEW.md` § Z12-1.2 (`GET /admin/runtime-health` admin-only; `AdminRuntimeHealthDto` mit `application`/`dependencies`/`directory`/`storage[]`; Severity `ok/warning/critical/unknown`; Schwellwerte deklarativ; FE-Andock im bestehenden `admin-health-panel`; Z12-2.x-Abgrenzung gegen Host-/VM-Metrik / Prometheus / Trends / Alerts) |
| Z12-2.1 | Backend Runtime-Health Endpoint + Service: Implementierung gemaess Z12-1.2 (App-/Runtime-Signale: API/DB/Directory/Mail-Status + einfache Runtime-Metriken Prozess-Speicher/Uptime/Storage); kein Host-/VM-Metrik-Code | HIGH | medium..high | sonnet | done (2026-05-06) — `GET /admin/runtime-health` admin-only; `AdminRuntimeHealthService` + `IAdminRuntimeHealthService`; DTO-Familie gemaess Vertrag; Severity-Logik deklarativ; Storage-Pfade via `RUNTIME_HEALTH_STORAGE_PATHS`; 42 neue Tests; 411/411 non-integration Tests gruen |
| Z12-2.2 | Frontend Admin-Dashboard-Betriebsblock: Erweiterung des bestehenden `admin-health-panel` um die neuen Runtime-Signale; Severity-Anzeige gemaess Z12-1.2; keine konkurrierende zweite Betriebslogik | HIGH | medium..high | sonnet | done (2026-05-06) — `AdminOverviewWorkspaceSection` erweitert: neuer Betriebsblock mit Severity-Badge (`overallSeverity`), API-Prozess-Kachel (Uptime, Managed Heap), Abhaengigkeiten-Kachel (DB, Auth, Mail summarized), Storage-Kacheln (nur wenn `storage[]` nicht leer); `useQuery` intern mit `staleTime 60s`/`refetchInterval 120s`; Wording „Betriebsstatus", kein „Server"/„RAM frei"/„Disk frei"; bestehende Kacheln (Verzeichnis-Sync, Mail-Versand, Offene Warnungen) semantisch erhalten; `--critical` CSS-Klasse fuer kritischen Gesamtzustand ergaenzt |
| *(Folgeschritt nach Z12)* | Optionaler Host-/VM-Metrik-Ausbau (CPU/RAM/Disk Server, Container-Health) — eigener Zyklus oder Slice nach Z12-2.2; nur bei konkretem Bedarf und nach Privilegien-/Plattform-Klaerung | — | — | — | bewusst ausserhalb Z12 |

**Erwartete Ausgaenge aus Z12:**
- Z12-1.1: Inventur in `CODE_REVIEW.md` § Z12-1.1 (heutige Signale, fehlende Signale, App vs. Container vs. Host).
- Z12-1.2: Vertrags-Skizze in `CODE_REVIEW.md` § Z12-1.2 (DTO-Felder, Schwellwerte, Severity, Domaenen-Tags, FE-Andockpunkt).
- Z12-2.1: neuer Runtime-Health-Endpoint + Service entlang Z12-1.2.
- Z12-2.2: sichtbarer Betriebsblock im Admin-Dashboard, andockend an `admin-health-panel`.
- Pro Slice: aktualisierte Doku (Status, Erkenntnisse, Folgeentscheidungen); Tests fuer den jeweiligen Slice angepasst/ergaenzt (ab Z12-2.x).

**Reihenfolge / Abhaengigkeiten:**
- Z12-1.1 → Z12-1.2 → Z12-2.1 → Z12-2.2 streng sequenziell. Z12-2.1 setzt den in Z12-1.2 verabschiedeten Vertrag voraus; Z12-2.2 setzt den in Z12-2.1 implementierten Endpoint voraus.
- Optionaler Host-/VM-Metrik-Ausbau ist explizit kein Z12-Slice und folgt erst nach Z12-2.2.

### § Z12-1.1 Inventur Runtime Health (done 2026-05-06)

**Auftrag:** heutige Health-Signale im Repo benennen, fehlende App-/Runtime-Signale fuer den Admin-Dashboard-Betriebsblock auflisten, sauber zwischen App-/Runtime-, Container- und Host-/VM-Sicht trennen, UI-Begriffsempfehlung fuer Z12 ableiten. Reine Doku, kein Code.

**Was bedeutet das praktisch?**
- Es gibt heute zwei getrennte Health-Welten: Backend-Endpunkte (`/health/live`, `/health/ready`, `/health`) und ein Admin-UI-Panel (`admin-health-panel`). Ein Admin sieht im Dashboard den DB- oder Auth-Zustand des `/health`-Endpoints **nicht** — und ein Operator sieht aus `/health` nicht den Verzeichnis-/Mail-/Warnungen-Block.
- Echte App-/Runtime-Signale (Prozess-Uptime, Managed-Heap-Druck, Storage-Auslastung der App-Schreibpfade) fehlen komplett — weder Backend noch UI.
- Z12 baut deshalb keinen zweiten Healthcheck, sondern verbindet die zwei bestehenden Welten und ergaenzt die fehlenden App-/Runtime-Signale — bewusst ohne Host-/VM-Metrik.

**Warum lohnt es sich, das anzugehen?**
- Operative Health-Signale sind heute fragmentiert. Ein Admin muss kombinieren: `/health` per curl, `admin-health-panel` im UI, plus „App laeuft eigentlich noch?" per Server-Login. Das ist genau der Schmerzpunkt, den Z12 adressieren soll.
- Ohne saubere Trennung App / Container / Host rutschen spaeter Host-Metriken (Disk-Free der ganzen VM, RAM frei der VM) still in den App-Vertrag, weil sie technisch oft aus denselben APIs (`DriveInfo`, `Process`) kommen — am Ende behauptet das UI „Server: ok", obwohl es nur den Container gemessen hat.
- Begriffsfestlegung jetzt ist billiger als spaeterer Umbau. Sobald Z12-2.2 ein Label „RAM frei" zeigt, ist es schwer zurueckzunehmen.

**Was wird dadurch besser, sicherer oder wartbarer?**
- **Klare Inventur** der heutigen Signale macht sichtbar, was Z12 wirklich neu schaffen muss vs. was nur konsolidiert wird.
- **Begriffstrennung** (App-/Runtime-Health, Container-Sicht, Host-/VM-Metrik) verhindert Etikettenschwindel im UI und gibt Z12-1.2 ein klares Geruest fuer das DTO.
- **Anschluss-Anker** fuer einen spaeteren Host-/VM-Metrik-Ausbau: er bekommt einen eigenen Block mit eigenem Wording, statt App-Signale zu ueberbauen.

#### Heute existierende Health-Signale

**Backend (`api/API/Extensions/LifecycleApplicationExtensions.cs`, `MapLifecycleHealthEndpoints`):**
- `GET /health/live` — `{ status: "ok" }`. Reines Process-Liveness-Signal: API-Prozess antwortet ueberhaupt. Unauthentifiziert. Praktischer Anwendungsfall: Container-Healthcheck, Caddy-Probe.
- `GET /health/ready` — `{ status, database }`. DB-Reachability via `SELECT 1` gegen `LifecycleRuntimeSettings.ConnectionString`. Status `ok`/`degraded`, HTTP 200/503. Unauthentifiziert.
- `GET /health` — `{ status, database, auth: { mode, status, reachability, devSimulationActive } }`. DB-Ping plus Entra-OIDC-Discovery-URL-Check (`https://login.microsoftonline.com/{tenant}/v2.0/.well-known/openid-configuration`). Auth-Mode unterscheidet `entra` / `dev-sim` / `none`. Unauthentifiziert.

**Admin-UI-Block (`web/src/components/admin-config/AdminOverviewWorkspaceSection.tsx`, CSS-Klasse `admin-health-panel`):**
- Verzeichnis-Sync-Kachel — Quelle: `GET /admin/directory/status` (`DirectorySyncStatusDto` aus `api/API/Services/IDirectorySyncService.cs:58`) plus `GET /admin/directory/pending-imports`. Felder: `lastSyncAt`, `lastSyncStatus` (`success`/`partial`/`failed`), `lastError`, `totalGroups`, `totalIdentities`, `totalMappings`, `configuredGroupPrefix`. Tone-Mapping in FE: `getDirectorySyncTone`.
- Mail-Versand-Kachel — Quelle: `GET /admin/config/notification-email` (`AdminNotificationEmailConfigurationDto` ueber `INotificationEmailConfigurationService.GetAdminConfiguration`). Felder im FE: `mode` (`enabled`/`sandbox`/`disabled`), `configurationStatus` (`complete`/`incomplete`). Tone-Mapping: `getMailTone`.
- Offene-Warnungen-Kachel — kein eigener Endpoint, sondern FE-seitige Aggregation in `adminWorkspaceModel.ts` (`AdminWorkspaceWarningCategory`: `department_lead`, `department_owner`, `responsibility_user`, `responsibility_department`, `mail_configuration`). Tone-Mapping: `getWarningTone`.

**Weitere bereits existierende Signale, die noch nicht im `admin-health-panel` haengen:**
- Auth-/Entra-Reachability liegt heute nur in `/health` (Endpoint-Antwort), nicht im UI.
- DB-Reachability liegt heute nur in `/health/ready` und `/health`, nicht im UI.
- Hosted Sync (`EntraDirectorySyncService`): geplante Sync-Frequenz steckt in `LifecycleRuntimeSettings`/`SchedulingOptions`, ist aber nicht als „naechster geplanter Lauf" sichtbar — nur „letzter Lauf" via `lastSyncAt`.

#### Was fehlt fuer einen Admin-Dashboard-Betriebsblock?

App-/Runtime-Signale, die heute weder Backend noch UI liefern:
- **Prozess-Uptime** (wann ist der API-Prozess gestartet, wie lange laeuft er) — wichtigstes „Hat sich gerade etwas neu gestartet?"-Signal fuer Admins.
- **Prozess-Speicher** im laufenden API-Prozess: Working Set, Managed Heap (`GC.GetTotalMemory`), GC-Druck (`GC.GetGCMemoryInfo`).
- **Storage-Auslastung der App-Schreibpfade** (z. B. Volume, das die API beschreibt — Logs, ggf. Uploads). Bewusst eng auf von der App genutzte Pfade, nicht auf „Disk-Free der ganzen VM".
- **DB-Reachability als sichtbarer UI-Eintrag** (heute nur per Endpoint-Aufruf).
- **Entra-/Auth-Reachability als sichtbarer UI-Eintrag** (heute nur per Endpoint-Aufruf, nicht differenziert nach `disabled`/`enabled`/`misconfigured`/`unreachable`).
- **Letzter Sync-Lauf vs. erwarteter naechster Lauf** als ein-Blick-Signal — heute nur „letzter Lauf" plus Frequenz-Setting.
- **Mail-Probe-Resultat** als Health-Signal: es gibt zwar `POST /admin/config/notification-email/test`, das Resultat haengt aber nicht im `admin-health-panel`.

#### Was ist sauber aus der API heraus lieferbar (ohne Host-Agent)?

Aus dem laufenden ASP.NET-Core-Prozess problemlos verfuegbar:
- Prozess-Uptime via `IHostApplicationLifetime` / `Process.GetCurrentProcess().StartTime`.
- Managed Heap via `GC.GetTotalMemory(false)` und `GC.GetGCMemoryInfo()`.
- Working Set via `Process.GetCurrentProcess().WorkingSet64` — Achtung: nur des API-Prozesses, **nicht** des Hosts.
- Thread-/Worker-Pool-Stats via `ThreadPool.GetAvailableThreads`.
- DB-Reachability — bereits via `CheckDatabaseStatusAsync`.
- Entra-OIDC-Reachability — bereits via Discovery-URL-Probe in `/health`.
- Mail-Konfig-Vollstaendigkeit — bereits via `INotificationEmailConfigurationService`.
- Directory-Sync-Status (`lastSyncAt`, `lastSyncStatus`, `lastError`) — bereits via `IDirectorySyncService.GetSyncStatusAsync`.
- Storage-Auslastung definierter App-Schreibpfade via `DriveInfo` **fuer diese explizit benannten Pfade** (z. B. Logs-Volume, Uploads-Volume) — nicht generisch „alle Drives".

#### Was ist nur Container-/Process-Sicht und darf nicht als „Server"-Metrik verkauft werden?

Diese Werte sind aus dem API-Prozess messbar, aber nur fuer den Prozess/Container — **niemals** als Host-/VM-Metrik kommuniziert:
- Working Set / Managed Heap / GC-Stats: nur des API-Prozesses.
- Prozess-Uptime: nur des Prozesses, sagt nichts ueber Host-Reboot.
- `DriveInfo` der API-Mount-Punkte: zeigt den Container-/Volume-View, nicht „Disk Free der VM".
- Thread-Pool-Saturation: nur des API-Prozesses.

Echte Host-/VM-Metrik (CPU-Last des ganzen Servers, RAM frei der VM, Disk-Free aller Volumes, Load-Average, Container-Runtime-Stats wie `docker stats`/`cgroup`) ist aus dem API-Prozess heraus **nicht zuverlaessig** lieferbar — braeuchte Host-Agent, OS-Privilegien, oder Compose-Stats-Sidecar. Bleibt bewusst ausserhalb Z12.

#### Drei-Domaenen-Modell als Pflicht-Begriffsraster fuer Z12

Z12-1.2 muss jedes Feld im neuen DTO klar einer dieser drei Domaenen zuordnen — und das UI in Z12-2.2 muss die Domaene benennen, statt sie zu verwischen:

| Domaene | Inhalt | Z12-Scope | Wording-Empfehlung |
|---------|--------|-----------|--------------------|
| **App-/Runtime-Health** | API-Prozess-Uptime, Managed Heap, DB-Reachability, Entra-Reachability, Directory-Sync-Status, Mail-Konfig-/Probe-Status, App-Warnungen | **Z12 (in Scope)** | „Anwendung", „API-Prozess", „Abhaengigkeiten" |
| **Container-/Volume-Sicht** | Working Set, App-Mount-Punkte (`DriveInfo` fuer App-Schreibpfade), Thread-Pool | **Z12 (in Scope, aber explizit als App-Container labeln)** | „API-Container", „Schreibpfad der Anwendung" — **nicht** „Server", **nicht** „RAM frei", **nicht** „Disk frei" |
| **Host-/VM-Metrik** | Host-CPU/RAM/Disk gesamt, Load-Average, Container-Runtime-Stats, OS-Patches | **bewusst ausserhalb Z12** | spaeter eigener Block „Server" / „VM" / „Host" — nie als Teil des App-Health-Blocks darstellen |

#### UI-Begriffsempfehlung fuer Z12 (Eingang in Z12-1.2/Z12-2.2)

- Dashboard-Block heisst **„Betriebsstatus"** oder **„Runtime Health der Anwendung"**, nicht „Server-Status" oder „Systemzustand".
- Untergliederung in „Anwendung" (Prozess-Uptime, Speicher), „Abhaengigkeiten" (DB, Entra/Auth, Mail), „Verzeichnis" (Sync-Status, pending imports), „Warnungen" (bestehender Aggregator).
- Speicher-Werte explizit als **„API-Prozess: Managed Heap"** oder **„API-Prozess: Working Set"** labeln, nicht als „RAM" oder „RAM frei".
- Storage-Werte explizit als **„Schreibpfad der Anwendung"** mit Pfadangabe labeln, nicht als „Disk frei".
- Severity-Stufen werden in Z12-1.2 verbindlich festgelegt; in Z12-1.1 noch keine Schwellwerte fixieren.
- Bestehende Kacheln (Verzeichnis-Sync, Mail, Warnungen) bleiben semantisch wo sie sind — Z12 fuegt App-/Abhaengigkeits-Kacheln davor/daneben hinzu, statt sie zu ersetzen.

#### Folgeentscheidungen fuer Z12-1.2

- DTO-Skizze in Z12-1.2 muss pro Feld die Domaenen-Zuordnung (App / Container / Host) explizit tragen, nicht implizit.
- Schwellwerte/Severity (`ok`/`warn`/`crit`) werden in Z12-1.2 **deklarativ** definiert — Z12-2.1 darf sie nicht im Code frei waehlen.
- Bestehender `admin-health-panel`-Aufbau (`directory`-Kachel, `mail`-Kachel, `warnings`-Kachel) bleibt Anker; neuer Block ergaenzt Anwendung/Abhaengigkeiten, ersetzt nichts.
- Host-/VM-Metrik wird in Z12-1.2 nur als ausgegrenzter Folgeschritt benannt, nicht als Feld geschrieben.

### § Z12-1.2 Vertrags-Skizze Admin Runtime Health (done 2026-05-06)

**Auftrag:** auf Basis der in Z12-1.1 fixierten Inventur und Drei-Domaenen-Trennung (App / Container / Host) den geplanten Backend-Vertrag fuer den ersten Z12-Umsetzungspfad skizzieren — ein einziger Admin-Runtime-Health-Endpoint, klar geschnittene DTO-Familie, pro Feld eine Domaenen-Zuordnung, deklarative Severity-Stufen und Schwellwerte sowie der FE-Andockpunkt am bestehenden `admin-health-panel`. Reine Doku-Slice, kein Code, kein API-Vertrags-Commit, keine DB-Aenderung.

**Was bedeutet das praktisch?**
- Z12-2.1 hat eine eindeutige Vorlage, die festlegt, welche Felder geliefert werden, was sie heissen, in welcher Domaene sie sind und welche Severity sie ab welchem Wert haben — niemand erfindet beim Implementieren still einen weiteren Healthcheck.
- Z12-2.2 weiss vorab, an welcher Stelle im `admin-health-panel` der Block einsteigt, und welche Begriffe sichtbar sind (`„API-Prozess: Managed Heap"`, `„Schreibpfad der Anwendung"`) — kein zweiter Health-UI-Block, keine konkurrierende Sprache.
- Schwellwerte sind hier deklarativ festgehalten; Z12-2.1 darf sie nur 1:1 uebernehmen, nicht erfinden.

**Warum lohnt es sich, das anzugehen?**
- Ohne festen Vertrag baut Z12-2.1 unweigerlich einen Endpoint, der spaeter umgebaut wird, sobald Z12-2.2 die UI-Etikettierung anders braucht. Die Doku-Slice ist die billigste Stelle, App-/Container-/Host-Semantik festzunageln.
- Ein einzelner Endpoint mit klar geschnittenen Sub-DTOs verhindert, dass Health-Felder ueber drei verschiedene Endpunkte (Directory, Mail, neuer Runtime) verteilt landen — der `admin-health-panel` haette sonst weiter inkonsistente Quellen.

**Was wird dadurch besser, sicherer oder wartbarer?**
- **Besser:** ein einziger Vertrag fuer alle App-/Container-Health-Felder; eine einzige Stelle, an der Schwellwerte gelesen werden.
- **Sicherer:** explizite Domaenen-Tags machen Etikettenschwindel im UI strukturell schwer — eine `host`-Markierung wuerde dort sofort auffallen und Z12-2.2 wuerde sie ablehnen.
- **Wartbarer:** Severity-Logik ist deklarativ (`ok`/`warning`/`critical`/`unknown`) statt freihaendig im Service-Code; spaetere Schwellwert-Anpassungen passieren in einer Tabelle, nicht in if-Ketten.
- **Anschlussfaehig:** der spaetere optionale Host-/VM-Metrik-Block bekommt einen klar abgegrenzten Eigenraum mit eigener `host`-Domaene; die App-Felder muessen nicht umbenannt werden.

#### 1) Backend-Vertrag — geplanter Runtime-Health-Endpoint

Genau **ein** neuer Admin-Read-Endpoint:

- `GET /admin/runtime-health`
- AuthN/AuthZ: gleiche Pflicht wie der bestehende Admin-Workspace (admin-only; nicht offen wie `/health/*`).
- Cache-Verhalten: keine Aggregation auf Server-Seite; jede Anfrage berechnet den aktuellen Snapshot. Empfehlung: serverseitig billiger In-Memory-Cache (z. B. 5–10 s) gegen versehentliches Hammern, aber kein Pflichtfeld der Vertrags-Skizze.
- Antwort: `AdminRuntimeHealthDto` (siehe unten).
- Dieser Endpoint **ersetzt** die bestehenden offenen `/health/live`/`/health/ready`/`/health` **nicht** — die bleiben unveraendert fuer Container-/Caddy-Probes. Z12 bedient ausschliesslich den Admin-UI-Pfad.

##### 1.1 DTO-Familie (Felder + Domaenen-Tags)

`Domain` ist ein deklarativer Tag pro Feld. Erlaubt: `app` (App-/Runtime-Health, App-Sicht der Abhaengigkeiten), `container` (Prozess-/Container-/App-Volume-Sicht), `host` (Host-/VM-Metrik — in Z12 **nicht belegt**, nur als ausgegrenzte Reservierung dokumentiert), `external` (externer Dienst gesehen aus Sicht der App, z. B. Entra-Discovery).

```
AdminRuntimeHealthDto
├── generatedAt              ISO timestamp        domain: app
├── overallSeverity          ok|warning|critical|unknown
├── application              ApplicationHealthDto domain: app
├── dependencies             DependenciesHealthDto
├── directory                DirectoryHealthDto   domain: app
└── storage                  StorageHealthDto[]   domain: container
```

`ApplicationHealthDto` (App-Prozess; alle Werte gelten **nur** fuer den API-Prozess/Container, nie fuer den Host):

```
ApplicationHealthDto
├── severity                 ok|warning|critical|unknown
├── processStartedAt         ISO timestamp        domain: app
├── uptimeSeconds            number               domain: app
├── managedHeapBytes         number               domain: container
├── managedHeapHighThresholdBytes  number?        domain: container   (aus GC.GetGCMemoryInfo)
├── workingSetBytes          number               domain: container
└── threadPool               { workerThreadsAvailable, completionPortThreadsAvailable }?  domain: container
```

`DependenciesHealthDto` (Sicht der App auf ihre Abhaengigkeiten; bewusst pro Abhaengigkeit eigenes Sub-DTO mit eigener `severity`, damit das UI pro Kachel entscheiden kann):

```
DependenciesHealthDto
├── severity                 ok|warning|critical|unknown
├── database                 DependencyHealthDto       domain: app
│   ├── severity
│   ├── reachable            boolean
│   ├── lastCheckedAt        ISO timestamp
│   ├── latencyMs            number?
│   └── lastError            string?
├── auth                     AuthDependencyHealthDto   domain: app + external
│   ├── severity
│   ├── mode                 entra|dev-sim|none
│   ├── reachability         reachable|unreachable|not_applicable
│   ├── lastCheckedAt        ISO timestamp
│   ├── latencyMs            number?
│   └── lastError            string?
└── mail                     MailDependencyHealthDto   domain: app
    ├── severity
    ├── mode                 enabled|sandbox|disabled
    ├── configurationStatus  complete|incomplete
    ├── lastProbeAt          ISO timestamp?
    └── lastProbeStatus      success|failure|never_run
```

`DirectoryHealthDto` (Verzeichnis-Sync; konsumiert intern `IDirectorySyncService.GetSyncStatusAsync` + `GetPendingImportsCountAsync`, aber **stabilisiert die Felder im Vertrag** — keine 1:1-Pass-Through-Abhaengigkeit):

```
DirectoryHealthDto
├── severity                 ok|warning|critical|unknown
├── lastSyncAt               ISO timestamp?       domain: app
├── lastSyncStatus           success|partial|failed|never_run    domain: app
├── lastError                string?              domain: app
├── nextScheduledSyncAt      ISO timestamp?       domain: app    (abgeleitet aus SchedulingOptions)
└── pendingImportsCount      number               domain: app
```

`StorageHealthDto[]` (genau die App-Schreibpfade, nie generisch alle Drives — Pfadliste wird in Z12-2.1 in Konfiguration explizit benannt; leere Liste ist erlaubt):

```
StorageHealthDto
├── label                    "logs"|"uploads"|... (deklarativer Bezeichner) domain: container
├── path                     string                                          domain: container
├── totalBytes               number                                          domain: container
├── freeBytes                number                                          domain: container
├── usedPercent              number                                          domain: container
└── severity                 ok|warning|critical|unknown
```

##### 1.2 Domaenen-Tags Zusammenfassung

- **`app`:** `generatedAt`, `processStartedAt`, `uptimeSeconds`, gesamter `directory`-Block, `database`-Block, `mail`-Block, `auth.mode`/`auth.reachability` aus App-Sicht.
- **`container`:** `managedHeapBytes`, `managedHeapHighThresholdBytes`, `workingSetBytes`, `threadPool.*`, gesamter `storage[]`-Block.
- **`external`:** `auth` zusaetzlich, weil die Reachability-Probe gegen einen externen Dienst (Entra OIDC Discovery) laeuft. Tag dokumentiert, dass eine `unreachable`-Antwort nicht zwingend ein App-Problem ist.
- **`host`:** in Z12 **nicht belegt**. Reservierter Tag fuer einen spaeteren optionalen Host-/VM-Metrik-Block (CPU/RAM/Disk Server, Load-Average) — bleibt explizit ausserhalb Z12.

#### 2) Severity-Modell

Vier Stufen, pro Sub-DTO und auf Top-Ebene:

- **`ok`** — Signal vorhanden, alle Schwellwerte unterschritten.
- **`warning`** — Signal vorhanden, ein Schwellwert ueberschritten, aber nicht kritisch.
- **`critical`** — Signal vorhanden und ueber kritischem Schwellwert; oder Pflicht-Abhaengigkeit nicht erreichbar.
- **`unknown`** — Signal aktuell nicht ermittelbar (z. B. Mail-Probe nie gelaufen, dev-sim-Auth nicht anwendbar, Storage-Pfad noch nicht konfiguriert). `unknown` ist **nicht** dasselbe wie `ok`.

**Aggregation `overallSeverity`:**

- `critical`, sobald **irgendein** Sub-DTO `critical` ist.
- sonst `warning`, sobald **irgendein** Sub-DTO `warning` ist.
- sonst `ok`, falls **alle** Sub-DTOs `ok` oder ein Mix aus `ok` und neutralisierten `unknown` sind (siehe unten).
- sonst `unknown`, falls kein `ok`/`warning`/`critical` ermittelbar war.

**Sonderfall `unknown` neutralisiert:** `auth.severity = unknown` darf den Gesamtstatus nicht auf `unknown` ziehen, wenn `auth.mode = dev-sim` oder `none` ist (dann ist `unknown` = bewusst nicht anwendbar). Genauso fuer leere `storage[]`-Liste — zaehlt nicht ins Aggregat.

**Aggregation pro Sub-DTO (z. B. `dependencies.severity`):** identische Regel ueber alle Felder mit eigener `severity`.

#### 3) Erste Schwellwerte / Heuristik (deklarativ, fuer Z12-2.1 1:1 zu uebernehmen)

| Feld | Domaene | `ok` | `warning` | `critical` | `unknown` |
|------|---------|------|-----------|------------|-----------|
| `database.reachable` | app | `SELECT 1` < 1000 ms | 1000–3000 ms | unreachable / Timeout / >3000 ms | nie geprueft |
| `auth.reachability` (mode=`entra`) | app+external | OIDC-Discovery < 2000 ms | 2000–5000 ms | unreachable / Timeout / >5000 ms | nie geprueft |
| `auth.reachability` (mode=`dev-sim`) | app | n/a (immer `ok` mit Hinweis) | — | — | — |
| `auth.reachability` (mode=`none`) | app | — | — | — | immer `unknown` |
| `mail` (`mode`+`configurationStatus`) | app | `enabled`+`complete`, oder `disabled` | `sandbox`, oder `enabled`+`incomplete` mit nur Detail-Luecken | `enabled`+`incomplete` mit Pflicht-Luecke (keine sinnvolle Versendung moeglich) | `mode` nicht ermittelbar |
| `directory.lastSyncAt` | app | letzter Lauf < 2 × `SchedulingOptions.Interval` her | 2–5 × Intervall her, oder `partial` | > 5 × Intervall her, oder `failed` | noch nie gelaufen |
| `application.uptimeSeconds` | app | rein informativ — immer `ok` (kein Schwellwert in Z12) | — | — | — |
| `application.managedHeapBytes` vs. `managedHeapHighThresholdBytes` | container | < 75 % | 75–90 % | > 90 % | Threshold nicht verfuegbar |
| `storage[].usedPercent` | container | < 80 % | 80–90 % | > 90 % | Pfad nicht erreichbar |

Hinweise:
- `application.workingSetBytes` und `threadPool.*` werden in Z12 **nur informativ** angezeigt, ohne Schwellwert. Begruendung: aussagekraeftige Schwellwerte haengen stark vom Container-Sizing ab; ohne Host-Wissen koennen wir keine seriose Heuristik liefern. Severity bleibt `ok` (oder `unknown` falls nicht messbar).
- Die Schwellwerte sind absichtlich konservativ. Sie sollen in Production typischerweise `ok` zeigen; jeder Wechsel auf `warning` ist ein bewusst sichtbares Signal.
- Aenderungen der Schwellwerte sind ein Doku-/Vertrags-Schritt (Update dieses Abschnitts), nicht ein Service-Code-Schritt.

#### 4) FE-Andockpunkt

- **Anker:** der bestehende `admin-health-panel` in `web/src/components/admin-config/AdminOverviewWorkspaceSection.tsx` (CSS-Klasse `panel admin-health-panel`).
- **Kein zweiter Block:** Z12-2.2 baut **innerhalb** dieses Panels, nicht daneben oder darueber. Es gibt nicht „den alten Admin-Health-Block plus den neuen Runtime-Health-Block".
- **Sichtbarer Titel:** der bestehende SectionHeader-Titel `„Systemstatus"` darf in Z12-2.2 zu `„Betriebsstatus"` oder `„Runtime Health der Anwendung"` umbenannt werden, damit der Begriffshorizont passt; der Anker bleibt aber dasselbe Panel.
- **Kachel-Reihenfolge in Z12-2.2 (Empfehlung, nicht Vertrag):**
  1. **Anwendung** (neu, aus `application`) — Uptime, Managed Heap (% von Threshold), Severity. Begriffe wie `„API-Prozess: Managed Heap"` strikt einhalten.
  2. **Abhaengigkeiten** (neu, aus `dependencies.database` + `dependencies.auth`) — DB- und Auth-Reachability mit Severity. Mail bleibt **nicht** hier, sondern in seiner bestehenden Kachel.
  3. **Verzeichnis-Sync** (vorhanden, jetzt aus `directory`) — die heutige Kachel kann ihre Werte aus dem neuen Endpoint beziehen, ihre Position bleibt; Felder bleiben semantisch gleich.
  4. **Mail-Versand** (vorhanden, jetzt aus `dependencies.mail`) — Position und Wording unveraendert.
  5. **Offene Warnungen** (vorhanden, FE-seitiger Aggregator) — bleibt unveraendert; **nicht** Teil von `AdminRuntimeHealthDto`.
  6. **Schreibpfade der Anwendung** (neu, aus `storage[]`, nur falls Liste nicht leer) — kompakte Kachelreihe pro Pfad mit Label und `usedPercent`.
- **Severity-Anzeige:** der Header des Panels kann den `overallSeverity` als Badge zeigen (`ok`/`warning`/`critical`/`unknown`); bestehende Tone-Helpers (`getDirectorySyncTone`, `getMailTone`, `getWarningTone`) bleiben fuer die Bestandskacheln. Fuer die neuen Kacheln kommt eine eigene Tone-Map analog dazu, gespeist aus dem Vertrags-Severity-Feld.
- **Kein duplizierter State:** das FE konsumiert pro neuer Kachel **nur** Felder aus `AdminRuntimeHealthDto`. Verzeichnis-Sync und Mail behalten zur Migration ihre bisherigen Endpoint-Quellen, koennen aber spaeter ohne Vertragsbruch auf den neuen Endpoint umgestellt werden — Z12-2.2 entscheidet pragmatisch, welcher Weg billiger ist (empfohlen: in Z12-2.2 zuerst nur die neuen App-/Abhaengigkeits-/Storage-Kacheln rendern, Verzeichnis/Mail bleiben an ihren heutigen Quellen).

#### 5) Was explizit **nicht** in Z12-2.1 / Z12-2.2 faellt

Bewusst ausserhalb dieses Vertrags:

- **Echte Host-/VM-Metrik:** Host-CPU, Host-RAM frei, Disk-Free aller Volumes, Load-Average, Boot-Zeit der VM, Container-Runtime-Stats (`docker stats`, `cgroup`).
- **Prometheus / Grafana / `node_exporter` / `cAdvisor` / OTLP-Exporter:** keinerlei Telemetry-Pipeline, kein neues Metrik-Backend, kein Scrape-Endpoint. Z12 ist ausschliesslich ein Admin-UI-Snapshot.
- **Tiefes Infra-Monitoring:** keine Service-Mesh-Hooks, keine Tracing-Integration, keine zentrale Log-Aggregation.
- **Aktiver Mail-Probe (echter Send-Test) als Pflicht-Bestandteil:** bestehender `POST /admin/config/notification-email/test` bleibt manuell; sein Resultat darf optional ueber `mail.lastProbeAt`/`lastProbeStatus` einfliessen, wird aber in Z12-2.1 nicht aktiv ausgeloest.
- **Historisierung / Trends / Sparklines:** kein Persistieren der Snapshots; keine Charts ueber Zeit.
- **Alerts / Notifications bei Severity-Wechsel:** kein automatisches Mail-/Webhook-Versenden bei `critical`. Der Block ist Anzeige, nicht Eskalationspfad.
- **Server-Logs / Log-Ingest:** der bestehende `Administration > System` Log-Konsole-Bereich ist nicht betroffen; Z12 baut keinen Log-Stream.
- **`/health/*`-Aenderungen:** die offenen Container-/Caddy-Probes bleiben stabil und unveraendert.

Diese Abgrenzung ist zwingend — wer in Z12-2.1 oder Z12-2.2 einen dieser Punkte „mit reinnimmt", weicht den Slice-Zuschnitt auf und muss zurueckgewiesen werden.

#### Folgeentscheidungen fuer Z12-2.1

- Endpoint-Pfad und Auth-Pflicht stehen (`GET /admin/runtime-health`, admin-only).
- DTO-Familie und Domaenen-Tags sind verbindlich — keine zusaetzlichen Felder ohne Vertragsupdate hier.
- Schwellwerte aus § 3 sind 1:1 zu uebernehmen.
- Storage-Pfade kommen aus expliziter Konfiguration (z. B. Settings oder Env), nicht aus generischer Drive-Enumeration.
- Mail-Probe wird in Z12-2.1 nicht aktiv getriggert; nur bestehende Probe-Resultate werden wiedergegeben (oder `never_run`).
- Bestehende `/health/*`-Endpunkte werden nicht beruehrt.

---

## Abgeschlossener Zyklus 11 — Admin-/Master-Data-Listen-Vertraege in Umsetzung (2026-05-05..06)

**Status:** eroeffnet 2026-05-05, formal abgeschlossen 2026-05-06. Reiner Umsetzungszyklus auf Basis des in Z10-1.3 verabschiedeten Slice-Plans. **Alle drei Slices done** (F1 2026-05-05, F2 2026-05-06, F3 2026-05-06). Kein aktiver Folgezyklus eroeffnet.

**Thema:** die in Zyklus 10 definierten Antwort-Hulls **P1** (`AdminListPageDto<T>` — `?limit&offset&search&sort` mit `total`) und **P2** (`CursorPageDto<T>` — Cursor-Stream ohne `total`) werden in Z11 in drei sauber getrennten Slices umgesetzt: **F1** fuehrt P1 ein und wendet sie auf Master-Data/Lookups an, **F2** fuehrt P2 ein und wendet sie auf die zwei Audit-Streams an, **F3** rollt die in F1 etablierte P1-Hull auf die sieben scoped Builder-Lese-Endpunkte aus.

**Was bedeutet das praktisch?**
- Wer im Erfassungsdialog eine Abteilung oder Rolle eintippt, bekommt nach F1 sofort die passende Trefferliste statt zu warten, bis der ganze Bestand geladen ist; die drei Master-Data-Tabs verhalten sich erstmals identisch.
- Wer im Audit „meine Aenderung von letzter Woche" sucht, kann sie nach F2 zurueckblaettern. Heute schneidet der stille `limit=100`/`limit=50`-Cap Historie ab, ohne dass das im UI sichtbar ist.
- Der Builder bleibt nach F3 schnell, auch wenn eine Definition Dutzende Aufgabenvorlagen, Antwortfelder oder Rollen-Defaults hat; Suche im Inspector findet wirklich alles, nicht nur den geladenen Block.

**Warum lohnt es sich, das anzugehen?**
- Z10 hat den Vertrag bewusst vor das Wachstum gezogen. Z11 ist die naechste konsequente Stufe: die Hulls aus der Skizze werden zu echtem Code, bevor sich pro Endpunkt unterschiedliche Mini-Implementierungen einschleichen.
- F1 trifft Hotspot #6 aus Z8-1.2 (`/departments`/`/roles`) und stabilisiert gleichzeitig die Plattform-Hull P1, die ab F3 nur noch ausgerollt wird.
- F2 ersetzt einen Cap, der heute ohne Hinweis Audit-Historie abschneidet — ein Sicherheitsgewinn, der unabhaengig von F1 zieht, weil P2 eine eigene Hull-Familie ist.
- F3 ist der breiteste Slice und setzt P1 voraus; ein eigener Slice am Ende verhindert, dass Builder-Refactor-Aufwand in F1/F2 einsickert.

**Was wird dadurch besser, sicherer, schneller oder wartbarer?**
- **Schneller:** Listen liefern bei wachsendem Bestand stabile Antwortzeiten, weil das Backend nur einen Ausschnitt rechnet und Suche/Sortierung per SQL passieren.
- **Sicherer:** keine still abgeschnittene Audit-Historie mehr; vollstaendige Server-Suche statt unvollstaendigem Browser-Filter.
- **Wartbarer:** zwei zentrale Hull-Definitionen (`AdminListPageDto<T>`, `CursorPageDto<T>`) statt pro Endpunkt erfundener Mini-Vertraege; FE bekommt zwei typed Adapter, die spaeter auch fuer Identity-Listen, Identities, Notification-Templates, Rotation, Runtime-Sub-Resources und Startable wiederverwendet werden.
- **FE-Stabilitaet:** Filterzustand wandert in URL-Query, Komponenten verlieren clientseitige Filterhilfen — eine Refactor-Achse, einmal etabliert, mehrfach genutzt.

**Begruendung gegen alternative Zyklen / alternative Reihenfolgen:**
- *Alle Endpunkte gleichzeitig migrieren*: erzwingt paralleles FE-Refactoring an mehreren Stellen und genau die halbgaarigen Workarounds, die Z10 verhindern soll.
- *Mit F3 starten (groesste Wirkung)*: setzt P1 voraus, ohne sie etabliert zu haben — entweder muesste der Slice die Hull-Definition implizit mitbringen (Bloat), oder F1 wuerde nachgeschoben (Rueckwaertsgang).
- *F2 vor F1*: F2 ist klein und unabhaengig, aber der lautere User-Hebel im Alltag liegt am Erfassungseinstieg (F1). F1 zuerst maximiert spuerbaren Nutzen pro Slice.
- *A Identity-Listen / C Identities / E Notification-Templates / F Rotation / G Runtime / H Startable in Z11 mitnehmen*: bewusst nicht — Begruendungen pro Block in § Z10-1.3 unter „Bewusst in Z11 (Folgezyklus) noch nicht angefasst". Z11 bleibt thematisch klar auf Hull-Foundation (F1/F2) und Breitenrolle der etablierten Hull (F3).
- *C Gaps/Pending Split jetzt*: Composite-DTO-Umbau ueber mehrere Endpunkte plus FE-Sichtumbau — explizit kein Hull-Anbau, eigener vorbereiteter Slice nach F1–F3.

**Fokus:**
1. **F1** P1 zentral einfuehren + B Master-Data/Lookups (`GET /departments`, `GET /roles`, `GET /admin/master-data/departments`, `GET /admin/master-data/positions`, `GET /admin/master-data/responsibilities`).
2. **F2** P2 zentral einfuehren + Audit-Streams (`GET /admin/auth/audit`, `GET /admin/directory/audit`).
3. **F3** P1 ausrollen + D Builder-Tabs scoped (`GET /admin/config/workflow-definitions`, `GET /admin/config/action-definitions`, `GET /admin/config/task-templates`, `GET /admin/config/task-templates/{id}/conditions`, `GET /admin/config/task-templates/{id}/dependencies`, `GET /admin/config/answer-definitions`, `GET /admin/config/role-answer-defaults`).

**Leitplanken Z11:**
- Slice-Reihenfolge ist streng F1 → F2 → F3. F1 etabliert P1, F2 etabliert P2, F3 nutzt nur die in F1 fertige P1.
- Pro Slice **eine** Hull-Familie. Kein Mischen P1/P2 in einem Slice.
- Pro Slice nur Read-/Listen-Vertraege; keine Schreibpfade, keine Versionierungs-/Publish-Pfade, keine Composite-DTO-Umbauten.
- Server-Clamp `limit` Default 50/Max 200 (P1 und P2). `search`-Felder, Sort-Whitelists und Scope-Pflichten sind pro Endpunkt deklarativ; FE darf den P2-Cursor nie zerlegen (opaque Base64).
- API-Vertraege der **nicht** in F1/F2/F3 enthaltenen Endpunkte bleiben in Z11 unangetastet.
- Frontend-Folgen sind Konsequenz pro Slice, **nicht** praeventives FE-TODO. Eintrag in `FRONTEND_TODO.md` mit Trigger-Kennzeichnung F1/F2/F3 erst beim Start des jeweiligen Slices.
- Schreibregel: jedes Slice-Ergebnis erklaert auch fuer normale Leser, was sich praktisch aendert, warum es sich gelohnt hat und welcher Nutzen entsteht — nicht nur Diff-Beschreibung.
- Keine stillen Mitnahme-Refactors ausserhalb des beauftragten Slices (`CLAUDE_CONTROL.md`).
- Nach jedem Slice: Commit mit klarem Slice-Bezug; Doku (`CODE_REVIEW.md`, `TODO.md`, `MEMORY.md`, `CODEX_SYNC.md`, `KauthWorkflow/Stand/Code-Review-Status.md`) im selben Pass nachziehen.

**Geplante Slices:**

| ID | Aufgabe | Hull | Endpunkte | Prio | Reasoning | Modell | Status |
|----|---------|------|-----------|------|-----------|--------|--------|
| Z11-F1 | P1 (`AdminListPageDto<T>`) zentral einfuehren + B Master-Data/Lookups; Server-`search`/`sort`-Whitelist pro Endpunkt; typed Wrapper im FE; bestehende Aufrufer zunaechst auf `items` adaptieren | P1 | `/departments`, `/roles`, `/admin/master-data/departments`, `/admin/master-data/positions`, `/admin/master-data/responsibilities` | HIGH | high | opus | done (2026-05-05) — `AdminListPageDto<T>` eingefuehrt, fuenf Endpunkte auf P1 umgestellt, FE-Service-Layer und aktuelle Consumer auf Page-Huelle adaptiert |
| Z11-F2 | P2 (`CursorPageDto<T>`) zentral einfuehren + Audit-Streams; opaque Base64-Cursor ueber `(occurredAt, id)`; FE-Wrapper plus „Mehr laden"-Knopf in beiden Audit-Tabs | P2 | `/admin/auth/audit`, `/admin/directory/audit` | HIGH | medium..high | sonnet | done (2026-05-06) — `CursorPageDto<T>` eingefuehrt, zwei Audit-Endpunkte auf P2 umgestellt, typed FE-Wrapper + Akkumulations-Hook + „Mehr laden"-Knopf in beiden Audit-Tabs |
| Z11-F3 | P1 ausrollen + D Builder-Tabs (scoped); Pflicht-Scope (`workflowDefinitionId` bzw. `task-template-id`) als Whitelist-Bedingung; Filterzustand im FE noch nicht in URL-Query verschoben (bewusst Restgrenze fuer separaten UI-Slice) | P1 (wiederverwendet aus F1) | `/admin/config/workflow-definitions`, `/admin/config/action-definitions`, `/admin/config/task-templates`, `/admin/config/task-templates/{id}/conditions`, `/admin/config/task-templates/{id}/dependencies`, `/admin/config/answer-definitions`, `/admin/config/role-answer-defaults` | HIGH | medium..high | opus | done (2026-05-06) — sieben scoped Builder-Endpunkte auf P1, FE-Service-Wrapper und alle Builder-Hooks auf `page.items` adaptiert |

**Erwartete Ausgaenge aus Z11:**
- F1 abgeschlossen: zentraler `AdminListPageDto<T>`-Hull-Typ im Backend, fuenf Master-Data/Lookup-Endpunkte auf P1, ein typed FE-Wrapper plus Aufrufer-Refactor, in `FRONTEND_TODO.md` als abgeschlossener Trigger F1 dokumentiert.
- F2 abgeschlossen: zentraler `CursorPageDto<T>`-Hull-Typ im Backend, zwei Audit-Endpunkte auf P2, ein typed FE-Wrapper plus Audit-Tab-„Mehr laden", Eintrag in `FRONTEND_TODO.md` mit Trigger F2.
- F3 abgeschlossen: sieben scoped Builder-Endpunkte auf P1, FE-Builder-Inspector ohne clientseitigen Filter, Eintrag in `FRONTEND_TODO.md` mit Trigger F3.
- Pro Slice: aktualisierte Doku (Status, Erkenntnisse, Folgeentscheidungen); Tests fuer den jeweiligen Slice angepasst/ergaenzt.

**Reihenfolge / Abhaengigkeiten:**
- Z11-F1 → Z11-F2 → Z11-F3 streng sequenziell. F3 setzt die in F1 etablierte P1-Hull voraus.
- Folgekandidaten nach Z11 (informativ, nicht beauftragt): A Identity-Listen, C `/admin/directory/identities`, H `/workflow-definitions/startable` mit dem etablierten P1-Adapter; G Runtime-Sub-Resources mit dem etablierten P2-Adapter; C Gaps/Pending Split als eigener vorbereiteter Slice.

### Z11-F1 — P1-Hull fuer Master-Data und Lookups (2026-05-05)

**Was passiert ist:** `AdminListPageDto<T>` plus `AdminListQuery` wurden als gemeinsame P1-Huelle eingefuehrt. Die Endpunkte `GET /departments`, `GET /roles`, `GET /admin/master-data/departments`, `GET /admin/master-data/positions` und `GET /admin/master-data/responsibilities` liefern jetzt `items`, `total`, `limit`, `offset` statt nackter Arrays. Repository- und Service-Signaturen wurden entlang derselben Kette auf die neue Hull umgestellt. Im Frontend gibt es einen typed Wrapper (`web/src/services/api/adminList.ts`), und die betroffenen bestehenden Aufrufer lesen vorerst bewusst `page.items`, damit der Vertrag live ist, ohne schon im selben Slice einen groesseren UI-Pagination-Umbau mitzuziehen.

**Was bedeutet das praktisch?**
- Listen unter Master-Data und im Erfassungseinstieg haben jetzt denselben Vertrag. Das verhindert, dass eine Stelle spaeter schon paging-faehig ist und die naechste weiter alle Daten blind laedt.
- Suche und Sortierung liegen fuer diese Endpunkte jetzt serverseitig vorbereitet bereit. Auch wenn einzelne Screens vorerst noch `limit: 200` nutzen, ist die API nicht mehr auf unendliche Voll-Loads festgelegt.

**Warum lohnt sich das?**
- Das war der breiteste sichtbare Read-Hotspot aus Z10/Z8, aber noch klein genug fuer einen kontrollierten Slice ohne Schreibpfade.
- F1 schafft die Plattform-Huelle, die F3 spaeter nur noch wiederverwendet. Ohne diesen Schnitt wuerde jeder weitere Listenumbau wieder seinen eigenen Mini-Vertrag mitbringen.

**Was wird dadurch besser, sicherer, schneller oder wartbarer?**
- **Wartbarer:** ein P1-Muster statt fuenf leicht unterschiedlicher Listenvertraege.
- **Schneller anschlussfaehig:** F2/F3 und spaetere Listen koennen denselben FE-/BE-Adapter wiederverwenden.
- **Stabiler:** Tests und Stubs haengen jetzt am neuen Vertrag; ein stilles Zurueck auf nackte Arrays wuerde sofort auffallen.

**Verifikation:** `dotnet build api/API.Tests/API.Tests.csproj` gruen (3 vorbestehende Nullability-Warnungen), `npm run build` gruen.

**Restgrenzen von F1:** Die heutigen FE-Consumer nutzen den neuen Vertrag noch pragmatisch ueber `page.items` und `limit: 200`; echte sichtbare Paging-/Search-UI folgt erst, wenn ein eigener UI-Slice oder F3/Folgeslices sie explizit ziehen. `GET /workflow-definitions/startable` bleibt bewusst draussen.

**Naechster Schritt:** Z11-F2 beauftragen — Codex setzt fuer Claude explizit `--model sonnet --effort medium..high`; Thema nur P2-Hull + Audit-Streams, kein P1-Mischslice.

### Z11-F2 — P2-Hull fuer Audit-Streams (2026-05-06)

**Was passiert ist:** `CursorPageDto<T>` plus `CursorPageQuery` wurden als gemeinsame P2-Huelle eingefuehrt. Die Endpunkte `GET /admin/auth/audit` und `GET /admin/directory/audit` liefern jetzt `items`, `nextCursor`, `hasMore` statt nackter Arrays. Der Cursor ist opaque (Base64 ueber JSON `{occurredAt, id}`) — FE darf ihn nie zerlegen. Das Backend implementiert echtes Keyset-Pagination (`WHERE (created_at < @cursorTs OR (created_at = @cursorTs AND id < @cursorId)) ORDER BY created_at DESC, id DESC LIMIT @limit+1`). Im Frontend gibt es einen typed Wrapper (`web/src/services/api/cursorPage.ts`), den Hook `useAdminConfigData` akkumuliert bei „Mehr laden" statt zu ersetzen, und beide Audit-Tabs haben einen „Mehr laden"-Knopf.

**Was bedeutet das praktisch?**
- Wer im Audit „meine Aenderung von letzter Woche" sucht, kann jetzt zurueckblaettern. Heute schneidet der stille `limit=100`/`limit=50`-Cap die Historie ab, ohne dass das im UI sichtbar ist.
- Der „Mehr laden"-Knopf erscheint nur, wenn wirklich mehr Eintraege existieren — kein blinder Endlosscroll, kein stiller Cut.

**Warum lohnt es sich?**
- F2 ist eigenstaendige Hull-Familie (P2, Cursor-Stream ohne `total`), die F1 nicht beruehrt und F3 nicht blockiert. Der Audit-Stream passt semantisch zu P2: kein `total`, unbegrenzt rueckwaerts scrollbar, ohne COUNT-Query-Overhead.
- Der heutige stille Cap ist ein versteckter Informationsverlust — sicherheitsrelevante Audit-Eintraege koennen nach hinten fallen und fehlen dann im UI ohne Hinweis.

**Was wird dadurch besser, sicherer, schneller oder wartbarer?**
- **Sicherer:** vollstaendige Audit-Historie ist nun vollstaendig durchsuchbar; kein stiller Cap mehr.
- **Schneller:** keine COUNT-Query; der `limit+1`-Trick reicht zum HasMore-Check; Keyset-Pagination ist stabiler als OFFSET bei wachsendem Bestand.
- **Wartbarer:** ein zentraler `CursorPageDto<T>`-Hull-Typ fuer alle zukuenftigen Cursor-Streams (G Runtime-Sub-Resources, spaetere Audit-Endpunkte); FE-Akkumulations-Muster einmal etabliert, wiederverwendbar.
- **FE-Stabilitaet:** `useAdminConfigData`-Hook akkumuliert sauber auf „Mehr laden" und resettet auf Reload — keine doppelten Eintraege, kein Zustandskonflikt.

**Verifikation:** `dotnet build api/API.Tests/API.Tests.csproj` gruen (3 vorbestehende CS8602-Warnungen, 0 Fehler), `npm run build` unter `web/` gruen (`built in 5.00s`, nur vorbestehende Chunk-Size-Warnung).

**Restgrenzen von F2:** FE-Consumer-Hookfunktionen fuer Permission-Audit und Directory-Audit im `useAdminPermissionManagement`-Hook wurden auf die neue Signatur angepasst. Kein sichtbarer Filterzustand in der URL — das ist F3-Thema. `GET /admin/auth/audit` hat noch keinen `search`-Parameter — bewusst draussen (P2-Hull sieht `search` vor, war aber nicht im F2-Scope).

**Naechster Schritt:** Z11-F3 beauftragen — P1 ausrollen + D Builder-Tabs (sieben scoped Endpunkte); Modell opus/medium..high.

### Z11-F3 — P1-Ausrollen fuer Builder-Tabs (2026-05-06)

**Was passiert ist:** Die in F1 etablierte P1-Hull (`AdminListPageDto<T>` + `AdminListQuery`) wurde ohne neue Hull-Definition auf die sieben scoped Builder-Lese-Endpunkte ausgerollt. Betroffen: `GET /admin/config/workflow-definitions`, `/admin/config/action-definitions`, `/admin/config/task-templates`, `/admin/config/task-templates/{id}/conditions`, `/admin/config/task-templates/{id}/dependencies`, `/admin/config/answer-definitions`, `/admin/config/role-answer-defaults`. Repository- und Service-Signaturen entlang derselben Kette umgestellt; SQL mit `COUNT(*) OVER()`, `LIMIT/OFFSET`, ILIKE-`search` und festen Sort-Whitelists pro Endpunkt; `workflowDefinitionId` bzw. `templateId` bleiben Pflicht-Scope und sind nicht durch `search` ersetzbar. FE-Service-Wrapper (`getAdminTaskTemplates`, `…Conditions`, `…Dependencies`, `getAdminAnswerDefinitions`, `getAdminRoleAnswerDefaults`, `getAdminWorkflowDefinitions`, `getAdminWorkflowActionDefinitions`) liefern jetzt `Promise<AdminListPage<T>>`; alle Builder-Hooks (`useAdminTaskTemplateData`, `useAdminAnswerDefinitionManagement`, `useAdminRoleAnswerDefaults`, `useAdminWorkflowVersionReferenceData`, `useAdminWorkflowBuilder`) lesen pragmatisch `page.items` mit `limit: 200`. Schreibpfade, Versionierungs-/Publish-Pfade und der Dependency-Graph-Endpunkt blieben bewusst unangetastet.

**Was bedeutet das praktisch?**
- Der Builder bleibt schnell, auch wenn eine Definition Dutzende Aufgabenvorlagen, Antwortfelder oder Rollen-Defaults haelt — das Backend rechnet pro Tab nur einen Ausschnitt und kann gezielt suchen, statt blind den gesamten Bestand zu liefern.
- Suche und Sortierung sind jetzt fuer alle Builder-Listen serverseitig vorbereitet. Die heutige Inspector-UI nutzt noch `limit: 200`, aber der Vertrag ist nicht mehr auf einen Voll-Load festgenagelt.
- Der Vertrag der sieben Endpunkte sieht jetzt ueberall identisch aus wie F1, statt pro Tab eigene Mini-Antwortformen zu haben.

**Warum lohnt es sich?**
- F3 war der breiteste Slice von Z11 und der einzige, der die in F1 etablierte Hull voraussetzt. Ohne F3 waeren die Builder-Tabs als letzte grosse Insel mit nacktem Array-Vertrag stehengeblieben.
- Sieben Endpunkte in einem Pass umzustellen verhindert, dass sich pro Tab eine eigene Mini-Pagination einschleicht; F1/F2 haben das Muster bereits durchgespielt, F3 bringt es ans Ziel.
- Der Pflicht-Scope (`workflowDefinitionId` bzw. `templateId`) bleibt im Vertrag verankert — die Hull-Generalisierung schwaecht den Sicherheits-Boundary nicht auf.

**Was wird dadurch besser, sicherer, schneller oder wartbarer?**
- **Schneller:** Builder-Listen liefern bei wachsenden Definitionen stabile Antwortzeiten; Server-`COUNT(*) OVER()` plus `LIMIT/OFFSET` statt unbegrenzter Voll-Loads.
- **Wartbarer:** ein einziger Hull-Typ fuer alle Admin-Listen (Master-Data + Builder); FE bekommt einen einzigen typed Adapter (`AdminListPage<T>`) statt sieben Mini-Antwortformen.
- **Sicherer:** Pflicht-Scope bleibt zwingend, Sort-Felder sind Whitelist (kein freier `ORDER BY`-Injection-Vektor); ILIKE-`search` ueber im Endpunkt fest deklarierte Felder.
- **Konsistenter:** alle drei Z11-Slices nutzen am Ende dieselbe P1-Hull bzw. die schwesterliche P2-Hull — keine Mischvertraege, kein Drift.

**Verifikation:** `dotnet build api/API/API.csproj` gruen, `dotnet build api/API.Tests/API.Tests.csproj` gruen (3 vorbestehende CS8602-Warnungen, 0 Fehler), `npm run build` unter `web/` gruen (`built in 6.13s`, nur vorbestehende Chunk-Size-Warnung). `dotnet test` Unit-Tests 369/369 gruen; AdminConfig-Integration-Tests 22/22 gruen; WorkflowDefinition-Integration-Tests 9/9 gruen.

**Restgrenzen von F3:** Die Builder-Inspector-UI nutzt weiterhin `limit: 200` und liest `page.items`; sichtbare Paging-/Filter-UI mit Filterzustand in der URL-Query wurde bewusst nicht in F3 mitgenommen — das ist ein eigenstaendiger UI-Slice, der nicht mehr unter Z11 faellt. Der Dependency-Graph-Endpunkt (`GET /admin/config/dependency-graph`) bleibt strukturell ein Graph, kein Listen-Endpunkt — gehoert nicht in eine P1-Hull. Schreibpfade und Versionierungs-/Publish-Endpunkte sind weiterhin nackte JSON-Antworten ohne Hull.

**Naechster Schritt:** Z11 ist mit F3 vollstaendig geschlossen. Kein neuer Zyklus aus diesem Slice heraus eroeffnet — Codex entscheidet, welcher der nach Z11 vorgesehenen Folgekandidaten (A Identity-Listen, C `/admin/directory/identities`, C Gaps/Pending Split, E Notification-Templates, F Rotation, G Runtime-Sub-Resources, H Startable, oder ein eigenstaendiger Builder-UI-Slice fuer URL-Filterzustand) der naechste aktive Zyklus wird.

---

## Abgeschlossener Zyklus 10 — Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege und Query-Kontrakt-Risiken (2026-05-05)

**Status:** eroeffnet 2026-05-05, formal abgeschlossen 2026-05-05. Reiner Review-/Planungszyklus. Alle drei Planungs-Slices done. Keine Code-Umsetzung in Z10. Umsetzung ab Folgezyklus (vorgeschlagen Z11) gemaess § Z10-1.3 (F1 → F2 → F3).

**Thema:** Admin- und Master-Data-Listen (z. B. Departments, Rollen, Group-Role-Mappings, Identities, Pending-Imports, Audit-Logs, Workflow-Definitionsbestand) werden derzeit zu grossen Teilen ohne Pagination, ohne Suchparameter und ohne stabilen Sortier-Vertrag gegen Backend-API und Frontend gefahren. Der Hotspot #6 aus Z8-1.2 (`GetDepartmentsAsync`/`GetRolesAsync` ohne Pagination) ist nur die sichtbarste Stelle; das Muster betrifft mehrere Read-Pfade unter Admin-Konfiguration und Directory-Sync.

**Was bedeutet das praktisch?**
- Admin-Listen werden bei wachsender Datenmenge spuerbar langsamer zu laden, irgendwann bricht das UI-Rendering ein, oder die Liste zeigt zwar Eintraege, aber Filter und Reihenfolge fuehlen sich „zufaellig" an.
- Suche und Filter passieren heute ueberwiegend im Browser. Damit findet man nur, was bereits geladen wurde — wer mit „nicht da, sehe ich auch in der Liste nicht" rechnet, kann etwas uebersehen, das wirklich existiert.
- Ohne expliziten Pagination-/Sort-Vertrag aendert sich Verhalten still (z. B. zwei UI-Stellen sortieren unterschiedlich), und API-Konsumenten ausserhalb des Frontends koennen sich nicht auf eine stabile Reihenfolge stuetzen.

**Warum lohnt es sich, das anzugehen?**
- Die Plattform geht in Richtung mehr Workflows, mehr Definitionen, mehr Identitaeten und mehr Mappings — das Wachstum der Listen ist eingeplant, nicht zufaellig.
- Bisher gab es **keinen** akuten Last-Trigger, deshalb wurden Vertrags-Themen zugunsten der Last-Pfade in Z8/Z9 hintenangestellt. Dieser Zyklus zieht den Vertrags-Teil bewusst vor das Wachstum, statt ihn am Schmerzpunkt nachzuschieben.
- Klare Vertraege (`?limit`, `?offset` oder `?cursor`, `?search`, `?sort`) verhindern halbgaarige FE-Workarounds und reduzieren das Risiko, dass spaetere Performance-Hotfixes API-Brueche fuer das FE bedeuten.

**Was wird dadurch besser, sicherer, schneller oder wartbarer?**
- **Schneller:** Listen liefern bei wachsendem Bestand stabile Antwortzeiten, weil das Backend nur einen Ausschnitt rechnet.
- **Sicherer:** Such-/Filterergebnisse sind komplett, nicht nur der gerade geladene Browser-Block.
- **Wartbarer:** ein einheitlicher Listen-/Suchvertrag ist der gleiche Mechanismus an mehreren Endpunkten — neue Listen koennen ihn uebernehmen, statt jedes Mal eigene Sonderloesungen zu bauen.
- **FE-stabilitaet:** das Frontend kann gezielt Filter, Sortierung und Pagination uebernehmen, ohne sich auf „alles auf einmal"-Annahmen zu stuetzen.

**Begruendung gegen alternative Zyklen:**
- *Direkt Pagination implementieren ohne Inventur*: erzeugt halbgaaren Stand mit unterschiedlichen Vertraegen je Endpunkt — genau das, was vermieden werden soll. Z10 startet bewusst als Inventur + Plan.
- *Z8-3.2/#8 Rotation-Regeneration*: bleibt deferred — admin-getriggert, kein kleiner SQL-Hebel, kein Listen-Vertragsproblem.
- *Neuer Hygiene-Refactor anderer grosser Services*: ohne Wachstums-/Vertrags-Trigger waere das blindes Aufraeumen.

**Fokus:**
1. Inventur aller Read-/Listen-Pfade unter Admin-Konfiguration, Directory-Sync, Workflow-Definitionsbestand und verbundener Audit-Listen, die heute ohne Pagination/Suche laufen.
2. Vertrags-Skizze: pro Endpunkt entscheiden, ob Pagination per `limit/offset` oder Cursor sinnvoll ist, ob Server-Suche/-Sort gebraucht wird, und welche FE-Folgen daran haengen.
3. Slice-Plan: erste sichere Umsetzungsslices (API-Vertrag + minimale FE-Adaption) als Vorschlag fuer einen Folgezyklus formulieren — **nicht** in Z10 implementieren.

**Leitplanken:**
- Z10 ist Review-/Planungszyklus, keine Implementierung. Kein Code-Slice in Z10 wird als done markiert, das eine API-Vertragsaenderung oder DB-Aenderung enthaelt.
- FE-Folgen werden klar benannt, sobald die Inventur sie sichtbar macht — kein „kuenstliches FE-TODO" ohne API-Trigger.
- Kein paralleler zweiter Hebel; Z10 bleibt thematisch fokussiert.
- Vertrags-Skizze muss explizit erklaeren, was der Vertrag fuer normal verstaendliche Leser im Alltag bedeutet (siehe Schreibregel oben), nicht nur Parameter-Listen.

**Geplante Slices (Erst-Definition, nicht Umsetzung):**

| ID | Aufgabe | Prio | Reasoning | Modell | Status |
|----|---------|------|-----------|--------|--------|
| Z10-1.1 | Inventur: alle Admin-/Master-Data-/Directory-Read-Endpunkte und ihre Repository-/Service-Pfade ohne Pagination/Suche/Sort-Vertrag dokumentieren (Datei/Symbol, Rueckgabeform, aktuelle Aufrufer im FE, beobachtete Kardinalitaet, Spuerbarkeit fuer Nutzer) | HIGH | high | opus | done (2026-05-05) — Ergebnis in § Z10-1.1 |
| Z10-1.2 | Vertrags-Skizze: pro identifiziertem Endpunkt entscheiden — `limit`/`offset` vs. Cursor, Server-`search` ja/nein, stabiler `sort`-Vertrag ja/nein, Antwort-Hull (`items` + `total`/`nextCursor`); jeweils kurz erklaeren, was sich fuer den Nutzer aendert und welche FE-Adaption noetig waere | HIGH | high | opus | done (2026-05-05) — Ergebnis in § Z10-1.2 (P1/P2/P3, Bloecke A–H) |
| Z10-1.3 | Slice-Plan fuer Folgezyklus: erste 2–3 sichere Umsetzungsslices priorisieren (API-Vertrag + minimaler FE-Adaption) inkl. Begruendung, warum gerade die zuerst; Vorschlag, welche Endpunkte in Z10 absichtlich noch nicht angefasst werden und warum | HIGH | medium..high | opus | done (2026-05-05) — Ergebnis in § Z10-1.3 (F1 P1+B → F2 P2+Audit → F3 P1+D Builder) |

**Erwartete Ausgaenge aus Z10:**
- aktualisierte Inventur in `CODE_REVIEW.md` § Z10-1.1
- Vertrags-Skizze in `CODE_REVIEW.md` § Z10-1.2
- Slice-Plan fuer Folgezyklus in `CODE_REVIEW.md` § Z10-1.3
- bei sichtbaren FE-Folgen: kurzer Hinweis in `FRONTEND_TODO.md` mit Trigger-Kennzeichnung, **nicht** als praeventives FE-TODO

**Reihenfolge / Abhaengigkeiten:**
- Z10-1.1 → Z10-1.2 → Z10-1.3 streng sequenziell. Inventur vor Vertrag, Vertrag vor Slice-Plan.
- Umsetzungs-Slices entstehen erst in einem Folgezyklus, nicht in Z10.

### Z10-1.1 Inventur Admin-/Master-Data-/Directory-Read-Endpunkte (2026-05-05)

Reine Inventur, kein Code-Change. Aufgenommen wurden alle GET-Endpunkte unter Admin-Konfiguration, Directory-Sync, Workflow-Definitionsbestand und Master-Data, deren Antwort eine **Liste** ist und die heute **keinen** vollstaendigen Pagination-/Server-Such-/Sort-Vertrag haben. Ein Endpunkt zaehlt zur Inventur, sobald mindestens eine der drei Achsen fehlt: (a) Pagination (`limit`/`offset` oder Cursor), (b) Server-`search`, (c) explizit dokumentierter `sort`. Detail-Reads (Single-Doc), reine Aggregat-Endpunkte (z. B. `/admin/system/logs/summary`) und bereits sauber vertragene Listen (siehe „Bereits vertraglich saubere Listen" am Ende) bleiben aussen vor.

**Schreibregel angewandt:** zu jedem Block kurz „Praktisch / Lohnenswert / Nutzen" pro Nutzersicht. Keine Vertragsentscheidung — die folgt in Z10-1.2.

#### A) Identity & Permissions (`AdminOrgEndpoints.cs`)

| Endpunkt (Datei:Zeile) | FE-Aufrufer | Aktueller Vertrag (Rueckgabe) | Achsen-Defizit |
|---|---|---|---|
| `GET /admin/auth/users` (`AdminOrgEndpoints.cs:12`) → `IUserAuthorizationRepository.GetAdminUsers()` | `web/src/services/adminApi.ts:217` `getAdminUsers()` (Admin-Konfig „Benutzer & Rollen") | `List<AdminUserDto>` als JSON-Array, kein Hull, keine Total | (a) keine Pagination, (b) keine Server-`search`, (c) kein dokumentierter `sort` |
| `GET /admin/auth/roles` (`AdminOrgEndpoints.cs:31`) → `GetAdminRoles()` | `adminApi.ts:221` (Admin-UI Rollenpflege) | `List<AdminRoleDto>` | a/b/c fehlen |
| `GET /admin/auth/groups` (`AdminOrgEndpoints.cs:50`) → `GetAdminGroups()` | `adminApi.ts:225` (Gruppenmatrix) | `List<AdminGroupDto>` | a/b/c fehlen |
| `GET /admin/auth/permissions` (`AdminOrgEndpoints.cs:69`) → `GetAdminPermissions()` | `adminApi.ts:229` (Rollen-Permission-Editor) | `List<AdminPermissionDto>` | a/b/c fehlen — Bestand heute klein, mittelfristig stabil |
| `GET /admin/auth/audit?limit` (`AdminOrgEndpoints.cs:88`) → `GetAdminPermissionAudit(limit ?? 100)` | `adminApi.ts:234` (Permission-Audit-Liste) | `List<AdminPermissionAuditEntryDto>`; nur `limit` (Default 100), kein Offset, kein Cursor, keine Filter | (a) nur Cap, kein Weiterblaettern; (b) keine Server-`search`/Filter (z. B. nach Actor, Reason); (c) kein expliziter `sort` |

**Praktisch:** Listen werden bei wachsender Nutzerschaft/Rollenzahl traege geladen, FE filtert/sucht heute clientseitig — was nicht im JSON-Array drin ist, findet niemand. Beim Audit-Log heisst „limit=100" praktisch: alles ab dem 101. Eintrag fehlt unsichtbar.
**Lohnenswert:** Identity-Bestand wird mit Entra-Sync und Plattformwachstum monoton groesser; das Audit-Log waechst pro Aenderung, ein Cap blendet Historie still aus.
**Nutzen:** vorhersehbare Antwortzeiten, vollstaendige Suche/Filter (z. B. „alle Aenderungen durch User X"), klar dokumentierter `sort`, der zwischen UI und API-Konsumenten gleich aussieht.

#### B) Master-Data (`AdminOrgEndpoints.cs` + `WorkflowMasterDataEndpoints.cs`)

| Endpunkt (Datei:Zeile) | FE-Aufrufer | Aktueller Vertrag | Achsen-Defizit |
|---|---|---|---|
| `GET /admin/master-data/departments` (`AdminOrgEndpoints.cs:108`) → `GetAdminDepartmentAssignments()` | `adminApi.ts:238` (Abteilungs-/Lead-Pflege, Zuweisungen) | `List<AdminDepartmentAssignmentDto>` | a/b/c fehlen |
| `GET /admin/master-data/positions` (`AdminOrgEndpoints.cs:127`) → `GetAdminDepartmentPositions()` | `adminApi.ts:242` (Positionen-Tab unter Abteilung) | `List<AdminRoleDto>` (Positionen abteilungsuebergreifend) | a/b/c fehlen |
| `GET /admin/master-data/responsibilities` (`AdminOrgEndpoints.cs:306`) → `GetAdminResponsibilityOwners()` | `adminApi.ts:246` (Responsibility-Pflege) | `List<AdminResponsibilityOwnerDto>` | a/b/c fehlen |
| `GET /departments` (`WorkflowMasterDataEndpoints.cs:12`) → `IWorkflowCatalogService.GetDepartmentsAsync()` | `web/src/services/lookupApi.ts:18` (Workflow-Erfassung, Filter) | `List<DepartmentDto>` (kompletter aktiver Bestand) | a/b/c fehlen — **Hotspot #6 aus Z8-1.2** |
| `GET /roles` (`WorkflowMasterDataEndpoints.cs:29`) → `GetRolesAsync(user)` | `lookupApi.ts:10` (Workflow-Erfassung, Konfig-Auswahl) | `List<RoleDto>` (alle Rollen, einmal pro User-Aufruf) | a/b/c fehlen — **Hotspot #6 aus Z8-1.2** |

**Praktisch:** Wer eine Abteilung oder Rolle eintippt, sucht im Browser — die UI laedt vorher die ganze Liste. Bei wachsender Org wird der Erfassungs-Dialog merklich traeger, und beim Filtern fehlt eine sichtbare Stelle ab der die Liste „mehr koennte" — niemand sieht, dass Eintraege ueberhaupt fehlen koennten.
**Lohnenswert:** Departments/Rollen sind Pflicht-Lookups in fast allen Workflow-Erfassungsmasken. Schon bei mittlerer Org-Groesse spuerbar; Hotspot #6 ist dort markiert, wurde in Z8 wegen FE-Folge und ohne akuten Last-Trigger aber bewusst zurueckgestellt.
**Nutzen:** Server-Suche im Erfassungsdialog (typeahead-fuehrend), schnelle Initialantwort, gleiches Verhalten in Master-Data-Pflege und Workflow-Erfassung — heute bedienen die zwei Pfade dieselben Daten ueber zwei Vertraege.

#### C) Directory-Sync (`AdminDirectorySyncEndpoints.cs`)

| Endpunkt (Datei:Zeile) | FE-Aufrufer | Aktueller Vertrag | Achsen-Defizit |
|---|---|---|---|
| `GET /admin/directory/groups` (`AdminDirectorySyncEndpoints.cs:67`) → `IDirectorySyncService.GetGroupsAsync()` | `adminConfigApi.ts:65` (Directory-Tab „Gruppen", Group-Role-Mapping-Editor) | `List<AdminDirectoryGroupDto>` | a/b/c fehlen |
| `GET /admin/directory/identities?limit&offset` (`AdminDirectorySyncEndpoints.cs:86`) → `GetIdentitiesAsync(limit ?? 100, offset ?? 0)` | `adminConfigApi.ts:68-77` (Directory-Tab „Identitaeten") | `List<AdminDirectoryIdentityDto>`; **hat** `limit`/`offset`, **kein** Hull mit Total/`hasMore`, **keine** Server-`search`, **kein** `sort` | (a) Pagination ohne Total/`hasMore` — UI weiss nicht, ob es eine naechste Seite gibt; (b) Server-Suche fehlt; (c) `sort` fehlt — Reihenfolge ergibt sich rein aus Repo-Implementierung |
| `GET /admin/directory/responsibility-gaps` (`AdminDirectorySyncEndpoints.cs:107`) → `GetResponsibilityGapsAsync()` | `adminConfigApi.ts:79` (Directory-Tab Luecken-Badge + Detail) | `DirectoryResponsibilityGapsDto` mit eingebetteten Listen (gemischter Hull) | a/b/c fehlen — Gaps-Listen koennen mit Bestand wachsen |
| `GET /admin/directory/pending-imports` (`AdminDirectorySyncEndpoints.cs:126`) → `GetPendingImportsAsync()` | `adminConfigApi.ts:83` (Directory-Tab „Offene Imports") | `DirectoryPendingImportsDto` mit eingebetteten Listen | a/b/c fehlen — Pending-Liste kann nach grossen Syncs hoch gehen |
| `GET /admin/directory/audit?limit` (`AdminDirectorySyncEndpoints.cs:183`) → `GetMappingAuditAsync(limit ?? 50)` | `adminConfigApi.ts:94` (Directory-Audit-Detail) | `List<AdminDirectoryMappingAuditEntryDto>`; nur `limit` (Default 50), kein Offset/Cursor, keine Filter, kein `sort` | (a) nur Cap, kein Weiterblaettern; (b) keine Server-`search`; (c) kein `sort` |

**Praktisch:** Identitaeten/Gaps/Pending-Imports sind die Bildschirme, an denen man nach einem Sync nachsieht, was zu tun ist — heute sieht man entweder „die ersten 100" oder den ganzen Block ohne Suchhilfe. Wer nach einem konkreten User oder einer Mailadresse sucht, scrollt oder druckt Strg+F im Browser. Beim Audit zeigt „die letzten 50" still nur einen Ausschnitt, ohne dass jemand merkt: davor gab es noch was.
**Lohnenswert:** Sync-Listen wachsen mit jedem Sync-Lauf monoton (insb. Audit, Pending-Imports); ohne klaren Vertrag werden hier zuerst Alltagsbeschwerden auflaufen, sobald die Org groesser wird oder Sync-Probleme aufgearbeitet werden muessen.
**Nutzen:** vollstaendige Suche/Filter (z. B. „nur Konflikte"), Pagination mit `total`/`hasMore` — UI kann ehrlich „Seite 3 von n" zeigen statt zu raten; gleicher Vertrag fuer Identitaeten und Audit, der die Sync-UI insgesamt ruhiger macht.

#### D) Workflow-Konfiguration / Builder (`AdminWorkflowDefinitionConfigEndpoints.cs`, `AdminProcessConfigEndpoints.cs`, `AdminAnswerConfigEndpoints.cs`)

| Endpunkt (Datei:Zeile) | FE-Aufrufer | Aktueller Vertrag | Achsen-Defizit |
|---|---|---|---|
| `GET /admin/config/workflow-definitions` (`AdminWorkflowDefinitionConfigEndpoints.cs:13`) → `IWorkflowRepository.GetAdminWorkflowDefinitions()` | `adminConfigApi.ts:342` (Workflow-Builder-Liste, AdminConfig-Bundle) | `List<WorkflowDefinitionSummaryDto>` (alle Definitionen) | a/b/c fehlen — Definitionsbestand waechst monoton mit dem Plattformziel (Versionierung) |
| `GET /admin/config/action-definitions` (`AdminWorkflowDefinitionConfigEndpoints.cs:32`) → `IWorkflowAutomationService.GetActionDefinitionsAsync()` | `adminConfigApi.ts:502` (Builder Inspector / Action-Picker) | `List<ActionDefinitionDto>` | a/b/c fehlen — Katalog waechst mit jeder Automation Definition |
| `GET /admin/config/task-templates?workflowDefinitionId` (`AdminProcessConfigEndpoints.cs:12`) → `GetAdminTaskTemplates(definitionId)` | `adminConfigApi.ts:118-123` (Builder „Aufgabenvorlagen") | `List<AdminTaskTemplateDto>`, gefiltert nur per Pflichtparameter `workflowDefinitionId` | a/b/c fehlen |
| `GET /admin/config/task-templates/{id}/conditions` (`AdminProcessConfigEndpoints.cs:131`) → `GetAdminTaskTemplateConditions(id)` | `adminConfigApi.ts:194-196` (Builder Inspector) | `List<AdminTaskTemplateConditionDto>` | a/b/c fehlen |
| `GET /admin/config/task-templates/{id}/dependencies` (`AdminProcessConfigEndpoints.cs:219`) → `GetAdminTaskTemplateDependencies(id)` | `adminConfigApi.ts:231-233` (Builder Inspector) | `List<AdminTaskTemplateDependencyDto>` | a/b/c fehlen |
| `GET /admin/config/answer-definitions?workflowDefinitionId` (`AdminAnswerConfigEndpoints.cs:12`) → `GetAdminAnswerDefinitions(definitionId)` | `adminConfigApi.ts:268-270` (Builder „Antwortfelder") | `List<AdminAnswerDefinitionDto>` | a/b/c fehlen |
| `GET /admin/config/role-answer-defaults?workflowDefinitionId` (`AdminAnswerConfigEndpoints.cs:131`) → `GetAdminRoleAnswerDefaults(definitionId)` | `adminConfigApi.ts:321-323` (Builder „Rollen-Defaults") | `List<AdminRoleAnswerDefaultDto>` | a/b/c fehlen — skaliert ueber Rollen × Antwortfelder |

**Praktisch:** Der Builder laedt pro Definition heute ihre Vorlagen, Bedingungen, Abhaengigkeiten und Antwortfelder als kompletten Block — und filtert clientseitig. Bei kleinen Definitionen bleibt das unauffaellig; sobald eine Definition Dutzende Aufgabenvorlagen oder Rollen-Defaults hat, wird der Builder beim ersten Klick traege, und Suche im Inspector findet nur, was bereits im Block enthalten ist.
**Lohnenswert:** Versionierte Definitionen sind ausdruecklich Zielarchitektur — die Listen wachsen kuenftig sowohl pro Definition (mehr Bausteine) als auch ueber Definitionen hinweg (mehr Versionen, mehr Definitionen). Builder-Antwortzeit ist Adminerlebnis Nummer eins.
**Nutzen:** ein einheitlicher Listen-/Sucheintritt im Builder fuehlt sich gleich an, egal welche Definition oder welcher Tab; Pagination/Server-Suche schuetzt die Builder-Performance, sobald Definitions- oder Vorlagebestand groesser werden.

#### E) Notification Templates List (`AdminNotificationTemplateEndpoints.cs`)

| Endpunkt (Datei:Zeile) | FE-Aufrufer | Aktueller Vertrag | Achsen-Defizit |
|---|---|---|---|
| `GET /admin/notification-templates` (`AdminNotificationTemplateEndpoints.cs:12`) → `INotificationTemplateService.GetAdminTemplates()` | `adminApi.ts:144` (Admin-Tab Notification-Templates) | `List<AdminNotificationTemplateDto>` | a/b/c fehlen |

**Praktisch:** Die Template-Uebersicht laedt aktuell den ganzen Bestand. Heute klein und stabil, deshalb nicht akut, aber das Wachstumsmuster (mehr Workflows → mehr Templates → mehr Sprachvarianten) zeigt in dieselbe Richtung wie der Rest.
**Lohnenswert:** Mit mehr Workflow-Definitionen waechst die Liste monoton; ohne Such-/Sort-Vertrag wird sie schnell unhandlich.
**Nutzen:** konsistenter Vertrag mit den Admin-Listen oben, billiger einzeichnen, solange der Bestand klein ist.

#### F) Rotation Action Templates (`AdminRotationConfigEndpoints.cs`)

| Endpunkt (Datei:Zeile) | FE-Aufrufer | Aktueller Vertrag | Achsen-Defizit |
|---|---|---|---|
| `GET /admin/rotation/action-templates?departmentId&isActive` (`AdminRotationConfigEndpoints.cs:11`) → `IRotationTemplateAdminService.GetDepartmentActionTemplatesAsync(departmentId, isActive)` | `web/src/services/rotationApi.ts:138` (Rotation-Konfig „Maßnahmenvorlagen") | `List<DepartmentActionTemplateDto>`; Filter nur per `departmentId`/`isActive`, kein `limit`/`offset`/`search`/`sort` | a/b/c fehlen |

**Praktisch:** Pro Abteilung kann der Vorlagenbestand wachsen, Suche im Browser; Reihenfolge wird heute durch das Repo bestimmt — der Admin merkt nicht, dass es eine implizite Sortierung gibt.
**Lohnenswert:** Rotation ist im B+-Note-Kontext aktiv weiter ausgebaut worden; mehr Vorlagen je Abteilung sind absehbar.
**Nutzen:** explizite Sortierung („nach `sortOrder`, dann Titel") und Server-`search` machen die Maßnahmenpflege im Alltag verlaesslich.

#### G) Runtime-Sub-Resources (`AdminWorkflowRuntimeEndpoints.cs`)

| Endpunkt (Datei:Zeile) | FE-Aufrufer | Aktueller Vertrag | Achsen-Defizit |
|---|---|---|---|
| `GET /admin/runtime/workflow-instances/{uid}/events` (`AdminWorkflowRuntimeEndpoints.cs:65`) → `GetWorkflowInstanceEventsAsync(uid)` | indirekt aus dem Runtime-Detail-Tab im Admin (Event-Trace einer Instanz) | `List<WorkflowRuntimeEventDto>` (alle Events der Instanz) | a/b/c fehlen — Events wachsen monoton ueber den Lifecycle der Instanz |
| `GET /admin/runtime/workflow-instances/{uid}/automation-jobs` (`AdminWorkflowRuntimeEndpoints.cs:85`) → `GetWorkflowAutomationJobsAsync(uid)` | gleicher Tab | `List<AutomationJobDetailDto>` (alle Jobs der Instanz) | a/b/c fehlen — Jobs wachsen mit Retry/Wiederholung |

**Praktisch:** Der Runtime-Detail-Tab bringt aktuell den gesamten Event-/Job-Verlauf einer Instanz auf einen Schlag in den Browser. Bei langen oder retry-lastigen Instanzen wird der Tab spuerbar traege, und „relevante Stelle finden" laeuft ueber Strg+F.
**Lohnenswert:** Wenn Z8 weiter geht und mehr Automation in der Plattform landet, waechst die Anzahl der Events/Jobs pro Instanz schneller als der Workflow-Bestand selbst.
**Nutzen:** konsistenter Listen-/Sort-Vertrag (zeitlich, Filter nach Severity/Status), damit der Admin eine Instanz auch nach Wochen Laufzeit ergonomisch nachvollziehen kann.

#### H) Workflow Catalog Lookup (`WorkflowMasterDataEndpoints.cs`)

| Endpunkt (Datei:Zeile) | FE-Aufrufer | Aktueller Vertrag | Achsen-Defizit |
|---|---|---|---|
| `GET /workflow-definitions/startable` (`WorkflowMasterDataEndpoints.cs:46`) → `IWorkflowCatalogService.GetStartableWorkflowDefinitionsAsync(user)` | `lookupApi.ts:14` (Workflow-Erfassung „startbare Definitionen") | `List<WorkflowStartableDefinitionDto>` (alle fuer den User startbaren Definitionen) | a/b/c fehlen — Bestand waechst mit Plattformziel |

**Praktisch:** Wer einen Workflow startet, bekommt heute alle erlaubten Definitionen auf einen Schlag — perfekt fuer 5–10 Definitionen, wird aber unuebersichtlich, sobald der Definitionsbestand spuerbar waechst (z. B. wenn Versionen pro Workflow als separate startbare Eintraege erscheinen).
**Lohnenswert:** Plattformziel ist „mehrere Workflows auf einem Plattformkern" — die Liste wird absehbar groesser.
**Nutzen:** Server-`search` und stabiler `sort` (z. B. zuletzt benutzt) machen den Erfassungs-Dialog auch bei vielen Definitionen schnell.

#### Bereits vertraglich saubere Listen (informativ, nicht Z10-Material)

| Endpunkt | Stand |
|---|---|
| `GET /workflows?status&department&workflowDefinitionKey&search&responsibility&limit&offset` (`WorkflowEndpoints.cs:85`) | hat vollen Vertrag (Filter + `search` + `limit`/`offset` + Hull `WorkflowListPageDto`) — Z2-Resultat |
| `GET /workflows/{uid}/audit-log?limit&offset` (`WorkflowEndpoints.cs:158`) | hat `limit`/`offset` |
| `GET /admin/system/logs?...&limit&offset` (`AdminSystemLogEndpoints.cs:12`) | reicher Filter + `search` + `limit`/`offset` |
| `GET /admin/system/logs/summary?...` (`AdminSystemLogEndpoints.cs:54`) | Aggregat, keine Liste |
| `GET /workflow-target-person-sources?query&limit` / `/workflow-target-people?query&limit` / `/people/search?query&limit` / `/people/rotation-eligible?query&limit` (`WorkflowMasterDataEndpoints.cs:63..152`) | Server-`search` + `limit` (typeahead-Vertrag) |
| `GET /admin/notification-templates/preview-targets/workflows?query&limit` / `…/rotation-plans?query&limit` (`AdminNotificationTemplateEndpoints.cs:79..117`) | Server-`search` + `limit` |
| `GET /rotation/plans/{planId}/audit?limit&offset` und `…/notifications?limit&offset` (`RotationPlanningEndpoints.cs:76..129`) | `limit`/`offset` mit Validierung |

#### Bewusst aussen vor (Detail-/Aggregat-Lese-Endpunkte)

- `GET /admin/directory/status`, `GET /admin/runtime/workflow-instances/{uid}`, `GET /admin/config/workflow-definition-versions/{id}`, `GET /admin/config/workflow-definitions/{id}/dependency-graph` — Single-Doc bzw. Graph-Payload, keine Listen, kein Listen-Vertragsproblem.

#### Beobachtete Kardinalitaet (Annahme statt Messung)

Die heutige Datenbasis ist klein — keine direkt messbare Lastbeschwerde, deshalb auch der bisherige bewusste Verzicht auf Pagination an diesen Pfaden (siehe Z10-Begruendung). Wachstumsrichtung pro Block:

- **A Identity & Permissions:** Users + Audit waechst mit Org-Groesse und Permission-Aenderungen monoton; Roles/Groups/Permissions klein und stabil.
- **B Master-Data + `/departments`/`/roles`:** Departments einige Dutzend, Positionen Hunderte, Responsibilities Dutzende — beim FE-Lookup auf jeden Workflow-Erfassungseinstieg.
- **C Directory:** Identitaeten skalieren wie Org-Groesse, Audit/Pending wachsen pro Sync-Run.
- **D Builder:** Definitions-, Vorlagen-, Bedingungs- und Default-Bestand waechst durch Plattformziel (mehr Workflows/Versionen).
- **E/F Notification + Rotation Templates:** waechst pro Workflow- bzw. Abteilungs-Konfiguration.
- **G Runtime Sub-Resources:** waechst pro Instanz mit Lifecycle/Retry — pro Instanz potenziell unbeschraenkt.
- **H Startable:** waechst mit Plattformziel.

#### Spuerbarkeit fuer Nutzer (zusammengefasst)

- **Admin-Konfiguration (A/B/D/E):** „die Liste laedt erst lange, danach finde ich genau, was schon da ist" — Hauptbeschwerde wird Antwortzeit + Suche, sobald der Bestand spuerbar groesser wird.
- **Directory-Sync (C):** „nach dem Sync sehe ich nur die ersten 100 Identitaeten / die letzten 50 Audit-Eintraege" — Schnitt heute unsichtbar fuer den Admin.
- **Builder (D):** „der Builder ist langsam beim ersten Klick auf eine grosse Definition" — relevant fuer Adoption der neuen Builder-Erfahrung.
- **Runtime-Detail (G):** „Eventliste einer langen Instanz ist unergonomisch" — relevant fuer Diagnose.
- **Workflow-Erfassung (B/H):** Endnutzer-Pfad — sobald der Definitions-/Department-Bestand groesser wird, wird das die erste Stelle, die auffaellt.

#### Mögliche FE-Folgen (nur Notiz, kein TODO)

Sichtbar sind heute potenzielle Adaptionen in `web/src/services/adminApi.ts` (A/B/E), `adminConfigApi.ts` (C/D), `lookupApi.ts` (B/H), `rotationApi.ts` (F) — sobald in Z10-1.2 ein konkreter Vertrag (z. B. Hull `items` + `total`/`nextCursor`, Server-`search`) gesetzt wird. Kein praeventives FE-TODO; FE-Eintraege entstehen erst, wenn aus Z10-1.2 ein konkreter API-Vertragsschnitt folgt.

#### Naechster Schritt

Z10-1.2 — Vertrags-Skizze pro identifiziertem Endpunkt auf Basis dieser Inventur. Reihenfolgenkandidaten (informativ, ohne Vorgriff): Hotspot-naheste FE-spuerbare Pfade (B `/departments`, `/roles`; D `task-templates`/`answer-definitions`; C `identities`/`audit`) zuerst skizzieren, weil dort der Wachstumstrigger zuerst sichtbar wird.

### Z10-1.2 Vertrags-Skizze Admin-/Master-Data-/Directory-Read-Endpunkte (2026-05-05)

Reine Skizze auf Basis von Z10-1.1, kein Code-Change und keine API-Vertragsaenderung. Pro Endpunkt wird das Zielmuster benannt (welcher Pagination-Stil, ob Server-`search`, ob stabiler `sort`, welche Antwort-Hull) plus die minimal noetige FE-Adaption als Folge — nicht als TODO. Vertraege werden bewusst **gemustert** statt pro Endpunkt einzeln, damit FE-Adapter wiederverwendbar bleiben und neue Listen das gleiche Verhalten erben.

**Schreibregel angewandt:** zu jedem Block kurz „Praktisch / Lohnenswert / Nutzen" pro Nutzersicht. FE-Folgen nur als Konsequenz des Vertrags, nicht als neues TODO.

#### Gemeinsame Muster (P1/P2/P3)

Aus Z10-1.1 ergeben sich drei klar unterscheidbare Vertrags-Familien. Jeder Endpunkt unten verweist auf eine davon — das hält den Plattformvertrag stabil und vermeidet, dass jeder Endpunkt sein eigenes Format erfindet.

| Muster | Wofür | Query-Parameter | Antwort-Hull | Sort-Vertrag |
|---|---|---|---|---|
| **P1 — Standard Admin Page** | endliche, monoton wachsende Adminlisten (Identity, Master-Data, Builder-Konfiguration, Directory-Status, Notification-/Rotation-Templates, Identitaeten) | `?limit&offset&search&sort` plus endpunkt-eigene Filter | `AdminListPageDto<T> { items: T[], total: int, limit: int, offset: int }` | `sort=field[:asc|desc][,field2[:dir]]`, Server validiert pro Endpunkt zugelassene Felder; Default explizit dokumentiert; Tie-Breaker immer `id` |
| **P2 — Cursor Stream** | append-only Logs/Events/Jobs mit potenziell unbeschraenktem Wachstum (Audit-Tabellen, Runtime-Events/Jobs) | `?limit&cursor&search` plus Filter (z. B. `severity`, `actor`, `since`) | `CursorPageDto<T> { items: T[], nextCursor: string?, hasMore: bool }` | implizit chronologisch absteigend `(occurredAt desc, id desc)` als Cursor-Schluessel; kein Sort-Wechsel zur Laufzeit |
| **P3 — Typeahead Lookup** | bewusst unvollstaendige Suche fuer Auswahl-Dialoge mit Eingabe-Trigger | `?query&limit` (bereits etabliert, siehe Z10-1.1 „bereits saubere Listen") | bare `T[]` (durch `limit` gedeckelt) | implizit Treffer-Score / Display-Name |

**Regeln fuer alle drei Muster:**
- `limit` Default 50, Maximum 200; Server klemmt Werte ausserhalb der Grenzen statt Fehler — Schutz vor versehentlichem „limit=10000".
- `search` ist case-insensitive, ueber im Endpunkt fest deklarierte Felder. FE darf nicht raten, wonach gesucht wird.
- `sort`-Felder sind eine **Whitelist pro Endpunkt**. Keine freien Spaltenausdruecke aus dem FE — schliesst SQL-Injection und semantisch instabile Sortierungen aus.
- Bei P1 wird `total` einmal pro Request gezaehlt (ein zusaetzliches `COUNT(*)` ueber dieselbe WHERE-Klausel). Das ist auf Adminlisten in dieser Groesse vertretbar; bei P2 bewusst weggelassen, weil Audit-/Event-Bestaende den Count-Aufwand nicht mehr rechtfertigen.
- Cursor in P2 ist **opaque** (Base64 ueber `(timestamp, id)`); FE behandelt ihn als String, nie zerlegen.

**Praktisch:** drei feste Muster bedeuten, dass das FE genau drei Adapter braucht (`AdminListPageDto<T>`, `CursorPageDto<T>`, plain `T[]`) und neue Listen sich automatisch in einen davon einfuegen.
**Lohnenswert:** verhindert, dass jeder Endpunkt am Ende ein eigenes Hull-Schema bekommt — genau das wuerde halbgare FE-Workarounds erzwingen.
**Nutzen:** ein Adminerlebnis, das ueberall gleich reagiert; das Backend kann die drei Hulls zentral validieren und testen, statt pro Endpunkt.

#### A) Identity & Permissions

| Endpunkt | Muster | Server-`search` | Stabiler `sort` (Whitelist) | Antwort-Hull | Filter |
|---|---|---|---|---|---|
| `GET /admin/auth/users` | P1 | ja: ueber `displayName`, `email`, `identityKey` | `displayName` (Default asc), `email`, `lastLoginAt`, `isActive` | `AdminListPageDto<AdminUserDto>` | `?role`, `?group`, `?isActive` |
| `GET /admin/auth/roles` | P1 | ja: ueber `key`, `name` | `name` (Default asc), `key` | `AdminListPageDto<AdminRoleDto>` | — |
| `GET /admin/auth/groups` | P1 | ja: ueber `key`, `name` | `name` (Default asc), `key` | `AdminListPageDto<AdminGroupDto>` | — |
| `GET /admin/auth/permissions` | P1 | ja: ueber `key`, `description` | `key` (Default asc) | `AdminListPageDto<AdminPermissionDto>` | `?role` |
| `GET /admin/auth/audit` | **P2** | ja: ueber `actorIdentityKey`, `targetIdentityKey`, `reason` | implizit `(occurredAt desc, id desc)` | `CursorPageDto<AdminPermissionAuditEntryDto>` | `?actor`, `?action`, `?since`, `?until` |

**Praktisch:** Benutzer-/Rollen-/Gruppenpflege fuehlt sich konsistent an — gleiche Suchbox, gleiches Pagination-Muster ueber alle Tabs. Beim Permission-Audit ersetzt der Cursor den heutigen `limit=100`-Cap; man kann aelter weiterblaettern statt blind abgeschnitten zu werden.
**Lohnenswert:** Identity-Bestand waechst mit Entra-Sync monoton; Audit-Log waechst pro Aenderung. Die Beschwerde „ich sehe meine letzte Aenderung nicht mehr" entsteht heute genau am Audit-Cap.
**Nutzen:** vollstaendige Suche und vorhersagbare Reihenfolge in der UI; Audit ist beliebig zurueckblaetterbar, ohne dass das Backend Riesen-Counts rechnen muss.
**FE-Folge (Konsequenz, kein TODO):** `adminApi.getAdminUsers/Roles/Groups/Permissions` muss von `Promise<T[]>` auf `Promise<AdminListPageDto<T>>` umgestellt werden; clientseitige Filter in den vier Tabs entfallen. Permission-Audit-Tab braucht einen „Mehr laden"-Knopf statt eines fixen Caps.

#### B) Master-Data + Workflow-Lookups (`/departments`, `/roles`)

Hier teilen sich zwei Konsumentenklassen denselben Datenbestand: die **Pflege-Listen** in der Admin-Konfiguration (Vollbild, Sortier-/Filter-fuehrend) und die **Lookup-Aufrufe** in der Workflow-Erfassung (Auswahl-Dialog, eingabe-getrieben). Statt zwei getrennte Endpunkte mit zwei Vertraegen wird das gemeinsame Muster ausdruecklich benannt.

| Endpunkt | Muster | Server-`search` | Stabiler `sort` (Whitelist) | Antwort-Hull | Filter |
|---|---|---|---|---|---|
| `GET /admin/master-data/departments` | P1 | ja: `name`, `key` | `name` (Default asc), `key` | `AdminListPageDto<AdminDepartmentAssignmentDto>` | `?leadStatus` (`resolved`/`missing`/`conflict`) |
| `GET /admin/master-data/positions` | P1 | ja: `name`, `key` | `name` (Default asc), `departmentName` | `AdminListPageDto<AdminRoleDto>` | `?departmentId` |
| `GET /admin/master-data/responsibilities` | P1 | ja: `key`, `name` | `name` (Default asc) | `AdminListPageDto<AdminResponsibilityOwnerDto>` | `?departmentId`, `?ownerType` |
| `GET /departments` | P1 (mit P3-kompatiblem Default) | ja: `name`, `key` | `name` asc | `AdminListPageDto<DepartmentDto>` | — |
| `GET /roles` | P1 (mit P3-kompatiblem Default) | ja: `name`, `key` | `name` asc | `AdminListPageDto<RoleDto>` | — |

**Gemeinsames Muster:** `/admin/master-data/departments` und `/departments` sind nicht zwei verschiedene Datenquellen, sondern dasselbe Set in zwei Sichten. Vertraglich teilen sie deshalb dieselbe Hull-Form (P1) und dieselbe `search`-Semantik. Workflow-Erfassungsdialoge konsumieren das gleiche Endpunktformat, schicken aber typisch nur `?search=…&limit=20` — das ist P3 als Spezialfall von P1, ohne dass das Backend zwei Pfade pflegen muss.

**Praktisch:** Der Erfassungs-Dialog tippt eine Abteilung/Rolle, das Backend antwortet mit Treffern statt mit allem. Die Pflege-Liste blaettert stabil. Beide Sichten sehen aus, als ob sie zur selben Datenquelle gehoeren.
**Lohnenswert:** Hotspot #6 aus Z8-1.2 ist genau hier; Departments/Rollen sind Pflicht-Lookup auf jedem Workflow-Erfassungseinstieg. Ein gemeinsamer Vertrag spart die Versuchung, einen zweiten „leichten" Endpunkt zu bauen.
**Nutzen:** schnelle Initialantwort beim Erfassen, vollstaendige Suche bei der Pflege, ein einziges FE-Muster fuer beide.
**FE-Folge:** `lookupApi.getDepartments/getRoles` werden zu `(query?: string, limit?: number) => Promise<AdminListPageDto<…>>`; bestehende Aufrufstellen entpacken `items`. AdminConfig-Pflegeseiten verlieren ihren clientseitigen Filter und uebergeben `?search` an die API.

#### C) Directory-Sync

| Endpunkt | Muster | Server-`search` | Stabiler `sort` | Antwort-Hull | Filter |
|---|---|---|---|---|---|
| `GET /admin/directory/groups` | P1 | ja: `displayName`, `objectId` | `displayName` (Default asc) | `AdminListPageDto<AdminDirectoryGroupDto>` | `?syncStatus` |
| `GET /admin/directory/identities` | P1 (Upgrade) | ja: `displayName`, `email`, `entraObjectId` | `displayName` (Default asc), `email`, `lastSeenAt` | `AdminListPageDto<AdminDirectoryIdentityDto>` | `?syncStatus`, `?hasAppUserLink` |
| `GET /admin/directory/responsibility-gaps` | **Split** in zwei P1-Listen + ein Summary-Endpunkt | innen ja | innen `responsibilityKey`, `departmentName` | je `AdminListPageDto<…>` | siehe unten |
| `GET /admin/directory/pending-imports` | **Split** in zwei P1-Listen + ein Summary-Endpunkt | innen ja | innen `submittedAt desc`, `displayName` | je `AdminListPageDto<…>` | siehe unten |
| `GET /admin/directory/audit` | **P2** | ja: `actor`, `targetIdentityKey`, `eventKey` | implizit `(occurredAt desc, id desc)` | `CursorPageDto<AdminDirectoryMappingAuditEntryDto>` | `?eventKey`, `?since`, `?until` |

**Split-Begruendung Gaps/Pending:** Der heutige Composite-DTO mischt mehrere Listen mit eingebetteten Counts. Sobald eine der inneren Listen waechst, wird der Composite-Vertrag unbrauchbar (kein Pagination-Anker pro Liste). Der Schnitt: `…/responsibility-gaps/summary` bleibt das aggregat (Counts/Status), `…/responsibility-gaps/missing` und `…/responsibility-gaps/excess` werden eigene P1-Listen. Analog Pending: `…/pending-imports/summary` + zwei P1-Listen je nach Trennung (`new` vs. `conflicts`). Das ist exakt das Muster, das `/admin/system/logs` + `/admin/system/logs/summary` bereits sauber etabliert hat.

**Praktisch:** Nach einem Sync sieht man genau die Treffer, nach denen man sucht — nicht „die ersten 100". Das Audit blaettert beliebig zurueck, ohne dass aelter heimlich abgeschnitten wird.
**Lohnenswert:** Sync-Listen wachsen mit jedem Sync-Run, genau hier laufen Alltagsbeschwerden zuerst auf. Das Audit-`limit=50` von heute ist der naechste echte Hotspot, sobald Sync-Probleme aufgearbeitet werden muessen.
**Nutzen:** UI kann ehrlich „Seite X von Y" zeigen statt zu raten; gleicher Vertrag fuer Identitaeten und Audit reduziert Sonderfaelle in der FE-Sync-Sicht.
**FE-Folge:** `adminConfigApi.getDirectoryIdentities` upgrade auf P1-Hull (heute schon `limit`/`offset`, fehlt nur Hull/`total`/`search`/`sort`); Gaps/Pending muessen im FE in Summary-Aufruf + zwei Listen-Aufrufe geteilt werden — Konsequenz aus dem Split, kein neuer Featurewunsch.

#### D) Workflow-Konfiguration / Builder

Alle Builder-Read-Endpunkte teilen ein gemeinsames Verhalten: sie sind **scoped** auf eine `workflowDefinitionId` (oder eine Sub-Ressource davon) und liefern den vollstaendigen Konfig-Block. Der Vertrag muss diesen Scope erhalten und gleichzeitig pro Scope blaetterbar werden.

| Endpunkt | Muster | Server-`search` | Stabiler `sort` | Antwort-Hull | Filter |
|---|---|---|---|---|---|
| `GET /admin/config/workflow-definitions` | P1 | ja: `key`, `name` | `name` (Default asc), `updatedAt desc` | `AdminListPageDto<WorkflowDefinitionSummaryDto>` | `?status`, `?owner` |
| `GET /admin/config/action-definitions` | P1 | ja: `key`, `name` | `name` (Default asc), `category` | `AdminListPageDto<ActionDefinitionDto>` | `?category` |
| `GET /admin/config/task-templates?workflowDefinitionId` | P1 (scoped) | ja: `key`, `title` | `sortOrder` (Default asc), `title` | `AdminListPageDto<AdminTaskTemplateDto>` | `?phase`, `?responsibilityKey` |
| `GET /admin/config/task-templates/{id}/conditions` | P1 (scoped) | nein (kleines Set pro Template) | `sortOrder` asc | `AdminListPageDto<AdminTaskTemplateConditionDto>` | — |
| `GET /admin/config/task-templates/{id}/dependencies` | P1 (scoped) | nein | `sortOrder` asc | `AdminListPageDto<AdminTaskTemplateDependencyDto>` | — |
| `GET /admin/config/answer-definitions?workflowDefinitionId` | P1 (scoped) | ja: `answerKey`, `label` | `sortOrder` asc, `answerKey` | `AdminListPageDto<AdminAnswerDefinitionDto>` | `?sectionKey` |
| `GET /admin/config/role-answer-defaults?workflowDefinitionId` | P1 (scoped) | ja: `roleKey`, `answerKey` | `roleKey` asc, `answerKey` asc | `AdminListPageDto<AdminRoleAnswerDefaultDto>` | `?roleKey`, `?answerKey` |

**Gemeinsames Muster:** „Builder-Liste = P1 mit erzwungenem Scope-Filter". Scope (`workflowDefinitionId` oder `task-template-id`) ist Pflicht-Query, nicht optional — der Builder laedt nie ueber alle Definitionen hinweg. Conditions/Dependencies bekommen bewusst keine Server-Suche, weil sie pro Template typischerweise klein sind und der Inspector ohnehin alle anzeigt; Pagination genuegt fuer den Wachstumsschutz.

**Praktisch:** Der Builder bleibt schnell, auch wenn eine Definition Dutzende Vorlagen oder Defaults hat. Suche im Inspector findet wirklich alles, nicht nur den geladenen Block.
**Lohnenswert:** Versionierte Definitionen sind Plattformziel — die Listen wachsen pro Definition (mehr Bausteine) und ueber Definitionen hinweg (mehr Versionen). Builder-Antwortzeit ist Adminerlebnis Nummer eins.
**Nutzen:** ein einheitlicher Tab-Vertrag; Builder kann pro Tab paginiert nachladen, statt beim ersten Klick ein Komplett-Set durchzukauen.
**FE-Folge:** `adminConfigApi`-Wrapper fuer die sieben Builder-Lese-Endpunkte werden auf `Promise<AdminListPageDto<…>>` umgezogen; clientseitige Filter im Builder-Inspector entfallen zugunsten Server-`search` mit definierter Trefferquelle.

#### E) Notification Templates

| Endpunkt | Muster | Server-`search` | Stabiler `sort` | Antwort-Hull | Filter |
|---|---|---|---|---|---|
| `GET /admin/notification-templates` | P1 | ja: `key`, `subject`, `description` | `key` (Default asc), `category` | `AdminListPageDto<AdminNotificationTemplateDto>` | `?category`, `?language` |

**Praktisch:** Template-Tab bleibt fuer den heutigen kleinen Bestand unauffaellig; sobald Sprachvarianten und Kategorien zunehmen, faengt P1 das ohne UI-Bruch ab.
**Lohnenswert:** mit mehr Workflow-Definitionen waechst die Liste monoton. Heute billig zu setzen, spaeter teuer.
**Nutzen:** konsistenter Vertrag mit den Admin-Listen oben — der Template-Tab faellt nicht aus dem Plattformmuster heraus.
**FE-Folge:** `adminApi.getNotificationTemplates` von `Promise<T[]>` auf `Promise<AdminListPageDto<T>>`; clientseitiger Filter im Template-Tab entfaellt.

#### F) Rotation Action Templates

| Endpunkt | Muster | Server-`search` | Stabiler `sort` | Antwort-Hull | Filter |
|---|---|---|---|---|---|
| `GET /admin/rotation/action-templates` | P1 | ja: `key`, `title` | `sortOrder` (Default asc), `title`, `updatedAt desc` | `AdminListPageDto<DepartmentActionTemplateDto>` | `?departmentId`, `?isActive` (heute schon) |

**Praktisch:** Maßnahmenpflege bleibt sortier-stabil; Suche nach Vorlage funktioniert, statt durchscrollen zu muessen.
**Lohnenswert:** Rotation ist B+-aktiv weiter ausgebaut worden, Vorlagenbestand pro Abteilung wird absehbar groesser.
**Nutzen:** explizite Sort-Reihenfolge (`sortOrder, title`) macht das, was heute implizit aus dem Repo kommt, vertraglich.
**FE-Folge:** `rotationApi.getDepartmentActionTemplates` auf Hull umstellen; bestehende `departmentId`/`isActive`-Filter bleiben.

#### G) Runtime-Sub-Resources

| Endpunkt | Muster | Server-`search` | Stabiler `sort` | Antwort-Hull | Filter |
|---|---|---|---|---|---|
| `GET /admin/runtime/workflow-instances/{uid}/events` | **P2** | ja: `eventKey`, `nodeKey` | implizit `(occurredAt desc, id desc)` | `CursorPageDto<WorkflowRuntimeEventDto>` | `?severity`, `?eventKey`, `?since`, `?until` |
| `GET /admin/runtime/workflow-instances/{uid}/automation-jobs` | **P2** | ja: `actionKey`, `status` | implizit `(scheduledAt desc, id desc)` | `CursorPageDto<AutomationJobDetailDto>` | `?status`, `?actionKey` |

**Begruendung P2:** Beide Listen sind pro Instanz append-only und potenziell unbeschraenkt (Retry, lange Laufzeit). `total` waere hier irrefuehrend (sowohl teuer als auch wenig hilfreich); ein Cursor mit „neuestes zuerst" matcht die typische Diagnose-Sicht „was ist gerade passiert, dann zurueckblaettern".

**Praktisch:** Runtime-Tab laedt auch fuer lange Instanzen schnell; man kann gezielt „nur Fehler" oder „nur seit gestern" filtern, statt im ganzen Verlauf nach Strg+F zu suchen.
**Lohnenswert:** mit mehr Automation pro Instanz waechst die Eventzahl schneller als der Workflow-Bestand. Diagnose ist genau der Moment, in dem die Liste am laengsten ist.
**Nutzen:** der Admin kann eine Instanz auch nach Wochen Laufzeit ergonomisch nachvollziehen; gleicher Hull-Typ wie Audit, ein Adapter im FE.
**FE-Folge:** Runtime-Detail-Tab bekommt zwei „Mehr laden"-Knoepfe (Events, Jobs); Severity-/Status-Filter werden Server-Filter statt Client-Filter.

#### H) Workflow Catalog Lookup (`/workflow-definitions/startable`)

| Endpunkt | Muster | Server-`search` | Stabiler `sort` | Antwort-Hull | Filter |
|---|---|---|---|---|---|
| `GET /workflow-definitions/startable` | P1 | ja: `name`, `key` | `name` (Default asc); spaeter optional `lastUsedAt desc` als Whitelist-Erweiterung | `AdminListPageDto<WorkflowStartableDefinitionDto>` | — |

**Praktisch:** Erfassungs-Dialog fuehlt sich auch bei vielen Definitionen wie eine kurze Auswahl an, weil per `?search` getippt wird statt blaetternd zu suchen.
**Lohnenswert:** Plattformziel ist „mehrere Workflows auf einem Plattformkern" — Bestand waechst absehbar.
**Nutzen:** kein Sonderpfad fuer den Erfassungseinstieg; gleicher Adapter wie B (Departments/Rollen).
**FE-Folge:** `lookupApi.getStartableWorkflowDefinitions` auf P1-Hull — analog zu B.

#### Konsolidierte FE-Konsequenz (Notiz, kein TODO)

Aus Z10-1.2 folgt **eine** Familie zusaetzlicher FE-Adapter, nicht eine pro Endpunkt:

- typed `AdminListPageDto<T>`-Wrapper in `web/src/services/` (P1) — bedient A/B/C-Identitaeten/D/E/F/H.
- typed `CursorPageDto<T>`-Wrapper (P2) — bedient A-Audit/C-Audit/G.
- Reihen von clientseitigen Filtern in den Admin-Tabs werden zu Server-Filtern; Filterzustand wandert in die URL/Query, nicht mehr in Komponenten-State.

Das sind zwei neue Hull-Typen und eine Refactor-Achse, nicht 25 neue Calls. Keine FE-TODOs in `FRONTEND_TODO.md` — die folgen erst aus dem Slice-Plan in Z10-1.3 und nur fuer die zuerst umgesetzten Endpunkte.

#### Reihenfolgen-Hinweis fuer Z10-1.3 (informativ, ohne Vorgriff)

Aus den Spuerbarkeits-Zonen oben legen sich drei Schubs nahe — die endgueltige Auswahl trifft Z10-1.3:

1. **B `/departments`+`/roles`** (gemeinsamer Schnitt, Hotspot #6, FE-spuerbar im Erfassungseinstieg).
2. **A-Audit + C-Audit** (P2 ablesbar, eigenstaendiger Vertrag, kein Hull-Bruch in den anderen Tabs).
3. **D Builder-Listen** (gemeinsames scoped P1, groesster Wirkungsgrad fuer Adminerlebnis im Builder).

Identitaeten (C) sind ein billiger Mitnahmeschnitt nach 1, weil sie heute schon `limit`/`offset` haben und nur Hull/`search`/`sort` fehlt. G (Runtime) und E/F bleiben absehbar in den hinteren Schubs, weil ihre Spuerbarkeit erst mit weiterem Wachstum entsteht.

#### Naechster Schritt

Z10-1.3 — Slice-Plan fuer Folgezyklus: 2–3 sichere Umsetzungsslices priorisieren (API-Vertrag + minimale FE-Adaption), inkl. Begruendung der Reihenfolge und expliziter Liste der Endpunkte, die in Z10 noch nicht angefasst werden und warum.

### Z10-1.3 Slice-Plan fuer Folgezyklus (2026-05-05)

Reine Planungsausgabe auf Basis von Z10-1.1 (Inventur) und Z10-1.2 (Vertrags-Skizze). Kein Code-Change, kein API-Vertrag wird hier aktiviert — die folgenden Slices definieren, **was als naechstes umgesetzt werden soll**, in welcher Reihenfolge und warum gerade so. Umsetzung erst im in einem Folgezyklus (Z11), nicht in Z10.

**Schreibregel angewandt:** zu jedem Slice Praktisch/Lohnenswert/Nutzen aus Nutzersicht; bewusst nicht angefasste Endpunkte bekommen jeweils eine kurze, verstaendliche Begruendung statt nur „spaeter".

#### Auswahl-Logik

Die Reihenfolge folgt drei klaren Kriterien, in dieser Prioritaet:

1. **Hull-Foundation vor Breitenrolle.** Der erste Slice muss die jeweils noch nicht existente Antwort-Hull (P1 bzw. P2) zentral und einmalig einfuehren. Sonst bekommen spaetere Endpunkte abweichende Mini-Implementierungen.
2. **Spuerbarkeit fuer Nutzer vor reinem Wachstumsschutz.** Endpunkte, an denen ein Nutzer im Alltag heute schon sucht/blaettert (Erfassungseinstieg, Audit-Tabs), werden vor reinen Adminpflege-Listen mit kleinem Bestand gezogen.
3. **Gemeinsame FE-Adapter wiederverwenden, statt mehrere parallele Hull-Familien gleichzeitig oeffnen.** Ein Slice fuehrt **eine** Hull-Familie ein und bedient alle Endpunkte, die genau diese Hull brauchen — kein Mischen P1/P2 in einem Slice.

#### Vorgeschlagene Slices F1–F3

| ID | Titel | Hull-Familie | Endpunkte | Prio | Reasoning | Modell |
|----|-------|--------------|-----------|------|-----------|--------|
| **F1** | P1 einfuehren + B Master-Data/Lookups | P1 (`AdminListPageDto<T>`) | `GET /departments`, `GET /roles`, `GET /admin/master-data/departments`, `GET /admin/master-data/positions`, `GET /admin/master-data/responsibilities` | HIGH | high | opus |
| **F2** | P2 einfuehren + Audit-Streams | P2 (`CursorPageDto<T>`) | `GET /admin/auth/audit`, `GET /admin/directory/audit` | HIGH | medium..high | sonnet |
| **F3** | P1 ausrollen + D Builder-Tabs (scoped) | P1 (wiederverwendet aus F1) | `GET /admin/config/workflow-definitions`, `GET /admin/config/action-definitions`, `GET /admin/config/task-templates`, `GET /admin/config/task-templates/{id}/conditions`, `GET /admin/config/task-templates/{id}/dependencies`, `GET /admin/config/answer-definitions`, `GET /admin/config/role-answer-defaults` | HIGH | medium..high | opus |

##### F1 — P1 einfuehren + B Master-Data/Lookups

- **Was passiert:** zentrale Definition `AdminListPageDto<T> { items, total, limit, offset }` im Backend; gemeinsame Query-Bindung `?limit&offset&search&sort` mit Server-Clamp (Default 50, Max 200), Whitelist-Sort, case-insensitive Server-Suche ueber pro-Endpunkt deklarierte Felder. Erstanwendung an genau den Endpunkten aus Block B (Z10-1.1/1.2).
- **FE-Folge (Konsequenz, nicht zusaetzliches TODO ueber das hinaus, was der Slice selbst mitnimmt):** ein neuer typed `AdminListPageDto<T>`-Wrapper in `web/src/services/`, plus Anpassung der Aufrufer in `lookupApi.ts` (`getDepartments`/`getRoles`) und `adminApi.ts` (`getAdminDepartmentAssignments`/`getAdminDepartmentPositions`/`getAdminResponsibilityOwners`) auf `Promise<AdminListPageDto<T>>`. Clientseitige Filter in den drei Master-Data-Tabs verschwinden zugunsten Server-`search`. **Eintrag in `FRONTEND_TODO.md` mit Trigger-Kennzeichnung „F1"** — erst beim Start von F1, nicht praeventiv.
- **Praktisch:** Wer im Erfassungs-Dialog eine Abteilung oder Rolle eintippt, bekommt sofort die passende Trefferliste statt zu warten, bis der ganze Bestand geladen wurde. Die Pflege-Listen unter Master-Data fuehlen sich ueber alle drei Tabs gleich an.
- **Lohnenswert:** Hotspot #6 aus Z8-1.2 — `/departments` und `/roles` sind Pflicht-Lookups in fast allen Workflow-Erfassungsmasken und werden mit wachsender Org zuerst spuerbar. Gleichzeitig wird hier die Plattform-Hull P1 zum ersten Mal echt scharfgestellt; jeder spaetere P1-Slice kann die gleiche Definition wiederverwenden.
- **Nutzen:** stabile Antwortzeiten am Erfassungseinstieg, vollstaendige Server-Suche ueber alle Departments/Rollen/Responsibilities, ein einziges Hull-Schema, das ab F1 in der Plattform existiert und in F3 nur noch ausgerollt wird.
- **Risikozaun:** F1 fasst keine Schreibpfade an, keine Composite-DTOs, keinen Audit-Stream. Reines Read-/Listenvertragsthema mit klarem Tx-/Repo-Boundary.

##### F2 — P2 einfuehren + Audit-Streams

- **Was passiert:** zentrale Definition `CursorPageDto<T> { items, nextCursor, hasMore }` im Backend; opaque Base64-Cursor ueber `(occurredAt, id)`; Erstanwendung an `/admin/auth/audit` und `/admin/directory/audit`. Beide ersetzen den heutigen stillen `limit`-Cap (`100`/`50`) durch echtes Weiterblaettern. Filter (`?actor`/`?action`/`?eventKey`/`?since`/`?until`) werden mitgenommen, weil sie ohne den Cursor-Vertrag keinen Mehrwert haben.
- **FE-Folge (Konsequenz):** ein neuer typed `CursorPageDto<T>`-Wrapper in `web/src/services/`; Permission-Audit-Tab und Directory-Audit-Detail bekommen einen „Mehr laden"-Knopf statt eines fixen Caps; `adminApi.getAdminPermissionAudit` und `adminConfigApi.getDirectoryMappingAudit` werden auf das Hull umgestellt. **Eintrag in `FRONTEND_TODO.md` mit Trigger-Kennzeichnung „F2"** — erst beim Start von F2.
- **Praktisch:** „Ich finde meine Aenderung von letzter Woche nicht mehr im Audit" verschwindet, weil zurueckblaetterbar. Filter (z. B. „nur Aenderungen durch Person X") greifen ueber den ganzen Audit-Bestand statt nur ueber die geladenen 100/50.
- **Lohnenswert:** Audit-Listen wachsen pro Aenderung monoton; der `limit`-Cap ist genau der Punkt, an dem heute Historie still abgeschnitten wird. Frueh angegangen, weil P2 eine eigenstaendige Hull-Familie ist und in F1 noch nicht gebraucht wird — F1 und F2 koennen sich nicht gegenseitig brechen.
- **Nutzen:** vollstaendiger, ergonomisch durchsuchbarer Audit-Verlauf in beiden Audit-Tabs; ein Cursor-Adapter im FE, der spaeter direkt fuer Runtime-Events/Jobs (G in der Vertrags-Skizze) wiederverwendbar ist.
- **Risikozaun:** F2 beruehrt keine P1-Endpunkte und keine Schreibpfade. `total` wird bewusst weggelassen, kein Versuch, Counts ueber wachsende Audit-Tabellen zu rechnen.

##### F3 — P1 ausrollen + D Builder-Tabs (scoped)

- **Was passiert:** Anwendung der in F1 etablierten P1-Hull auf die sieben Builder-Lese-Endpunkte aus Block D, mit Pflicht-Scope (`workflowDefinitionId` bzw. `task-template-id`) als Whitelist-Bedingung im Server. `task-templates/{id}/conditions` und `…/dependencies` bewusst ohne Server-`search` (kleines Set pro Template), aber mit Pagination und stabilem `sortOrder asc`. Keine API-Erweiterung um neue Filter ueber Z10-1.2 hinaus.
- **FE-Folge (Konsequenz):** `adminConfigApi`-Wrapper fuer die sieben Builder-Lese-Endpunkte werden auf `Promise<AdminListPageDto<…>>` umgezogen; clientseitige Filter im Builder-Inspector werden durch Server-`search` ersetzt; Filterzustand wandert in URL-Query, nicht in Komponenten-State. **Eintrag in `FRONTEND_TODO.md` mit Trigger-Kennzeichnung „F3"** — erst beim Start von F3.
- **Praktisch:** Der Builder bleibt schnell, auch wenn eine Definition Dutzende Aufgabenvorlagen, Antwortfelder oder Rollen-Defaults hat. Suche im Inspector findet wirklich alles, nicht nur den geladenen Block.
- **Lohnenswert:** Versionierte Definitionen sind Plattformziel — Wachstum ueber Definitionen und pro Definition ist eingeplant. Builder-Antwortzeit ist Adminerlebnis Nummer eins.
- **Nutzen:** ein einheitliches Tab-Verhalten ueber den ganzen Builder; weil F1 die P1-Hull bereits stabilisiert hat, ist F3 ein reines Ausrollen, kein neuer Vertrag.
- **Risikozaun:** F3 schreibt nicht in den Definitionsbestand. Keine Aenderung an Versionierungs- oder Publish-Pfaden. Scope-Pflicht bleibt erhalten, das Risiko eines versehentlichen „global ueber alle Definitionen" Calls wird durch Server-Validierung des Scope-Filters explizit ausgeschlossen.

#### Reihenfolge und Begruendung

- **F1 vor F2:** F1 bringt die haeufigste Hull (P1) und den lautesten User-Hebel (Erfassungseinstieg). F2 baut die zweite Hull-Familie (P2) und kann F1 nicht stoeren, weil keine Endpunkte ueberlappen.
- **F2 vor F3:** F2 ist klein, eigenstaendig und ersetzt einen Cap, der heute still Historie abschneidet — klarer Sicherheitsgewinn vor jedem Builder-Refactor. Ausserdem entkoppelt F2 die P2-Adoption von F3, sodass F3 sich rein auf P1-Ausrollung konzentrieren kann.
- **F3 als Drittes:** F3 setzt P1 voraus (kommt aus F1) und ist im Umfang am breitesten (sieben Endpunkte). Ein eigener Slice am Ende vermeidet, dass die Builder-Refactor-Achse in einen vorherigen Slice einsickert.

#### Bewusst in Z11 (Folgezyklus) noch nicht angefasst — Begruendung

| Block / Endpunkt | Aus Z10-1.2 | Warum spaeter (oder nicht) |
|---|---|---|
| **A Identity-Listen** (`/admin/auth/users`, `/admin/auth/roles`, `/admin/auth/groups`, `/admin/auth/permissions`) | P1 | Wachstum vorhanden, aber heute keine Beschwerden zur Such-/Listen-Ergonomie. F2 deckt den akuten Hebel im Identity-Bereich (Audit-Cap) bereits ab. Diese vier Listen lassen sich nach F3 in einem schmalen Folgeslice mit dem etablierten P1-Adapter mitnehmen — Risiko sinkt durch Wiederverwendung. |
| **C `/admin/directory/identities`** | P1 (Upgrade, hat schon `limit`/`offset`) | „Billiger Mitnahmeschnitt", aber bewusst **nicht** in F1, weil der Identities-Pfad an Directory-Sync-DTOs haengt und F1 stoffrein nur Master-Data/Lookups oeffnen soll. Direkter Folgekandidat nach F3. |
| **C Gaps/Pending Split** (`/admin/directory/responsibility-gaps`, `/admin/directory/pending-imports`) | Composite → Summary + zwei P1-Listen | API-Vertragsumbau ueber **mehrere** neue Endpunkte plus Frontend-Sichtumbau (zwei Lade-Aufrufe statt einer). Das ist explizit kein „kleiner Hull-Anbau", sondern ein Schnitt im DTO-Modell — gehoert in einen eigenen, vorbereiteten Slice nach F1–F3. F2 macht den Audit-Pfad daneben unabhaengig nutzbar, sodass der Sync-Tab nicht warten muss. |
| **E Notification Templates** | P1 | Bestand heute klein und stabil; keine Last- oder Such-Beschwerde. Erst sinnvoll, wenn der P1-Adapter im FE breit etabliert ist (nach F3) — dann ein billiges Mitnehmen, vorher reines Polieren ohne Trigger. |
| **F Rotation Action Templates** | P1 | Bestand klein, Rotation-FE ist frisch (Karten/Tabelle, Personenakte 360°). Kein akuter Trigger; Wartezeit kostet nichts und verhindert paralleles Refactoring im selben FE-Bereich. |
| **G Runtime Sub-Resources** (`…/events`, `…/automation-jobs`) | P2 | Pro Instanz, nur in der Diagnose-Sicht relevant. Heute kein Spuerbarkeits-Trigger. F2 etabliert bereits den `CursorPageDto<T>`-Adapter, sodass G spaeter ohne neue Hull-Familie ausgerollt werden kann — sparsamer Schnitt. |
| **H `/workflow-definitions/startable`** | P1 (mit P3-kompatiblem Default) | Selber Lookup-Charakter wie B (Departments/Rollen). Sinnvoll **direkt nach F1** mitzunehmen, falls F1 sich am Ende kleiner zeigt als geplant; sonst eigenstaendiger schmaler Slice nach F3 mit dem etablierten Adapter. Bewusst nicht in F1 gebuendelt, um F1 thematisch auf Master-Data klar zu halten. |

**Gemeinsame Begruendung „warum nicht alles auf einmal":** jeder Slice fuehrt entweder eine neue Hull ein (F1, F2) oder rollt eine bestehende auf eine grosse Endpunkt-Gruppe aus (F3). Das gleichzeitige Oeffnen aller Vertraege wuerde paralleles FE-Refactoring an mehreren Stellen erzwingen und genau das Risiko erzeugen, das Z10 verhindern soll: halbgaarige Workarounds, weil ein Endpunkt schon umgestellt ist und der naechste noch nicht.

#### Was Z10 mit Z10-1.3 abschliesst

- Inventur (Z10-1.1) → was heute Vertragsachsen vermissen laesst.
- Vertrags-Skizze (Z10-1.2) → welche Hull-Familien ueberhaupt entstehen sollen.
- Slice-Plan (Z10-1.3, dieser Block) → in welcher Reihenfolge die ersten drei Umsetzungsslices in Z11 angegangen werden, plus explizite Liste der Endpunkte, die in Z11 noch nicht angefasst werden und warum.

Ob daraus ein eigenstaendiger Folgezyklus „Z11" wird oder Z10 mit dem Slice-Plan abgeschlossen und ein anderer Hebel als naechstes gezogen wird, entscheidet die naechste Eroeffnung — Z10-1.3 trifft diese Entscheidung **nicht**, sondern liefert nur den Plan, der einer Eroeffnung zugrunde liegen wuerde.

#### Naechster Schritt

Z10 inhaltlich abgeschlossen mit Z10-1.3. Folgender Schritt ist die Eroeffnung des Umsetzungszyklus (vorgeschlagen Z11) auf Basis dieses Slice-Plans, nicht in Z10. Bis dahin bleibt der Plan in `CODE_REVIEW.md` § Z10-1.3 die Quelle fuer die F1/F2/F3-Reihenfolge.

---

## Abgeschlossener Zyklus 9 — `EntraDirectorySyncService`-Split / Testbarkeit (2026-05-05)

**Status:** abgeschlossen 2026-05-05. Z9-3 Coverage gegen die neuen Interfaces ist gesetzt; Z9 damit geschlossen.

**Thema:** Z8 hat die Lastpfade gehaertet (Bulk-Lookups, Sweep-Batching, Group/Member-Bulk in `EntraDirectorySyncService.SyncAllAsync`). Die offene Grenze aus Z8 ist **nicht** ein neuer Last-Hotspot, sondern die **fehlende saubere Test-Isolation** der neuen Batch-Helfer (`UpsertDirectoryIdentitiesBatch`, `InsertGroupMembershipsBatch`): sie sind `private` hinter dem 2.4k-Zeilen-Service, der direkt gegen Live-Graph + DB laeuft. Reflection-Probing waere fragil, ein End-to-End-Test ueber `SyncAllAsync` braucht einen Graph-Stub und einen sauberen Schnitt zwischen Orchestrierung, Graph-Zugriff und DB-Batch-Operationen.

LQ2-Z3 (`EntraDirectorySyncService` File-Split, 2591 Zeilen) trifft genau diesen Hebel: er bringt Wartbarkeit **und** testbare Abgrenzung, beides mit konkretem, aus Z8 begruendetem Nutzen — nicht als blindes Aufraeumen.

**Begruendung gegen alternative Zyklen:**
- *#6 Departments/Rollen Pagination*: erzeugt API-Vertrags- und FE-Folgen ohne aktuellen Last-Trigger. Wartet auf Anlass.
- *#8 RotationTaskGeneration*: bewusst deferred (admin-getriggert, kein kleiner SQL-Hebel).
- *Neue Last-Hotspots erfinden*: Z8 hat die priorisierten Pfade gepushed; ein synthetischer Last-Zyklus waere Bauchgefuehl-Refactor.
- *Reine Hygiene-Splits anderer grosser Dateien*: ohne Z8-Kopplung waere das genau das untersagte „blinde Aufraeumen".

**Fokus:**
1. Boundary-Inventur: oeffentliche API, Aufrufer, interne Achsen (Graph-Zugriff, DB-Batch, Orchestrierung, DepartmentLead-Resolution, Import-Pfad).
2. Extract-Plan: konkreter File-/Klassen-Schnitt, der Graph- und DB-Seite hinter Interfaces stellt und `SyncAllAsync` zur duennen Orchestrierung schrumpft.
3. `SyncAllAsync`-Zuschnitt nach Plan; Graph-/Batch-Helfer hinter Interface bringen.
4. Coverage nachziehen: insbesondere die Z8-2.3-Batch-Helfer (`UpsertDirectoryIdentitiesBatch`, `InsertGroupMembershipsBatch`) plus DepartmentLead-Resolver.

**Leitplanken:**
- Kein paralleler zweiter Hebel. Nur LQ2-Z3.
- Keine semantischen Aenderungen an Sync-Verhalten oder DB-Spalten — reine Strukturarbeit mit Test-Nutzen.
- Keine FE-Folgen erwartet (Service ist Backend-/Timer-Pfad).
- API-Vertraege der Admin-Endpunkte bleiben unveraendert.

**Geplante Slices (Erst-Definition, nicht Umsetzung):**

| ID | Aufgabe | Prio | Reasoning | Modell | Status |
|----|---------|------|-----------|--------|--------|
| Z9-1.1 | Boundary-/Split-Inventur: oeffentliche API, Aufrufer (`DirectorySyncHostedService`, Admin-Endpunkte), interne Achsen (Graph-Zugriff, DB-Batch-Helfer, `SyncAllAsync`-Orchestrierung, `SyncDepartmentLeadAssignmentsFromDirectory`, `ImportDirectoryIdentitiesAsync`); pro Achse: Abhaengigkeiten, Test-Isolations-Hindernisse | HIGH | high | opus | offen |
| Z9-1.2 | Extract-Plan: konkreter File-/Klassen-Schnitt (Kandidaten z. B. `EntraGraphClient`/-Adapter, `EntraDirectoryBatchOperations`, `EntraDepartmentLeadResolver`, schlanker `EntraDirectorySyncOrchestrator`), Reihenfolge der Extraktionen, Test-Strategie (Graph-Stub vs. echtem Client), explizite Nicht-Ziele | HIGH | high | opus | offen |
| Z9-2.1 | Pre-Cleanup (Dead-Code raus) + `SyncAllAsync` in Phasen-Methoden zerlegen, ohne Verhaltensaenderung, noch in derselben Datei | HIGH | medium..high | sonnet | done 2026-05-05 — Detail im Sync-Log und in `CODEX_SYNC.md` |
| Z9-2.2 | Graph-Zugriff hinter Adapter-Interface; Adapter testbar (Stub) machen | HIGH | medium..high | sonnet | done 2026-05-05 — `IEntraGraphClient`/`EntraGraphClient` unter `api/API/Services/Directory/`; `Microsoft.Graph` aus Hauptdatei raus |
| Z9-2.3 | DB-Batch-Helfer (`UpsertDirectoryIdentitiesBatch`, `InsertGroupMembershipsBatch`) hinter dediziertes, testbares Operations-Modul ziehen | HIGH | medium | sonnet | done 2026-05-05 — `IEntraDirectorySyncOperations` + `EntraDirectorySyncOperations` unter `api/API/Services/Directory/`; Service konsumiert per Konstruktor; Reflection-Test auf direkten Aufruf umgestellt; Detail im Sync-Log |
| Z9-3 | Coverage nachziehen: Integration-Tests fuer Batch-Helfer (aus Z8-4 verschoben) + Unit-Tests fuer Orchestrator gegen Graph-Stub | MEDIUM | medium | sonnet | done 2026-05-05 — Unit-Tests `EntraDirectorySyncServiceTests` (3/3 gruen) gegen `IEntraGraphClient`-/`IEntraDirectorySyncOperations`-Stubs; Integration-Tests `EntraDirectorySyncOperationsIntegrationTests` fuer `UpsertDirectoryIdentitiesBatchAsync` und `InsertGroupMembershipsBatchAsync` ueber bestehende Postgres-Fixture (lokal ohne Docker/Postgres dieselbe Fixture-Gating-Grenze wie Z8-4.1). Detail unten § Z9-3. |

### Z9-1.1 Boundary-/Split-Inventur (2026-05-05)

Reine Inventur, kein Code-Change. Datei: `api/API/Services/EntraDirectorySyncService.cs` (2591 Z., `internal sealed class EntraDirectorySyncService : IDirectorySyncService`).

**Konstruktor-Abhaengigkeiten** (4): `IGraphApplicationConfigurationService` (Graph-Credentials), `LifecycleRuntimeSettings` (ConnectionString, DirectoryGroupPrefix, DirectoryExplicitGroupIds, DevSimulationEnabled), `ISystemEventLogService` (Audit/Eventlog), `ILogger<EntraDirectorySyncService>`. Kein Repository-Interface — der Service oeffnet `NpgsqlConnection` direkt aus dem ConnectionString.

#### Oeffentliche API (`IDirectorySyncService` → `EntraDirectorySyncService`)

| Methode | LOC | Aufruf-Pfad | Kohaesionsbereich |
|---------|-----|-------------|-------------------|
| `SyncAllAsync(groupPrefixOverride?, ct)` | 34-393 | HostedService + Admin POST `/admin/directory/sync` | Sync-Orchestrierung (Graph + DB + Eventlog) |
| `GetSyncStatusAsync(ct)` | 467 | Admin GET `/admin/directory/status` | DB-Read |
| `GetGroupsAsync(ct)` | 537 | Admin GET `/admin/directory/groups` | DB-Read |
| `GetIdentitiesAsync(limit, offset, ct)` | 647 | Admin GET `/admin/directory/identities` | DB-Read |
| `GetMappingAuditAsync(limit, ct)` | 726 | Admin GET `/admin/directory/audit` | DB-Read |
| `UpsertGroupRoleMappingAsync(req, actor?, ct)` | 788 | Admin POST `/admin/directory/group-mappings` | DB-Write + Mapping-Audit |
| `DeleteGroupRoleMappingAsync(id, actor?, ct)` | 882 | Admin DELETE `/admin/directory/group-mappings/{id}` | DB-Write + Mapping-Audit |
| `GetResponsibilityGapsAsync(ct)` | 928 | Admin GET `/admin/directory/responsibility-gaps` | DB-Read (komplexe Aggregation) |
| `GetPendingImportsAsync(ct)` | 1022 | Admin GET `/admin/directory/pending-imports` | DB-Read |
| `ImportIdentitiesAsync(req, actor?, ct)` | 1104 | Admin POST `/admin/directory/import` | DB-Write (Import-Pfad, ruft `ImportSingleIdentityAsync`) |

#### Echte externe Aufrufer

- `DirectorySyncHostedService` (`api/API/Services/DirectorySyncHostedService.cs`): nur `SyncAllAsync(ct)` — periodisch (DIRECTORY_SYNC_INTERVAL_MINUTES), gated durch `DirectorySyncEnabled` + `DirectorySyncScheduled`.
- `AdminDirectorySyncEndpoints` (`api/API/Endpoints/AdminDirectorySyncEndpoints.cs`): saemtliche `IDirectorySyncService`-Methoden ueber das Admin-API.
- DI-Registrierung: `LifecycleServiceCollectionExtensions.cs` (Z. 165 `IGraphApplicationConfigurationService`; `IDirectorySyncService → EntraDirectorySyncService` ist scoped registriert in derselben Datei).
- Tests: ausschliesslich `api/API.Tests/PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs` — **Reflection-Zugriff** auf zwei `private`-Methoden (siehe Test-Isolations-Hindernisse).

Keine weiteren Aufrufer (verifiziert via `Grep` ueber Repo).

#### Interne Achsen / Kohaesionsbereiche innerhalb der Datei

1. **Sync-Orchestrierung** (`SyncAllAsync`, 34-393, ~360 Z.) — Connection-Open, Graph-Client-Bau, Group-Loop, Cross-Cutting-Eventlog, Status-Aggregation, Final-`LogSyncRun`. Direkt an Graph **und** DB **und** SystemEventLog gekoppelt.
2. **Graph-Zugriff** (Microsoft.Graph) — `LoadSecurityGroupsAsync` (395), `LoadGroupMembersAsync` (424), `ResolveGraphCredentialsAsync` (453), `GraphServiceClient`-Bau in `SyncAllAsync` (61-89). Einzige Stellen mit `Microsoft.Graph.*`-Abhaengigkeit.
3. **DB-Batch-Helfer (Sync-Pfad)** — `UpsertDirectoryIdentitiesBatch` (1528), `InsertGroupMembershipsBatch` (1596) — die in Z8-2.3 eingefuehrten `unnest`-Bulk-Statements; aktuell `private static`. Daneben Single-Row-Helfer `UpsertDirectoryGroup` (1446), `UpsertDirectoryIdentity` (1469, dead-code-Kandidat — nach Z8-2.3 nicht mehr aufgerufen, pruefen), `ClearGroupMemberships` (1500), `InsertGroupMembership` (1511), `AutoLinkIdentitiesToAppUsers` (1619).
4. **Projection / Activation** (DB-Batch im Sync-Pfad) — `EnsureDirectoryProjectionUserColumnsAsync` (1996), `EnsureDirectoryDepartmentsExist` (1732), `UpdateExistingAppUsersFromDirectory` (1770), `UpdateDirectoryUserActivationStates` (2018), `EnsureDevelopmentDefaultGroupMappings` (2484). Reine SQL-Pfade, keine Graph-Kopplung.
5. **Sync-Logging / Audit** — `LogSyncRun` (1633), `LogMappingAuditAsync` (1358), `LogDirectoryAuditEventAsync` (1390), `CreateDepartmentLeadAuditSnapshot` (1422). Schreiben in `directory_sync_log` / `directory_mapping_audit_log`. Nicht zu verwechseln mit `_systemEventLogService` (zweiter Audit-Kanal).
6. **Group-/Filter-Helfer (CPU)** — `ShouldSyncGroup` (1711), `MatchesGroupPrefix` (1665), `ResolveEffectiveGroupPrefix` (1680), `ParseExplicitGroupIds` (1696), `Normalize` (1691), `NormalizeScope` (1659), `ParseDirectoryEmployeeNumber` (2524). Reine statische CPU-Helfer ohne IO.
7. **Admin-Read/Write-Methoden** (Status, Groups, Identities, MappingAudit, GroupRoleMapping CRUD, ResponsibilityGaps, PendingImports) — DB-Zugriffe + Mapping-Audit. Gehoeren fachlich zu „Admin-Konfigurations-Sicht", nicht zur Sync-Orchestrierung.
8. **Import-Pfad** — `ImportIdentitiesAsync` (1104) + `ImportSingleIdentityAsync` (1179). Eigene Achse: erzeugt `app_users`-Datensaetze aus bekannten `directory_identities`. Kein Graph-Zugriff. Findet `FindExistingMappingIdAsync` (1263) und `GetGroupRoleMappingByIdAsync` / `…OrNullAsync` (1295/1343) als Mapping-Lookup-Helfer.
9. **DepartmentLead-Resolver (toter Pfad in Prod)** — `SyncDepartmentLeadAssignmentsFromDirectory` (2099), `LoadDepartmentLeadSyncStatesAsync` (2242), `EnsureDirectoryManagedPersonRecordAsync` (2307), `UpsertDepartmentLeadAssignmentAsync` (2439), `ClearDepartmentLeadAssignmentAsync` (2470). **Wichtig:** `SyncAllAsync` ruft diese Methoden **nicht** mehr auf (Z. 306-317 schreibt explizit `directory_department_assignments_skipped`); einziger lebender Aufruf ist die **Reflection-Invocation aus dem Test** (siehe unten). `KauthWorkflow/Architektur/Entscheidungen.md` dokumentiert den bewussten Stop. Status: **dead code im Prod-Pfad**, im Test als Black-Box-Subroutine genutzt.

#### Was haengt direkt an Microsoft Graph

- `using Azure.Identity;`, `using Microsoft.Graph;`, `using Microsoft.Graph.Models;` (Z. 1-4).
- `GraphServiceClient`-Konstruktion (`SyncAllAsync` 61-89).
- `LoadSecurityGroupsAsync` (395), `LoadGroupMembersAsync` (424).
- `ResolveGraphCredentialsAsync` (453) — bezieht Credentials aus `IGraphApplicationConfigurationService`; ist nicht selbst Graph-call, gehoert aber in den Adapter, weil sie pure Graph-Bootstrap-Logik ist.
- Alles andere ist Graph-frei.

#### Was ist DB-Batch / Projection / Audit

- **Batch-Operations (Sync):** `UpsertDirectoryIdentitiesBatch`, `InsertGroupMembershipsBatch`, `ClearGroupMemberships`, `AutoLinkIdentitiesToAppUsers` (Z8-2.3 Hebel). Plus die Single-Row-Pendants `UpsertDirectoryGroup`, `UpsertDirectoryIdentity` (Legacy/dead?), `InsertGroupMembership`.
- **Projection:** `EnsureDirectoryProjectionUserColumnsAsync`, `EnsureDirectoryDepartmentsExist`, `UpdateExistingAppUsersFromDirectory`, `UpdateDirectoryUserActivationStates`, `EnsureDevelopmentDefaultGroupMappings`.
- **Audit:** `LogSyncRun`, `LogMappingAuditAsync`, `LogDirectoryAuditEventAsync`, `CreateDepartmentLeadAuditSnapshot` (Audit ueber `directory_*`-Tabellen, nicht `_systemEventLogService`).

#### Was ist `SyncAllAsync`-Orchestrierung (im engeren Sinn)

Genau Z. 34-393. Schritte in dieser Reihenfolge:
1. Settings/Prefix-Resolution + Connection-Validierung.
2. Graph-Credential-Aufloesung + Graph-Client-Bau (Fehlerpfade `failed`).
3. `EnsureDirectoryProjectionUserColumnsAsync`.
4. `LoadSecurityGroupsAsync` + `ShouldSyncGroup`-Filter + Selection-Eventlog.
5. Group-Loop: `UpsertDirectoryGroup` → `LoadGroupMembersAsync` → `ClearGroupMemberships` → `UpsertDirectoryIdentitiesBatch` → `InsertGroupMembershipsBatch`. Per-Group Try/Catch → Status `partial`.
6. `AutoLinkIdentitiesToAppUsers`.
7. `EnsureDirectoryDepartmentsExist` + Eventlog.
8. `UpdateExistingAppUsersFromDirectory` + Eventlog.
9. `EnsureDevelopmentDefaultGroupMappings` (nur DevSim).
10. `UpdateDirectoryUserActivationStates` + per-Change-Eventlog.
11. „skipped"-Eventlog fuer DepartmentLead.
12. Final `LogSyncRun` + Sammel-Eventlog. Rueckgabe `DirectorySyncResult`.

#### Test-Isolations-Hindernisse (aktuell)

- **Reflection-Zugriffe** auf `private`-Methoden in `api/API.Tests/PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs`:
  - Z. 103-105: `EntraDirectorySyncService.SyncDepartmentLeadAssignmentsFromDirectory` (Instance, NonPublic) — fragil und testet einen Pfad, den `SyncAllAsync` nicht mehr aufruft. Bricht beim Rename/Move.
  - Z. 177-179: `EntraDirectorySyncService.UpdateExistingAppUsersFromDirectory` (Static, NonPublic) — analog fragil.
- **Live-Graph-Kopplung in `SyncAllAsync`**: `GraphServiceClient` wird inline gebaut (Z. 61-89). Es gibt **kein** `IGraphClient`/Adapter-Interface, das im Test gestubbt werden koennte. End-to-End-Test gegen `SyncAllAsync` braucht entweder echtes Tenant oder einen Adapter-Schnitt.
- **DB-Batch-Helfer hinter `SyncAllAsync`-2.4k-Z**: `UpsertDirectoryIdentitiesBatch` / `InsertGroupMembershipsBatch` sind `private static`. Direktes Coverage erfordert Reflection (genau das fragile Pattern oben) oder Sichtbarkeits-Aenderung. In Z8-4.1 wurden sie deshalb explizit nicht abgedeckt.
- **Verstreute `_systemEventLogService.WriteAsync`-Calls**: `SyncAllAsync` schreibt mehrfach Eventlog-Eintraege im Sync-Pfad. Trennung Orchestrierung ↔ Eventing ist nur durch das DI-Interface (`ISystemEventLogService`) gegeben; Stub `StubSystemEventLogService` existiert bereits in Tests.
- **Direkte `NpgsqlConnection`-Konstruktion** im Service statt Repository-Interface — End-to-End-Tests brauchen weiterhin Postgres-Fixture; saubere Unit-Tests gegen einen reinen Orchestrator brauchen ein DB-Operations-Interface oder einen Connection-Factory-Stub.
- **`UpsertDirectoryIdentity`-Single-Row-Helfer (Z. 1469)**: nach Z8-2.3 evtl. ungenutzt (im Hot-Pfad ersetzt durch Batch). Vor dem Split pruefen, ob er noch lebt — wenn nicht, gehoert er nicht in den Split.

#### Versteckte Kopplungen

- `LifecycleRuntimeSettings.DevSimulationEnabled` schaltet `EnsureDevelopmentDefaultGroupMappings` an (Z. 274). Diese Verzweigung ist kein Graph-/DB-Schnitt, sondern eine Dev-Sim-Sonderlogik.
- `DirectoryExplicitGroupIds`-Setting wird **nur** in `SyncAllAsync` (via `ParseExplicitGroupIds`) genutzt — Filter-Logik gehoert in den CPU-Helfer-Block.
- Audit-Logs schreiben in **zwei** Kanaele (`directory_*`-Tabellen via `LogDirectoryAuditEventAsync`, plus `system_event_log` via `_systemEventLogService`) — der Split muss klar machen, welcher Kanal beim Orchestrator bleibt und welcher beim DB-Operations-Modul.

#### Was explizit **nicht** in den ersten Split gehoert

- **DepartmentLead-Resolver** (Achse 9): toter Pfad in Prod. Vor dem Split entscheidet Z9-1.2, ob loeschen, in eigene Datei isolieren oder unangetastet lassen — **nicht** in den Sync-Adapter ziehen, sonst zementiert der Split einen ungenutzten Pfad.
- **Admin-Read/Write-Methoden** (Achse 7) und **Import-Pfad** (Achse 8): logisch eigene Verantwortung („Admin-Sicht / Import"), aber **nicht** Treiber von Z9. Der Trigger ist Test-Isolation der **Sync-Lastpfade** aus Z8-2.3. Admin-Methoden in dieselbe Iteration zu ziehen vergroessert den Slice ohne Z8-Kopplung. → in Folge-Slices oder spaeter, nicht in Z9-2.x.
- **Single-Row-Helfer** `UpsertDirectoryIdentity` / `InsertGroupMembership` (falls nach Z8-2.3 dead): vor dem Split Lebendigkeit pruefen; wenn dead, in Z9-1.2 als Loesch-Kandidat markieren statt im Split „mitnehmen".
- **`LifecycleRuntimeSettings`-Auswertung**: bleibt im Service-Konstruktor / Orchestrator. Kein eigenes Modul.
- **Eventlog-Integration (`ISystemEventLogService`)**: nicht hinter neuen Wrapper. Der bestehende DI-Stub (`StubSystemEventLogService`) reicht fuer Tests.
- **CPU-Helfer** (Achse 6): koennen in eine `internal static`-Helper-Klasse, **muessen** aber nicht — keine Test-Isolations-Wirkung.

#### Schnittland-Empfehlung (informativ, nicht Plan)

Aus Z8-Kopplung getrieben, mit klarem Test-Nutzen:
- **Graph-Adapter** (Achse 2) hinter `IEntraGraphClient`-aehnlichem Interface. Bringt `SyncAllAsync` einen Stub-Punkt.
- **DB-Sync-Operations-Modul** (Achsen 3+4 nur fuer den Sync-Pfad: Batch-Helfer, Projection, Activation). Bringt `UpsertDirectoryIdentitiesBatch` / `InsertGroupMembershipsBatch` aus dem `private`-Schatten und macht sie integration-testbar.
- **`EntraDirectorySyncOrchestrator`** als duenner `SyncAllAsync`-Treiber gegen Graph- + DB-Interface + `ISystemEventLogService`.
- Achsen 5 (Sync-Logging) und 6 (CPU-Helfer) folgen passiv im jeweils naechstgelegenen Modul.
- Achsen 7+8+9 bleiben in dieser Iteration **ausserhalb** des Splits.

Z9-1.2 entscheidet die konkrete Reihenfolge und die Test-Strategie (Graph-Stub vs. echter Client, Coverage-Reihenfolge).

**Reihenfolge / Abhaengigkeiten:**
- Z9-1.1 → Z9-1.2 sequenziell (Inventur vor Plan).
- Z9-2.x: 2.1 zuerst (Orchestrierungs-Schnitt), danach 2.2 und 2.3 unabhaengig moeglich, aber sequenziell halten, damit der Tree pro Slice klar bleibt.
- Z9-3 erst, wenn die Batch-/Graph-Schnitte stehen. Coverage darf den Refactor nicht treiben.

### Z9-2.2 Graph-Adapter extrahiert (2026-05-05)

Slice gemaess Z9-1.2 § Z9-2.2 abgeschlossen.

- Neue Files: `api/API/Services/Directory/IEntraGraphClient.cs`, `api/API/Services/Directory/EntraGraphClient.cs` (Namespace `API.Services.Directory`).
- `IEntraGraphClient` exponiert `InitializeAsync` (Status: `Ready` / `MissingCredentials` / `Failed` plus `ErrorMessage`/`ExceptionType`), `LoadSecurityGroupsAsync`, `LoadGroupMembersAsync`. Rueckgabewerte sind plain DTOs (`EntraSecurityGroup`, `EntraDirectoryUser`), damit `Microsoft.Graph.*` aus der Hauptdatei verschwindet.
- `EntraGraphClient` kapselt `IGraphApplicationConfigurationService`-Lookup, `ClientSecretCredential`- und `GraphServiceClient`-Bau, Pagination beim Group-/Member-Load. Nicht-User-Members werden bereits hier herausgefiltert (vorher im Orchestrator via `is not Microsoft.Graph.Models.User`); Verhalten unveraendert, da der Orchestrator diese Members ohnehin uebersprang.
- `EntraDirectorySyncService.cs`:
  - Konsumiert `IEntraGraphClient` per Konstruktor; `IGraphApplicationConfigurationService` und `_logger` als Graph-Bootstrap-Empfaenger entfallen (Field + Argument geloescht).
  - `SyncAllAsync` ersetzt den Inline-Credential/Client-Aufbau (vorher Z. 61-89) durch `await _graphClient.InitializeAsync(ct)` mit identischen Eventlog-Eintraegen `directory_sync_missing_graph_credentials` und `directory_sync_graph_client_failed` (inkl. `error`/`exceptionType` aus `EntraGraphInitResult.ExceptionType`).
  - `RunGroupSyncAsync` verliert den `GraphServiceClient`-Parameter und ruft `_graphClient.LoadSecurityGroupsAsync` / `LoadGroupMembersAsync`.
  - `validUsers`-Tupel und `UpsertDirectoryIdentitiesBatch`-Signatur tragen jetzt `EntraDirectoryUser` statt `Microsoft.Graph.Models.User` (Property-Mapping unveraendert: `UserPrincipalName`/`Mail`/`DisplayName`/`AccountEnabled`/`Department`/`EmployeeId`).
  - `LoadSecurityGroupsAsync`/`LoadGroupMembersAsync`/`ResolveGraphCredentialsAsync` aus der Hauptdatei entfernt.
  - `using Azure.Identity;` / `using Microsoft.Graph;` / `using Microsoft.Graph.Models;` aus der Hauptdatei entfernt; `using API.Services.Directory;` ergaenzt.
- DI: `services.AddScoped<API.Services.Directory.IEntraGraphClient, API.Services.Directory.EntraGraphClient>()` direkt vor der `IDirectorySyncService`-Registrierung in `LifecycleServiceCollectionExtensions`. Scoped passt zum Per-Run-Scope von `DirectorySyncHostedService`.
- `PROJECT_STRUCTURE.md` mitgezogen: `api/API/Services/Directory/` als neuer Sub-Namespace eingetragen.
- Verhaltensgleichheit: gleiche Fehlerpfade fuer fehlende Credentials und Client-Bau, gleiche Eventlog-Eintraege, gleiche per-Group-Try/Catch-Logik mit Status `partial`.
- Verifikation: `dotnet build API/API.csproj` (0 Warn / 0 Err), `dotnet build API.Tests/API.Tests.csproj` (3 vorhandene CS8602-Warnungen, 0 Err).

### Z9-1.2 Extract-Plan (2026-05-05)

Reine Planung, kein Code-Change. Verbindlicher Schnitt fuer Z9-2.x, basierend auf Z9-1.1.

#### Lebendigkeitspruefung (vorab verifiziert)

Repo-weite `Grep`-Pruefung der drei Z9-1.1-Verdachtsfaelle:
- `EntraDirectorySyncService.SyncDepartmentLeadAssignmentsFromDirectory` (Z. 2099): einziger lebender Aufrufer ist die Reflection-Invocation in `api/API.Tests/PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs:104`. **Dead code im Prod-Pfad** bestaetigt. Sub-Helfer `LoadDepartmentLeadSyncStatesAsync`, `EnsureDirectoryManagedPersonRecordAsync`, `UpsertDepartmentLeadAssignmentAsync`, `ClearDepartmentLeadAssignmentAsync`, `CreateDepartmentLeadAuditSnapshot` sind ausschliesslich Sub-Aufrufer dieser toten Methode → ebenfalls dead.
- `UpsertDirectoryIdentity` (Single-Row, Z. 1469): keine Aufrufer im Repo. **Dead**.
- `InsertGroupMembership` (Single-Row, Z. 1511): keine Aufrufer im Repo. **Dead**.

Damit ist der Pre-Cleanup eindeutig: alles drei wird in Z9-2.1 entfernt, **bevor** der File-Split startet. Begruendung: ein File-Split, der toten Code mitnimmt, zementiert ihn in einem neuen Modul und vergroessert die Splittsflaeche unnoetig.

#### Ziel-Endzustand nach Z9-2.x

Drei Files unter neuem Namespace `api/API/Services/Directory/`:

1. **`EntraDirectorySyncService.cs`** (alte Datei, deutlich verschlankt; bleibt am bestehenden Pfad fuer DI-Stabilitaet):
   - `IDirectorySyncService`-Implementation aller 11 oeffentlichen Methoden.
   - `SyncAllAsync` als duenner Treiber: orchestriert `_graphClient` (Z9-2.2) + `_syncOps` (Z9-2.3) + `_systemEventLogService`.
   - Admin-Read/Write (Achse 7): `GetSyncStatusAsync`, `GetGroupsAsync`, `GetIdentitiesAsync`, `GetMappingAuditAsync`, `UpsertGroupRoleMappingAsync`, `DeleteGroupRoleMappingAsync`, `GetResponsibilityGapsAsync`, `GetPendingImportsAsync`. Bleiben hier.
   - Import-Pfad (Achse 8): `ImportIdentitiesAsync` + `ImportSingleIdentityAsync` + `FindExistingMappingIdAsync` + `GetGroupRoleMappingByIdAsync`/`…OrNullAsync`. Bleiben hier.
   - Mapping-Audit-Helfer (`LogMappingAuditAsync`) bleibt hier (Admin-Pfad-Bedarf).
   - CPU-Helfer (Achse 6) bleiben als `private static` hier — keine Test-Isolations-Wirkung.
   - Erwartete LOC: ~1200 (von 2591).

2. **`api/API/Services/Directory/IEntraGraphClient.cs` + `EntraGraphClient.cs`** (Z9-2.2):
   - Interface kapselt: `Task<IReadOnlyList<Group>> LoadSecurityGroupsAsync(ct)`, `Task<IReadOnlyList<DirectoryObject>> LoadGroupMembersAsync(string groupId, ct)`.
   - `EntraGraphClient` impl: `ResolveGraphCredentialsAsync` + `GraphServiceClient`-Bau intern (Konstruktor-Inject `IGraphApplicationConfigurationService`). Einzige Stelle mit `Microsoft.Graph.*`-Abhaengigkeit nach dem Schnitt.
   - DI: `services.AddScoped<IEntraGraphClient, EntraGraphClient>()` in `LifecycleServiceCollectionExtensions`.

3. **`api/API/Services/Directory/IEntraDirectorySyncOperations.cs` + `EntraDirectorySyncOperations.cs`** (Z9-2.3):
   - Interface enthaelt nur die **Sync-Pfad**-DB-Operationen aus Achsen 3+4+5 (Sync-Logging):
     - `EnsureDirectoryProjectionUserColumnsAsync`
     - `UpsertDirectoryGroup`
     - `ClearGroupMemberships`
     - `UpsertDirectoryIdentitiesBatch` (Z8-2.3-Hebel)
     - `InsertGroupMembershipsBatch` (Z8-2.3-Hebel)
     - `AutoLinkIdentitiesToAppUsers`
     - `EnsureDirectoryDepartmentsExist`
     - `UpdateExistingAppUsersFromDirectory`
     - `EnsureDevelopmentDefaultGroupMappings` (DevSim-Pfad bleibt; Verzweigung weiterhin im Orchestrator entschieden)
     - `UpdateDirectoryUserActivationStates`
     - `LogSyncRun`
     - `LogDirectoryAuditEventAsync` fuer Sync-Pfad-Eintraege
   - Methoden werden `public` auf der Klasse → integration-testbar ohne Reflection.
   - Konstruktor: `LifecycleRuntimeSettings` (ConnectionString) + `ILogger<EntraDirectorySyncOperations>`. **Kein** `ISystemEventLogService`-Cross-Cutting hier; Eventlog-Schreiben bleibt im Orchestrator.
   - DI: `services.AddScoped<IEntraDirectorySyncOperations, EntraDirectorySyncOperations>()`.

#### Was explizit **draussen** bleibt

- **Admin-Read/Write-Methoden** (Achse 7) und **Import-Pfad** (Achse 8): kein Z8-Trigger, kein Test-Isolations-Druck. Bleiben in `EntraDirectorySyncService.cs`. In Folgezyklen ggf. eigener Schnitt.
- **Mapping-Audit** (`LogMappingAuditAsync`): wird auch von Admin-Pfaden (UpsertGroupRoleMapping/Delete) verwendet. Bleibt in der Hauptklasse, nicht ins Operations-Modul ziehen.
- **CPU-Helfer** (Achse 6): keine Test-Isolations-Wirkung. Bleiben `private static` in der Hauptklasse.
- **`ISystemEventLogService`-Wrapper**: nicht einfuehren. Bestehender DI-Stub `StubSystemEventLogService` reicht.
- **Repository-/Connection-Factory-Abstraktion**: nicht in Z9. `EntraDirectorySyncOperations` oeffnet weiterhin `NpgsqlConnection` aus `LifecycleRuntimeSettings.ConnectionString`. Integration-Tests laufen ueber bestehende Postgres-Fixture.
- **Namespace-Massenmove** anderer Services: nur die drei neuen Files unter `Services/Directory/`. Bestehende Datei behaelt ihren Pfad.

#### Umgang mit Dead Code (verbindlich)

- **DepartmentLead-Resolver** (`SyncDepartmentLeadAssignmentsFromDirectory` + 5 Sub-Helfer): in **Z9-2.1 loeschen**. Begruendung gegen "in eigene Datei isolieren": der Pfad ist seit der bewussten Stop-Entscheidung (`KauthWorkflow/Architektur/Entscheidungen.md`) aus dem Prod-Pfad raus; eine isolierte Datei dafuer waere Konservierung von totem Code in einem neuen Modul. Reflection-Test in `PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs:103-105` wird im selben Slice mit entfernt. Test fuer `UpdateExistingAppUsersFromDirectory` (Z. 177-179) bleibt — Methode wird in Z9-2.3 nach `EntraDirectorySyncOperations` verschoben und dort `public`, der Test kann auf direktem Aufruf umgestellt oder einstweilen entfernt werden (siehe Z9-2.3-Test-Strategie).
- **Single-Row-Helfer** `UpsertDirectoryIdentity` (1469) und `InsertGroupMembership` (1511): in **Z9-2.1 loeschen**. Verifiziert ohne Aufrufer im Repo. Begruendung gegen "im Operations-Modul behalten als Convenience": kein einziger Aufrufer existiert, Behalten bedeutet ungetestete tote API im neuen Modul.

#### Slice-Schnitt Z9-2.1 / 2.2 / 2.3 (verbindlich)

**Z9-2.1 — Pre-Cleanup + SyncAllAsync-Strukturierung**
- Loeschen: `SyncDepartmentLeadAssignmentsFromDirectory` und Sub-Helfer (`LoadDepartmentLeadSyncStatesAsync`, `EnsureDirectoryManagedPersonRecordAsync`, `UpsertDepartmentLeadAssignmentAsync`, `ClearDepartmentLeadAssignmentAsync`, `CreateDepartmentLeadAuditSnapshot`); zugehoerige `DepartmentLeadSyncSummary`/`DepartmentLeadSyncState`/`DepartmentLeadSyncOutcome`-Records, falls nicht anderweitig genutzt; Reflection-Test in `PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs:103-105`.
- Loeschen: `UpsertDirectoryIdentity` (1469), `InsertGroupMembership` (1511).
- `SyncAllAsync` intern in drei klar benannte Phasen-private-Methoden zerlegen (`RunGroupSyncAsync`, `RunDirectoryProjectionAsync`, `RunActivationAsync`) — **noch in derselben Datei**, kein neuer Namespace. Bereitet 2.2/2.3 vor.
- LOC-Erwartung: ~600 Zeilen weniger in der Hauptdatei.
- **Test-Strategie**: keine neuen Tests. Bestehende API.Tests-Suite bleibt gruen (minus den geloeschten Reflection-Test). Verifikation: `dotnet build` + `dotnet test --filter Category!=Integration`.

**Z9-2.2 — Graph-Adapter extrahieren**
- Neue Files unter `api/API/Services/Directory/`: `IEntraGraphClient.cs`, `EntraGraphClient.cs`.
- Verschieben: `LoadSecurityGroupsAsync`, `LoadGroupMembersAsync`, `ResolveGraphCredentialsAsync`, GraphServiceClient-Bau aus `SyncAllAsync` Z. 61-89.
- `EntraDirectorySyncService` bekommt `IEntraGraphClient` per Konstruktor.
- Microsoft.Graph-`using` aus der Hauptdatei entfernen.
- DI-Registrierung in `LifecycleServiceCollectionExtensions`.
- **Test-Strategie**: Smoke-Test, dass Build gruen ist und bestehende Suite gruen bleibt. Echte Stub-getriebene Orchestrator-Tests kommen erst in Z9-3, sobald `IEntraDirectorySyncOperations` (2.3) ebenfalls steht — sonst muesste man halb-fertige Test-Doubles bauen.

**Z9-2.3 — DB-Sync-Operations-Modul extrahieren**
- Neue Files unter `api/API/Services/Directory/`: `IEntraDirectorySyncOperations.cs`, `EntraDirectorySyncOperations.cs`.
- Verschieben: die unter "Ziel-Endzustand Punkt 3" gelisteten Methoden inkl. `UpsertDirectoryIdentitiesBatch` und `InsertGroupMembershipsBatch`. Methoden werden `public` auf der Operations-Klasse.
- `EntraDirectorySyncService` bekommt `IEntraDirectorySyncOperations` per Konstruktor; `SyncAllAsync` ruft `_syncOps.UpsertDirectoryIdentitiesBatch(...)` etc.
- Reflection-Test fuer `UpdateExistingAppUsersFromDirectory` in `PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs:177-179`: auf direkten `EntraDirectorySyncOperations`-Aufruf umstellen (Methode ist jetzt public). Falls der Aufwand das Slice unnoetig aufblaeht, alternativ entfernen — die echte Coverage kommt in Z9-3.
- DI-Registrierung in `LifecycleServiceCollectionExtensions`.
- **Test-Strategie**: bestehende Suite gruen halten. Direkte Integration-Tests gegen die neuen `public`-Methoden kommen in Z9-3, nicht hier. Begruendung: Coverage darf den Refactor nicht treiben (Leitplanke Z9).

#### Test-Strategie zusammengefasst (pro Slice)

| Slice | Neue Tests | Bestehende Tests | Verifikation |
|-------|-----------|------------------|--------------|
| Z9-2.1 | keine | Reflection-Test fuer `SyncDepartmentLeadAssignmentsFromDirectory` entfernen | `dotnet build`; `dotnet test --filter Category!=Integration` gruen |
| Z9-2.2 | keine | Build muss gruen bleiben; keine Verhaltensaenderung | `dotnet build`; Suite gruen |
| Z9-2.3 | keine (Coverage in Z9-3) | Reflection-Test fuer `UpdateExistingAppUsersFromDirectory` umstellen oder entfernen | `dotnet build`; Suite gruen |
| Z9-3 | Integration-Tests fuer `UpsertDirectoryIdentitiesBatch`, `InsertGroupMembershipsBatch` + Unit-Tests fuer Orchestrator gegen `IEntraGraphClient`-Stub und `IEntraDirectorySyncOperations`-Stub | bestehende gruen halten | `dotnet test` inkl. Integration |

Begruendung gegen "Tests pro Slice mitziehen": die Z9-2.x-Slices sind reine Strukturarbeit ohne Verhaltensaenderung; sinnvolle Coverage haengt erst nach 2.3 an stabilen Interfaces. Tests in 2.2 zu schreiben, die in 2.3 erneut umgebaut werden muessen, ist verschwendet.

#### Risiken / Watchouts

- **DI-Reihenfolge**: `EntraDirectorySyncService`, `EntraGraphClient`, `EntraDirectorySyncOperations` muessen alle `Scoped` registriert werden, damit der Sweep-Lebenszyklus konsistent bleibt (`DirectorySyncHostedService` erstellt pro Run einen Scope).
- **`LifecycleRuntimeSettings`-Sharing**: das Settings-Objekt wird jetzt in Service + Operations gleichzeitig konsumiert — kein Problem (Singleton/Scoped Snapshot), aber bei Aenderungen von `ConnectionString`/`DirectoryGroupPrefix` einheitlich halten.
- **DevSim-Verzweigung**: `EnsureDevelopmentDefaultGroupMappings` ruft die Operations, aber die Entscheidung "DevSim aktiv?" bleibt im Orchestrator — Operations-Modul kennt die DevSim-Flag nicht.
- **Reflection-Test-Umstellung in 2.3**: wenn die Test-Anpassung die Fixture-Initialisierung beruehrt, kann das den Slice unerwartet vergroessern. Fallback: Test entfernen, Coverage in Z9-3 neu schreiben.
- **Naming**: `Services/Directory/` als Sub-Namespace ist neu. `PROJECT_STRUCTURE.md` muss in Z9-2.2 mitgezogen werden, sobald die ersten Files dort liegen.

**Frontend-Folgen:** **keine**. Reiner Backend-Refactor. `FRONTEND_TODO.md` wird nicht angefasst.

**Abgrenzung zu Z8:**
- Z8 hat `SyncAllAsync` an der Last-Front gehaertet (Group/Member-Bulk). Z9 fasst die so eingefuehrten Helfer **nicht inhaltlich** an, sondern nur strukturell, damit sie isoliert testbar werden.
- LQ2-Z3 wird als aktiver Zyklus 9 aus dem zyklusuebergreifend-offenen Block herausgehoben; die Coverage fuer die Z8-2.3-Batch-Helfer wandert formal nach Z9-3 (vorher: aus Z8-4 nach LQ2-Z3 verschoben).

### Z9-3 Coverage gegen die neuen Interfaces (2026-05-05)

Nach Abschluss der Strukturarbeit (Z9-2.1..2.3) deckt Z9-3 die in Z8-2.3 eingefuehrten und in Z9-2.3 unter `IEntraDirectorySyncOperations` verschobenen Batch-Helfer plus den nun duennen Orchestrator ab.

**Unit-Tests Orchestrator** — `api/API.Tests/EntraDirectorySyncServiceTests.cs` (neu, 3 Tests):
- `SyncAllAsync_MissingConnectionString_FailsBeforeGraphInit` — `LifecycleRuntimeSettings.ConnectionString = null`; verifiziert: Status `failed`, `directory_sync_missing_connection_string`-Eventlog, **kein** Graph-`InitializeAsync`, **kein** `UpsertDirectoryIdentitiesBatchAsync`.
- `SyncAllAsync_GraphMissingCredentials_FailsAndLogsEvent` — Graph-Stub liefert `EntraGraphInitStatus.MissingCredentials`; verifiziert: Status `failed`, `directory_sync_missing_graph_credentials`-Event, kein `LoadSecurityGroupsAsync`, `AppliedGroupPrefix` aus `groupPrefixOverride` durchgereicht.
- `SyncAllAsync_GraphFailed_FailsAndLogsErrorWithDetails` — Graph-Stub liefert `Failed` mit `ErrorMessage`/`ExceptionType`; verifiziert: Status `failed`, `ErrorMessage` durchgereicht, `directory_sync_graph_client_failed`-Event geschrieben.

Diese drei Pfade sind die im aktuellen Service ohne reale Postgres-Verbindung sauber stub-fahigen Orchestrierungs-Pfade. Alles jenseits des `MissingConnectionString`/Graph-Init-Gates oeffnet eine echte `NpgsqlConnection` und gehoert in Integration-Tests; ein zusaetzlicher Connection-Factory-Schnitt waere ein neuer Refactor und damit gegen die Leitplanke "kein weiterer Produktiv-Refactor in Z9-3". Offen benannt.

**Integration-Tests Operations** — `api/API.Tests/EntraDirectorySyncOperationsIntegrationTests.cs` (neu, 3 Tests, `[Trait("Category", "Integration")]`, gleiche `PostgresWorkflowRepositoryIntegrationCollection`-Fixture wie Z8-4.1):
- `UpsertDirectoryIdentitiesBatchAsync_InsertsNewIdentitiesAndReturnsMapping` — zwei frische `entra_object_id`s; verifiziert RETURNING-Mapping (beide Schluessel vorhanden) sowie persistierte Spalten (`display_name`, `mail`, `account_enabled`, `department_name`, `employee_number` inkl. `Normalize`/`ParseDirectoryEmployeeNumber`).
- `UpsertDirectoryIdentitiesBatchAsync_UpdatesExistingAndDedupsLastWins` — zweite Batch trifft denselben `entra_object_id` zweimal; verifiziert `ON CONFLICT … DO UPDATE` (selbe `id` zurueck) und das in der Implementation dokumentierte „last-wins"-Verhalten der internen Dedup-Map.
- `InsertGroupMembershipsBatchAsync_InsertsAndIsIdempotent` — zwei Memberships, zweimaliger Aufruf; verifiziert exakt zwei Eintraege (ON CONFLICT DO NOTHING) plus den Empty-Array-No-Op-Pfad.

**Verifikation:**
- `dotnet test --filter "FullyQualifiedName~EntraDirectorySyncServiceTests"` (im Test-Projekt): 3/3 gruen.
- `dotnet test --filter "FullyQualifiedName~EntraDirectorySyncOperationsIntegrationTests"`: lokal **nicht** ausfuehrbar — der `PostgresWorkflowRepositoryDatabaseFixture` faellt mit `ArgumentException : Docker is either not running or misconfigured` zurueck (kein Docker / kein Postgres unter `127.0.0.1:26432`). Identisches Fixture-Gating wie alle anderen `[Category=Integration]`-Tests im Projekt (vgl. Z8-4.1). Tests sind im etablierten Fixture-Muster angelegt; Verifikation auf einem CI-/Lokal-Setup mit Docker oder live-DB.
- `dotnet build api/API.Tests/API.Tests.csproj`: erfolgreich (3 vorbestehende CS8602-Warnungen in `PostgresWorkflowRepositoryConcurrencyTests.cs`, 0 Fehler).

**Bewusst draussen:**
- Tests fuer `RunGroupSyncAsync`/`RunDirectoryProjectionAsync`/`RunActivationAsync`-Pfade als reine Unit-Tests — verlangen Connection-Factory-Schnitt, der explizit nicht in Z9 angefasst wird.
- Coverage fuer `EnsureDirectoryProjectionUserColumnsAsync`, `AutoLinkIdentitiesToAppUsersAsync`, `EnsureDirectoryDepartmentsExistAsync`, `EnsureDevelopmentDefaultGroupMappingsAsync` — nicht in der Z9-Auftragsliste; wuerden den Slice ohne neuen Hebel verbreitern.

**Z9-Abschluss:** Mit Z9-3 ist Zyklus 9 abgeschlossen. Naechster Schritt liegt zyklusuebergreifend (`#6` Pagination, `#8` Rotation-Regeneration) oder bei einem neuen, durch Anlass getriggerten Zyklus.

---

## Abgeschlossener Zyklus 8 — Skalierbarkeits- & Last-Haertung (2026-05-05)

**Status:** geschlossen 2026-05-05. Alle priorisierten Hotspots abgearbeitet (#1, #2/#3, #4, #5 verifiziert, #7 Bulk-Lookup) oder mit Begruendung deferred (#8). Coverage-Pflicht-Slices erledigt (Z8-2.1 FactsTest, Z8-2.2 Sweep-Batching-Test, Z8-4.1 Integration-Tests fuer den Bulk-Recipient-Helper). Bewusst deferred bleiben:
- **#8 / Z8-3.2** — admin-getriggert, kein kleiner SQL-/Batch-Hebel ohne breiten Umbau.
- **EntraDirectorySync-Batch-Helfer-Coverage** — `private` hinter `SyncAllAsync` im 2.4k-Zeilen-Service; saubere Test-Isolation verlangt LQ2-Z3-File-Split + Graph-Stub. Wandert nach LQ2-Z3.
- **#6 Departments/Rollen Pagination** — bewusst nicht in Z8 angefasst (FE-Folgen ohne Last-Trigger). Wartet auf konkreten Anlass.

Naechster Zyklus offen — siehe Zyklus-Historie.

### Zyklus-8-Detail (Historie)

**Thema:** Nach Abschluss der Lifecycle- und Validation-Hygiene aus Zyklus 7 ist Skalierbarkeit (Note **B-**) die niedrigste Gesamtbewertung und damit der naechste sinnvolle Hebel. Z2 hat den Workflow-Task-Filter SQL-pre-narrowed, aber an mehreren Stellen laufen Listen, Filter und Sweeps weiter ungebremst durch In-Memory-Pfade. Das ist keine Theorie-Schwaeche, sondern wird bei realer Last sichtbar (Workflow-Liste, MyTasks, RotationOperations, Notification-Dispatch, RotationTask-Sweep).

**Begruendung gegen alternative Zyklen:**
- *EntraDirectorySyncService Split (LQ2-Z3, 2485 Z.)* bleibt deferred ohne Trigger — Timer-Pfad, kein User-Pfad, keine offene Beschwerde. Reine Bewegung.
- *Auth-Haertung* — Note B+ stabil, keine konkrete neue Luecke seit Zyklus 4.
- *Frontend-Polish* — nach FE-25..FE-31 stabil; B+ ohne offenen Schmerzpunkt.
- *Repository-Splits* ohne Anlass — reine Hygiene ohne klaren Last- oder Produkthebel.

**Fokus:**
1. Inventur saemtlicher Pfade mit unbeschraenktem Laden, In-Memory-Filter/-Sortierung und N+1-Risiko.
2. Top-Hotspots als SQL-Pushdown / Pagination loesen.
3. Background-Sweeps (RotationTask-Sweep, Notification-Dispatch) auf Last gegenpruefen.
4. Test-Coverage fuer die neu gepushten Pfade nachziehen.

**Priorisierung:**

| ID | Befund | Prio |
|----|--------|------|
| Z8-1.1 | Inventur: Endpunkte + Repos mit unbeschraenktem Laden, In-Memory-Filter/-Sort, N+1 | **done** (2026-05-05) — siehe § Z8-1.1 Hotspot-Inventur |
| Z8-1.2 | Top-3-Hotspot-Auswahl + Slice-Plan auf Basis der Inventur | **done** (2026-05-05) — siehe § Z8-1.2 Slice-Plan |
| Z8-2.1 | Hotspot #1 — `WorkflowCatalogService.GetStartableWorkflowDefinitionsAsync`: N+1 fuer `IsManagerCreatableDefinition` aufloesen (Bulk-/SQL-Pushdown) | **done** (2026-05-05) — Bulk-Lookup `GetManagerCreatableDefinitionKeys()` |
| Z8-2.2 | Hotspot #2+#3 (gemeinsamer Slice) — `RotationNotificationService` Daily-Sweep: `LIMIT`/Batch-Fetch + Batch-Update der Dispatch-Results | **done** (2026-05-05) — Service-Loop mit `DispatchBatchSize=200`; Apply mit Bulk-Metadata + Bulk-UPDATE via `unnest` |
| Z8-2.3 | Hotspot #4 — `EntraDirectorySyncService.SyncAllAsync`: Group-Member-Schleifen auf Batch-Upsert/-Insert umstellen | **done** (2026-05-05) — `UpsertDirectoryIdentitiesBatch` (Bulk-Upsert via `unnest` + RETURNING) und `InsertGroupMembershipsBatch` ersetzen pro-Member Round-Trips |
| Z8-3.1 | Hotspot #5 verifizieren + Hotspot #7 Recipient-Bulk-Lookup | HIGH | **done** (2026-05-05) — #5 false positive (CPU/Policy-Pfad ohne Repo-Hits); #7 nutzt jetzt `LoadActiveUserNotificationRecipientsBulk` einmalig pro Preview/Create statt pro Recipient |
| Z8-3.2 | Hotspot #8 `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` | **deferred** (2026-05-05) — siehe § Z8-3.2 Defer-Begruendung. Z8-3 damit geschlossen. |
| Z8-4 | Test-Coverage fuer die neu gepushten Pfade (Integration + Unit) | **done** (2026-05-05) — Z8-4.1 Bulk-Recipient-Helper; Z8-2.1/Z8-2.2-Coverage bereits in den Umsetzungs-Slices enthalten; EntraDirectorySync-Batch-Helfer offen benannt (LQ2-Z3) |

### Z8-4.1 Coverage `LoadActiveUserNotificationRecipientsBulk` (2026-05-05)

Direkte Integration-Coverage fuer den Bulk-Recipient-Helper aus Z8-3.1 ergaenzt. Neuer Test-File `api/API.Tests/PostgresRepositorySharedHelpersIntegrationTests.cs` mit 3 Faellen ueber die bestehende `PostgresWorkflowRepositoryIntegrationCollection`-Fixture:

- `LoadActiveUserNotificationRecipientsBulk_MapsRolesAndSkipsInactive` — drei aktive User (`auth_admin`, `auth_manager`, ohne Rollen) + ein inaktiver + eine unbekannte Id; verifiziert PreferredPath-Mapping (`/workflows`, `/supervisor`, `/tasks/my`), DisplayName/Email/IdentityKey-Treue und das `is_active = TRUE`-Filter.
- `LoadActiveUserNotificationRecipientsBulk_EmptyInput_ReturnsEmpty` — leere Eingabe → leere Map ohne SQL-Roundtrip-Fehler.
- `LoadActiveUserNotificationRecipientsBulk_PrefersNotificationEmailAndExternalKey` — `notification_email`/`external_key` mit umgebenden Whitespace werden via `BTRIM`/`COALESCE` korrekt ueberschrieben.

`EntraDirectorySyncService.UpsertDirectoryIdentitiesBatch` / `InsertGroupMembershipsBatch`: bewusst nicht zusaetzlich abgedeckt. Die Helfer sind `private` innerhalb des 2.4k-Zeilen-Service und liegen hinter `SyncAllAsync` (Live-Graph + DB). Reflection-Probing waere fragil und ohne Mehrwert; ein End-to-End-Test ueber `SyncAllAsync` braucht einen Graph-Stub und gehoert zur LQ2-Z3-File-Split-Arbeit. Grenze offen benannt.

**Verifikation:** `dotnet build api/API.Tests` (0 Fehler), `dotnet test --no-build --filter "Category!=Integration&FullyQualifiedName!~Integration&FullyQualifiedName!~Concurrency"` → 364/364 gruen. Die drei neuen Faelle laufen ueber die testcontainers-/Postgres-Fixture und konnten lokal nicht ausgefuehrt werden (kein Docker-Daemon, kein Postgres auf 127.0.0.1:26432); sie folgen exakt dem Pattern der bestehenden `PostgresWorkflowRepository...IntegrationTests` und greifen dieselbe Fixture, also dieselbe CI-Lauf-Erwartung.

**Empfohlener Einstieg:** Z8-1.1 als reine Inventur — opus/high. Output: konkret nummerierte Hotspot-Liste mit Aufrufer-Pfad und Datenkardinalitaet, kein Code-Change. Erst auf dieser Basis entscheidet Z8-1.2, ob Pagination, Sortier-Pushdown oder N+1-Aufloesung den groessten Hebel hat.

### Z8-1.1 Hotspot-Inventur (2026-05-05)

Reine Inventur, kein Code-Change. Pro Hotspot: Datei/Symbol, Art `(a)` unbegrenztes Laden / `(b)` In-Memory-Filter-Sort / `(c)` N+1 / `(d)` Sweep-/Dispatch-Last, Kardinalitaetsgrund, Prio fuer Z8-1.2.

1. **`WorkflowCatalogService.GetStartableWorkflowDefinitionsAsync`** — `api/API/Services/WorkflowCatalogService.cs:8-42`. Art **(b)+(c)**. Laedt **alle** publizierten Definitionen ohne Limit und ruft danach pro Definition `repository.IsManagerCreatableDefinition(definitionKey)` in einer foreach-Schleife auf (Z. 34). Aufrufer: Workflow-Start (Master-Data-Endpoint). Kardinalitaet: pro Manager-User bei jedem Workflow-Anlegen 1 + N DB-Calls; bei wachsendem Definitionsbestand linear teurer. **Prio HIGH**.
2. **`RotationNotificationService.ExecuteDailySweepAsync` + `IRotationRepository.GetDispatchableRotationNotifications`** — `api/API/Services/RotationNotificationService.cs:11-62`, SQL in `api/API/Repositories/PostgresRotationRepository.NotificationOperations.cs`. Art **(a)+(d)**. Laedt **alle** dispatchable Notifications ohne `LIMIT` und uebergibt sie en bloc an den Mail-Sender. Kein Batching, kein Pagings. Kardinalitaet: bei Backlog (z. B. nach Mail-Ausfall) zehntausende Saetze in einem Aufruf — Memory- und Mail-Pfad. **Prio HIGH**.
3. **`RotationNotificationOperations.ApplyRotationNotificationDispatchResults`** — `api/API/Repositories/PostgresRotationRepository.NotificationOperations.cs:192-299`. Art **(c)+(d)**. Pro Result: Select + Update sequenziell statt Batch-Update. Kardinalitaet: skaliert 1:1 mit #2; verdoppelt die DB-Last des Sweeps. **Prio HIGH** (haengt logisch an #2 — bietet sich als gemeinsamer Slice an).
4. **`EntraDirectorySyncService.SyncAllAsync` (Group-Member-Schleifen)** — `api/API/Services/EntraDirectorySyncService.cs` ~Z. 153-218. Art **(c)+(d)**. Geschachtelte Schleifen `groups` × `members` mit `UpsertDirectoryIdentity` und `InsertGroupMembership` einzeln pro Member. Kein Batch-Insert. Aufrufer: `DirectorySyncHostedService` (24h-Sweep, 2h-Timeout aus Z6). Kardinalitaet: bei breiterer Org (>100 Gruppen × Dutzende Mitglieder) tausende Round-Trips pro Sweep. **Prio HIGH**, aber Timer-Pfad — Last zeigt sich erst beim Timeout. (Hinweis: vollstaendiger File-Split bleibt LQ2-Z3 deferred; hier geht es nur um die Sync-Schleifen, nicht um Strukturhygiene.)
5. **`WorkflowVisibilityService.ApplyWorkflowTaskPermissions`** — `api/API/Services/WorkflowVisibilityService.cs:59-110`. Art **(b)** rein CPU. Pro Task drei Policy-Aufrufe (`CanUpdateTaskStatus`/`CanDecideTaskApproval`/`CanAddTaskComment`). **Z8-3.1 Verifikation (2026-05-05): false positive als DB-Hotspot.** Alle drei Methoden in `AuthorizationPolicyService` arbeiten ausschliesslich auf `CurrentUser` (Rollen/Permissions/Responsibilities) und dem `TaskWithWorkflowDto` (Status, Assignments) — keine Repo-Hits, kein N+1. Bleibt als reiner CPU-Pfad ohne aktuellen Hebel; aus Z8 herausgenommen.
6. **`WorkflowCatalogService.GetDepartmentsAsync` / `GetRolesAsync`** — `api/API/Services/WorkflowCatalogService.cs:44-52`, SQL in `api/API/Repositories/PostgresWorkflowRepository.MasterDataOperations.cs`. Art **(a)**. `ORDER BY` ohne `LIMIT`/Pagination. Kardinalitaet: in einer Filiale unkritisch, in groesseren Org-Strukturen wird die Master-Data-Liste ohne Pagination zum Hot-Path. **Prio MEDIUM** — Pagination/Suche im Frontend bedeutet auch FE-Folgen, deshalb fuer Z8-1.2 separat bewerten.
7. **`PostgresWorkflowNotificationDispatchOperations.BuildReadyTaskNotificationPreviewTargetsAsync`** — `api/API/Repositories/PostgresWorkflowNotificationDispatchOperations.cs` ~Z. 280-313. Art **(c)**. `foreach` ueber Recipient-Ids mit `LoadActiveUserNotificationRecipient` pro Empfaenger statt einem JOIN/IN-Set. Aufrufer: Notification-Preview. **Z8-3.1 done (2026-05-05):** ersetzt durch einmaligen `LoadActiveUserNotificationRecipientsBulk(@userIds = ANY)`. Selber Bulk-Helper auch in `CreateReadyTaskNotificationsAsync` genutzt (identisches Recipient-Pattern, minimaler Mitnahme-Refactor).
8. **`RotationTaskGenerationService.RegenerateDepartmentPlansAsync`** — `api/API/Services/RotationTaskGenerationService.cs:53-64`. Art **(c)**. Liest Plan-Ids einer Abteilung und ruft `SynchronizeRotationGeneratedTasks(planId)` pro Plan in einer foreach-Schleife. Aufrufer: Admin-/Regeneration-Pfad. Kardinalitaet: pro Abteilung × Plaene; admin-getriggert, kein Hot-Path. **Prio LOW-MEDIUM** — eher Z8-3-Material (Sweep/Regeneration-Performance) als Top-3-Kandidat.
9. **`EntraDirectorySyncService.ImportDirectoryIdentitiesAsync`** — `api/API/Services/EntraDirectorySyncService.cs` ~Z. 1114-1153. Art **(c)**. Sequenzieller Import pro Identity inkl. SystemEventLog-Schreiben pro Item. Aufrufer: Admin-Import-Endpoint. Kardinalitaet: blockiert UI bei Massen-Import; nicht permanent unter Last. **Prio LOW-MEDIUM**.

**False Positives / bewusst ausgenommen:**
- `WorkflowLifecycleService` — Commit-Grenze, kein Listenpfad.
- Workflow-/Task-Listen-Filter (`PostgresWorkflowRepository.WorkflowQueryOperations` + `TaskOperations` Filter): bereits durch Z2 SQL-pre-narrowed, keine doppelte Listung.
- `WorkflowDefinitionDraftValidator` / `SnapshotValidator` (Z7) — reiner CPU-Pfad pro Validierung, keine Listen-Last.
- Suche-Endpunkte `SearchWorkflowTargetPersonSources/People` — bereits mit `limit`-Parameter; nicht hier.

**Empfehlung fuer Z8-1.2:** Top-Kandidaten **#1, #2 (+#3 als gemeinsamer Slice), #4**. Begruendung:
- #1 ist nutzersichtbarer Hot-Path bei jedem Workflow-Anlegen → Pagination greift hier nicht, sondern SQL-seitige `IsManagerCreatable`-Auswertung pro Definition oder Bulk-Lookup.
- #2/#3 ist der naechste Skalierungs-Cliff im Background-Sweep mit klarem Hebel (LIMIT + Batch-Update).
- #4 ist der teuerste Sweep ohne Batching; alternativ kann #4 nach Z8-3 verschoben werden, wenn #1/#2 zuerst gehaertet werden.

**Frontend-Folgen:** aus #6 entstehen ggf. FE-Items (Pagination/Suche fuer Departments/Rollen). Erst bei Z8-1.2 entscheiden — bis dahin **kein** FE-Eintrag.

**Frontend-Folgen Status Z8 gesamt:** aktuell **keine**. Z8 ist backend-fokussiert. Wenn Z8-2 API-Vertraege aendert (z. B. Pagination-Tokens, Sortier-Parameter), entstehen erst dann FE-Items in `FRONTEND_TODO.md`. Bis dahin wird keine FE-Arbeit kuenstlich erzeugt.

### Z8-3.2 Hotspot #8 — Defer-Begruendung (2026-05-05)

`RotationTaskGenerationService.RegenerateDepartmentPlansAsync` (`api/API/Services/RotationTaskGenerationService.cs:53-64`) wurde als verbliebener Z8-3-Resthebel geprueft. Ergebnis: **deferred ohne Code-Change**. Begruendung:

- Pfad ist admin-getriggert (Department-Regeneration), kein Hot-/Sweep-Pfad und kein Background-Timer. Keine offene Last-Beschwerde.
- Die foreach-Schleife ruft pro Plan `SynchronizeRotationGeneratedTasks(planId)` auf — eine transaktionale Multi-Step-Synchronisation pro Plan (Diff vs. Bestand, Insert/Update/Delete generierter Tasks, Audit). Es gibt **keinen** kleinen SQL-/Batch-Hebel analog zu Z8-2.x: Bundling mehrerer Plaene in eine Statement-Schicht waere genau der untersagte breite Umbau an `SynchronizeRotationGeneratedTasks` und den zugehoerigen Repository-Pfaden.
- Parallelisierung der Schleife (`Task.WhenAll`) wuerde mehrere transaktionale Sync-Pfade auf dieselbe Connection/Tx-Grenze setzen und ist im aktuellen Pool-/Tx-Modell riskant — kein klarer kleiner Hebel.
- Keine Frontend-Folgen, keine API-Vertragsaenderung.

Damit ist Z8-3 abgeschlossen (Z8-3.1 done, Z8-3.2 deferred). #8 wandert in die zyklusuebergreifenden offenen Befunde mit Defer-Status; Re-Bewertung nur bei konkretem Last-Trigger oder wenn `SynchronizeRotationGeneratedTasks` ohnehin angefasst wird.

### Z8-1.2 Top-3-Auswahl + Slice-Plan (2026-05-05)

Reine Planung, kein Code-Change. Bestaetigt die Empfehlung aus Z8-1.1 und schneidet Z8-2.x.

**Bestaetigte Top-3 fuer Z8-2.x:**

1. **Hotspot #1 — `WorkflowCatalogService.GetStartableWorkflowDefinitionsAsync`** → Slice **Z8-2.1**.
   - User-sichtbarer Hot-Path bei jedem Workflow-Anlegen durch Manager.
   - Hebel: N+1 ueber `IsManagerCreatableDefinition` aufloesen (Bulk-Lookup oder SQL-seitige `EXISTS`-Auswertung im selben Statement, das die Definitionen liefert).
   - Pagination greift hier nicht (Auswahl-Liste, fachliche Vollstaendigkeit erforderlich) — daher gezielter Bulk- bzw. JOIN-Pushdown statt LIMIT.
   - Risiko: gering, klar lokal abgrenzbar; keine API-Vertragsaenderung erwartet → keine FE-Folge.
   - Modell: opus, Reasoning: medium..high.

2. **Hotspot #2 + #3 — `RotationNotificationService` Daily-Sweep + `ApplyRotationNotificationDispatchResults`** → **gemeinsamer Slice Z8-2.2**.
   - Begruendung fuer Bundling: #3 ist der DB-Schreibpfad genau fuer die Eintraege, die #2 lieferte. Getrennt zu schneiden hiesse, eine Haelfte (LIMIT) ohne die andere (Batch-Update) zu landen, was den Sweep-Cliff nur halb behebt und zusaetzliche Migrations-Schritte zwischen den Slices erzeugt. Gemeinsam ist der Slice immer noch klein und in einem Service-Namespace.
   - Hebel: dispatchable-Notifications mit `LIMIT`/Batch-Fenster laden, Apply-Phase auf Batch-Update statt Select+Update pro Item.
   - Risiko: gering, Background-Pfad ohne UI-Vertraege.
   - Modell: sonnet, Reasoning: medium..high.

3. **Hotspot #4 — `EntraDirectorySyncService.SyncAllAsync` (Group×Member-Schleifen)** → Slice **Z8-2.3**, bewusst **hinter** #1 und #2/#3.
   - Entscheidung: #4 bleibt in der Top-3, aber **als letzter** der drei Z8-2-Slices. Begruendung: Timer-Pfad (24h-Sweep) ohne aktuelle User-Beschwerde, aber groesster Round-Trip-Hebel pro Sweep und einziger der Top-Sweeps mit Batch-Insert-Potenzial fuer Group-Memberships. Vor #1/#2 zu ziehen waere falsch priorisiert (kein User-Pfad). Nach Z8-3 zu schieben waere unsauber, weil Z8-3 explizit fuer `RotationTaskRegenerationEngine`-Sweep und Resthebel reserviert ist und der Entra-Sweep technisch denselben Batching-Ansatz wie #2 nutzt — also gehoert er thematisch zum Pushdown-Block, nicht zum Resthebel.
   - Hebel: Batch-`UpsertDirectoryIdentity` und Batch-`InsertGroupMembership` statt Item-by-Item; ggf. Set-Diff statt Vollabgleich pro Group.
   - Achtung: vollstaendiger File-Split bleibt LQ2-Z3 deferred — Z8-2.3 fasst nur die Sync-Schleifen an, keine Strukturhygiene.
   - Modell: sonnet, Reasoning: medium.

**Nicht in Top-3 fuer Z8-2.x (bewusst):**
- #5 `WorkflowVisibilityService.ApplyWorkflowTaskPermissions`: vor Slice noch verifizieren, ob Policy-Service intern Repo-Hits macht. Bleibt MEDIUM und wird nach Bedarf in Z8-3 oder einem Folgezyklus aufgenommen.
- #6 `GetDepartmentsAsync` / `GetRolesAsync`: erzeugt API-Vertragsaenderung (Pagination/Suche) und FE-Folgen. Ohne konkreten Last-Trigger nicht in Z8-2 — Re-Bewertung am Ende von Z8.
- #7 `BuildReadyTaskNotificationPreviewTargetsAsync`: gehoert zu Z8-3 (Notification-Pfad-Resthebel).
- #8 `RotationTaskGenerationService.RegenerateDepartmentPlansAsync`: admin-getriggert, kein Hot-Path → Z8-3.
- #9 `EntraDirectorySyncService.ImportDirectoryIdentitiesAsync`: Admin-Massen-Import, kein permanenter Last-Pfad → Folgezyklus.

**Frontend-Folgen Z8-2.x:** weiterhin **keine**. #1, #2/#3, #4 aendern keine API-Vertraege; #6 mit FE-Folgen bewusst nicht in Top-3. `FRONTEND_TODO.md` wird nicht angefasst.

**Reihenfolge / Abhaengigkeiten:**
- Z8-2.1 → Z8-2.2 → Z8-2.3 sequenziell. Keine harten Code-Abhaengigkeiten zwischen den Slices, aber sequenziell, damit der Worker nicht parallel mehrere Pfade halb anfasst und die Tests pro Slice klar zuordenbar bleiben.
- Z8-3 startet nach Z8-2.3 mit Resthebel #5/#7/#8.
- Z8-4 (Test-Coverage) parallel pro Slice mitziehen, nicht erst am Ende — Z8-4 bleibt als eigene ID nur fuer ergaenzende Coverage uebrig.

---

## Abgeschlossener Zyklus 7 — Kurzfassung

Zyklus 7 hat zwei zentrale Hygiene-Bloecke abgeschlossen:
- Lifecycle-Service-Konsolidierung: `WorkflowLifecycleService` ist jetzt die Commit-Grenze fuer Create/Form/Approval/Task.
- Validation-Split: `WorkflowDefinitionValidationService` wurde in Draft-/Snapshot-/Helper-/Catalog-Slices zerlegt.

Die Detailhistorie von Zyklus 7 liegt in:
- `CODE_REVIEW_ARCHIVE.md`
- `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`

---

## Offene Befunde aus frueheren Zyklen

| ID | Aufgabe | Status | Quelle |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | offen — Nutzer-Aufgabe, KI kann nicht pruefen | L7 |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | backlog — kein konkreter Bedarf | L7 |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | deferred — wartet auf Produkt-Entscheidung | Zyklus 1 |
| LQ2-Z3 | `EntraDirectorySyncService.cs` (2591 → 1563 Z.) Split + Coverage `UpsertDirectoryIdentitiesBatch`/`InsertGroupMembershipsBatch` | **abgeschlossen als Zyklus 9** (2026-05-05) — Z9-1.1/1.2 Inventur+Plan, Z9-2.1/2.2/2.3 Splits, Z9-3 Coverage | Zyklus 3 / Z8 → Z9 |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | deferred — admin-getriggert, kein Hot-Path; kein kleiner SQL-/Batch-Hebel ohne breiten Umbau an `SynchronizeRotationGeneratedTasks` | Zyklus 8 |
| Z8-1.2/#6 | `GetDepartmentsAsync`/`GetRolesAsync` (und vergleichbare Master-Data-/Admin-Listen) ohne Pagination/Suche/Sort-Vertrag | **abgeschlossen als Zyklus 10** (2026-05-05) — adressiert breiter als nur #6 unter „Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege, Query-Kontrakt-Risiken" | Zyklus 8 → Zyklus 10 |

---

## Zyklus-Historie

| Zyklus | Datum | Hauptthema |
|--------|-------|------------|
| 1 | 2026-04-23 | Code-Review + Hardening (C1–C4, H1–H7, L1/L3/L5/L6) |
| 2 | 2026-05-02 | HQ1–HQ5 + LQ1–LQ7: Decision-Migration, SQL-Task-Filter, Audit-Trail, Testcontainers, Page-Refactor |
| 3 | 2026-05-02 | Test-Coverage + Wartbarkeits-Split: Hook-Tests, Repo-Splits, Hook-Zerlegung |
| 4 | 2026-05-02 | Naming + Haertungen: LegacyProcessTypeKey, effectiveResponsibilityIds, Error-Boundaries |
| 5 | 2026-05-02..03 | Legacy-Abbau (LA1–LA5): LegacyWorkflowStatus, setup-Node, definition_key, HasLegacyRolePermission, Specs am Node |
| 6 | 2026-05-03..04 | Runtime-Lifecycle (Schritt 7): Engine-Extraktion + Lifecycle-Service mit Conn+Tx-Scope |
| 7 | 2026-05-05 | Lifecycle-Service-Konsolidierung + Validation-Split |
| 8 | 2026-05-05 | Skalierbarkeits- & Last-Haertung (Hotspots #1/#2/#3/#4/#7 gepushed; #5 verifiziert; #8 deferred; Z8-4 Coverage) |
| 9 | 2026-05-05 | `EntraDirectorySyncService`-Split / Testbarkeit (LQ2-Z3 aktiviert) — abgeschlossen |
| 10 | 2026-05-05 | Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege, Query-Kontrakt-Risiken — eroeffnet |
