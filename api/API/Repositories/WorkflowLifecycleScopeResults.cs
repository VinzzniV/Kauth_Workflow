namespace API;

// Ergebnis-Records fuer die *InScope-Methoden in IWorkflowLifecycleScopedRepository.
// Der Lifecycle-Service liest daraus, welche Runtime-Seite er nach dem Task-Level-Work
// noch in derselben Transaktion anstoessen muss.

internal sealed record TaskStatusUpdateScopeResult(
    bool ShouldCompleteRuntimeTaskNode,
    long? RuntimeNodeInstanceId,
    long WorkflowId,
    Guid WorkflowUid,
    bool ShouldTryAdvanceRuntimeSetup
);

internal sealed record DecideTaskApprovalScopeResult(
    long NodeInstanceId,
    long WorkflowId,
    Guid WorkflowUid
);
