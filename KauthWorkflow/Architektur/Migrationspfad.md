# Migrationspfad

#architektur #migration

Wo das Projekt jetzt steht, wohin es geht, und in welcher Reihenfolge die Migration abläuft.

Primärquelle im Repo: `PROJECT_CONTEXT.md`

---

## Stand Mai 2026

### Bereits vorhanden ✓

- Backend (ASP.NET Core 8), Frontend (React 19), PostgreSQL als tragfähige Basis
- Workflow-Instanzen, Tasks, Kommentare, Deadlines, Audit, Admin-Konfiguration
- Directory-/Identity-Integration mit Entra
- Mehrere Prozessarten und konfigurierbare Formular-/Task-Bausteine
- Erste phasenbasierte Definitionen für `onboarding`, `offboarding`, `department_change` im Definition Layer
- Rotation/Abteilungsdurchlauf (Phase 1–8 abgeschlossen)
- Automation Layer Kern — Jobs, Versuche, Logs
- Mitarbeiterzentrierter Lifecycle-Schnitt: `people` als fachlicher Anker
- Konfigurierbare Mail-Vorlagen (`notification_templates`)
- Zentrales System-Event-Log

### Noch nicht im Zielbild ✗

- Expliziter Definition Layer mit vollständiger Versionierung (teilweise da, nicht vollständig genutzt)
- Eigenständige Runtime-/Orchestrierungsschicht (Runtime-Events existieren, aber noch kein echter Node-Lifecycle-Loop)
- Echter Automation Layer mit produktiven Handlern (derzeit simuliert: `CreateAdUser`, `SendWelcomeMail`)
- Guided Builder für neue Workflow-Definitionen (Canvas-UI existiert, aber Nutzbarkeit unklar)
- Saubere Repository-/Service-Grenzen (Monolith-Repository als Hauptproblem)

---

## Migrationspfad (Reihenfolge)

| Schritt | Inhalt | Status |
|---------|--------|--------|
| 1 | Artefakt- und Secret-Hygiene | ✓ |
| 2 | Produktkern ent-onboarden | laufend |
| 3 | Zielarchitektur dokumentieren, Begriffe harmonisieren | ✓ |
| 4 | Definition Layer einführen | ✓ teilweise |
| 5 | Runtime parallel einführen | in Arbeit |
| 6 | Bestehende Workflows mappen | ✓ für Kern-3 |
| 7 | Task-System an Node-Runtime anbinden | ✓ erledigt (Zyklus 7, 2026-05-05: Lifecycle-Service als Commit-Grenze + Validation-Split abgeschlossen) |
| 8 | Generische Validierung einführen | ✓ erledigt (CLA-4, DAG-Erreichbarkeitscheck + Publish-Guard) |
| 9 | Automation Layer bauen | Basis da, echte Handler fehlen |
| 9a | Schreibender Automation-Layer (Windows-Worker, AD on-prem führt) | ✓ Sub-Architektur 2026-05-12 (Schritt 1); ✓ Worker-Skeleton 2026-05-12 (Schritt 2); ✓ Erster echter LDAPS-Handler `CreateAdUserLdaps` + DPAPI produktiv 2026-05-12 (Schritt 3); Schritt 4 (Linux-Sweep + Vault + Migration der anderen `simulated_*`-Handler) offen |
| 10 | Guided Builder ausbauen | Basis da |
| 11 | Altwelt gezielt zurückbauen | nach Parität |

---

## Offene technische Schulden (aus Code Review)

### Kritisch (Stand April 2026)

| ID | Problem | Status |
|----|---------|--------|
| C1 | Transaktionsgrenzen Rotation-Regenerierung | ✓ erledigt |
| C2 | Rotation-Template ohne Zuständigkeit → Aufgaben unsichtbar | ✓ erledigt (2026-04-24) |
| C3 | Entra-gelöschte User werden nicht deaktiviert | ✓ erledigt |
| C4 | Task-Filter in-memory statt SQL | ✓ erledigt (Rotation) |

### Hoch

| ID | Problem | Status |
|----|---------|--------|
| H1 | Monolithisches Repository | ✓ teilweise erledigt — Rotation-Slice herausgeschnitten (CLA-3); weitere Schnitte offen |
| H5 | Keine DAG-Validierung vor Workflow-Definition-Publish | ✓ erledigt (CLA-4, 2026-04-24) |

→ Details: [[Code-Review-Status]]

---

## Erledigter Repository-Schnitt: CLA-3 (2026-04-23)

Der Rotation-Slice wurde aus dem Monolithen herausgeschnitten:

