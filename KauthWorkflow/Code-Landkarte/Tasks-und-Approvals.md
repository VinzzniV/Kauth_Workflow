# Code-Karte · Tasks und Approvals

#code-landkarte #tasks #approvals

Tasks sind eine Laufzeitwirkung von Workflow-Nodes: Aktivierung eines `task`- oder `approval`-Nodes generiert konkrete Aufgaben, zugewiesen an User oder Responsibility. Supervisor-Approval-Bridge: bestimmte Form-Antworten erzeugen einen Approval-Skip.

---

## Backend

**Endpoints**
- `api/API/Endpoints/TaskEndpoints.cs` — Task lesen, abschließen, kommentieren
- `api/API/Endpoints/WorkflowSupervisorEndpoints.cs` — Supervisor-Approve/Reject

**Services**
- `api/API/Services/TaskApplicationService.cs` — Task-Operationen (Complete, Skip, Comment)
- `api/API/Services/PostgresSupervisorStepService.cs` — Approval-Bridge-Skip-Logik
- `api/API/Services/WorkflowLifecycleService.cs` — orchestriert Task-Complete → Re-Plan (siehe [[Workflow-Runtime]])

**Repositories**
- `api/API/Repositories/PostgresWorkflowRepository.TaskOperations.cs` — Task-CRUD
- `api/API/Repositories/PostgresWorkflowRepository.TaskGenerationOperations.cs` — Task-Generierung bei Node-Aktivierung
- `api/API/Repositories/PostgresWorkflowRepository.AssignmentOperations.cs` — User-/Responsibility-Zuweisung
- `api/API/Repositories/PostgresWorkflowRepository.TaskMetadataOperations.cs` — Frist, Status, Conditions
- `api/API/Repositories/PostgresWorkflowTaskGenerationService.cs` — Spec-Auflösung: Welche Tasks entstehen aus einem Measure-Node
- `api/API/Repositories/TaskDueDateRules.cs` — Fälligkeits-Berechnung

## Frontend

**Pages**
- `web/src/pages/MyTasksPage.tsx` — Persönliche Task-Liste
- `web/src/pages/MyTaskGroups.tsx` — Gruppen-/Responsibility-Tasks

**Komponenten**
- `web/src/components/workflows/TaskCommentsSection.tsx`, `TaskStatusPill.tsx`, `TaskSlaPill.tsx`
- Supervisor-Approval-UI hängt in `SupervisorStepPage.tsx` (siehe [[Workflow-Runtime]])

**Admin-Gated-Automation-Approval (task-Nodes mit Action-Bundle)**
- `web/src/components/workflow-detail/AutomationApprovalDialog.tsx` — Plan-Vorschau mit Bundle-Stepper + Soft-Re-Auth + Auto-Complete-Poll
- Trigger im Task-Card-Bereich von `WorkflowTaskAreasSection.tsx`, sichtbar wenn `task.nodeKey`/`task.automationAdminRole` gesetzt und der User die geforderte Rolle trägt
- Volle Code-Karte für Plan + Approval-Service unter [[Code-Landkarte/Automation]]

## DB

`workflow_tasks`, `workflow_task_comments`, `workflow_task_dependencies`, `task_assignments` (User + Responsibility)

## Tests

- `api/API.Tests/TaskEndpointsTests.cs`
- `api/API.Tests/WorkflowTaskGeneration*Tests.cs`
- `api/API.Tests/SupervisorStep*Tests.cs`
- `api/API.Tests/TaskConditionEvaluatorTests.cs`

## Cross-Links

- Domäne: [[Workflow]] § Tasks
- Verwandt: [[Workflow-Builder]] (Task-Templates werden hier konfiguriert), [[Workflow-Runtime]] (was Tasks erzeugt), [[Auth-und-Permissions]] (Wer darf welchen Task sehen/abschließen)
