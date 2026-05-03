namespace API;

// Snapshot-Records fuer WorkflowRuntimeEngine.Plan(...).
// Schritt 7 Slice 1.2.
//
// Diese Records bilden den DB-freien Eingabezustand der Engine ab. Der
// Repo-Adapter (Slice 1.5) laedt sie in einem konsolidierten Read-Block,
// damit die Engine selbst keine SQL-Calls mehr braucht.
//
// Bewusst noch kein Konsument: die Records werden in Slice 1.4 von
// Engine.Plan(...) verwendet und in Slice 1.5 vom Repo-Adapter
// zusammengebaut. Aktuell reine Definition.

internal sealed class WorkflowRuntimeSnapshot
{
    public required long WorkflowId { get; init; }

    public required WorkflowDefinitionGraphRecord Graph { get; init; }

    public required IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> AnswersByKey { get; init; }

    // Status pro `workflow_node_id` (active/done/failed/cancelled). Wird vom
    // Idempotenz-Guard und vom parallel_join-Konvergenz-Check gebraucht.
    public required IReadOnlyDictionary<long, string> NodeInstanceStatusByWorkflowNodeId { get; init; }

    // Workflow-Kontext fuer Status-Mapping + Supervisor-Gatekeeper-Logik.
    public required string? PrimaryLegacyProcessTypeKey { get; init; }

    public required bool RequiresSupervisorStep { get; init; }

    // Workflow-Level-TemplateKey fuer den Bridge-Skip-Vergleich. Kommt aus
    // PostgresWorkflowTaskGenerationService.LoadWorkflowTaskGenerationContextAsync
    // (Workflow-Definition-Spalte `approval_task_template_key`). Wenn null/empty
    // ist Bridge-Skip ausgeschaltet.
    public required string? ApprovalTaskTemplateKey { get; init; }

    // Approval-Spec-Hints pro `workflow_node_id` fuer den Supervisor-Approval-
    // Bridge-Pfad (Q5/Q6-Kompromiss): Engine bekommt nur den TemplateKey vorab,
    // nicht die volle Spec. Reicht fuer ShouldAutoCompleteSupervisorApprovalBridge.
    public required IReadOnlyDictionary<long, RuntimeApprovalNodeHint> ApprovalSpecByNodeId { get; init; }
}

// Pre-loaded Spec-Info fuer `approval`-Nodes im Graph. Slim by design — nur
// das, was der Bridge-Skip-Pfad fuer den TemplateKey-Vergleich braucht.
internal sealed class RuntimeApprovalNodeHint
{
    public required string TemplateKey { get; init; }
}
