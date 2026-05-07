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

- **Was bedeutet das praktisch?** — was ein normal verstaendlicher Leser im Alltag merkt.
- **Warum lohnt es sich, das anzugehen?** — der konkrete Anlass oder das Risiko.
- **Was wird dadurch besser, sicherer, schneller oder wartbarer?** — der erwartete Nutzen.

Reine Technik-Beschreibung ohne Nutzen-/Bedeutung-Erklaerung ist nicht ausreichend. Die Regel gilt fuer alle neuen Zyklen, fuer einzelne Befunde und fuer den jeweils gefuehrten Slice-Plan. Bei zyklusuebergreifend offenen Befunden reicht ein kurzer Hinweis, warum sie aktuell nicht angegangen werden.

Diese Regel ist auch in `CLAUDE_CONTROL.md` als Arbeits-Pflicht fuer Claude unter Codex-Orchestrierung verankert.

---

**Stand**: 2026-05-07 — Aktiver Zyklus 14 (Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung). Die abgeschlossenen Detailzyklen 8 bis 13 wurden in `CODE_REVIEW_ARCHIVE.md` ausgelagert; diese Datei bleibt die kompakte aktive Steuerdatei.
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..03 Zyklus 2–5; 2026-05-03..04 Zyklus 6; 2026-05-05 Zyklus 7; 2026-05-05 Zyklus 8 abgeschlossen; 2026-05-05 Zyklus 9 abgeschlossen; 2026-05-05 Zyklus 10 abgeschlossen; 2026-05-05 Zyklus 11 eroeffnet) + Codex-Fallback (2026-05-05 Z11-F1 Abschluss waehrend Claude-Rate-Limit) + Claude (2026-05-06 Z11-F2 Abschluss; 2026-05-06 Z11-F3 Abschluss = Z11 vollstaendig geschlossen; 2026-05-06 Z12 eroeffnet + abgeschlossen; 2026-05-06 Z13 eroeffnet + abgeschlossen; 2026-05-07 Z14 eroeffnet).

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

## Archivstatus

- Die Detailzyklen **Z8 bis Z13** liegen jetzt in `CODE_REVIEW_ARCHIVE.md`.
- Die Detailhistorie von **Zyklus 7** liegt weiterhin in `CODE_REVIEW_ARCHIVE.md` und `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`.
- In dieser aktiven Datei bleiben nur Gesamtbewertung, offene zyklusuebergreifende Befunde und die grobe Historie.

---

## Offene Befunde aus frueheren Zyklen

| ID | Aufgabe | Status | Quelle |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | offen — Nutzer-Aufgabe, KI kann nicht pruefen | L7 |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | backlog — kein konkreter Bedarf | L7 |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | deferred — wartet auf Produkt-Entscheidung | Zyklus 1 |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | deferred — admin-getriggert, kein Hot-Path; kein kleiner SQL-/Batch-Hebel ohne breiten Umbau an `SynchronizeRotationGeneratedTasks` | Zyklus 8 |

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
| 8 | 2026-05-05 | Skalierbarkeits- & Last-Haertung — abgeschlossen |
| 9 | 2026-05-05 | `EntraDirectorySyncService`-Split / Testbarkeit — abgeschlossen |
| 10 | 2026-05-05 | Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege, Query-Kontrakt-Risiken — abgeschlossen |
| 11 | 2026-05-05..06 | Admin-/Master-Data-Listen-Vertraege in Umsetzung — abgeschlossen |
| 12 | 2026-05-06 | Admin-Dashboard-Betriebsblock fuer Runtime-/System-Health — abgeschlossen |
| 13 | 2026-05-06 | Echte Linux-Host-/VM-Metriken im Admin-Runtime-Health-Block — abgeschlossen |
| 14 | 2026-05-07 | Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung — aktiv |

---

## Aktiver Zyklus 14 — Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung

Eroeffnet 2026-05-07 als reiner Review-/Planungszyklus. Ziel: Problem sauber eroeffnen und schneiden, **keine Umsetzung**.

