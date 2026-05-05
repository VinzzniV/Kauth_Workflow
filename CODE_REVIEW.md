# Code Review — kauth_workflow

**Stand**: 2026-05-05 — nach Abschluss von Zyklus 6.
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..03 Zyklus 2–5; 2026-05-03..04 Zyklus 6; 2026-05-05 Zyklus 7).

---

## Aktuelle Gesamtbewertung

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| Backend-Architektur | **A-** | Repository-Monolith aufgespalten; TaskTemplate 3-fach; GraphMapping ausgelagert; Lifecycle-Service nach S7 wirksam, aber als reine Commit-Grenze noch unvollstaendig |
| Datenbankdesign | **A-** | Solides Schema, gute Constraints |
| Auth & Berechtigungen | **B+** | Permission-Audit hat Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | **B+** | RotationTaskRegenerationEngine als pure Domain-Engine; HQ5-Hooks getestet; Sweep-Timeout |
| Frontend-Architektur | **B+** | Builder + Listen-Workspaces refactored; Split-Views; Karten-/Tabellenmodus; AdminConfig-Bundle-Refactor |
| Testbarkeit | **B** | Testcontainers + Integration-Tests; Lifecycle-Service hat jetzt eine eigene Service-Testdatei fuer Routing, Rollback und Automation-Scope |
| Skalierbarkeit | **B-** | Workflow-Task-Filter SQL-pre-narrowed |
| Sicherheit | **B+** | `/client/log-events` rate-limited; dev-sim-Guard hard-throw |
| Lesbarkeit | **B+** | Konventionen durchgaengig; Validation-Service als groesster Monolith ausstehend |

---

## Aktiver Zyklus 7 — Lifecycle-Service-Konsolidierung (2026-05-05)

**Thema:** Folgearbeit aus Zyklus 6 (Schritt 7). Der Lifecycle-Service existiert nominell, ist als zentrale Commit-Grenze fuer Runtime- und Task-Mutationen aber noch nicht vollstaendig wirksam. Zusaetzlich bleibt `WorkflowDefinitionValidationService` der groesste verbleibende Service-Monolith.

**Fokus:**
1. Lifecycle-Service als echte Schreibgrenze konsolidieren (Pass-Through-Routen + statische Repo-Aufrufe + parallele Aufrufstellen).
2. Eigene Test-Suite fuer den Lifecycle-Service.
3. Validation-Service splitten (Draft vs. Snapshot).

**Priorisierung:**

| ID | Befund | Prio |
|----|--------|------|
| Z7-1.1 | Lifecycle-Inventur (Aufrufer + Conn+Tx-Bedarf + Doppelpfade) | **HIGH** — done |
| Z7-2 | `WorkflowLifecycleService` Test-Coverage (Routing + Rollback + Automation-Scope) | **HIGH** — done am 2026-05-05 |
| Z7-1.2 | Definition-Runtime-Mutationen ueber Lifecycle Conn+Tx | **HIGH** — done am 2026-05-05 |
| Z7-1.3 | Statische Runtime-Helfer als Scoped-Repo-Instanzmethoden | **HIGH** — done am 2026-05-05 |
| Z7-1.4 | Doppelte Aufrufstellen `TaskOperations.cs:84/86/158` + `AutomationOperations.cs:100` aufloesen | **HIGH** — done am 2026-05-05 |
| Z7-1.5a | Public non-scope Wrapper aus `PostgresWorkflowRuntimeRepository` + Interface entfernen, Tests/Stubs auf Lifecycle-Service migrieren | **HIGH** — done am 2026-05-05 |
| Z7-1.5b | Inhalte der `*InScope`-Methoden physisch in den Lifecycle-Service ziehen (in 5 Sub-Slices b.i–b.v zerlegt) | **HIGH** — done am 2026-05-05 (alle Sub-Slices) |
| Z7-3 | `WorkflowDefinitionValidationService` (2131 Z.) splitten | MEDIUM |

