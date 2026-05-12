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

Bei zyklusuebergreifend offenen Befunden reicht ein kurzer Hinweis, warum sie aktuell nicht angegangen werden. Diese Regel ist auch in `CLAUDE_CONTROL.md` als Arbeits-Pflicht verankert.

---

**Stand**: 2026-05-12 — **Z21 PROD_TODO-Slice-Plan (S1..S6) abgeschlossen**. Offene Resthebel (P0-1 Automation real, P1-2 AND/OR-Mehrbedingungen, P1-3 Mail-Dispatch-Health, P2-3..P3-x) bleiben hier sichtbar. Detail-Belege zu Z21-S1..S6 sind in `CODE_REVIEW_ARCHIVE.md` (Abschnitt „Zyklus 21 (Done-Findings)").

---

## Aktuelle Gesamtbewertung

### Backend / Datenmodell / Skalierbarkeit (Z19-Stand)

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| Backend-Architektur | **A-** | Lifecycle-Service als Commit-Grenze, Repository-Monolith reduziert |
| Datenbankdesign | **A-** | Solides Schema, Schema-Parity-Test gegen `db/manual/` |
| Auth & Berechtigungen | **B+** | Permission-Audit mit Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | **B+** | RotationTaskRegenerationEngine als pure Domain-Engine |
| Testbarkeit | **B** | Testcontainers + Integration-Tests; 566 Backend-Tests gruen |
| Skalierbarkeit | **B-** | Listen-/Sweep-/Dispatch-Pfade weiter Kandidaten fuer SQL-Pushdown |
| Sicherheit | **B+** | `/client/log-events` rate-limited; dev-sim-Guard hard-throw |
| Lesbarkeit | **B+** | Konventionen durchgaengig; grobe Monolithen reduziert |

### Produkt / Funktion / UX (Z21-Stand)

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| **Automatisierung (Layer + Handler)** | **D** | Layer fachlich richtig, alle Handler Simulation — im UI klar markiert (Z21-S1); produktiv unverantwortlich bis echte Handler existieren |
| **Hybrid-AD-Faehigkeit (on-prem)** | **F** | Richtung entschieden (Z21-S2: Windows-Worker, AD on-prem fuehrt) — Implementation offen |
| Workflow-Storno | **A-** | `POST /workflows/{uid}/cancel` mit Pflicht-Grund + Audit (Z21-S3) |
| Workflow-Builder | **B** | Mapping-Labels + Wording „Schritt" (Z21-S5); AND/OR-Mehrbedingungen offen |
| Workflow-Detail | **A-** | Drei Tabs mit persistentem Header (Z21-S4) |
| Listen-Trennung Worker/Manager | **B** | Worker-Persona sieht Aufgaben vor Workflows (Z21-S4) |
| Mitarbeiter-/Personenverzeichnis | **B+** | `directory_only` in eigener Sektion (Z21-S4) |
| Dashboard / Persona-Switcher | **B+** | Hinweistext „Nur Anzeige – keine Rechteaenderung" (Z21-S4) |
| Notification-/Mail-Konfig | **A-** | Microsoft-Graph-Versand real; Resthebel Runtime-Sichtbarkeit blockierter Dispatches |
| Frontend-Architektur | **B+** | Saubere Services/Queries-Schichten; Builder-Refactor |
| Administration | **B+** | Breit + strukturiert |
| Laufende Vorgaenge | **A-** | Saved Views, Pagination, Split-Vorschau |
| Durchlaufplanung | **A-** | HR-Modus, Stations-Timeline, Audit |
| Meine Aufgaben | **A-** | Split-Detail, Counts, Approval-Pfad |

---

## Offene Z21-Findings

#### 🔴 P0

**Z21-P0-1 · Automation-Layer ist End-to-End nur Simulation**

Sichtbare Markierung erledigt (Z21-S1). Echte Handler fehlen weiter — alle 5 registrierten Action-Handler erben von `SimulatedWorkflowAutomationActionHandler`, `EntraGraphClient` ist read-only.

**Praktisch:** Workflows mit `CreateAdUser` + `SendWelcomeMail` laufen „succeeded" durch, ohne dass in AD/Entra/Mail etwas passiert. Heute durch die „Simuliert"-Badge und das Admin-Banner sichtbar, aber nicht behoben.

**Warum lohnt es sich:** Falsche Erwartung an Automatisierung ist die schwerste Form von Bug.

**Was wird besser:** Erst mit echtem Schreibpfad aus Z21-S2 (Windows-Worker / Migrationspfad-Etappe 9a) verschwindet das Risiko. Bis dahin bleibt der Banner aktiv.

**Z21-P0-2 · Hybrid-AD-Schreibpfad fehlt** — Richtung entschieden (Z21-S2), Implementation offen

Entscheidung: AD on-prem fuehrt, Windows-Worker schreibt. Doku in `KauthWorkflow/Architektur/Entscheidungen.md` + `Migrationspfad.md` Etappe 9a + `PROJECT_CONTEXT.md`-Guardrail. Detail im Archiv.

**Resthebel:** Worker-Skeleton, Transport API↔Worker, AD-Schreibmechanik, Domaen-Authentisierung, Audit-Rueckkanal. Eigener Etappenpfad ausserhalb des Z21-Slice-Plans.

---

#### 🟠 P1

**Z21-P1-2 · Workflow-Builder fachsprachlicher — Resthebel**

UX-Teil erledigt (Z21-S5: Mapping-Labels mit Fach-/Technik-Optgroups, Wording „Schritt"). Offen:

- 🟡 **AND/OR-Mehrbedingungen am Decision-Edge** — `WorkflowRuntimeEngine.ParseDecisionCondition` akzeptiert heute strikt eine Bedingung. Aufgenommen als Z21-S5b (PROD_TODO) / Z21-S6b (TODO.md). Runtime-Schema-Erweiterung mit Rueckwaertskompat zur Single-Form.
- 🟡 **Stammdaten „Technische Details"** — Feld `Definition-Schluessel` (Slug) ist in Section 1 weiter sichtbar (`AdminWorkflowBuilderFormSection.tsx:610-642`). Bewusst nicht im UX-Slice, weil Schreibpfad-Thema (Slug-Editierbarkeit nach Erst-Anlage).

**Z21-P1-3 · Mail-Dispatch-Health: Runtime-Sichtbarkeit fehlt**

Konfiguration und Versandpfad sind real und mit Warnungen versehen (Z21-S1 hat das Konfig-Stueck mit abgedeckt). Was fehlt: rotes Stat-Tile oder Health-Hinweis fuer aktuell fehlgeschlagene Dispatches im operativen Dashboard, nicht nur fuer statische Konfigluecken. Aufgenommen als `Z21-S5` in `TODO.md`.

**Praktisch:** Endbenutzer merken fehlende Mails spaet, wenn Runtime-Fehler nur im Log sichtbar bleiben.

**Was wird besser:** Sichtbarkeit fehlgeschlagener/blockierter Dispatches im Admin-Dashboard ohne Logsuche.

---

#### 🟡 P2

- **Z21-P2-3** · Builder-Dirtystate ist vorhanden (Ungespeichert/Speichern/Verwerfen + Publish-Sperre). Optional staerkerer Page-Top-Banner — Komfort, kein Fehler.
- **Z21-P2-4** · Frontend-Bundle 624 KB Hauptchunk + 242 KB AdminConfigPage. Fuer internes Netz vertretbar, am Handy spuerbar. Bessere `manualChunks` waeren moeglich.
- **Z21-P2-5** · `tailwind.config.js` `extend: {}` leer, Tokens nur als CSS-Variablen. Bewusst nicht migriert (siehe `FRONTEND_TODO.md`). Kein konkreter Schmerz.

---

#### 🟢 P3

- **Z21-P3-1** · `PeopleDirectoryPage` Inline-Styles (`PeopleDirectoryPage.tsx:307-422`) — Theme-Drift-Risiko, Backlog.
- **Z21-P3-3** · `R8` (Browser-Verifikation Form-Editor) + `R10` (Mobile-Layout) — Nutzer-Aufgaben, nicht code-pruefbar.

---

## Z21-Status nach Bereich (Kurzversion)

| Bereich | Funktion | UX | Produktionsreif? |
|---|---|---|---|
| **Uebersicht** (Dashboard) | ✅ | ✅ (Z21-S4) | Ja |
| **Workflow-Builder** | ✅ | 🟢 Labels + Wording (Z21-S5); AND/OR offen | Ja fuer Mappings; AND/OR-Folge offen |
| **Laufende Vorgaenge** | ✅ + Storno (Z21-S3) | ✅ | Ja |
| **Workflow-Detail** | ✅ + Storno (Z21-S3) | ✅ (Z21-S4) | Ja |
| **Mitarbeiter** | ✅ | ✅ (Z21-S4); Inline-Styles offen (P3-1) | Ja |
| **Durchlaufplanung** | ✅ | 🟡 `?mode=create` URL-versteckt | Ja |
| **Wechsel & Aufgaben** | ✅ | ✅ (Z21-S4) | Ja |
| **Meine Aufgaben** | ✅ | ✅ | Ja |
| **Administration** | ✅ | 🟡 viele Sektionen | Ja, klarere Eingangstexte empfohlen |
| **Automatisierung** | 🔴 nur Simulation | ✅ als Simulation markiert (Z21-S1) | ❌ bis echter Schreibpfad existiert |

### Z21-Fazit Automation & Hybrid-AD

Trennung „fachlich vorgesehen / im Code vorbereitet / real lauffaehig / produktiv verantwortbar":

| Aspekt | Bewertung | Beleg |
|---|---|---|
| Fachlich vorgesehen | ✅ | `KauthWorkflow/Domäne/Automation.md`, `AutomationPropertyCatalog.cs` |
| Im Code vorbereitet | ✅ | `WorkflowAutomationService.cs`, `WorkflowAutomationHandlerRegistry.cs` |
| Real lauffaehig | 🟡 nur als Simulation | `SimulatedWorkflowAutomationHandlers.cs:76-80` |
| Produktiv verantwortbar | ❌ | siehe Z21-P0-1 |

**Schreibrichtung:** AD on-prem fuehrt; schreibende Aktionen ueber Windows-Worker (Migrationspfad-Etappe 9a). `EntraGraphClient` bleibt read-only.

### Z21-Verifikationsluecken

- **Browser-Verifikation**: nicht ausgefuehrt — UI-Aussagen aus Code abgeleitet.
- **Tests**: nur Builds + Slice-spezifische Test-Suites ausgefuehrt; voller Backend-Lauf zuletzt Z20 (566 gruen).
- **DB-Lauf**: kein lokaler PostgreSQL-Lauf in Z21.
- **Entra-Live-Verifikation**: Mail-Versand-Pfad nicht mit echten Credentials getestet.

---

## Archivstatus

- Detailzyklen **Z8 bis Z20** + **Z21 Done-Findings (Z21-S1..S6)** liegen in `CODE_REVIEW_ARCHIVE.md`.
- Detailhistorie **Zyklus 7** in `CODE_REVIEW_ARCHIVE.md` + `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`.
- In dieser Datei bleiben nur Gesamtbewertung, offene Z21-Findings, offene zyklusuebergreifende Befunde und grobe Historie.

---

## Offene Befunde aus frueheren Zyklen

| ID | Aufgabe | Status | Quelle |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | offen — Nutzer-Aufgabe | L7 |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | backlog — kein konkreter Bedarf | L7 |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | deferred — wartet auf Produkt-Entscheidung | Zyklus 1 |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | deferred — admin-getriggert, kein Hot-Path | Zyklus 8 |

---

## Zyklus-Historie

| Zyklus | Datum | Hauptthema |
|--------|-------|------------|
| 1–18 | 2026-04-23 .. 2026-05-08 | Code-Review/Hardening, HQ/LQ, Test-Coverage, Naming, Legacy-Abbau, Runtime-Lifecycle, Skalierbarkeit, Mehrrollen-Persona, Mitarbeiterakte, Theme-Leaks, Frontend Full Review |
| 19 | 2026-05-11 | Backend Full Review / Holistic Audit — abgeschlossen |
| 20 | 2026-05-11 | Admin/Directory/Runtime Read Contracts Phase 2 — abgeschlossen |
| **21** | **2026-05-12** | Produkt-/Funktions-/UX-Review — PROD_TODO-Slice-Plan abgeschlossen; Reste in offenen Findings + Folge-Slices |
