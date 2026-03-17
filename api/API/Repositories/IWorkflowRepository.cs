namespace API;

internal interface IWorkflowRepository
{
    Task<List<DepartmentDto>> GetDepartments();
    Task<List<RoleDto>> GetRoles();
    Task<List<RequirementDto>> GetRequirements(int? roleId = null);
    Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId);
    Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId);
    Task<WorkflowDetailDto?> CompleteSupervisorStep(Guid workflowUid, IReadOnlyList<RequirementSelectionInputDto> selections);
    Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(Guid workflowUid);
    Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowCompletionNotifications(Guid workflowUid);
    Task ApplyNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results);
    Task<List<WorkflowListItemDto>> GetWorkflows();
    Task<WorkflowDetailDto?> GetWorkflowByUid(Guid workflowUid);
    Task<HashSet<int>> GetRequirementSelectionDepartmentIds(long userId);
    Task<List<TaskWithWorkflowDto>> GetTasks();
    Task<TaskWithWorkflowDto?> GetTaskById(long taskId);
    Task<TaskWithWorkflowDto?> UpdateTaskStatus(long taskId, string status);
    Task<TaskWithWorkflowDto?> UpdateTaskAssignment(long taskId, TaskAssignRequest request);
}
