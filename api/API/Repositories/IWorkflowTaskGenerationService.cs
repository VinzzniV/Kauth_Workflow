using Npgsql;

namespace API;

internal interface IWorkflowTaskGenerationService
{
    Task<int> GenerateWorkflowTasks(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int workflowDepartmentId,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        TaskGenerationStage stage);

    Task<WorkflowTaskGenerationContext> LoadWorkflowTaskGenerationContext(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId);

    Task<DateTime?> LoadWorkflowDueAt(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId);
}
