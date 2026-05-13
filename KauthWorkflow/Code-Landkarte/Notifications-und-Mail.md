# Code-Karte · Notifications und Mail

#code-landkarte #notifications #mail

Mail-Versand für Workflow-Lifecycle-Events (Workflow created, Task ready, Workflow completed, Reminder, Overdue, Upcoming Change). Templates konfigurierbar pro Typ; Platzhalter-Whitelist im Catalog. Versand via Microsoft Graph App-only oder SMTP-Fallback.

---

## Backend

**Endpoints**
- `api/API/Endpoints/AdminNotificationTemplateEndpoints.cs` — Template-CRUD

**Services**
- `api/API/Services/NotificationTemplateService.cs` (+ `INotificationTemplateService.cs`) — Catalog + DB-Override-Auflösung
- `api/API/Services/INotificationTemplateResolver.cs` — schmaler Resolver-Vertrag (für Handler)
- `api/API/Services/NotificationTemplateCatalog.cs` — Catalog mit Default-Templates + erlaubten Platzhaltern
- `api/API/Services/NotificationEmailTemplateBuilder.cs` — Platzhalter-Substitution
- `api/API/Services/NotificationEmailConfigurationService.cs` (+ Validator, Options, RuntimeConfiguration) — Mail-Versand-Konfig
- `api/API/Services/INotificationEmailTestSender.cs` — Test-Versand
- `api/API/Services/WorkflowNotificationDispatchService.cs` — Dispatch-Orchestrierung
- `api/API/Services/GraphWorkflowEmailNotificationSender.cs` — Graph-App-only-Versand
- `api/API/Services/IWorkflowEmailNotificationSender.cs` — Versand-Vertrag

**Repositories**
- `api/API/Repositories/PostgresNotificationTemplateRepository.cs` (+ `INotificationTemplateRepository.cs`)
- `api/API/Repositories/PostgresNotificationEmailConfigurationRepository.cs` (+ `INotificationEmailConfigurationRepository.cs`)
- `api/API/Repositories/PostgresWorkflowNotificationDispatchOperations.cs` (+ `IWorkflowNotificationDispatchOperations.cs`)
- `api/API/Repositories/PostgresWorkflowNotificationReadRepository.cs` (+ `IWorkflowNotificationReadRepository.cs`)
- `api/API/Repositories/PostgresWorkflowRepository.NotificationOperations.cs`

## Frontend

**Komponenten**
- `web/src/components/admin-config/AdminNotificationTemplateSection.tsx` — Template-Pflege-UI
- `web/src/components/admin-config/AdminNotificationEmailSection.tsx` — Versand-Konfig (SMTP/Graph)
- `web/src/components/workflow-detail/WorkflowNotificationsPanel.tsx` — Notifications zum Workflow

## DB

`notification_templates`, `notification_email_settings`, `workflow_notifications`

## Tests

- `api/API.Tests/NotificationTemplate*Tests.cs`
- `api/API.Tests/NotificationEmailTemplateBuilderTests.cs`
- `api/API.Tests/WorkflowNotificationDispatch*Tests.cs`
- `api/API.Tests/GraphWorkflowEmailNotificationSenderTests.cs`

## Cross-Links

- Verwandt: [[Automation]] (`SendWelcomeMailGraph` ist ein Automation-Handler, der `INotificationTemplateResolver` nutzt), [[Workflow-Runtime]] (Lifecycle-Events triggern den Dispatch), [[Rotation]] (Upcoming-Change-Mails)
