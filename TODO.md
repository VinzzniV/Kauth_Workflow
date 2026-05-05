# TODO.md

Diese Datei steuert die Reihenfolge der Umsetzung aktiver Review-Zyklen.
Die aktuelle Priorisierung und Review-Begruendung stehen zentral in `CODE_REVIEW.md`.

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `CODE_REVIEW.md` lesen (Priorisierungs- und Analyseabschnitte).

Zusaetzlich immer mitlesen: `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md`, `MEMORY.md`.

Pflicht nach dem Lesen:
- Vor der Umsetzung kurz festhalten, welche Dokus mitgezogen werden muessen, falls sich Struktur, Scope, Verhalten, Setup oder Risiken aendern.
- Doku-Aenderungen gehoeren in denselben Arbeitsgang wie die Code-Aenderung.
- Bei Abschluss Status hier auf `done` setzen.

## Pflicht zwischen Aufgaben

Bevor die KI mit einer neuen Aufgabe anfaengt, **muss** sie ansagen:

1. **Welche Aufgabe als naechstes ansteht** (mit ID/Block-Bezeichner aus TODO.md)
2. **Reasoning Effort** (`low` / `medium` / `high`)
3. **Empfohlenes Modell** (`sonnet` / `opus`)

Format-Beispiel: *„Naechster Schritt: Z7-1.1 Lifecycle-Inventur. Reasoning: high. Modell: opus."*

## Aufgabenteilung Codex/Claude

> Fuer jede Aufgabe gilt: Die KI, die sie abschliesst, traegt Datum + kurze Aenderungszusammenfassung in `CODEX_SYNC.md` ein.
> Claude liest `CODEX_SYNC.md` am Sitzungsanfang, um Codex-Aenderungen nachzuvollziehen.

---

## Aktiver Zyklus 7 — Lifecycle-Service-Konsolidierung (2026-05-05)

Begruendung in `CODE_REVIEW.md` § "Aktiver Zyklus 7".

