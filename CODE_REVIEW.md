# Code Review — kauth_workflow

## Zweck

- aktive Review-Priorisierung (Technik + Produkt/UX)
- Begruendung fuer den naechsten Arbeitszyklus
- kompakter Status fuer Mensch und KI

## Primaerquelle fuer

- aktuellen Review-Fokus
- Reihenfolge der Nacharbeit
- offene zyklusuebergreifende Befunde

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

**Stand**: 2026-05-12 — **Z21 eroeffnet** (Produkt-/Funktions-/UX-Review). Z20 und Z19 bleiben am 2026-05-11 vollstaendig abgeschlossen. Detailhistorie aller Backend-Zyklen liegt in `CODE_REVIEW_ARCHIVE.md`.

**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..06 Z2–Z13; 2026-05-07 Z14; 2026-05-08 Z15–Z18 + A1/A2/A3/B/C; 2026-05-11 Z19 vollstaendig + Z20-S1/B1; 2026-05-12 Z21 Produkt-/UX-Review). Codex (2026-05-11 Z20-B2/B3/B4 Abschluss).

---

## Aktuelle Gesamtbewertung

### Backend / Datenmodell / Skalierbarkeit (Z19-Stand)

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| Backend-Architektur | **A-** | Lifecycle-Service ist echte Commit-Grenze, Repository-Monolith reduziert |
| Datenbankdesign | **A-** | Solides Schema, gute Constraints, Schema-Parity-Test gegen `db/manual/` |
| Auth & Berechtigungen | **B+** | Permission-Audit hat Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | **B+** | RotationTaskRegenerationEngine als pure Domain-Engine; HQ5-Hooks getestet |
| Testbarkeit | **B** | Testcontainers + Integration-Tests; 566 Backend-Tests gruen |
| Skalierbarkeit | **B-** | Mehrere Listen-/Sweep-/Dispatch-Pfade weiter Kandidaten fuer SQL-Pushdown |
| Sicherheit | **B+** | `/client/log-events` rate-limited; dev-sim-Guard hard-throw |
| Lesbarkeit | **B+** | Konventionen durchgaengig; grobe Monolithen reduziert |

### Produkt / Funktion / UX (Z21-Stand, 2026-05-12)

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| **Automatisierung (Layer + Handler)** | **D** | Layer fachlich richtig, alle Handler Simulation — jetzt im UI klar markiert (Z21-S1); produktiv unverantwortlich bis echte Handler existieren |
| **Hybrid-AD-Faehigkeit (on-prem)** | **F** | Richtung entschieden (Z21-S2, 2026-05-12: Windows-Worker, AD on-prem fuehrt) — Implementation offen: kein Worker, kein LDAP/LDAPS-Adapter, `EntraGraphClient` weiter read-only |
| Workflow-Storno (laufende Vorgaenge) | **D** | Kein Cancel-Endpunkt, nur `archive` (completed) und `delete` (draft) — taeglicher Edge-Case nicht abgedeckt |
| Workflow-Builder (Conditions/Mappings) | **B-** | Canvas + DAG-Layout stark; Conditions haben Formularmodus, Mappings/technische Keys bleiben Power-User-lastig |
| Workflow-Detail (Panelauswahl) | **B-** | Sechs+ Panels untereinander, „was ist offen?" verteilt sich |
| Listen-Trennung Worker/Manager | **B-** | `/workflows`, `/tasks/my`, `/rotation/operations` ueberlappen fuer Mehrrollen-User |
| Mitarbeiter-/Personenverzeichnis | **B** | 360°-Akte stark; `directory_only` und echte Karten visuell zu eng gemischt |
| Dashboard / Persona-Switcher | **B** | Zone-Struktur klar; Persona-Switcher beschriftet, Wirkung „nur Ansicht, keine Rechteaenderung" aber nicht erklaert |
| Notification-/Mail-Konfig | **A-** | Microsoft-Graph-Versand real, Sandbox + Test-Senden; Admin-Warnungen vorhanden, Runtime-Fehler koennten noch sichtbarer sein |
| Frontend-Architektur | **B+** | Saubere Services/Queries-Schichten; AdminConfig-Bundle-Refactor; Builder-Refactor |
| Administration (Konfig-Breite) | **B+** | Breit + strukturiert; einzelne Einstiegstexte koennten klarer sein |
| Laufende Vorgaenge (Liste + Filter) | **A-** | Saved Views, Pagination, Karten-/Tabellenmodus, Split-Vorschau |
| Durchlaufplanung (Rotation) | **A-** | HR-Modus, Stations-Timeline, Kalender, Audit, Notifications |
| Meine Aufgaben (Worker-Inbox) | **A-** | Split-Detail, Counts, Kommentar-Drafts, Approval-Pfad |

