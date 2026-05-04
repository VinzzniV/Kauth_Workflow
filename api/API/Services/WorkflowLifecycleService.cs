using Npgsql;

namespace API;

internal sealed class WorkflowLifecycleService(
    IWorkflowRepository workflowRepository,
    IWorkflowDefinitionRuntimeRepository definitionRuntimeRepository,
    IWorkflowLifecycleScopedRepository scopedRepository) : IWorkflowLifecycleService
{
    public async Task<TaskWithWorkflowDto?> UpdateTaskStatusAsync(long taskId, string status, long actorUserId)
    {
        var normalizedStatus = TaskStatusRules.NormalizeTaskStatus(status);
        await using var connection = new NpgsqlConnection(LifecycleRuntimeSettingsResolver.GetRequiredConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var result = await scopedRepository.UpdateTaskStatusInScope(connection, transaction, taskId, normalizedStatus, actorUserId);
        if (result is null)
            return null;
        if (result.ShouldCompleteRuntimeTaskNode && result.RuntimeNodeInstanceId.HasValue)
            await PostgresWorkflowRuntimeRepository.CompleteTaskNodeRuntimeSide(connection, transaction, result.WorkflowId, result.WorkflowUid, result.RuntimeNodeInstanceId.Value, actorUserId);
        else if (result.ShouldTryAdvanceRuntimeSetup)
            await PostgresWorkflowRuntimeRepository.TryAdvanceSetupNodeIfReady(connection, transaction, result.WorkflowId, result.WorkflowUid, actorUserId);
        await transaction.CommitAsync();
        return await workflowRepository.GetTaskById(taskId);
    }

    public Task<TaskWithWorkflowDto?> UpdateTaskStatusByRefAsync(string taskRef, string status, long actorUserId)
    {
        if (WorkflowTaskRef.TryParse(taskRef, out var workflowTaskId))
            return UpdateTaskStatusAsync(workflowTaskId, status, actorUserId);

        // Rotation-Tasks bleiben ausserhalb der Lifecycle-Engine; Repo routet auf Rotation-Repository.
        return workflowRepository.UpdateTaskStatusByRef(taskRef, status, actorUserId);
    }

    public async Task<TaskWithWorkflowDto?> DecideTaskApprovalAsync(long taskId, TaskApprovalDecisionRequest request, long actorUserId)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var connection = new NpgsqlConnection(LifecycleRuntimeSettingsResolver.GetRequiredConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var result = await scopedRepository.DecideTaskApprovalInScope(connection, transaction, taskId, request, actorUserId);
        if (result is null)
            return null;
        await PostgresWorkflowRuntimeRepository.ApplyApprovalNodeDecision(connection, transaction, result.WorkflowId, result.WorkflowUid, result.NodeInstanceId, request.Approved, actorUserId);
        await transaction.CommitAsync();
        return await workflowRepository.GetTaskById(taskId);
    }

    public Task<TaskWithWorkflowDto?> DecideTaskApprovalByRefAsync(string taskRef, TaskApprovalDecisionRequest request, long actorUserId)
    {
        if (WorkflowTaskRef.TryParse(taskRef, out var workflowTaskId))
            return DecideTaskApprovalAsync(workflowTaskId, request, actorUserId);

        // Rotation-Tasks bleiben ausserhalb der Lifecycle-Engine.
        return workflowRepository.DecideTaskApprovalByRef(taskRef, request, actorUserId);
    }

    public async Task OnAutomationJobCompletedAsync(ClaimedAutomationJobRecord job, WorkflowAutomationHandlerResult result, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(LifecycleRuntimeSettingsResolver.GetRequiredConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await scopedRepository.CompleteAutomationJobSuccessInScope(connection, transaction, job, result, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowInstanceAsync(CreateWorkflowDefinitionInstanceRequest request, long actorUserId)
        => definitionRuntimeRepository.CreateWorkflowDefinitionInstance(request, actorUserId);

    public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteFormNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeFormNodeRequest request, long actorUserId)
        => definitionRuntimeRepository.CompleteRuntimeFormNode(workflowUid, nodeInstanceId, request, actorUserId);

    public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteApprovalNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeApprovalNodeRequest request, long actorUserId)
        => definitionRuntimeRepository.CompleteRuntimeApprovalNode(workflowUid, nodeInstanceId, request, actorUserId);

    public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteTaskNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeTaskNodeRequest request, long actorUserId)
        => definitionRuntimeRepository.CompleteRuntimeTaskNode(workflowUid, nodeInstanceId, request, actorUserId);
}
