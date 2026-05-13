# Code-Karte · Automation

#code-landkarte #automation

Handler für AD-/Mailbox-/Mail-Operationen. Zwei Ausführungsumgebungen: Linux-API (Graph) + Windows-Worker (LDAPS). Job-Queue über DB, Vault für Initial-Passwörter, Mapping-Sources für Input-Verkettung. Vollständige Erklärung: [[Automation]] (Ist-Stand), [[Admin-Gated-Automation]] (Zielbild).

---

## Backend — Linux-API

**Services (Infrastruktur)**
- `api/API/Services/WorkflowAutomationService.cs` — Job-Claiming, Ausführung, Retry
- `api/API/Services/WorkflowAutomationHostedService.cs` — Background-Polling
- `api/API/Services/WorkflowAutomationHandlerRegistry.cs` — Handler-Registry
- `api/API/Services/IWorkflowAutomationActionHandler.cs` — Handler-Vertrag
- `api/API/Services/WorkflowAutomationRetryPolicy.cs`, `WorkflowAutomationRetrySettings.cs` — Retry-Logik inkl. Per-Action-Override
- `api/API/Services/AutomationPropertyCatalog.cs` — Mapping-Source-Whitelist + Catalog-DTO

**Handler (echte Linux-API-Pfade)**
- `api/API/Services/SendWelcomeMailGraphHandler.cs` — Welcome-Mail mit Vault-Decrypt
- `api/API/Services/CreateMailboxGraphHandler.cs` — Exchange-Lizenz-Assignment
- `api/API/Services/GraphMailboxProvisioner.cs` (+ `IGraphMailboxProvisioner.cs`) — SMTP-Strictness, Outcome-DU
- `api/API/Services/GraphMailSender.cs` (+ `IGraphMailSender.cs`) — Graph-SendMail-Wrapper
- `api/API/Services/GraphApplicationConfigurationService.cs` (+ Validator + RuntimeConfiguration) — Graph-App-only-Auth-Konfig

**Handler (simuliert, für Bestands-Workflows + Dev)**
- `api/API/Services/SimulatedWorkflowAutomationHandlers.cs`

**Repositories**
- `api/API/Repositories/PostgresWorkflowAutomationOperations.cs` — Job-Lifecycle, Mapping-Source-Resolver, `LoadCreatedAdUserOutputsForWorkflowInScope`
- `api/API/Repositories/PostgresWorkflowRepository.AutomationOperations.cs` — Workflow-Repo-Anteil für Automation
- `api/API/Repositories/PostgresWorkflowAutomationReadRepository.cs` — Read-Pfade
- `api/API/Repositories/ITemporaryCredentialRepository.cs`, `PostgresTemporaryCredentialRepository.cs` — **Vault**

**Sweeper**
- `api/API/Services/StaleWorkerClaimSweeper.cs` — Worker-Crash-Recovery
- `api/API/Services/ExternalAutomationJobCompletionSweeper.cs` — External-Completion-Pfad
- `api/API/Services/WorkerLeaseSettings.cs` — Heartbeat-/Stale-Werte

## Backend — Windows-Worker

- `worker/AdAutomationWorker.Core/` — plattform-neutrale Core-Engine (Handler-Logik, Polling, JobStore-Vertrag)
- `worker/AdAutomationWorker/` — Windows-Host (`net8.0-windows`) mit `LdapsAdUserWriter`, `LdapsAdGroupMembershipWriter`, DPAPI-Decryptor
- `worker/AdAutomationWorker.Tests/` — Worker-Tests (cross-platform)
- `worker/setup/*.ps1` — Setup-Skripte (DPAPI-Konfig, gMSA-Service-Switch, Vault-Key)

## Frontend

- `web/src/components/admin-config/WorkflowBuilderActionEditor.tsx` — Action-Wahl am Automation-Node (siehe [[Workflow-Builder]])
- `web/src/components/admin-config/WorkflowBuilderActionMappingEditor.tsx` — Input-Mapping pro Parameter

## DB

`action_definitions` (mit `max_attempts_override`, `subsequent_retry_delay_seconds_override`, `target_runtime`), `workflow_node_actions`, `automation_jobs`, `automation_job_attempts`, `automation_job_logs`, `temporary_credentials`

## Tests

- `api/API.Tests/WorkflowAutomation*Tests.cs`
- `api/API.Tests/WorkflowAutomationRetryPolicyTests.cs`
- `api/API.Tests/GraphMailboxProvisionerOutcomeTests.cs`
- `api/API.Tests/CreateMailboxGraphHandlerTests.cs`
- `api/API.Tests/SendWelcomeMailGraph*Tests.cs`
- `api/API.Tests/CreatedAdUserSourceResolutionTests.cs`, `CreatedMailboxSourceResolutionTests.cs`
- `api/API.Tests/TemporaryCredentialRepositoryTests.cs`, `EnvVaultKeyProviderTests.cs`
- `api/API.Tests/AutomationPropertyCatalogTests.cs`
- `worker/AdAutomationWorker.Tests/**`

## Cross-Links

- Domäne: [[Automation]] (Ist-Stand)
- Architektur: [[Admin-Gated-Automation]] (Zielbild Prod), [[Hybrid-Worker-Sub-Architektur]] (Worker-Architektur), [[Entscheidungen]] § AD/Entra-Schreibrichtung
- Verwandt: [[Workflow-Runtime]] (`automation`-Node triggert die Jobs hier), [[Workflow-Builder]] (Action-Konfiguration)
