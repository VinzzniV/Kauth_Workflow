# Projektstruktur

Diese Uebersicht beschreibt die aktuell vorhandene Struktur des Repositories. Fokus sind produktive Quelltexte und relevante Infrastrukturdateien. Build-Artefakte wie `node_modules/`, `web/node_modules/`, `api/API/bin/` oder `api/API/obj/` werden bewusst nicht im Detail beschrieben.

## Root

`PROJECT_STRUCTURE.md`
Diese Detailuebersicht.

`PRODUCTIVE_TARGET_ARCHITECTURE.md`
Sollbild fuer produktive Architektur, Auth, Rollenmodell, Datenmodell und Migrationspfad.

`compose.yml`
Gemeinsame Container-Basis fuer `db`, `api` und `web`.

`compose.dev-db.yml`
Lokale Entwicklungs-Ergaenzung nur fuer PostgreSQL mit Dev-Init und Host-Port.

`compose.prod.yml`
Servernahes Override fuer Linux-Deployment mit Entra-Auth und Caddy-Reverse-Proxy.

`.env.prod.example`
Vorlage fuer die serverseitigen Pflicht- und Optional-Variablen.

`SETUP.md`
Zentrale operative Doku fuer lokale Entwicklung und Linux-Deployment.

`db/`  
SQL-Dateien fuer Schema und Demo-/Seed-Daten.

`api/`  
Backend-Solution und API-Projekt.

`web/`  
Frontend-Projekt auf Basis von React und Vite.

## Frontend: `web/`

`package.json`  
Frontend-Abhaengigkeiten und Skripte fuer `dev`, `build`, `lint` und `preview`.

`Dockerfile`  
Mehrstufiges Image: Build auf `node:20-alpine`, Auslieferung ueber `nginx:1.27-alpine` mit Runtime-Config ueber `app-config.js`.

`nginx.conf`  
SPA-Fallback fuer React-Routing, Proxy auf `/api/` und `/health` sowie No-Cache-Regeln fuer `app-config.js`.

`vite.config.ts`  
Vite-Konfiguration mit React- und Tailwind-Plugin.

`index.html`  
HTML-Einstiegspunkt der Single-Page-Application.

### `web/src`

`main.tsx`  
Bindet `AuthProvider`, `CurrentUserProvider` und `BrowserRouter` ein.

`App.tsx`  
Zentrale Routen, Layout-Einbindung, Demo-Access-Route und Feature-Guards.

`index.css`  
Globale Styles der Anwendung.

### `web/src/auth`

`AuthContext.tsx`  
Verwaltet aktuell Demo-Login, Logout, Session-Wiederherstellung und den globalen Auth-Status. Produktiv ist hier ein Entra-basiertes Auth-Modell vorgesehen.

`CurrentUserContext.tsx`  
Leitet Rollen, Labels, Features und Default-Route aus dem angemeldeten Benutzer ab.

`IdentityProvider.ts`  
Frontend-Abstraktion fuer Demo-Login und Entra-Login. Liest den Auth-Modus ueber die zentrale Runtime-Config.

`roleModel.ts`  
Rollenkeys, Feature-Matrix und Standardrouten fuer die UI.

### `web/src/navigation`

`RouteGuard.tsx`  
Schuetzt Seiten anhand der freigegebenen Features des aktuellen Benutzers.

`useRoleAwareNavigation.ts`  
Baut Sidebar-Navigation, Dashboard-Aktionen und Persona-Texte rollenabhaengig auf.

### `web/src/pages`

`DemoLoginPage.tsx`  
Startseite ohne Session; Demo-Benutzer koennen direkt ausgewaehlt werden.

`DemoAccessPage.tsx`  
Token-basierter Einstieg fuer Benachrichtigungslinks; setzt eine Session und leitet in die App weiter.

`DashboardPage.tsx`  
Rollenabhaengiger Startbereich.

`CreateWorkflowPage.tsx`  
Formular zum Anlegen eines neuen Onboardings.

`WorkflowListPage.tsx`  
Uebersicht der sichtbaren Onboarding-Faelle. Fuer Abteilungsleitungen ist die Sicht auf die eigenen Abteilungen begrenzt; standardmaessig werden laufende statt aller Faelle angezeigt.

`WorkflowSearchPage.tsx`  
Freie Suche und Filter ueber Onboarding-Faelle nach Name, Abteilung, Stelle, Personalnummer oder ID.

`WorkflowDetailPage.tsx`  
Detailansicht mit Anforderungen, Prozessstand, Aufgaben nach Bereichen und Notification-Historie.

`SupervisorStepPage.tsx`  
Arbeitsbereich der Abteilungsleitung fuer den Supervisor-Schritt mit bedingten Anforderungen, z. B. Referenzuser, Hardware-Auswahl und Laufwerksrechten.

`MyTasksPage.tsx`  
Persoenlicher Aufgabenarbeitsplatz des Fachbereichs.

`AdminConfigPage.tsx`
Verwaltet aktuell Benutzer, Rollen, Gruppen, Abteilungen, Verantwortlichkeiten und Notification-E-Mail-Einstellungen. Das Zielbild verschiebt den Fokus weg von User-CRUD hin zu Gruppen-Mapping, Verantwortlichkeiten, Sync-Status und Ausnahmen.

