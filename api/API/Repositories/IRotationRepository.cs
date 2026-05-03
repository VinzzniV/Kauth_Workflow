namespace API;

internal sealed class RotationPlanConflictState
{
    public bool HasActivePlanForPerson { get; init; }
    public bool HasOpenPlanForSourceWorkflow { get; init; }
}

internal interface IRotationRepository
{
    Task<bool> DepartmentExists(int departmentId);
    Task<bool> ResponsibilityExists(int responsibilityId);
    Task<WorkflowTargetPersonSourceDto?> GetCompletedOnboardingSource(Guid workflowUid);
    Task<RotationPlanConflictState> GetRotationPlanConflictState(long personId, Guid sourceWorkflowUid);
    Task<List<RotationPlanListItemDto>> GetRotationPlans(long? personId, IReadOnlyCollection<int>? observableDepartmentIds = null);
    Task<RotationPlanDetailDto?> GetRotationPlan(long planId);
    Task<RotationPlanDetailDto> CreateRotationPlan(CreateRotationPlanRequest request, long createdByUserId);
    Task<List<RotationStationDto>> GetRotationStations(long planId);
    Task<RotationStationDto?> GetRotationStation(long stationId);
    Task<RotationStationDto?> CreateRotationStation(long planId, RotationStationUpsertRequest request, long actorUserId);
    Task<RotationStationDto?> UpdateRotationStation(long stationId, RotationStationUpsertRequest request, long actorUserId);
    Task<bool> DeleteRotationStation(long stationId, long actorUserId);
    Task<List<RotationGeneratedTaskDto>> GetRotationGeneratedTasks(long planId);
    Task<RotationGeneratedTaskDto?> GetRotationGeneratedTask(long taskId);
    Task<List<RotationAuditEntryDto>> GetRotationAuditLog(long planId, int limit = 200, int offset = 0);
    Task<List<RotationNotificationDto>> GetRotationNotifications(long planId, int limit = 200, int offset = 0);
    Task<List<long>> GetRotationPlanIdsForDepartment(int departmentId);
    Task<RotationTaskRegenerationResultDto> SynchronizeRotationGeneratedTasks(long planId, long actorUserId, string reason);
    Task<int> CreateDueRotationNotifications(DateOnly asOfDate);
    Task<List<RotationNotificationDispatchTarget>> GetDispatchableRotationNotifications();
    Task ApplyRotationNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results);
    Task<List<DepartmentActionTemplateDto>> GetDepartmentActionTemplates(int? departmentId, bool? isActive = null);
    Task<DepartmentActionTemplateDto?> GetDepartmentActionTemplate(int templateId);
    Task<DepartmentActionTemplateDto> CreateDepartmentActionTemplate(DepartmentActionTemplateUpsertRequest request);
    Task<DepartmentActionTemplateDto?> UpdateDepartmentActionTemplate(int templateId, DepartmentActionTemplateUpsertRequest request);
    Task<bool> DeleteDepartmentActionTemplate(int templateId);
    Task<List<TaskWithWorkflowDto>> GetAllRotationTaskEnvelopes();
    Task<List<TaskWithWorkflowDto>> GetRotationTaskEnvelopesForUser(long userId, int[] effectiveResponsibilityIds);
    Task<TaskWithWorkflowDto?> GetRotationTaskEnvelope(long taskId);
    Task<TaskWithWorkflowDto?> GetRotationTaskEnvelopeByRef(string taskRef);
    Task<TaskWithWorkflowDto?> UpdateRotationTaskStatusByRef(string taskRef, string status, long actorUserId);
    Task<TaskWithWorkflowDto?> UpdateRotationTaskAssignmentByRef(string taskRef, TaskAssignRequest request, long actorUserId);
    Task<TaskWithWorkflowDto?> AddRotationTaskCommentByRef(string taskRef, string commentText, long actorUserId);
    Task<TaskWithWorkflowDto?> DecideRotationTaskApprovalByRef(string taskRef, TaskApprovalDecisionRequest request, long actorUserId);
}
