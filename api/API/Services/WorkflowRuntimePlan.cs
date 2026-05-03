namespace API;

// Plan-Records fuer WorkflowRuntimeEngine.Plan(...).
// Schritt 7 Slice 1.3.
//
// Engine.Plan emittiert eine geordnete Liste von Node-Schritten plus ein
// Workflow-Outcome. Der Repo-Adapter (Slice 1.5) wendet beide Teile in
// derselben DB-Transaction an. Engine selbst tut keine DB-Operation.
//
// Bewusst noch kein Konsument: die Records werden in Slice 1.4 von
// Engine.Plan(...) erzeugt. Aktuell reine Definition.

internal sealed class WorkflowRuntimePlan
{
    // Reihenfolge im Apply: Step nach Step durchlaufen, dann Outcome.
    public required IReadOnlyList<RuntimeNodeStep> NodeSteps { get; init; }

    public required RuntimeWorkflowOutcome Outcome { get; init; }
}

// ---- Per-Node-Steps ----------------------------------------------------

// Common base fuer alle Schritte, die einen konkreten Node betreffen.
// Apply-Pfad nutzt Pattern-Match auf Sub-Typ.
internal abstract class RuntimeNodeStep
{
    public required long NodeId { get; init; }

    public required string NodeKey { get; init; }

    public required string NodeType { get; init; }
}

// `start`, `end`, `parallel_split`, `parallel_join`: Node wird automatisch
// als done eingetragen, kein Wait, kein User-Input.
internal sealed class AutoCompleteStep : RuntimeNodeStep
{
    public string? ResultPayloadJson { get; init; }
}

// `decision`: Auto-Complete plus Auswahl der gewinnenden Edge fuer das
// Audit-Event und die Folgeknoten-Aufloesung.
internal sealed class DecisionStep : RuntimeNodeStep
{
    public required long SelectedTargetNodeId { get; init; }

    public required string SelectedTargetNodeKey { get; init; }

    public required long SelectedEdgeId { get; init; }

    public required int EdgePriority { get; init; }
}

// `approval`-Node, der ueber den Supervisor-Gatekeeper-Form bereits
// erfuellt wurde (LA5/LA-Bridge): Apply markiert Node sofort als done
// mit auto=true / reason=supervisor_gatekeeper_form_already_completed.
internal sealed class SupervisorApprovalBridgeSkipStep : RuntimeNodeStep
{
}

// `measure_provision` / `_deprovision` / `_change` / `_rename`: Node wird
// active, Apply triggert EnsureRuntimeSetupTasksGenerated. Ergebnis (0
// generierte Tasks → sofortiges TryCompleteRuntimeSetupNodeIfReady) fuehrt
// im Apply zur Re-Plan-Iteration via ApplyResult (Q6, Option a).
internal sealed class MeasureNodeActivationStep : RuntimeNodeStep
{
}

// `form` / `task` / `approval` / `automation`: Node geht in Wait-State,
// Apply legt NodeInstance an + erzeugt Task/Automation-Job je nach Typ.
// Spec-Lookup passiert im Apply (Q5: Engine bleibt frei von Spec-Details).
internal sealed class WaitNodeActivationStep : RuntimeNodeStep
{
}

// ---- Workflow-Outcomes -------------------------------------------------

// Closed hierarchy: jeder Engine.Plan-Lauf endet entweder mit Wait,
// Completion oder Failure. Der Apply-Pfad wendet die Outcome am Ende der
// NodeSteps an.
internal abstract class RuntimeWorkflowOutcome
{
}

// Workflow bleibt running, mindestens ein Wait-Node ist aktiv.
internal sealed class WorkflowWaitOutcome : RuntimeWorkflowOutcome
{
    // Engine-derived Status aus MapLegacyStatusForActiveNodes. Apply MAY
    // ueberschreiben, wenn RequiresStatusRecalc=true.
    public required string LegacyStatus { get; init; }

    // Wenn nach Plan-Apply ein measure_*-Node aktiv ist, muss Apply
    // RecalculateAndPersistWorkflowStatusAsync aufrufen und das Ergebnis
    // statt LegacyStatus persistieren. Diese DB-Recalc-Logik mit Tasks/
    // Conditions/Dependencies kann die Engine nicht spiegeln, ohne den
    // gesamten Status-Algorithmus pure zu portieren.
    public required bool RequiresStatusRecalc { get; init; }
}

// Workflow erreicht einen `end`-Node ohne offene Wait-States.
internal sealed class WorkflowCompletionOutcome : RuntimeWorkflowOutcome
{
}

// Engine konnte den Plan nicht aufstellen (z. B. Decision-Edge ohne
// Match, Decision-Condition ungueltig). Apply schreibt
// runtime_status=failed + Audit, kein Throw.
internal sealed class WorkflowFailureOutcome : RuntimeWorkflowOutcome
{
    public required string Reason { get; init; }
}

// ---- Apply-Result (Q6) -------------------------------------------------

// Vom Apply-Pfad zurueckgegeben. Jede ID in `ImmediatelyCompletedMeasureNodeIds`
// markiert ein measure_*-Node, dessen Setup-Tasks beim Apply sofort als done
// erkannt wurden (z. B. 0 Pflicht-Tasks generiert oder alle Pflicht-Tasks
// bereits done) — der Lifecycle-Service muss daraufhin pro ID einen Re-Plan
// mit diesem Node als `completedNode` ausfuehren (Apply-seitige Iteration,
// Q6 Option a). Bei parallelen Measure-Branches koennen mehrere zugleich
// auflaufen.
internal sealed class WorkflowRuntimeApplyResult
{
    public required IReadOnlyList<long> ImmediatelyCompletedMeasureNodeIds { get; init; }
}