`PersonWorkflowHistoryPage.tsx`
Zeigt alle Workflows einer Person (nach Personen-ID) in chronologischer Reihenfolge.

### `web/src/components`

`dashboard/`  
Rollenspezifische Dashboard-Bausteine, aktuell `DashboardOverview.tsx`.

`feedback/`  
Generische Lade- und Leerstates (`LoadingState.tsx`, `EmptyState.tsx`).

`layout/`  
App-Shell und Seitenkopf (`AppLayout.tsx`, `PageHeader.tsx`).

`workflows/`  
Wiederverwendbare Form- und Anzeigekomponenten fuer Rollen-, Anforderungs-, Icon- und Workflow-Daten.

### `web/src/services`

`lifecycleApi.ts`
Zentraler HTTP-Client fuer Demo-Auth, Stammdaten, Workflow-, Aufgaben- und Admin-Endpunkte.

`onboardingApi.ts`
Kompatibilitaets-Re-Export auf `lifecycleApi.ts` fuer alte Importpfade.

`api/backendDtos.ts`
Rohe Backend-Antworttypen, 1:1 zu den Backend-DTOs.

`api/mappers.ts`
Transformiert Backend-DTOs in Frontend-Typen.

`api/client.ts`
Basis-HTTP-Client mit einheitlichem Fehlerhandling.

### `web/src/hooks`

`useRoles.ts`  
Laedt Abteilungen und Rollen fuer Auswahl- und Verwaltungsoberflaechen.

`useWorkflowCreation.ts`  
Kapselt Formularzustand, Validierung und das Senden neuer Workflows.

`useRows.ts`  
Laedt und filtert Workflow-Listen fuer Uebersichten.

### `web/src/types`

`workflow.ts`  
Frontend-Modelle fuer Workflow-Konfiguration, Workflow-Details, Aufgaben und Notifications.

`auth.ts`  
Typen fuer Demo-Login, aktuellen Benutzer und Admin-Stammdaten.

### `web/src/utils`

`iconRegistry.ts`  
Ordnet fachliche Icon-Keys den gebuendelten Assets zu.

`taskAssignment.ts`  
Hilfslogik fuer Aufgaben-Zuweisungen.

`taskStatus.ts`  
Hilfslogik fuer Statusdarstellung und Statuslabels.

### `web/src/assets/icons`

Bild- und Icon-Dateien fuer Branding sowie fachliche Anforderungen und Aufgaben.

## Backend: `api/API`

`API.csproj`  
ASP.NET Core 8 Projekt mit Npgsql, Microsoft Graph und Swagger.

`API.sln`  
Solution-Datei fuer die API.

`Program.cs`  
Registriert Services, Swagger und alle Minimal-API-Endpunkte.

`appsettings.json`  
Default-Konfiguration fuer Notification-E-Mails; Versand ist initial deaktiviert.

`Dockerfile`  
Container-Build und Publish fuer die API.

`Properties/launchSettings.json`  
Lokale Startprofile fuer Entwicklung in IDE oder CLI.

### `api/API/Auth`

`CurrentUser/`  
Liefert den aufgeloesten aktuellen Benutzer und den Request-Kontext.

`Identity/`  
Abstraktion zur Ermittlung der Identitaet aus eingehenden Requests. Aktuell demo-lastig, produktiv auf Entra/OIDC auszurichten.

`Resolvers/`  
Konkrete Resolver fuer Demo-Header und Demo-Session-Token. Diese sind Entwicklungs-/Demo-Helfer und kein produktives Zielmodell.

`Sessions/`  
In-Memory-Speicher fuer Demo-Sessions.

### `api/API/Authorization`

`AuthorizationRoles.cs`  
Zentrale Rollenschluessel der Anwendung.

`IAuthorizationPolicyService.cs`  
Interface fuer Rollen- und Aufgabenfreigaben.

`AuthorizationPolicyService.cs`  
Implementiert Zugriffsregeln fuer Views, Workflows, Supervisor-Schritt, Aufgaben und Admin-Bereich, einschliesslich effektiver Verantwortlichkeiten und Beobachtungsrechten fuer Abteilungsleitungen.

### `api/API/Contracts`

`WorkflowDtos.cs`  
DTOs fuer Konfiguration, Erstellung, Listen, Detailansichten, Anforderungen, Aufgaben und Notifications.

`AuthModels.cs`  
DTOs und Domaintypen fuer Identitaet, aktuellen Benutzer, Demo-Login und Admin-Konfiguration.

### `api/API/Repositories`

`IWorkflowRepository.cs`  
Vertrag fuer Workflow-, Aufgaben- und Konfigurationszugriffe.

`PostgresWorkflowRepository.cs`  
PostgreSQL-Implementierung fuer Workflow-Lebenszyklus, Anforderungslogik, Aufgaben, Abhaengigkeiten, Creator-Tracking und Notifications.

`IUserAuthorizationRepository.cs`  
Vertrag fuer Benutzer-, Rollen-, Gruppen- und Stammdatenzugriffe.

