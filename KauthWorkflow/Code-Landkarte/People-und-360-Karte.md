# Code-Karte · People und 360°-Karte

#code-landkarte #people #identity

Person ist der fachliche Primäranker. `people`-Tabelle führt fachlich, `directory_identities` liefert die technische Entra-Identity dazu. Die 360°-Mitarbeiterakte (`/people/:personId`) zeigt den Gesamtzustand: Stammdaten, Rollen, laufende/abgeschlossene Workflows, Directory-Status.

---

## Backend

**Endpoints**
- `api/API/Endpoints/AdminPeopleEndpoints.cs` — People-CRUD, 360°-Karte-Read
- `api/API/Endpoints/AdminDirectorySyncEndpoints.cs` — Sync-Trigger, Pending-Imports
- `api/API/Endpoints/AdminOrgEndpoints.cs` — Departments / Positions / Responsibilities (Organisation)

**Services**
- `api/API/Services/EntraDirectorySyncService.cs` — Entra-Sync-Logik (read-only)
- `api/API/Services/DirectorySyncHostedService.cs` — Background-Sync-Worker
- `api/API/Services/PersonLifecycleProjectionService.cs` — Workflow-Outcomes → `people` projizieren (Eintritts-/Austrittsdatum, etc.)

**Repositories**
- `api/API/Repositories/PostgresWorkflowRepository.MasterDataOperations.cs` — Departments, Positions, Roles
- `api/API/Repositories/PostgresWorkflowRepository.PersonLifecycleOperations.cs` — Person-Lifecycle-Schreibpfad
- `api/API/Repositories/PostgresUserAuthorizationRepository.AdminDepartmentOperations.cs` (+ DepartmentReadOperations, PositionOperations, PositionReadOperations) — Org-Strukturen
- `api/API/Repositories/PostgresUserAuthorizationRepository.AdminResponsibilityOperations.cs` (+ AdminResponsibilityReadOperations) — Responsibilities

## Frontend

**Pages**
- `web/src/pages/PeopleDirectoryPage.tsx` — Listen-/Suchseite (HR + Admin)
- `web/src/pages/PersonWorkflowHistoryPage.tsx` — Workflow-Spur einer Person
- Die 360°-Karte selbst ist Teil der Detail-Route — siehe Komponenten

**Komponenten**
- `web/src/components/admin-config/AdminPersonenSection.tsx` — Personen-Workspace
- `web/src/components/admin-config/AdminOrganizationUserEditor.tsx` — User-Editor
- `web/src/components/admin-config/AdminDirectorySyncSection.tsx`, `AdminEntraImportSection.tsx`, `AdminDepartmentEntraImportSection.tsx`, `AdminDirectoryPendingImportsSection.tsx` — Directory-/Entra-Import-UI
- `web/src/components/admin-config/AdminOrganizationDepartmentEditor.tsx`, `AdminOrganizationResponsibilityEditor.tsx`, `AdminOrganizationRelationsPanel.tsx` — Org-Pflege

## DB

`people`, `app_users`, `directory_identities`, `directory_groups`, `directory_group_members`, `directory_group_role_mappings`, `directory_mapping_audit_log`, `directory_sync_log`, `person_match_audit_log`, `departments`, `department_settings`

## Tests

- `api/API.Tests/AdminPeopleEndpointsTests.cs`
- `api/API.Tests/EntraDirectorySync*Tests.cs`
- `api/API.Tests/PersonLifecycle*Tests.cs`
- `api/API.Tests/DirectoryMapping*Tests.cs`

## Cross-Links

- Domäne: [[Identity]]
- Architektur: [[Entscheidungen]] § Identity & Directory; § `/people` als eigener Navigationsbereich
- Verwandt: [[Workflow-Typen]] (Person-Lifecycle-Projektion), [[Automation]] (Zielbild: 360°-Karte zeigt Post-Execution-Summary — siehe [[Admin-Gated-Automation]])
