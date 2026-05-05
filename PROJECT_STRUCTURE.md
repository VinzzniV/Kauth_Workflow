# Projektstruktur

Diese Uebersicht beschreibt die aktuell relevante Struktur des Repositories.
Sie nennt den Ist-Stand des Codes und markiert die wichtigsten Leitdokumente fuer die Migration zur Workflow-Plattform.

## Root

`DOCS_CONTROL.md`
Steuerungsdatei fuer Doku-Lesereihenfolge, Schreibziele und Pflege-Regeln.

`PROJECT_CONTEXT.md`
Stabile Projektwahrheit und fachliche Guardrails fuer die Workflow-Plattform.

`CODE_REVIEW.md`
Aktuelle CodeReview mit priorisierten Risiken und konkreter Nacharbeit.

`MEMORY.md`
Kurzlebiges Arbeitsgedaechtnis fuer naechste Sessions.

`TODO.md`
Priorisierte Umsetzungssteuerung fuer Review-Nacharbeit.

`FRONTEND_TODO.md`
Frontend-spezifische Umsetzungs- und UX-Roadmap fuer React-, CSS- und Interaktionsarbeit.

`CODEX_SYNC.md`
Handoff-Protokoll zwischen Claude und Codex.

`CLAUDE.md`
Hinweise fuer KI-Zusammenarbeit im Repo.

`CLAUDE_CONTROL.md`
Operative Steuerungsdatei fuer Claude-Arbeit unter Codex-Orchestrierung: Rollen, Slice-Workflow, Doku-Pflicht und Commit-Regel pro abgeschlossenem Schritt.

`KauthWorkflow/`
Obsidian Vault mit stabiler Wissensbasis. Einstieg: `KauthWorkflow/00 Start.md`
Enthaelt: Zielarchitektur, Entscheidungen, Migrationspfad, Domänenkonzepte, Betriebsdoku, Stand.

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
- `src/components/layout/` — Shell-, Header- und gemeinsame Arbeitsflächen-Bausteine wie `ViewModeToggle`
- `src/components/admin-config/`
- `src/components/workflow-detail/`
- `src/services/`

