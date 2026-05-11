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
- `legacyProcessTypeKey` / `PrimaryLegacyProcessTypeKey` als Brücke für Builder, Gatekeeper, Requirements und Teile der Runtime

---

## Bereits Abgebaut

| Thema | Status | Kurznotiz |
| --- | --- | --- |
| `process_types`-Tabelle | erledigt | Physische Tabelle und zentrale FKs sind abgebaut. |
| `task_templates`-Cluster | erledigt | Durch `workflow_node_task_specs*` ersetzt. |
| `CompletedOnboarding*`-Aliaspfade | erledigt | Fachlich neutrale Endpunkte und DTOs sind produktiv. |
| Permission-Schema `workflows.create.<key>` | erledigt | Quelle ist `workflow_definitions.key`, nicht mehr `process_types.key`. |
| `approval_task_template_key` | erledigt | Rename auf `approval_spec_key` inkl. manueller DB-Helfer dokumentiert. |
| `setup`-Node-Type (Code/Seeds) | erledigt | Kein `node_type = 'setup'` mehr in aktivem Code oder Seeds; nur noch in `db/_archive/`. Einziger Restpunkt: DB-Inventur gegen persistierte Definitionen (manuell, kein DB-Zugriff hier). |
| Veraltete Doku-/ERD-Artefakte | erledigt | ERD neu generiert (2026-05-11): `process_types`, `task_templates` entfernt, `workflow_node_task_specs*` und `person_match_audit_log` korrekt zugeordnet. Migrationspfad und Zielarchitektur sind aktuell. |
| `processType`-Benennung in Read-DTOs | erledigt | `WorkflowProcessTypeDto` → `WorkflowDefinitionRefDto`, Property `processType` → `workflowDefinition` in BE + FE. `LegacyStatus` / `MapLegacyStatusForActiveNodes` → `ComputedStatus` / `ComputeWorkflowStatusFromActiveNodes`. Seed-Descriptions bereinigt. 494 Tests grün. |

---

## Restinventur

| Cluster | Wo noch sichtbar | Was für die Umstellung nötig ist | Nutzen | Aufwand / Risiko |
| --- | --- | --- | --- | --- |
| `legacyProcessTypeKey` / `PrimaryLegacyProcessTypeKey` | `api/API/Services/WorkflowLifecycleService.cs`, `WorkflowRuntimeEngine*.cs`, `LifecycleStartupValidationExtensions.cs`, `PostgresWorkflowRuntimeRepository.cs`, `PostgresWorkflowRepository.MasterDataOperations.cs`, `WorkflowDefinitionAdminOperations.cs`, `web/src/hooks/adminWorkflowBuilderModel.ts`, `useAdminWorkflowVersionReferenceData.ts`, Seeds in `db/02_*.sql` | Gatekeeper-/Form-Konfiguration und Builder intern auf `workflowDefinitionKey` umstellen; Requirements/WorkflowConfig/Answer-Definition-Loads endgültig definitionsbasiert machen; Seed-JSON und Tests mitziehen | Entfernt die letzte echte Brücke zwischen Definition-Layer und Altwelt; weniger Sonderlogik in Runtime, Publish-Validierung und Builder | hoch / hoch |
| Legacy-Status im Runtime-Pfad | `api/API/Services/WorkflowRuntimePlan.cs`, `WorkflowRuntimeEngine.cs`, `PostgresWorkflowRuntimeRepository.EngineAdapter.cs`, `WorkflowRuntimeEngineTests.cs` | Die Mapping-Logik bleibt korrekt (`ComputeWorkflowStatusFromActiveNodes`); Benennung ist bereinigt. Einzig verbliebener Punkt: ggf. `legacyStatus` als SQL-Parametername in `WorkflowLifecycleService.cs` + `PostgresWorkflowRuntimeRepository.cs` nach großem Key-Cut nachziehen. | niedrig / niedrig nach Key-Cut |
| Legacy-Sprache in Seeds und Tests | Test-Fixtures mit `legacyProcessTypeKey` in config_json (korrekt, weil Produktiv-JSON-Struktur) und `TemporaryProcessType`-Hilfsklassen in Integrationstests | Erst nach dem großen Key-Cut nachziehen; vorher wären die Fixture-Werte falsch | niedrig / niedrig |

---

## Empfohlene Abarbeitungsreihenfolge

1. ~~`setup`-Realitätscheck und Doku-Drift bereinigen~~ — ✓ erledigt 2026-05-11
2. ~~Veraltete Doku-/ERD-Artefakte~~ — ✓ erledigt 2026-05-11 (ERD neu generiert, Migrationspfad + Zielarchitektur aktuell)
3. ~~Read-DTOs und API-Benennung auf den neuen Anker ziehen~~ — ✓ erledigt 2026-05-11 (`WorkflowDefinitionRefDto`, `workflowDefinition`, `ComputedStatus`)
4. `legacyProcessTypeKey` / `PrimaryLegacyProcessTypeKey` abschneiden. Das ist der eigentliche Kernrest des Legacy-Abbaus.
5. Seed-/Test-Cleanup als Schlussarbeit (erst nach Key-Cut sinnvoll, da config_json-Fixtures vorher korrekt sind).

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