**Praktisch:** Ein Benutzer mit mehreren Rollen — typisch Admin + Fachbereich, Admin + Manager, Admin + HR — landet im Dashboard und in der Navigation auf einer generischen Sammelansicht und verliert dabei die fachlich erwartete Sicht. Wer sich als „Admin" einloggen will, sieht den Admin-Betriebsblock nicht, weil die Mehrfachrolle die Persona auf `generic` zwingt; wer als Manager arbeiten will, verliert die Manager-Aktionen aus demselben Grund. Die Wahl der Ansicht ist heute implizit, nicht steuerbar, und greift bereits bei zwei Rollen.

**Warum lohnt es sich, das anzugehen:** Genau die Personen mit den meisten Rollen sind die Power-User des Systems (Admins, die zugleich Fachbereich/HR/Manager-Aufgaben haben). Der Effekt trifft also nicht Randfaelle, sondern den Alltag der Schluesselnutzer. Die Logik liegt zentral an wenigen Stellen (`web/src/auth/roleModel.ts`, `web/src/navigation/useRoleAwareNavigation.ts`, Dashboard- und Insights-Schichten), d. h. der Hebel pro Aufwand ist hoch und ein sauberer Vertrag laesst sich definieren, bevor weitere Rollen-/Persona-Verzweigungen entstehen.

**Was wird dadurch besser:** klare Begriffstrennung Rolle vs. Persona vs. aktive Ansicht; vorhersagbares Verhalten beim Login mit mehreren Rollen; ein dokumentierter Vertrag, an dem neue Personas (z. B. spaetere Spezialrollen) andocken koennen, statt jedes Mal die `generic`-Falle zu erweitern.

**Konkreter Anlass / technischer Verdacht (nicht abschliessend, Ergebnis von Z14-1.1):** in `web/src/navigation/useRoleAwareNavigation.ts` wird die effektive `dashboardPersona` bei `capabilities.hasMultipleRoles === true` deterministisch auf `"generic"` gesetzt; in `web/src/auth/roleModel.ts` wird `dashboardPersona` aus der hoechstrangigen Rolle abgeleitet (admin > hr > manager > worker > reader), aber das Ergebnis wird durch die Mehrfachrollen-Regel im Navigation-Hook ueberschrieben. Folge: Admin-Aktionen, HR-/Manager-Aktionen und persona-spezifische Insight-Bloecke werden ausgeblendet, sobald irgendeine zweite Rolle mitlaeuft. Diese Inventur ist Aufgabe von Z14-1.1; der Verdacht hier ist Anker, nicht Befund.

**Leitplanken Z14:**
- Keine Implementierung in Z14, nur Review-/Planungsdoku.
- Keine neuen Produktentscheidungen als abgeschlossen darstellen — Z14 hebt das Problem, schneidet Optionen, schlaegt Folge-Slices vor.
- Schreibregel verbindlich: jeder Slice nennt praktische Bedeutung, Anlass und erwarteten Nutzen.
- Kein verstecktes UI-Refactor unter „Persona-Vertrag".
- Begriffstrennung Pflicht: **Rolle** (Berechtigung), **Persona** (abgeleitete Standard-Sicht), **aktive Ansicht** (was der Nutzer aktuell sehen will). Diese Drei werden in Z14-1.2 sauber definiert, nicht vorausgesetzt.

