# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Collaboration Workflow

This project uses a split-AI workflow:

- **Claude** – Design, analysis, architecture decisions. Writes findings and implementation instructions into `TODO.md` under the relevant task, so Codex can execute them.
- **Codex** – Implementation. Reads task descriptions and Claude-written design blocks from `TODO.md`.

When working on a task: read `DOCS_CONTROL.md` first. It defines which documents to read for which task type and where to write output.

Documentation and task descriptions are written in **German**.

---

## Project Overview

Employee Lifecycle Management application. Manages onboarding/offboarding workflows with role-based access, directory sync against Entra ID (Azure AD), task assignment, and email notification.

**Stack:**
- Backend: ASP.NET Core 8, Minimal API, Npgsql (PostgreSQL), .NET Identity Web
- Frontend: React 19, TypeScript, Vite, TanStack Query, React Router 7, Tailwind CSS 4, MSAL (Entra auth)
- Database: PostgreSQL
- Proxy: Caddy (production only)
- Auth: `AUTH_MODE=dev-sim` (local) or `AUTH_MODE=entra` (production)

---

## Development Commands

### Backend

```powershell
# Start local DB (port 25432)
docker compose -f compose.yml -f compose.dev-db.yml up -d db

# Run API locally (AUTH_MODE=dev-sim, connects to localhost:25432)
dotnet run --project api/API/API.csproj --launch-profile API

# Build (release)
dotnet build api/API/API.csproj -c Release

# Run all backend tests (requires DB on localhost:25432)
dotnet test api/API.Tests/API.Tests.csproj -c Release -p:UseAppHost=false

# Run a single backend test class or method
dotnet test api/API.Tests/API.Tests.csproj --filter "FullyQualifiedName~ClassName"
```

### Frontend

```powershell
cd web
npm install          # or npm ci for CI
npm run dev          # Vite dev server on :5173, proxies /api to :5001
npm run lint
npm test             # vitest run (single pass)
npm run build        # tsc + vite build
```

Run a single frontend test file:
```powershell
cd web
npx vitest run tests/taskStatus.test.ts
```

### Production stack

```bash
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml up -d --build
```

---

## Architecture

### Backend structure

```
api/API/
  Program.cs                        # Bootstrap only – delegates to extension methods
  LifecycleRuntimeSettings.cs       # All env-var resolution in one place
  Endpoints/                        # Minimal API endpoint groups (one file per domain)
  Repositories/                     # PostgresWorkflowRepository (partial classes by operation type)
                                    # PostgresUserAuthorizationRepository (partial classes)
  Services/                         # Domain services (directory sync, email, supervisor step)
  Auth/                             # CurrentUser resolution, Entra + DevSim identity resolvers
  Authorization/                    # Role/permission policy service
  Requirements/                     # RequirementBehaviorEngine (visibility, reset rules)
  Extensions/                       # OnboardingServiceCollectionExtensions (DI registration)
                                    # OnboardingApplicationExtensions (middleware + health endpoints)
                                    # OnboardingStartupValidationExtensions (startup guards)
  Contracts/                        # DTO types for API responses
```

Startup sequence: `DotEnvLoader.LoadOptional(".env.prod")` → `AddLifecycleApiServices` → `ValidateLifecycleStartup` → `ConfigureLifecycleApi` → `MapLifecycleApiEndpoints` → `ValidateLifecycleRouteRegistration`.

`LifecycleRuntimeSettings` is the single source of truth for all environment-driven behavior. All runtime guards (auth mode, swagger, HTTPS requirement) are enforced there or in the startup validation extensions.

`PostgresWorkflowRepository` is split into partial classes by operation type (ReadOperations, TaskOperations, RequirementOperations, etc.) to keep file sizes manageable.

### Frontend structure

```
web/src/
  auth/           # CurrentUser context, roleModel.ts (capabilities derivation), IdentityProvider abstraction
  config/         # appRuntimeConfig.ts – reads window.__APP_CONFIG__ (runtime) or VITE_* (local)
  hooks/          # All stateful business logic (admin management, workflow creation, requirement editor)
  pages/          # Route-level page components – thin wrappers over hooks + presentational components
  components/     # Presentational components, co-located model files (workflowDetailModel.ts etc.)
  services/       # API layer
    api/client.ts        # Single fetch wrapper (requestJson), handles auth + 401 retry
    api/mappers.ts       # Backend DTO → frontend type mapping
    api/backendDtos.ts   # Backend response shape types
    lookupApi.ts         # Process types, roles, departments
    taskApi.ts           # Task CRUD
    workflowApi.ts       # Workflow CRUD and queries
    adminApi.ts          # Admin management endpoints
    adminConfigApi.ts    # Admin config endpoints
    authApi.ts           # Auth/session endpoints
    peopleApi.ts         # People search
    queries/             # TanStack Query query option factories
    mutations/           # TanStack Query mutation helpers
    lifecycleApi.ts      # Compatibility barrel – re-exports all domain modules (do not import new code from here)
    onboardingApi.ts     # Compatibility barrel – re-exports lifecycleApi + adminConfigApi (do not import new code from here)
  types/
    workflow.ts   # All workflow-related frontend types
    auth.ts       # AdminUser, AdminRole, Me, etc.
  utils/          # Pure functions: requirementEditor, requirements, taskStatus, workflowStatus, etc.
```

**Key patterns:**
- `requestJson` in `services/api/client.ts` is the only HTTP entry point. All service modules call it directly. Tests that need to verify URL routing mock `../src/services/api/client` at the low level.
- For all other service tests: mock at the domain module level (`vi.mock("../src/services/taskApi", ...)`), not at the `client` level.
- React Query is used for data fetching in most pages. Some admin hooks still use manual fetch with cancellation flags – these are candidates for migration (see TODO P2.1).
- `lifecycleApi.ts` and `onboardingApi.ts` are legacy compatibility barrels. New code imports directly from the domain modules.

### Auth modes

| Mode | When | Description |
|---|---|---|
| `dev-sim` | Local dev | Simulated login from directory-synced users; no real Entra token |
| `entra` | Production | Real MSAL/Entra OIDC flow; requires all `ENTRA_*` env vars |

`AUTH_MODE=dev-sim` in production causes a startup abort. `SWAGGER_ENABLED=true` in production also causes a startup abort.

### Health endpoints

- `/health/live` – process alive only, always HTTP 200
- `/health/ready` – DB connectivity check, returns 503 when DB is unavailable; used as container healthcheck probe
- `/health` – deep health (DB + Entra reachability), always HTTP 200, for ops diagnosis only; **not** used as a container probe

### Database

- Dev init: `db/init/dev/00_init.sql` + `02_seed.sql` (includes `90_dev_defaults.sql`)
- Production init: `db/init/prod/00_init.sql` + `02_bootstrap.sql` (no seed data)
- Backend tests expect a running PostgreSQL at `localhost:25432` with the dev init applied

### Testing

Frontend tests live in `web/tests/`. Shared test helpers are in `web/tests/testUtils.tsx`:
- `renderWithApp` – wraps components with QueryClient, Router, CurrentUserContext, Toast/Dialog providers
- `createWorkflowSummary`, `createTaskWithWorkflow`, `createRequirementSnapshot` – typed fixture builders
- `createAdminUser`, `createAdminDepartmentAssignment` – admin fixture builders (full type shape)

Backend tests are in `api/API.Tests/`.
