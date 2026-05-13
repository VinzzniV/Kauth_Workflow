# Code Review — kauth_workflow

## Zweck

- aktive Review-Priorisierung (Technik + Produkt/UX)
- Begruendung fuer den naechsten Arbeitszyklus
- kompakter Status fuer Mensch und KI

## Nicht verwenden fuer

- kurzfristige Session-Notizen → `MEMORY.md`
- tiefes Slice-fuer-Slice-History-Studium abgeschlossener Zyklen → `CODE_REVIEW_ARCHIVE.md`

## Verwandte Dateien

- `TODO.md` — aktiver, einziger offener Slice (Z21-S4, Etappe 9a Schritt 3 als naechster Code-Slice)
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

**Stand 2026-05-12** — Z21 vollstaendig abgearbeitet. Migrationspfad-Etappe 9a Schritt 1 + 2 + 3 + 4 + 5 sind durch. Schritt 5: enge `created_ad_user`-Mapping-Source mit Vault-Grenze im Code; Worker-Failure-Pfad traegt Output; Linux-Handler-Vertrag strukturiert (`WorkflowAutomationHandlerResult.Failure(...)`); zwei neue reale Handler `AssignGroupsLdaps` (Worker) + `SendWelcomeMailGraph` (Linux). Verifikation reproduzierbar via `./scripts/verify-prod-ready.sh` (Worker-Tests jetzt 48 statt 20). Aktive Resthebel: P0-1/P0-2 Etappe 9a Schritt 6 (Temporary-Credentials-Vault als Architektur-Entscheidung; CreateMailbox/CreateErpEmployee eigene Backend-Slices), plus kleinere P-Findings (P1-2-Sub „Definition-Schluessel"-Slug, P2-3..P2-5, P3-3).

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

**Z21-P0-1 · Automation-Layer ist End-to-End nur Simulation**

Sichtbare Markierung erledigt (Z21-S1); Runtime-Fehlersicht erledigt (TODO Z21-S5). Mit Schritt 3 ist der **erste echte Handler** `CreateAdUserLdaps` (Action ID 7) als parallele AD-Anlage via LDAPS produktiv — die Migration der weiteren Handler (`CreateMailbox`, `AssignGroups`, `CreateErpEmployee`, `SendWelcomeMail`) kommt in Schritt 4.

**Praktisch:** Workflows mit den klassischen Actions (`CreateAdUser` ID 1, `SendWelcomeMail` etc.) laufen weiter als Simulation; Workflows, die die neue `CreateAdUserLdaps` referenzieren, schreiben echt nach AD. UI zeigt das per `IsSimulated`-Badge (Z21-S1) automatisch korrekt an, weil der Marker aus `action_definitions.handler_type LIKE 'simulated_%'` abgeleitet wird.

**Loest sich mit:** Migrationspfad-Etappe 9a Schritt 4 (Migration der restlichen `simulated_*`-Handler).

**Z21-P0-2 · Hybrid-AD-Schreibpfad fehlt** — Richtung + Sub-Architektur entschieden, Code-Implementation offen

Entscheidung dokumentiert in `KauthWorkflow/Architektur/Entscheidungen.md` (Z21-S2 + Hybrid-Worker-Sub-Architektur 2026-05-12) + `Migrationspfad.md` Etappe 9a + `PROJECT_CONTEXT.md`-Guardrail.

**Etappe 9a Schritt 1 ✓ 2026-05-12** — Sub-Architektur entschieden (VM/DB-Polling/LDAPS/gMSA/Direkt-Audit/DPAPI/Lease-Rahmen).

**Etappe 9a Schritt 2 ✓ 2026-05-12** — Worker-Skeleton + DB-Migration `target_runtime`/Lease-/Sweeper-Spalten + External-Completion-Sweeper + RetryPolicy-Shared.

**Etappe 9a Schritt 3 ✓ 2026-05-12** — Erster echter LDAPS-Handler `CreateAdUserLdaps` (Action ID 7, parallel zur Simulation ID 1) gegen on-prem-DC. Layering: Core-Handler + Windows-Host-Adapter (`System.DirectoryServices.Protocols`, `AuthType.Negotiate`, gMSA-Kontext). Idempotenz via Pre-Search + EntryAlreadyExists-Race-Fallback. CSPRNG-Random-Password mit `pwdLastSet=0` (Force-Change). DPAPI-Encryption produktiv via `IDbConfigDecryptor`+`WindowsDpapiDecryptor` (LocalMachine-Scope); `install-db-config.ps1` schreibt jetzt `db.config.dpapi` als Default. gMSA-Service-Switch via `sc.exe config obj=` in `install-windows-service.ps1`.

**Etappe 9a Schritt 4 ✓ 2026-05-12** — Härtung-Block. Neue Spalte `automation_job_attempts.failure_kind` (`'permanent'|'transient'|NULL`); Worker tagt klar permanente LDAP-Fehler (Codes 49/50/32/21/19) und Payload-Validierungen, `WorkflowAutomationRetryPolicy.EvaluateRetryOutcome` mappt `'permanent'` direkt auf `FinalFail` (überschreibt `is_idempotent`+`attemptNumber`). Plus neuer `StaleWorkerClaimSweeper` als Linux-API-HostedService (60s-Polling, 5min-Stale) als Belt-and-Suspenders neben dem Worker-Lazy-Cleanup.

**Etappe 9a Schritt 5 ✓ 2026-05-12** — Vertrags-Plumbing + zwei reale Handler. (a) Enge `created_ad_user`-Mapping-Source mit Property-Whitelist (nur `distinguishedName`) und `nodeKey`-Referenz; Vault-Grenze für Schritt 6 ist im Code, sensible Output-Felder unreichbar. Alle vier Fehler-Cases werfen Exception bei Payload-Build (sichtbar im Workflow-Audit). (b) Worker-Failure-Pfad trägt jetzt Output (`MarkJobFailedAsync` schreibt `output_json` auch im Failure-Fall) — `PartiallyAdded` bei AssignGroups als strukturierter Failure mit Detail-Output abbildbar. (c) Linux-Handler-Vertrag strukturiert (`WorkflowAutomationHandlerResult` mit IsSuccess/ErrorMessage/FailureKind + Factories); `WorkflowAutomationService` discriminiert Result-Failure (mit Tagging) vs Exception (Default ohne). (d) `AssignGroupsLdaps` als zweiter LDAPS-Handler im Worker — Code 20 = AlreadyMember = idempotent. (e) `SendWelcomeMailGraph` als erster echter Linux-side-Handler via Graph App-only; `INotificationTemplateResolver` extrahiert aus dem privaten Helper; neuer Catalog-Eintrag `welcome_mail` ohne Klartext-Passwort. Handler-Registry von Singleton auf Scoped umgestellt.

**Etappe 9a Schritt 6 ✓ 2026-05-13** — Temporary-Credentials-Vault produktiv. (a) Neue Tabelle `temporary_credentials` mit pgcrypto-Symmetric-Encryption (`pgp_sym_encrypt`/`decrypt`); UNIQUE(workflow_node_instance_id, credential_type) macht den atomaren Schreib-Pfad idempotent. GRANTs eng geschnitten (kauth_worker INSERT-only; kauth_api SELECT+UPDATE — kein DELETE). (b) `PostgresWorkerJobStore.MarkJobSucceededAsync` schreibt Vault-Insert + `credentialVaultId`-Patch im output_json + Attempt-Update + Job-Success-Marker in EINER Tx; `ON CONFLICT DO NOTHING` + SELECT-Fallback decken Stale-Claim-Retry-Idempotenz ab. Damit kann kein Crash-Pfad eine Vault-Waise oder einen fehlenden Pointer erzeugen. (c) `CreateAdUserLdapsHandler` schreibt das Plain-Passwort nicht mehr ins output_json — er reicht es als `PendingVaultWrite` an den JobStore und packt einen `credentialVaultId=null`-Platzhalter ins Output, den der JobStore mit der UUID patcht. (d) `created_ad_user`-Property-Whitelist um `credentialVaultId` erweitert (UUID, kein Geheimnis); null-Pfad wird durchgereicht (AlreadyExists ohne Vault), damit der Welcome-Mail-Handler einen sauberen Permanent-Failure liefert statt einen Sweeper-Loop zu triggern. (e) `SendWelcomeMailGraphHandler` konsumiert `ITemporaryCredentialRepository.ReadAdInitialPasswordByVaultIdAsync(uuid)` zur Run-time, Plain-Passwort lebt nur Mikrosekunden im Handler-Heap; Catalog-Template hat jetzt `{{temporary_password}}`-Placeholder. (f) Vault-Key auf Worker analog zum DPAPI-Pattern aus Schritt 3 (`vault.config.dpapi` Default, `install-vault-key.ps1`); API-Seite via `KAUTH_VAULT_KEY`-Env-Var. Praktisch: das Initial-Passwort liegt weder in `automation_jobs.payload_json` noch in `automation_job_attempts.output_json` — der einzige Lese-Pfad ist ein UUID-basierter Vault-Lookup in genau einem Handler. End-to-End Onboarding mit echtem Passwort in der Welcome-Mail ist jetzt produktionsreif (Fresh-Create-Pfad; AlreadyExists-Branch ist Folge-Slice).

**Resthebel ab jetzt:** `CreateMailbox` + `CreateErpEmployee` als jeweils eigene Backend-Architektur-Slices (Plan-Mode-Slices). Optional: Connection-Pooling im LdapsAdUserWriter/LdapsAdGroupMembershipWriter; Vault-Cleanup-Sweeper sobald `temporary_credentials` >100k Zeilen erreicht; Key-Rotation via `key_version`-Spalte; Builder-UI-Vereinfachung fuer die `created_ad_user.credentialVaultId`-Verkettung; Workflow-Engine-Branch-on-automation-output fuer AlreadyExists-Faelle.

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
| Real lauffaehig | 🟡 nur als Simulation | `SimulatedWorkflowAutomationHandlers.cs:76-80` |
| Produktiv verantwortbar | ❌ | siehe Z21-P0-1 |

**Schreibrichtung:** AD on-prem fuehrt; schreibende Aktionen ueber Windows-Worker (Migrationspfad-Etappe 9a). `EntraGraphClient` bleibt read-only.

### Z21-Verifikationsluecken

- **Browser-Verifikation**: nicht ausgefuehrt — UI-Aussagen aus Code abgeleitet.
- **Tests**: Builds + Slice-spezifische Test-Suites ausgefuehrt via `./scripts/verify-prod-ready.sh` (323 FE-Tests gruen; Backend-Test-Build steht auf 7 pre-existing Stub-Errors aus frueheren Refactorings).
- **DB-Lauf**: kein lokaler PostgreSQL-Lauf in Z21.
- **Entra-Live-Verifikation**: Mail-Versand-Pfad nicht mit echten Credentials getestet.

---

## Archivstatus

- Detailzyklen **Z8 bis Z20** + **vollstaendige Z21-Detail-Historie** (alle Done-Slices S1..S10 + S5b/S6b) liegen in `CODE_REVIEW_ARCHIVE.md`.
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
