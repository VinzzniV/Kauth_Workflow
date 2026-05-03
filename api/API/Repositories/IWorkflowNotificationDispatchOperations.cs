using Npgsql;

namespace API;

internal interface IWorkflowNotificationDispatchOperations
{
    Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowNotifications(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int departmentId,
        bool requiresSupervisorStep,
        string processTypeKey,
        string processTypeName);
}
