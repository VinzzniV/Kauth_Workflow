# Code Review Status

#stand #review

Aktueller Stand der Code Review. Was offen ist.

Primärquelle im Repo: `CODE_REVIEW.md`

> Diese Datei kann veralten. Für den aktuellen Stand immer `CODE_REVIEW.md` und `TODO.md` im Repo prüfen.

## Zweck

- menschlich lesbarer Spiegel des aktuellen Review-Status
- komprimierte Orientierung ohne die aktive Root-Datei zu ersetzen

## Nicht verwenden fuer

- den exakten naechsten Arbeitsschritt
- Slice-fuer-Slice-History abgeschlossener Zyklen

---

## Schreibregel (verbindlich)

Jedes Review-Finding und jeder Slice in dieser Datei wird neben dem technischen Befund kurz aus Nutzersicht erklaert: was es praktisch bedeutet, warum es sich lohnt, das anzugehen, und was dadurch besser, sicherer, schneller oder wartbarer wird. Detail in `CODE_REVIEW.md` § „Schreibregel fuer Reviews und Findings" und `CLAUDE_CONTROL.md`.

---

## Gesamtbewertung (Stand 2026-05-08 — Zyklus 15 vollstaendig abgeschlossen: Z15-S1 Datenmodell/Hook done; Z15-S2 Override-Stellen done — fachlicher Fehler behoben; Z15-S3 Persona-Switcher done. Kein aktiver Folgezyklus offen. Zyklen 8–14 abgeschlossen.)

| Bereich | Note | Hauptgrund |
|---------|------|-----------|
| Backend-Architektur | A- | Repo-Monolith reduziert; Lifecycle-Service nach Z7 zentrale Commit-Grenze fuer Runtime-/Task-Mutationen; Resthebel jetzt vor allem Skalierbarkeit |
| Datenbankdesign | A- | Solides Schema, gute Constraints |
| Auth & Berechtigungen | B+ | Permission-Audit hat Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | B+ | Engine als Domain-Service + HQ5-Hooks getestet; Sweep-Timeout |
| Frontend-Architektur | B+ | Builder + Listen-Workspaces refactored; Split-Views + Karten/Tabelle; Personenakte 360° |
| Testbarkeit | B | Testcontainers + Integration-Tests; Lifecycle-Service hat jetzt einen eigenen Service-Test fuer Routing, Rollback und Automation-Scope |
| Skalierbarkeit | B- | Workflow-Task-Filter SQL-pre-narrowed |
| Sicherheit | B+ | `/client/log-events` rate-limited; dev-sim-Guard verifiziert |
| Lesbarkeit | B+ | Konventionen durchgaengig; grobe Monolithen reduziert, Resthebel liegen eher bei Lastpfaden als bei Strukturhygiene |

---

## Zyklus-Historie

| Zyklus | Datum | Stichwort |
|--------|-------|-----------|
| 1 | 2026-04-23 | Code-Review + Hardening: C1–C4, H1–H7, L1/L3/L5/L6 |
| 2 | 2026-05-02 | HQ1–HQ5 + LQ1–LQ7: Decision-Migration, Task-Filter SQL-Push, Audit-Trail, Testcontainers, Page-Refactor |
| 3 | 2026-05-02 | Test-Coverage + Wartbarkeits-Split: HQ1-Z3 (Hook-Tests), HQ3-Z3 (Repo-Splits), HQ2-Z3 (Hook-Zerlegung) |
| 4 | 2026-05-02 | Naming + Härtungen: HQ1-Z4 LegacyProcessTypeKey, HQ2-Z4 effectiveResponsibilityIds, LQ1-Z4..LQ4-Z4 |
| 5 | 2026-05-02..03 | Legacy-Abbau: LA1 (LegacyWorkflowStatus), LA2 (setup-Node), LA3 (definition_key Filter), LA4 (HasLegacyRolePermission), LA5 (Specs am Node) |
| 6 | 2026-05-03..04 | Runtime-Lifecycle (Schritt 7): Engine-Extraktion + Lifecycle-Service mit Conn+Tx-Scope; 409 Tests gruen |
| 7 | 2026-05-05 | Lifecycle-Service-Konsolidierung (Z7-1 in 5 Sub-Slices) + Validation-Service-Split (Z7-3 in 4 Sub-Slices); 416/417 Tests gruen |
| 8 | 2026-05-05 | Skalierbarkeits- & Last-Haertung: #1 Bulk-Lookup, #2/#3 Sweep+Apply Batching, #4 Entra Group/Member Bulk, #7 Recipient-Bulk; #5 false positive; #8 deferred; Z8-4 Coverage |
| 9 | 2026-05-05 | `EntraDirectorySyncService`-Split / Testbarkeit (LQ2-Z3) — abgeschlossen (Split + Coverage) |
| 10 | 2026-05-05 | Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege, Query-Kontrakt-Risiken — abgeschlossen (Review-/Planungszyklus, alle Slices done) |
| 11 | 2026-05-05..06 | Admin-/Master-Data-Listen-Vertraege in Umsetzung — abgeschlossen (F1 P1+B Master-Data, F2 P2+Audit, F3 P1+D Builder, alle Slices done) |
| 12 | 2026-05-06 | Admin-Dashboard-Betriebsblock fuer Runtime-/System-Health — abgeschlossen (Z12-1.1 done; Z12-1.2 done; Z12-2.1 done: `GET /admin/runtime-health` + `AdminRuntimeHealthService` + 42 Tests; Z12-2.2 done: Frontend Betriebsblock mit Severity-Badge, API-Prozess/Abhaengigkeiten/Storage-Kacheln) |
| 13 | 2026-05-06 | Echte Linux-Host-/VM-Metriken im Admin-Runtime-Health-Block — abgeschlossen (Z13-1 Zykluseroeffnung/Scope; Z13-2 Implementierung done: HostHealthDto + procfs-Leser + FE-Kachel + compose.prod.yml + start-vm.sh dev + 50 Tests gruen) |
| 14 | 2026-05-07 | Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung — abgeschlossen (Z14-1.1 Inventur done; Z14-1.2 Vertrags-/UX-Entscheidung done; Z14-1.3 Slice-Plan Folgezyklus done) |
| 15 | 2026-05-08 | Implementierung Mehrrollen-Persona — **vollstaendig abgeschlossen** (Z15-S1 done; Z15-S2 Override-Stellen done; Z15-S3 Persona-Switcher done; 274/274 Tests gruen) |