---

## Aktiver Zyklus 21 — Produkt-/Funktions-/UX-Review (eroeffnet 2026-05-12)

**Praktisch:** Erster zusammenhaengender Produkt-Review nach Z19/Z20-Backend-Abschluss. Fokus auf reale Nutzbarkeit fuer Endbenutzer und Produktionsrisiken — nicht auf Codequalitaet oder Stil. Geprueft anhand von Code + Doku + lokalem Build; **API-Release-Build und Frontend-Build gruen**, Tests nicht ausgefuehrt, Browser-Verifikation offen.

**Lohnenswert:** Das technische Fundament steht — die naechste Frage ist nicht „ist der Code sauber" sondern „funktioniert das fuer einen normalen Benutzer und kann es produktiv betrieben werden". Genau diese Trennung war bisher nicht zusammenhaengend bewertet.

**Nutzen:** Klare Priorisierung, was den Schritt von „funktioniert im Dev-Modus" zu „produktiv verantwortbar" tatsaechlich blockiert. Trennung „fachlich vorgesehen / im Code vorbereitet / real lauffaehig / produktiv verantwortbar" macht falsche Erwartungen sichtbar.

### Z21-Findings nach Schwere

#### 🔴 P0 — Show-Stopper fuer Produktivnutzung

**Z21-P0-1 · Automation-Layer ist End-to-End nur eine Simulation, aber im UI nicht als solche markiert**

- Alle 5 registrierten Action-Handler (`CreateAdUser`, `CreateMailbox`, `AssignGroups`, `CreateErpEmployee`, `SendWelcomeMail`) erben von `SimulatedWorkflowAutomationActionHandler` und geben nur `simulated:true`-Objekte zurueck. Belege: `api/API/Services/SimulatedWorkflowAutomationHandlers.cs:5-80`, DI-Registrierung in `api/API/Extensions/LifecycleServiceCollectionExtensions.cs:154-158`.
- Im Action-Katalog (Seed + Prod-Bootstrap) ist als technische Bindung `simulated_directory`, `simulated_mailbox`, … eingetragen — auch fuer die produktionsnahe Bootstrap-DB. Belege: `db/02_dev_seed.sql:34-38`, `db/02_bootstrap.sql:28-32`.
- `EntraGraphClient` hat **ausschliesslich Lese-Operationen** (`LoadSecurityGroupsAsync`, `LoadGroupMembersAsync`). Kein `CreateUser`, kein `Update`, kein `AddMember`. Beleg: `api/API/Services/Directory/IEntraGraphClient.cs:31-37`.
- Im Builder-UI (`web/src/components/admin-config/WorkflowBuilderActionEditor.tsx:62-79`) sieht der Admin „Create AD User" oder „Send Welcome Mail" — **ohne sichtbaren Hinweis**, dass die Aktion eine Simulation ist. Nur die englische Beschreibung in der Detailansicht enthaelt das Wort „Simulated".

**Was bedeutet das praktisch?** Wenn ein Admin im Builder einen Onboarding-Workflow mit `CreateAdUser` + `SendWelcomeMail` baut und in Produktion startet, passiert in AD/Entra/Mail genau **nichts** — das Job-Log meldet aber „succeeded". Die Aufgaben wirken erledigt, das Konto existiert nicht. Bemerkt wird das erst, wenn ein neuer Mitarbeiter sich nicht anmelden kann.

**Warum lohnt es sich?** Falsche Erwartung an Automatisierung ist die schwerste Form von Bug — sie wird erst nach Tagen entdeckt. Bis dahin sind weitere Workflows durchgelaufen und das Vertrauen ist weg.