| ID | Aufgabe | Prio | Status | Reasoning Effort | Modell | Hinweis |
|----|---------|------|--------|------------------|--------|---------|
| Z7-1.1 | Inventur: alle Aufrufer der vier Pass-Through-Methoden in `WorkflowLifecycleService` (`CreateWorkflowInstance`, `CompleteFormNode`, `CompleteApprovalNode`, `CompleteTaskNode`) plus die zwei Aufrufstellen der drei statischen Repo-Helfer kartieren. Ergebnis: Conn+Tx-Bedarf je Pfad, Notification-Dispatch-Grenze dokumentiert. | HIGH | done | high | opus | Ergebnis: §12 in `Schritt7-Runtime-TaskSystem-Skizze.md`. Befunde unten in 7.1.2–7.1.5 eingearbeitet. |
| Z7-2 | `WorkflowLifecycleServiceTests.cs` anlegen: Routing WorkflowTaskRef vs. RotationTaskRef, Conn+Tx-Boundary (Fehler im zweiten Schritt rollt ersten zurueck), `OnAutomationJobCompletedAsync`-Erfolgspfad. | HIGH | done | medium | sonnet | 2026-05-05 erledigt. 6 Tests gruen (`dotnet test API.Tests.csproj --filter WorkflowLifecycleServiceTests`). |
| Z7-1.2 | Definition-Runtime-Mutationen ueber `IWorkflowLifecycleScopedRepository` (oder Erweiterung) fuehren. `WorkflowLifecycleService` oeffnet Conn+Tx, Repo-Methoden bekommen explizite `(NpgsqlConnection, NpgsqlTransaction)`-Signatur. **Achtung:** Vor-Code in `PostgresWorkflowRuntimeRepository.cs:487-512` (Approval Task-Status-Sync + Audit) und `:547-572` (Task Vor-Code) muss mitwandern, sonst doppelte Audit-Eintraege oder verlorene `task_status_changed`-Events. | HIGH | done | high | opus | 2026-05-05 erledigt. Neuer Scoped-Vertrag `IWorkflowDefinitionRuntimeScopedRepository`; Lifecycle-Service owns Tx fuer Create/Form/Approval/Task; `WorkflowRuntimeService`-Create routed jetzt ueber Lifecycle. |
| Z7-1.3 | Statische Repo-Helfer `CompleteTaskNodeRuntimeSide` / `TryAdvanceSetupNodeIfReady` / `ApplyApprovalNodeDecision` als Service-private oder Scoped-Repo-Instanzmethoden ziehen; Service haengt nicht mehr an konkreter Repo-Klasse. | HIGH | done | medium | sonnet | 2026-05-05 erledigt. Helfer wandern als `*InScope`-Instanzmethoden in `IWorkflowDefinitionRuntimeScopedRepository` und `IWorkflowLifecycleScopedRepository`; Lifecycle-Service haengt nur noch an Interfaces. |
| Z7-1.4 | Doppelte Aufrufstelle `PostgresWorkflowRepository.TaskOperations.cs:84/86/158` aufloesen — Endpoints sind bereits am Lifecycle-Service; nur Tests rufen `repository.UpdateTaskStatus` / `DecideTaskApproval` noch direkt. Tests umstellen, Wrapper-Methoden + statische Aufrufstellen :84/86/158 loeschen. **Mit erledigen:** `PostgresWorkflowRepository.AutomationOperations.cs:100` (`CompleteAutomationJobSuccess`-Wrapper) hat ebenfalls keinen produktiven Aufrufer mehr (`WorkflowAutomationService` geht ueber Lifecycle). | HIGH | done | medium | sonnet | 2026-05-05 erledigt. `UpdateTaskStatus`/`DecideTaskApproval`/`CompleteAutomationJobSuccess` aus Repo+Interfaces entfernt; `*ByRef` jetzt rotation-only; Tests auf Lifecycle-Service migriert. 416/417 Tests gruen. |
| Z7-1.5a | Public non-scope Wrapper aus `PostgresWorkflowRuntimeRepository` und `IWorkflowDefinitionRuntimeRepository` entfernen (`CreateWorkflowDefinitionInstance`, `CompleteRuntimeFormNode`, `CompleteRuntimeApprovalNode`, `CompleteRuntimeTaskNode`); Tests + DI-Stubs auf Lifecycle-Service migrieren. **Kein** physisches Herausziehen der `*InScope`-Logik in den Service. | HIGH | done | medium | sonnet | 2026-05-05 erledigt. Wrapper aus Interface + Repo entfernt; `*InScope` bleibt im Repo unter Lifecycle-owned Tx; Integrationstests rufen Lifecycle-Service direkt; 416/417 Tests gruen. |
| Z7-1.5b.i | `CompleteRuntimeFormNodeInScope` aus dem Runtime-Repo + Scoped-Vertrag in den Lifecycle-Service ziehen. | HIGH | done | medium | sonnet | 2026-05-05 erledigt. Form-Node-Orchestrierung lebt jetzt im Service; Helper auf `internal static` gehoben; 416/417 Tests gruen. |
| Z7-1.5b.ii | `CompleteRuntimeApprovalNodeInScope` aus dem Runtime-Repo + Scoped-Vertrag in den Lifecycle-Service ziehen (linked-task-Prelude bleibt mit b.iii ggf. zusammenfuehrbar). | HIGH | done | medium | sonnet | 2026-05-05 erledigt. Approval-Orchestrierung lebt im Service; `IWorkflowAuditWriteOperations` + `IWorkflowStatusCalculationService` injiziert; `LoadWorkflowTaskIdByNodeInstanceId` auf `internal static` gehoben; 416/417 Tests gruen. |
| Z7-1.5b.iii | `CompleteRuntimeTaskNodeInScope` aus dem Runtime-Repo + Scoped-Vertrag in den Lifecycle-Service ziehen; `LoadWorkflowTaskIdByNodeInstanceId` ggf. auf `internal static` heben. | HIGH | done | medium | sonnet | 2026-05-05 erledigt. Task-Node-Orchestrierung lebt im Service; linked-task Status-Sync via `IWorkflowAuditWriteOperations` + `IWorkflowStatusCalculationService`; `CompleteTaskNodeRuntimeSide` direkt aufgerufen; Scoped-Vertrag haelt nur noch `CreateWorkflowDefinitionInstanceInScope`; 416/417 Tests gruen. |
| Z7-1.5b.iv | `CreateWorkflowDefinitionInstanceInScope` (~250 Z., haengt an `_notificationDispatch`) physisch in den Lifecycle-Service ziehen; ggf. dedizierter Helper-Typ. | HIGH | done | high | opus | 2026-05-05 erledigt. Lifecycle-Service injiziert `IWorkflowNotificationDispatchOperations`; `LoadPublishedWorkflowDefinitionVersion`/`CreateWorkflowNodeInstance`/`NormalizeRuntimeOptionalText` auf `internal static` gehoben; Scoped-Vertrag jetzt leer. 416/417 Tests gruen. |
| Z7-1.5b.v | Restlichen Scoped-Vertrag (`IWorkflowDefinitionRuntimeScopedRepository`) aufloesen oder auf reine SQL-Helfer reduzieren. | HIGH | done | low | sonnet | 2026-05-05 erledigt. Interface-Datei + DI-Registrierung entfernt; Lifecycle-Service-Ctor-Parameter bereinigt; `PostgresWorkflowRuntimeRepository` implementiert nur noch `IWorkflowDefinitionRuntimeRepository`; Test-Stubs entfernt. 416/417 Tests gruen. |
| Z7-3 | `WorkflowDefinitionValidationService.cs` (2131 Z.) splitten in `WorkflowDefinitionDraftValidator` + `WorkflowDefinitionSnapshotValidator` + `WorkflowDefinitionValidationHelpers` + `WorkflowDefinitionValidationCatalog` (Konstanten). Bestehende Tests anpassen. | MEDIUM | done | medium | sonnet | 2026-05-05 erledigt. Splits in 3.1–3.4 abgeschlossen; Service ist jetzt duenne Facade. 370 Unit-Tests gruen. |
| Z7-3.1 | `WorkflowDefinitionValidationCatalog` extrahieren: vier Konstanten-Sets aus dem Service in neue interne static class. | MEDIUM | done | low | sonnet | 2026-05-05 erledigt. Service haelt nur noch private Property-Aliase auf den Catalog; Verhalten unveraendert. 416/417 Tests gruen. |
| Z7-3.2 | `WorkflowDefinitionValidationHelpers` extrahieren (pure statische Helfer: `NormalizeRequiredKey`, `NormalizeOptionalText`, `ValidateRequiredStringConfig`-Overloads, `ValidateOptionalObjectConfig`-Overloads). | MEDIUM | done | low | sonnet | 2026-05-05 erledigt. Pure Helfer (Normalize/TryNormalize-Overloads, HasConfig, CreateIssue, ValidateRequiredStringConfig/ValidateOptionalObjectConfig-Overloads) in neue `WorkflowDefinitionValidationHelpers`-static-class gezogen; Service haelt private Forwarder fuer unveraenderte Call-Sites. 416/417 Tests gruen. |
| Z7-3.3 | `WorkflowDefinitionSnapshotValidator` extrahieren (`ValidateSnapshot` + Snapshot-Overload-Varianten). | MEDIUM | done | medium | sonnet | 2026-05-05 erledigt. `ValidateSnapshot` + alle issues-basierten Validate-/Normalize-Overloads + `*ForRead`-Spec-Helfer + `ValidateSupervisorGatekeeper` + `ValidateMeasurePhaseProcessConsistency` in neue `WorkflowDefinitionSnapshotValidator`-static-class; Service-Methode delegiert. Shared-Helfer (`CloneConfig`, `FindUnreachableNodeKeys`, `HasAnyExpectedValue`, Node-Type-Predicates, `TryGetNodeConfigValue`) in Helpers, `AllowedSpecConditionOperators` in Catalog. 416/417 Tests gruen. |
| Z7-3.4 | `WorkflowDefinitionDraftValidator` extrahieren (`NormalizeDefinitionKey`, `ValidateAndNormalize` + Draft-Overload-Varianten); Service als duenne Facade oder entfernen. | MEDIUM | done | medium | sonnet | 2026-05-05 erledigt. Draft-Pfad (`NormalizeDefinitionKey`, `ValidateAndNormalize`, alle Graph-/Action-/Spec-Validatoren und Normalisierer) in neue `WorkflowDefinitionDraftValidator`-static-class gezogen; `WorkflowDefinitionValidationService` ist jetzt eine duenne Facade ueber Draft- + Snapshot-Validator. 370 Unit-Tests gruen. |