### Z7-1 Lifecycle-Service-Konsolidierung (HIGH)

**Befund (nach Z7-1.1 Inventur, 2026-05-05).** Nach Abschluss von Schritt 7 ist `WorkflowLifecycleService` (`api/API/Services/WorkflowLifecycleService.cs`, 79 Z.) ein duenner Wrapper im Mid-State. Detail-Inventur in `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md` § 12.

- Vier Methoden (`CreateWorkflowInstanceAsync`, `CompleteFormNodeAsync`, `CompleteApprovalNodeAsync`, `CompleteTaskNodeAsync`) sind reine Pass-Throughs an `IWorkflowDefinitionRuntimeRepository`. Conn+Tx wird im Repo geoeffnet, nicht im Service. Einziger produktiver Aufrufer: `WorkflowDefinitionRuntimeService.CompleteAndDispatchAsync` (Notification-Dispatch + PersonLifecycleProjection bewusst ausserhalb der Tx).
- Drei statische Repo-Aufrufe (`PostgresWorkflowRuntimeRepository.CompleteTaskNodeRuntimeSide`, `TryAdvanceSetupNodeIfReady`, `ApplyApprovalNodeDecision`) im Service-Body (`WorkflowLifecycleService.cs:20/22/45`) lassen den Service an einer konkreten Repo-Klasse haengen.
- Dieselben drei statischen Aufrufe stehen ein zweites Mal in `PostgresWorkflowRepository.TaskOperations.cs:84/86/158`. **Aufgeloest:** Endpoint-Pfad ist bereits ueber `TaskApplicationService` am Lifecycle-Service; produktive Aufrufer von `repository.UpdateTaskStatus` / `DecideTaskApproval` existieren nicht mehr — nur Tests rufen sie noch (z. B. `PostgresWorkflowRepositoryConcurrencyTests`).
- Symmetrisch gilt das fuer `PostgresWorkflowRepository.AutomationOperations.cs:100` (`CompleteAutomationJobSuccess`-Wrapper): `WorkflowAutomationService` geht ueber `lifecycleService.OnAutomationJobCompletedAsync`, der Wrapper hat keinen produktiven Aufrufer mehr.
- Vor-Code in `PostgresWorkflowRuntimeRepository.cs:487-512` (Approval Task-Status-Sync + Audit) und `:547-572` (Task-Status-Sync + Audit) wandert mit Z7-1.2 mit. Wird das uebersehen, drohen doppelte Audit-Eintraege oder verlorene `task_status_changed`-Events.

**Wirkung.** Lifecycle-Service besitzt Conn+Tx fuer Task-Status, Approval und Automation (3 Pfade) — nicht fuer Definition-Runtime (4 Pass-Throughs).

