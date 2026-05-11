# Legacy-Abbau-Plan

#stand #migration #legacy

Aktuelle Inventur der noch verbliebenen Legacy-Komponenten und die sinnvolle Reihenfolge für deren Abbau.

Bezugsdokumente: [[Zielarchitektur]], [[Migrationspfad]], [[Code-Review-Status]], `PROJECT_CONTEXT.md`, `MEMORY.md`.

---

## Ist-Stand 2026-05-11

Der Legacy-Abbau ist weiter als der alte Plan noch suggerierte:

- `process_types` als physische Tabelle ist bereits entfernt.
- `task_templates` und `legacyTemplateKey` im produktiven Read-/Write-Pfad sind bereits durch `workflow_node_task_specs*` ersetzt.
- die alten Onboarding-Alias-Endpunkte sind entfernt.
- das Permission-Schema ist definitionsgetrieben.
- `approval_task_template_key` wurde zu `approval_spec_key` umbenannt.

Offen sind damit vor allem **semantische Brücken** und **veraltete Dokumentation**, nicht mehr ein großer separater Legacy-Datenblock.

Der wichtigste Resthebel ist heute die Mischung aus:

- `workflowDefinitionKey` als neuem fachlichen Anker
- `legacyProcessTypeKey` als read-only-Fallback für persistierten Altbestand in Builder, Gatekeeper, Requirements und Runtime-Pfad

---

## Bereits Abgebaut

| Thema | Status | Kurznotiz |
| --- | --- | --- |
| `process_types`-Tabelle | erledigt | Physische Tabelle und zentrale FKs sind abgebaut. |
| `task_templates`-Cluster | erledigt | Durch `workflow_node_task_specs*` ersetzt. |
| `CompletedOnboarding*`-Aliaspfade | erledigt | Fachlich neutrale Endpunkte und DTOs sind produktiv. |
| `CompletedOnboarding*`-Fachbegriff-Rest (BE/FE) | erledigt 2026-05-11 | `GetCompletedOnboardingSource` → `GetSourceWorkflow`; DTO-Properties `LatestCompletedOnboardingWorkflowUid`/`At` → `LatestSourceWorkflowUid`/`CompletedAt`; SQL CTEs, FE-Typen und UI-Labels durchgehend entlegacyt. |
| Permission-Schema `workflows.create.<key>` | erledigt | Quelle ist `workflow_definitions.key`, nicht mehr `process_types.key`. |
| `approval_task_template_key` | erledigt | Rename auf `approval_spec_key` inkl. manueller DB-Helfer dokumentiert. |
| `setup`-Node-Type (Code/Seeds) | erledigt | Kein `node_type = 'setup'` mehr in aktivem Code oder Seeds; nur noch in `db/_archive/`. Einziger Restpunkt: DB-Inventur gegen persistierte Definitionen (manuell, kein DB-Zugriff hier). |
| Veraltete Doku-/ERD-Artefakte | erledigt | ERD neu generiert (2026-05-11): `process_types`, `task_templates` entfernt, `workflow_node_task_specs*` und `person_match_audit_log` korrekt zugeordnet. Migrationspfad und Zielarchitektur sind aktuell. |
| `processType`-Benennung in Read-DTOs | erledigt | `WorkflowProcessTypeDto` → `WorkflowDefinitionRefDto`, Property `processType` → `workflowDefinition` in BE + FE. `LegacyStatus` / `MapLegacyStatusForActiveNodes` → `ComputedStatus` / `ComputeWorkflowStatusFromActiveNodes`. Seed-Descriptions bereinigt. 494 Tests grün. |

---

## Teilfortschritt 2026-05-11

Erster risikofreier Teilschnitt des verbleibenden Key-Clusters ist erledigt:

- Builder-internes `primaryLegacyProcessTypeKey` wurde auf `workflowDefinitionKey` umgestellt.
- Die Builder-UI spricht jetzt von Workflow-Typ bzw. Workflow-Definition-Schlüssel statt von Prozesstyp.
- Die lokalen Builder-Helfer und die Measure-Vorschau verwenden definitionsbasierte Benennung.
- Backend-intern wurden die naming-only-Reste im selben Schnitt reduziert:
  `IWorkflowRepository.GetRequirements/GetWorkflowConfig` sind nun auch in der Signatur definitionsbasiert benannt; Runtime-/Startup-Validation-Records tragen `WorkflowDefinitionKey` statt `PrimaryLegacyProcessTypeKey`.