Hinweis:
Seit T8 lebt der Workflow Builder auf der eigenen Route `/builder`; alte Builder-Einstiege unter `/admin/config?section=builder|templates|answers|defaults` werden dorthin umgeleitet. Die Builder-Logik sitzt primär in `src/pages/WorkflowBuilderPage.tsx`, `src/components/admin-config/`, `src/hooks/useAdminWorkflowBuilder.ts`, `src/hooks/adminWorkflowBuilderModel.ts` und `src/services/adminConfigApi.ts`.
Seit T11 nutzt `/workflows/create` den startbaren Definitionen-Katalog aus `src/services/lookupApi.ts` statt `process_types`; die alten Konfigurationssektionen fuer Process Types, Templates und Answer Defaults sind im Admin-Workspace nicht mehr navigierbar.
Seit T12 ist `Administration > System` die zentrale Betriebs- und Log-Konsole: `src/components/admin-config/AdminSystemLogSection.tsx`, `src/services/adminApi.ts` und `src/services/systemLogReporter.ts` verbinden die neue Admin-Log-Ansicht mit automatischem Frontend-Error-Reporting; der alte Admin-Bereich fuer `Massenaktionen` wurde vollstaendig entfernt.
Seit T13 hat `Administration > System > Mail-Vorlagen` einen eigenen Workspace fuer konfigurierbare Benachrichtigungen. `src/components/admin-config/AdminNotificationTemplateSection.tsx`, `src/hooks/useAdminNotificationTemplates.ts` und die neuen Admin-APIs in `src/services/adminApi.ts` pflegen Betreff/Text, Trigger-Info und eine read-only Preview auf Basis echter Workflows bzw. Durchlaufplaene.
Seit Phase 6 gibt es fuer HR zusaetzlich den Rotation-Frontend-Slice auf `/rotation` und `/rotation/plans/:planId`; `/rotation` dient jetzt primaer als Uebersichts- und Einstiegseite fuer bestehende Durchlaufplaene, waehrend die Anlage neuer Durchlaeufe ueber `Neuer Vorgang` und den Link nach `/rotation?mode=create` startet. Die Seiten in `src/pages/RotationPlanningPage.tsx` und `src/pages/RotationPlanDetailPage.tsx` nutzen `src/services/rotationApi.ts`, `src/services/queries/rotationQueries.ts` und `src/types/rotation.ts`.
Seit Phase 7 gibt es fuer IT und Fachbereiche den operativen Rotation-Slice auf `/rotation/operations` und `/rotation/tasks/:taskRef`; die Seiten `src/pages/RotationOperationsPage.tsx` und `src/pages/RotationTaskDetailPage.tsx` nutzen den familienfaehigen `/tasks`-Envelope, `src/services/taskApi.ts`, `src/services/mutations/workflowMutations.ts` und die erweiterten Task-/Status-Mappings in `src/services/api/` und `src/utils/taskStatus.ts`.
Seit Phase 8 sind Audit-/Verlaufs- und Benachrichtigungshistorie in den bestehenden Rotations-Detailseiten sichtbar; `src/components/rotation/RotationAuditLog.tsx` und `src/components/rotation/RotationNotificationsPanel.tsx` werden in `RotationPlanDetailPage` und `RotationTaskDetailPage` eingebunden; `src/services/queries/rotationQueries.ts` enthaelt die planbezogenen History-Queries.
Seit dem mitarbeiterzentrierten Lifecycle-Schnitt startet `/workflows/create` fachlich immer von einer kanonischen Person: bestehende Lifecycle-Prozesse suchen ueber `src/services/peopleApi.ts` und `src/services/queries/peopleQueries.ts`, neue Onboardings legen die Person zuerst per `POST /people` an und starten danach den Workflow mit `targetPersonId`. Die Personenhistorie lebt auf `src/pages/PersonWorkflowHistoryPage.tsx`.
Seit FE-28 nutzen operative Listen (`WorkflowList`, `MyTasks`, `RotationOperations`, `PersonWorkflowHistory`) einen gemeinsamen Karten-/Tabellen-Schalter aus `src/components/layout/ViewModeToggle.tsx`; die Tabellen bleiben clientseitig sortierbar und fallen mobil auf Karten-/Listenansichten zurück.

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
Definition Layer, Runtime-Orchestrierung und Automation Layer werden schrittweise parallel eingefuehrt; die aktuelle Review priorisiert zusaetzlich Transaktionen, SQL-seitige Task-Sichtbarkeit und Repository-Schnitte.
Seit T11 existiert zusaetzlich ein definition-first Oeffnungspfad ueber `GET /workflow-definitions/startable` und `POST /workflows` mit `workflowDefinitionKey`; Legacy-`processTypeKey` bleibt nur noch als Kompatibilitaetsalias erhalten.
Seit dem Mitarbeiter-Lifecycle-Schnitt sind `people` der fachliche Primäranker fuer Lifecycle und Rotation: `WorkflowMasterDataEndpoints` expose `POST /people`, `GET /people/search`, `GET /people/{personId}/workflow-history` und `GET /people/rotation-eligible`; `WorkflowRuntimeService`, `PostgresWorkflowRepository.PersonLifecycleOperations.cs` und `PersonLifecycleProjectionService` koppeln Workflow-Starts, Directory-Linking und kanonische Personenfortschreibung an `targetPersonId`.

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
Konsolidiertes Grundschema fuer Stammdaten, Workflow-Laufzeit, Definition Layer, Automation Layer, Directory-Projektion, Rollen-/Permission-Modell, Runtime-Konfiguration, Rotations-Layer, `system_event_log` und konfigurierbare Mail-Vorlagen (`notification_templates`). Erzeugt aus `pg_dump --schema-only` nach Anwendung aller historischen Migrationen.

`02_bootstrap.sql`
Konsolidierte Produktions-Seed-Daten (Process Types, System Responsibilities, Action Definitions, Notification Templates, Default-Departments).

`02_dev_seed.sql`
Konsolidierte Dev-Seed-Daten: prod-Bootstrap plus dev-spezifische Inhalte (Rotations-Beispieltemplates, Azubi-Abteilungen, lokale `notification_email_settings`).

`_archive/`
Originale Migrationen `02_reset.sql`, `02_seed.sql`, `03_*.sql` … `67_*.sql`, `90_dev_defaults.sql` sowie der vor-konsolidierte Stand von `01_schema.sql` und `02_bootstrap.sql`. Werden nicht mehr von den Init-Scripten geladen, dienen nur noch als Referenz fuer Historie und Domaenen-Kontext.

