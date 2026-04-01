# Projektstruktur

Diese Uebersicht beschreibt die aktuell relevante Struktur des Repositories. Fokus sind produktive Quelltexte, Tests und betriebsrelevante Infrastrukturdateien. Generierbare Artefakte wie `node_modules/`, `web/dist/`, `api/API/bin/` oder `api/API/obj/` werden bewusst nicht im Detail beschrieben.

## Root

`DOCS_CONTROL.md`
Steuerungsdatei fuer Doku-Lesereihenfolge, Schreibziele und Pflege-Regeln.

`PROJECT_CONTEXT.md`
Stabile Projektwahrheit, Leitplanken und fachliche Guardrails.

`MEMORY.md`
Kurzlebiges Arbeitsgedaechtnis fuer naechste Sessions.

`DECISIONS.md`
Langfristige Architektur- und Produktentscheidungen.

`ENGINEERING_RULES.md`
Technische Arbeitsregeln, inklusive Doku-Hygiene.

`TODO.md`
Groesserer Produktions- und Release-Backlog.

`FRONTEND_TODO.md`
Frontend-spezifische UI-Guardrails und Review-Kriterien.

`PRODUCTIVE_TARGET_ARCHITECTURE.md`
Sollbild fuer produktive Architektur und Migrationsrichtung.

`SETUP.md`
Operative Doku fuer lokale Entwicklung und Linux-Deployment.

`compose.yml`
Gemeinsame Compose-Basis fuer `db`, `api` und `web`.

`compose.dev-db.yml`
Lokales Override fuer PostgreSQL mit Dev-Init und Host-Port.

`compose.prod.yml`
Produktionsnahes Override mit Entra-Auth, produktivem DB-Init und Caddy-Reverse-Proxy.

`.env.prod.example`
Vorlage fuer produktive Laufzeitkonfiguration.

`deploy/Caddyfile`
HTTPS-Reverse-Proxy fuer den produktiven Stack.

`db/`
Schema, Bootstrap, Migrationen und Init-Reihenfolge fuer Dev und Production.

`api/`
Backend-Solution, API-Projekt und Backend-Tests.

`web/`
Frontend-Projekt auf Basis von React, Vite und React Query.

## Frontend: `web/`

`package.json`
Frontend-Abhaengigkeiten und Skripte fuer `dev`, `build`, `lint`, `test` und `preview`.

`Dockerfile`
Mehrstufiges Image fuer Build und Auslieferung ueber Nginx.

`nginx.conf`
SPA-Fallback, Proxy fuer `/api/` und Runtime-Config-Auslieferung.

`README.md`
Frontend-spezifische Orientierung fuer Module, Einstiegspunkte und Service-Layer.

`tests/`
Vitest- und React-Testing-Library-Tests fuer Seiten, Komponenten und Hilfsmodelle.

### `web/src`

`main.tsx`
Startet React, Router, React Query, Auth-/CurrentUser-Kontext, Theme, Toasts und Dialoge.

`App.tsx`
Zentrale App-Huelle mit Login-Routing, Layout und Feature-Guards.

`index.css`
Globaler CSS-Einstieg, der die Styleschichten zusammenzieht.

### `web/src/auth`

`AuthContext.tsx`
Verwaltet Session-Wiederherstellung, Login-Zustand und Logout fuer `dev-sim` oder Entra.

`CurrentUserContext.tsx`
Leitet Rollen, Permissions, Persona und Default-Route aus dem aktuellen Benutzer ab.

`IdentityProvider.ts`
Abstraktion ueber `dev-sim` und Entra.

`EntraIdentityProvider.ts`
MSAL-basierte Entra-Integration.

`msalConfig.ts`
MSAL-Konfiguration aus Runtime-Config.

`roleModel.ts`
Rollenkeys, Permissions, Features und Default-Routen der UI.

### `web/src/pages`

