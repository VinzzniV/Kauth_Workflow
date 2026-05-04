# Schritt 7 — Runtime ↔ Task-System

#architektur #migration #runtime #skizze

Migrationspfad-Schritt 7 (offen): "Task-System an Node-Runtime anbinden".
Diese Skizze klärt, was tatsächlich offen ist, welche Optionen sinnvoll sind,
und in welcher Reihenfolge umgesetzt werden sollte.

Status: **Skizze, kein Beschluss.** Die Empfehlung am Ende ist explizit so markiert.

---

## 1. Ausgangslage — was tatsächlich existiert

Anders als der Migrationspfad-Eintrag vermuten lässt, ist Schritt 7 **nicht "vom Reißbrett bauen"**.
Der Node-Lifecycle-Loop läuft bereits, und Tasks treiben ihn bereits weiter — nur an der falschen Stelle.

### Runtime-Loop existiert

Zentrale Engine: `PostgresWorkflowRuntimeRepository.AdvanceRuntimeUntilWaitOrTerminal`
(Zeilen 1003–1339, ca. 340 Z. SQL+C#).

Was der Loop kann:
- nimmt einen `completedNode` und einen Workflow-Definitionsgraph
- ermittelt via `ResolveNextNodes` Folgeknoten (inkl. Edge-Conditions bei Decisions)
- prüft Aktivierbarkeit via `CanActivateRuntimeNode` (z. B. Parallel-Join-Konvergenz)
- behandelt pro Node-Type:
  - `start` / `end` / `parallel_split` / `parallel_join` → automatisch durchlaufen
  - `decision` → wertet Edge-Conditions aus, wählt Target-Edge, setzt Audit-Event
  - `measure_provision` / `_deprovision` / `_change` / `_rename` → erzeugt Tasks via `EnsureRuntimeSetupTasksGenerated`, geht in `waiting_on_node`
  - `form` / `approval` / `task` → wartet (wird durch externe Completion-Events fortgeführt)
  - `automation` → triggert Automation-Job (siehe Schritt 9)
- `ShouldAutoCompleteSupervisorApprovalBridge` springt über bereits durch Form gefüllte Approval-Knoten
- bricht ab bei Fehler (`FailRuntimeWorkflow`), Wait-State, oder Terminal

Eintrittspunkte (heute 4):
1. `CreateWorkflowDefinitionInstance` → initialer Loop ab `start`
2. `CompleteRuntime{Form,Approval,Task}Node` (vom `WorkflowDefinitionRuntimeService`)
3. **Aus dem Task-System** (siehe §2): `CompleteRuntimeTaskNodeFromTaskStatusUpdate`,
   `TryAdvanceRuntimeSetupFromTaskStatusUpdate`, `ApplyRuntimeApprovalDecisionFromWorkflowTask`
4. **Aus dem Automation Layer**: `PostgresWorkflowRepository.AutomationOperations.cs:202`
   nach Job-Completion

### Task-System existiert separat

`TaskApplicationService` operiert auf `taskId`/`taskRef`, kennt Runtime-Konzepte nicht.
Status-Updates gehen über `IWorkflowRepository.UpdateTaskStatus`.

### Brücke Task → Runtime liegt im Repo, nicht im Service

`PostgresWorkflowRepository.TaskOperations.cs` ist die echte Kopplungsstelle:

```text
TaskApplicationService.UpdateTaskStatusAsync
  → repository.UpdateTaskStatus
  → PostgresWorkflowRepository.TaskOperations.UpdateTaskStatus  ← in DB-Transaction
       Z. 131: if (isRuntimeNodeTask && terminal)
                  → CompleteRuntimeTaskNodeFromTaskStatusUpdate  → AdvanceRuntimeUntilWaitOrTerminal
       Z. 145: else (Setup-Task einer measure-Node)
                  → TryAdvanceRuntimeSetupFromTaskStatusUpdate   → ggf. AdvanceRuntimeUntilWaitOrTerminal
                                                                  wenn alle Pflicht-Tasks done
```

Analog `DecideTaskApproval` (Z. 215): `ApplyRuntimeApprovalDecisionFromWorkflowTask`.

**Das funktioniert.** Onboarding/Offboarding/Department-Change laufen produktiv darüber.
Es ist aber nicht das Zielbild.

---

## 2. Was an der heutigen Lösung tatsächlich weh tut

### 2.1 Loop ist nicht testbar
`AdvanceRuntimeUntilWaitOrTerminal` ist `internal static async`, nimmt `NpgsqlConnection` + `NpgsqlTransaction`,
ruft 8 SQL-Helfer auf. Es gibt **keinen einzigen Unit-Test** für die Lifecycle-Logik. Jede Regression schlägt
nur in Integration-Tests durch — die aber lokal ohne Postgres nicht laufen (HQ4 ist gefixt, aber langsam).

Genau dasselbe Problem hatte die Rotation-Engine, gelöst durch H6 (`RotationTaskRegenerationEngine` als pure
Domain-Engine + 12 Unit-Tests).

### 2.2 Kopplungsstelle ist unsichtbar
Wer `TaskApplicationService.cs` liest, sieht: "Status updaten, Notification dispatchen, fertig."
Der Runtime-Lifecycle wird zwei Schichten tiefer aus einem `if`-Branch im Repository getriggert.
Folge:
- neue Trigger (Task-cancel triggert Compensation? Comment auf Approval-Task triggert Reminder?) sind
  schwer einzuordnen — gehören sie ins Service, ins Repo, in die Engine?
- Tests gegen `TaskApplicationService` decken das Lifecycle-Verhalten gar nicht ab.
- Der Service-Layer wirkt anämisch, das Repo trägt Domänen-Logik.

### 2.3 Kein expliziter Lifecycle-Vertrag
Der `WorkflowDefinitionRuntimeService` exponiert nur drei Completion-Endpunkte (`Form`, `Approval`, `Task`).
`measure_*`-Nodes haben keinen Service-Endpunkt — sie kommen ausschließlich über das Task-System weiter.
Wenn man jemals "manuell measure-Node abbrechen" oder "manuell Knoten überspringen" braucht, gibt es keine
saubere Stelle dafür.

### 2.4 Verdoppelte Status-Wahrheit
- `workflow_node_instances.status` (`active`/`done`/`failed`/`cancelled`) → Runtime-Sicht
- `workflow_tasks.status` + `task_assignments` → Task-Sicht
- `workflows.status` → Workflow-Aggregat (`RecalculateAndPersistWorkflowStatus`)

Heute werden alle drei in derselben Transaction gepflegt. Solange die Brücke in einer Transaction läuft,
ist das ok. Sobald aber eine fehlt (z. B. neuer Pfad ergänzt jemand `UpdateTaskStatus` ohne Runtime-Trigger),
driften die drei auseinander — und es gibt keinen statischen Schutz dagegen.

### 2.5 `automation`-Node-Pfad wirkt brüchig
`AutomationOperations.cs:202` ruft `AdvanceRuntimeUntilWaitOrTerminal` direkt — gleiche Brücke wie das
Task-System, aber separat verdrahtet. Bei Schritt 9 (echte Handler) wird das mehr.

---

## 3. Optionen

### Option A — "Brücke ins Service ziehen" (kleinster Schnitt)

Nur die drei `*FromTaskStatusUpdate`/`*FromWorkflowTask`-Aufrufe **aus dem Repo herausziehen** und
explizit in `TaskApplicationService` (oder einer neuen `IWorkflowLifecycleService`) machen.

- TaskApplicationService nach `repository.UpdateTaskStatus` zusätzlich `lifecycleService.OnTaskTerminalAsync(...)` aufrufen.
- Das Repo schreibt nur noch Task-Status, nicht mehr Runtime-Events.
- Der Lifecycle-Service ruft den Runtime-Service auf, der den Repo-Loop triggert.

**Pro:**
- Sichtbarkeit: Aufrufkette in C# lesbar, nicht in SQL versteckt.
- Wenig Risiko: Verhalten unverändert, nur Aufrufer verschoben.
- Erlaubt Service-Tests gegen Mock-Lifecycle-Service.

**Kontra:**
- Verlust der Single-Transaction-Garantie. Heute läuft Task-Update + Runtime-Advance in einer
  DB-Transaction. Wenn die Brücke ins Service wandert, sind das zwei Transaktionen — Konsistenzlücke,
  wenn die zweite fehlschlägt.
- Loop bleibt im Repo, immer noch nicht testbar.
- Behebt 2.1 nicht, behebt 2.5 nicht.

### Option B — "Engine als pure Domain-Service" (analog H6 / Rotation-Engine)

`AdvanceRuntimeUntilWaitOrTerminal` als Plan-erzeugende Domain-Engine extrahieren:

```csharp
internal static class WorkflowRuntimeEngine
{
    public static RuntimePlan Plan(
        WorkflowGraphSnapshot graph,
        WorkflowRuntimeStateSnapshot state,
        WorkflowAnswerSnapshot answers,
        WorkflowNodeRecord completedNode);
}
```

`RuntimePlan` enthält reine Records: `NodesToActivate`, `NodesToAutoComplete`, `DecisionsToRecord`,
`MeasureNodesToActivateWithTaskGeneration`, `WorkflowStatusTransition`, `Failure`.

Der Repo-Layer:
1. Lädt Graph + State in In-Memory-Records (read-only DB-Roundtrip).
2. Ruft `WorkflowRuntimeEngine.Plan(...)` (pure, kein DB).
3. Führt den Plan SQL-seitig aus (`workflow_node_instances`-Inserts, Events, Audit, ggf. Tasks).

**Pro:**
- Loop wird unit-testbar mit klaren Records (200+ Tests realistisch).
- Konsistenzlücke aus Option A entfällt: Plan-Ausführung bleibt in einer Transaction.
- Adressiert 2.1, 2.3, 2.5 strukturell.

**Kontra:**
- Schnitt durch ~340 Zeilen sehr SQL-nahen Code, viele Helper (`CanActivateRuntimeNode`,
  `EnsureRuntimeSetupTasksGenerated`, `ShouldAutoCompleteSupervisorApprovalBridge`) müssen mit-extrahiert
  oder neu organisiert werden.
- `EnsureRuntimeSetupTasksGenerated` hat heute SQL-Side-Effects (Tasks erzeugen) im Loop — entweder als
  eigener Plan-Step oder als nachgelagerter Aufruf.
- Größerer Schnitt → mehr Migrations-Risiko bei laufenden Workflows.

### Option C — "Hybrid: Brücke ins Service + Engine später"

Phase 1: Option A (Brücke ins Service ziehen, Single-Transaction durch `TransactionScope` oder
explizite Connection-Übergabe halten).
Phase 2: Option B (Engine extrahieren), wenn Phase 1 stabil läuft.

**Pro:**
- Risiko verteilt; jede Phase einzeln rückrollbar.
- Phase 1 erlaubt schon Service-Level-Tests.
- Phase 2 wird einfacher, weil Aufrufer schon im Service sitzt.

**Kontra:**
- Zwei Migrations-Phasen, doppelter Doku-Aufwand.
- Single-Transaction durch alle Schichten zu fädeln ist nicht trivial — entweder Connection-Param
  durchreichen (hässlich) oder Unit-of-Work-Pattern einführen (großer Schnitt).

### Option D — "Status quo akzeptieren, nur Doku"

Brücke im Repo lassen, dafür explizit dokumentieren ("Task-Status-Updates triggern Runtime in derselben
DB-Transaction; siehe TaskOperations.cs:131,145,215") und mit Tests gegen das Repo absichern.

**Pro:**
- Kein Code-Risiko.

**Kontra:**
- Behebt nichts. Loop bleibt 340 Zeilen SQL-im-C#, ungetestet.
- Migrationspfad-Schritt 7 bleibt offen für die nächste Sub-Generation.

---

## 4. Empfehlung — Option B in zwei Slices

**Empfehlung: Option B**, aber in **zwei Slices**, nicht alles auf einmal:

**Slice 1 — Engine-Extraktion (pure)**
- `WorkflowRuntimeEngine.Plan(...)` baut den Plan aus Records.
- Repo lädt In-Memory-Snapshot, ruft Engine, führt Plan aus.
- Verhalten bleibt 1:1; nur Strukturwechsel.
- Unit-Tests gegen Engine-Plans (Decision-Branching, Parallel-Join-Konvergenz, Auto-Complete-Bridge,
  Measure-Node-Aktivierung, Failure-Pfade).

**Slice 2 — Service-Brücke**
- Neuer `IWorkflowLifecycleService` (oder direkt im `WorkflowDefinitionRuntimeService`) wird zur
  einzigen Stelle, die Engine-Aufrufe orchestriert.
- `TaskApplicationService` ruft `lifecycleService.OnTaskTerminalAsync(...)` explizit nach
  `repository.UpdateTaskStatus`.
- Single-Transaction-Garantie über Unit-of-Work-Pattern (siehe §6).
- Repo-Brücken (`*FromTaskStatusUpdate`, `*FromWorkflowTask`) entfernt.

**Warum Option B + zwei Slices, nicht Option C:**
- Option C macht Slice 2 vor Slice 1 — d. h. Lifecycle-Service ruft Repo, Repo ruft 340-Zeilen-SQL-Loop.
  Das macht den Loop genauso schwer testbar wie heute, nur mit einer zusätzlichen Schicht.
- Option B umgekehrt: erst die Engine sauber, dann die Service-Brücke. Slice 1 liefert sofort
  Test-Coverage; Slice 2 bekommt eine bereits abgesicherte Engine.

**Warum nicht Option A allein:**
- Verlust der Single-Transaction-Garantie ohne Sicherheitsnetz ist eine Konsistenz-Regression.
- Der Hauptpain (untestbarer 340-Zeilen-Loop) bleibt komplett.

---

## 5. Slice-Plan

### Slice 1 — `WorkflowRuntimeEngine` als Pure-Domain (HIGH, ~3–5 d, opus)

**Voraussetzungen:**
- Vollständige Inventur der `internal static`-Helpers in `PostgresWorkflowRuntimeRepository.cs`,
  die `AdvanceRuntimeUntilWaitOrTerminal` aufruft (geschätzt 8–10).
- Welche davon sind pure (nur Records) vs. SQL-Side-Effects?

**Schritte:**
1. Records definieren: `WorkflowGraphSnapshot`, `WorkflowRuntimeStateSnapshot`, `WorkflowAnswerSnapshot`,
   `WorkflowNodeRecord`, `RuntimePlan`-Sub-Records.
2. Engine-Methode `Plan(...)` extrahieren — nimmt Records, gibt Plan zurück.
3. Pure Helpers mit-extrahieren (`ResolveNextNodes`, `CanActivateRuntimeNode`,
   `ShouldAutoCompleteSupervisorApprovalBridge`, `ParseDecisionCondition`, `ResolveDecisionTarget`).
4. Repo-Adapter: `LoadRuntimeSnapshot(workflowId)` → Snapshot, `ApplyRuntimePlan(plan)` → SQL.
5. `EnsureRuntimeSetupTasksGenerated` bleibt SQL-Side-Effect, wird aus dem Plan getriggert (separater
   Plan-Step `MeasureNodeActivation { GenerateTasks: true }`).
6. Bestandstests laufen unverändert (Verhaltens-Parität).
7. Neue Unit-Test-Suite `WorkflowRuntimeEngineTests.cs` mit ~20–30 Tests auf Plan-Granularität.

**Risiken:**
- `EnsureRuntimeSetupTasksGenerated` ist heute eng mit dem Loop verschränkt; falls die Task-Generierung
  selbst Runtime-Events triggern muss, wird der Schnitt komplexer.
- `ShouldAutoCompleteSupervisorApprovalBridge` enthält DB-Lookups — entweder vorab in Snapshot laden
  oder als Plan-Vorbereitung extrahieren.

### Slice 2 — Service-Brücke + Unit-of-Work (HIGH, ~2–3 d, opus/sonnet)

**Voraussetzungen:**
- Slice 1 stabil, alle Bestandstests grün.
- Entscheidung: Unit-of-Work-Pattern (`IWorkflowLifecycleScope`?) oder explizite Connection-Übergabe.

**Schritte:**
1. `IWorkflowLifecycleService` definieren mit Methoden `OnTaskTerminalAsync`, `OnTaskApprovalDecidedAsync`,
   `OnAutomationCompletedAsync`.
2. Diese Service-Methoden orchestrieren: Snapshot laden → Engine.Plan → ApplyPlan, alles in einer
   Transaction.
3. `TaskApplicationService` nach `repository.UpdateTaskStatus`/`DecideTaskApproval` den Lifecycle-Service
   rufen.
4. `WorkflowAutomationService` nach Job-Completion den Lifecycle-Service rufen.
5. Repo-Brücken entfernen: `CompleteRuntimeTaskNodeFromTaskStatusUpdate`,
   `TryAdvanceRuntimeSetupFromTaskStatusUpdate`, `ApplyRuntimeApprovalDecisionFromWorkflowTask`.
6. Service-Level-Integration-Tests: TaskApplicationService → Lifecycle → Engine → Repo.

**Risiken:**
- Single-Transaction-Garantie: ohne sauberes Unit-of-Work driften die drei Status-Quellen (siehe 2.4).
  Die Connection muss zwischen `repository.UpdateTaskStatus` und `lifecycleService.On...Async` geteilt
  werden. Mögliche Lösungen:
  - **Connection-Scope**: TaskApplicationService öffnet Connection+Transaction, übergibt sie an Repo
    und Service. Saubere Lösung, aber Eingriff in fast alle Repo-Signaturen.
  - **AmbientTransaction (`TransactionScope`)**: System.Transactions in ASP.NET Core mit Npgsql
    möglich, aber heute nirgends genutzt → Risiko, dass Connection-Pool-Verhalten überrascht.
  - **Outbox-Pattern**: `repository.UpdateTaskStatus` schreibt nur Status + Outbox-Event, ein
    Hosted-Service liest Outbox und triggert Lifecycle. Sauberer Schnitt, aber asynchron — bricht die
    aktuelle Erwartung "Task-Update sieht direkt den nächsten Workflow-Schritt".

  **Vorschlag:** Connection-Scope. Macht Unit-of-Work explizit, bricht keine Erwartungen, kostet aber
  einen Refactor-Pass.

### Slice 0 (vor Slice 1) — Inventur (LOW, ~0.5 d, sonnet)

Bevor extrahiert wird: alle Aufrufer von `AdvanceRuntimeUntilWaitOrTerminal` und seinen Helpers listen,
SQL-Side-Effects markieren, Plan-Schnitt skizzieren. Output: erweiterte Tabelle in dieser Skizze.

---

## 6. Entschiedene Fragen (2026-05-03)

| ID | Frage | Entscheidung | Begründung |
|----|-------|---------------|-------------|
| Q1 | Unit-of-Work | **Connection-Scope, vom Lifecycle-Service besessen.** `IWorkflowLifecycleService` öffnet Connection + Transaction; Repo bekommt scoped Varianten (`UpdateTaskStatusInScope(NpgsqlConnection, NpgsqlTransaction, ...)`); existing Repo-Methoden bleiben während der Migration für Nicht-Lifecycle-Pfade. | Outbox bricht "Task-Update sieht direkt den nächsten Schritt"-Erwartung. `TransactionScope` ist im Codebase nirgends genutzt → unbekannte Kanten mit Npgsql-Pool. Connection-Scope ist explizit, async-sauber, testbar. |
| Q2 | Service-Schnitt | **Separater `IWorkflowLifecycleService`.** `IWorkflowDefinitionRuntimeService.CompleteFormNode/Approval/Task` delegiert an `lifecycleService.HandleNodeCompletionAsync(...)`. Aufrufer aus dem Task-System und dem Automation-Layer rufen den Lifecycle-Service direkt. | Lifecycle = Orchestrator; DefinitionRuntimeService = öffentliche API. Symmetrie über alle Eintrittspunkte. Der Lifecycle-Service wird die einzige Stelle, die Connection+Transaction owned und Engine.Plan + Plan-Apply hintereinander hält. |
| Q3 | Repo-Brücken in Slice 1 | **In Slice 1 behalten** (nur intern auf Engine umstellen). **In Slice 2 entfernen** (Aufrufer auf Lifecycle-Service geshiftet). | Slice 1 ist nur Strukturwechsel ohne Verhaltens-Änderung. Brücken-Removal zwingt Caller-Migration → das ist Slice 2's Job. Sauber getrennt: Slice 1 = Engine, Slice 2 = Service-Routing. |
| Q4 | Plan-Failures / Saga | **Keine Saga.** DB-Transaction ist die Grenze. Engine liefert entweder `RuntimePlan` (Erfolgsfall) oder `RuntimeFailure { Reason }` als Plan-Variant; der Repo wendet beides idempotent an (Failure-Apply schreibt `runtime_status='failed'` + Audit). Throws aus dem Apply-Pfad → Rollback → User retried die Aktion. Automation-Trigger landen als `INSERT INTO automation_jobs` im Plan, der Hosted-Service führt extern aus (eigene Retry-Loop, schon vorhanden). | Transaction war schon immer die Konsistenz-Grenze; das wollen wir behalten. Saga-Pattern lohnt sich nur, wenn der Plan mehrere unabhängige externe Side-Effects ohne Rollback-Möglichkeit hätte — hier ist alles entweder DB (rollback-bar) oder asynchron (Automation, eigene Retry). |
| Q5 | Specs im Snapshot | **Lazy via Plan-Step.** Engine emittiert `MeasureNodeActivation { NodeInstanceId, NodeId }`; der Repo lädt Specs + erzeugt Tasks im Apply-Pfad. Engine kennt Graph + Edge-Conditions + Decision-Routing, **nicht** Task-Generierungsregeln. | Spielt H6's Pattern (`RotationTaskRegenerationEngine`) durch: Engine plant strukturell, Repo führt aus. Hält die Engine pur von LA5/FE-9-Spec-Details — spätere Spec-Erweiterungen (neue Spec-Typen, Conditions) brauchen keine Engine-Änderung. |

| Q6 | Rekursiver Loop-Pfad | **(a) Apply-seitige Iteration.** Engine.Plan bleibt single-shot. Apply-Pfad führt den Plan aus; wenn `MeasureNodeActivationStep` mit 0 generierten Pflicht-Tasks endet, signalisiert das ApplyResult einen Re-Plan-Trigger. Lifecycle-Service lädt Snapshot neu und ruft `Engine.Plan` erneut auf — alles in derselben Transaction. Loop terminiert nach max. `count(measure_nodes_in_graph)` Iterationen (jeder measure-Node kann nur einmal pro Workflow-Instanz feuern). | (b) ist nur (a) mit Extra-Ceremony — Engine kann Stage 2 nicht ohne Stage-1-Ergebnis planen, müsste sowieso zurückfragen, also kein Gewinn. (c) bricht Q5 explizit: Spec-Counts pre-rechnen heißt Condition-Evaluation in zwei Stellen, wächst mit jeder Spec-Erweiterung — Anti-Pattern. (a) hält Engine pur, Apply-Pfad bekommt einen kleinen, topologisch-beschränkten Loop (in der Praxis 1–2 Iterationen). |

**Konkrete Apply-Form (Slice 1 / 2):**

```csharp
// IWorkflowLifecycleService
async Task HandleNodeCompletionAsync(workflowId, completedNodeId, actorUserId, ...) {
    using var conn = ...;
    using var tx = conn.BeginTx();
    var snapshot = await LoadRuntimeSnapshot(conn, tx, workflowId);
    var completedNode = snapshot.Graph.NodesById[completedNodeId];

    while (true) {
        var plan = WorkflowRuntimeEngine.Plan(snapshot, completedNode);
        var applyResult = await ApplyRuntimePlan(conn, tx, plan, actorUserId);

        if (applyResult.ImmediatelyCompletedMeasureNode is not { } reTriggerNodeId) break;

        snapshot = await LoadRuntimeSnapshot(conn, tx, workflowId);
        completedNode = snapshot.Graph.NodesById[reTriggerNodeId];
    }

    tx.Commit();
}
```

`ApplyResult` ist ein record mit `ImmediatelyCompletedMeasureNode: long?` — gesetzt vom Apply-Pfad nach `EnsureRuntimeSetupTasksGenerated`, wenn 0 mandatory tasks zurückkamen.

---

## 7. Bewusst nicht im Scope von Schritt 7

- **Schritt 9** (Automation echte Handler) — separate Sub-Migration. Slice 2 macht den Trigger sauber,
  aber die Handler selbst bleiben simulated.
- **`subworkflow`-Node-Typ** — nicht im aktuellen Soll-Set, kein Trigger.
- **`wait`-Node-Typ** — Zielarchitektur erwähnt es als "später erweiterbar"; nicht jetzt.
- **Saga/Compensation für gescheiterte Tasks** — Q4 anschneidet, ist aber eigene Diskussion.
- **Frontend-Builder-Sicht auf Runtime-Events** — separate UX-Diskussion.

---

## 8. Verwandte Notizen

- [[Migrationspfad]] — Schritt 7 als offener Punkt
- [[Zielarchitektur]] — Runtime-/Task-/Automation-Schichten
- [[Entscheidungen]] — "Runtime ist mehr als Task-Generierung"
- [[LA5-TaskSpezifikation-Skizze]] — Vorgänger: Specs am Node, nicht am Template
- [[FE9-Spec-Carry-Over-Skizze]] — Vorgänger: Specs reisen mit Version-DTO

## 9. Status

- 2026-05-03 — Skizze erstellt, Empfehlung Option B (zwei Slices), keine Code-Änderungen.
- 2026-05-03 — Option B vom Nutzer angenommen. Q1–Q5 entschieden (siehe §6).
- 2026-05-03 — Slice 0 abgeschlossen: Inventur in §10 angehängt.
- 2026-05-03 — Q6 entschieden: Option (a) Apply-seitige Iteration mit `ApplyResult.ImmediatelyCompletedMeasureNode`-Signal.
- 2026-05-03 — Slice 1 (1.1–1.8) abgeschlossen: Engine extrahiert, Loop von 340 Z. auf 30 Z. queue-basierte Drainage reduziert, 25 Engine-Unit-Tests gruen, 384 Bestandstests gruen.
- 2026-05-04 — Slice 2.0 abgeschlossen: Inventur-Refresh (§11). 6 Loop-Eintrittspunkte, 6 Brücken-Aufrufer, 4 Connection-Owners identifiziert. Lifecycle-Service-API skizziert.
- Nächste Aktion: **Slice 2.1** — `IWorkflowLifecycleService` Skelett + DI (kein Verhaltenswechsel). Gated nur noch auf User-Approval der Sub-Slicing-Tabelle in TODO.md.

---

## 10. Slice 0 — Inventur (2026-05-03)

### 10.1 Loop-Aufrufe in `AdvanceRuntimeUntilWaitOrTerminal`

Methode in `PostgresWorkflowRuntimeRepository.cs`, Z. 1003–1338.

| # | Methode | Klassifikation | Zweck |
|---|---------|----------------|-------|
| 1 | `SetWorkflowRuntimeState(conn, tx, workflowId, status, legacyStatus, completedAt)` (Z. 1012) | db-write | Setzt `current_runtime_status` + Legacy-Status auf dem Workflow-Row am Loop-Eingang |
| 2 | `ResolveNextNodes(graph, completedNode, answersByKey)` (Z. 1019, 1092, 1159) | pure | Liest Graph-Edges im Speicher, gibt Folgeknoten zurück; bei `decision` delegiert an `ResolveDecisionTarget` |
| 3 | `EnqueueIfNeeded(pendingNodes, scheduledNodeIds, node)` (Z. 1021, 1094, 1161) | pure | Fügt Knoten in die Queue ein, wenn noch nicht scheduled |
| 4 | `FailRuntimeWorkflow(conn, tx, workflowId, actorUserId, reason)` (Z. 1026, 1303) | db-write | Schreibt `runtime_status=failed`, Runtime-Event, Audit-Eintrag |
| 5 | `LoadWorkflowNodeInstanceByWorkflowNodeId(conn, tx, workflowId, nodeId)` (Z. 1037) | db-read | Prüft via `SELECT … FOR UPDATE`, ob ein Node-Instance-Row bereits existiert (Idempotenz-Guard) |
| 6 | `CanActivateRuntimeNode(conn, tx, workflowId, graph, node)` (Z. 1042) | db-read | Nur bei `parallel_join`: prüft via `LoadWorkflowNodeInstanceStates`, ob alle eingehenden Branches `done` sind |
| 7 | `ShouldAutoCompleteSupervisorApprovalBridge(conn, tx, workflowId, graph, completedNode, candidateNode)` (Z. 1047) | db-read | Prüft, ob ein `approval`-Knoten wegen bereits ausgefülltem Gatekeeper-Form übersprungen werden soll; lädt Workflow-Kontext + Approval-Spec per DB |
| 8 | `CreateWorkflowNodeInstance(conn, tx, workflowId, node, status, resultJson)` (Z. 1055, 1108, 1171, 1226) | db-write | INSERT in `workflow_node_instances`, RETURNING id |
| 9 | `InsertWorkflowRuntimeEvent(conn, tx, workflowId, nodeInstanceId, eventType, payload)` (Z. 1067, 1116, 1145, 1179, 1234) | db-write | INSERT in `workflow_runtime_events` |
| 10 | `PostgresRepositorySharedHelpers.InsertAuditEntry(...)` (Z. 1081, 1195, 1284) | db-write | INSERT in Audit-Tabelle |
| 11 | `ResolveDecisionTarget(graph, node, answersByKey, out selectedEdge)` (Z. 1126) | pure | Wählt ausgehende Edge per Condition-Evaluation (`ParseDecisionCondition` + `TaskConditionEvaluator.EvaluateCondition`); kein DB-Zugriff |
| 12 | `UpdateNodeInstanceStatus(conn, tx, nodeInstanceId, status, resultJson)` (Z. 1133) | db-write | UPDATE `workflow_node_instances` (Status + `completed_at`) |
| 13 | `EnsureRuntimeSetupTasksGenerated(conn, tx, workflowId, node, answersByKey, actorUserId)` (Z. 1187) | mixed | Prüft via DB ob Tasks bereits existieren; wenn nicht: generiert Tasks via `PostgresWorkflowTaskGenerationService.GenerateWorkflowTasksAsync`, schreibt Availability + Status |
| 14 | `TryCompleteRuntimeSetupNodeIfReady(conn, tx, workflowId, graph, nodeExecution, answersByKey, actorUserId)` (Z. 1206) | mixed | Prüft via DB ob alle Setup-Tasks `done` sind; wenn ja: markiert Node als done, ruft `AdvanceRuntimeUntilWaitOrTerminal` rekursiv auf |
| 15 | `CreateRuntimeWorkflowTask(conn, tx, workflowId, nodeInstanceId, node)` (Z. 1245) | mixed | Liest Task-Spec + Workflow-Kontext per DB, INSERT in `workflow_tasks` + Assignments |
| 16 | `PostgresWorkflowAutomationOperations.BuildAutomationJobPayloadAsync(...)` (Z. 1266) | db-read | Lädt Workflow-Answers + Kontext, baut JSON-Payload für Automation-Job |
| 17 | `PostgresWorkflowAutomationOperations.CreateAutomationJobAsync(...)` (Z. 1273) | db-write | INSERT in `automation_jobs` |
| 18 | `LoadActiveRuntimeNodes(conn, tx, workflowId)` (Z. 1308) | db-read | Lädt alle Node-Instances mit `status=active` am Loop-Ende |
| 19 | `PostgresWorkflowStatusCalculationService.RecalculateAndPersistWorkflowStatusAsync(...)` (Z. 1314) | mixed | Neuberechnung Legacy-Status bei `measure_*`-Knoten; liest Tasks + schreibt `workflows.status` |
| 20 | `PostgresWorkflowRepository.LoadWorkflowStatusForUpdate(conn, tx, workflowId)` (Z. 1315) | db-read | SELECT `workflows.status FOR UPDATE` |
| 21 | `LoadRuntimeWorkflowStatusContext(conn, tx, workflowId)` (Z. 1319) | db-read | Lädt `definition_key` + `requires_supervisor_step` vom Workflow |
| 22 | `MapLegacyStatusForActiveNodes(graph, activeNodes, primaryKey, requiresSupervisor)` (Z. 1320) | pure | Leitet Legacy-Status aus Node-Typen ab (Memory-only) |
| 23 | `CompleteRuntimeWorkflow(conn, tx, workflowId, actorUserId, status, eventType)` (Z. 1337) | db-write | Setzt `runtime_status=completed`, Runtime-Event, Audit-Eintrag |

**Zusammenfassung:** Von 23 Aufrufen im Loop-Body sind 5 pure, 7 db-read und 11 db-write/mixed. Die Hälfte der Aufrufe läuft mitten im Iteration-Step, nicht nur am Rand.

---

### 10.2 Eintrittspunkte in den Loop

Direkte Aufrufer von `AdvanceRuntimeUntilWaitOrTerminal` und der drei Brücken:

| Aufrufer (Datei:Zeile) | Trigger | Transaction-Owner | Anmerkung |
|------------------------|---------|--------------------|------------|
| `PostgresWorkflowRuntimeRepository.cs:363` | `CreateWorkflowDefinitionInstance` — nach Start-Node-Erstellung | `CreateWorkflowDefinitionInstance` öffnet `NpgsqlConnection` + `BeginTransaction` | Initialer Loop; `start`-Node wird vorher manuell als `done` markiert |
| `PostgresWorkflowRuntimeRepository.cs:690` | `CompleteRuntimeFormNodeInternal` — nach Form-Completion + Answer-Persist | Aufrufer (`CompleteRuntimeFormNode` / `CompleteRuntimeSupervisorGatekeeperStep`) owns Transaction | Form-Node → Answers werden neu geladen (`LoadStoredAnswersByKey`) vor Loop-Aufruf |
| `PostgresWorkflowRuntimeRepository.cs:1966` | `TryCompleteRuntimeSetupNodeIfReady` — wenn alle Setup-Tasks `done` | Erbt Connection+Transaction vom übergeordneten Loop-Call | Rekursiver Aufruf: Setup-Node done → Loop läuft weiter vom measure-Node |
| `PostgresWorkflowRuntimeRepository.cs:2428` | `CompleteRuntimeTaskNodeFromTaskStatusUpdate` — nach Task-Node done | Erbt Connection+Transaction vom Aufrufer (TaskOperations oder `CompleteRuntimeTaskNode`) | Brücke; auch direkt von `CompleteRuntimeTaskNode` (Z. 574) gerufen |
| `PostgresWorkflowRuntimeRepository.cs:2518` | `ApplyRuntimeApprovalDecisionFromWorkflowTask` — nach Approval approved | Erbt Connection+Transaction vom Aufrufer (TaskOperations oder `CompleteRuntimeApprovalNode`) | Nur bei `approved=true`; rejected → `FailRuntimeWorkflow`, kein Loop |
| `PostgresWorkflowRepository.TaskOperations.cs:131` | `UpdateTaskStatus` — `isRuntimeNodeTask=true` + Terminal-Status | `UpdateTaskStatus` öffnet eigene `NpgsqlConnection` + `BeginTransaction` (Z. 50–55 ca.) | Ruft `CompleteRuntimeTaskNodeFromTaskStatusUpdate`; Transaction-Commit bei Z. 153 |
| `PostgresWorkflowRepository.TaskOperations.cs:145` | `UpdateTaskStatus` — `isRuntimeNodeTask=false` (Setup-Task) | Dieselbe Transaction wie oben | Ruft `TryAdvanceRuntimeSetupFromTaskStatusUpdate`; dieser lädt selbst Graph + Measure-Node |
| `PostgresWorkflowRepository.TaskOperations.cs:215` | `DecideTaskApproval` — Approval-Entscheidung auf Task-Ebene | `DecideTaskApproval` öffnet eigene `NpgsqlConnection` + `BeginTransaction` | Ruft `ApplyRuntimeApprovalDecisionFromWorkflowTask`; Transaction-Commit bei Z. 224 |
| `PostgresWorkflowRepository.AutomationOperations.cs:202` | Nach erfolgreicher Automation-Job-Completion | `CompleteAutomationJobSuccess` öffnet eigene Transaction (ca. Z. 140) | Lädt Graph + Node vorher selbst; direkter Aufruf ohne Brücken-Wrapper |

---

### 10.3 Plan-Schnitt-Vorschlag

`RuntimePlan` enthält folgende Records (abgeleitet aus den Loop-Branches):

- `AutoCompleteStep { NodeId, NodeKey, NodeType, ResultPayload }` — für `start`, `end`, `parallel_split`, `parallel_join`; keine DB-Reads im Apply nötig außer dem bereits vorhandenen Node-Instance-Insert.
- `DecisionStep { NodeId, NodeKey, SelectedTargetNodeId, SelectedEdgeId, EdgePriority }` — für `decision`; `ResolveDecisionTarget` ist pure, kein Apply-DB-Read.
- `SupervisorApprovalBridgeSkipStep { NodeId, NodeKey }` — für den Auto-Complete-Pfad des `approval`-Knotens nach Gatekeeper-Form; im Apply: CreateNodeInstance + Event + Audit, kein DB-Read.
- `MeasureNodeActivationStep { NodeId, NodeKey, NodeType }` — für `measure_*`; Apply muss `EnsureRuntimeSetupTasksGenerated` ausführen → **DB-Read + DB-Write** (Task-Generierung, Spec-Lookup, Availability-Neuberechnung). Engine selbst braucht dafür keine Daten.
- `WaitNodeActivationStep { NodeId, NodeKey, NodeType }` — für `form`, `task`, `approval`, `automation`; Apply: NodeInstance + Event + ggf. Task-Insert (`task`/`approval`) oder Automation-Job-Insert. Für `task`/`approval` braucht Apply einen **DB-Read** (Spec via `LoadTaskSpecForNode`). Für `automation` braucht Apply einen **DB-Read** (`BuildAutomationJobPayloadAsync`).
- `WorkflowCompletionStep { }` — kein DB-Read im Apply.
- `WorkflowFailureStep { Reason }` — kein DB-Read im Apply.
- `WorkflowWaitStep { LegacyStatus }` — `MapLegacyStatusForActiveNodes` ist pure; aber `LoadActiveRuntimeNodes` muss vorher in DB gelesen werden (wird zu einem Plan-Vorbereitungs-Read, nicht zum Engine-Input).

**Snap-shot-Daten, die die Engine als Input bekommt (kein DB-Zugriff im Engine.Plan):**

- `WorkflowDefinitionGraphRecord` — Nodes, Edges, EdgeConditions, NodeActions, NodeActionsByNodeId (schon heute in Helpers geladen)
- `WorkflowRuntimeStateRecord` — bestehende Node-Instance-States (für Idempotenz-Guard + Parallel-Join)
- `WorkflowAnswersByKey` — für Decision-Condition-Evaluation
- `WorkflowContextRecord` — `RequiresSupervisorStep`, `ApprovalTaskTemplateKey`, `PrimaryLegacyProcessTypeKey` (heute teils via separatem DB-Load in `ShouldAutoCompleteSupervisorApprovalBridge`)
- `ApprovalSpecByNodeId` — Mapping NodeId → `TemplateKey` für Supervisor-Bridge-Check (heute `LoadTaskSpecForNode`-Call im Loop)

---

### 10.4 Risiken / Schmerzpunkte

- **`ShouldAutoCompleteSupervisorApprovalBridge` (Z. 1816)**: lädt `WorkflowTaskGenerationContext` + `TaskSpecForNode` per DB innerhalb des Loop-Iterations-Steps. Für Slice 1 müssen beide in den Snapshot vorab geladen werden — das bedeutet eine neue Snapshot-Komponente (`ApprovalSpecByNodeId`) und eine Erweiterung des Workflow-Kontext-Snapshots. Wird nötig sein, bevor der Supervisor-Bridge-Pfad pure werden kann.

- **`EnsureRuntimeSetupTasksGenerated` (Z. 1855)**: sehr breiter DB-Side-Effect (Task-Generierung, `PostgresWorkflowTaskGenerationService.GenerateWorkflowTasksAsync`, Availability-Neuberechnung, Status-Neuberechnung). Kann nicht in die Engine gezogen werden. Als `MeasureNodeActivationStep` im Apply-Pfad bleibt sie im Repo-Layer — muss aber sauber vom Engine-Output getriggert werden, nicht implizit durch den Loop.

- **`TryCompleteRuntimeSetupNodeIfReady` + rekursiver Loop-Aufruf (Z. 1926 / 1966)**: Setup-Nodes können sich im selben Loop-Durchlauf bereits als `done` erweisen (wenn 0 Tasks generiert wurden). Das führt zu einem rekursiven `AdvanceRuntimeUntilWaitOrTerminal`-Aufruf. Engine müsste diesen Pfad als zusätzlichen `AutoCompleteStep` nach `MeasureNodeActivationStep` modellieren oder der Apply-Pfad muss nach Task-Generierung selbst prüfen und ggf. nochmals `Engine.Plan` aufrufen.

- **`CreateRuntimeWorkflowTask` (Z. 1640)**: lädt `LoadTaskSpecForNode` + `LoadWorkflowRuntimeTaskContext` + `LoadWorkflowDueAtAsync` per DB im Loop. Für Slice 1 entweder in den Snapshot vorab laden oder als Apply-seitiger DB-Read belassen (wäre Plan-Step `WaitNodeActivationStep` mit Apply-Read — vertretbar, da der Spec-Lookup strukturell zum Apply gehört).

- **`BuildAutomationJobPayloadAsync` (Z. 1266)**: DB-Read für `automation`-Node mitten im Switch-Branch. Payload-Mapping-Logik ist heute in `AutomationOperations` — für Slice 1 muss entschieden werden, ob der Payload-Build in den Apply-Pfad wandert oder ob das Input-Mapping als Teil des Snapshots vorab geladen wird.

- **Drei separate Transaction-Owners** (`TaskOperations.cs`, `DecideTaskApproval`, `AutomationOperations.cs`): alle öffnen eigene Connections und rufen dann den Loop-Eintrittspunkt auf. Slice 1 ändert das nicht; erst Slice 2 adressiert das via `IWorkflowLifecycleService` mit Connection-Scope (Q1/Q2).

---

## 11. Slice 2.0 — Inventur-Refresh nach Slice 1.6/1.7/1.8 (2026-05-04)

Stand der Codebasis am Anfang von Slice 2, nachdem Slice 1 vollständig abgeschlossen ist.

### 11.1 Was Slice 1.6 strukturell verändert hat

`AdvanceRuntimeUntilWaitOrTerminal` (`PostgresWorkflowRuntimeRepository.cs:1017–1053`) ist jetzt eine **~30-Zeilen queue-basierte Drainage** über `WorkflowRuntimeEngine.Plan` + `EngineAdapter.LoadRuntimeSnapshot` + `EngineAdapter.ApplyRuntimePlan`. Die alten ~340 Zeilen Switch-/SQL-Logik aus §10.1 sind entfernt.

Re-Plan-Pfad für sofort-completable Measure-Nodes läuft über `ApplyResult.ImmediatelyCompletedMeasureNodeIds`-Queue (Q6 (a), bestätigt umgesetzt).

### 11.2 Aktuelle Loop-Eintrittspunkte (6 Stellen)

| # | Aufrufer | Zweck | Tx-Owner |
|---|----------|-------|----------|
| 1 | `PostgresWorkflowRuntimeRepository.cs:363` | `CreateWorkflowDefinitionInstance` — initialer Loop ab `start`-Node | eigene Conn+Tx |
| 2 | `PostgresWorkflowRuntimeRepository.cs:690` | `CompleteRuntimeFormNodeInternal` — Form-Completion | eigene Conn+Tx (vom Public-Service-Pfad) |
| 3 | `PostgresWorkflowRuntimeRepository.cs:1434` | `TryCompleteRuntimeSetupNodeIfReady` — Setup-Node done (von Bridge `TryAdvanceRuntimeSetupFromTaskStatusUpdate` ausgelöst) | erbt von Bridge |
| 4 | `PostgresWorkflowRuntimeRepository.cs:1793` | `CompleteRuntimeTaskNodeFromTaskStatusUpdate` (Brücke) | erbt von TaskOperations.cs:78 oder Service-Pfad |
| 5 | `PostgresWorkflowRuntimeRepository.cs:1883` | `ApplyRuntimeApprovalDecisionFromWorkflowTask` (Brücke) | erbt von TaskOperations.cs:161 oder Service-Pfad |
| 6 | `PostgresWorkflowRepository.AutomationOperations.cs:202` | `CompleteAutomationJobSuccess` — Automation-Job-Completion | eigene Conn+Tx |

### 11.3 Brücken-Aufrufer (Slice-2-Ziele)

Es gibt **drei** Brücken-Methoden, die Slice 2 entfernt, und **sechs** Aufrufstellen, die migriert werden müssen:

| Aufrufer (Datei:Zeile) | Brücke | Entry-Pfad |
|------------------------|--------|------------|
| `TaskOperations.cs:131` | `CompleteRuntimeTaskNodeFromTaskStatusUpdate` | Task-System (`PUT /tasks/{id}/status` mit Runtime-Task in Terminal-Status) |
| `TaskOperations.cs:145` | `TryAdvanceRuntimeSetupFromTaskStatusUpdate` | Task-System (`PUT /tasks/{id}/status` mit Setup-Task) |
| `TaskOperations.cs:215` | `ApplyRuntimeApprovalDecisionFromWorkflowTask` | Task-System (`PUT /tasks/{id}/approval`) |
| `AutomationOperations.cs:202` | direkt `AdvanceRuntimeUntilWaitOrTerminal` | Automation-Worker nach Job-Erfolg |
| `PostgresWorkflowRuntimeRepository.cs:514` | `ApplyRuntimeApprovalDecisionFromWorkflowTask` | Service-Pfad `WorkflowDefinitionRuntimeService.CompleteApprovalNodeAsync` (`POST /runtime/.../approval`) |
| `PostgresWorkflowRuntimeRepository.cs:574` | `CompleteRuntimeTaskNodeFromTaskStatusUpdate` | Service-Pfad `WorkflowDefinitionRuntimeService.CompleteTaskNodeAsync` (`POST /runtime/.../task`) |

**Wichtig:** Die letzten beiden Aufrufer waren in der ursprünglichen Skizze §10.2 nicht als Slice-2-Pflicht markiert, sind es aber: solange die Brücken existieren, leben sie. Slice 2 schiebt diese Logik in den Lifecycle-Service.

### 11.4 Aktuelle Connection-Owners (4 Stellen)

Heute öffnet jeder dieser Pfade selbst eine `NpgsqlConnection` + `BeginTransaction` und treibt den Loop in derselben Tx:

1. `TaskOperations.cs:78` — `UpdateTaskStatus`
2. `TaskOperations.cs:161` — `DecideTaskApproval`
3. `AutomationOperations.cs:105` — `CompleteAutomationJobSuccess`
4. `PostgresWorkflowRuntimeRepository.cs` — `CreateWorkflowDefinitionInstance`, `CompleteRuntimeFormNode`, `CompleteRuntimeApprovalNode`, `CompleteRuntimeTaskNode` (jeweils eigene Conn+Tx je Public-Methode)

Q1-Entscheidung: **Alle vier wandern in den `IWorkflowLifecycleService`**. Repo bekommt `*InScope`-Varianten, die Conn+Tx als Parameter nehmen.

### 11.5 Schnitt der Lifecycle-Service-API

Aus den Aufrufketten ableitbar:

```csharp
internal interface IWorkflowLifecycleService
{
    // Task-System
    Task<TaskWithWorkflowDto?> UpdateTaskStatusAsync(long taskId, string status, long actorUserId);
    Task<TaskWithWorkflowDto?> UpdateTaskStatusByRefAsync(string taskRef, string status, long actorUserId);
    Task<TaskWithWorkflowDto?> DecideTaskApprovalAsync(long taskId, TaskApprovalDecisionRequest request, long actorUserId);
    Task<TaskWithWorkflowDto?> DecideTaskApprovalByRefAsync(string taskRef, TaskApprovalDecisionRequest request, long actorUserId);

    // Automation-Layer
    Task OnAutomationJobCompletedAsync(ClaimedAutomationJobRecord job, WorkflowAutomationHandlerResult result, CancellationToken cancellationToken);

    // Definition-Service-Pfade
    Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowInstanceAsync(CreateWorkflowDefinitionInstanceRequest request, long actorUserId);
    Task<WorkflowDefinitionRuntimeDetailDto?> CompleteFormNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeFormNodeRequest request, long actorUserId);
    Task<WorkflowDefinitionRuntimeDetailDto?> CompleteApprovalNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeApprovalNodeRequest request, long actorUserId);
    Task<WorkflowDefinitionRuntimeDetailDto?> CompleteTaskNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeTaskNodeRequest request, long actorUserId);
}
```

Jede Methode öffnet Conn+Tx, ruft `repository.*InScope(conn, tx, ...)` für die Status-/Persistenz-Vorarbeit, treibt dann via `EngineAdapter` den Plan-Apply, committet.

### 11.6 Gotchas / Risiken für Slice 2

- **Service-Pfade `CompleteRuntimeApprovalNode` Z. 514 und `CompleteRuntimeTaskNode` Z. 574** machen vor der Brücke noch zusätzliche Arbeit (Task-Status-Sync, Audit-Eintrag). Der Lifecycle-Service muss diesen Vor-Code mitnehmen — sonst Doppel-Audit oder verlorenes `task_status_changed`-Event.
- **`DecideTaskApproval` (TaskOperations.cs:215)** persistiert vor der Brücke `done` + Audit + Comment. Auch das muss im Lifecycle-Pfad entweder vor oder via `*InScope`-Variante passieren — sonst kippt die Approval-Reihenfolge.
- **`UpdateTaskStatus` (TaskOperations.cs:74)** hat zwei Brücken-Pfade (`isRuntimeNodeTask=true` mit Terminal vs. Setup-Task). Lifecycle muss beide unterscheiden — die Information `isRuntimeNodeTask` kommt aus `LoadTaskStateForUpdate`, also braucht der Lifecycle-Pfad das Result aus der `*InScope`-Variante zurück, bevor er die Engine-Verzweigung wählt.
- **TaskOperations.cs unterstützt Rotation-Tasks via `_rotationRepository`** (`UpdateTaskStatusByRef`, `DecideTaskApprovalByRef`). Rotation-Tasks haben kein `node_instance_id` und treiben keinen Workflow-Loop — Lifecycle-Service darf für Rotation-Pfade nicht in den Engine-Apply gehen. Routing-Branch (`WorkflowTaskRef.TryParse` vor `RotationTaskRef.TryParse`) liegt heute im Repo; der Lifecycle-Service umfasst nur den Workflow-Task-Pfad, der Rotation-Pfad bleibt direkt im Repo.
- **Verhaltens-Paritäts-Beweis**: 384 Bestandstests + 25 Engine-Unit-Tests müssen grün bleiben. Insbesondere die Integration-Tests, die `PUT /tasks/{id}/status` und `POST /runtime/.../task` durchgehen, sind die Hauptabsicherung gegen Regressionen in der Brücken-Migration.

### 11.7 Geplante Sub-Slices

Detaillierte Tabelle in `TODO.md` (S7-Slice2.0 bis S7-Slice2.6).
