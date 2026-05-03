# Legacy-Abbau-Plan

#stand #migration #legacy

Roadmap, um sämtliche bewusst gehaltenen Legacy-Codepfade in eine kohärente, „nicht mehr Legacy"-Architektur zu überführen.

Bezugsdokumente: [[Zielarchitektur]], [[Migrationspfad]], [[Code-Review-Status]], `PROJECT_CONTEXT.md`, `MEMORY.md`.

---

## Ausgangslage

Die als Legacy markierten Stellen sind nicht zufällig entstanden. Sie sind das Resultat des Migrationsschritts „Definition Layer parallel zur Altwelt einführen" (Schritt 4–6 im Migrationspfad). Schritt 11 — „Altwelt zurückbauen *nach Parität*" — ist bisher nicht angelaufen.

Der Plan unten ist die operative Ausführung von Schritt 11. Reihenfolge ist wichtig: jeder Schritt baut Risiko ab, bevor der nächste, riskantere kommt.

---

## Konventionen

- **Reasoning-Effort** (für die KI, die den Schritt umsetzt): `low` / `medium` / `high`. Bezieht sich auf den Denkaufwand pro Tool-Call, nicht auf den Gesamtaufwand.
- **Modell-Empfehlung**: `haiku` (schnell, mechanisch), `sonnet` (Standard, gute Balance), `opus` (komplex, mehrere Domänen, Risiko).
- **Aufwand**: grobe Schätzung in Personentagen.
- **Risiko**: niedrig / mittel / hoch. Hoch bedeutet: Bug ist im Produktiveinsatz schmerzhaft.

---

## Schritt 1 — Status-Feld vereinheitlichen

**Ziel:** `WorkflowLegacyStatus` aus den DTOs entfernen, Frontend konsumiert nur noch das typsichere `WorkflowStatus`.

**Umfang in einem Arbeitsschritt:**
- Frontend-Mapper `toWorkflowLegacyStatus()` und `normalizeWorkflowLegacyStatus()` in `web/src/utils/workflowStatus.ts` entfernen
- Aufrufstellen in `web/src/services/api/mappers.ts:330,370,454` umstellen (oder ersatzlos entfernen, falls das typsichere Feld schon ankommt)
- Backend: `WorkflowLegacyStatus` aus `WorkflowDtos.cs:399` entfernen, alle 4 Schreibstellen (`PostgresWorkflowRepository.ReadOperations.cs:140`, `WorkflowRuntimeService.cs:356`, `WorkflowVisibilityService.cs:77`, `AuthorizationPolicyServiceTests.cs:865`) bereinigen
- Tests ggf. anpassen

**Reasoning-Effort:** low
**Modell:** sonnet
**Aufwand:** 0,5 Tage
**Risiko:** niedrig
**Voraussetzungen:** keine

---

## Schritt 2 — `setup`-Node-Type endgültig ablegen

**Ziel:** Der `setup`-Node-Type existiert weder im Code noch in produktiven Definitionen. Builder-Inspector kennt nur noch die `measure_*`-Nodes.

**Umfang in einem Arbeitsschritt:**
- **Inventur** der Produktions-DB: gibt es noch `setup`-Nodes in `workflow_nodes` für published Definition-Versions? (Manuelle Admin-Aufgabe — sollte vor dem Code-Cleanup laufen.)
- Falls ja: Daten-Migration `setup` → fachlich richtiger `measure_*`-Type pro Definition. SQL-Migrationsskript schreiben, gegen Snapshot getestet, dann produktiv.
- Code-Aufräumen: `LEGACY_SETUP_TITLES` (web/src/hooks/adminWorkflowBuilderModel.ts:84-89), alle `nodeType === "setup"`-Sonderbehandlungen (~6 Stellen in `BuilderInspectorPanel.tsx`, `AdminWorkflowBuilderSection.tsx`, `adminWorkflowBuilderModel.ts`), `setup` aus dem Node-Type-Union entfernen
- Backend-Validation prüfen: gibt `WorkflowRuntimeService` oder ein Repository noch `setup` aus? Falls ja, mit ablegen.
- `KauthWorkflow/Architektur/Zielarchitektur.md` und ggf. `KauthWorkflow/Domäne/Workflow.md` final bereinigen

**Reasoning-Effort:** medium
**Modell:** sonnet
**Aufwand:** 1–2 Tage (Inventur + Migration treiben den Aufwand)
**Risiko:** mittel — wenn die Migration eine Definition vergisst, bricht der Builder beim Laden. Pre-Check-SQL in der Migration ist Pflicht.
**Voraussetzungen:** keine, kann parallel zu Schritt 1 laufen.

---

## Schritt 3 — `processTypeKey` aus dem Workflow-Create-Pfad entfernen

**Ziel:** Workflows werden ausschließlich über `workflowDefinitionKey` erstellt. `processTypeKey` ist im Request-DTO weg.

**Umfang in einem Arbeitsschritt:**
- **Definition-Coverage-Check:** für jeden in Produktion aktiv genutzten `process_type` muss eine published `workflow_definition` mit semantisch gleichem Key existieren. Inventur-SQL plus Abgleich mit den Frontend-Werten (`useWorkflowCreation`).
- Frontend: `web/src/hooks/workflowCreationModel.ts:187,195` — `processTypeKey` aus dem Payload entfernen
- Backend: `ProcessTypeKey` aus `WorkflowCreateRequest` als `[Obsolete]` markieren oder ab sofort ablehnen (entscheiden, ob Übergangsphase nötig); Branches in `WorkflowRuntimeService.cs:54,553` entfernen oder hinter Feature-Flag stellen
- Tests anpassen (`AdminWorkflowDefinitionConfigEndpointsTests.cs:866` und vermutlich Endpoint-Integrationstests)

**Reasoning-Effort:** low (Code) / medium (Coverage-Check)
**Modell:** sonnet
**Aufwand:** 0,5–1 Tag
**Risiko:** mittel — wenn die Coverage lückenhaft ist, schlagen Workflow-Erstellungen für nicht-gemappte Prozesstypen fehl.
**Voraussetzungen:** Definition-Coverage muss schwarz auf weiß bestätigt sein.

---

## Schritt 4 — Workflow-Filter-UIs auf Definitionen umstellen

**Ziel:** Dashboard, Workflow-Liste, Workflow-Suche filtern nach `workflow_definition_key` statt nach `process_type_key`.

**Umfang in einem Arbeitsschritt:**
- Backend-Endpunkt prüfen: liefert `/workflow-definitions/startable` alle Definitionen, die für Filter relevant sind (auch nicht-startbare aktive)? Falls nicht: neuen Endpunkt `/workflow-definitions/listable` anlegen oder bestehenden erweitern
- Frontend umstellen: `DashboardOverview.tsx`, `workflowListPageModel.ts`, `WorkflowSearchPage.tsx`, `dashboardInsights*`-Module — `useProcessTypes()` durch ein Definitions-Pendant ersetzen
- Filter-Query-Parameter in `WorkflowQueryOptions` von `processTypeKey` auf `workflowDefinitionKey` umstellen; Backend muss neuen Parameter akzeptieren
- Übergangsphase: Backend kann beides eine Zeit lang akzeptieren, Frontend schickt nur noch das neue
- `web/src/services/lookupApi.ts`: `getProcessTypes()` deprecaten

**Reasoning-Effort:** medium
**Modell:** sonnet
**Aufwand:** 1 Tag
**Risiko:** niedrig — UI-Bug ist sichtbar, kein Datenverlust
**Voraussetzungen:** Schritt 3 abgeschlossen (Coverage-Check ist dieselbe Voraussetzung).

---

## Schritt 5 — Permission-System auf `definition_key` umstellen ✓ 2026-04-30

**Ziel:** Permissions wie `workflows.create.onboarding` werden zu `workflows.create.<definition_key>`. `AuthorizationPolicyService` operiert auf `definition_key` statt auf `process_type_key`.