**Slice-Plan (nach Inventur reordered):**
- ✅ 7.1.1 Inventur: erledigt. Ergebnis: §12 in der S7-Skizze.
- 7.2 (vorgezogen) Test-Coverage als Sicherheitsnetz, **bevor** die Conn+Tx-Bewegung greift.
- ✅ 7.1.2 Definition-Runtime-Mutationen ueber Scoped-Repo-Vertrag gefuehrt; neuer `IWorkflowDefinitionRuntimeScopedRepository`, Vor-Code aus `PostgresWorkflowRuntimeRepository.cs:487-512/547-572` bleibt im Runtime-Repo, aber unter Lifecycle-owned Tx. `WorkflowRuntimeService.CreateWorkflowAsync` routed jetzt ebenfalls ueber `IWorkflowLifecycleService`.
- ✅ 7.1.3 Statische Runtime-Helfer (`CompleteTaskNodeRuntimeSide`, `TryAdvanceSetupNodeIfReady`, `ApplyApprovalNodeDecision`) als `*InScope`-Instanzmethoden in `IWorkflowDefinitionRuntimeScopedRepository`/`IWorkflowLifecycleScopedRepository`. Lifecycle-Service haengt nur noch an Interfaces.
- ✅ 7.1.4 Dupletten in `TaskOperations.cs` (`UpdateTaskStatus`/`DecideTaskApproval`) und `AutomationOperations.cs` (`CompleteAutomationJobSuccess`-Wrapper) entfernt; `*ByRef`-Pfade rotation-only; Tests auf `WorkflowLifecycleService` migriert.
- ✅ 7.1.5a Public non-scope Wrapper (`CreateWorkflowDefinitionInstance`, `CompleteRuntimeFormNode`, `CompleteRuntimeApprovalNode`, `CompleteRuntimeTaskNode`) aus `PostgresWorkflowRuntimeRepository` + `IWorkflowDefinitionRuntimeRepository` entfernt; Integrationstests rufen jetzt `WorkflowLifecycleService` direkt; `StubWorkflowLifecycleService` in `WorkflowEndpointsTests` liefert das gestubte Create-Result selbst.
- 7.1.5b Inhalte der `*InScope`-Methoden physisch in den Lifecycle-Service ziehen, Scoped-Vertrag aufloesen. Aufgeteilt in:
  - ✅ 7.1.5b.i `CompleteRuntimeFormNodeInScope` in den Service gezogen (2026-05-05); Helper `LoadRuntimeWorkflowHeader`/`LoadNodeExecutionForUpdate`/`EnsureActiveRuntimeNode`/`CompleteRuntimeFormNodeInternal`/`GetWorkflowDefinitionRuntimeDetailInternal` auf `internal static`.
  - ✅ 7.1.5b.ii `CompleteRuntimeApprovalNodeInScope` in den Service gezogen (2026-05-05); Service injiziert `IWorkflowAuditWriteOperations` + `IWorkflowStatusCalculationService` fuer linked-task Status-Sync; `LoadWorkflowTaskIdByNodeInstanceId` auf `internal static`.
  - ✅ 7.1.5b.iii `CompleteRuntimeTaskNodeInScope` in den Service gezogen (2026-05-05); Service nutzt `IWorkflowAuditWriteOperations` + `IWorkflowStatusCalculationService` fuer linked-task Status-Sync und ruft `CompleteTaskNodeRuntimeSide` direkt; Scoped-Vertrag haelt nur noch `CreateWorkflowDefinitionInstanceInScope`.
  - ✅ 7.1.5b.iv `CreateWorkflowDefinitionInstanceInScope` in den Service gezogen (2026-05-05); Service injiziert `IWorkflowNotificationDispatchOperations` und ruft `LoadPublishedWorkflowDefinitionVersion`/`CreateWorkflowNodeInstance`/`NormalizeRuntimeOptionalText` (auf `internal static` gehoben) direkt; Scoped-Vertrag ist jetzt leer.
  - ✅ 7.1.5b.v Leeren Scoped-Vertrag `IWorkflowDefinitionRuntimeScopedRepository` und DI-Registrierung entfernt (2026-05-05); Lifecycle-Service-Ctor um den ungenutzten Parameter bereinigt; `PostgresWorkflowRuntimeRepository` implementiert nur noch `IWorkflowDefinitionRuntimeRepository`; Test-Stubs entfernt.

**Aktueller Stand nach Z7-1.5b.v.** Lifecycle-Service orchestriert Create-Instance, Form-, Approval- und Task-Node komplett direkt; der Scoped-Vertrag fuer Definition-Runtime ist abgebaut. Z7-1.5b ist damit insgesamt erledigt.

### Z7-2 Lifecycle-Service Test-Coverage (HIGH, done 2026-05-05)

**Ergebnis.** `api/API.Tests/WorkflowLifecycleServiceTests.cs` ist jetzt vorhanden. Abgedeckt sind:
- Routing `wf:` vs. `rot:` fuer `UpdateTaskStatusByRefAsync` und `DecideTaskApprovalByRefAsync`
- Conn+Tx-Boundary: Fehler im zweiten Lifecycle-Schritt rollt einen vorbereiteten Task-Status-Schreibzugriff zurueck
- `OnAutomationJobCompletedAsync` laeuft ueber den Scoped-Repo-Pfad in offener Transaktion