---

## Abgeschlossener Zyklus 14 — Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung (2026-05-07)

Eroeffnet und abgeschlossen 2026-05-07 als reiner Review-/Planungszyklus. **Keine Implementierung in Z14.** Detail jetzt in `CODE_REVIEW_ARCHIVE.md` (Zyklus 14); `CODE_REVIEW.md` bleibt die kompakte aktive Steuerdatei.

**Praktisch:** Ein Benutzer mit mehreren Rollen (z. B. Admin + Fachbereich, Admin + Manager, Admin + HR) landet im Dashboard und in der Navigation auf einer generischen Sammelansicht und verliert dabei die fachlich erwartete Sicht. Wer als Admin arbeiten will, sieht den Admin-Betriebsblock nicht; wer als Manager arbeiten will, verliert die Manager-Aktionen. Die Wahl der Ansicht ist heute implizit, nicht steuerbar, und greift bereits bei zwei Rollen.

**Lohnenswert:** Genau die Personen mit den meisten Rollen sind die Power-User des Systems. Der Effekt trifft also den Alltag der Schluesselnutzer, nicht Randfaelle. Die Logik liegt zentral an wenigen Stellen (`web/src/auth/roleModel.ts`, `web/src/navigation/useRoleAwareNavigation.ts`, Dashboard- und Insights-Schichten), d. h. der Hebel pro Aufwand ist hoch und ein sauberer Vertrag laesst sich definieren, bevor weitere Persona-Verzweigungen entstehen.

**Nutzen:** klare Begriffstrennung Rolle vs. Persona vs. aktive Ansicht; vorhersagbares Verhalten beim Login mit mehreren Rollen; ein dokumentierter Vertrag als Andockpunkt fuer kuenftige Personas, statt jedes Mal die `generic`-Falle zu erweitern.

| Befund | Prio | Status |
|--------|------|--------|
| Z14-1.1 — Inventur Persona-/Mehrrollen-Kollisionen (alle Stellen, an denen `dashboardPersona` / `hasMultipleRoles` Sicht/Aktionen/Navigation/Insights kollabieren; betroffene Bereiche Dashboard-Overview, Navigation, Aktionen, Insights, Admin-Betriebsblock; Auflistung der heutigen `generic`-Faelle und ihrer Konsequenzen fuer den Nutzer) | HIGH | done 2026-05-07 — Inventur jetzt in `CODE_REVIEW_ARCHIVE.md` (Zyklus 14): Override `useRoleAwareNavigation.ts:253` + Routing-Override `roleModel.ts:210`; vier Sicht-Konsumenten kollabieren auf `generic`; Header/Aktionen/Routen-Guards bleiben capability-getrieben und sind nicht betroffen |
| Z14-1.2 — Vertrags-/UX-Entscheidung: Begriffsklaerung Rolle vs. Persona vs. aktive Ansicht; Optionen fuer Mehrrollen-Behandlung skizzieren (Switcher mit Default + Persistenz; Aggregations-Persona statt `generic`; Admin-Vorrang fuer Admin+X; explizite Login-Auswahl); Pro/Contra je Option, ohne Festlegung | HIGH | done 2026-05-07 — Vertrag jetzt in `CODE_REVIEW_ARCHIVE.md` (Zyklus 14): Begriffsraster Rolle/Persona/aktive Ansicht; vier Optionen mit Pro/Contra; Vorzugsrichtung Persona-Switcher mit Vorrangs-Default + Persistenz, Admin-Vorrang als Default-Regel, Fallback `generic` |
| Z14-1.3 — Slice-Plan Folgezyklus: 2–3 sichere Umsetzungsslices mit Reihenfolge-Begruendung (typisch: Vertrag/Datenmodell zuerst, dann FE-Switcher, dann Aufraeumen der `generic`-Faelle in den abhaengigen Bloecken) | HIGH | done 2026-05-07 — Slice-Plan jetzt in `CODE_REVIEW_ARCHIVE.md` (Zyklus 14): drei Umsetzungsslices I Datenmodell „aktive Ansicht", II gemeinsame Umstellung der zwei Override-Stellen, III sichtbarer Persona-Switcher; empfohlen `claude-sonnet-4-6` + `--effort medium` |