`PostgresUserAuthorizationRepository.cs`  
Liest und pflegt aktuell Benutzer, Rollen, Gruppen, Abteilungen und Verantwortlichkeiten. Produktiv darf die Anwendung Identitaeten und Gruppen nicht primaer selbst besitzen.

`INotificationEmailConfigurationRepository.cs`  
Vertrag fuer Notification-E-Mail-Konfiguration.

`PostgresNotificationEmailConfigurationRepository.cs`  
Persistiert Mailversand-Konfiguration und Teststatus in PostgreSQL.

### `api/API/Services`

`ISupervisorStepService.cs`  
Vertrag fuer den Supervisor-Schritt.

`PostgresSupervisorStepService.cs`  
Laedt zugewiesene Workflows der Abteilungsleitung und verarbeitet deren Rueckmeldungen.

`INotificationEmailConfigurationService.cs`  
Fachservice fuer Lese- und Schreibzugriffe auf die Mailkonfiguration.

`NotificationEmailConfigurationService.cs`  
Validiert, normalisiert und speichert Notification-E-Mail-Einstellungen.

`NotificationEmailConfigurationValidator.cs`  
Prueft Vollstaendigkeit und Gueltigkeit der Mailkonfiguration.

`NotificationEmailOptions.cs`  
Bindet Default-Werte aus `appsettings.json`.

`NotificationEmailRuntimeConfiguration.cs`  
Gemeinsames Laufzeitmodell fuer Mailversand und Tests.

`IWorkflowEmailNotificationSender.cs`  
Vertrag fuer Workflow-Benachrichtigungen.

`INotificationEmailTestSender.cs`  
Vertrag fuer Testmails.

`GraphWorkflowEmailNotificationSender.cs`  
Versendet gebuendelte Aufgaben- und Abschlussbenachrichtigungen ueber Microsoft Graph. Demo-Zugangslinks sind nur fuer Demo/Dev akzeptabel und muessen fuer Produktivbetrieb entfallen.

### Endpunkte in `Program.cs`

- Demo-Auth und aktueller Benutzer: `/auth/demo-users`, `/auth/demo-login`, `/auth/demo-logout`, `/me`, `/auth/current-user`
- Stammdaten fuer das Frontend: `/departments`, `/roles`, `/requirements`, `/workflow-config`
- Admin-Konfiguration: `/admin/config/workflow`, `/admin/config/notification-email`, `/admin/config/notification-email/test`
- Admin-Stammdaten und Rechte: `/admin/auth/*`, `/admin/master-data/*`
- Workflow und Aufgaben: `/workflows`, `/workflows/{uid}`, `/workflows/{uid}/tasks`, `/workflows/supervisor-step`, `/workflows/{uid}/supervisor-step`, `/tasks`, `/tasks/{id}`, `/tasks/{id}/status`, `/tasks/{id}/assign`

## Datenbank: `db/`

`01_schema.sql`  
Definiert das Datenmodell fuer:

- Stammdaten: `departments`, `app_roles`, `app_users`, `people`, `app_groups`, `app_responsibilities`, `department_settings`, `system_responsibilities`
- Rechtezuweisungen: `app_user_roles`, `app_user_groups`, `app_group_roles`, `app_user_responsibilities`, `app_group_responsibilities`
- Workflow-Definition: `workflow_answer_definitions`, `workflow_answer_options`, `app_role_answer_defaults`, `app_role_answer_default_options`, `task_templates`, `task_template_conditions`, `task_template_dependencies`
- Workflow-Laufzeit: `workflows`, `workflow_answers`, `workflow_answer_selected_options`, `workflow_tasks`, `workflow_task_dependencies`, `task_assignments`, `workflow_notifications`
- Benachrichtigungskonfiguration: `notification_email_settings`

`02_bootstrap.sql`  
Produktiver Bootstrap fuer Referenzdaten und Onboarding-Grundkonfiguration ohne Demo-Benutzer oder localhost-Defaults.

`02_seed.sql`  
Dev-Wrapper, der `02_bootstrap.sql` und `90_demo_seed.sql` gemeinsam laedt.

`90_demo_seed.sql`  
Demo-/Dev-Ergaenzungen wie Demo-Benutzer, Gruppen, Zuordnungen und lokale Mail-Defaults.

`init/dev/00_init.sql` und `init/prod/00_init.sql`  
Definieren die feste Init-Reihenfolge fuer Dev bzw. Production.

Die Seed-/Bootstrap-Dateien enthalten zusammen:

- bedingte Anforderungsdefinitionen fuer Referenzuser, Hardware, Laptop-VPN-Variante und Laufwerksrechte
- fachliche Zustaendigkeiten fuer IT, QS, AV und QMB
- Task-Generierungsregeln aus Anforderungen
- Backfill-Logik fuer fehlende Aufgaben und Beschreibungen in bereits offenen Workflows

## Nicht im Fokus dieser Uebersicht

- `node_modules/` und `web/node_modules/`
- `web/dist/` als generierbares Build-Artefakt
- `api/API/bin/` und `api/API/obj/`
