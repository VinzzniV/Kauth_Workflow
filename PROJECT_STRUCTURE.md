# PROJECT_STRUCTURE.md

## Zweck

- aktuelles Dateilayout des Repos knapp erklaeren
- Einstiegspunkte fuer Backend, Frontend, DB und Doku zeigen

## Primaerquelle fuer

- Repo-Struktur
- wichtige Root-Dateien
- technische Startpunkte im Code

## Nicht verwenden fuer

- Produktregeln
- Review-Priorisierung
- Session-Notizen

## Wann aktualisieren

- bei neuen Ordnern, neuen Entry-Points oder umbenannten Modulen
- wenn sich technische Einstiegspunkte sichtbar verschieben

## Verwandte Dateien

- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `CODE_REVIEW.md`
- `KauthWorkflow/00 Start.md`

---

# Projektstruktur

Diese Uebersicht beschreibt die aktuell relevante Struktur des Repositories.
Sie nennt den Ist-Stand des Codes und markiert die wichtigsten Leitdokumente fuer die Migration zur Workflow-Plattform.

## Root

`DOCS_CONTROL.md`
Steuerungsdatei fuer Doku-Lesereihenfolge, Schreibziele und Pflege-Regeln.

`PROJECT_CONTEXT.md`
Stabile Projektwahrheit und fachliche Guardrails fuer die Workflow-Plattform.

`PROJECT_STRUCTURE.md`
Aktuelle Repo-Struktur und Einstiegspunkte.

`CODE_REVIEW.md`
Aktuelle Review-Priorisierung und aktiver technischer Fokus.

`CODE_REVIEW_ARCHIVE.md`
Detailarchiv abgeschlossener Review-Zyklen.

`MEMORY.md`
Kurzlebiges Arbeitsgedaechtnis fuer die naechste Session.

`TODO.md`
Aktive Umsetzungssteuerung fuer Review-Nacharbeit.

`FRONTEND_TODO.md`
Frontend-spezifische Umsetzungs- und UX-Roadmap.

`CODEX_SYNC.md`
Aktives Handoff-Protokoll zwischen Codex und Claude.

`CODEX_SYNC_ARCHIVE.md`
Archiv aelterer Handoff-Eintraege.

`CLAUDE.md`
Allgemeine Hinweise fuer Claude.

`CLAUDE_CONTROL.md`
Operative Steuerungsdatei fuer Claude-Arbeit unter Codex-Orchestrierung.

`KauthWorkflow/`
Obsidian Vault mit stabiler Wissensbasis. Einstieg: `KauthWorkflow/00 Start.md`.

`compose.yml`, `compose.dev-db.yml`, `compose.prod.yml`
Compose-Basis und Overlays fuer Dev-DB sowie produktionsnahen Stack.

`.env.prod.example`
Vorlage fuer produktive Laufzeitkonfiguration.

`start.ps1`
Windows-Helferscript zum Starten von `dev` oder `prod`.

`scripts/start-vm.sh`
Linux-VM-Helferscript fuer `dev`- und `prod`-Start sowie `status`, `logs`, `stop` und `restart`.

`db/`
Schema, Bootstrap, historische Migrationen und Init-Reihenfolge.

`api/`
Backend-Solution, API-Projekt und Backend-Tests.

`web/`
Frontend-Projekt auf Basis von React, Vite und React Query.

## Vault: `KauthWorkflow/`

`00 Start.md`
Einstieg, Bereichsuebersicht und Lesepfade.

`Architektur/`
Zielbild, Entscheidungen, Migrationspfad und groessere technische Skizzen.

`Domäne/`
Fachbegriffe und Domänenkonzepte wie Workflow, Rotation, Identity.

`Betrieb/`
Setup, Laufzeit- und Betriebswissen.

`Stand/`
Menschlich lesbare Statusspiegel und Bestandsaufnahmen.

`Arbeit/`
KI-Workflow, Engineering-Regeln und Zusammenarbeit.

`Lernen/`
Lerneinheiten und Einarbeitungspfad.

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
- `src/components/layout/`
- `src/components/admin-config/`
- `src/components/workflow-detail/`
- `src/services/`

Aktuell wichtige Produktpfade:
- Builder auf `/builder`
- Workflow-Start ueber `/workflows/create`
- Rotation auf `/rotation`, `/rotation/plans/:planId`, `/rotation/operations`, `/rotation/tasks/:taskRef`
- Personenhistorie auf `src/pages/PersonWorkflowHistoryPage.tsx`

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

Aktuell wichtige technische Schwerpunkte:
- `WorkflowLifecycleService` als Commit-Grenze fuer zentrale Runtime-Mutationen
- `PostgresWorkflowRepository` weiterhin als groeßter fachlicher Persistenzblock
- `WorkflowDefinitionDraftValidator` / `WorkflowDefinitionSnapshotValidator` als Validation-Split aus Zyklus 7

## Backend-Tests: `api/API.Tests`

Unit- und integrationsnahe Tests fuer:
- Authorization
- Endpunkte
- Requirement- und Statusregeln
- Workflow-Repositories
- Audit-Log und Workflow-Links
- Notification-Logik
