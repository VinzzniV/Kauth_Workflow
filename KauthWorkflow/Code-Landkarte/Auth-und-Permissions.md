# Code-Karte · Auth und Permissions

#code-landkarte #auth #permissions

Login via Entra (Prod) oder Dev-Sim (lokal). Authorization über Policies + Rollen + Responsibilities + Group-Mappings. Konzept-Trennung: Rolle = technischer Zugriff; Responsibility = fachliche Ownership. Siehe [[Entscheidungen]] § Rollen ≠ Responsibilities.

---

## Backend

**Endpoints**
- `api/API/Endpoints/AuthEndpoints.cs` — Login, Logout, Whoami
- `api/API/Endpoints/EndpointSupport.cs` — `RequireAuthorization`-Helper für Endpoints
- `api/API/Endpoints/AdminEndpoints.cs` — Admin-Operationen (User/Role-Pflege)

**Auth-Pfad (`api/API/Auth/` falls vorhanden; sonst in Services + DI)**
- Entra-Token-Validation, Dev-Sim-Fallback (Token-Header), User-Context-Building
- Authorization-Policy-Service: prüft pro Endpoint, ob der aktuelle User die geforderte Operation darf

**Services**
- `api/API/Services/UserContext.cs` / `IUserContext.cs` (typisch via DI) — aktueller User pro Request
- Authorization-Policy-Service liefert Funktionen wie `CanCreateWorkflow`, `CanAccessWorkflowOverview`, `CanAccessPeopleDirectory` — siehe `EndpointSupport.RequireAuthorization`

**Repositories**
- `api/API/Repositories/PostgresUserAuthorizationRepository.cs` — Hauptklasse (partial)
- `api/API/Repositories/PostgresUserAuthorizationRepository.AdminUserOperations.cs` — User-CRUD
- `api/API/Repositories/PostgresUserAuthorizationRepository.AdminGroupOperations.cs` — Groups + Directory-Group-Role-Mappings
- `api/API/Repositories/PostgresUserAuthorizationRepository.Permissions.cs` — Permission-Auflösung
- `api/API/Repositories/PostgresUserAuthorizationRepository.AdminOperations.cs`, `AdminReadOperations.cs` — Admin-Pfade

## Frontend

**Pages**
- `web/src/pages/EntraLoginPage.tsx` — Produktiv-Login
- `web/src/pages/SimulationLoginPage.tsx` — Dev-Sim-Login

**Komponenten**
- `web/src/components/admin-config/AdminUsersSection.tsx` — User-Workspace
- `web/src/components/admin-config/AdminPermissionsSection.tsx` — Permission-Übersicht
- `web/src/components/admin-config/AdminGroupMappingSection.tsx` — Entra-Group → App-Role
- `web/src/components/admin-config/AdminResponsibilitiesSection.tsx`, `AdminResponsibilitiesAndRequirementsSection.tsx` — Responsibilities
- `web/src/components/admin-config/AdminTechnicalAccessSection.tsx` — technische Zugänge

## DB

`app_users`, `app_roles`, `app_user_roles`, `app_role_permissions`, `app_permissions`, `app_user_permission_overrides`, `app_groups`, `app_user_groups`, `app_group_roles`, `app_responsibilities`, `app_user_responsibilities`, `app_group_responsibilities`, `system_responsibilities`, `auth_permission_audit_log`, `directory_group_role_mappings`

## Tests

- `api/API.Tests/AuthEndpointsTests.cs`
- `api/API.Tests/AuthorizationPolicy*Tests.cs`
- `api/API.Tests/PostgresUserAuthorizationRepository*Tests.cs`

## Cross-Links

- Domäne: [[Identity]] (Person ≠ technische Identity; Rollen ≠ Responsibilities)
- Architektur: [[Entscheidungen]] § Rollen ≠ Responsibilities
- Verwandt: [[Tasks-und-Approvals]] (Wer darf Tasks abschließen), [[People-und-360-Karte]] (User-Identity-Verknüpfung)
