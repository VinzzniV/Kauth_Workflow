# Projektstruktur

Diese Uebersicht beschreibt die aktuell relevante Struktur des Repositories.
Sie nennt den Ist-Stand des Codes und markiert die wichtigsten Leitdokumente fuer die Migration zur Workflow-Plattform.

## Root

`DOCS_CONTROL.md`
Steuerungsdatei fuer Doku-Lesereihenfolge, Schreibziele und Pflege-Regeln.

`PROJECT_CONTEXT.md`
Stabile Projektwahrheit und fachliche Guardrails fuer die Workflow-Plattform.

`Workflow_Plattform_Implementation_Plan.md`
Zentrale Umsetzungsanweisung fuer die Migration auf Definition Layer, Runtime und Automation Layer.

`PRODUCTIVE_TARGET_ARCHITECTURE.md`
Stabiles Sollbild der Plattformarchitektur.

`DECISIONS.md`
Langfristige Architektur- und Produktentscheidungen.

`MEMORY.md`
Kurzlebiges Arbeitsgedaechtnis fuer naechste Sessions.

`ENGINEERING_RULES.md`
Technische Arbeitsregeln fuer inkrementelle, migrationssichere Umsetzung.

`TODO.md`
Priorisierter Umsetzungs-Backlog entlang der Plattformphasen.

`SETUP.md`
Operative Doku fuer lokale Entwicklung und Linux-Deployment.

`PRODUCTION_CHECKLIST.md`
Kurze Deploy-Checkliste fuer produktive Umgebungen.

`CLAUDE.md`
Hinweise fuer KI-Zusammenarbeit im Repo.

`compose.yml`, `compose.dev-db.yml`, `compose.prod.yml`
Compose-Basis und Overlays fuer Dev-DB sowie produktionsnahen Stack.

`.env.prod.example`
Vorlage fuer produktive Laufzeitkonfiguration.

`db/`
Schema, Bootstrap, historische Migrationen und Init-Reihenfolge.

`api/`
Backend-Solution, API-Projekt und Backend-Tests.

`web/`
Frontend-Projekt auf Basis von React, Vite und React Query.

## Frontend: `web/`

`README.md`
Frontend-spezifische Orientierung fuer UI-Module, Admin-Bereiche und API-Layer.

`package.json`
Abhaengigkeiten und Skripte fuer `dev`, `build`, `lint`, `test` und `preview`.

`Dockerfile`, `nginx.conf`
Build- und Auslieferungspfad fuer den deployten Web-Container.

`tests/`
Vitest- und React-Testing-Library-Tests.

### `web/src`

`main.tsx`
Startet React, Router, React Query, Auth-/CurrentUser-Kontext, Theme, Toasts und Dialoge.

`App.tsx`
Zentrale App-Huelle mit Routing, Login-Flows und geschuetzten Bereichen.

Wichtige Bereiche:
- `src/auth/`
- `src/pages/`
- `src/components/admin-config/`
- `src/components/workflow-detail/`
- `src/services/`

Hinweis:
Seit T8 lebt der Workflow Builder auf der eigenen Route `/builder`; alte Builder-Einstiege unter `/admin/config?section=builder|templates|answers|defaults` werden dorthin umgeleitet. Die Builder-Logik sitzt primär in `src/pages/WorkflowBuilderPage.tsx`, `src/components/admin-config/`, `src/hooks/useAdminWorkflowBuilder.ts`, `src/hooks/adminWorkflowBuilderModel.ts` und `src/services/adminConfigApi.ts`.
Seit T11 nutzt `/workflows/create` den startbaren Definitionen-Katalog aus `src/services/lookupApi.ts` statt `process_types`; die alten Konfigurationssektionen fuer Process Types, Templates und Answer Defaults sind im Admin-Workspace nicht mehr navigierbar.

## Backend: `api/API`

`Program.cs`
Startpunkt mit Runtime-Konfiguration, Service-Registrierung, Startup-Validierung und Endpunkt-Mapping.

`LifecycleRuntimeSettings.cs`
Zentrale Aufloesung von Auth-, Directory- und Laufzeitwerten.

Wichtige Bereiche:
- `Auth/`
- `Authorization/`
- `Endpoints/`
- `Repositories/`
- `Services/`
- `Contracts/`

Hinweis:
Der aktuelle Code bildet den alten lifecycle-/task-getriebenen Kern noch stark ab.
Definition Layer, Runtime-Orchestrierung und Automation Layer werden gemaess Implementierungsplan schrittweise parallel eingefuehrt.
Seit T11 existiert zusaetzlich ein definition-first Oeffnungspfad ueber `GET /workflow-definitions/startable` und `POST /workflows` mit `workflowDefinitionKey`; Legacy-`processTypeKey` bleibt nur noch als Kompatibilitaetsalias erhalten.

