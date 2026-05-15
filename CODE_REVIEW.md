# Code Review — kauth_workflow

## Zweck

- aktive Review-Priorisierung (Technik + Produkt/UX)
- Begruendung fuer den naechsten Arbeitszyklus
- kompakter Status fuer Mensch und KI

## Nicht verwenden fuer

- kurzfristige Session-Notizen → `MEMORY.md`
- tiefes Slice-fuer-Slice-History-Studium abgeschlossener Zyklen → `CODE_REVIEW_ARCHIVE.md`

## Verwandte Dateien

- `TODO.md` — aktive Backlog-Punkte (Etappe 9a + Admin-Gated-Automation Slices 1..6 alle erledigt; einzig offen `CreateErpEmployee` stakeholder-blockiert)
- `PROD_TODO.md` — abgeschlossener Slice-Plan + Mapping zwischen den Z21-Nummerierungen
- `MEMORY.md` — aktueller Fokus
- `CODE_REVIEW_ARCHIVE.md` — vollstaendige Detail-Historie
- `KauthWorkflow/Stand/Code-Review-Status.md`
- `KauthWorkflow/Architektur/Migrationspfad.md` — Etappe 9a-Detail

---

## Schreibregel (verbindlich)

Jedes Review-Finding und jeder Slice muss neben dem technischen Befund kurz erklaeren:

- **Was bedeutet das praktisch?** — was ein normaler Leser im Alltag merkt.
- **Warum lohnt es sich, das anzugehen?** — der konkrete Anlass oder das Risiko.
- **Was wird dadurch besser, sicherer, schneller oder wartbarer?** — der erwartete Nutzen.

Diese Regel ist auch in `CLAUDE_CONTROL.md` als Arbeits-Pflicht verankert.

---