- `PostgresRotationRepository` (neu) ← aus `RotationTaskGenerationOperations` + `RotationOperations` + weiteren Rotation-Partials
- `PostgresRepositorySharedHelpers` (neu) ← geteilte Helfer (Assignment/Audit/Notification/MapTargetPerson)
- `PostgresWorkflowRepository` delegiert Rotation-Task-Routing über ctor-injiziertes `IRotationRepository`

Noch ausstehend: weitere fachlich motivierte Schnitte um `PostgresWorkflowRepository` bzw. verbleibende große Partials. Reine Split-Arbeit ohne Last-, Runtime- oder Wartbarkeits-Trigger ist derzeit nicht der naechste Hebel.

---

## Etappe 9a · Schreibender Automation-Layer

Architekturrichtung entschieden 2026-05-12 (Z21-S2): **AD on-prem führt**, schreibende Lifecycle-Aktionen laufen über einen dedizierten Windows-Worker. Begründung und verworfene Alternativen in [[Entscheidungen]] → "AD/Entra-Schreibrichtung".

Bis Etappe 9a abgeschlossen ist, bleibt der Automation-Layer offiziell im Simulationsmodus. Z21-S1 hat das im UI als Banner + Badge sichtbar gemacht; der Action-Katalog ist weiter `simulated_*`-getrieben.

**Zielbild in vier Schritten:**

1. **Sub-Architekturfragen klären** ✓ entschieden 2026-05-12 — siehe [[Entscheidungen]] § "Hybrid-Worker-Sub-Architektur":
   - Worker-Deployment: dedizierte Windows-VM (domain-joined)
   - Transport: DB-Polling auf zentrale Postgres mit `target_runtime`-Diskriminator
   - AD-Schreibmechanik: `System.DirectoryServices.Protocols` (LDAPS), `AuthType.Negotiate` unter gMSA-Kontext
   - Domänen-Auth: gMSA
   - Audit-Rückkanal: direkt in `automation_jobs` + `automation_job_attempts`
   - Postgres-Auth des Workers: eigener Login `kauth_worker` + DPAPI-Konfig
   - Job-Claim-Sicherheit: `FOR UPDATE SKIP LOCKED` + Lease-Spalten; Heartbeat-Detail in Schritt 2
2. **Worker-Skeleton:** ✓ 2026-05-12 — `worker/Worker.sln` mit drei Projekten (`AdAutomationWorker.Core` als plattform-neutrale Engine, `AdAutomationWorker` als Windows-Service-Host, `AdAutomationWorker.Tests`). DB-Migration `2026-05-12_automation_jobs_target_runtime.sql` fügt `target_runtime`, Lease-Spalten (`claimed_at`/`claimed_by`/`heartbeat_at`) und Sweeper-Spalten (`completion_processed_at`/`completion_claimed_at`) hinzu. Simulierter Handler `simulated_windows_worker_ping` beweist Transport- + Audit-Pfad ohne AD-Zugriff. Auf Linux-API-Seite: `ExternalAutomationJobCompletionSweeper` triggert die zwei neuen Lifecycle-Methoden `OnExternalAutomationJobSucceededAsync` / `OnExternalAutomationJobFailedAsync`; gemeinsame Retry-Quelle in `WorkflowAutomationRetryPolicy`.
3. **Erster echter Handler:** ✓ 2026-05-12 — `CreateAdUserLdaps` (Action ID 7, parallel zur Simulation ID 1) schreibt AD-User via LDAPS gegen den on-prem-DC. Layering: `IAdUserWriter`/`AdUserSpec`/`AdWriteOutcome` im plattform-neutralen Core; `LdapsAdUserWriter` im net8.0-Windows-Host. Idempotent durch Pre-Search; Race-Schutz via `EntryAlreadyExists`-Fallback. Random-Passwort (CSPRNG, 16 Zeichen, alle 4 Komplexitätsklassen) wird mit `pwdLastSet=0` gesetzt → Force-Change beim ersten Login; das Passwort landet im `automation_job_attempts.output_json` (akzeptiertes Audit-Risiko, Vault-Pointer ist Schritt 4). Minimal-Error-Whitelist im Writer mappt LDAP-Codes 49/50/32/21/19 auf `PermanentFailure`. DPAPI-Encryption produktiv: `IDbConfigDecryptor` im Core, `WindowsDpapiDecryptor` im Host, `install-db-config.ps1` schreibt jetzt `db.config.dpapi` als Default. `install-windows-service.ps1` setzt den gMSA via `sc.exe config obj=`.
4. **Schritt 4 (offen):** Error-Klassifikation generalisieren (Konfig-Vertrag pro Handler-Type), Linux-API Stale-Worker-Claim-Sweep als zentraler HostedService, Temporary-Credentials-Vault statt Klartext-Passwort im `output_json`, Migration der weiteren `simulated_*`-Handler (`CreateMailbox`, `AssignGroups`, `CreateErpEmployee`, `SendWelcomeMail`). Action-Katalog enthält am Ende keinen `simulated_*`-Eintrag mehr; die `IsSimulated`-Banner aus Z21-S1 verschwinden naturgemäß.