**Was wird besser?** Saubere Trennung „aktiv produktiv" vs. „nur Simulation" bewahrt vor Fehlbedienung. Entweder Simulation deutlich machen oder echte Handler liefern.

---

**Z21-P0-2 · Hybrid-AD-Frage: Im Projekt fehlt ein on-prem-AD-Schreibpfad** — ✅ Richtung entschieden 2026-05-12 (Z21-S2)

**Entscheidung:** Option 2 — AD on-prem fuehrt, schreibende Aktionen laufen ueber einen dedizierten Windows-Worker. `EntraGraphClient` bleibt read-only. Belegt in `KauthWorkflow/Architektur/Entscheidungen.md` Abschnitt „AD/Entra-Schreibrichtung" und `KauthWorkflow/Architektur/Migrationspfad.md` Etappe 9a. Guardrail in `PROJECT_CONTEXT.md` ergaenzt. Der Schreibpfad selbst (Worker-Skeleton, echter Handler) ist eigener Etappenpfad ausserhalb des Z21-Slice-Plans. Bis dahin bleibt der Automation-Layer im Simulationsmodus (Z21-S1-Banner aktiv).

Urspruengliche Befundlage:

- Es gibt **keinen** on-prem-AD-Adapter. Kein LDAP-Client, kein PowerShell-Worker, kein AD-PowerShell-Remoting-Aufruf, kein `System.DirectoryServices`-Code im Repo.
- Der Automation-Layer laeuft als Hosted-Service in der API (`KauthWorkflow/Betrieb/Setup.md`). Das ist auf einer Linux-VM in Containern. Linux kann grundsaetzlich per LDAP/LDAPS oder ueber einen separaten Worker mit on-prem AD sprechen; **dieses Projekt implementiert diesen Pfad aber nicht**:
  - kein Adapter fuer LDAP-Bind oder Kerberos zur on-prem-DC,
  - kein „Hybrid-Worker"-Konzept (Helper auf Windows-Server, der die Jobs ausfuehrt),
  - keine Queue-/Tunneling-Loesung Richtung interne AD-Server.
- Entra-Schreibpfade fehlen ebenfalls (siehe Z21-P0-1).

**Was bedeutet das praktisch?** Aus der aktuellen App kann **nichts** an on-prem AD veraendert werden. Das ist nicht „nur teilweise fertig", sondern im Repo nicht angelegt. „AD on-prem als fuehrende technische Aenderungsquelle" + „aus dieser App heraus konsistent aendern" ist im aktuellen Stand **nicht abbildbar**.

**Warum lohnt es sich?** Jede Implementierung ohne klare Architektur-Entscheidung wird teuer verworfen. Erst entscheiden, dann bauen.

**Was wird besser?** Klare Entscheidung zwischen den realistischen Optionen:
1. **Entra fuehrt, on-prem AD ist nachgefuehrt** ueber AD Connect → App schreibt nur gegen Graph. Linux-VM-Stack reicht. Realistisch in Wochen.
2. **AD on-prem fuehrt** → braucht eigenen Windows-Worker als zusaetzlichen Stack-Bestandteil. Groesserer Umbau, groessere Betriebskosten.
3. **Beidseitige Spiegelung** → praktisch nie wirklich „beides fuehrend".

Was fehlt konkret technisch fuer Option 2: schreibender Worker-Service (DB-Poll → AD-Operation), Authentisierung des Workers, sicherer Kanal von Linux-VM zum Worker, Idempotenz + Audit-Pflicht, UI-Trennung „live" vs. „Simulation".

---

**Z21-P0-3 · Aktive Workflow-Instanzen lassen sich nicht abbrechen**

Die Workflow-Lifecycle-API kennt nur:
- `POST /workflows/{uid}/archive` — nur fuer **completed**-Status erlaubt (`api/API/Endpoints/WorkflowEndpoints.cs:239-263`).
- `DELETE /workflows/{uid}` — nur fuer **draft**-Status erlaubt (`api/API/Endpoints/WorkflowEndpoints.cs:265-289`).
- Keinen Endpunkt fuer `cancel`, `abort` oder „stoppen". Grep nach `cancelWorkflow|cancel_workflow|cancelInstance` im `api/API`-Verzeichnis: 0 Treffer.

