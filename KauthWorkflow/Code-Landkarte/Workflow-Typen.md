# Code-Karte · Workflow-Typen

#code-landkarte #workflow-typen

Die konkreten Workflow-Definitionen für `onboarding`, `offboarding`, `department_change`, `name_change`. Diese sind **Daten**, nicht Code — sie liegen als Seeds in der DB und werden vom [[Workflow-Builder]] gepflegt. Die Engine ([[Workflow-Runtime]]) führt sie generisch aus; es gibt nur an wenigen Stellen typ-spezifische Logik.

**Wichtig:** Der `workflowDefinitionKey` (z. B. `"onboarding"`) ist der zentrale fachliche Anker, der diese Typen unterscheidet. Siehe [[Entscheidungen]] § `workflowDefinitionKey` ist der kanonische Name.

---

## Wo die Definitionen leben

- **Seeds:** `db/02_dev_seed.sql` enthält die Definition-Versionen für die vier Kern-Typen (Nodes, Edges, Task-Specs, Default-Antworten). Echter Prod-Stand kommt aus der DB.
- **Persistente DB:** `workflow_definitions` + `workflow_definition_versions` + `workflow_nodes` + `workflow_edges` + `workflow_node_task_specs`.

## Wo typ-spezifische Logik im Code hängt

Die meisten Pfade sind generisch (Engine, Validation, Builder). Es gibt aber Stellen, an denen ein `workflowDefinitionKey` als Discriminator wirkt:

- `api/API/Services/WorkflowLifecycleService.cs` — Supervisor-Bridge-Pfad nutzt `WorkflowDefinitionKey` aus dem Runtime-Snapshot
- `api/API/Services/WorkflowVisibilityService.cs` — Sichtbarkeitsregeln je Definition
- `api/API/Repositories/PostgresWorkflowRepository.WorkflowCreateOperations.cs` — Workflow-Anlage validiert Pflicht-Antworten je Typ
- `api/API/Services/PersonLifecycleProjectionService.cs` — projiziert Workflow-Outcomes auf `people` (z. B. Eintritts-/Austritts-Datum)
- `api/API/Repositories/PostgresWorkflowRepository.PersonLifecycleOperations.cs` — Person-Lifecycle-Schreibpfad

**Legacy-Brücke:** `legacyProcessTypeKey` existiert noch als read-only-Fallback für Altbestand-Definitionen — siehe [[Entscheidungen]] § API-Kompatibilität.

## Frontend-Spezifika

- `web/src/components/workflows/EmployeeForm.tsx` — Onboarding-Formular mit Person-Daten
- `web/src/components/workflows/TargetPersonSelection.tsx` — Offboarding/Change wählen die Zielperson
- `web/src/components/workflows/RequirementsSelection.tsx`, `RequirementIcon.tsx` — Anforderungs-Auswahl (von Abteilungsleitung)
- `web/src/components/workflows/RoleSelection.tsx` — Rollen-spezifische Default-Antworten

## Wenn du an einem spezifischen Workflow-Typ arbeitest

1. **Definition ändern (Nodes, Edges, Task-Specs)** → über [[Workflow-Builder]] in der UI (live im Admin-Workspace), produziert neue Version
2. **Dev-Seed ändern** → `db/02_dev_seed.sql` für frische DB-Setups
3. **Typ-spezifische Logik im Code** → die oben gelisteten Diskriminator-Stellen prüfen + ergänzen
4. **Validation-Regel ergänzen** → `WorkflowDefinitionValidationService.cs` (siehe [[Workflow-Builder]])

## Cross-Links

- Domäne: [[Workflow]] § Workflow-Typen
- Verwandt: [[Workflow-Builder]], [[Workflow-Runtime]], [[People-und-360-Karte]] (Person-Lifecycle-Projektion)