Zweiter Teilschnitt (semantischer Rest um `node.config.legacyProcessTypeKey`) ist ebenfalls erledigt:

- Neuer aktiver Anker ist überall `node.config.workflowDefinitionKey`. `legacyProcessTypeKey` ist nur noch read-only-Fallback für bereits persistierte Definitionen.
- Runtime-Engine, Snapshot-Validator, Draft-Validator und Lifecycle-Startup-Validierung lesen über zentrale Helfer (`TryGetWorkflowDefinitionKeyFromNodeConfig` / `GetRequiredWorkflowDefinitionKeyFromNodeConfig`) mit Fallback; Publish-/Draft-Validatoren erzwingen den neuen Schlüssel als Pflicht-Property.
- Supervisor-Gatekeeper-Regel-Record wurde von `LegacyProcessTypeKey` auf `WorkflowDefinitionKey` umbenannt; Fehlercode `unknown_form_legacy_process_type` → `unknown_form_workflow_definition_key`.
- FE-Builder-Hint, Builder-Hook-Kommentar und der Referenzdaten-Hook (`useAdminWorkflowVersionReferenceData`) lesen jetzt `workflowDefinitionKey` zuerst und fallen für persistierten Altbestand auf `legacyProcessTypeKey` zurück.
- Seeds (`db/02_dev_seed.sql`, `db/02_bootstrap.sql`) und sämtliche Test-Fixtures (BE + FE) schreiben den neuen Schlüssel.

Damit andocken neue aktive Pfade nicht mehr an `legacyProcessTypeKey`; nur der Read-Fallback bleibt als Brücke für persistierten Altbestand stehen.

---

## Restinventur

| Cluster | Wo noch sichtbar | Was für die Umstellung nötig ist | Nutzen | Aufwand / Risiko |
| --- | --- | --- | --- | --- |
| `node.config.legacyProcessTypeKey` (semantisch) | erledigt | Aktive Pfade lesen + erzwingen `workflowDefinitionKey`; `legacyProcessTypeKey` bleibt nur read-only-Fallback für persistierten Altbestand. Seeds + Tests auf den neuen Schlüssel migriert. |
| `PrimaryLegacyProcessTypeKey` (Naming-Rest auf DTO-/Record-Properties) | erledigt 2026-05-11 | Parameter `primaryLegacyProcessTypeKey` in `ResolveWorkflowDefinitionLegacyProcessTypeId` → `workflowDefinitionKey`; Fehlermeldungen entlegacyt; Skizzen-Dokus nachgezogen. |
| `LegacyProcessTypeKey` / `ProcessTypeName` in Notification-DTOs und Link-Lookup | erledigt 2026-05-11 | `WorkflowNotificationDispatchTarget`, `WorkflowNotificationRenderContext`, `WorkflowLinkLookupRecord` + alle 8 Consumer-Dateien auf `WorkflowDefinitionKey` / `WorkflowDefinitionName` umbenannt. Kein Semantik-Change. |
| Internes `processType*`-Naming in Backend-Workflow-Pfaden | erledigt 2026-05-11 | `WorkflowStatusRules`, `PostgresWorkflowStatusCalculationService`, `PostgresRepositorySharedHelpers` (`WorkflowTaskGenerationContext`), `PostgresWorkflowTaskGenerationService`, `PostgresWorkflowRepository.*` (Create-/QueryOperations, lokaler Record), `NotificationEmailTemplateBuilder`, `NotificationTemplateService`, `WorkflowLifecycleService` + Tests: `processTypeName`/`processTypeKey`/`ProcessTypeCreateRecord`/`ResolveProcessTypeEmailContext` → `workflowDefinitionName`/`workflowDefinitionKey`/`WorkflowDefinitionCreateRecord`/`ResolveWorkflowDefinitionEmailContext`. Fehlermeldungen entlegacyt. Kein Semantik-Change. |
| Legacy-Status im Runtime-Pfad | erledigt 2026-05-11 | `legacyStatus` als SQL-Parametername in `WorkflowLifecycleService.cs` + `PostgresWorkflowRuntimeRepository.cs` auf `workflowStatus` umbenannt. Kein Semantik-Change. |
| Legacy-Sprache in Seeds und Tests | erledigt 2026-05-11 | `TemporaryProcessType`-Hilfsklassen in AdminConfig- und Link-Integrationstests → `TemporaryWorkflowDefinition`; alle lokalen Vars, Parameter und SQL-@params mitgezogen. `primaryLegacyProcessTypeKey` aus FE-Test-Fixtures entfernt (Property nicht mehr im Typ). Stub-Parameternamen `legacyProcessTypeKey` → `workflowDefinitionKey` in `WorkflowLifecycleServiceTests`. `legacyProcessTypeKey` in config_json-Fixtures bleibt (korrekt, da Produktiv-JSON-Fallback-Struktur). |

