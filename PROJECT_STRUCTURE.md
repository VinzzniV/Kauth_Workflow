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
Grundschema fuer Stammdaten, Workflow-Laufzeit, Directory-Projektion, Rollen-/Permission-Modell und Runtime-Konfiguration.

`02_bootstrap.sql`
Produktiver Bootstrap ohne Dev-Spezifika.

`02_seed.sql`
Dev-Wrapper fuer produktiven Bootstrap plus lokale Defaults.

`03_*.sql` bis `40_*.sql`
Historische Migrationen und Erweiterungen fuer Task-Layer, Prozessarten, Identity-/People-Trennung, Gruppen-/Permission-Modell und Runtime-Konfiguration.

`41_workflow_definition_layer.sql`, `42_workflow_runtime_layer.sql`, `43_workflow_definition_mappings.sql`
Inkrementelle Einfuehrung von Definition Layer, paralleler Runtime und ersten publizierten Legacy-Mappings fuer `onboarding`, `offboarding` und `department_change`.

`90_dev_defaults.sql`
Lokale Entwicklungs-Defaults.

`init/dev/00_init.sql`, `init/prod/00_init.sql`
Init-Reihenfolgen fuer Dev und Production.

Hinweis:
Das neue Ziel-Datenmodell fuer Definition Layer, Runtime Events und Automation Layer ist noch nicht vollstaendig im Repo verankert und wird inkrementell eingefuehrt.

## Root-Dokumente

`LEGACY_WORKFLOW_MAPPING.md`
T6-Mapping-Artefakt fuer die ersten drei Legacy-Prozesse auf publizierte Workflow-Definitionen.
