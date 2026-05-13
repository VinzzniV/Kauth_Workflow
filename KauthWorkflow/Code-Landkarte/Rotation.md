# Code-Karte · Rotation

#code-landkarte #rotation

Abteilungsdurchläufe / Department-Rotation: Pläne, Stationen, automatisch generierte Tasks pro Aufenthalt, Notifications. Eigenes Repository-Slice (`PostgresRotationRepository`), aus dem Monolithen herausgeschnitten.

---

## Backend

**Endpoints**
- `api/API/Endpoints/RotationPlanningEndpoints.cs` — Plan-CRUD
- `api/API/Endpoints/AdminRotationConfigEndpoints.cs` — Template-Pflege (Departments × Action-Templates)

**Services**
- `api/API/Services/RotationPlanningService.cs` — Plan-Operationen
- `api/API/Services/RotationTemplateAdminService.cs` — Template-Konfiguration
- `api/API/Services/RotationTaskGenerationService.cs` — Generiert Tasks pro Station beim Eintritt in eine Phase
- `api/API/Services/RotationNotificationService.cs` — Erinnerungen / Upcoming-Change-Notifications
- `api/API/Services/RotationNotificationHostedService.cs` — täglicher Background-Worker

**Repositories**
- `api/API/Repositories/PostgresRotationRepository.cs` — Hauptklasse (partial)
- `api/API/Repositories/PostgresRotationRepository.PlanOperations.cs` — Plan-CRUD
- `api/API/Repositories/PostgresRotationRepository.TemplateOperations.cs` — Action-Templates
- `api/API/Repositories/PostgresRotationRepository.TaskGenerationOperations.cs` — Task-Generierung
- `api/API/Repositories/PostgresRotationRepository.HistoryOperations.cs` — Audit-Log
- `api/API/Repositories/PostgresRotationRepository.NotificationOperations.cs` — Notification-Reads

## Frontend

**Pages**
- `web/src/pages/RotationPlanningPage.tsx` — Plan-Übersicht / Anlage
- `web/src/pages/RotationPlanDetailPage.tsx` — Plan-Detail mit Stationen
- `web/src/pages/RotationOperationsPage.tsx` — Operativer Blick (laufende Rotationen)
- `web/src/pages/RotationTaskDetailPage.tsx` — Einzel-Task
- `web/src/pages/AdminRotationConfigPage.tsx` — Template-Konfig

**Komponenten**
- `web/src/components/rotation/RotationCalendarView.tsx`, `RotationStationTimeline.tsx`, `RotationStationFormCard.tsx`, `StationDateRangePicker.tsx`, `RotationNotificationsPanel.tsx`, `RotationAuditLog.tsx`

## DB

`rotation_plans`, `rotation_stations`, `rotation_generated_tasks`, `rotation_task_assignments`, `rotation_task_comments`, `rotation_notifications`, `rotation_audit_log`, `department_action_templates`

## Tests

- `api/API.Tests/RotationPlanning*Tests.cs`
- `api/API.Tests/RotationTaskGeneration*Tests.cs`
- `api/API.Tests/RotationNotificationServiceTests.cs`
- `api/API.Tests/RotationTaskRegenerationEngineTests.cs`

## Cross-Links

- Domäne: [[Rotation]]
- Architektur: [[Migrationspfad]] § Repository-Schnitt CLA-3 (Rotation als erstes Slice herausgeschnitten)
- Verwandt: [[Tasks-und-Approvals]] (Rotation hat eigenes Task-Modell, parallel zum Workflow-Task-Modell), [[Notifications-und-Mail]] (Upcoming-Change-Mails)