| Befund | Prio | Status |
|--------|------|--------|
| Z14-1.1 — Inventur Persona-/Mehrrollen-Kollisionen: alle Stellen, an denen `dashboardPersona` / `hasMultipleRoles` die Sicht/Aktionen/Navigation/Insights veraendern oder kollabieren; betroffene Bereiche (Dashboard-Overview, Navigation, Aktionen, Insights, Admin-Betriebsblock, ggf. Sub-Pages); Auflistung der heutigen `generic`-Faelle und ihrer Konsequenzen fuer den Nutzer | HIGH | done 2026-05-07 — Inventur in `CODE_REVIEW.md` § Z14-1.1 (Override-Punkt in `useRoleAwareNavigation.ts:253`; zweiter Override im Login-Routing `roleModel.ts:210`; vier Sicht-Konsumenten kollabieren auf `generic`; Header/Aktionen/Routen-Guards bleiben capability-getrieben und sind nicht betroffen) |
| Z14-1.2 — Vertrags-/UX-Entscheidung: Begriffsklaerung Rolle vs. Persona vs. aktive Ansicht; Optionen fuer Mehrrollen-Behandlung skizzieren (z. B. Persona-Switcher mit Default + Persistenz; Aggregations-Persona statt `generic`; Admin-Vorrang fuer Admin+X; explizite Login-Auswahl); Pro/Contra je Option, ohne Festlegung; Vorgabe, was der Vertrag liefern muss (sichtbarer Schalter, Persistenz, Default-Regel, Fallback) | HIGH | offen |
| Z14-1.3 — Slice-Plan Folgezyklus: 2–3 sichere Umsetzungsslices mit Reihenfolge-Begruendung (typisch: Vertrag/Datenmodell zuerst, dann FE-Switcher, dann Aufraeumen der `generic`-Faelle in den abhaengigen Bloecken); ausdruecklich kein Code | HIGH | offen |

### Z14-1.1 Kernergebnis — Inventur Persona-/Mehrrollen-Kollisionen

**Praktisch:** Wer mehrere Rollen hat, sieht heute weder die Admin-Betriebsuebersicht noch die HR-/Manager-/Worker-spezifischen Bloecke. Stattdessen erscheint eine leere Generic-Seite mit dem Hinweis „Freigegebenen Bereich waehlen.". Das trifft genau die Power-User. Die Header-Navigation und die Schnellaktionen funktionieren weiter, weil sie auf Capabilities basieren — der Effekt sitzt in der **Sicht**, nicht in den **Rechten**.

**Lohnenswert:** Die Falle sitzt in zwei einzelnen Codezeilen mit kaskadierender Wirkung auf vier Sicht-Konsumenten und einen Routing-Pfad. Inventur jetzt sauber zu schneiden vermeidet, dass kuenftige Persona- und Insight-Erweiterungen jedes Mal die gleiche generic-Falle mitschleppen.

**Nutzen:** klare Abgrenzung Rolle vs. Persona vs. aktive Ansicht; benannte Einzeloverrides als zukuenftige Eingriffspunkte fuer Switcher/Aggregat/Vorrang; sichtbarer Beleg, dass Header/Aktionen/Routen-Guards nicht angefasst werden muessen.

#### Heutige Persona-Ableitungskette

1. `web/src/auth/roleModel.ts:151-161` — `dashboardPersona` aus Vorrangskette `admin > hr > manager > worker > reader > generic`. Auch bei Mehrfachrollen fuehrt das **nicht** zu `generic`; ein Admin+HR-User waere hier formal `admin`. **Praktisch:** die hier gewaehlte Persona ist nicht das, was der Nutzer am Ende auf dem Dashboard sieht.
2. `web/src/auth/roleModel.ts:132` — `hasMultipleRoles = roleSet.size > 1`. Zaehlt **jede** zweite Rolle, auch unkritische Kombinationen wie reader+worker. **Praktisch:** das Flag triggert den Kollaps schon bei jeder zweiten Rolle.
3. `web/src/navigation/useRoleAwareNavigation.ts:252-254` — die exportierte effektive Persona wird auf `"generic"` umgebogen, sobald `hasMultipleRoles === true`. **Das ist der eigentliche Kollisionspunkt.** Der Vorrang aus Schritt 1 wird hier verworfen. **Praktisch:** alle Konsumenten unterhalb sehen `"generic"`, nicht die echte Vorrangs-Persona.
4. `web/src/auth/roleModel.ts:209-249` — `getDefaultRoute` hat einen zweiten, eigenstaendigen Mehrrollen-Override: bei `hasMultipleRoles` wird zwingend `/` gewaehlt; persona-spezifische Default-Routen (admin → `/admin/config`, manager → `/supervisor`, worker → `/tasks/my`) entfallen. **Praktisch:** der Login landet immer auf der Dashboard-Seite, die danach durch Schritt 3 leer ist.

