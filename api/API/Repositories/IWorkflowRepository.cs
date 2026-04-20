namespace API;

internal sealed class WorkflowListQuery
{
    public bool ReaderOnly { get; init; }
    public string? Status { get; init; }
    public string? ProcessTypeKey { get; init; }
    public string? Search { get; init; }
    public int? DepartmentId { get; init; }
    public string? Responsibility { get; init; }
    public int? Limit { get; init; }
    public int Offset { get; init; }
    public bool IncludeFilterOptions { get; init; }
    public IReadOnlyCollection<int>? ObservableDepartmentIds { get; init; }
}

internal sealed class WorkflowListResult
{
    public required List<WorkflowListItemDto> Items { get; init; }
    public int TotalCount { get; init; }
    public List<DepartmentDto> DepartmentOptions { get; init; } = new();
    public List<WorkflowResponsibilityOptionDto> ResponsibilityOptions { get; init; } = new();
}

internal interface IWorkflowRepository
{
    Task<List<DepartmentDto>> GetDepartments();
    Task<List<RoleDto>> GetRoles();
    Task<List<WorkflowProcessTypeDto>> GetActiveProcessTypes(bool managerOnly = false);
    Task<List<WorkflowStartableDefinitionDto>> GetStartableWorkflowDefinitions();
    Task<List<RequirementDto>> GetRequirements(string processTypeKey);
    Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId, string processTypeKey);
    Task<bool> IsManagerCreatableProcessType(string processTypeKey);
    Task<WorkflowTargetPersonDto> CreatePerson(CreatePersonRequest request, long actorUserId);
    Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId);
    Task<WorkflowDetailDto?> CompleteSupervisorStep(Guid workflowUid, IReadOnlyList<RequirementSelectionInputDto> selections, long actorUserId);
    Task<List<WorkflowNotificationDispatchTarget>> GetWorkflowCreatedNotificationDispatchTargets(Guid workflowUid);
    Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(Guid workflowUid);
    Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowCompletionNotifications(Guid workflowUid);
    Task<List<Guid>> GetWorkflowUidsWithDisabledNotifications(string notificationType);
    Task ApplyNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results);
    Task<List<WorkflowListItemDto>> GetWorkflows();
    Task<WorkflowListResult> GetFilteredWorkflows(WorkflowListQuery query);
    Task<WorkflowDetailDto?> GetWorkflowByUid(Guid workflowUid);
    Task<List<WorkflowAuditEntryDto>> GetWorkflowAuditLog(Guid workflowUid, int limit = 200, int offset = 0);
    Task<HashSet<int>> GetRequirementSelectionDepartmentIds(long userId);
    Task<List<TaskWithWorkflowDto>> GetTasks();
    Task<TaskWithWorkflowDto?> GetTaskById(long taskId);
    Task<TaskWithWorkflowDto?> GetTaskByRef(string taskRef);
    Task<TaskWithWorkflowDto?> UpdateTaskStatus(long taskId, string status, long actorUserId);
    Task<TaskWithWorkflowDto?> UpdateTaskStatusByRef(string taskRef, string status, long actorUserId);
    Task<TaskWithWorkflowDto?> DecideTaskApproval(long taskId, TaskApprovalDecisionRequest request, long actorUserId);
    Task<TaskWithWorkflowDto?> DecideTaskApprovalByRef(string taskRef, TaskApprovalDecisionRequest request, long actorUserId);
    Task<TaskWithWorkflowDto?> UpdateTaskAssignment(long taskId, TaskAssignRequest request, long actorUserId);
    Task<TaskWithWorkflowDto?> UpdateTaskAssignmentByRef(string taskRef, TaskAssignRequest request, long actorUserId);
    Task<TaskWithWorkflowDto?> AddTaskComment(long taskId, string commentText, long actorUserId);
    Task<TaskWithWorkflowDto?> AddTaskCommentByRef(string taskRef, string commentText, long actorUserId);
    Task<bool> ArchiveWorkflow(Guid workflowUid, long actorUserId);
    Task<bool> DeleteDraftWorkflow(Guid workflowUid);
    Task<PersonWorkflowHistoryDto?> GetPersonWorkflowHistory(long personId);
    Task<List<WorkflowLinkDto>> GetWorkflowLinks(Guid workflowUid);
    Task<List<RelatedWorkflowSummaryDto>> GetRelatedWorkflows(Guid workflowUid);
    Task<WorkflowLinkDto?> CreateWorkflowLink(Guid targetWorkflowUid, CreateWorkflowLinkRequest request, long actorUserId);
    Task<bool> DeleteWorkflowLink(Guid workflowUid, long linkId, long actorUserId);
    Task<List<WorkflowTargetPersonSourceDto>> SearchWorkflowTargetPersonSources(
        string? search,
        int limit = 20,
        IReadOnlyCollection<int>? observableDepartmentIds = null);
    Task<List<WorkflowTargetPersonDto>> SearchWorkflowTargetPeople(
        string? query,
        int limit = 20,
        IReadOnlyCollection<int>? observableDepartmentIds = null);
    Task<List<WorkflowTargetPersonDto>> SearchRotationEligiblePeople(
        string? query,
        int limit = 20,
        IReadOnlyCollection<int>? observableDepartmentIds = null);
    Task ApplyPersonLifecycleProjection(Guid workflowUid, long? actorUserId = null);
    Task<List<LinkableWorkflowDto>> FindLinkableWorkflows(int employeeNumber, Guid? excludeWorkflowUid = null);
    Task<List<DerivedAnswerDto>> GetDerivedAnswers(Guid sourceWorkflowUid, string targetProcessTypeKey);
    Task<List<WorkflowDefinitionSummaryDto>> GetAdminWorkflowDefinitions();
    Task<WorkflowDefinitionSummaryDto> CreateAdminWorkflowDefinition(CreateWorkflowDefinitionRequest request);
    Task<WorkflowDefinitionSummaryDto?> UpdateAdminWorkflowDefinition(int definitionId, UpdateWorkflowDefinitionRequest request);
    Task<bool> DeleteAdminWorkflowDefinition(int definitionId);
    Task<WorkflowDefinitionVersionSummaryDto?> CreateAdminWorkflowDefinitionVersion(
        int definitionId,
        CreateWorkflowDefinitionVersionRequest request);
    Task<WorkflowDefinitionVersionDetailDto?> GetOrCreateAdminWorkflowDefinitionWorkingDraft(int definitionId);
    Task<WorkflowDefinitionVersionDetailDto?> GetAdminWorkflowDefinitionVersion(long versionId);
    Task<WorkflowDefinitionVersionDetailDto?> ReplaceAdminWorkflowDefinitionVersion(
        long versionId,
        ReplaceWorkflowDefinitionVersionRequest request);
    Task<List<AdminProcessTypeDto>> GetAdminProcessTypes();
    Task<AdminProcessTypeDto?> UpdateProcessType(int processTypeId, AdminProcessTypeUpdateRequest request);
    Task<List<AdminTaskTemplateDto>> GetAdminTaskTemplates(int processTypeId);
    Task<AdminTaskTemplateDto> CreateAdminTaskTemplate(AdminTaskTemplateUpsertRequest request);
    Task<AdminTaskTemplateDto?> UpdateAdminTaskTemplate(int templateId, AdminTaskTemplateUpsertRequest request);
    Task<bool> DeleteAdminTaskTemplate(int templateId);
    Task<List<AdminTaskTemplateConditionDto>> GetAdminTaskTemplateConditions(int templateId);
    Task<AdminTaskTemplateConditionDto> CreateAdminTaskTemplateCondition(int templateId, AdminTaskTemplateConditionCreateRequest request);
    Task<bool> DeleteAdminTaskTemplateCondition(int templateId, long conditionId);
    Task<List<AdminTaskTemplateDependencyDto>> GetAdminTaskTemplateDependencies(int templateId);
    Task<AdminTaskTemplateDependencyDto> CreateAdminTaskTemplateDependency(int templateId, AdminTaskTemplateDependencyCreateRequest request);
    Task<bool> DeleteAdminTaskTemplateDependency(int templateId, long dependencyId);
    Task<List<AdminAnswerDefinitionDto>> GetAdminAnswerDefinitions(int processTypeId);
    Task<AdminAnswerDefinitionDto> CreateAdminAnswerDefinition(AdminAnswerDefinitionUpsertRequest request);
    Task<AdminAnswerDefinitionDto?> UpdateAdminAnswerDefinition(int definitionId, AdminAnswerDefinitionUpsertRequest request);
    Task<bool> DeleteAdminAnswerDefinition(int definitionId);
    Task<List<AdminRoleAnswerDefaultDto>> GetAdminRoleAnswerDefaults(int processTypeId);
    Task<List<AdminRoleAnswerDefaultDto>> UpsertAdminRoleAnswerDefaults(AdminRoleAnswerDefaultsBulkUpsertRequest request);
    Task<AdminDependencyGraphDto> GetAdminDependencyGraph(int processTypeId);
}