**Inventur-Ergebnis vor Umsetzung:**
- Alle 6 `workflow_definitions.definition_key`-Werte sind 1:1 identisch mit den `process_types.key`-Werten (`onboarding`, `offboarding`, `department_change`, `position_change`, `role_change`, `name_change`).
- Permissions-Seed (`02_bootstrap.sql:110-115`, `02_dev_seed.sql:116-121`) referenziert genau diese Strings.
- → **DB-Migration entfällt:** Permission-Strings bleiben sichtbar identisch; geändert hat sich nur die Quelle/Bedeutung des Suffix.

**Umgesetzt:**
- `AuthorizationPermissions.WorkflowCreate(...)` Parameter umbenannt (`processTypeKey` → `workflowDefinitionKey`); XML-Kommentar dokumentiert Datenidentität als Übergangsbedingung.
- `IAuthorizationPolicyService.CanCreateWorkflowForProcessType` → `CanCreateWorkflowForDefinition` (Parameter `managerCreatableDefinition`).
- `AuthorizationPolicyService.CanCreateWorkflowForDefinition` analog umgesetzt.
- `WorkflowRuntimeService.HasUnscopedWorkflowCreatePermission` / `HasScopedWorkflowCreatePermission`: Doppel-Lookup (Definition-Key + Legacy-process_type-Key) bleibt — als explizite Brücke bis Schritt 6 dokumentiert.
- Tests in `AuthorizationPolicyServiceTests.cs` (3 Methoden) auf neue Signaturen umgebogen.
- Kein Frontend-Cleanup nötig: Permission-Strings sind serverseitig kanonisch, Frontend konsumiert nur die Effective-Permissions.
- Keine DB-Migration nötig: Permission-Strings unverändert, kein Audit-Log-Drift.

**Reasoning-Effort:** medium (Inventur entscheidet — wenn definition_key ≠ process_type_key entstanden wäre, hätte echte Migration gebraucht)
**Modell:** opus
**Aufwand (real):** 0,5 Tage (statt geplant 4–6, weil Inventur Migration eliminiert hat)
**Risiko (real):** niedrig — keine DB-Änderung, keine Permission-String-Veränderung
**Voraussetzungen:** Schritte 1–4 abgeschlossen ✓

**Offene Brücken (für Schritt 6):**
- Doppel-Lookup gegen `legacyProcessTypeKey` in `WorkflowRuntimeService` kann zurückgebaut werden, sobald `process_types`-Tabelle weg ist.
- `IsManagerCreatableProcessType` liest weiter aus `process_types.allows_manager_creation` — gehört zu Schritt 6.
- `PrimaryLegacyProcessTypeKey` in den Definition-DTOs bleibt bis Schritt 6.

---

## Schritt 6 — `process_types`-Tabelle stilllegen (in Slices 6.1–6.4)

**Slice 6.1 ✓ 2026-04-30 — Frontend-Lookup-Stack entfernt**
- `useProcessTypes` (verwaist nach Schritt 4) + `processTypeQueries.ts` aufgelöst, Hook in `workflowDefinitionQueries.ts` migriert
- `getProcessTypes()` aus `lookupApi.ts` entfernt, `queryKeys.processTypes()` weg, Test gelöscht
- 135/135 Web-Tests grün
- **Nicht migriert (geht in 6.4):** 4 Admin-Hooks mit `getAdminProcessTypes` (verwalten Stammdaten mit FK an `process_types`); `ProcessType`-Typ als DTO-Property; Backend-Endpunkt `/process-types`

**Slice 6.3d-ii — Reality-Check 2026-05-01 (vor Umsetzung)**

Beim Versuch, mit Sub-Slice 6.3d-ii (FK-Migration der 3 Stammdaten-Tabellen) zu starten, hat ein detaillierter Scan den realen Umfang offengelegt:

**Daten-Realität:**
- `process_types.id` ≠ `workflow_definitions.id` in den Seeds (z.B. offboarding hat `pt.id=3` aber `wd.id=2`, name_change hat `pt.id=5` aber `wd.id=7`). ID-Mapping ist nötig: `1→1, 3→2, 4→3, 5→7, 6→8, 7→9`.
- `app_role_answer_defaults` hat einen **composite FK** `(answer_definition_id, process_type_id) → workflow_answer_definitions(id, process_type_id)`. Lock-Step-Migration nötig: `workflow_answer_definitions` muss zuerst migriert werden, dann composite-FK auf `(answer_definition_id, workflow_definition_id)`.

**Schema/Seed-Aufwand:**
- 3 Tabellen mit Spaltenumbenennung in `01_schema.sql`
- 4 FK-Constraints umdefinieren
- ~100 INSERT-Zeilen in `02_dev_seed.sql` + ~80 in `02_bootstrap.sql` mit ID-Mapping per Hand pflegen (jede Zeile pflegen, Tippfehler katastrophal)
- 1 composite FK neu

**Code-Aufwand:**
- 557 Treffer von `process_type_id`/`processTypeId`/`ProcessTypeId` in 50 Files. Davon hängen *viele* an `workflows.process_type_id` (Workflow-Instance-FK, NICHT Teil von Slice 6.3d-ii) und *viele* an den 3 Stammdaten-Tabellen-Spalten.
- Pro betroffener Stammdaten-Tabelle: SQL-Reader + Writer + DTOs + Frontend-Hooks + Tests umstellen
- Reader-Indices nachziehen, Composite-FK-Logik pflegen

**Realistische Schätzung:** 2–3 Sessions à mehrere Stunden, ohne Stop-Punkt zwischen Schema und Code (sonst Tests broken). Pro Session ein Sub-Sub-Slice (eine Tabelle vollständig).

**Empfehlung:** Sub-Slice 6.3d-ii in 3 separate Sub-Sub-Slices zerteilen, jeweils eine Tabelle pro Session:
1. **6.3d-ii-A:** `workflow_answer_definitions` (Schema + Seed + Reader/Writer + DTO + Tests) — Pilot, weil ohne FK-Abhängigkeiten
2. **6.3d-ii-B:** `app_role_answer_defaults` (composite FK auf neuer Spalte) — danach, weil von 6.3d-ii-A abhängt
3. **6.3d-ii-C:** `task_templates` — separat, größte Frontend-Surface (Admin-Template-Editor)

Erst dann wird 6.3d-iii (4 Frontend-Admin-Hooks endgültig auf Definitions umstellen, `/process-types`-Endpoints raus) und 6.3d-iv (Schema-Drop) sinnvoll machbar.

**Was es bringen würde:** Nach 6.3d-ii ist `process_types.id` als FK-Quelle für Stammdaten weg. Damit ist `process_types`-Tabelle nur noch Display-Quelle für die 4 Frontend-Admin-Hooks und den `/process-types`-Endpoint — ein viel kleinerer Slice, der dann in 6.3d-iii final aufgelöst werden kann.

**Pausierungsgrund:** Sub-Slice 6.3d-ii braucht Schema + Seed + Code in einem Wurf (sonst Tests broken im Zwischenstand). Das ist mehrere Stunden konzentrierter Arbeit pro Tabelle. Eine Session ist zu kurz.

---

**Schritt 8 Session 1 ✓ 2026-05-01 — `PostgresRepositorySharedHelpers` ausgebaut (Vorarbeit fuer Slice-Refactor)**

Vorarbeit fuer Schritt 8 (Repository-Schnitte). Cross-Slice-Helper aus den 18 Workflow-Partials nach `PostgresRepositorySharedHelpers` verlagert, damit die Folge-Slices (WorkflowRuntime/Automation/Audit/Notification) ohne gegenseitige Klassenkopplung extrahiert werden koennen.

**SharedHelpers von 290 auf 766 Zeilen ausgebaut. Verlagert:**

| Helper | Origin | Cross-Slice-Aufrufer |
|---|---|---|
| `InsertAuditEntry` | AuditOperations | 9 Files |
| `BuildTaskStatusAuditDetail` | AuditOperations | mehrere |
| `LoadPrimaryTaskAssignmentAuditLabel` | AuditOperations | mehrere |
| `LoadResponsibilityIdByKey` | AssignmentOperations | 2 Files |
| `EnsureValidPositionRole` | LifecycleOperations | 4 Files |
| `LoadTargetPerson` + `TargetPersonRecord` | WorkflowCreateOperations | 2 Files |
| `LoadStoredAnswersByKey` | LinkOperations | 3 Files |
| `LoadWorkflowDefinitionGraph` | WorkflowRuntimeOperations | 2 Files |
| `WorkflowDefinitionGraphRecord` + 3 weitere Records | WorkflowRuntimeOperations + AutomationOperations | mehrere |
| `ParseJsonElement` | WorkflowDefinitionAdminOperations | 3 Files |
| `TaskGenerationStage` enum | PostgresWorkflowRepository | mehrere |

