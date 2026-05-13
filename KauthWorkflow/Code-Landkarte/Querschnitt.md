# Code-Karte · Querschnitt

#code-landkarte #querschnitt

Cross-Cutting Concerns: Audit-Log, System-Events, Health-Reads, Background-Sweeper, Client-System-Log. Nichts davon ist fachlich einem Bereich zugeordnet — diese Komponenten dienen Operations und Diagnose.

---

## Audit & System-Events

**Endpoints**
- `api/API/Endpoints/AdminSystemLogEndpoints.cs` — System-Event-Log lesen
- `api/API/Endpoints/ClientSystemLogEndpoints.cs` — Frontend-Errors ans Backend posten

**Services**
- `api/API/Services/SystemEventLogService.cs` (+ `ISystemEventLogService.cs`) — System-Event-Schreiben/Lesen

**Repositories**
- `api/API/Repositories/PostgresWorkflowAuditWriteOperations.cs` (+ `IWorkflowAuditWriteOperations.cs`)
- `api/API/Repositories/PostgresWorkflowAuditReadRepository.cs` (+ `IWorkflowAuditReadRepository.cs`)

**Frontend**
- `web/src/components/admin-config/AdminSystemLogSection.tsx`
- `web/src/components/feedback/AppErrorBoundary.tsx` — meldet Errors an `/client-system-logs`

**DB**: `system_event_log`, `workflow_audit_log`, `auth_permission_audit_log`, `directory_mapping_audit_log`, `person_match_audit_log`, `rotation_audit_log`

## Runtime-Health & Configuration

**Endpoints**
- `api/API/Endpoints/AdminRuntimeConfigEndpoints.cs` — Runtime-Konfiguration anzeigen
- `api/API/Endpoints/AdminRuntimeHealthEndpoints.cs` — Health-Checks für Dashboard

**Services**
- `api/API/Services/AdminRuntimeHealthService.cs` (+ `IAdminRuntimeHealthService.cs`) — Sammelt Health-Status für Admin-Dashboard

**Frontend**
- `web/src/components/admin-config/AdminSystemConfigurationSection.tsx`
- `web/src/components/admin-config/AdminGraphApplicationSection.tsx`
- `web/src/components/dashboard/DashboardAdminRuntimeHealthBlock.tsx`

## Background-Services (Hosted)

Alle laufen als `IHostedService` im API-Prozess:

- `api/API/Services/WorkflowAutomationHostedService.cs` — Job-Polling (→ [[Automation]])
- `api/API/Services/StaleWorkerClaimSweeper.cs` — verwaiste Worker-Claims (→ [[Automation]])
- `api/API/Services/ExternalAutomationJobCompletionSweeper.cs` — externe Completions (→ [[Automation]])
- `api/API/Services/DirectorySyncHostedService.cs` — Entra-Sync-Zyklus (→ [[People-und-360-Karte]])
- `api/API/Services/RotationNotificationHostedService.cs` — täglicher Rotation-Notification-Worker (→ [[Rotation]])

## Dashboard & Overview

**Pages**
- `web/src/pages/DashboardPage.tsx`

**Komponenten**
- `web/src/components/dashboard/DashboardOverview.tsx`, `DashboardAdminOverview.tsx`, `PersonaSwitcher.tsx`, `DashboardAdminRuntimeHealthBlock.tsx`

## Cross-Links

- Domäne: keine direkte Domain-Doku (Querschnitt ist Operations-Thema)
- Verwandt: alle Bereiche, je nach Sweeper/Service-Zuordnung