**Beleg für Z21-S1-Brücke:** Das Backend leitet `IsSimulated` aus `action_definitions.handler_type LIKE 'simulated_%'` ab. Sobald ein Handler aus simulated zu real wechselt, fällt der UI-Marker automatisch weg — keine Doppelpflege.

---

## Parallelzustand: Legacy + Neu

Der große Parallelzustand ist deutlich geschrumpft. Offen sind vor allem noch Benennungs- und Runtime-Brücken:

| Legacy | Neu | Status |
|--------|-----|--------|
| `legacyProcessTypeKey` in Form-/Gatekeeper-Config, Requirements- und WorkflowConfig-Pfaden | `workflowDefinitionKey` | noch aktiv in Builder, Publish-Validierung, Runtime-Gatekeeper und Teilen der Master-Data-Leser |
| `LegacyProcessTypeKey` / `ProcessTypeName` in Notification-DTOs und Link-Lookup-Record | `WorkflowDefinitionKey` / `WorkflowDefinitionName` | ✓ erledigt 2026-05-11: `WorkflowNotificationDispatchTarget`, `WorkflowNotificationRenderContext`, `WorkflowLinkLookupRecord` und alle Consumer umbenannt |
| Internes `processType*`-Naming in Backend-Workflow-Pfaden (`WorkflowStatusRules`, Calculation-/Generation-Service, Create-/QueryOperations, `NotificationEmailTemplateBuilder`) | `workflowDefinition*`-Benennung durchgehend | ✓ erledigt 2026-05-11: 11 Dateien umbenannt, Fehlermeldungen entlegacyt, Build 0/0, 490 Tests grün |
| `PrimaryLegacyProcessTypeKey` in Definition-/Runtime-Snapshots | direkter Definition-Key | ✓ erledigt 2026-05-11: Properties in C#-Code und Doku auf `workflowDefinitionKey` / `WorkflowDefinitionKey` umbenannt |
| `WorkflowLegacyStatus` bzw. `LegacyStatus` im Runtime-Pfad | typsicheres `WorkflowStatus` | kein eigener DTO-Schwerpunkt mehr, aber intern noch als Übergangsstatus in Engine/Apply-Pfad vorhanden |
| `CompletedOnboarding*` in Rotation-Pfad (Methode, DTO-Properties, FE-Typen, UI-Labels) | `SourceWorkflow`-Terminologie | ✓ erledigt 2026-05-11: `GetCompletedOnboardingSource` → `GetSourceWorkflow`; `LatestCompletedOnboardingWorkflowUid`/`At` → `LatestSourceWorkflowUid`/`CompletedAt`; SQL CTEs und UI vollständig entlegacyt |
| `WorkflowProcessTypeDto` / `processType`-Lesesurface | definitionsbasierte Workflow-Metadaten | ✓ erledigt 2026-05-11: umbenannt zu `WorkflowDefinitionRefDto` / `workflowDefinition`; FE-Typen und alle Consumer synchron |
| `setup`-Node-Type | `measure_provision` / `_deprovision` / `_change` / `_rename` | ✓ Code/Seeds clean (nur noch `db/_archive/`); Doku bereinigt; einzig offener Rest: manuelle DB-Inventur gegen persistierte Definitionen |
| Monolith-Repository | Slice-Repositories (`PostgresRotationRepository` + Helpers) | Rotation ✓ erledigt; Runtime/Automation/Audit/Notification offen |

Bereits abgebaut sind u. a. `process_types` als Tabelle, die alten Onboarding-Alias-Endpunkte, das harte Permission-Array und der `task_templates`-/`legacyTemplateKey`-Pfad. Konkrete Restinventur: [[Legacy-Abbau-Plan]].

Konkrete Roadmap zum Abbau: siehe [[Legacy-Abbau-Plan]].

---

## Verwandte Notizen

- [[Zielarchitektur]] — Was das Ziel ist
- [[Entscheidungen]] — Warum Migration statt Big Bang
- [[Code-Review-Status]] — Was konkret aussteht