`SimulationLoginPage.tsx`
Lokale Login-Seite fuer Entwicklersimulation auf Basis synchronisierter Verzeichnisidentitaeten.

`EntraLoginPage.tsx`
Login-Entry fuer Entra-Modus.

`DashboardPage.tsx`
Rollenspezifischer Startbereich.

`CreateWorkflowPage.tsx`
Erstellung neuer Lifecycle-Vorgaenge.

`WorkflowListPage.tsx`
Gefilterte Uebersicht sichtbarer Workflows.

`WorkflowSearchPage.tsx`
Freie Suche ueber Vorgaenge.

`WorkflowDetailPage.tsx`
Detailansicht mit Anforderungen, Aufgaben, Audit-Log, Benachrichtigungen und Verknuepfungen.

`SupervisorStepPage.tsx`
Arbeitsbereich fuer Abteilungsleitungen.

`MyTasksPage.tsx`
Persoenlicher Aufgabenarbeitsplatz fuer Fachbereiche.

`PersonWorkflowHistoryPage.tsx`
Chronologische Historie aller Vorgaenge einer Person.

`AdminConfigPage.tsx`
Administration fuer Organisation, Konfiguration, Zugriffe, Verzeichnis-Sync, System und Massenaktionen.

### `web/src/components`

`admin-config/`
Admin-Workspaces fuer Organisation, Templates, Antwortfelder, Standardwerte, Rechte, Directory-Sync, System und Bulk-Operationen.

`workflow-detail/`
Bausteine fuer Detailansicht, Audit-Log, Links, Management und Aufgabenbereiche.

`workflows/`
Wiederverwendbare Komponenten fuer Erstellung, Aufgabenstatus, Kommentare und Workflow-Karten.

`dashboard/`
Dashboard-Bausteine und Insight-Modelle.

`layout/`
App-Shell und Seitenkopf.

`feedback/`
Loading-, Empty-, Toast- und Dialog-Komponenten.

`ui/`
Kleinere generische UI-Bausteine.

### `web/src/services`

`authApi.ts`
Login, Logout, Session-Token und aktueller Benutzer.

`workflowApi.ts`, `taskApi.ts`, `peopleApi.ts`, `lookupApi.ts`
Fachliche API-Module fuer Workflows, Aufgaben, Personen und Lookup-Daten.

`adminApi.ts`, `adminConfigApi.ts`
Admin-, Permission-, Directory- und Runtime-Konfigurations-Endpunkte.

`services/api/`
Basis-Client, Backend-DTOs und Mapping-Schicht.

`services/queries/`
React-Query-Lesezugriffe.

`services/mutations/`
React-Query-Schreiboperationen.

`queryClient.ts`, `queryKeys.ts`, `cache.ts`
Clientseitige Query-Infrastruktur.

### `web/src/hooks`

Form-, Admin- und Workflow-Hooks wie:
- `useWorkflowCreation.ts`
- `useRows.ts`
- `useTaskInteraction.ts`
- `useRequirementEditor.ts`
- `useAdmin*`

### `web/src/theme`, `web/src/styles`, `web/src/utils`, `web/src/types`

`theme/`
Theme-Aufloesung und ThemeProvider.

`styles/`
Globale Style-Schichten fuer Basis, Admin, Dashboard, Komponenten und Workflows.

`utils/`
Formatierungs- und Hilfslogik fuer Status, Aufgaben, Anforderungen und Icons.

`types/`
Frontend-Domaenenmodelle fuer Auth und Workflows.

## Backend: `api/API`

`API.csproj`
ASP.NET Core 8 Webprojekt mit Npgsql, Microsoft Identity Web, Microsoft Graph und Swagger.

`Program.cs`
Startpunkt mit optionalem `.env.prod`-Fallback, Service-Registrierung, Startup-Validierung und Endpunkt-Mapping.

`LifecycleRuntimeSettings.cs`
Zentrale Aufloesung von Auth-Modus, Connection String und Directory-/Entra-Laufzeitwerten.

