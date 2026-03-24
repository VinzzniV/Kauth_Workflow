namespace API;

internal interface IWorkflowRepository
{
    Task<List<DepartmentDto>> GetDepartments();
    Task<List<RoleDto>> GetRoles();
    Task<List<RequirementDto>> GetRequirements();
    Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId);
    Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId);
    Task<WorkflowDetailDto?> CompleteSupervisorStep(Guid workflowUid, IReadOnlyList<RequirementSelectionInputDto> selections, long actorUserId);
    Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(Guid workflowUid);
    Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowCompletionNotifications(Guid workflowUid);
    Task<List<Guid>> GetWorkflowUidsWithDisabledNotifications(string notificationType);
    Task ApplyNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results);
    Task<List<WorkflowListItemDto>> GetWorkflows();
    Task<WorkflowDetailDto?> GetWorkflowByUid(Guid workflowUid);
    Task<List<WorkflowAuditEntryDto>> GetWorkflowAuditLog(Guid workflowUid, int limit = 200, int offset = 0);
    Task<HashSet<int>> GetRequirementSelectionDepartmentIds(long userId);
    Task<List<TaskWithWorkflowDto>> GetTasks();
    Task<TaskWithWorkflowDto?> GetTaskById(long taskId);
    Task<TaskWithWorkflowDto?> UpdateTaskStatus(long taskId, string status, long actorUserId);
    Task<TaskWithWorkflowDto?> UpdateTaskAssignment(long taskId, TaskAssignRequest request, long actorUserId);
    Task<TaskWithWorkflowDto?> AddTaskComment(long taskId, string commentText, long actorUserId);
}