**Was bedeutet das praktisch?** Wenn HR aus Versehen ein Onboarding fuer die falsche Person startet, oder wenn ein Mitarbeiter doch nicht eintritt, gibt es keinen kontrollierten Weg, den laufenden Vorgang sauber zu beenden. Aufgaben bleiben offen, Benachrichtigungen feuern weiter, Statistiken werden verzerrt.

**Warum lohnt es sich?** Stornieren ist in jedem realen HR-Workflow taeglicher Edge-Case (Probearbeit abgesagt, Eintritt verschoben, falsche Stammdaten). Ohne Storno hilft sich der Betrieb mit Workarounds (Tasks einzeln abhaken, Person loeschen, Datenmuell) — die alle den Audit-Pfad zerstoeren.

**Was wird besser?** Expliziter Cancel-Endpunkt mit Grund-Pflichtfeld und sauberem Uebergang auf `cancelled`-Status macht den Audit-Trail vollstaendig und nimmt manuelle Reparatur-Workarounds aus dem Betrieb.

---

#### 🟠 P1 — Verdeckt Bedienprobleme oder Risiken

**Z21-P1-1 · `directory_only` und echte Mitarbeiterkarten im selben Listenbereich gemischt**

`web/src/pages/PeopleDirectoryPage.tsx:300-360` und `web/src/components/admin-config/AdminEntraImportSection.tsx` bieten zwei parallele Wege, einen Verzeichnis-Eintrag in eine echte Mitarbeiterakte zu konvertieren: Inline-Import in der Mitarbeiter-Liste und Bulk-Import im Admin.

**Was bedeutet das praktisch?** Ein Nutzer sieht teilweise „Personen" mit „Detail oeffnen"-Link, teilweise nur „Importieren"-Button (`directory_only`). Es ist nicht sofort klar, wann welche Aktion gemeint ist. Der Importpfad dedupliziert technisch ueber `directory_identity_id`, `employee_number` und `app_user_id`; das Hauptproblem ist deshalb eher UX-Verwirrung und vergessene Importe als massenhafte Duplikate.

**Warum lohnt es sich?** Personen-Anker ist der fachliche Primaeranker — Duplikate hier verzerren spaeter alle Workflows und Reports.