**Z14-1.1 Kernergebnis:** zwei Override-Punkte tragen die Falle — `web/src/navigation/useRoleAwareNavigation.ts:253` (Sicht-Kollaps auf `generic`) und `web/src/auth/roleModel.ts:210` (Login-Routing zwingt auf `/`). Vier Sicht-Konsumenten haengen direkt an der effektiven Persona: `DashboardOverview` (Admin-Block, Filter, Manager-Liste), `DashboardPage` (Seitenkopf + optionaler Anlege-Button), `useDashboardInsights`/`useDashboardInsightsQuery` (Cache-Schluessel persona-getrieben), Insight-Lader-Switch (`loadDashboardInsights` ruft `loadGenericInsights` mit leerem Datensatz). Header-Navigation und Schnellaktionen bauen direkt auf Capabilities und sind **nicht** betroffen — der Effekt ist eine **Sicht**-Falle, keine **Aktions**-Falle. Generic-Faelle und ihre Nutzerfolge: Admin+X verliert den Admin-Betriebsblock; HR+Manager verliert HR-Engpaesse + Manager-Freigaben; Manager+Worker verliert Freigaben + eigene Aufgaben; jeder Mehrrollen-Login landet auf einer leeren Generic-Seite mit „Freigegebenen Bereich waehlen.". Vertragsanker fuer Z14-1.2: `loadDashboardInsights(persona, options)` ist die zentrale Persona-Vertragsstelle; der Query-Key cached bereits pro Persona; ein kuenftiger Switcher dockt hier an, ohne die Loader oder die Capability-Schicht zu beruehren.

