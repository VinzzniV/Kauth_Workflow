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

**Stand**: 2026-05-07 — Zyklus 14 abgeschlossen (Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung; Z14-1.1 Inventur done, Z14-1.2 Vertrags-/UX-Entscheidung done, Z14-1.3 Slice-Plan Folgezyklus done). Kein aktiver Zyklus. Naechster Folgezyklus: Codex entscheidet ueber Eroeffnung des Implementierungszyklus auf Basis von Z14-1.3. Die abgeschlossenen Detailzyklen 8 bis 13 wurden in `CODE_REVIEW_ARCHIVE.md` ausgelagert; diese Datei bleibt die kompakte aktive Steuerdatei.
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..03 Zyklus 2–5; 2026-05-03..04 Zyklus 6; 2026-05-05 Zyklus 7; 2026-05-05 Zyklus 8 abgeschlossen; 2026-05-05 Zyklus 9 abgeschlossen; 2026-05-05 Zyklus 10 abgeschlossen; 2026-05-05 Zyklus 11 eroeffnet) + Codex-Fallback (2026-05-05 Z11-F1 Abschluss waehrend Claude-Rate-Limit) + Claude (2026-05-06 Z11-F2 Abschluss; 2026-05-06 Z11-F3 Abschluss = Z11 vollstaendig geschlossen; 2026-05-06 Z12 eroeffnet + abgeschlossen; 2026-05-06 Z13 eroeffnet + abgeschlossen; 2026-05-07 Z14 eroeffnet + abgeschlossen).

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
| 14 | 2026-05-07 | Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung — abgeschlossen |

---

## Abgeschlossener Zyklus 14 — Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung

Eroeffnet 2026-05-07 als reiner Review-/Planungszyklus, abgeschlossen 2026-05-07. Ziel war: Problem sauber eroeffnen und schneiden, **keine Umsetzung**. Alle drei Slices done. Z14-1.3 liefert den Slice-Plan fuer den Folgezyklus; Codex entscheidet ueber dessen Eroeffnung.

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
| Z14-1.2 — Vertrags-/UX-Entscheidung: Begriffsklaerung Rolle vs. Persona vs. aktive Ansicht; Optionen fuer Mehrrollen-Behandlung skizzieren (z. B. Persona-Switcher mit Default + Persistenz; Aggregations-Persona statt `generic`; Admin-Vorrang fuer Admin+X; explizite Login-Auswahl); Pro/Contra je Option, ohne Festlegung; Vorgabe, was der Vertrag liefern muss (sichtbarer Schalter, Persistenz, Default-Regel, Fallback) | HIGH | done 2026-05-07 — Vertrag in `CODE_REVIEW.md` § Z14-1.2 (Begriffsraster Rolle/Persona/aktive Ansicht; vier Optionen mit Pro/Contra; Vorzugsrichtung „Persona-Switcher mit Vorrangs-Default + Persistenz, Admin-Vorrang fuer Admin+X als Default-Regel, Fallback `generic`"; Vertragspflichten Default/Persistenz/Fallback/Schalter/Login-Routing getrennt; Capability-Schicht und Routen-Guards explizit unberuehrt; Andock an `loadDashboardInsights`/Query-Key/`DashboardOverview`-Schalter beschrieben, ohne Implementierungsslice) |
| Z14-1.3 — Slice-Plan Folgezyklus: 2–3 sichere Umsetzungsslices mit Reihenfolge-Begruendung (typisch: Vertrag/Datenmodell zuerst, dann FE-Switcher, dann Aufraeumen der `generic`-Faelle in den abhaengigen Bloecken); ausdruecklich kein Code | HIGH | done 2026-05-07 — Slice-Plan in `CODE_REVIEW.md` § Z14-1.3 (drei Umsetzungsslices: I „aktive Ansicht" als reines Datenmodell + Persistenz + Fallback ohne Sicht-Konsumenten; II zwei Override-Stellen aus Z14-1.1 auf die neue Quelle umstellen; III sichtbarer Persona-Switcher nur fuer `hasMultipleRoles === true`; Reihenfolge Datenmodell → Routing/Sicht → UI-Polish; Modell/Effort pro Slice; Loader und drei `DashboardOverview`-Schalter bleiben unangetastet) |

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

### Z14-1.2 Kernergebnis — Vertrags-/UX-Entscheidung Mehrrollen-Behandlung

**Praktisch:** Z14-1.2 fixiert, **was** der kuenftige Mehrrollen-Vertrag im Frontend leisten muss, ohne ihn zu implementieren. Ein Power-User mit mehreren Rollen soll am Ende eine fachlich erwartete Standard-Ansicht sehen (z. B. Admin+HR landet auf Admin), bei Bedarf bewusst auf eine andere Sicht umschalten koennen, und diese Auswahl soll ihn ueber Logins hinweg begleiten — alles ohne dass die Berechtigungen, der Header oder die Schnellaktionen sich aendern.

**Lohnenswert:** Der Vertrag wird jetzt sauber definiert, weil die zwei Override-Punkte aus Z14-1.1 (`useRoleAwareNavigation.ts:253` Sicht-Kollaps; `roleModel.ts:210` Login-Routing) sonst gegenlaeufig leben und jede Persona-Erweiterung die `generic`-Falle vergroessert. Begriff und Vorgaben ein einziges Mal niederzuschreiben verhindert Doppel-Logik und macht den Folgeslice (Z14-1.3) sicher schneidbar.

**Nutzen:** klare Begriffstrennung Rolle / Persona / aktive Ansicht; eine bewusst gewaehlte Vorzugsrichtung als Vertragsentscheidung, nicht als Code-Vorgriff; benannte Andockpunkte (`loadDashboardInsights`, `queryKeys.dashboard.insights`, die drei `DashboardOverview`-Schalter), die in Z14-1.3 ohne neuen Vertragsdurchlauf benutzt werden koennen.

#### Begriffsraster (verbindlich fuer Folgezyklen)

- **Rolle**: persistente Berechtigungs-Zuordnung im Backend (`admin`, `hr`, `manager`, `worker`, `reader`). Liefert die **Capabilities**. Mehrere Rollen sind erlaubt und werden additiv zu einem Vereinigungs-Set an Capabilities verarbeitet (Header/Aktionen/Routen-Guards laufen direkt darauf). **Rollen sind nicht der Schalter, sondern das Recht.**
- **Persona**: aus den Rollen ableitbare **Standard-Sicht** fuer das Dashboard und persona-spezifische Insights. Heute bereits ueber die Vorrangskette `admin > hr > manager > worker > reader > generic` modelliert (`web/src/auth/roleModel.ts:151-161`). Eine Persona ist **kein** Recht, sondern eine voreingestellte Sicht-Auswahl. Mehrfachrollen erzeugen heute genau die `generic`-Kollision, weil die Vorrangsableitung im Sicht-Pfad ueberschrieben wird.
- **Aktive Ansicht**: das, was der Nutzer **gerade** sehen will. Sie ist von der Persona getrennt, weil sie pro Session vom Nutzer ueberschrieben werden koennen muss (Persona = Default, aktive Ansicht = aktuelle Wahl). Die aktive Ansicht steuert ausschliesslich Sicht-Konsumenten (Dashboard-Body, Insight-Lader, Cache-Schluessel, Seitenkopf-Texte), niemals Rechte oder Routen-Guards.

**Warum die drei Begriffe getrennt sein muessen:** Heute kollabieren Persona und aktive Ansicht in einer Variable (`dashboardPersona`), und die Mehrrollen-Regel ueberschreibt beide gleichzeitig auf `generic`. Solange die drei Konzepte nicht getrennt sind, kann ein Power-User nicht „Persona = admin, aktive Ansicht = manager" haben, ohne entweder seine Capabilities zu verlieren oder die Standard-Sicht aller anderen Mehrrollen-Nutzer mit zu veraendern. Die Trennung erlaubt: Capabilities bleiben rein rollen-/permissiongetrieben; Persona bleibt deterministisch aus Rollen ableitbar; aktive Ansicht ist eine **vom Nutzer beeinflussbare**, persistierte Praeferenz, die auf einem Default basiert und auf bekannte Werte begrenzt ist.

#### Optionen fuer die Mehrrollen-Behandlung

##### Option A — Persona-Switcher mit Default + Persistenz

Ein sichtbarer Schalter im Dashboard (z. B. Tab-Leiste oder Dropdown im Seitenkopf), mit dem der Nutzer aus den fuer ihn gueltigen Personas waehlen kann. Default kommt aus der bestehenden Vorrangskette (admin > hr > manager > worker > reader). Auswahl wird in `localStorage` (oder serverseitig in einer User-Setting-Spalte) persistiert und beim naechsten Login wiederverwendet, solange der Nutzer die zugehoerige Rolle weiterhin hat.

- **Pro:** loest die Mehrrollen-Falle ohne Berechtigungsumbau; macht die Persona explizit und nutzerseitig steuerbar; baut auf bestehenden Persona-Andockpunkten (`loadDashboardInsights(persona)`, persona-getriebene Query-Keys, drei `DashboardOverview`-Schalter); jede neue Persona dockt automatisch an, sobald sie in die Loader-Tabelle eingetragen wird; trennt sauber Persona (Default) und aktive Ansicht (Auswahl).
- **Contra:** zusaetzliche UI-Flaeche; Persistenz-Edgefaelle (Rolle wird entzogen → Auswahl nicht mehr gueltig → Fallback noetig); Risiko, dass Nutzer die Auswahl vergessen und „falsche Sicht" als Bug melden; Persistenzort (Client vs. Server) ist eine separate Vertragsentscheidung.

##### Option B — Aggregations-Persona statt `generic`

Anstatt bei Mehrrollen auf `generic` zu kollabieren, wird eine **kombinierte** Sicht ausgeliefert, die die Insight-Bloecke aller relevanten Rollen zusammenfuehrt (z. B. Admin+HR sieht Admin-Betriebsblock + HR-Engpaesse untereinander). Die `loadGenericInsights`-Leerstelle wird durch einen Aggregations-Loader ersetzt, der die einzelnen Persona-Loader aufruft und ihre Datensaetze zu einem grossen Datensatz zusammensetzt.

- **Pro:** kein zusaetzlicher UI-Schalter notwendig; Power-User sehen *alle* fuer sie relevanten Bloecke; Persistenz entfaellt; semantisch ehrlichste Sicht („Du hast alle diese Zustaendigkeiten, also siehst Du sie alle").
- **Contra:** lange, ueberfrachtete Dashboards bei drei oder vier Rollen; Insight-Bloecke wurden nicht fuer Co-Existenz entworfen (Stats-Kacheln, Filter, Manager-Mitarbeitendenliste konkurrieren um den Seitenkopf); jede neue Persona zwingt den Aggregations-Loader zu einer neuen Reihenfolge-/Gewichtungs-Entscheidung; loest das Login-Routing-Problem nicht (Fallback bleibt notwendig); macht das Mental Model wieder unklar — der Nutzer hat *eine* Seite, aber keine *eine* Sicht.

##### Option C — Admin-Vorrang fuer Admin+X

Der Sicht-Kollaps auf `generic` wird gezielt fuer Admin+X aufgehoben: hat der Nutzer die Admin-Rolle, gewinnt sie unabhaengig von weiteren Rollen, und sowohl Sicht (`useRoleAwareNavigation.ts:253`) als auch Login-Routing (`roleModel.ts:210`) liefern `admin`. Andere Mehrrollen-Konstellationen (HR+Manager, Manager+Worker, …) bleiben heute wie heute (oder fallen kontrolliert auf die hoechste Vorrangs-Persona zurueck).

- **Pro:** kleinster denkbarer Eingriff; trifft die haeufigste reale Konstellation (Admin, der nebenbei Fachzustaendigkeit hat); braucht keine UI-Aenderung und keine Persistenz; entspricht dem heutigen Mental Model „Admin ist erstrangig".
- **Contra:** lost nur Admin+X, nicht HR+Manager oder Manager+Worker; ein HR+Manager-User bleibt auf der Generic-Leerseite; verlagert das Problem statt es vertraglich zu loesen; macht die Persona-Ableitung asymmetrisch (Vorrang ja, aber nur fuer Admin); verschiebt den Tag, an dem Optionen A oder B trotzdem gebaut werden muessen.

##### Option D — Explizite Login-Auswahl

Der Nutzer waehlt beim Login (oder beim ersten Dashboard-Aufruf einer Session) explizit die gewuenschte Persona aus. Die Auswahl gilt nur fuer die Session und wird nicht persistiert (oder optional ueber „Diese Auswahl merken").

- **Pro:** maximale Klarheit, Nutzer trifft die Entscheidung bewusst; macht die Mehrrollen-Situation als solche sichtbar; vermeidet stille Defaults.
- **Contra:** Reibung bei jedem Login; nervt Single-Role-Nutzer, falls die Auswahl auch dort erscheint (Sonderfall noetig); ohne Persistenz unbequem fuer Power-User; mit Persistenz liegt die UX praktisch wieder bei Option A; fuer Login-Routing (`roleModel.ts:210`) braucht es trotzdem eine Default-Regel, falls die Auswahl noch nicht passiert ist.

#### Vorzugsrichtung (Vertragsentscheidung, kein Code-Slice)

Vorzugsrichtung fuer den Folgezyklus: **Option A (Persona-Switcher mit Vorrangs-Default + Persistenz)** als tragende Entscheidung, ergaenzt um **Option C (Admin-Vorrang fuer Admin+X) als Default-Regel** und einer harten **Fallback-Regel auf `generic`** fuer den Fall, dass eine persistierte Auswahl nicht mehr zu den aktuellen Rollen passt.

Begruendung: Die zwei Override-Punkte aus Z14-1.1 koennen nur sauber aufgehoben werden, wenn Persona (Default) und aktive Ansicht (Auswahl) getrennt sind — das leistet Option A als einzige der vier. Option B ueberlaedt das Dashboard und loest das Routing-Problem nicht. Option C deckt die haeufigste Konstellation ab und passt natuerlich als Default-Regel in einen Switcher. Option D bringt ohne Persistenz mehr Reibung als Nutzen und kollabiert mit Persistenz auf Option A.

**Diese Vorzugsrichtung ist eine Vertragsentscheidung, kein Implementierungsfreigabe.** Die Umsetzung wird in Z14-1.3 als Slice-Plan geschnitten. Codex kann die Vorzugsrichtung dort nochmal bestaetigen oder kippen, ohne dass der Vertrag selbst neu geschrieben werden muss.

#### Was der Vertrag liefern muss

Unabhaengig von der finalen Implementierung muss der Mehrrollen-Vertrag folgende Punkte beantworten — und zwar **getrennt** fuer die zwei Override-Punkte aus Z14-1.1:

1. **Sichtbarer Schalter vs. bewusste Nicht-Sichtbarkeit:** Der Vertrag muss explizit benennen, ob ein UI-Element zur Auswahl der aktiven Ansicht existiert. Vorzugsrichtung A: ja, sichtbar im Dashboard-Bereich, nur fuer Nutzer mit `hasMultipleRoles === true`. Single-Role-Nutzer sehen keinen Schalter (kein Bedarf, keine Reibung).
2. **Default-Regel (Standard-Persona):** Welche Persona ist die Standard-Sicht beim ersten Login eines Mehrrollen-Nutzers? Vorzugsrichtung: Vorrangskette aus `roleModel.ts:151-161` bleibt die Quelle des Defaults; `admin > hr > manager > worker > reader > generic`. Damit erbt Admin+X automatisch Admin als Default (Option C als Default-Regel).
3. **Persistenz-Regel:** Wo wird die zuletzt aktive Ansicht gespeichert, und wie lange gilt sie?
   - Speicherort: `localStorage` als erste Stufe (kein Backend-Aenderungsbedarf, kein Auth-/Sync-Risiko); spaeter optional in eine User-Setting-Spalte heben, wenn ein Cross-Device-Bedarf entsteht. Diese Hebung ist explizit **nicht** Teil von Z14, sondern bewusst spaeter.
   - Schluessel: per Person-/User-ID gebunden (`kauth.activeView.<personId>`), damit Shared-Browser-Faelle nicht ueber Nutzer hinweg lecken.
   - Lebensdauer: bis aktiv geaendert oder bis Fallback greift (siehe naechster Punkt).
4. **Fallback-Regel:** Was passiert, wenn eine persistierte Auswahl nicht mehr gueltig ist (Rolle entzogen, Persona umbenannt, Fremd-Wert)? Vorzugsrichtung: harter Fallback auf den **aktuellen Default** aus der Vorrangskette; falls auch der nicht gueltig ist, harter Fallback auf `generic`. `generic` bleibt im Vertrag erhalten als **letzter Notnagel**, nicht als regulaerer Mehrrollen-Zustand. Ein einmal-Logging-Hinweis im FE ist optional (kein Toast, kein Modal — Z14 fasst keine UI-Polish-Themen an).
5. **Verhalten Login-Routing vs. Dashboard-Sicht (getrennt!):** Beide Override-Punkte aus Z14-1.1 muessen unabhaengig versorgt werden:
   - **Dashboard-Sicht** (`useRoleAwareNavigation.ts:253`): liest die *aktive Ansicht* (Persistenz → Default → Fallback) statt heute hart `generic` zu erzwingen.
   - **Login-Routing** (`roleModel.ts:210`): liest dieselbe *aktive Ansicht*, um die persona-spezifische Default-Route (admin → `/admin/config`, manager → `/supervisor`, worker → `/tasks/my`) auch fuer Mehrrollen-Nutzer zu liefern. Wenn aktiv = `generic`, faellt das Routing weiterhin auf `/`. Der Vertrag muss explizit sagen: ein Persona-Switcher reicht **nicht automatisch** auch fuer das Routing — das Routing muss aus derselben Quelle lesen, nicht aus der eigenen Vorrangskette zweiter Ordnung.
6. **Was der Vertrag *nicht* anfasst (verbindlich):**
   - **Rechte/Capabilities**: `permissions`, `capabilities`, `hasHrRole`, `hasManagerRole`, `canManageAdminConfiguration`, `canAccessFeature` etc. bleiben unveraendert. Das Vereinigungs-Set bleibt das Vereinigungs-Set.
   - **Header-Navigation und Schnellaktionen** (`headerNavItems`, `dashboardActions`): bleiben capability-getrieben. Der Switcher veraendert keine Aktion und blendet keine Aktion aus.
   - **Routen-Guards** in `App.tsx` und Feature-Checks: bleiben capability-getrieben. Wer keine Admin-Rolle hat, kommt nicht in den Admin-Bereich, auch wenn er „aktive Ansicht = admin" persistiert haette — diesen Pfad gibt es per Vertrag nicht (siehe Persistenz: nur Personas, fuer die der Nutzer die Rolle besitzt).
   - **Backend-Berechtigungsmodell**: Z14 ist Frontend-Vertrag. Keine API-, Repo-, Service- oder DB-Aenderungen.

#### Andocken an die bestehenden Persona-Anker (Vertrag, nicht Implementierung)

Der Vertrag muss sich an genau drei bekannten Stellen anbinden, ohne sie zu zerlegen — dieser Abschnitt ist die Vorgabe fuer den Implementierungs-Slice (Z14-1.3) und **nicht** dessen Umsetzung:

- **`loadDashboardInsights(persona, options)`** (`web/src/components/dashboard/dashboardInsights.ts:24-42`): bleibt die **zentrale Persona-Vertragsstelle**. Aufrufer uebergeben statt der heutigen effektiven Persona die *aktive Ansicht*. Die Loader (`loadAdminInsights`, `loadHrInsights`, `loadManagerInsights`, `loadWorkerInsights`, `loadViewerInsights`, `loadGenericInsights`) bleiben **unveraendert**. `loadGenericInsights` bleibt als Fallback-Pfad erhalten (siehe Punkt 4).
- **`queryKeys.dashboard.insights(persona, definitionKey)`** (`web/src/services/queryKeys.ts:50-54`) **+** `useDashboardInsightsQuery` (`web/src/services/queries/dashboardQueries.ts:7-18`): der Query-Key ist heute schon persona-getrieben. Vertraglich uebergibt der Aufrufer die *aktive Ansicht* an den Hook; jede Sicht behaelt einen eigenen Cache-Eintrag, und ein Wechsel der aktiven Ansicht zieht keinen Cache-Reset nach sich, sondern haelt mehrere Sichten parallel warm. **Wichtig:** der Key-Aufbau aendert sich nicht — Z14-1.3 darf hier nichts umbauen.
- **Drei `DashboardOverview`-Schalter** (`web/src/components/dashboard/DashboardOverview.tsx:17-23, 82-218`): `isAdminDashboard = persona === "admin"`, `supportsProcessTypeFilter = persona ∈ {hr, manager, reader}`, Manager-Mitarbeitendenliste an `persona === "manager"`. Vertraglich werden diese drei Schalter mit der *aktiven Ansicht* gespeist statt mit der heutigen effektiven Persona. **Keine Logik-Aenderung in diesen drei Schaltern.** Der Vertrag verbietet ausdruecklich, die drei Bedingungen zu vereinheitlichen oder zu generalisieren — sie bleiben drei einzelne Verzweigungen, der einzige Wechsel ist die Quelle (aktive Ansicht statt persona).

`DashboardPage` (`web/src/pages/DashboardPage.tsx:11-31`) liest die aktive Ansicht ebenfalls fuer die zwei textgetriebenen Stellen (Seitenkopf-Beschreibung, optionaler „Neuen Vorgang anlegen"-Button). Hier ist nichts neu — nur die Quelle wechselt.

**Was Z14-1.3 daraus konkret zu schneiden hat (Vertrag, nicht Vorgriff):** zuerst das Datenmodell der aktiven Ansicht (Hook + Persistenz + Fallback) bauen, ohne Sicht-Konsumenten zu beruehren; dann die zwei Override-Stellen aus Z14-1.1 von `generic` auf die neue Quelle umstellen; dann den sichtbaren Switcher anbinden. Der genaue Schnitt entscheidet Z14-1.3.

**Nicht in Z14:**
- Backend-Aenderungen am Berechtigungsmodell — Rollen/Permissions bleiben unveraendert; Z14 betrifft die Ableitung der **Sicht**, nicht der **Rechte**.
- Neue Personas oder Spezialrollen einfuehren.
- Mobile-/Tablet-Layout fuer das Dashboard (R10 bleibt eigener Backlog-Eintrag).
- UI-Polish ausserhalb der Persona-/Sicht-Logik.

### Z14-1.3 Kernergebnis — Slice-Plan Folgezyklus

**Praktisch:** Z14-1.3 schneidet den Vertrag aus Z14-1.2 in drei kleine, unabhaengig verifizierbare Umsetzungsschritte. Ein Mehrrollen-Nutzer sieht nach Slice II bereits seine fachlich erwartete Default-Sicht zurueck; Slice III hebt die Auswahl in Nutzerhand. Slice I ist reine Datenmodell-/Persistenz-Arbeit ohne Sicht-Auswirkung — der erste Slice ist also sicher kommittierbar, ohne dass das UI sich aendert.

**Lohnenswert:** Drei kleine Slices statt ein grosser UI-Umbau halten den Tree zu jedem Zeitpunkt freigabefaehig. Wenn Slice III aus Kapazitaetsgruenden verschoben werden muss, hat der Nutzer trotzdem schon die korrekte Default-Sicht aus Slice I + II — der Power-User-Effekt der `generic`-Falle ist dann bereits weg, der Switcher ist Komfort. Das ist genau die Trennung Persona (Default) vs. aktive Ansicht (Auswahl) aus Z14-1.2: Slice I + II versorgen den Default, Slice III die Auswahl.

**Nutzen:** klarer Reihenfolge-Vertrag fuer den Folgezyklus; explizite Andockpunkte je Slice ohne neuen Vertragsdurchlauf; pro Slice ein eigenes Modell-/Effort-Profil, sodass Codex die Slices einzeln per CLI mit `--model`/`--effort` erzwingen kann.

#### Reihenfolge-Begruendung (Datenmodell → Routing/Sicht → UI)

- **Datenmodell zuerst.** Wer das Datenmodell zuletzt baut, biegt entweder den Switcher um oder Override-Stellen mehrfach an. Der Hook fuer die *aktive Ansicht* ist die einzige Quelle, die danach beide Override-Stellen aus Z14-1.1 (`useRoleAwareNavigation.ts:253` Sicht-Kollaps, `roleModel.ts:210` Login-Routing) versorgt — wenn er existiert, sind die zwei Folge-Slices reine Adapter-Arbeit. Z14-1.2 verbietet ausdruecklich Logik-Aenderungen an Loadern und an den drei `DashboardOverview`-Schaltern; ein Datenmodell-only-Slice respektiert das natuerlich.
- **Routing/Sicht in der Mitte.** Sobald der Hook steht, wechseln genau zwei Codestellen ihre Quelle: der Mehrrollen-Override im Navigation-Hook und der Mehrrollen-Override in `getDefaultRoute`. Beide muessen aus derselben Quelle lesen — Z14-1.2 hat das als Vertragspflicht festgehalten, weil ein Persona-Switcher allein das Login-Routing nicht heilt. Diese zwei Stellen zusammen in einem Slice zu machen verhindert, dass Sicht und Routing auseinanderlaufen.
- **UI zuletzt.** Der Switcher bedeutet sichtbare UX-Entscheidungen (Dropdown vs. Tabs, Wording, Position im Seitenkopf, Accessibility). Wenn Slice III spaeter kommt, kann der UX-Teil isoliert getestet werden, ohne Routing- oder Persistenz-Risiken zu schultern. Vor dem Switcher ist das System bereits korrekt — der Switcher ist Komfort, kein Korrekturpfad.

**Warum sicherer als ein grosser UI-Umbau:** Ein Komplett-Slice „Persona-Switcher mit Persistenz, Routing und Sicht in einem" kombiniert vier Risikoachsen (Persistenz, SSR-/Hydration-Verhalten, Routing-Regression, UI-/A11y-Polish) in einem Pull-Request, und jeder Fehler in einer Achse zieht das ganze Paket zurueck. Der hier vorgeschlagene Schnitt isoliert jede Achse: Slice I hat **null** Sicht-Konsumenten (purer Hook + Tests), Slice II hat **null** UI-Aenderungen (nur Quellen-Wechsel an zwei Codezeilen), Slice III hat **null** Routing-/Persistenz-Aenderungen (UI plus Schreibpfad in den bestehenden Hook). Jeder Slice ist fuer sich allein freigabefaehig und reversibel.

#### Slice I — „Aktive Ansicht" als reines Datenmodell + Persistenz + Fallback

**Praktisch passiert:** Ein neuer FE-Hook (Arbeitstitel `useActiveView`) liefert die *aktive Ansicht* aus drei Quellen in dieser Kaskade: persistierter Wert aus `localStorage` (Schluessel `kauth.activeView.<personId>`) → Default aus der bestehenden Vorrangskette `admin > hr > manager > worker > reader > generic` (`web/src/auth/roleModel.ts:151-161`) → harter Fallback `generic`. Der Hook validiert die persistierte Auswahl gegen die aktuell vorhandenen Capabilities (Persona ist nur waehlbar, wenn der Nutzer die zugehoerige Rolle hat). Er stellt zusaetzlich die Liste der gueltigen Optionen und einen Setter (`setActiveView`) bereit. **Keine Sicht-Konsumenten werden umgestellt; weder `useRoleAwareNavigation.ts:253` noch `roleModel.ts:210` werden in diesem Slice angefasst.**

**Warum der Zuschnitt sinnvoll ist:** Reine Datenmodell-Arbeit ohne sichtbare Wirkung. Der Tree bleibt nach Commit unauffaellig, der Hook ist allein per Vitest verifizierbar, und der naechste Slice findet eine fertige, getestete Quelle vor.

**Was dadurch besser wird:** ein einziger, dokumentierter Vertrags-Adapter zwischen Persona (Default) und aktiver Ansicht (Auswahl); klare Persistenz-Grenzen mit person-/user-gebundenem Schluessel; vorhersagbarer Fallback in allen Edge-Cases (Rolle entzogen, Fremdwert, fehlender Storage).

**Betroffene Dateien / Bereiche:** neuer Hook unter `web/src/auth/` oder `web/src/navigation/` (genaue Platzierung Implementer-Entscheidung im Slice); zugehoerige Vitest-Datei. **Nicht angefasst:** `useRoleAwareNavigation.ts`, `roleModel.ts:209-249`, `DashboardOverview.tsx`, `DashboardPage.tsx`, `dashboardInsights.ts`, `dashboardInsightsLoaders.ts`, `dashboardQueries.ts`, `queryKeys.ts`.

**Testfokus:** Vitest-Unit fuer (1) Default aus Vorrangskette bei leerem Storage, (2) persistierter Wert wird gelesen, wenn er zu aktuellen Capabilities passt, (3) Fallback auf Default, wenn persistierter Wert nicht mehr zu Rollen passt, (4) Fallback auf `generic`, wenn auch Default nicht gueltig ist, (5) Setter persistiert und liefert beim naechsten Render den neuen Wert, (6) Single-Role-Nutzer bekommt deterministisch genau eine Option (kein Schalter-Bedarf), (7) `personId`-Wechsel liest aus dem neuen Schluessel. Keine Integrations-/E2E-Tests notwendig.

**Doku-Folgen:** `MEMORY.md` (aktiver Zyklus / naechster Schritt aktualisieren), `CODEX_SYNC.md` (Eintrag), `KauthWorkflow/Stand/Code-Review-Status.md` (spiegeln). Kein `web/README.md` (kein UI-Effekt). Kein Vault-Begriffseintrag noetig — Begriffsraster steht bereits in Z14-1.2.

**Was bewusst NICHT in Slice I gehoert:**
- **kein UI-Element** (kein Dropdown, keine Tab-Leiste, kein Toast).
- **keine** Aenderung an den zwei Override-Stellen aus Z14-1.1 — Slice I darf den `generic`-Kollaps nicht entfernen, sonst aendert sich die Sicht ohne dass Slice II die Quelle versorgt.
- **keine** Aenderung an Loadern (`loadAdminInsights`, `loadHrInsights`, …) oder am Insight-Lader-Switch.
- **keine** Aenderung an `queryKeys.dashboard.insights` oder `useDashboardInsightsQuery` — der Query-Key bleibt persona-getrieben.
- **keine** Aenderung an den drei `DashboardOverview`-Schaltern (`isAdminDashboard`, `supportsProcessTypeFilter`, Manager-Liste).
- **kein** Backend, **keine** API-Erweiterung, **keine** Server-Persistenz. Cross-Device-Hebung bleibt explizit ausserhalb Z14.
- **keine** Telemetrie/Logs fuer Persona-Wechsel — das ist ein eigenstaendiger Folgepunkt, nicht Teil des Vertrages.

**Empfohlenes Modell/Effort:** `claude-sonnet-4-6` mit `--effort medium`. Begruendung: kleiner, klar abgegrenzter Hook mit engem Vertrag und vollstaendig per Unit-Tests verifizierbar; keine Architekturentscheidung mehr offen; kein UI; keine breite Code-Streuung. Opus-Reasoning ist hier Overhead.

#### Slice II — Zwei Override-Stellen aus Z14-1.1 auf die neue Quelle umstellen

**Praktisch passiert:** Genau zwei Codezeilen wechseln ihre Quelle. (1) `web/src/navigation/useRoleAwareNavigation.ts:253` liest die *aktive Ansicht* aus Slice I statt bei `hasMultipleRoles === true` hart `generic` zu setzen. (2) `web/src/auth/roleModel.ts:210` liest dieselbe Quelle, sodass Login-Routing fuer Mehrrollen-Nutzer auf die persona-spezifische Default-Route faellt (admin → `/admin/config`, manager → `/supervisor`, worker → `/tasks/my`); aktiv = `generic` faellt weiter sauber auf `/`. Die vier Sicht-Konsumenten (`DashboardOverview`, `DashboardPage`, `useDashboardInsights`/Query, Insight-Lader-Switch) bekommen automatisch ueber den vorhandenen Persona-Pfad die richtige Quelle; ihre Schalter-Logik wird **nicht** angefasst. `loadGenericInsights` bleibt als Fallback-Pfad erhalten.

**Warum der Zuschnitt sinnvoll ist:** Beide Override-Stellen aus Z14-1.1 muessen aus derselben Quelle gespeist werden — Z14-1.2 hat das als Vertragspflicht festgehalten, weil ein Switcher allein das Login-Routing nicht heilt. Sie zusammen in einem Slice umzustellen verhindert, dass Sicht und Routing auseinanderlaufen, und macht den Power-User-Effekt sichtbar: nach diesem Slice landet ein Admin+HR-Login wieder auf `/admin/config` mit dem Admin-Betriebsblock, ein HR+Manager-Login auf `/` mit der HR-Sicht.

**Was dadurch besser wird:** Mehrrollen-Nutzer sehen die fachlich erwartete Default-Sicht. Die Falle aus Z14-1.1 ist beseitigt, ohne dass eine UI-Flaeche dafuer existieren muss. Die Vertragspflicht „beide Overrides lesen aus derselben Quelle" ist nachweisbar erfuellt.

**Betroffene Dateien / Bereiche:** `web/src/navigation/useRoleAwareNavigation.ts:252-254`, `web/src/auth/roleModel.ts:208-249`, evtl. eine kleine Adapter-Stelle in `useCurrentUser` oder Aequivalent, falls der Hook aus Slice I dort konsumiert werden muss. Tests: `web/tests/roleModel.test.ts` (vorhanden) erweitern; ggf. neue `useRoleAwareNavigation.test.ts` ergaenzen.

**Testfokus:** Vitest-Unit fuer (1) `getDefaultRoute` mit Mehrrollen-Konstellationen — admin+hr → `/admin/config`, hr+manager → `/`, manager+worker → `/`, persistiertes `manager` fuer admin+manager → `/supervisor`, persistiertes `admin` ohne Admin-Rolle → Fallback auf Default-Route der hoechsten verfuegbaren Persona; (2) `useRoleAwareNavigation` — bei Mehrrollen wird die Default-Persona aus der Vorrangskette zurueckgegeben statt `generic`, persistierter Wert wird respektiert, ungueltiger persistierter Wert faellt zurueck. Browser-Smoke-Test optional fuer Login als Admin+HR und Login als HR+Manager.

**Doku-Folgen:** `MEMORY.md`, `CODEX_SYNC.md`, `KauthWorkflow/Stand/Code-Review-Status.md` spiegeln. `web/README.md` falls dort die Mehrrollen-Erwartung beschrieben ist (Codex pruefen).

**Empfohlenes Modell/Effort:** `claude-sonnet-4-6` mit `--effort medium`. Begruendung: kleiner Touch in zwei Codestellen, klares Vertragsergebnis, Tests gegen `roleModel`-Pfade gut isoliert. Opus nur dann sinnvoll, wenn Codex zusaetzlich den Single-Source-Adapter (Hook-Konsum) breiter durchziehen will.

#### Slice III — Sichtbarer Persona-Switcher fuer Mehrrollen-Nutzer

**Praktisch passiert:** Eine sichtbare Auswahlkomponente (Dropdown oder kleine Tab-Leiste, Detail Implementer-Entscheidung) wird im Dashboard-Body — bzw. an einem in der UX-Skizze festgelegten Anker im Seitenkopf — fuer Nutzer mit `hasMultipleRoles === true` gerendert. Die Komponente listet die fuer den Nutzer gueltigen Personas (aus Slice I) und schreibt die Auswahl ueber `setActiveView` in den Hook. Single-Role-Nutzer sehen den Schalter nicht.

**Warum der Zuschnitt sinnvoll ist:** Reines UI-Polish nach abgeschlossenem Datenmodell + Routing. Wenn Slice III verschoben werden muss, ist das System nach Slice I + II trotzdem korrekt — der Nutzer sieht den richtigen Default, kann ihn nur noch nicht selbst wechseln. Der Slice ist isoliert auf eine Komponente plus Styles/A11y und ohne Routing- oder Persistenz-Risiko.

**Was dadurch besser wird:** der Mehrrollen-Nutzer kann die Sicht bewusst wechseln, ohne den Default zu verlieren; Persistenz wirkt fuer ihn jetzt nutzbar; das Mental Model „Persona = Default, aktive Ansicht = Auswahl" wird sichtbar.

**Betroffene Dateien / Bereiche:** neue Komponente unter `web/src/components/dashboard/` (z. B. `DashboardActiveViewSwitcher.tsx`); Einbindung in `DashboardOverview.tsx` und/oder `DashboardPage.tsx` an einem klar abgegrenzten Anker — **ohne** die drei `DashboardOverview`-Schalter zu beruehren. Tests: neue Vitest-Komponententests.

**Testfokus:** Komponententest fuer (1) Schalter erscheint nur bei Mehrfachrollen, (2) Optionen entsprechen den Capabilities, (3) Auswahl persistiert und triggert die Neu-Renderung der Sicht-Konsumenten via Slice I, (4) Fallback bei entzogener Rolle (Auswahl fliegt aus den Optionen, Default greift), (5) Tastatur-Erreichbarkeit. Browser-Smoke-Test fuer Schalter-Roundtrip empfohlen.

**Doku-Folgen:** `web/README.md` (neuer Dashboard-Schalter), `MEMORY.md`, `CODEX_SYNC.md`, `KauthWorkflow/Stand/Code-Review-Status.md`. Falls UX-Wording „Sicht" / „aktive Ansicht" stabilisiert wird, Vault-Eintrag in `KauthWorkflow/Domäne/` erwaegen.

**Empfohlenes Modell/Effort:** `claude-sonnet-4-6` mit `--effort medium`. Begruendung: UI-Slice mit klaren Vertrags-Andockpunkten und engen Tests; keine Architekturentscheidung. Wenn Codex eine breite UX-Skizze (Wording, Position, Tabs vs. Dropdown, A11y-Pattern) gemeinsam entscheiden will, Eskalation auf `claude-opus-4-7` mit `--effort medium` rechtfertigbar — als Default reicht Sonnet.

#### Risiken pro Slice (knapp)

- **Slice I:** SSR-/Hydration-Mismatch durch synchrones `localStorage`-Lesen. Mitigation: Persistenz-Lesen Client-only, Default beim Erststand, dann nachladen. Edge-Case Shared-Browser ist durch person-/user-gebundenen Schluessel abgedeckt.
- **Slice II:** Login-Routing-Test fuer Single-Role-Nutzer darf nicht regressieren — bestehende `roleModel.test.ts` muss um die Mehrrollen-Pfade erweitert werden, nicht ersetzt. Risiko, dass `useRoleAwareNavigation` ohne Provider bei Tests den Hook nicht aufloesen kann; Mitigation: Wrapper im Test-Setup.
- **Slice III:** A11y-Pattern und Wording sind UX-Entscheidungen, kein Architekturthema; Codex sollte vor dem Start kurz das Layout (Dropdown vs. Tab-Leiste) und das Wording festziehen, damit Slice III nicht in Iterationen abrutscht.

#### Zusammenspiel mit zyklusuebergreifenden offenen Punkten

- R10 (Mobile-/Tablet-Layout fuer Form-Editor) bleibt eigenstaendig — der Switcher in Slice III ist Dashboard-Layout, nicht Form-Editor.
- Z8-3.2/#8 bleibt deferred und ist von Z14 unabhaengig.
- Persistenz-Hebung in eine Backend-Spalte (Cross-Device-Sicht) bleibt explizit ausserhalb des Folgezyklus — wird erst geplant, wenn ein konkreter Bedarf entsteht.

**Naechster Schritt:** Zyklus 14 vollstaendig abgeschlossen (Z14-1.1 + Z14-1.2 + Z14-1.3 done, kein Code-Change in Z14). Codex eroeffnet im Folgezyklus (Arbeitstitel Zyklus 15) Slice I auf Basis dieses Plans und erzwingt Modell/Effort per CLI-Flag.