---

## Empfohlene Abarbeitungsreihenfolge

1. ~~`setup`-Realitätscheck und Doku-Drift bereinigen~~ — ✓ erledigt 2026-05-11
2. ~~Veraltete Doku-/ERD-Artefakte~~ — ✓ erledigt 2026-05-11 (ERD neu generiert, Migrationspfad + Zielarchitektur aktuell)
3. ~~Read-DTOs und API-Benennung auf den neuen Anker ziehen~~ — ✓ erledigt 2026-05-11 (`WorkflowDefinitionRefDto`, `workflowDefinition`, `ComputedStatus`)
4. ~~`node.config.legacyProcessTypeKey` semantisch abschneiden~~ — ✓ erledigt 2026-05-11 (read-Fallback bleibt; aktive Pfade ankerlos auf `workflowDefinitionKey`).
5. ~~`PrimaryLegacyProcessTypeKey`-Properties als reines Naming-Folge-Slice umbenennen.~~ — ✓ erledigt 2026-05-11
6. ~~Seed-/Test-Cleanup~~ — ✓ erledigt 2026-05-11 (gemeinsam mit Schritt 4).
7. ~~Internes `processType*`-Naming in Backend-Workflow-Pfaden bereinigen~~ — ✓ erledigt 2026-05-11 (WorkflowStatusRules, PostgresWorkflowStatusCalculationService, WorkflowTaskGenerationContext, WorkflowCreateOperations, WorkflowQueryOperations, NotificationEmailTemplateBuilder + Consumer).
8. ~~`legacyStatus` als SQL-/Parameter-Name im Runtime-/Lifecycle-Pfad~~ — ✓ erledigt 2026-05-11 (`WorkflowLifecycleService.cs`, `PostgresWorkflowRuntimeRepository.cs`: `legacyStatus` → `workflowStatus`).
9. ~~`CompletedOnboarding*`-Fachbegriff-Rest in BE/FE~~ — ✓ erledigt 2026-05-11 (`GetCompletedOnboardingSource` → `GetSourceWorkflow`; DTO-Properties und FE-Typen auf `LatestSourceWorkflowUid`/`CompletedAt`; UI-Labels entlegacyt).
10. ~~Test-/Fixture-Hygiene für verbliebene Legacy-Benennung~~ — ✓ erledigt 2026-05-11 (`TemporaryProcessType` → `TemporaryWorkflowDefinition`; `primaryLegacyProcessTypeKey` aus FE-Fixtures; Stub-Params in `WorkflowLifecycleServiceTests`).

---

## Was Der Nächste Technische Schnitt Bringt

Der größte operative Nutzen liegt im Key-Cut:

- weniger Übergangslogik in Builder, Runtime und Publish-Validierung
- klarere API-Verträge ohne "legacy" im zentralen Workflow-Pfad
- weniger Risiko, dass neue Features versehentlich wieder an alte `processType`-Annahmen andocken

Der größte organisatorische Nutzen liegt im Doku-Cleanup:

- neue Review-Zyklen starten von der echten Architektur aus
- weniger Zeitverlust durch veraltete ERD-/Plan-Aussagen

---

## Nicht Teil Dieses Plans

- Repository-Schnitte als Strukturarbeit
- Automation-Layer mit echten produktiven Handlern
- allgemeine Performance-Themen
- Builder-UX-Verbesserungen ohne Legacy-Bezug

Diese Themen bleiben wichtig, sind aber nicht der Kern des noch offenen Legacy-Abbaus.

---

## Pflege-Hinweis

Wenn ein Cluster erledigt ist:

1. Status hier anpassen.
2. `KauthWorkflow/Architektur/Migrationspfad.md` Parallelzustand nachziehen.
3. `CODEX_SYNC.md` kurz aktualisieren.
4. Nur dann `MEMORY.md`/`TODO.md` anfassen, wenn daraus wieder aktive Arbeit entsteht.