#### Sicht-Konsumenten der `generic`-Persona

| Stelle | heutige Verzweigung auf Persona | was bei `generic` ausfaellt |
| --- | --- | --- |
| `web/src/components/dashboard/DashboardOverview.tsx:17-23, 82-218` | `isAdminDashboard = persona === "admin"`; `supportsProcessTypeFilter = persona ∈ {hr, manager, reader}`; Manager-Mitarbeitendenliste an `persona === "manager"` | gesamter `DashboardAdminOverview`-Block (Zone 1–4 inkl. Admin-Warnungen, Operations, Runtime-Health-Block); Prozesstyp-Filter in Zone 1; Manager-Mitarbeitendenliste; Stats/Queue ohnehin leer (siehe Insights-Lader) |
| `web/src/components/dashboard/dashboardInsights.ts:24-42` und `dashboardInsightsLoaders.ts:619-628` (`loadGenericInsights`) | `switch (persona)` in `loadDashboardInsights`; `default` ruft `loadGenericInsights` | komplett leerer Insights-Datensatz: keine Stats, keine Queue, kein NextStep ausser „Freigegebenen Bereich waehlen.". Es findet **keine Aggregation** ueber HR/Manager/Worker statt — das ist eine bewusste Leerstelle, kein Bug |
| `web/src/pages/DashboardPage.tsx:11-31` | `dashboardPersona === "admin"` schaltet `pageDescription` und blendet den Top-Right-Button „Neuen Vorgang anlegen" aus | bei Mehrrollen wird die Nicht-Admin-Beschreibung gezeigt und der Anlege-Button erscheint, sobald `canCreateWorkflow` true ist — die Seitenkopf-Aussage ist dann fachlich falsch fuer einen Admin+X-User |
| `web/src/services/queries/dashboardQueries.ts:7-18` und `web/src/services/queryKeys.ts:50-54` | Query-Key enthaelt die effektive Persona | bei Mehrrollen-Login wird der Cache-Schluessel `["dashboard", "insights", "generic", null]` gesetzt; der Query-Layer transportiert also weiter die effektive Persona. **Wichtig fuer Z14-1.2:** der Key ist persona-, nicht rollen-getrieben — sobald Z14-1.x einen Persona-Switcher einfuehrt, kommt jede Sicht in einen eigenen Cache-Eintrag, ohne dass der Key umgebaut werden muss |

#### Generic-Faelle und ihre Nutzerfolge

| Rollen-Konstellation | Persona aus `roleModel.ts` | effektive Persona im Dashboard | spuerbarer Effekt fuer den Nutzer |
| --- | --- | --- | --- |
| nur admin | `admin` | `admin` | Admin-Betriebsblock sichtbar |
| nur hr | `hr` | `hr` | HR-Engpaesse, aktive Vorgaenge, Prozesstyp-Filter sichtbar |
| nur manager | `manager` | `manager` | Freigaben + Mitarbeitendenliste + Filter sichtbar |
| nur worker | `worker` | `worker` | offene eigene Aufgaben sichtbar |
| nur reader | `reader` | `reader` | Erfolgsquote / Lesemodus sichtbar |
| admin + hr/manager/worker/reader | `admin` | **`generic`** | Admin-Betriebsblock weg, kein Insight-Datensatz, keine Filter; auf der Startseite sieht der Power-User nur die Generic-Leerseite |
| hr + manager | `hr` | **`generic`** | weder HR-Engpaesse noch Manager-Freigaben sichtbar; Filter weg; statt dessen Generic-Leerseite |
| manager + worker | `manager` | **`generic`** | weder offene Freigaben als Manager noch eigene Aufgaben als Worker sichtbar |
| worker + reader | `worker` | **`generic`** | Worker-Aufgaben weg; Reader-Stats weg |