**Records jetzt Top-Level `internal sealed class`** in `PostgresRepositorySharedHelpers.cs` — leicht von neuen Slice-Repos importierbar.

**Aufrufer-Migration:** ~50 Call-Sites in 10 Partials via `sed -i` umgebogen (`Method(...)` → `PostgresRepositorySharedHelpers.Method(...)`).

**Bewusst nicht angefasst:**
- `GenerateWorkflowTasks` (TaskGenerationOperations) — 5 file-interne Sub-Helper (LoadWorkflowTaskGenerationContext, LoadTaskTemplates, LoadTaskTemplateConditions, LoadTaskTemplateDependencies, LoadWorkflowDueAt)
- `RecalculateAndPersistWorkflowStatus` (StatusCalculationOperations) — ~7 file-interne Sub-Helper
- `CreateWorkflowNotifications` (NotificationOperations) — ~10 file-interne Sub-Helper

Diese drei sind effektiv eigene Subsysteme. Verschiebung nur sinnvoll mit allen Sub-Helpern (~600+ Zeilen) — gehoert in Slices 2-4 (Session 2/3) wo dann die Architektur-Entscheidung pro Subsystem getroffen wird.

**Build:** .NET clean (API + Tests, 0 Errors), TypeScript clean.

**Naechster Schritt:** Session 2 = Slice 1 (WorkflowRuntimeRepository) extrahieren. Dort sind die SharedHelpers schon bereit, sodass die Cross-Klassenkopplung zwischen Runtime-Slice und den uebrigen Partials minimal bleibt.

---

**Slice 7B+7C ✓ 2026-05-01 — `hr_onboarding`-Responsibility umbenannt + Dead-Code-Cleanup Permission-Array**

**7B — `hr_onboarding` → `hr_workflow_initiator`:**
- Surface war klein: 2 Seed-Files + 1 Backend-Fallback. Responsibility ist faktisch ein generischer Workflow-Initiator-Slot (Fallback für `workflow_created`-Notifications, wenn kein task-template-spezifischer Responsibility existiert).
- `db/02_bootstrap.sql` + `db/02_dev_seed.sql`: Responsibility id=15 umbenannt, Label "HR-Workflow-Initiierung", neutralisierte Description "Verantwortung fuer Start, Abstimmung und Begleitung von Workflows."
- `api/API/Repositories/PostgresWorkflowRepository.NotificationOperations.cs:849`: `LoadResponsibilityIdByKey(..., "hr_onboarding")` → `"hr_workflow_initiator"`
- System ist nicht produktiv → keine Daten-Migration in `app_user_responsibilities` nötig

**7C — Dead-Code-Cleanup `WorkflowCreatePermissions[]`-Array:**
- `api/API/Authorization/AuthorizationPermissions.cs`: ungenutztes hardcoded Array mit 6 Permission-Strings (`workflows.create.onboarding` etc.) entfernt — kein Lese-Zugriff im gesamten Repo. Permission-Schema (`workflows.create.<definition_key>`) ist seit Slice 6.3d-iv vollstaendig definitionsgetrieben.
- Permission-Strings im DB-Seed (`app_permissions` Tabelle) bleiben unverändert — Suffix = `definition_key`, also fachlich korrekt.

**Build:** .NET clean (0 Errors), TypeScript clean.

---

**Slice 7A ✓ 2026-05-01 — Legacy-Endpoints + DTO `CompletedOnboardingSearchResultDto` entfernt**

Bestand: Es existierten parallel die "fachlich neutralen" Endpoints `/workflow-target-person-sources` und `/people/rotation-eligible` UND die Legacy-Endpoints `/workflows/completed-onboardings` (strukturidentischer Wrapper) und `/rotation/completed-onboardings` (Frontend rief ohnehin schon `/people/rotation-eligible`). Slice räumte die Legacy-Endpoints ab.

**Backend:**
- `/workflows/completed-onboardings` Endpoint + `ToCompletedOnboardingSearchResult`-Mapper in `WorkflowMasterDataEndpoints.cs` gelöscht
- `/rotation/completed-onboardings` Endpoint + Mapper in `RotationPlanningEndpoints.cs` gelöscht
- DTO `CompletedOnboardingSearchResultDto` in `WorkflowDtos.cs` gelöscht (war strukturidentisch mit dem schon existierenden `WorkflowTargetPersonSourceDto`)
- Backend-Test `WorkflowTargetPersonSourcesAlias_PassesSearchAndLimitToRepository` (testete den entfernten Legacy-Endpoint) gelöscht

**Frontend:**
- `web/src/types/rotation.ts`: gesamten lokalen `CompletedOnboardingSearchResult`-Type-Block durch `import { WorkflowTargetPerson } from "./workflow"; export type RotationEligiblePerson = WorkflowTargetPerson;` ersetzt
- `web/src/types/workflow.ts`: Type-Alias `CompletedOnboardingSearchResult = WorkflowTargetPersonSource` gelöscht
- `web/src/services/api/backendDtos.ts`: `BackendCompletedOnboardingSearchResultDto` gelöscht
- `web/src/services/workflowApi.ts`: Thin-Wrapper `searchCompletedOnboardings` gelöscht (war 4-Zeiler-Forward zu `searchWorkflowTargetPersonSources`)
- `web/src/services/rotationApi.ts`: Funktion `searchCompletedRotationOnboardings` → `searchRotationEligiblePeople`, Type-Import auf `RotationEligiblePerson` umgestellt
- `web/src/services/queries/rotationQueries.ts`: Hook `useRotationCompletedOnboardings` → `useRotationEligiblePeople`
- `web/src/services/queryKeys.ts`: Keys `workflows.completedOnboardings` und `rotation.completedOnboardings` entfernt
- `web/src/pages/RotationPlanningPage.tsx`: Imports + Type-Annotations + Hook-Name umgestellt
- `web/tests/RotationPlanningPage.test.tsx`: Mock-Hook-Name + Helper-Funktion `createCompletedOnboarding` → `createRotationEligiblePerson` umbenannt

**Build:** .NET clean (0 Errors), TypeScript clean.

**Bewusst nicht angefasst:**
- `IRotationRepository.GetCompletedOnboardingSource` und Implementation in `PostgresRotationRepository.PlanOperations.cs` — die SQL filtert auf `wd.definition_key = 'onboarding'` und ist fachlich onboarding-spezifisch (validiert "ist das ein abgeschlossenes Onboarding für Rotation?")
- Felder `latestCompletedOnboardingWorkflowUid` und `latestCompletedOnboardingAt` in `WorkflowTargetPersonDto` etc. — fachliche Information
- Schritt 7B (`hr_onboarding`-Responsibility umbenennen) und 7C (`workflows.create.onboarding`-Permission umbenennen) — würden Daten-Migrationen in produktiven Stammdaten/User-Rechten brauchen, User-Entscheidung: nicht machen

---

**Slice 6.3d-iv ✓ 2026-05-01 — `process_types`-Tabelle gedroppt, `workflows.process_type_id` migriert**

Komplette Stilllegung der `process_types`-Tabelle und Migration der Workflow-Instance-FK.

**Schema-Änderungen in `01_schema.sql`:**
- `workflow_definitions` um zwei Spalten ergänzt: `requires_target_person boolean DEFAULT false NOT NULL` und `approval_task_template_key character varying(120)` plus `CONSTRAINT workflow_definitions_supervisor_step_requires_approval_task CHECK ((NOT requires_supervisor_step) OR (approval_task_template_key IS NOT NULL))`
- `workflows.process_type_id` umbenannt zu `workflow_definition_id` (FK auf `workflow_definitions(id)` ON DELETE RESTRICT)
- `workflow_definition_versions.primary_legacy_process_type_id` gedroppt (Spalte + FK + Index)
- `process_types`-Tabelle komplett gedroppt (CREATE TABLE + Sequence + 2 Constraints)
- 2 FK-Constraints, die auf `process_types(id)` zeigten, entfernt; einer durch neuen `workflows_workflow_definition_id_fkey` ersetzt