## Backend-Tests: `api/API.Tests`

Unit- und integrationsnahe Tests fuer:
- Authorization
- Endpunkte
- Requirement- und Statusregeln
- Workflow-Repositories
- Audit-Log und Workflow-Links
- Notification-Logik

## Datenbank: `db/`

`01_schema.sql`
Grundschema fuer Stammdaten, Workflow-Laufzeit, Definition Layer, Automation Layer, Directory-Projektion, Rollen-/Permission-Modell und Runtime-Konfiguration.

`02_bootstrap.sql`
Produktiver Bootstrap ohne Dev-Spezifika.

`02_seed.sql`
Dev-Wrapper fuer produktiven Bootstrap plus lokale Defaults.

`03_*.sql` bis `40_*.sql`
Historische Migrationen und Erweiterungen fuer Task-Layer, Prozessarten, Identity-/People-Trennung, Gruppen-/Permission-Modell und Runtime-Konfiguration.

`41_workflow_definition_layer.sql`, `42_workflow_runtime_layer.sql`, `43_workflow_definition_mappings.sql`, `45_automation_layer.sql`
Inkrementelle Einfuehrung von Definition Layer, paralleler Runtime, ersten publizierten Legacy-Mappings fuer `onboarding`, `offboarding` und `department_change` sowie dem ersten Automation Layer mit Action-Katalog, Job-Queue und Ausfuehrungslogs.

`90_dev_defaults.sql`
Lokale Entwicklungs-Defaults.

`init/dev/00_init.sql`, `init/prod/00_init.sql`
Init-Reihenfolgen fuer Dev und Production.

Hinweis:
Das neue Ziel-Datenmodell fuer Definition Layer, Runtime Events und Automation Layer wird inkrementell eingefuehrt; T9 verankert den ersten produktiv nutzbaren Automation-Kern, Builder-UI und echte externe Adapter folgen spaeter.

## Backend-Erweiterungen aus T9

`api/API/Contracts/WorkflowAutomationDtos.cs`
Read-DTOs fuer Action-Katalog sowie Automation-Job-, Attempt- und Log-Ansichten.

`api/API/Services/WorkflowAutomationService.cs`
Koordiniert Job-Claiming, Handler-Ausfuehrung, Retry-Regeln und Runtime-Fortschritt fuer `automation`-Nodes.

`api/API/Services/WorkflowAutomationHostedService.cs`
API-interner Background Worker fuer Polling und Ausfuehrung der Automation-Jobs.

`api/API/Services/SimulatedWorkflowAutomationHandlers.cs`
Simulierte Handler fuer die ersten Plattform-Actions wie `CreateAdUser` und `SendWelcomeMail`.

`api/API/Repositories/PostgresWorkflowRepository.AutomationOperations.cs`
PostgreSQL-Zugriff fuer Action-Katalog, Job-Claiming, Attempt-/Log-Schreibung, Mapping-Aufloesung und Folgejob-Erzeugung.

## Frontend-Erweiterungen aus T8

`web/src/components/admin-config/AdminWorkflowBuilderSection.tsx`
Canvas-first Builder fuer Workflow-Definitionen, Versionen, Nodes, Edges, Validation-Issues und Publish; wird jetzt auf `/builder` als zentrale Arbeitsflaeche gerendert.

`web/src/pages/WorkflowBuilderPage.tsx`
Eigenstaendige Produktseite fuer den Builder mit Header, Rollenmodus (`Builder` vs. `Admin Builder`) und eigener Page-Shell ausserhalb des Admin-Settings-Layouts.

`web/src/hooks/useAdminWorkflowBuilder.ts`
Kapselt Builder-Zustand, Selektion, Dirty-State, Save/Reload, Publish und lokale Guardrails fuer Definitionen und Versionen.

`web/src/hooks/adminWorkflowBuilderModel.ts`
Hilfsmodell fuer Draft-Zustand, Replace-Payloads und lokale Validierung von Nodes, Edges, JSON-Feldern und Automation-Actions.

`web/src/services/adminConfigApi.ts`
Frontend-Client fuer Definition-Layer-Admin-Endpunkte inklusive Definitionen, Versionen, Replace, Publish und Action-Katalog.

## Root-Dokumente

`LEGACY_WORKFLOW_MAPPING.md`
T6-Mapping-Artefakt fuer die ersten drei Legacy-Prozesse auf publizierte Workflow-Definitionen.