**Stand 2026-05-15** — Z21 + Migrationspfad-Etappe 9a Schritt 1..8 + Admin-Gated-Automation Slices 1..6 alle durch. Reale Onboarding-Pipeline `CreateAdUserLdaps → CreateMailboxGraph → (AlreadyExists-Decision) → AssignGroupsLdaps → SendWelcomeMailGraph` produktionsreif (vor Live-Rollout: Browser-Smoke + Graph-/Mail-Live-Verifikation). Admin-Gated-Approval-UI (Plan-Vorschau, Re-Auth, Bundle-Dialog mit Auto-Complete) ist nutzbar; vier Folge-Slices bewusst offen (echtes Entra-Re-Auth, Live-Log, 360°-Karte-Aggregator, Referenzuser-Mapping). Aktiver Resthebel: nur noch `CreateErpEmployee` (InforLN, stakeholder-blockiert) plus kleinere P-Findings (P1-2-Sub „Definition-Schluessel"-Slug, P2-3..P2-5, P3-3). Detail-Historie aller acht Etappen + sechs Slices in `CODE_REVIEW_ARCHIVE.md`.

---

## Aktuelle Gesamtbewertung

### Backend / Datenmodell / Skalierbarkeit (Z19-Stand)

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| Backend-Architektur | **A-** | Lifecycle-Service als Commit-Grenze, Repository-Monolith reduziert |
| Datenbankdesign | **A-** | Solides Schema, Schema-Parity-Test gegen `db/manual/` |
| Auth & Berechtigungen | **B+** | Permission-Audit mit Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | **A-** | Plan-Activate erfordert Stationen + kein Aktiv-Konflikt (Z21-S7) |
| Testbarkeit | **B** | Testcontainers + Integration-Tests; 566 Backend-Tests gruen (Z20-Stand) |
| Skalierbarkeit | **B-** | Listen-/Sweep-/Dispatch-Pfade weiter Kandidaten fuer SQL-Pushdown |
| Sicherheit | **B+** | `/client/log-events` rate-limited; dev-sim-Guard hard-throw |
| Lesbarkeit | **B+** | Konventionen durchgaengig; grobe Monolithen reduziert |

### Produkt / Funktion / UX (Z21-Stand 2026-05-12)

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| **Automatisierung (Layer + Handler)** | **D** | Layer fachlich richtig, alle Handler Simulation — UI-Markierung (Z21-S1) + Runtime-Fail-Sicht (TODO Z21-S5) erledigt; produktiv unverantwortlich bis echte Handler existieren |
| **Hybrid-AD-Faehigkeit (on-prem)** | **F** | Richtung entschieden (Z21-S2: Windows-Worker, AD on-prem fuehrt) — Implementation offen (Migrationspfad-Etappe 9a) |
| Workflow-Storno | **A-** | `POST /workflows/{uid}/cancel` + Pflicht-Grund + Audit (Z21-S3); Storno-Banner im Detail-Header (Z21-S8) |
| Workflow-Builder | **B+** | Mapping-Labels + Wording (Z21-S5); AND/OR-Mehrbedingungen am Decision-Edge (Z21-S6b) |
| Workflow-Detail | **A-** | Drei Tabs + persistenter Header + Storno-Banner (Z21-S4 + S8) |
| Listen-Trennung Worker/Manager | **B** | Worker-Persona sieht Aufgaben vor Workflows (Z21-S4) |
| Mitarbeiter-/Personenverzeichnis | **A-** | `directory_only` eigene Sektion (Z21-S4); Inline-Styles auf CSS-Klassen (Z21-S9) |
| Dashboard / Persona-Switcher | **B+** | Hinweistext „Nur Anzeige" (Z21-S4) |
| Notification-/Mail-Konfig | **A** | Microsoft-Graph real; Runtime-Failures als Stat-Tiles (TODO Z21-S5) |
| Durchlaufplanung | **A** | Activate-Pfad erfordert Stationen (Z21-S7); HR-Modus, Timeline, Audit |
| Frontend-Architektur | **B+** | Saubere Services/Queries-Schichten; Builder-Refactor |
| Administration | **B+** | Breit + strukturiert |
| Laufende Vorgaenge | **A-** | Saved Views, Pagination, Split-Vorschau |
| Meine Aufgaben | **A-** | Split-Detail, Counts, Approval-Pfad |

---

## Offene Findings

#### 🔴 P0

**Z21-P0-1 · Automation-Layer ist End-to-End nur Simulation** — gelöst durch Etappe 9a Schritt 3..7. Vier produktive Handler (`CreateAdUserLdaps`, `AssignGroupsLdaps`, `CreateMailboxGraph`, `SendWelcomeMailGraph`) parallel zu den simulierten Bestands-Handlern; UI markiert Simulationen via `IsSimulated`-Badge automatisch. Details siehe `CODE_REVIEW_ARCHIVE.md` § „Migrationspfad-Etappe 9a".

**Z21-P0-2 · Hybrid-AD-Schreibpfad fehlt** — gelöst durch Etappe 9a Schritt 1..8 (Sub-Architektur, Worker, LDAPS, gMSA, DPAPI, Vault, Mailbox-Graph, AlreadyExists-Branch im Engine). Verbleibender Code-Slice: **`CreateErpEmployee`** als eigener Backend-Architektur-Slice (Ziel: InforLN), zurueckgestellt bis Stakeholder-Wunsch.

**Admin-Gated-Automation Slices 1..6 (2026-05-13 .. 2026-05-15)** — Plan-Vorschau (WhatIf), Task-Automation-Binding, Approval-Endpoint mit Soft-Re-Auth, Builder-UI, Approval-Runtime-Dialog mit Bundle-Stepper. Details + Folge-Slice-Liste (echtes Entra-Re-Auth, Live-Log, 360°-Karte, Referenzuser-Mapping) in `CODE_REVIEW_ARCHIVE.md` § „Admin-Gated-Automation Slices 1-6".

**Optionale offene Resthebel (kein blocker):** Builder-UI-Erweiterung fuer `automation_output`-Bedingungen (Source-Dropdown, `ConditionProperties`-Filter); `RemoveMailboxLicense` fuer User-Deprovisionierung; Lizenz-Pool-Monitoring; Sub-License-Disabling; Connection-Pooling im LDAPS-Writer-Pfad; Vault-Cleanup-Sweeper; Key-Rotation.

---

#### 🟠 P1

**Z21-P1-2-Rest · Stammdaten „Technische Details"** — `Definition-Schluessel` (Slug) ist in Section 1 weiter sichtbar (`AdminWorkflowBuilderFormSection.tsx:610-642`). Schreibpfad-Thema (Slug-Editierbarkeit nach Erst-Anlage), eigener Folge-Slice bei Bedarf.

---

#### 🟡 P2

- **Z21-P2-3** · Builder-Dirtystate ist vorhanden. Optional staerkerer Page-Top-Banner — Komfort, kein Fehler.
- **Z21-P2-4** · Frontend-Bundle 624 KB Hauptchunk + 242 KB AdminConfigPage. Fuer internes Netz vertretbar, am Handy spuerbar. Bessere `manualChunks` waeren moeglich. (≡ TODO Z21-N1)
- **Z21-P2-5** · `tailwind.config.js` `extend: {}` leer. Bewusst nicht migriert (siehe `FRONTEND_TODO.md`).

---

#### 🟢 P3

- **Z21-P3-3** · `R8` (Browser-Verifikation Form-Editor) + `R10` (Mobile-Layout) — Nutzer-Aufgaben, nicht code-pruefbar.
- **Durchlaufplanung `?mode=create`-URL** — leichte Auffindbarkeit, kein konkreter Schmerz.
- **Administration: Eingangstexte** — klarer wuenschenswert, kein Fehler.

---

## Z21-Fazit Automation & Hybrid-AD

Trennung „fachlich vorgesehen / im Code vorbereitet / real lauffaehig / produktiv verantwortbar":

| Aspekt | Bewertung | Beleg |
|---|---|---|
| Fachlich vorgesehen | ✅ | `KauthWorkflow/Domäne/Automation.md`, `AutomationPropertyCatalog.cs` |
| Im Code vorbereitet | ✅ | `WorkflowAutomationService.cs`, `WorkflowAutomationHandlerRegistry.cs` |
| Real lauffaehig | ✅ vier produktive Handler | `CreateAdUserLdapsHandler`, `AssignGroupsLdapsHandler`, `CreateMailboxGraphHandler`, `SendWelcomeMailGraphHandler` |
| Produktiv verantwortbar | 🟡 95%-Pfad nutzbar, Browser-Smoke + Graph-/AD-Live-Verifikation offen | siehe Verifikationsluecken |

**Schreibrichtung:** AD on-prem fuehrt; schreibende Aktionen ueber Windows-Worker (Migrationspfad-Etappe 9a). `EntraGraphClient` bleibt read-only.

### Z21-Verifikationsluecken

- **Browser-Verifikation**: nicht ausgefuehrt — UI-Aussagen aus Code abgeleitet.
- **Tests**: Builds + Slice-spezifische Test-Suites ausgefuehrt via `./scripts/verify-prod-ready.sh` (323 FE-Tests gruen; Backend-Test-Build steht auf 7 pre-existing Stub-Errors aus frueheren Refactorings).
- **DB-Lauf**: kein lokaler PostgreSQL-Lauf in Z21.
- **Entra-Live-Verifikation**: Mail-Versand-Pfad nicht mit echten Credentials getestet.

---

## Archivstatus

- Detailzyklen **Z8 bis Z20** + **vollstaendige Z21-Detail-Historie** (alle Done-Slices S1..S10 + S5b/S6b) liegen in `CODE_REVIEW_ARCHIVE.md`.
- **Migrationspfad-Etappe 9a Schritt 1..8** (2026-05-12..13) liegt seit 2026-05-15 in `CODE_REVIEW_ARCHIVE.md`.
- **Admin-Gated-Automation Slices 1..6** (2026-05-13..15) liegen in `CODE_REVIEW_ARCHIVE.md`.
- Detailhistorie **Zyklus 7** in `CODE_REVIEW_ARCHIVE.md` + `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`.

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
| **21** | **2026-05-12** | Produkt-/Funktions-/UX-Review — vollstaendig abgearbeitet bis auf Z21-S4 (blockiert) |