**Seed-Migration (`02_bootstrap.sql` + `02_dev_seed.sql`):**
- `process_types` INSERT-Block + Sequence-Setval entfernt (beide Files)
- `workflow_definitions` INSERT um 2 Spalten erweitert: `requires_target_person`, `approval_task_template_key` mit Backfill (Onboarding `false`/`'supervisor_fills_document'`, alle anderen `true`/`NULL`)
- `workflow_definition_versions` INSERT: 7. Spalte `primary_legacy_process_type_id` aus allen Tupeln entfernt
- `workflows`-Tabelle ist im Seed leer — kein Daten-Backfill nötig

**Backend-Code (13 Repository-Files + 1 Extension):**
- Alle `JOIN process_types pt ON pt.id = w.process_type_id` → `JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id` (alle 12 Repository-Partials)
- Alle `LEFT JOIN process_types vpt ON vpt.id = v.primary_legacy_process_type_id` → `LEFT JOIN workflow_definitions vpt ON vpt.id = v.workflow_definition_id`
- `pt.key` → `pt.definition_key`, `vpt.key` → `vpt.definition_key` durchgängig
- `INSERT INTO workflows (process_type_id, ...)` → `INSERT INTO workflows (workflow_definition_id, ...)` in `WorkflowCreateOperations` und `WorkflowRuntimeOperations`
- `LoadProcessTypeForCreate`-SELECT auf `workflow_definitions` umgestellt; `IsActive`-Property + `record.IsActive`-Check entfernt (workflow_definitions hat kein is_active)
- `NormalizeWorkflowDefinitionId`-Bridge gelöscht, `EnsureProcessTypeExists` vereinfacht zu reiner Existenz-Prüfung gegen workflow_definitions
- `InsertWorkflowDefinitionVersion`-Helper: `primaryLegacyProcessTypeId`-Parameter + SQL-Spalte entfernt; alle 4 Aufrufer angepasst
- `LegacyProcessTypeExists` (in beiden Files: Repository und LifecycleStartupValidationExtensions) queryt jetzt `workflow_definitions WHERE definition_key = ?`
- `LoadPublishedWorkflowDefinitionVersion`-SQL: kein `JOIN process_types` mehr; `PrimaryLegacyProcessTypeId`/`PrimaryLegacyProcessTypeKey`-Felder im Record bleiben (semantisch redundant zu WorkflowDefinitionId/Key, aber für Audit-Pfad-Identität bewahrt)
- `LoadRuntimeWorkflowStatusContext` + Header-Loader: SQL auf `workflow_definitions pt` umgestellt
- `WorkflowDefinitionAdminOperations.GetStartableWorkflowDefinitions`: `pt.is_active = TRUE`-Filter entfällt (workflow_definitions hat kein is_active)
- `WorkflowQueryOperations.QueryFilteredWorkflowRows`: `LOWER(pt.key) = @processTypeKey` → `LOWER(pt.definition_key) = @processTypeKey` (Parameter-Name aus API-Kompatibilität gehalten)
- `LifecycleStartupValidationExtensions.ValidateSupervisorConfigurationAsync` + `ValidatePublishedWorkflowDefinitionsConfigurationAsync` SQLs auf workflow_definitions umgestellt

**Test-Code (7 Files):**
- `WorkflowDefinitionSqlArtifactsTests.SqlArtifacts_DefineWorkflowRuntimeLayerArtifacts`: `Assert.Contains("primary_legacy_process_type_id", ...)` entfernt
- 6 Integration-Test-Files (Concurrency, AuditLog, Automation, WorkflowDefinition, AdminConfig, Link): alle CREATE/INSERT/DELETE-Statements + `IF NOT EXISTS`-Schemas + Cleanup-Funktionen umgestellt — `process_types` → `workflow_definitions`, `process_type_id` → `workflow_definition_id`, `pt.key` → `wd.definition_key` etc.

**Build-Status:** .NET Build clean (0 Errors), TypeScript Build clean. Integration-Tests gegen lokale Postgres nicht ausgeführt.

**Audit-Code-Strings bewusst nicht umbenannt:** `"missing_supervisor_gatekeeper_primary_process_type"`, `"unknown_form_legacy_process_type"`, `"supervisor_gatekeeper_process_type_mismatch"`, `"measure_flow_process_type_mismatch"` könnten in Audit-Log-Datensätzen referenziert sein.

---

**Slice 6.3d-iii ✓ 2026-05-01 — Frontend-Naming-Lie aus 6.3d-ii bereinigt**

Alle Frontend-Typen/Variablen/Props heißen jetzt durchgängig `workflowDefinitionId`/`workflowDefinitions`:
- `web/src/types/auth.ts`: `AdminProcessType`-Type entfernt; alle `processTypeId: number` in DTOs zu `workflowDefinitionId: number` umbenannt
- `web/src/services/adminConfigApi.ts`: `getAdminProcessTypes` und `updateAdminProcessType` Service-Methoden entfernt
- `web/src/hooks/useAdminWorkflowBuilder.ts`: `processTypeIdsToLoad` → `workflowDefinitionIdsToLoad`, alle Loop-Vars + Service-Calls umgestellt
- `web/src/components/admin-config/{AdminWorkflowBuilderSection,WorkflowBuilderSidebar,BuilderInspectorPanel,BuilderInspectorFocusPanel}.tsx`: Props `processTypes: AdminProcessType[]` → `workflowDefinitions: AdminWorkflowDefinitionSummary[]`, `processTypesByKey`-Map aus `builder.definitions` gebaut, `BuilderInspectorFocusTarget.processTypeId` → `workflowDefinitionId`
- `web/src/hooks/{useAdminAnswerDefinitionManagement,useAdminRoleAnswerDefaults,useAdminTaskTemplateData}.ts` analog umgestellt

Backend-Bridge `NormalizeWorkflowDefinitionId` wird in 6.3d-iv entfernt (jetzt redundant). TypeScript Build clean.

---

**Slice 6.3d-ii ✓ 2026-05-01 — Stammdaten-Tabellen FK-Migration auf `workflow_definitions`**

Vier Tabellen mit `process_type_id`-FK auf `process_types` umgehängt zu `workflow_definition_id`-FK auf `workflow_definitions`:
- `workflow_answer_definitions`
- `task_templates`
- `app_role_answer_defaults` (mit composite FK)
- `workflow_answer_derivation_rules` (zwei FKs: source + target)

**Schema-Änderungen in `01_schema.sql`:**
- 4 Spalten-Renames in `CREATE TABLE`
- 5 FK-Constraints umgelenkt auf `workflow_definitions(id)` (statt `process_types(id)`)
- 1 composite FK `app_role_answer_defaults(answer_definition_id, workflow_definition_id) → workflow_answer_definitions(id, workflow_definition_id)`
- 2 UNIQUE-Constraint-Renames
- 2 Index-Renames
- 1 CHECK-Constraint angepasst (`source != target`)

**Seed-Migration (`02_bootstrap.sql` + `02_dev_seed.sql`):**
- Python-Helper-Skript hat 252 Tupel-Zeilen automatisiert mit ID-Mapping `1→1, 3→2, 4→3, 5→7, 6→8, 7→9` migriert
- `app_role_answer_defaults` hat keine Seed-Daten (leer)
- Skript wurde nach Lauf entfernt (nicht versioniert)