`appsettings.json`
Default-Werte fuer Notification-Laufzeitkonfiguration.

`Properties/launchSettings.json`
Lokales Startprofil fuer API-Entwicklung mit `dev-sim`.

### `api/API/Auth`

`CurrentUser/`
Aktueller Benutzer und Request-Kontext.

`Identity/`
Abstraktion ueber eingehende Identitaeten.

`Resolvers/`
Resolver fuer Entra-Tokens und Dev-Simulations-Sessions.

`Sessions/`
In-Memory-Speicher fuer Dev-Simulations-Sessions.

### `api/API/Authorization`

Zentrale Rollen-, Permission- und Policy-Logik.

### `api/API/Endpoints`

Minimal-API-Module fuer:
- Auth und aktueller Benutzer
- Admin-Runtime-Konfiguration
- Admin-Organisation, Permissions und Directory-Sync
- Workflow-Stammdaten
- Workflow-Lifecycle, Suche, Historie, Archivierung und Loeschung
- Supervisor-Schritt
- Workflow-Verknuepfungen
- Aufgaben, Status, Assignment und Kommentare

### `api/API/Services`

Fach- und Infrastrukturservices wie:
- `PostgresSupervisorStepService`
- `EntraDirectorySyncService`
- `DirectorySyncHostedService`
- `NotificationEmailConfigurationService`
- `GraphApplicationConfigurationService` als read-only Runtime-Provider fuer Tenant/Client/Secret-Status
- `GraphWorkflowEmailNotificationSender`

### `api/API/Repositories`

PostgreSQL-Repositories fuer:
- Workflows, Aufgaben, Audit, Verknuepfungen und Konfiguration
- Benutzer, Rollen, Gruppen, Permissions und Verantwortlichkeiten
- Notification-E-Mail-Konfiguration

### `api/API/Contracts`

DTOs fuer Workflows, Auth, Directory-Sync und Admin-Modelle.

## Backend-Tests: `api/API.Tests`

Enthaelt Unit- und Integrationsnahe Tests fuer:
- Authorization
- Endpunkte
- Requirement- und Statusregeln
- Workflow-Repositories
- Audit-Log und Workflow-Links
- Notification-Template-Logik

## Datenbank: `db/`

`01_schema.sql`
Grundschema fuer Stammdaten, Workflow-Laufzeit, Directory-Projektion, Rollen-/Permission-Modell und Runtime-Konfiguration.

`02_bootstrap.sql`
Produktiver Bootstrap fuer Basisdaten ohne Demo-Benutzerwelt.

`02_seed.sql`
Dev-Wrapper, der produktiven Bootstrap plus lokale Defaults laedt.

`02_reset.sql`
Reset-/Neuaufbau-Helfer fuer reproduzierbare Initialisierung.

`03_*.sql` bis `39_*.sql`
Historische Migrationen und Facherweiterungen, u. a.:
- Backfills
- Task-Hardening
- Prozessarten
- Workflow-Links
- Archivierung
- Directory-Tabellen
- Identity-/People-Trennung
- Permission-Modell
- historische Graph-Anwendungseinstellungen

`40_remove_secret_persistence.sql`
Entfernt persistierte Graph-/Secret-Spalten und baut das Runtime-Only-Modell fuer produktive Secrets fest ein.

`90_dev_defaults.sql`
Lokale Entwicklungs-Defaults ohne kuenstliche Demo-Benutzer oder Demo-Gruppen.

`init/dev/00_init.sql`
Dev-Init-Reihenfolge mit Seed-Datei.

`init/prod/00_init.sql`
Produktive Init-Reihenfolge ohne Dev-Seed.

## Nicht im Fokus dieser Uebersicht

- `node_modules/` und `web/node_modules/`
- `web/dist/`
- `api/API/bin/` und `api/API/obj/`