**Z14-1.2 Kernergebnis:** Begriffsraster verbindlich gesetzt — **Rolle** (persistente Berechtigung, additives Capability-Set), **Persona** (deterministischer Default aus Vorrangskette `admin > hr > manager > worker > reader > generic`), **aktive Ansicht** (vom Nutzer beeinflussbare, persistierte Praeferenz, die ausschliesslich Sicht-Konsumenten steuert, niemals Rechte). Vier Optionen mit Pro/Contra: (A) Persona-Switcher mit Default + Persistenz, (B) Aggregations-Persona statt `generic`, (C) Admin-Vorrang fuer Admin+X, (D) explizite Login-Auswahl. Vorzugsrichtung als Vertragsentscheidung: **A als tragende Entscheidung, C als Default-Regel, harte Fallback-Regel auf `generic`** — A ist die einzige Option, die Persona (Default) und aktive Ansicht (Auswahl) sauber trennt; B ueberlaedt das Dashboard und loest das Routing nicht; C deckt die haeufigste Konstellation ab und passt natuerlich als Default; D bringt ohne Persistenz mehr Reibung und kollabiert mit Persistenz auf A. Vertrag muss liefern: sichtbarer Schalter nur fuer `hasMultipleRoles === true`; Default aus Vorrangskette; Persistenz in `localStorage` mit person-/user-gebundenem Schluessel (Cross-Device-Hebung explizit ausserhalb Z14); Fallback-Kaskade aktive → Default → `generic`; **getrennte** Versorgung der zwei Override-Stellen aus Z14-1.1 (Dashboard-Sicht in `useRoleAwareNavigation.ts:253` *und* Login-Routing in `roleModel.ts:210` lesen aus derselben Quelle „aktive Ansicht"). Capabilities, Header, Schnellaktionen, Routen-Guards bleiben explizit unberuehrt. Andockpunkte fuer Z14-1.3 ohne Logik-Aenderung: `loadDashboardInsights(persona, options)`, `queryKeys.dashboard.insights(persona, definitionKey)`, drei `DashboardOverview`-Schalter (`isAdminDashboard`, `supportsProcessTypeFilter`, Manager-Liste); `loadGenericInsights` bleibt als Fallback-Pfad erhalten.

**Nicht in Z14:** Backend-Aenderungen am Berechtigungsmodell (Rollen/Permissions bleiben unveraendert; Z14 betrifft die Ableitung der **Sicht**, nicht der **Rechte**); neue Personas oder Spezialrollen einfuehren; Mobile-/Tablet-Layout (R10 bleibt eigenstaendig); UI-Polish ausserhalb der Persona-/Sicht-Logik.

**Z14-1.3 Kernergebnis:** Drei Umsetzungsslices in fester Reihenfolge, jeder fuer sich freigabefaehig und reversibel. **Slice I — „aktive Ansicht" als reines Datenmodell + Persistenz + Fallback:** neuer FE-Hook (Arbeitstitel `useActiveView`), Fallback-Kaskade persistiert (`localStorage`-Schluessel `kauth.activeView.<personId>`) → Default aus Vorrangskette `admin > hr > manager > worker > reader > generic` → harter Fallback `generic`; Validierung gegen aktuelle Capabilities; Setter; **null Sicht-Konsumenten**, weder `useRoleAwareNavigation.ts:253` noch `roleModel.ts:210` werden in Slice I angefasst; Loader, Query-Key, drei `DashboardOverview`-Schalter und `loadGenericInsights` bleiben unberuehrt; Tests Vitest-Unit. **Slice II — zwei Override-Stellen aus Z14-1.1 gemeinsam auf neue Quelle:** `useRoleAwareNavigation.ts:253` liest die aktive Ansicht statt hart `generic` zu setzen; `roleModel.ts:210` liest dieselbe Quelle, sodass Login-Routing fuer Mehrrollen-Nutzer wieder die persona-spezifische Default-Route faellt (admin → `/admin/config`, manager → `/supervisor`, worker → `/tasks/my`); Mehrrollen-Nutzer sehen nach diesem Slice ihre Default-Sicht zurueck — der Power-User-Effekt der `generic`-Falle ist beseitigt, ohne UI-Flaeche; Tests `roleModel.test.ts` erweitern + neuer `useRoleAwareNavigation`-Test. **Slice III — sichtbarer Persona-Switcher fuer Mehrrollen-Nutzer:** Auswahlkomponente nur fuer `hasMultipleRoles === true`, schreibt ueber den Hook aus Slice I; Single-Role-Nutzer sehen nichts; Komponententest + Browser-Smoke. **Reihenfolge-Begruendung:** Datenmodell zuerst → vermeidet doppeltes Umbiegen, gibt Slice II eine fertige Quelle; Routing/Sicht in der Mitte → Vertragspflicht „beide Override-Stellen aus derselben Quelle" wird im selben Slice erfuellt, Sicht und Routing koennen nicht auseinanderlaufen; UI zuletzt → Slice III ist Komfort, kein Korrekturpfad; nach Slice II ist das System bereits korrekt. Sicherer als ein grosser UI-Umbau, weil jede Risikoachse (Persistenz/SSR, Routing-Regression, UI/A11y) in einem eigenen Slice liegt; jeder Slice ist allein freigabefaehig und reversibel. **Bewusst NICHT in Slice I:** UI-Element, Aenderung an Override-Stellen, Loader, Query-Key, drei `DashboardOverview`-Schalter, Backend, API-Erweiterung, Server-Persistenz, Telemetrie/Logs. **Modell/Effort pro Slice:** `claude-sonnet-4-6` + `--effort medium` (Slice III darf bei breiterer UX-Skizze auf `claude-opus-4-7` eskalieren).

**Naechster Schritt:** Zyklus 14 vollstaendig abgeschlossen (2026-05-07). Kein aktiver Zyklus. Codex eroeffnet im Folgezyklus (Arbeitstitel Zyklus 15) Slice I aus Z14-1.3 und erzwingt Modell/Effort per CLI-Flag.

---

## Abgeschlossener Zyklus 12 — Admin-Dashboard-Betriebsblock fuer Runtime-/System-Health (2026-05-06)

Eroeffnet 2026-05-06. Thema: das Admin-Dashboard soll fuer `admin` Signale aus dem laufenden System sichtbar machen — API/DB/Directory/Mail-Status plus einfache Runtime-Metriken (Prozess-Speicher, Uptime, Storage der App-Schreibpfade). Bestehender Admin-Health-Begriff (`admin-health-panel` + `/health/*`-Endpunkte) bleibt Anker; keine konkurrierende zweite Betriebslogik.

**Wichtige Produkt-/Scope-Entscheidung:** Z12 adressiert **zuerst App-/Runtime-Health**, **nicht** vollwertige Host-/VM-Metrik. Echte Host-Metrik (CPU/RAM/Disk Server) bleibt bewusst optionaler Folgeschritt nach Z12-2.2.

**Praktisch:** Admins sehen ohne Server-Login direkt im Dashboard, ob die App und ihre Abhaengigkeiten gesund laufen — heute muss man dafuer mehrere Stellen kombinieren oder auf den Server. **Lohnenswert:** App-/Runtime-Health hat den groessten Hebel pro Aufwand und schafft den Anker, an dem ein spaeterer Host-Metrik-Ausbau sauber andocken kann, statt App-Signale zu ueberbauen. **Nutzen:** ein konsolidierter Betriebsblock; klare Begriffstrennung App vs. Container vs. Host; ein expliziter Runtime-Health-Vertrag, der wiederverwendbar ist. Detail in `CODE_REVIEW.md` § „Aktiver Zyklus 12".

| Befund | Prio | Status |
|--------|------|--------|
| Z12-1.1 — Begriffsklaerung / Vertragsinventur Runtime Health (heutige Signale, fehlende Signale, App vs. Container vs. Host) | HIGH | done 2026-05-06 — Inventur in `CODE_REVIEW.md` § Z12-1.1 (heutige Signale `/health/*` + `admin-health-panel`; fehlende App-Signale Prozess-Uptime/Managed Heap/Working Set/Storage; Drei-Domaenen-Modell App/Container/Host; UI-Wording-Empfehlung „Betriebsstatus") |
| Z12-1.2 — Vertrags-Skizze DTO + Schwellwerte + Begriffsabgrenzung App/Container/Host | HIGH | done 2026-05-06 — Vertrags-Skizze in `CODE_REVIEW.md` § Z12-1.2 (`GET /admin/runtime-health` admin-only; `AdminRuntimeHealthDto` mit `application`/`dependencies`/`directory`/`storage[]`; Severity vier Stufen + `overallSeverity`-Aggregation mit `unknown`-Neutralisierung; Schwellwerte deklarativ; FE-Andock im bestehenden `admin-health-panel`; Abgrenzung gegen Host-/VM-Metrik / Prometheus / Trends / Alerts / Mail-Send-Probe / `/health/*`-Aenderungen) |
| Z12-2.1 — Backend Runtime-Health Endpoint + Service (App-/Runtime-Signale, kein Host-/VM-Metrik-Code) | HIGH | done 2026-05-06 — `GET /admin/runtime-health` + `AdminRuntimeHealthService` + DTO-Familie + Severity-Logik deklarativ; 42 neue Tests; Storage via `RUNTIME_HEALTH_STORAGE_PATHS` |
| Z12-2.2 — Frontend Admin-Dashboard-Betriebsblock (andockend an `admin-health-panel`) | HIGH | done 2026-05-06 — `DashboardAdminRuntimeHealthBlock` als Zone 4 in `DashboardOverview`, nur fuer `dashboardPersona === "admin"`; Severity-Badge, API-Prozess-Kachel, Abhaengigkeiten-Kachel, Storage-Kacheln (optional); Polling 60s/120s; Wording „Betriebsstatus"; `AdminOverviewWorkspaceSection` unveraendert |
| *(Folgeschritt nach Z12)* — Optionaler Host-/VM-Metrik-Ausbau (CPU/RAM/Disk Server) — eigener Zyklus oder Slice nach Z12-2.2, nur bei konkretem Bedarf | — | bewusst ausserhalb Z12 |

**Z12-1.1 Kernergebnis:** Heute existieren zwei getrennte Health-Welten — Backend-Endpunkte (`/health/live`, `/health/ready`, `/health` in `api/API/Extensions/LifecycleApplicationExtensions.cs`) und ein Admin-UI-Block (`admin-health-panel` in `web/src/components/admin-config/AdminOverviewWorkspaceSection.tsx` mit Verzeichnis-Sync, Mail, offene Warnungen). Sie sind nicht verbunden. App-/Runtime-Signale (Prozess-Uptime, Managed Heap, Working Set, Storage-Auslastung App-Schreibpfade) fehlen komplett. Drei-Domaenen-Modell als Pflicht-Begriffsraster fuer Z12-1.2: **App-/Runtime-Health** (im Z12-Scope), **Container-/Volume-Sicht** (im Z12-Scope, aber als „API-Container" / „Schreibpfad der Anwendung" labeln), **Host-/VM-Metrik** (ausserhalb Z12). UI-Wording-Empfehlung: „Betriebsstatus" / „Runtime Health der Anwendung", „API-Prozess: Managed Heap", „Schreibpfad der Anwendung" — **nicht** „Server", „RAM frei", „Disk frei".

**Leitplanken Z12:** App-/Runtime-Health zuerst (kein Host-/VM-Metrik-Code in Z12); bestehender Admin-Health-Begriff bleibt Anker; Begriffstrennung App vs. Container vs. Host Pflicht; Reihenfolge streng sequenziell Z12-1.1 → Z12-1.2 → Z12-2.1 → Z12-2.2; Z12-1.x sind reine Doku-Slices; Schwellwerte/Severity in Z12-1.2 deklarativ skizzieren.

**Naechster Schritt:** Zyklus 12 vollstaendig abgeschlossen (2026-05-06). Kein aktiver Zyklus. Optionaler Host-/VM-Metrik-Ausbau bleibt bewusst als eigenstaendiger Folgeschritt dokumentiert; Codex entscheidet ueber den naechsten aktiven Zyklus.

---

## Abgeschlossener Zyklus 11 — Admin-/Master-Data-Listen-Vertraege in Umsetzung (2026-05-05..06)

Eroeffnet 2026-05-05, abgeschlossen 2026-05-06 als reiner Umsetzungszyklus auf Basis von Z10-1.3. Reihenfolge F1 → F2 → F3 streng sequenziell durchgespielt. **Alle drei Slices done.** Detail in `CODE_REVIEW.md` § „Abgeschlossener Zyklus 11".

**Praktisch:** F1 macht Erfassungseinstieg und Master-Data-Pflege spuerbar schnell und konsistent; F2 macht Audit-Verlauf vollstaendig durchsuchbar (heute schneidet ein stiller `limit`-Cap die Historie ab); F3 macht den Builder schnell, auch wenn Definitionen wachsen. **Lohnenswert:** Vertrag jetzt sauber bauen, statt unter Last halbgaarig nachzuruesten — drei kontrollierte Slices statt N parallele Mini-Vertraege. **Nutzen:** zwei zentrale Hull-Typen (`AdminListPageDto<T>`, `CursorPageDto<T>`), zwei typed FE-Adapter, eine wiederverwendbare Refactor-Achse fuer alle spaeter folgenden Listen.

| Befund | Prio | Status |
|--------|------|--------|
| Z11-F1 — P1-Hull einfuehren + B Master-Data/Lookups (`/departments`, `/roles`, `/admin/master-data/{departments,positions,responsibilities}`); Server-`search`/`sort`-Whitelist; FE: typed Wrapper + Aufrufer-Refactor | HIGH | done 2026-05-05 — P1-Hull live fuer 5 Endpunkte, FE-Adapter aktiv, aktuelle Consumer auf `items` adaptiert |
| Z11-F2 — P2-Hull einfuehren + Audit-Streams (`/admin/auth/audit`, `/admin/directory/audit`); opaque Base64-Cursor; FE: typed Wrapper + „Mehr laden"-Knopf | HIGH | done 2026-05-06 — `CursorPageDto<T>` + Keyset-Pagination, typed FE-Wrapper, Akkumulations-Hook, „Mehr laden"-Knopf in beiden Audit-Tabs |
| Z11-F3 — P1 ausrollen + D Builder-Tabs (sieben scoped Endpunkte: `workflow-definitions`, `action-definitions`, `task-templates` + `…/conditions` + `…/dependencies`, `answer-definitions`, `role-answer-defaults`); Pflicht-Scope; Filterzustand im FE in URL-Query als separater UI-Slice ausgelagert | HIGH | done 2026-05-06 — sieben scoped Builder-Endpunkte auf P1, FE-Service-Wrapper + alle Builder-Hooks auf `page.items` adaptiert |

**Nicht in Z11 (Begruendung in `CODE_REVIEW.md` § Z10-1.3):** A Identity-Listen, C `/admin/directory/identities`, C Gaps/Pending Split, E Notification-Templates, F Rotation Action-Templates, G Runtime-Sub-Resources, H `/workflow-definitions/startable`. Diese werden nach Z11 als billige Mitnahmeschnitte mit dem dann etablierten P1-/P2-Adapter geplant; der Composite-Split fuer C Gaps/Pending bleibt eigener vorbereiteter Slice.

Frontend-Folgen Z11-Fx: Konsequenz pro Slice, kein praeventiver Sammeleintrag — Z11-F1, Z11-F2 und Z11-F3 sind in `FRONTEND_TODO.md` als abgeschlossene Trigger vermerkt.

---

## Abgeschlossener Zyklus 10 — Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege, Query-Kontrakt-Risiken (2026-05-05)

Eroeffnet und abgeschlossen 2026-05-05 als reiner Review-/Planungszyklus. Alle drei Slices done. Formaler Zyklusabschluss vollzogen. Naechster Schritt: Eroeffnung Z11 auf Basis F1/F2/F3 aus `CODE_REVIEW.md` § Z10-1.3. Folge-Hebel aus Z8-1.2 (#6 `GetDepartmentsAsync`/`GetRolesAsync` ohne Pagination), aber breiter gefasst: das Vertrags-Thema betrifft mehrere Admin-/Master-Data-/Directory-Read-Pfade und nicht nur Departments/Rollen.

**Praktisch:** Admin-Listen werden bei wachsendem Bestand spuerbar langsamer; Suche und Filter laufen heute ueberwiegend im Browser, deshalb fuehlen sich Ergebnisse irgendwann unvollstaendig oder „zufaellig sortiert" an. **Lohnenswert:** ein einheitlicher Listen-/Such-/Sort-Vertrag jetzt zu definieren ist deutlich billiger als spaeterer Hotfix unter Last und vermeidet API-Brueche fuer das FE. **Nutzen:** stabile Antwortzeiten, vollstaendige Server-Suche, klarer Vertrag, der an mehreren Endpunkten gleich aussieht und so neue Listen direkt mitnimmt.

| Befund | Prio | Status |
|--------|------|--------|
| Z10-1.1 — Inventur aller Admin-/Master-Data-/Directory-Read-Endpunkte ohne Pagination/Suche/Sort-Vertrag (Datei/Symbol, FE-Aufrufer, Kardinalitaet, Spuerbarkeit fuer Nutzer) | HIGH | done 2026-05-05 — `CODE_REVIEW.md` § Z10-1.1 (Bloecke A Identity, B Master-Data + `/departments`/`/roles`, C Directory, D Builder, E Notification-Templates, F Rotation, G Runtime-Subresources, H Startable; saubere Listen und Single-Doc-Endpunkte explizit ausgenommen) |
| Z10-1.2 — Vertrags-Skizze pro Endpunkt (`limit`/`offset` vs. Cursor, Server-`search`, stabiler `sort`, Antwort-Hull) inkl. FE-Adaption-Folgen | HIGH | done 2026-05-05 — `CODE_REVIEW.md` § Z10-1.2 (Muster P1 Standard Admin Page / P2 Cursor Stream / P3 Typeahead Lookup; Bloecke A–H; Identity-/Directory-/Audit getrennt nach P1/P2; Builder als scoped P1; Runtime-Sub-Resources P2) |
| Z10-1.3 — Slice-Plan fuer Folgezyklus: erste 2–3 sichere Umsetzungsslices mit Begruendung der Reihenfolge | HIGH | done 2026-05-05 — `CODE_REVIEW.md` § Z10-1.3 (F1 P1+B → F2 P2+Audit → F3 P1+D Builder; bewusst spaeter: A Identity-Listen, C Identities, C Gaps/Pending Split, E Notification-Templates, F Rotation, G Runtime, H Startable mit Einzelbegruendung) |

Frontend-Folgen Z10-1.x: aktuell **keine**. Z10 produziert Inventur und Vertrags-Skizze, kein Code-Change. FE-Eintraege entstehen erst, wenn aus Z10-1.2 konkrete API-Vertragsaenderungen folgen — dann mit Trigger-Kennzeichnung in `FRONTEND_TODO.md`, nicht praeventiv.

Detail in `CODE_REVIEW.md` § „Abgeschlossener Zyklus 10".

---

## Abgeschlossener Zyklus 9 — `EntraDirectorySyncService`-Split / Testbarkeit (2026-05-05)

Abgeschlossen 2026-05-05. Folge-Hebel aus Z8: die in Z8-2.3 eingefuehrten Bulk-Helfer lagen `private` hinter dem 2.4k-Z. `SyncAllAsync`-Service. Z9 hat den Service in Graph-Adapter (`IEntraGraphClient`) und DB-Sync-Operations (`IEntraDirectorySyncOperations`) zerlegt und die Coverage gegen die neuen Interfaces gesetzt.

| Befund | Prio | Status |
|--------|------|--------|
| Z9-1.1 — Boundary-/Split-Inventur | HIGH | done 2026-05-05 — `CODE_REVIEW.md` § Z9-1.1 |
| Z9-1.2 — Extract-Plan (File-/Klassen-Schnitt) | HIGH | done 2026-05-05 — `CODE_REVIEW.md` § Z9-1.2 |
| Z9-2.1 — Pre-Cleanup (Dead-Code raus) + `SyncAllAsync` in Phasen-Methoden | HIGH | done 2026-05-05 — Dead-Code raus (DepartmentLead-Resolver + 5 Sub-Helfer + Single-Row-Helfer + Reflection-Test); `SyncAllAsync` in `RunGroupSyncAsync`/`RunDirectoryProjectionAsync`/`RunActivationAsync` zerlegt; 2591 → 2104 Z. |
| Z9-2.2 — Graph-Adapter `IEntraGraphClient` unter `Services/Directory/` extrahieren | HIGH | done 2026-05-05 — `IEntraGraphClient`/`EntraGraphClient` neu unter `api/API/Services/Directory/`; `Microsoft.Graph` aus Hauptdatei raus; DI scoped |
| Z9-2.3 — DB-Sync-Operations-Modul `IEntraDirectorySyncOperations` unter `Services/Directory/` extrahieren | HIGH | done 2026-05-05 — `IEntraDirectorySyncOperations`/`EntraDirectorySyncOperations` neu unter `api/API/Services/Directory/`; Service konsumiert per Konstruktor; Reflection-Test umgestellt; LOC Hauptdatei ~2030 → 1563 |
| Z9-3 — Coverage Batch-Helfer (aus Z8-4 verschoben) + Orchestrator-Stub-Tests | MEDIUM | done 2026-05-05 — `EntraDirectorySyncServiceTests` (3/3 gruen, Stub-Pfade ConnectionString/MissingCredentials/GraphFailed) + `EntraDirectorySyncOperationsIntegrationTests` (Upsert+Dedup, Membership-Insert+Idempotenz; Fixture-Gating wie Z8-4.1) |

Frontend-Folgen: keine. Detail in `CODE_REVIEW.md` § "Aktiver Zyklus 9".

---

## Abgeschlossener Zyklus 8 — Skalierbarkeits- & Last-Haertung (2026-05-05)

Geschlossen 2026-05-05. Skalierbarkeit war mit B- die niedrigste Gesamtnote nach Zyklus 7. Detail in `CODE_REVIEW.md` § "Abgeschlossener Zyklus 8".

| Befund | Prio | Status |
|--------|------|--------|
| Z8-1.1 — Inventur unbegrenztes Laden / In-Memory-Filter / N+1 | HIGH | done 2026-05-05 — Hotspot-Liste in `CODE_REVIEW.md` § Z8-1.1 |
| Z8-1.2 — Top-3-Hotspot-Auswahl + Slice-Plan | HIGH | done 2026-05-05 — Slice-Plan in `CODE_REVIEW.md` § Z8-1.2 |
| Z8-2.1 — Hotspot #1 `WorkflowCatalogService` N+1-Aufloesung | HIGH | done 2026-05-05 — Bulk-Lookup `GetManagerCreatableDefinitionKeys()` |
| Z8-2.2 — Hotspot #2+#3 `RotationNotificationService` Sweep+Apply (gemeinsamer Slice) | HIGH | done 2026-05-05 — Sweep batched (200), Apply Bulk-Metadata+Bulk-UPDATE |
| Z8-2.3 — Hotspot #4 `EntraDirectorySyncService` Group/Member Batch | HIGH | done 2026-05-05 — Bulk-Upsert via `unnest`+RETURNING und Bulk-Insert fuer Memberships ersetzen pro-Member Round-Trips |
| Z8-3.1 — #5 Verifikation + #7 Recipient-Bulk-Lookup | HIGH | done 2026-05-05 — #5 false positive (CPU/Policy), #7 Bulk-Lookup |
| Z8-3.2 — Hotspot #8 `RotationTaskGenerationService` | MEDIUM | deferred 2026-05-05 — kein kleiner SQL-/Batch-Hebel ohne breiten Umbau; admin-getriggert. Z8-3 geschlossen |
| Z8-4 — Test-Coverage fuer neu gepushte Pfade | MEDIUM | done 2026-05-05 — Z8-4.1 Bulk-Recipient-Helper; Z8-2.1/Z8-2.2-Coverage in den Umsetzungs-Slices; EntraDirectorySync-Batch-Helfer-Coverage nach LQ2-Z3 verschoben |

Frontend-Folgen: aktuell **keine**. Z8 ist backend-fokussiert; FE-Items entstehen erst, falls API-Vertraege brechen.

---

## Zyklusuebergreifend offen

| ID | Aufgabe | Status |
|----|---------|--------|
| R8 | Browser-Verifikation Form-Editor | offen — Nutzer-Aufgabe |
| R10 | Mobile-Layout Form-Editor | backlog |
| L2 | Datenbereinigung Drafts | deferred — Produkt-Entscheidung |
| LQ2-Z3 | `EntraDirectorySyncService` Split + Coverage Z8-2.3-Batch-Helfer | abgeschlossen als Zyklus 9 (2026-05-05) |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | deferred — admin-getriggert, kein kleiner SQL-Hebel |
| FE-8 | `approval_task_template_key` → `approval_spec_key` Rename | defer ohne Trigger |

---

## Verwandte Notizen

- `CODE_REVIEW_ARCHIVE.md` — Detailarchiv abgeschlossener Review-Zyklen
- [[Migrationspfad]] — Gesamtbild offener Punkte
- [[Schritt7-Runtime-TaskSystem-Skizze]] — Architekturarbeit aus Zyklus 6
- [[Rotation]] — Rotation-spezifische Issues
- [[Zielarchitektur]] — Worauf hingearbeitet wird