**Backend-Code (16 Files):**
- 34 SQL-Reader-Statements automatisch via Skript: `JOIN process_types`-Pfade auf `workflow_definitions` umgestellt wo Tabellen-Aliases zu den 4 migrierten Tabellen gehören
- INSERT/UPDATE-Statements in 3 Admin-Operations-Files manuell angepasst (Skript erkennt nur FROM/JOIN-Kontexte)
- `ResolveProcessTypeId(connection, transaction, processTypeKey)` umgestellt: liest jetzt aus `workflow_definitions WHERE definition_key = ?` (statt `process_types`); semantische Quelle ist jetzt wd.id
- `ResolveWorkflowDefinitionLegacyProcessTypeId(connection, transaction, key, requireActive)` umgestellt: liefert wd.id statt pt.id; Naming bleibt aus Legacy-Gründen
- Neuer Helper `NormalizeWorkflowDefinitionId(connection, transaction, idMaybeProcessTypeOrWorkflowDefinition)` in `MasterDataOperations` — toleriert beide Eingabe-IDs (pt.id ODER wd.id) und konvertiert auf wd.id; Brücke für Admin-Endpoints, die heute noch pt.id vom Frontend bekommen
- `EnsureProcessTypeExists(connection, transaction, id)` umgebaut: ruft intern `NormalizeWorkflowDefinitionId` auf, validiert in `workflow_definitions`, gibt jetzt `int` (die normalisierte wd.id) statt `void` zurück; alle 10 Aufrufer angepasst (Reader nutzen Rückgabewert für nachfolgende SQL-Operations, Upsert-Methoden binden `normalizedProcessTypeId` an SQL-Parameter)
- `LifecycleOperations.CompleteSupervisorStep`-SQL: zusätzlicher `JOIN workflow_definitions wd ON wd.definition_key = pt.key`, liest jetzt `wd.id AS workflow_definition_id` (statt `w.process_type_id`); `workflowProcessTypeId`-Variable ist semantisch jetzt wd.id
- `LinkOperations.LoadWorkflowLinkLookup`-SQL: analog umgestellt, `WorkflowLinkLookupRecord.ProcessTypeId` enthält jetzt wd.id
- `LinkOperations.GetDerivedAnswersInternal`-SQL: aufwändiger umgeschrieben (4-fach JOIN mit Brücke zwischen process_types.key und workflow_definitions.definition_key, weil derivation_rules workflow_definitions referenzieren, Workflow-Instance aber weiter process_types)
- `NotificationOperations.LoadWorkflowCreatedResponsibilityIdFromProcessTasks`-SQL: JOIN auf `workflow_definitions` statt `process_types` (matched via definition_key)

**Frontend:** Keine Änderung nötig. Frontend sendet weiter pt.id (vom `/admin/config/process-types`-Endpoint), Backend normalisiert in `EnsureProcessTypeExists` auf wd.id. DTOs `processTypeId: number` enthalten jetzt teilweise pt.id (vom Backend-Endpoint) und teilweise wd.id (von Stammdaten-Reads) — Naming Lie, aber funktional. Wird in 6.3d-iii (Admin-Hooks-Migration auf workflow_definitions) bereinigt.

**Tests:** 330/330 Backend-Unit-Tests + 135/135 Frontend-Tests + Lint clean. Integration-Tests (gegen lokale Postgres) wurden nicht ausgeführt — Schema-Migration kann erst dort verifiziert werden.

**Naming Lie als bekannte Schuld:** Spalten `workflow_definition_id` sind semantisch korrekt benannt; aber C#-Variablen, DTO-Properties und Frontend-Types heißen weiter `processTypeId`/`ProcessTypeId`. Wird in 6.3d-iii (Admin-UI-Migration) zu `workflowDefinitionId` umbenannt.

**Bekannte Brücken bis 6.3d-iii/iv:**
- Workflow-Instance-FK `workflows.process_type_id` zeigt weiter auf `process_types(id)` — Slice 6.3d-iii oder eigene Migration
- `LifecycleOperations` und `LinkOperations` joinen weiter auf `process_types` für die Workflow-Instance-Konversion
- `GetActiveProcessTypes`, `IsManagerCreatableProcessType` (-Definition), `LoadProcessTypeForCreate` lesen weiter aus `process_types`
- `/admin/config/process-types`-Endpoints + Frontend-Hook `getAdminProcessTypes` sind unverändert

**Slice 6.3d-i ✓ 2026-05-01 — `requires_supervisor_step` auf `workflow_definitions` migriert + Legacy-Helper entfernt**
- Schema: neue Spalte `workflow_definitions.requires_supervisor_step boolean DEFAULT false NOT NULL` (analog zu `allows_manager_creation` aus Slice 6.2)
- Seed-Backfill in `02_bootstrap.sql` + `02_dev_seed.sql`: nur `onboarding` → `true`, alle anderen `false` (entspricht den `process_types`-Daten)
- Repo: `LoadLegacyProcessTypeRequiresSupervisorStep` → `LoadDefinitionRequiresSupervisorStep`, liest jetzt aus `workflow_definitions.requires_supervisor_step`
- Aufrufer (`BuildWorkflowDefinitionValidationSnapshot`) automatisch konsistent
- **Aufgeräumt:** `LegacyManagerCreatableProcessTypeKeys`-Hardcoded-Liste entfernt + `HasProcessTypeManagerCreationColumn`-Schema-Probe-Helper entfernt; `GetActiveProcessTypes` direkt mit beiden SQL-Varianten ohne Fallback (Spalte existiert garantiert)
- 330/330 Backend-Unit-Tests grün
- **Status Schritt 6:** Designentscheidung-Punkt erreicht. Sub-Slices 6.3d-ii (FK-Migration der 3 Stammdaten-Tabellen `workflow_answer_definitions`, `task_templates`, `role_answer_defaults`), 6.3d-iii (Backend-Endpoints + 4 Frontend-Admin-Hooks), 6.3d-iv (Schema-Drop) bleiben offen — jeweils Mehrtage-Refactor

**Slice 6.3c ✓ 2026-05-01 — DTO-Felder + SQL-Reader + SQL-Funktion + Frontend-Type bereinigt**
- **Backend DTOs (5 Stellen):** `PrimaryLegacyProcessTypeKey` aus `WorkflowStartableDefinitionDto`, `WorkflowDefinitionVersionSummaryDto`, `WorkflowDefinitionVersionDetailDto`, `ReplaceWorkflowDefinitionVersionRequest`, `WorkflowDefinitionRuntimeDetailDto` (+ `PrimaryLegacyProcessTypeName`) entfernt
- **SQL-Reader (5 Stellen):** JOIN auf `process_types` aus den Reader-SELECTs entfernt, Reader-Indices nachgezogen — `GetStartableWorkflowDefinitions`, `GetAdminWorkflowDefinitions`, `GetAdminWorkflowDefinition` (single), `GetAdminWorkflowDefinitionVersionSummary`, `GetAdminWorkflowDefinitionVersionDetailById`, `GetWorkflowDefinitionRuntimeDetail`
- **SQL-Updates:** `UPDATE workflow_definition_versions` schreibt nicht mehr in `primary_legacy_process_type_id` (in `ReplaceAdminWorkflowDefinitionVersion` und `PublishWorkflowDefinitionVersion`); Spalte bleibt nullable bis Slice 6.3d
- **`ResolveWorkflowDefinitionLegacyProcessTypeId`-Aufrufe** in 2 Stellen entfernt (Version-Clone und ReplaceVersion); Helper-Methode bleibt für `CompleteRuntimeFormNodeInternal` (löst Process-Type-Key aus Node-Config zur ID auf für `workflow_answer_definitions.process_type_id`-FK — geht in 6.3d)
- **`WorkflowRuntimeService.HasUnscopedWorkflowCreatePermission` / `HasScopedWorkflowCreatePermission`** entfernt (Doppel-Lookup-Brücke), `CanCreateWorkflowDefinition` Parameter `primaryLegacyProcessTypeKey` weg → reine Single-Lookup-Logik
- **`BuildWorkflowDefinitionValidationSnapshot`** vereinfacht: `LoadLegacyProcessTypeRequiresSupervisorStep` mit `detail.DefinitionKey`; missing/unknown-Process-Type-Validation entfällt (DefinitionKey ist immer da)
- **SQL-Funktion `upsert_linearized_workflow_definition`** umgeschrieben: kein `process_types`-Lookup mehr, kein `primary_legacy_process_type_id`-Argument, INSERT ohne die Spalte
- **Frontend Types:** `StartableWorkflowDefinition.primaryLegacyProcessTypeKey`, `AdminWorkflowDefinitionVersionSummary.primaryLegacyProcessTypeKey`, `AdminWorkflowDefinitionVersionDetail.primaryLegacyProcessTypeKey` weg
- **Frontend API:** `replaceAdminWorkflowDefinitionVersion`-Payload kennt das Feld nicht mehr
- **Frontend Builder-Modell:** `WorkflowBuilderVersionDraft.primaryLegacyProcessTypeKey` BLEIBT als internes Datenfeld (wird vom Builder-UI für Validation/Maßnahmen-Logik genutzt), wird aber jetzt aus `detail.definitionKey` befüllt; `buildVersionReplacePayload` schickt das Feld nicht mehr ans Backend
- **Frontend `useWorkflowCreation`:** workflow-config-Lookup nutzt jetzt `selectedWorkflowDefinition.definitionKey` statt `primaryLegacyProcessTypeKey`
- **Tests:** ~16 Backend-Test-Fixtures (`PrimaryLegacyProcessTypeKey = "..."`) per sed gelöscht; 1 Integration-Test-Assertion entfernt; 1 Frontend-Mock auf synchron `definitionKey == "onboarding"` gesetzt
- **330/330 Backend-Unit-Tests + 12/12 SQL-Artefakt-Tests + 135/135 Frontend-Tests grün, Lint clean**

