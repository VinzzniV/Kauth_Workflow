namespace API;

internal interface ITaskApplicationService
{
    Task<IReadOnlyList<TaskWithWorkflowDto>> GetTasksAsync(CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> GetTaskByIdAsync(long taskId, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> GetTaskByRefAsync(string taskRef, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> UpdateTaskStatusAsync(long taskId, TaskStatusUpdateRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> UpdateTaskStatusByRefAsync(string taskRef, TaskStatusUpdateRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> DecideTaskApprovalAsync(long taskId, TaskApprovalDecisionRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> DecideTaskApprovalByRefAsync(string taskRef, TaskApprovalDecisionRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> UpdateTaskAssignmentAsync(long taskId, TaskAssignRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> UpdateTaskAssignmentByRefAsync(string taskRef, TaskAssignRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> AddTaskCommentAsync(long taskId, TaskCommentCreateRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> AddTaskCommentByRefAsync(string taskRef, TaskCommentCreateRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
}
