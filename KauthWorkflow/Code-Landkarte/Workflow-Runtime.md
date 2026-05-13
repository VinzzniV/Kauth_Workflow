# Code-Karte · Workflow-Runtime

#code-landkarte #workflow-runtime

Aktive Ausführung: Workflow-Instanz wird angelegt, Engine läuft den Graph durch, aktiviert Nodes, wertet Decisions aus, generiert Tasks, startet Automationen. `Plan → Apply`-Pattern: Engine ist DB-frei pur, Apply schreibt SQL.

---

## Backend

**Endpoints**
- `api/API/Endpoints/WorkflowEndpoints.cs` — Workflow anlegen, lesen, abbrechen
- `api/API/Endpoints/AdminWorkflowRuntimeEndpoints.cs` — Runtime-Detailblick für Admins
- `api/API/Endpoints/WorkflowLinkEndpoints.cs` — Magic-Link-Auflösung

**Services**
- `api/API/Services/WorkflowRuntimeEngine.cs` — **Pure Engine**: `Plan(snapshot, completedNode)` + Resolver
- `api/API/Services/WorkflowRuntimeEngineSnapshot.cs` — DB-freier Eingabezustand der Engine
- `api/API/Services/WorkflowRuntimePlan.cs` — Plan-Records (NodeSteps + Outcome)
- `api/API/Services/WorkflowLifecycleService.cs` — **Commit-Grenze**: orchestriert Plan + Apply pro Lifecycle-Event (Form-Save, Task-Complete, Approval-Decide)
- `api/API/Services/WorkflowRuntimeService.cs` — Read-Pfade für Workflow-Status
- `api/API/Services/WorkflowVisibilityService.cs` — Wer darf welche Workflows sehen
- `api/API/Services/PostgresSupervisorStepService.cs` — Supervisor-Approval-Bridge

**Repositories**
- `api/API/Repositories/PostgresWorkflowRuntimeRepository.cs` — Runtime-Loop, Node-Instances
- `api/API/Repositories/PostgresWorkflowRuntimeRepository.EngineAdapter.cs` — Adapter Engine ↔ SQL: `LoadRuntimeSnapshot` + `ApplyRuntimePlan`
- `api/API/Repositories/PostgresWorkflowRepository.WorkflowCreateOperations.cs` — Workflow-Anlage
- `api/API/Repositories/PostgresWorkflowRepository.LifecycleOperations.cs` — Lifecycle-Schreibpfade
- `api/API/Repositories/PostgresWorkflowRepository.WorkflowQueryOperations.cs` — List-/Detail-Reads
- `api/API/Repositories/PostgresWorkflowRepository.ReadOperations.cs` — gemeinsame Read-Helper
- `api/API/Repositories/WorkflowLifecycleScopeResults.cs` — Scope-Records (Lifecycle-Apply-Output)
- `api/API/Repositories/PostgresWorkflowStatusCalculationService.cs` — Status-Berechnung

## Frontend

**Pages**
- `web/src/pages/CreateWorkflowPage.tsx` — Neues Workflow anlegen
- `web/src/pages/CreateWorkflowPageSections.tsx` — Form-Sektionen
- `web/src/pages/WorkflowDetailPage.tsx` — Workflow-Detailansicht
- `web/src/pages/WorkflowListPage.tsx` (+ Filters, Results, SavedViewsBar) — Workflow-Liste
- `web/src/pages/SupervisorStepPage.tsx` — Supervisor-Approval-Schritt
- `web/src/pages/PersonWorkflowHistoryPage.tsx` — Workflow-Spur pro Person

**Komponenten**
- `web/src/components/workflow-detail/WorkflowHeaderPanel.tsx`, `WorkflowManagementPanel.tsx`, `WorkflowTaskAreasSection.tsx`, `WorkflowRequirementsPanel.tsx`, `WorkflowLinksPanel.tsx`, `CancelWorkflowDialog.tsx`, `WorkflowAuditLog.tsx`
- `web/src/components/workflows/CreateWorkflowButton.tsx`, `EmployeeForm.tsx`, `RoleSelection.tsx`, `TargetPersonSelection.tsx`, `RequirementsSelection.tsx`, `RequirementIcon.tsx`, `WorkflowCard.tsx`

## DB

`workflows`, `workflow_node_instances`, `workflow_runtime_events`, `workflow_answers` (+ SelectedOptions, SingleSelectKeepValues), `workflow_links`, `workflow_audit_log`

## Tests

- `api/API.Tests/WorkflowRuntimeEngineTests.cs` — Pure-Engine-Tests gegen `Plan(...)`
- `api/API.Tests/WorkflowLifecycleServiceTests.cs`
- `api/API.Tests/WorkflowEndpointsTests.cs`
- `api/API.Tests/WorkflowCreate*Tests.cs`

## Cross-Links

- Domäne: [[Workflow]]
- Architektur: [[Zielarchitektur]] § Runtime
- Verwandt: [[Workflow-Builder]] (definiert die Graphen, die hier ausgeführt werden), [[Tasks-und-Approvals]] (was beim Aktivieren entsteht), [[Automation]] (automation-Nodes triggern Jobs)