**Nicht angefasst (Slice 6.3d):**
- `process_types`-Tabelle + Spalte `workflow_definition_versions.primary_legacy_process_type_id` + FK
- `LoadLegacyProcessTypeRequiresSupervisorStep` (liest weiter aus `process_types.requires_supervisor_step`)
- `LoadProcessTypeForCreate` + `LoadAnswerDefinitionRecords` + `LoadRoleDefaultRecords` (nutzen `process_types.id` als FK auf `workflow_answer_definitions`/`role_answer_defaults`)
- `LifecycleStartupValidationExtensions` (validiert weiter mit `pt.key`-JOIN)
- `LegacyProcessTypeExists` Helper (Validation-Brücke)
- 4 Admin-Hooks im Frontend (`useAdminAnswerDefinitionManagement` etc.) — gebunden an `workflow_answer_definitions.process_type_id`-FK
- `/process-types` und `/admin/config/process-types/*` Endpoints
- `LegacyManagerCreatableProcessTypeKeys` Hardcoded-Liste
- `requires_supervisor_step` als 2. Datenpunkt: muss analog zu Slice 6.2 nach `workflow_definitions` migriert werden — Designentscheidung vor 6.3d

**Slice 6.3b ✓ 2026-05-01 — Backend-Verhaltenslogik auf `definitionKey` umgestellt**
- `WorkflowDefinitionSupervisorGatekeeperRules.Evaluate`: Parameter `primaryLegacyProcessTypeKey` → `workflowDefinitionKey`. Internal `normalizedPrimaryProcessTypeKey` → `normalizedDefinitionKey`. Kommentar erklärt Datenidentität als Übergangsbedingung.
- `WorkflowDefinitionValidationService`:
  - `ExpectedMeasureNodeTypeByProcessTypeKey` Dictionary → `ExpectedMeasureNodeTypeByDefinitionKey` (Schlüssel "onboarding" etc. unverändert)
  - `GetExpectedMeasureNodeTypeForProcessTypeKey` → `GetExpectedMeasureNodeTypeForDefinitionKey` (+ Doku-Kommentar)
  - `ValidateMeasurePhaseProcessConsistency` Parameter rename + Fehlertext nutzt jetzt `workflowDefinitionKey`
  - `WorkflowDefinitionValidationContext.PrimaryLegacyProcessTypeKey` → `WorkflowDefinitionKey`
- Production-Aufrufer: `LifecycleStartupValidationExtensions.cs` + `PostgresWorkflowRepository.WorkflowDefinitionAdminOperations.cs` befüllen den Context jetzt aus `definition.DefinitionKey` (statt aus `PrimaryLegacyProcessTypeKey`-Feld). Funktional unverändert, weil die Werte identisch sind (Slice-6.2-Inventur).
- Test-Aufrufer in `WorkflowDefinitionValidationServiceTests.cs` (9 Stellen) auf `WorkflowDefinitionKey =` umgestellt
- 330/330 Backend-Unit-Tests grün
- **FailureCodes bewusst behalten** (`missing_supervisor_gatekeeper_primary_process_type`, `supervisor_gatekeeper_process_type_mismatch`, `measure_flow_process_type_mismatch`) — könnten in Audit-Log-Einträgen vorkommen, sind keine externe API; interne Bedeutung ist jetzt aber definitionKey
- **Nicht angefasst (Slice 6.3c):** DTO-Felder `PrimaryLegacyProcessTypeKey` auf `WorkflowDefinitionVersionDetailDto` etc., 10 SQL-Reader-Stellen, Doppel-Lookup in `WorkflowRuntimeService`, Frontend-Type `auth.ts` mit `primaryLegacyProcessTypeKey`

**Slice 6.3a ✓ 2026-05-01 — Frontend-Builder-UI: Process-Type-Felder ausgeblendet**
- `AdminWorkflowBuilderSection.tsx`: Eingabefeld für `versionDraft.primaryLegacyProcessTypeKey` aus dem "Details zum Arbeitsentwurf"-Panel entfernt (User editiert das Feld nicht mehr — kommt vom Backend, geht zurück)
- `BuilderInspectorPanel.tsx`: "Prozessgrundlage"-Override-Dropdown am form-Node entfernt + ungenutzte `processTypeOptions`-Variable weg. **Verhaltensänderung:** Form-Nodes können kein abweichendes `legacyProcessTypeKey` mehr im Node-Config setzen. Bestehende Override-Werte in `workflow_node_configs` werden weiterhin gelesen (Read-Pfad in `resolveEffectiveProcessTypeKey` bleibt) — nur das Edit-UI ist weg.
- Datenmodell `WorkflowBuilderVersionDraft.primaryLegacyProcessTypeKey` bleibt: wird vom Backend befüllt und beim Save zurückgesendet, nicht mehr User-editierbar
- 135/135 Web-Tests grün, Build clean, Lint clean
- **Inventur-Hinweis:** Falls in produktiven `workflow_node_configs` ein form-Node ein abweichendes `legacyProcessTypeKey` gesetzt hat (= Override-Feature wurde genutzt), bleibt der Wert wirksam, aber nicht mehr admin-editierbar. Vor 6.3b/c klären, ob es solche Datensätze gibt — wenn ja, separate Migration.

**Slice 6.2 ✓ 2026-04-30 — Manager-Creation-Flag auf workflow_definitions migriert (Option A)**
- Schema: neue Spalte `workflow_definitions.allows_manager_creation boolean DEFAULT false NOT NULL` in `01_schema.sql`
- Seed-Backfill in `02_bootstrap.sql` + `02_dev_seed.sql` aus den `process_types`-Werten: `department_change`/`name_change`/`position_change`/`role_change` → true; `onboarding`/`offboarding` → false
- Repo: `IsManagerCreatableProcessType(processTypeKey)` → `IsManagerCreatableDefinition(workflowDefinitionKey)` — liest direkt aus `workflow_definitions.allows_manager_creation`, kein Fallback mehr nötig
- Service-Aufrufer (`WorkflowRuntimeService`, `WorkflowCatalogService`) übergeben jetzt `selectedDefinition.DefinitionKey` statt `PrimaryLegacyProcessTypeKey`
- 2 Test-Fake-Repos angepasst (Property/Method-Renames)
- 330/330 Backend-Unit-Tests + 12/12 SQL-Artefakt-Tests grün
- **Nicht angefasst (Slice 6.4):** `GetActiveProcessTypes` und `LegacyManagerCreatableProcessTypeKeys` (für `/process-types`-Endpunkt), `process_types.allows_manager_creation`-Spalte (Daten-Quelle für Frontend-Admin-UI), `PrimaryLegacyProcessTypeKey` in DTOs, Doppel-Lookup-Brücke in `WorkflowRuntimeService` (Kommentar steht)
- **Offen:** Admin-UI in `/admin/config/process-types`-PATCH, das `allows_manager_creation` editierbar macht — entspricht jetzt `process_types`-Spalte, nicht `workflow_definitions`. Bis Slice 6.4 müssen Manager-Flag-Änderungen in beiden Tabellen erfolgen, falls Admin sie editiert. Aktuell: weder Builder noch Admin-UI editiert das Flag — nur Seed setzt es. Keine sofortige Aktion nötig.