`init/dev/00_init.sql`, `init/prod/00_init.sql`
Init-Reihenfolgen fuer Dev und Production. Beide laden `01_schema.sql` plus den jeweiligen Seed.

Solange die Plattform nicht produktiv laeuft, werden Schema-/Seed-Aenderungen direkt in den drei konsolidierten Dateien gepflegt. Sobald produktiv: konsolidierte Dateien einfrieren, Aenderungen nur noch additiv ueber neue Migrationen.

Hinweis:
Das neue Ziel-Datenmodell fuer Definition Layer, Runtime Events und Automation Layer wird inkrementell eingefuehrt; T9 verankert den ersten produktiv nutzbaren Automation-Kern, Builder-UI und echte externe Adapter folgen spaeter.

## Backend-Erweiterungen aus T9

`api/API/Contracts/WorkflowAutomationDtos.cs`
Read-DTOs fuer Action-Katalog sowie Automation-Job-, Attempt- und Log-Ansichten.

`api/API/Contracts/RotationDtos.cs`
DTOs fuer den neuen Rotations-/Durchlauf-Slice inklusive Plan-/Stations-Requests sowie Admin-Requests und Responses fuer `department_action_templates`.

`api/API/Endpoints/RotationPlanningEndpoints.cs`
Minimal-API-Endpunkte fuer die HR-Planung: Suche abgeschlossener Onboardings, Lesen/Erstellen von Durchlaufplaenen sowie CRUD fuer Stationen. Die Planliste unter `GET /rotation/plans` akzeptiert jetzt auch keinen Personenfilter mehr und dient damit sowohl der personenbezogenen Detailansicht als auch der globalen Durchlauf-Uebersicht.

`api/API/Endpoints/AdminRotationConfigEndpoints.cs`
Admin-Endpunkte fuer `department_action_templates` unter `/admin/rotation/action-templates`.

`api/API/Services/RotationPlanningService.cs`
Fachliche Phase-2-Schicht fuer Sichtpruefung, Plananlage aus abgeschlossenem Onboarding sowie Validierung von Stationskonflikten und Sortierung. Seit dem Follow-up nach Phase 10 laesst die Service-Schicht die globale Planliste auch ohne `personId` zu.

`api/API/Services/RotationTemplateAdminService.cs`
Fachliche Phase-3-Schicht fuer Validierung und Pflege von `department_action_templates`.

`api/API/Services/RotationTaskGenerationService.cs`
Phase-4-Service fuer planbezogene Generated-Task-Reads, manuelle Regenerierung und automatische Department-Re-Syncs.

`api/API/Services/RotationNotificationService.cs`, `api/API/Services/RotationNotificationHostedService.cs`
Phase-5-Slice fuer taegliche Rotation-Benachrichtigungen: erzeugt deduplizierte `rotation_notifications`, verschickt sie ueber den bestehenden Mail-/Graph-Unterbau und schreibt Versandstatus zurueck.

`api/API/Contracts/RotationNotificationContracts.cs`
Interne Mail-/Dispatch-Typen fuer Rotation-Benachrichtigungen, Payload-Snapshots und Sweep-Ergebnisse.

`api/API/Repositories/PostgresWorkflowRepository.RotationHistoryOperations.cs`
Phase-8-Read-Schicht fuer `GetRotationAuditLog` und `GetRotationNotifications` mit stabilem `created_at DESC, id DESC`-Paging.

`api/API/Services/IRotationPlanningService.cs` (erweitert)
Fuegt `GetRotationAuditLogAsync` und `GetRotationNotificationsAsync` hinzu; Sichtbarkeit folgt Plan-Sichtbarkeit.

`api/API/Endpoints/RotationPlanningEndpoints.cs` (erweitert)
Neue Lese-Endpunkte `GET /rotation/plans/{planId}/audit` und `GET /rotation/plans/{planId}/notifications` mit `limit`/`offset`-Validierung.

