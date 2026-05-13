# Code-Karte · Workflow-Builder

#code-landkarte #workflow-definition

Admin baut Workflow-Definitionen aus Nodes + Edges. Mehrere Versionen pro Definition; Draft → Validation → Publish. Form-Editor statt freier Canvas. Konzepte: [[Workflow]] § Definition Layer.

---

## Backend

**Endpoints**
- `api/API/Endpoints/AdminWorkflowDefinitionConfigEndpoints.cs` — CRUD für Definitions, Versions, Draft-Save, Publish
- `api/API/Endpoints/AdminProcessConfigEndpoints.cs` — Legacy-Process-Konfig (in Abbau)
- `api/API/Endpoints/AdminAnswerConfigEndpoints.cs` — Antwort-Definitionen
- `api/API/Endpoints/WorkflowMasterDataEndpoints.cs` — Departments/Roles/Positions als Picker-Quelle

**Services**
- `api/API/Services/WorkflowDefinitionRuntimeService.cs` — Read-Pfad: aktive Version laden
- `api/API/Services/WorkflowDefinitionValidationService.cs` — Publish-Validation (Node-Typen, Edges, Bedingungen)
- `api/API/Services/WorkflowDefinitionDraftValidator.cs` — Draft-Save-Validation (weicher als Publish)
- `api/API/Services/WorkflowDefinitionSnapshotValidator.cs` — Snapshot-Konsistenzregeln
- `api/API/Services/WorkflowDefinitionValidationCatalog.cs` — Fehler-Codes + UI-Labels
- `api/API/Services/WorkflowDefinitionValidationHelpers.cs` — DAG-Erreichbarkeit etc.

**Repositories**
- `api/API/Repositories/PostgresWorkflowRepository.WorkflowDefinitionAdminOperations.cs` — Definition/Version-CRUD
- `api/API/Repositories/PostgresWorkflowRepository.WorkflowDefinitionGraphMappingOperations.cs` — Nodes/Edges/Specs persistieren
- `api/API/Repositories/PostgresWorkflowRepository.AnswerDefinitionAdminOperations.cs` — Antwort-Definitionen
- `api/API/Repositories/PostgresWorkflowRepository.TaskTemplateAdminOperations.cs` (+ ConditionOperations, DependencyOperations) — Task-Templates
- `api/API/Repositories/PostgresWorkflowRepository.RoleAnswerDefaultAdminOperations.cs` — Rollen-spezifische Default-Antworten

## Frontend

**Pages**
- `web/src/pages/WorkflowBuilderPage.tsx` — Hauptseite des Builders
- `web/src/pages/AdminConfigPage.tsx` — Admin-Konfig-Workspace

**Komponenten**
- `web/src/components/admin-config/AdminWorkflowBuilderFormSection.tsx` — Form-Editor-Mantel
- `web/src/components/admin-config/WorkflowBuilderStepCard.tsx` — Step-Karte (ein Node)
- `web/src/components/admin-config/WorkflowBuilderStepConfigEditor.tsx` — Pro-Step-Config
- `web/src/components/admin-config/WorkflowBuilderSpecEditor.tsx` — Task-Specs am Measure-Node
- `web/src/components/admin-config/WorkflowBuilderActionEditor.tsx` — Action am Automation-Node
- `web/src/components/admin-config/WorkflowBuilderActionMappingEditor.tsx` — Input-Mapping zur Action
- `web/src/components/admin-config/WorkflowBuilderConditionEditor.tsx` — Decision-Conditions
- `web/src/components/admin-config/WorkflowBuilderGraphPreview.tsx` — Read-only Graph-Vorschau
- `web/src/components/admin-config/WorkflowBuilderMeasurePreview.tsx` — Measure-Vorschau
- `web/src/components/admin-config/DependencyGraphEditor.tsx` — Edge-Editor
- `web/src/components/admin-config/AdminTaskTemplateEditor.tsx` (+ Sidebar, ConditionsPanel, DependenciesPanel) — Task-Template-Pflege

## DB

`workflow_definitions`, `workflow_definition_versions`, `workflow_nodes`, `workflow_edges`, `workflow_node_configs`, `workflow_node_task_specs` (+ Conditions/Dependencies), `workflow_node_actions`, `workflow_answer_definitions` (+ Options, Validation-/Visibility-/Reset-Rules)

## Tests

- `api/API.Tests/WorkflowDefinitionValidationServiceTests.cs`
- `api/API.Tests/PostgresWorkflowRepositoryWorkflowDefinitionIntegrationTests.cs`
- `api/API.Tests/AdminWorkflowDefinitionConfigEndpointsTests.cs`
- Frontend: `web/src/**/__tests__/WorkflowBuilder*.test.tsx`

## Cross-Links

- Domäne: [[Workflow]]
- Architektur: [[Zielarchitektur]] § Definition Layer
- Verwandt: [[Workflow-Runtime]] (was mit den Definitions zur Laufzeit passiert), [[Tasks-und-Approvals]] (Task-Specs werden hier konfiguriert)