**Ziel:** `process_types` als Tabelle und `PrimaryLegacyProcessTypeKey` als Konzept existieren nicht mehr.

**Umfang in einem Arbeitsschritt:**
- DB-Migration: FK `workflow_definition_versions.primary_legacy_process_type_id` entfernen, dann Tabelle `process_types` droppen. SQL-Funktion `upsert_linearized_workflow_definition` umschreiben — kein Process-Type-Lookup mehr.
- Backend: `PrimaryLegacyProcessTypeKey`-Felder aus `WorkflowDefinitionDtos.cs` (4 Stellen), `WorkflowRuntimeDtos.cs` und Service-Logik entfernen
- Frontend: Builder-UI bereinigen — `legacyProcessTypeKey`-Override im Node-Config raus, Felder im Definition-Form raus
- `/process-types`-Endpunkt entfernen (Frontend nutzt es nach Schritt 4 nicht mehr); `getProcessTypes()`-Service-Funktion löschen
- `getAdminProcessTypes()` weg, betroffene Admin-Hooks (`useAdminAnswerDefinitionManagement`, `useAdminRoleAnswerDefaults`, `useAdminTaskTemplateData`, `useAdminWorkflowBuilder`) auf Definitions umstellen oder ohne diese Liste auskommen

**Reasoning-Effort:** high
**Modell:** opus
**Aufwand:** 3–5 Tage
**Risiko:** hoch — DB-Schema-Änderungen sind irreversibel ohne Backup. Kollateralschaden in Admin-Hooks und Builder möglich.
**Voraussetzungen:** Schritte 1–5 abgeschlossen.

---

## Schritt 7 — Onboarding-spezifische Vertragsnamen modernisieren