**Wirkung.** Z7-1.2 kann die Vor-Code-Migration in `PostgresWorkflowRuntimeRepository.cs:487-512/547-572` jetzt mit direkter Service-Absicherung angehen, statt sich nur auf Endpoint-/Repo-Tests zu verlassen.

### Z7-3 WorkflowDefinitionValidationService Split (MEDIUM)

**Befund.** `api/API/Services/WorkflowDefinitionValidationService.cs` (2131 Z.) ist nach den Z3/Z4-Splits der groesste verbleibende Service-Monolith. Zwei klar separierbare Pfade:
- **Draft-Validation** (`NormalizeDefinitionKey`, `ValidateAndNormalize` fuer `ReplaceWorkflowDefinitionVersionRequest`): Pre-Save Normalisierung von Builder-Eingaben.
- **Snapshot-Validation** (`ValidateSnapshot` ueber `WorkflowDefinitionValidationContext`): Validierung gespeicherter Versionen, separate Helper-Sets.

Mehrere Helper existieren als Overload-Paar (z. B. `ValidateGraphStructure`, `ValidateReachability`, `ValidateNodeConfigurations`, `ValidateNodeActions`, `ValidateDecisionConditions`, `ValidateRequiredStringConfig`) — einer fuer Draft-, einer fuer Snapshot-Pfad.

**Empfehlung.** Split in `WorkflowDefinitionDraftValidator`, `WorkflowDefinitionSnapshotValidator` und gemeinsamen `WorkflowDefinitionValidationHelpers` (statische Pure-Funktionen). Konstanten (`AllowedNodeTypes`, `MeasureGenerationNodeTypes`, `ExpectedMeasureNodeTypeByDefinitionKey`, `SupportedDecisionOperators`) wandern in eine kleine `WorkflowDefinitionValidationCatalog`-Klasse. Tests in `WorkflowDefinitionValidationServiceTests.cs` bestehen bleiben, falls noetig in zwei Test-Dateien geteilt.

**Slice-Plan (2026-05-05, inkrementell, kein Big-Bang):**
- **Z7-3.1 (done 2026-05-05)** — `WorkflowDefinitionValidationCatalog` extrahieren: vier Konstanten-Sets (`SupportedDecisionOperators`, `AllowedNodeTypes`, `MeasureGenerationNodeTypes`, `ExpectedMeasureNodeTypeByDefinitionKey`) in neue interne Klasse ziehen; `WorkflowDefinitionValidationService` referenziert nur noch den Catalog. Niedrigstes Risiko, reine Move-Operation, keine Verhaltensaenderung.
- **Z7-3.2 (done 2026-05-05)** — `WorkflowDefinitionValidationHelpers` extrahieren: pure statische Helfer (`NormalizeRequiredKey`, `NormalizeOptionalText`, `TryNormalizeRequiredKey`-Overloads, `HasConfig`, `CreateIssue`, `ValidateRequiredStringConfig`-Overloads, `ValidateOptionalObjectConfig`-Overloads) in neue interne `static class`. Service haelt private Forwarder fuer unveraenderte Call-Sites; keine Verhaltensaenderung.
- **Z7-3.3 (done 2026-05-05)** — `WorkflowDefinitionSnapshotValidator` extrahieren: `ValidateSnapshot` plus alle issues-basierten Validate-Overloads (`ValidateGraphStructure`, `ValidateReachability`, `ValidateNodeConfigurations`, `ValidateNodeActions`, `ValidateDecisionConditions`, `ValidateGatewayTopology`, `ValidateMeasurePhaseTopology`), die snapshot-only Validierungen (`ValidateSupervisorGatekeeper`, `ValidateMeasurePhaseProcessConsistency`) und die Snapshot-/`*ForRead`-Normalisierer (`NormalizeNodeActions`/`NormalizeNodeSpecs`/`NormalizeSpecConditions`/`NormalizeSpecDependencies`/`ValidateSpecDependenciesPointToSiblings`) in neue interne `static class WorkflowDefinitionSnapshotValidator`. Service haelt nur noch eine Delegation. Shared zwischen Write- und Snapshot-Pfad gemeinsam genutzte Helfer (`CloneConfig`, `FindUnreachableNodeKeys`, `HasAnyExpectedValue`, `IsMeasureGenerationNodeType`, `GetExpectedMeasureNodeTypeForDefinitionKey`, `NodeTypeCanHaveSpecs`, `NodeTypeAllowsAtMostOneSpec`, `TryGetNodeConfigValue`) sind nach `WorkflowDefinitionValidationHelpers` gewandert; `AllowedSpecConditionOperators` nach `WorkflowDefinitionValidationCatalog`. Service ist von 1971 auf ~700 Zeilen geschrumpft. 416/417 Tests gruen.
- **Z7-3.4** — `WorkflowDefinitionDraftValidator` extrahieren: Rest (`NormalizeDefinitionKey`, `ValidateAndNormalize`, Draft-Overload-Varianten der Validate-/Normalize-Helfer). Service wird Facade oder verschwindet, falls nichts mehr uebrigbleibt.