`api/API/Repositories/IRotationRepository.cs`, `api/API/Repositories/PostgresWorkflowRepository.RotationOperations.cs`, `api/API/Repositories/PostgresWorkflowRepository.MasterDataOperations.cs`, `api/API/Repositories/PostgresWorkflowRepository.RotationTemplateOperations.cs`, `api/API/Repositories/PostgresWorkflowRepository.RotationTaskGenerationOperations.cs`
Rotation-spezifischer Persistenzzugriff auf Basis des bestehenden PostgreSQL-Repositories fuer Planung, personenzentrierte Eligibility-Suche, Vorlagenpflege, Generated-Task-Sync und Rotation-Task-Mutationen. Die Auswahl fuer neue Durchlaeufe startet jetzt ueber `people`, waehrend `source_workflow_id` nur noch das letzte abgeschlossene Onboarding als Provenienz und Eligibility-Nachweis haelt.

`api/API/Repositories/PostgresWorkflowRepository.RotationNotificationOperations.cs`
PostgreSQL-Zugriff fuer die Generierung, Deduplizierung, Dispatch-Vorbereitung und Ergebnisverbuchung von `upcoming_change`, `reminder` und `overdue` in `rotation_notifications`.

`api/API/RotationTaskRef.cs`, `api/API/RotationTaskStatusRules.cs`
Gemeinsame Phase-4-Helfer fuer globale `taskRef`-Adressen (`wf:*`, `rot:*`) und die statusspezifischen Regeln fuer Rotation-Tasks.

`api/API/Endpoints/TaskEndpoints.cs`
Liefert seit Phase 4 kanonische `/tasks/ref/{taskRef}`-Routen fuer Read, Status, Assignment, Kommentare und Approval-Entscheidungen; Rotation-Tasks werden jetzt mit Workflow-Tasks aggregiert in `/tasks` ausgeliefert.

`api/API/Services/GraphWorkflowEmailNotificationSender.cs`, `api/API/Services/NotificationEmailTemplateBuilder.cs`
Der bestehende Mail-Unterbau versendet jetzt neben Workflow-Mails auch Rotation-Notifications; Rendering, Platzhalterersetzung und feste HTML-Huelle laufen ueber das konfigurierbare Template-System.

`api/API/Services/NotificationTemplateService.cs`, `api/API/Services/NotificationTemplateCatalog.cs`, `api/API/Repositories/PostgresNotificationTemplateRepository.cs`, `api/API/Endpoints/AdminNotificationTemplateEndpoints.cs`
Neuer Mail-Template-Slice fuer Admin-Read/Write, Platzhalter-Validierung, read-only Preview mit echten Vorgangs-/Durchlaufdaten und Persistenz in `notification_templates`.

`api/API/Services/WorkflowAutomationService.cs`
Koordiniert Job-Claiming, Handler-Ausfuehrung, Retry-Regeln und Runtime-Fortschritt fuer `automation`-Nodes.

`api/API/Services/WorkflowAutomationHostedService.cs`
API-interner Background Worker fuer Polling und Ausfuehrung der Automation-Jobs.

`api/API/Services/SimulatedWorkflowAutomationHandlers.cs`
Simulierte Handler fuer die ersten Plattform-Actions wie `CreateAdUser` und `SendWelcomeMail`.

`api/API/Repositories/PostgresWorkflowRepository.AutomationOperations.cs`
PostgreSQL-Zugriff fuer Action-Katalog, Job-Claiming, Attempt-/Log-Schreibung, Mapping-Aufloesung und Folgejob-Erzeugung.

`api/API/Contracts/SystemEventLogDtos.cs`, `api/API/Services/ISystemEventLogService.cs`, `api/API/Services/SystemEventLogService.cs`
Zentrale Log-Vertraege und Persistenz fuer `system_event_log`, inklusive Admin-Read-Modell, Frontend-Ingest und Redaction sensibler Detailfelder.

`api/API/Endpoints/AdminSystemLogEndpoints.cs`, `api/API/Endpoints/ClientSystemLogEndpoints.cs`
Neue Endpunkte `GET /admin/system/logs`, `GET /admin/system/logs/summary` und `POST /client/log-events`; sie versorgen die Admin-System-Konsole und nehmen sichtbare Frontend-Fehler strukturiert entgegen.

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

`CODE_REVIEW.md`
Aktuelles Review-Artefakt vom 2026-04-23 mit Findings, Prioritaeten und empfohlenen Umsetzungsschnitten.