**Naechster sinnvoller Schritt:** Z7 ist damit abgeschlossen — naechstes offenes Slice gemaess `CODE_REVIEW.md`.

---

## Watch-Items / Defer

| ID | Aufgabe | Status |
|----|---------|--------|
| LQ2-Z3 | `EntraDirectorySyncService` (2485 Z.) Split | defer ohne Trigger (Risiko niedrig — Timer-Pfad, kein User-Pfad) |

---

## Offene Restposten (zyklusuebergreifend)

| ID | Aufgabe | Quelle | Status |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | L7 | offen — Nutzer-Aufgabe |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | L7-Backlog | backlog — kein konkreter Bedarf |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | Zyklus 1 | deferred — wartet auf Produkt-Entscheidung |

---

## Abgeschlossene Zyklen

Zyklen 1–6 (2026-04-23 bis 2026-05-04) sind abgeschlossen. Detail-Historie via `git log`; Highlights pro Zyklus in `KauthWorkflow/Stand/Code-Review-Status.md`.

---

## Abschlussregel fuer jede KI-Aufgabe

Nach jedem groesseren Schritt muss berichtet werden:
1. Welche Dateien wurden geaendert?
2. Auf welchen Abschnitt in `CODE_REVIEW.md` wurde gearbeitet?
3. Wie passt die Aenderung zur Zielarchitektur?
4. Welche Risiken oder Luecken bleiben offen?
5. Welche Tests wurden angepasst oder fehlen noch?
6. Welche Doku musste mitgezogen werden?
7. Wurde die erledigte Aufgabe in `TODO.md` auf `done` gesetzt?