#### Was nicht kollabiert (wichtige Abgrenzung)

- `headerNavItems` und `dashboardActions` (`useRoleAwareNavigation.ts:212-247`) werden direkt aus den Capabilities (`hasHrRole`, `hasManagerRole`, `canManageAdminConfiguration`, …) gebaut — sie zeigen das **Vereinigungs-Set** der Aktionen, fuer die der Nutzer Rechte hat. Header und Schnellaktionen sind also **nicht** vom `generic`-Effekt betroffen.
- Routen-Guards in `App.tsx` und Feature-Checks via `canAccessFeature` arbeiten ebenfalls auf Capabilities, nicht auf Persona. Wer Rechte hat, kommt weiter durch — nur die Uebersicht ist leer.
- Rollen, Permissions und Capability-Flags selbst sind unabhaengig von der Persona-Ableitung. Z14 fasst keine Berechtigungen an.

#### Wo die Persona im Datenfluss weiter mitlaeuft (Vertragsanker fuer Z14-1.2)

- `dashboardInsights.ts:loadDashboardInsights(persona, options)` ist die zentrale Persona-Vertragsstelle: alle persona-spezifischen Loader (`loadAdminInsights`, `loadHrInsights`, `loadManagerInsights`, `loadWorkerInsights`, `loadViewerInsights`, `loadGenericInsights`) haengen hier dran. **Ein Persona-Switcher wuerde an genau diesem Vertrag andocken**, ohne die Loader zu beruehren.
- Der Query-Key (`queryKeys.dashboard.insights(persona, definitionKey)`) cached bereits pro Persona — eine Umschaltung wuerde automatisch verschiedene Eintraege halten und nicht ueber Bord werfen.
- `DashboardOverview` verwendet `dashboardPersona` an drei sichtbaren Stellen (Admin-Schalter, Filter-Schalter, Manager-Liste). Diese drei Schalter sind die kuenftigen Eingriffspunkte fuer einen Persona-Switcher; sie sind aktuell die einzigen persona-getriebenen Verzweigungen im Dashboard-Body.
- `DashboardPage` haengt nur an der Persona fuer Texte und einen optionalen Top-Right-Button — kein eigenes Datenmodell.

#### Zusammenfassung der Hebelstellen fuer Folgezyklen

- **Ein** Override-Punkt fuer die Sicht: `useRoleAwareNavigation.ts:253`.
- **Ein** Override-Punkt fuer das Login-Routing: `roleModel.ts:210`.
- **Vier** Sicht-Konsumenten der effektiven Persona: `DashboardOverview`, `DashboardPage`, `useDashboardInsights`/Query, Insight-Lader-Switch.
- **Null** Aktions-/Berechtigungs-Konsumenten — Header und Capabilities sind sauber getrennt.

Das ist der Anker fuer Z14-1.2: die Vertrags-/UX-Entscheidung muss fuer genau diese zwei Overrides eine Regel angeben, ohne die vier Sicht-Konsumenten umzubauen, und ohne die capability-getriebenen Aktionen zu beruehren.

**Nicht in Z14:**
- Backend-Aenderungen am Berechtigungsmodell — Rollen/Permissions bleiben unveraendert; Z14 betrifft die Ableitung der **Sicht**, nicht der **Rechte**.
- Neue Personas oder Spezialrollen einfuehren.
- Mobile-/Tablet-Layout fuer das Dashboard (R10 bleibt eigener Backlog-Eintrag).
- UI-Polish ausserhalb der Persona-/Sicht-Logik.

**Naechster Schritt:** Z14-1.1 — Inventur. Codex entscheidet ueber Modell/Effort und Reihenfolge.