**Ziel:** Die Codebase spiegelt wider, dass Onboarding *einer von vielen* Workflows ist (siehe [[Zielarchitektur]] „Onboarding ist nur ein Workflow"). Begriffe wie `completed-onboardings`, `CompletedOnboardingSearchResultDto`, `hr_onboarding` sind ersetzt durch fachlich neutrale Namen.

**Umfang in einem Arbeitsschritt:**
- **Produkt-Entscheidung einholen** vor dem ersten Code-Change: ist die Umbenennung wirklich gewollt, oder bleiben die Begriffe bewusst stehen? Stand `MEMORY.md` ist sie auf der Preserve-Liste.
- Endpunkt `/workflows/completed-onboardings` und `/people/rotation-eligible` konsolidieren auf einen klaren Namen (Vorschlag: `/people/rotation-eligible` ist bereits da, der `completed-onboardings`-Pfad kann eine Übergangszeit lang als 301/Alias bestehen)
- DTO `CompletedOnboardingSearchResultDto` → `RotationEligiblePersonDto` (oder konkreter); Frontend-Type-Aliases mit umstellen
- Responsibility `hr_onboarding` per DB-Migration umbenennen — `responsibility_key` in `app_responsibilities` und alle Verweise (`app_user_responsibilities`, Permissions, Templates)
- Permission-Key-Updates wenn nicht in Schritt 5 mit erledigt
- `MEMORY.md` Preserve-Liste entsprechend reduzieren
- API-Vertragsfrage: externe Konsumenten (falls vorhanden) müssen informiert werden, sonst ist das ein Major-Release-Cut

**Reasoning-Effort:** high
**Modell:** opus
**Aufwand:** 3–4 Tage Code + Stakeholder-Abstimmung
**Risiko:** mittel-hoch — Permissions und Responsibilities sind lebende Stammdaten in produktiven Rollen.
**Voraussetzungen:** Schritte 5 + 6 abgeschlossen, Produkt-Entscheidung dokumentiert.

---

## Schritt 8 (parallel-Track) — Repository-Schnitte fortsetzen

**Ziel:** `PostgresWorkflowRepository` ist auf eine reine Workflow-Runtime-Klasse geschrumpft; Automation, Audit, Notification haben eigene Slices analog zu `PostgresRotationRepository`.

**Umfang pro Slice (jeder ein eigener Arbeitsschritt):**
1. `WorkflowRuntimeRepository` herausschneiden (Workflow-Lifecycle-Operations)
2. `AutomationRepository` herausschneiden (`AutomationOperations.cs`)
3. `AuditRepository` herausschneiden (`AuditLogOperations.cs`)
4. `NotificationRepository` herausschneiden (`NotificationOperations.cs`)

Pro Slice: Interface anlegen, Operations herauskopieren, gemeinsame Helper in `PostgresRepositorySharedHelpers` verlagern, DI-Registrierung anpassen, Tests aufteilen.

**Reasoning-Effort:** medium pro Slice (high für den ersten als Vorlage)
**Modell:** opus für den ersten Slice (Pattern-Festlegung), sonnet für die folgenden
**Aufwand:** 1–2 Tage pro Slice
**Risiko:** niedrig — rein strukturell, Verhaltensänderungen sind nicht beabsichtigt; Tests fangen Regressionen.
**Voraussetzungen:** keine, läuft parallel zu Schritten 1–7. Sinnvoller Einstieg ist nach Schritt 1 (Aufwärm-Übung).

### Reality-Check 2026-04-30 — Schritt 8 ist nicht so umsetzbar wie geplant

Beim Versuch, mit Slice 1 (`WorkflowRuntimeRepository`) zu starten, hat ein detaillierter Code-Scan gezeigt, dass die Plan-Annahme "1–2 Tage pro Slice, niedriges Risiko, läuft parallel" so nicht stimmt. Die **Architektur-Realität**:

**Befund 1 — `WorkflowRuntimeOperations.cs` (3030 Zeilen) ist nicht der „Workflow-Runtime-Slice", sondern *der Workflow-Kern selbst*.** Die 7 öffentlichen Methoden (entspricht dem bereits existierenden `IWorkflowDefinitionRuntimeRepository`) rufen *dutzende* Helper aus *mindestens 5 anderen Partial-Files* auf: `LoadWorkflowDefinitionGraph`, `LoadTargetPerson`, `LoadStoredAnswersByKey`, `CreateWorkflowNodeInstance`, `CreateRuntimeWorkflowTask`, `GenerateWorkflowTasks`, `RecalculateAndPersistWorkflowStatus`, `EnsureValidPositionRole`, `MapLegacyStatusForActiveNodes`, `ResolveDepartmentRequirementSelectionAssignment`, `GetAdminWorkflowDefinitionVersionDetailById`, `ResolveWorkflowDefinitionLegacyProcessTypeId`, etc.

**Befund 2 — Audit-/Notification-Helper sind transaktional inline-gekoppelt, nicht eigenständig.** `InsertAuditEntry` wird aus 8 anderen Operations heraus innerhalb derselben Transaction aufgerufen (LifecycleOperations, LinkOperations, StatusCalculationOperations, TaskGenerationOperations, TaskOperations, WorkflowCreateOperations, WorkflowRuntimeOperations, AutomationOperations). Audit ist hier *kein* Subsystem, sondern ein internes Logging-Helfer-Pattern. Die einzige *echte* Audit-Schnittstelle ist `GetWorkflowAuditLog` (Read-only). Notification ist analog: dispatch-/list-Reads sind isoliert, aber `CreateWorkflowNotifications` wird inline in der Workflow-Mutation aufgerufen.

**Befund 3 — `PostgresRotationRepository` war machbar, weil Rotation orthogonal zum Workflow-Kern ist.** Workflow-Runtime ist es nicht — sie *ist* der Kern. Das gleiche Pattern lässt sich nicht 1:1 auf die Workflow-Subsysteme anwenden.

### Was ein „echter" Schritt 8 bedeuten würde

Damit der ursprüngliche Plan funktioniert, müssten *zuerst* die geteilten Helper konsolidiert werden:

1. **Vorarbeit (3–5 Tage):** `PostgresRepositorySharedHelpers` von aktuell ~5 Methoden auf ~30+ Methoden ausbauen — alle transaktionsfreien oder transaktionsneutralen Loader/Persister/Resolvers (`LoadWorkflowDefinitionGraph`, `LoadTargetPerson`, `LoadStoredAnswersByKey`, `EnsureValidPositionRole`, `ResolveWorkflowDefinitionLegacyProcessTypeId`, `BuildAutomationJobPayload`, etc.) als `internal static` herausziehen. Aufrufer in den 18 Partial-Files umbiegen. Tests bleiben grün, weil reines move + rename.

2. **Slice 1 (2–3 Tage):** *Erst danach* lässt sich `WorkflowRuntimeRepository` extrahieren — die 7 Public-Methoden plus die 15 datei-internen Helper bilden dann eine geschlossene Klasse, die nur über `PostgresRepositorySharedHelpers` mit dem Rest interagiert.

3. **Slice 2–4 (je 1–2 Tage):** Automation/Audit/Notification analog. Da der Hauptknoten Runtime ist, werden die Folge-Slices dann tatsächlich kleiner.

**Was es bringen würde:**
- `PostgresWorkflowRepository` würde von ~7 verbleibenden Public-Schnittstellen (siehe `IWorkflowRepository`) auf eine reine Master-Data-/Read-Klasse schrumpfen
- Klare Verantwortungsgrenzen: Runtime ↔ Automation ↔ Audit-Read ↔ Notification-Dispatch
- Test-Aufteilung: Heute laden alle Integrations-Tests den Mega-Repo; danach könnten sie gegen das relevante Slice-Repo testen → schnellere und gezieltere Tests
- Echte Möglichkeit, Subsystem-spezifische Optimierungen einzubringen (z.B. Read-Replikat für Audit, eigener Connection-Pool für Automation-Worker)

**Was es nicht bringen würde:** Verhalten ändert sich nicht — reine Strukturarbeit. Solange nicht parallel auch die Transaktionsgrenzen umgezogen werden (separater, größerer Schritt), bleibt das Pay-Off architektonisch, nicht operativ.

**Empfehlung:** Schritt 8 erst angehen, wenn entweder (a) die Mega-Klasse echte operative Probleme verursacht (Build-Zeit, Test-Zeit, Merge-Konflikte) oder (b) ein Subsystem für eine echte Optimierung herausgelöst werden soll. Bis dahin ist der Pay-Off zu klein gegenüber dem ~10-Tage-Aufwand.

**Mini-Schnitt der jederzeit machbar ist (~30 min, geringer Pay-Off):** `IWorkflowAuditReadRepository` mit nur `GetWorkflowAuditLog` als eigenständige Klasse extrahieren. Etabliert das Pattern für Read-Repos, ändert sonst nichts.

---

## Reihenfolge und Dauer

```
Schritt 1 (0,5 d) ──→ Schritt 2 (1–2 d) ──→ Schritt 3 (0,5–1 d) ──→ Schritt 4 (1 d)
                                                                       │
                                                                       ▼
                                                              Schritt 5 (4–6 d)
                                                                       │
                                                                       ▼
                                                              Schritt 6 (3–5 d)
                                                                       │
                                                                       ▼
                                                              Schritt 7 (3–4 d)

Schritt 8 läuft parallel — pro Slice 1–2 d
```

Gesamt: ca. **15–22 Personentage** für die Hauptlinie, plus 4–8 Tage für die Repository-Schnitte. Realistisch über 6–10 Wochen.

---

## Was NICHT in diesem Plan steht

- Performance-Themen wie der monolithische Repository-Speed (eigene Achse, [[Code-Review-Status]] H1/H6)
- Builder-UI-Verbesserungen (eigener Track L7)
- Automation-Layer mit echten Handlern (Migrationspfad Schritt 9)
- Rotation-spezifische Folgearbeiten

Diese sind orthogonal zum Legacy-Abbau und verzögern ihn nicht.

---

## LA5 ✓ 2026-05-03 — `task_templates`-Cluster aus dem Definition-Layer geloest

Architekturentscheidung: Option B (Owned Tables) — siehe [[LA5-TaskSpezifikation-Skizze]] §6.

**Schema:**
- 3 neue Tabellen `workflow_node_task_specs` / `_conditions` / `_dependencies` mit FK an `workflow_nodes`. Composite-FK erzwingt Same-Node-Constraint fuer Dependencies.
- `task_templates` + `task_template_conditions` + `task_template_dependencies` + `workflow_tasks.task_template_id`-Spalte gedroppt.
- Spalten gestrichen (waren in real data konstant — Inventur §5): `is_active`, `owning_department_id`, `condition_group`, `dependencies.required_status`.

**Daten-Migration:**
- 76 Specs + 64 Conditions + 65 Dependencies anchored am Maßnahmen-Node der published Version. 1:1-Kopie ueber Bootstrap- und Dev-Seed.

**Backend:**
- Generator `PostgresWorkflowTaskGenerationService`: lest aus Specs ueber `MeasureNodeId` der Workflow-Version.
- Per-Node-Resolver `LoadTaskSpecForNode(nodeId)` (statt frueherem `LoadActiveTaskTemplateByKey(legacyTemplateKey)`).
- Admin-CRUD (`TaskTemplate{Admin,Condition,Dependency}Operations`): liest+schreibt `workflow_node_task_specs*`, behaelt aber `AdminTaskTemplateDto`-Form fuer Frontend-Kompat (`IsActive=true`, `OwningDepartmentId=null` hardcoded).
- Validation (`WorkflowDefinitionValidationService` + `WorkflowDefinitionGraphMappingOperations` + `LifecycleStartupValidationExtensions`): kein `legacyTemplateKey`-Pflichtfeld mehr; task/approval-Nodes muessen statt dessen keine Config haben.
- Read-Joins (`ReadOperations.cs`): JOIN auf `workflow_node_task_specs` via `workflow_node_task_spec_id` mit Fallback auf `spec_key = task_key`.
- Notification-Lookup (`PostgresWorkflowNotificationDispatchOperations`), Department-Delete-Check, Answer-Definition-Reference-Check — alle umgestellt.

**Frontend:**
- `legacyTemplateKey`-Dropdown im Builder-Step-Editor entfernt; Hint ersetzt mit Verweis auf das Admin-Task-Templates-Panel.
- Step-Card-Hint mit `legacyTemplateKey`-Beispiel ersetzt.
- AdminTaskTemplate-DTO-Form bleibt unveraendert. Frontend-Rename `AdminTaskTemplate` → `AdminTaskSpec` defer als Watch-Item.

**Tests:**
- Validation-Test-Fixtures von `legacyTemplateKey`-Configs befreit (18 Stellen via Skript, 3 manuell).
- `Runtime_WaitsForParallelJoinUntilAllBranchesAreDone` bekommt `SeedTaskNodeSpecsAsync`-Helper.
- `CreateTemporaryProcessTypeAsync` (Admin-Config-Tests) legt jetzt published Version + Maßnahmen-Node mit an, damit Admin-CRUD-Tests funktionieren.
- Cleanup-SQL `CleanupTemporaryProcessTypeAsync` umgestellt.

**Build:** .NET clean. **377/378 Tests gruen** (1 pre-existing skip). Frontend-TS-Errors sind pre-existing, nicht von LA5.

**Bewusst nicht angefasst (Watch-Items):**
- `workflow_definitions.approval_task_template_key`-Spaltenname: semantisch ist es jetzt `_spec_key`, nominal heisst es noch `_template_key`. 30+ Files Rename, defer ohne fachlichen Druck.
- Frontend-DTO-Rename `AdminTaskTemplate` → `AdminTaskSpec`, `templateKey` → `specKey`. ~22 Files mechanisch.
- Spec-Carry-Over zwischen Versionen: wenn Admin per Builder eine neue Definition-Version published, werden Specs aktuell **nicht automatisch** vom alten zum neuen Massnahmen-Node geklont. Pre-Prod ohne Versions-Wechsel-Praxis. Wenn Builder-Verwendung steigt, separat einplanen.

---

## Pflege-Hinweis

Wenn ein Schritt erledigt ist:
1. Status-Update hier im Plan eintragen (✓ + Datum)
2. `MEMORY.md` Preserve-Liste reduzieren, falls eingelöst
3. `KauthWorkflow/Architektur/Migrationspfad.md` Parallelzustand-Tabelle anpassen
4. `CODEX_SYNC.md` mit dem Schritt verknüpfen