**Was wird besser?** Klare visuelle Trennung (eigene Sektion „Aus Entra noch nicht uebernommen") + ein einziger primaerer Import-Einstiegspunkt.

---

**Z21-P1-2 · Workflow-Builder ist fuer „Nicht-Entwickler" deklariert, Mappings bleiben aber technisch**

- Conditions haben bereits einen Formularmodus mit Antwortauswahl, Operator und Wert (`web/src/components/admin-config/WorkflowBuilderConditionEditor.tsx`). Der JSON-/Power-Modus ist nicht der Standard, bleibt aber sichtbar, sobald Rohdaten oder Spezialfaelle noetig sind.
- Action-Mappings zeigen direkt Backend-Property-Pfade wie `target_person.firstName`, `directory_identity.userPrincipalName`, `workflow.definitionKey` (`web/src/components/admin-config/WorkflowBuilderActionMappingEditor.tsx:14-41`).
- Section 1 „Stammdaten" hat unter „Technische Details" Felder wie `Definition-Schluessel` (technische Slug) sichtbar (`AdminWorkflowBuilderFormSection.tsx:610-642`).
- Connect-Mode (Verbindungen ziehen) braucht zwingend gesetzte `nodeKey`-Werte — Fehlermeldung „Der Quell-Schritt braucht einen Schritt-Key" begreift ein Fachanwender nicht ohne Erklaerung.

**Was bedeutet das praktisch?** Der Builder ist gut fuer einen geschulten Power-User. Fuer einen HR-Admin ohne technische Vorpraegung sind vor allem Mapping-Editor, technische Keys und Operator-Begriffe Stolperfallen; der Conditions-Bereich ist besser als urspruenglich bewertet, aber noch nicht komplett fachsprachlich.

**Warum lohnt es sich?** Die Zielarchitektur sagt explizit: „Ein Nicht-Entwickler soll fachliche Workflows aus sicheren Bausteinen konfigurieren koennen". Das Versprechen wird heute nur teilweise eingeloest.

**Was wird besser?** Mapping als „aus diesem Feld der Person" mit menschlich lesbarem Label, Condition-Wording naeher an „Wenn Antwort X = Wert Y" statt Operator + answerKey + expectedValue.

---

**Z21-P1-3 · Notification-Versand: Konfiguration ist real; Resthebel ist Runtime-Sichtbarkeit**

Mail-Versand ueber Microsoft Graph ist **echt** (`api/API/Services/GraphWorkflowEmailNotificationSender.cs:28-105`). Konfigurierbar in `Administration > System > System-Konfiguration`. Gut: `disabled`-Pfad, Sandbox-Redirect, Test-Versand, Audit im `system_event_log`.

Korrektur zur ersten Z21-Einschaetzung: Der Admin-Bereich zeigt bereits Statuskarten und Warnungen fuer unvollstaendige Mail-Konfiguration bzw. aktiven Versand ohne Sandbox. Das Speichern blockiert zudem `enabled=true`, wenn Pflichtwerte fehlen. Was bleibt: fehlgeschlagene Dispatches oder Runtime-Blockaden koennten im operativen Dashboard noch deutlicher als aktuelle Stoerung auftauchen.

**Was bedeutet das praktisch?** „Konfiguration vorhanden" heisst nicht automatisch „letzte Versandversuche liefen erfolgreich". Endbenutzer merken fehlende Mails spaet, wenn Runtime-Fehler nur im Log sichtbar bleiben.

**Warum lohnt es sich?** Workflows ohne Benachrichtigung blockieren menschliche Tasks — niemand weiss, dass er dran ist.

**Was wird besser?** Rotes Stat-Tile oder Health-Hinweis fuer aktuelle Versandfehler/blocked dispatches, nicht nur fuer statische Konfigurationsluecken.

---

**Z21-P1-4 · Persona-Switcher fuer Mehrrollen-User nur teilweise selbsterklaerend**

`web/src/components/dashboard/DashboardOverview.tsx:84-91` schaltet je nach `dashboardPersona` einen komplett anderen Bildschirm. `PersonaSwitcher` hat bereits sichtbare Beschriftung/ARIA („Ansicht wechseln", „Ansicht:"), erklaert aber nicht, dass nur die Dashboard-Ansicht und nicht die Rechte gewechselt werden.

**Was bedeutet das praktisch?** Mehrrollen-User (z. B. Admin + HR) sehen den Switcher und koennen sich vertippen — die Uebersicht passt dann nicht zur Erwartung. Risiko fuer Anrufe „warum sehe ich das Falsche?".

**Warum lohnt es sich?** Kleine Klarheit hier verhindert Support-Aufwand bei jeder Persona-Verwechslung.

**Was wird besser?** Beschriftung „Ansicht wechseln (nur Anzeige, keine Rechtsaenderung)" oder Visualisierung der aktiven Rolle als Badge.

---

**Z21-P1-5 · Linux-VM `dev`-Pfad braucht zusaetzliche Tools, `prod`-Pfad nicht**

`KauthWorkflow/Betrieb/Setup.md:189-197`: Fuer `./scripts/start-vm.sh dev` muessen `dotnet` und `npm` extra auf der VM installiert werden. `prod` laeuft komplett in Containern.

**Was bedeutet das praktisch?** Wer dachte, dass eine Linux-VM mit Docker reicht, laeuft beim Wechsel auf `dev` in eine Wand.

**Warum lohnt es sich?** Demo-/Test-Setups dauern unnoetig laenger.

**Was wird besser?** Vorab-Check `command -v dotnet` im Skript mit klarer Fehlermeldung — oder `dev`-Pfad, der dotnet/npm auch in Containern faehrt.

---

#### 🟡 P2 — Bedienkomfort und Konsistenz

**Z21-P2-1 · Drei sehr aehnliche Listenbereiche fuer Worker/Manager**

`web/src/navigation/useRoleAwareNavigation.ts:113-191`: Je nach Rollenmix bis zu fuenf Listen-Ansichten (`/workflows`, `/tasks/my`, `/rotation/operations`, `/supervisor`, `/people`) mit ueberlappender Logik. „Laufende Vorgaenge" und „Meine Aufgaben" sind aus Worker-Sicht nicht klar abgegrenzt.

**Was wird besser?** Klare Zwei-Klick-Regel — Manager/HR landen auf Workflows, Worker auf Aufgaben. Heute wirkt es wie „alle sehen alles, jeder darf alles filtern".

---

**Z21-P2-2 · Workflow-Detailseite: sechs+ Panels untereinander, Reihenfolge nicht offensichtlich**

`web/src/pages/WorkflowDetailPage.tsx` rendert `WorkflowHeaderPanel`, `WorkflowRequirementsPanel`, `WorkflowTaskAreasSection`, `WorkflowNotificationsPanel`, `WorkflowLinksPanel`, `WorkflowManagementPanel`, `WorkflowAuditLog`.

**Was wird besser?** Tabs („Status & Aufgaben" / „Anforderungen" / „Audit & Links") wie auf `PersonWorkflowHistoryPage`. Selbe Idee dort funktioniert, beim Workflow-Detail fehlt sie.

---

**Z21-P2-3 · Korrigiert: Builder-Dirtystate ist vorhanden; nur optional staerkerer Banner**

Die urspruengliche Einschaetzung war falsch positiv. `AdminWorkflowBuilderFormSection.tsx` zeigt bereits „Ungespeichert", hat `Speichern` und `Verwerfen`, und Publish ist bei ungespeicherten Aenderungen mit Tooltip gesperrt. `hasUnsavedChanges` wirkt nicht nur als Disable am Loeschen-Button.

**Was wird besser?** Optional koennte ein Page-Top-Banner den Zustand noch auffaelliger machen. Das ist Komfort, kein aktiver Fehler.

---

**Z21-P2-4 · Frontend-Bundle 623 KB Hauptchunk + 241 KB AdminConfigPage**

Output `npm run build`. Vite-Warning erscheint. Fuer internes Netz vertretbar, am Handy spuerbar.

**Was wird besser?** Bessere `manualChunks`-Konfiguration koennte den Index-Bundle weiter teilen.

---

**Z21-P2-5 · `tailwind.config.js` `extend: {}` leer, Tokens nur als CSS-Variablen**

Bewusst nicht migriert (siehe `FRONTEND_TODO.md:52`). Heute kein konkreter Schmerz; nur wenn jemand „mit Tailwind-Reflex" neu baut, fehlt der Theme-Bezug.

---

#### 🟢 P3 — Cosmetics / Detail

- **Z21-P3-1** · `PeopleDirectoryPage` hat viele harte Inline-Styles (`PeopleDirectoryPage.tsx:307-422`) — Theme-Drift-Risiko.
- **Z21-P3-2** · Builder-Wording schwankt zwischen „Schritt", „Knoten", „Baustein".
- **Z21-P3-3** · `R8` (Browser-Verifikation Form-Editor) und `R10` (Mobile-Layout) sind in `TODO.md` als offen markiert — explizit nicht code-pruefbar.

### Bereich-fuer-Bereich-Bewertung

| Bereich | Fachliche Funktion | UX-Verstaendlichkeit | UI-Qualitaet | Produktionsreif? |
|---|---|---|---|---|
| **Uebersicht** (Dashboard) | ✅ Solide, persona-spezifisch | 🟡 Persona-Switcher-Wirkung nur teilweise erklaert (P1-4) | ✅ Zone-Struktur | **Ja, mit Hinweis Persona-Switcher** |
| **Workflow-Builder** | ✅ Versionierte Definitions + DAG-Canvas | 🟡 Mappings/Keys technisch (P1-2) | ✅ Dirtystate/Speichern/Verwerfen vorhanden | **Teilweise — fuer Power-User ok, nicht fuer jeden Admin** |
| **Laufende Vorgaenge** | ✅ Saved-Views, Pagination, Vorschau-Split | ✅ Klar | ✅ Karten + Tabelle | **Ja** — aber kein Storno (P0-3) |
| **Workflow-Detail** | ✅ Header/Requirements/TaskAreas/… | 🟡 viele Panels untereinander (P2-2) | ✅ Saubere Komponenten | **Teilweise — Storno fehlt** |
| **Mitarbeiter** | ✅ HR-/Admin-Liste + 360°-Akte mit Tabs | 🟡 `directory_only` und Personen gemischt (P1-1) | 🟡 Inline-Styles (P3-1) | **Ja, kleinere UX-Klaerungen** |
| **Durchlaufplanung** | ✅ HR-Modus, Stations-Timeline, Audit | 🟡 `?mode=create` URL-versteckt | ✅ Kalender + Timeline | **Ja** |
| **Wechsel & Aufgaben** | ✅ Filter, Karten/Tabelle, IT-/eigene-Abt. | 🟡 Ueberlappung mit „Meine Aufgaben" (P2-1) | ✅ Strukturierte Sicht | **Ja** |
| **Meine Aufgaben** | ✅ Split-Detail, Kommentar-Drafts, Approval | ✅ Klar | ✅ Kompakt | **Ja** |
| **Administration** | ✅ Breit + strukturiert | 🟡 viele Sektionen | ✅ Workspace-Bundles | **Ja**, klarere Eingangstexte empfohlen |
| **Automatisierung** | 🔴 **NUR Simulation** | 🔴 nicht als Simulation gekennzeichnet (P0-1) | 🟡 UI vorhanden, aber technisch | **❌ NICHT produktionsreif** |

### Z21-Fazit Automation & Hybrid-AD

Trennung „fachlich vorgesehen / im Code vorbereitet / real lauffaehig / produktiv verantwortbar":

| Aspekt | Bewertung | Beleg |
|---|---|---|
| Fachlich vorgesehen | ✅ Ja — `action_definitions`, Mapping, Retry, Logging, Handler-Registry, Audit | `KauthWorkflow/Domäne/Automation.md`, `AutomationPropertyCatalog.cs` |
| Im Code vorbereitet | ✅ Datenmodell + Hosted Service + Job-Worker, Handler werden korrekt aufgeloest | `WorkflowAutomationService.cs`, `WorkflowAutomationHandlerRegistry.cs` |
| Real lauffaehig | 🟡 Ja, aber **nur als Simulation** | `SimulatedWorkflowAutomationHandlers.cs:76-80` |
| Produktiv verantwortbar | ❌ **Nein.** Admin glaubt, AD-User wird angelegt — tatsaechlich passiert nichts | siehe Z21-P0-1 |

**Schreibrichtung entschieden (Z21-S2, 2026-05-12):** AD on-prem fuehrt; schreibende Aktionen laufen ueber einen Windows-Worker (Migrationspfad-Etappe 9a). `EntraGraphClient` bleibt read-only — kein direkter Graph-Schreibpfad.

**Aus Linux-VM Richtung Entra/Graph (Cloud):** explizit verworfen als Schreibpfad. Read-only bleibt.

**Aus Linux-VM Richtung on-prem AD direkt:** weiterhin nicht abbildbar — Architektur sieht das auch nicht vor. Schreiben gehoert in den Windows-Worker.

### Z21-Verifikationsluecken

- **Browser-Verifikation**: Nicht ausgefuehrt — UI-Aussagen aus Code-Struktur und Klassen abgeleitet, nicht aus gerenderten Screens.
- **Tests**: Nicht ausgefuehrt — nur Builds (API Release + Vite). Letzter dokumentierter Stand: 566 Backend-Tests gruen (Z20).
- **DB-Lauf**: Kein lokaler PostgreSQL-Lauf — Storno-Befund nur ueber Endpunkt-Grep ausgeschlossen.
- **Entra-Live-Verifikation**: Mail-Versand-Pfad nicht mit echten Credentials getestet — Code-Pfad ist da.

### Z21-Naechste Schritte (priorisiert nach Endnutzer-/Produktionsnutzen)

1. **✅ Z21-S1 done (2026-05-12) · Simulation klar als Simulation markieren.** `ActionDefinitionDto.IsSimulated` (abgeleitet aus `handler_type LIKE 'simulated_%'`) im Backend. FE: „Simuliert"-Badge im `WorkflowBuilderActionEditor` neben jeder simulierten Aktion + Hinweis im Dropdown. Admin-Dashboard: Simulation-Hinweis-Banner im `AdminOverviewWorkspaceSection` mit Link zum Aktionskatalog. CSS: `.wf-action-sim-badge`, `.admin-sim-notice`. Tests: `ActionDefinitionDto_IsSimulated_DerivedFromHandlerType` (Theory, 6 Faelle).
2. **🔴 Storno fuer laufende Workflows einfuehren.** Neuer `POST /workflows/{uid}/cancel` mit Grund-Pflichtfeld, Uebergang in `cancelled`, Tasks automatisch `cancelled`, Notifications stoppen, Audit-Eintrag. UI-Button in `WorkflowManagementPanel` fuer HR/Admin bei `running`-Status.
3. **✅ Z21-S2 done (2026-05-12) · Hybrid-AD-Architekturentscheidung getroffen.** AD on-prem fuehrt; schreibende Lifecycle-Aktionen laufen ueber einen dedizierten Windows-Worker, `EntraGraphClient` bleibt read-only. Doku in `KauthWorkflow/Architektur/Entscheidungen.md` (Abschnitt „AD/Entra-Schreibrichtung") + `KauthWorkflow/Architektur/Migrationspfad.md` (Etappe 9a mit 4-Schritte-Zielbild) + `PROJECT_CONTEXT.md` (Guardrail). Sub-Entscheidungen (Deployment / Transport / Schreibmechanik / Auth / Audit) bewusst offen — eigener Plan-Mode-Slice vor Code-Arbeit.
4. **🟠 Builder fachsprachlicher machen.** Mapping-Editor mit menschlich lesbaren Labels, Condition-Wording weniger technisch, technische Keys nur im Power-Modus.
5. **🟡 Workflow-Detail- und Listen-Ergonomie auflockern.** Tabs auf Workflow-Detail, klarere Listen-Trennung Worker/Manager, Runtime-Sichtbarkeit fuer blockierte/fehlgeschlagene Mail-Dispatches.

---

## Archivstatus

- Die Detailzyklen **Z8 bis Z20** liegen in `CODE_REVIEW_ARCHIVE.md`.
- Die Detailhistorie von **Zyklus 7** liegt in `CODE_REVIEW_ARCHIVE.md` und `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`.
- In dieser aktiven Datei bleiben nur Gesamtbewertung, aktiver Z21-Zyklus, offene zyklusuebergreifende Befunde und die grobe Historie.

---

## Offene Befunde aus frueheren Zyklen

| ID | Aufgabe | Status | Quelle |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | offen — Nutzer-Aufgabe, KI kann nicht pruefen | L7 |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | backlog — kein konkreter Bedarf | L7 |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | deferred — wartet auf Produkt-Entscheidung | Zyklus 1 |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | deferred — admin-getriggert, kein Hot-Path | Zyklus 8 |

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
| 10 | 2026-05-05 | Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege — abgeschlossen |
| 11 | 2026-05-05..06 | Admin-/Master-Data-Listen-Vertraege — abgeschlossen |
| 12 | 2026-05-06 | Admin-Dashboard-Betriebsblock fuer Runtime-/System-Health — abgeschlossen |
| 13 | 2026-05-06 | Echte Linux-Host-/VM-Metriken im Admin-Runtime-Health-Block — abgeschlossen |
| 14 | 2026-05-07 | Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation — abgeschlossen |
| 15 | 2026-05-08 | Implementierung Mehrrollen-Persona — vollstaendig abgeschlossen |
| 16 | 2026-05-08 | Mitarbeiterakte als eigener Navigationsbereich + Identity-/Permission-Vertrag — vollstaendig abgeschlossen |
| 17 | 2026-05-08 | Light/Dark-Mode-Theme-Leaks — abgeschlossen |
| 18 | 2026-05-08 | Frontend Full Review — vollstaendig abgeschlossen |
| A1–C | 2026-05-08 | People-Import, Entra-Stellen-Import, Mitarbeiterkarten-Felder, Verzeichnis-Sicht — abgeschlossen |
| 19 | 2026-05-11 | Backend Full Review / Holistic Audit — vollstaendig abgeschlossen |
| 20 | 2026-05-11 | Admin/Directory/Runtime Read Contracts Phase 2 — vollstaendig abgeschlossen |
| **21** | **2026-05-12** | **Produkt-/Funktions-/UX-Review — eroeffnet** |
