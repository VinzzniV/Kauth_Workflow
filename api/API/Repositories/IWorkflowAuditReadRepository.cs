namespace API;

internal interface IWorkflowAuditReadRepository
{
    Task<List<WorkflowAuditEntryDto>> GetWorkflowAuditLog(Guid workflowUid, int limit = 200, int offset = 0);
}
