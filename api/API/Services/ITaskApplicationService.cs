namespace API;

internal interface ITaskApplicationService
{
    Task<IReadOnlyList<TaskWithWorkflowDto>> GetTasksAsync(CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> GetTaskByIdAsync(long taskId, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> UpdateTaskStatusAsync(long taskId, TaskStatusUpdateRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> UpdateTaskAssignmentAsync(long taskId, TaskAssignRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<TaskWithWorkflowDto?> AddTaskCommentAsync(long taskId, TaskCommentCreateRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
}