Naechster Schritt: Z7-3.4 (DraftValidator). Reasoning: medium. Modell: sonnet.

---

## Offene Befunde aus frueheren Zyklen

| ID | Aufgabe | Status | Quelle |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | offen — Nutzer-Aufgabe, KI kann nicht pruefen | L7 |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | backlog — kein konkreter Bedarf | L7 |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | deferred — wartet auf Produkt-Entscheidung | Zyklus 1 |
| LQ2-Z3 | `EntraDirectorySyncService.cs` (2485 Z.) Split | deferred ohne Trigger — Risiko niedrig (Timer-Pfad). Refactor erst bei Anlass | Zyklus 3 |

---

## Zyklus-Historie

Detail-Reports zu Zyklus 1–6 sind aus dieser Datei entfernt — Detail im `git log` und in `KauthWorkflow/Stand/Code-Review-Status.md`.

| Zyklus | Datum | Hauptthema |
|--------|-------|------------|
| 1 | 2026-04-23 | Code-Review + Hardening (C1–C4, H1–H7, L1/L3/L5/L6) |
| 2 | 2026-05-02 | HQ1–HQ5 + LQ1–LQ7: Decision-Migration, SQL-Task-Filter, Audit-Trail, Testcontainers, Page-Refactor |
| 3 | 2026-05-02 | Test-Coverage + Wartbarkeits-Split: Hook-Tests, Repo-Splits, Hook-Zerlegung |
| 4 | 2026-05-02 | Naming + Haertungen: LegacyProcessTypeKey, effectiveResponsibilityIds, Error-Boundaries |
| 5 | 2026-05-02..03 | Legacy-Abbau (LA1–LA5): LegacyWorkflowStatus, setup-Node, definition_key, HasLegacyRolePermission, Specs am Node |
| 6 | 2026-05-03..04 | Runtime-Lifecycle (Schritt 7): Engine-Extraktion + Lifecycle-Service mit Conn+Tx-Scope; 409 Tests gruen |
| 7 | 2026-05-05 (aktiv) | Lifecycle-Service-Konsolidierung + Validation-Split (siehe oben) |

---

## Verwandte Dokumente

- `TODO.md` — aktuelle Priorisierung + Aufgabenstatus (Backend / Full-Stack)
- `FRONTEND_TODO.md` — Frontend-spezifischer Backlog (Zyklus 7 ist backend-fokussiert)
- `KauthWorkflow/Architektur/Migrationspfad.md` — Gesamtbild der Migration
- `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md` — Architekturarbeit aus Zyklus 6
- `KauthWorkflow/Stand/Code-Review-Status.md` — Vault-Spiegel dieses Status
