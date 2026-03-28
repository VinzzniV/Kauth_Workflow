namespace API;

// DTOs fuer Workflow-Konfiguration, Workflow-Laufzeit und Aufgabenkommunikation mit dem Frontend.
public sealed class DepartmentDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}

public sealed class RoleDto
{
    public required int Id { get; init; }
    public required int DepartmentId { get; init; }
    public required string DepartmentName { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class RequirementOptionDto
{
    public required int Id { get; init; }
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required string Label { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsDefault { get; init; }
}

public sealed class RequirementVisibilityDependencyDto
{
    public required string DependencyKey { get; init; }
    public required string Kind { get; init; }
    public string? ExpectedValue { get; init; }
    public required bool MissingResult { get; init; }
}

public sealed class RequirementValidationDto
{
    public required string Kind { get; init; }
    public required string Message { get; init; }
}

public sealed class RequirementResetTargetDto
{
    public required string RequirementKey { get; init; }
    public bool ClearBoolean { get; init; }
    public bool ClearText { get; init; }
    public bool ClearNumber { get; init; }
    public bool ClearSelectedOption { get; init; }
    public bool ClearSelectedOptions { get; init; }
}

public sealed class RequirementSingleSelectResetDto
{
    public required List<string> KeepSelectedOptionValues { get; init; }
    public required List<RequirementResetTargetDto> Targets { get; init; }
}

public sealed class RequirementBehaviorDto
{
    public required List<RequirementVisibilityDependencyDto> VisibilityDependencies { get; init; }
    public RequirementValidationDto? Validation { get; set; }
    public required List<RequirementResetTargetDto> ResetTargetsWhenNotTrue { get; init; }
    public RequirementSingleSelectResetDto? SingleSelectReset { get; set; }
}

public sealed class RequirementDto
{
    public required int Id { get; init; }
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string IconKey { get; init; }
    public required string InputType { get; init; }
    public required bool IsRequired { get; init; }
    public required int SortOrder { get; init; }
    public required List<RequirementOptionDto> Options { get; init; }
    public required RequirementBehaviorDto Behavior { get; init; }
}

public sealed class RoleRecommendationDefaultValueDto
{
    public required int RequirementId { get; init; }
    public bool? ValueBoolean { get; init; }
    public string? ValueText { get; init; }
    public decimal? ValueNumber { get; init; }
}

public sealed class RoleRecommendationSelectedOptionsDto
{
    public required int RequirementId { get; init; }
    public int? SelectedOptionId { get; init; }
    public required List<int> SelectedOptionIds { get; init; }
}

public sealed class RoleRecommendationsDto
{
    public required List<int> RecommendedRequirementIds { get; init; }
    public required List<RoleRecommendationDefaultValueDto> DefaultValues { get; init; }
    public required List<RoleRecommendationSelectedOptionsDto> DefaultSelectedOptions { get; init; }
}

public sealed class WorkflowConfigDto
{
    public required List<RequirementDto> Requirements { get; init; }
    public required RoleRecommendationsDto RoleRecommendations { get; init; }
}

// Eingaben fuer Workflow-Erstellung und den Schritt der Abteilungsleitung.
public sealed class RequirementSelectionInputDto
{
    public required int RequirementId { get; init; }
    public bool? ValueBoolean { get; init; }
    public string? ValueText { get; init; }
    public decimal? ValueNumber { get; init; }
    public int? SelectedOptionId { get; init; }
    public List<int>? SelectedOptionIds { get; init; }
}

public sealed class CreateWorkflowRequest
{
    public required string ProcessTypeKey { get; init; }
    public int? DepartmentId { get; init; }
    public int? RoleId { get; init; }
    public long? TargetPersonId { get; init; }
    public Guid? SourceWorkflowUid { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public int? EmployeeNumber { get; init; }
    public int? BadgeNumber { get; init; }
    public DateOnly? DeadlineDate { get; init; }
}

public sealed class WorkflowCreateResponse
{
    public required Guid Uid { get; init; }
    public required int NotificationTargets { get; init; }
    public required int FailedNotifications { get; init; }
    public required WorkflowCreateSummaryDto Summary { get; init; }
}

public sealed class WorkflowCreateSummaryDto
{
    public required string WorkflowStatus { get; init; }
    public required int TaskCount { get; init; }
    public required int ReadyTaskCount { get; init; }
    public required int BlockedTaskCount { get; init; }
    public required int DoneTaskCount { get; init; }
    public required int AssignmentCount { get; init; }
    public required int PendingNotifications { get; init; }
}

public sealed class WorkflowRequirementSummaryDto
{
    public required int TotalCount { get; init; }
    public required int VisibleCount { get; init; }
    public required int AnsweredVisibleCount { get; init; }
    public required int PendingVisibleCount { get; init; }
}

public sealed class WorkflowTaskCountSummaryDto
{
    public required int TotalCount { get; init; }
    public required int OpenCount { get; init; }
    public required int InProgressCount { get; init; }
    public required int DoneCount { get; init; }
    public required int CompletedCount { get; init; }
    public required int ActiveCount { get; init; }
}

public sealed class WorkflowTaskMetricsDto
{
    public required WorkflowTaskCountSummaryDto Overall { get; init; }
    public required WorkflowTaskCountSummaryDto DepartmentPhase { get; init; }
}

public sealed class WorkflowTaskAreaSummaryDto
{
    public required string Name { get; init; }
    public required bool IsCurrentArea { get; init; }
    public required WorkflowTaskCountSummaryDto Counts { get; init; }
}

public sealed class WorkflowProcessTypeDto
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required bool RequiresTargetPerson { get; init; }
}

// Zusammenfassungen und Detailmodelle fuer Listen, Aufgaben und Detailseiten.
public sealed class WorkflowListItemDto
{
    public required Guid Uid { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required int EmployeeNumber { get; init; }
    public required int BadgeNumber { get; init; }
    public required int DepartmentId { get; init; }
    public required string DepartmentName { get; init; }
    public required WorkflowProcessTypeDto ProcessType { get; init; }
    public required int RoleId { get; init; }
    public required string RoleName { get; init; }
    public required string Status { get; init; }
    public required string WorkflowStatus { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateOnly? DeadlineDate { get; init; }
    public DateTime? ArchivedAt { get; init; }
    public required int PendingNotifications { get; init; }
    public required int FailedNotifications { get; init; }
    public required WorkflowRequirementSummaryDto RequirementSummary { get; init; }
    public required WorkflowTaskMetricsDto TaskMetrics { get; init; }
    public required string TaskSummary { get; init; }
    public required List<WorkflowResponsibilityOptionDto> ResponsibilityOptions { get; init; }
}

public sealed class WorkflowListPageDto
{
    public required List<WorkflowListItemDto> Items { get; init; }
    public required int Count { get; init; }
    public required int Offset { get; init; }
    public required int? Limit { get; init; }
    public required List<DepartmentDto> DepartmentOptions { get; init; }
    public required List<WorkflowResponsibilityOptionDto> ResponsibilityOptions { get; init; }
}

public sealed class WorkflowResponsibilityOptionDto
{
    public required string Value { get; init; }
    public required string Label { get; init; }
}

public sealed class WorkflowRequirementSnapshotDto
{
    public required long WorkflowRequirementId { get; init; }
    public required int Id { get; init; }
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string IconKey { get; init; }
    public required string InputType { get; init; }
    public required bool IsRequired { get; init; }
    public required bool IsVisible { get; set; }
    public required int SortOrder { get; init; }
    public required List<WorkflowRequirementOptionSnapshotDto> Options { get; init; }
    public required RequirementBehaviorDto Behavior { get; init; }
    public required WorkflowRequirementValueDto Value { get; set; }
}

public sealed class WorkflowRequirementOptionSnapshotDto
{
    public required long Id { get; init; }
    public int? SourceOptionId { get; init; }
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required string Label { get; init; }
    public required int SortOrder { get; init; }
}

public sealed class WorkflowRequirementSelectedOptionDto
{
    public required long Id { get; init; }
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required string Label { get; init; }
    public required int SortOrder { get; init; }
}

public sealed class WorkflowRequirementValueDto
{
    public bool? ValueBoolean { get; init; }
    public string? ValueText { get; init; }
    public decimal? ValueNumber { get; init; }
    public long? SelectedOptionId { get; init; }
    public string? SelectedOptionKey { get; init; }
    public string? SelectedOptionValue { get; init; }
    public string? SelectedOptionLabel { get; init; }
    public required List<WorkflowRequirementSelectedOptionDto> SelectedOptions { get; init; }
}

public sealed class WorkflowNotificationDto
{
    public required long Id { get; init; }
    public required string TargetName { get; init; }
    public required string TargetEmail { get; init; }
    public required string NotificationType { get; init; }
    public required string Status { get; init; }
    public required int Attempts { get; init; }
    public long? RecipientUserId { get; init; }
    public string? LastError { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? SentAt { get; init; }
}

public sealed class WorkflowAuditEntryDto
{
    public required long Id { get; init; }
    public required string EventType { get; init; }
    public required DateTime CreatedAt { get; init; }
    public long? TaskId { get; init; }
    public string? TaskKey { get; init; }
    public string? TaskTitle { get; init; }
    public long? ActorUserId { get; init; }
    public string? ActorUserName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? Detail { get; init; }
}

public sealed class WorkflowTaskCommentDto
{
    public required long Id { get; init; }
    public required long TaskId { get; init; }
    public long? AuthorUserId { get; init; }
    public string? AuthorUserName { get; init; }
    public required string CommentText { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed class WorkflowTaskAssignmentDto
{
    public required long Id { get; init; }
    public required string AssignmentType { get; init; }
    public required bool IsPrimary { get; init; }
    public long? AssigneeUserId { get; set; }
    public string? AssigneeUserName { get; set; }
    public string? AssigneeUserEmail { get; set; }
    public int? AssigneeResponsibilityId { get; init; }
    public string? AssigneeResponsibilityKey { get; init; }
    public string? AssigneeResponsibilityName { get; init; }
    public string? AssigneeResponsibilityType { get; init; }
    public required DateTime AssignedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed class WorkflowTaskDependencyDto
{
    public required long WorkflowTaskId { get; init; }
    public required long DependsOnWorkflowTaskId { get; init; }
    public required string RequiredStatus { get; init; }
    public required string DependsOnTaskKey { get; init; }
    public required string DependsOnTitle { get; init; }
}

public sealed class WorkflowTaskDto
{
    public required long Id { get; init; }
    public int? TaskTemplateId { get; init; }
    public required string TaskKey { get; init; }
    public bool IsApprovalTask { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string IconKey { get; init; }
    public required string Status { get; init; }
    public required bool IsRequired { get; init; }
    public required int SortOrder { get; init; }
    public required DateTime CreatedAt { get; init; }
    public int? DueInDays { get; init; }
    public DateTime? DueAt { get; init; }
    public required string SlaStatus { get; init; }
    public DateTime? ReadyAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ProcessArea { get; set; }
    public bool IsDepartmentPhaseTask { get; set; }
    public bool CanUpdateStatus { get; set; }
    public bool CanAddComment { get; set; }
    public required List<WorkflowTaskAssignmentDto> Assignments { get; init; }
    public required List<WorkflowTaskDependencyDto> Dependencies { get; init; }
    public required List<WorkflowTaskCommentDto> Comments { get; init; }
}

public sealed class TaskWorkflowContextDto
{
    public required long WorkflowId { get; init; }
    public required Guid WorkflowUid { get; init; }
    public required string WorkflowStatus { get; init; }
    public required string WorkflowLegacyStatus { get; init; }
    public required DateTime WorkflowCreatedAt { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required int EmployeeNumber { get; init; }
    public required int BadgeNumber { get; init; }
    public required int DepartmentId { get; init; }
    public required string DepartmentName { get; init; }
    public required int RoleId { get; init; }
    public required string RoleName { get; init; }
}

public sealed class TaskWithWorkflowDto
{
    public required WorkflowTaskDto Task { get; init; }
    public required TaskWorkflowContextDto Workflow { get; init; }
}

public sealed class TaskStatusUpdateRequest
{
    public required string Status { get; init; }
}

public sealed class TaskAssignRequest
{
    public long? AssigneeUserId { get; init; }
    public int? AssigneeResponsibilityId { get; init; }
}

public sealed class TaskCommentCreateRequest
{
    public required string CommentText { get; init; }
}

public sealed class SupervisorStepUpdateRequest
{
    public required List<RequirementSelectionInputDto> RequirementSelections { get; init; }
}

public sealed class WorkflowDetailDto
{
    public required Guid Uid { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required int EmployeeNumber { get; init; }
    public required int BadgeNumber { get; init; }
    public required int DepartmentId { get; init; }
    public required string DepartmentName { get; init; }
    public required WorkflowProcessTypeDto ProcessType { get; init; }
    public required int RoleId { get; init; }
    public required string RoleName { get; init; }
    public required string Status { get; init; }
    public required string WorkflowStatus { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateOnly? DeadlineDate { get; init; }
    public DateTime? ArchivedAt { get; init; }
    public long? TargetPersonId { get; init; }
    public required List<WorkflowRequirementSnapshotDto> Requirements { get; init; }
    public required WorkflowRequirementSummaryDto RequirementSummary { get; set; }
    public required List<WorkflowTaskDto> Tasks { get; init; }
    public required WorkflowTaskMetricsDto TaskMetrics { get; set; }
    public required List<WorkflowTaskAreaSummaryDto> TaskAreas { get; set; }
    public required List<WorkflowNotificationDto> Notifications { get; init; }
}

public sealed class WorkflowNotificationDispatchTarget
{
    public required long NotificationId { get; init; }
    public required string NotificationType { get; init; }
    public required string ProcessTypeKey { get; init; }
    public required string ProcessTypeName { get; init; }
    public long? WorkflowTaskId { get; init; }
    public long? RecipientUserId { get; init; }
    public string? RecipientIdentityKey { get; init; }
    public required string TargetName { get; init; }
    public required string TargetEmail { get; init; }
    public string? TaskTitle { get; init; }
    public string? PreferredPath { get; init; }
}

public sealed class NotificationDispatchResult
{
    public required long NotificationId { get; init; }
    public required string Status { get; init; }
    public required bool Success { get; init; }
    public required bool Attempted { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class WorkflowCreationResult
{
    public required long WorkflowId { get; init; }
    public required Guid Uid { get; init; }
    public required List<WorkflowNotificationDispatchTarget> NotificationTargets { get; init; }
}

// Workflow-Verknüpfung: DTOs fuer Links, ableitbare Workflows und abgeleitete Antworten.
public sealed class WorkflowLinkDto
{
    public required long Id { get; init; }
    public required Guid SourceWorkflowUid { get; init; }
    public required Guid TargetWorkflowUid { get; init; }
    public required string LinkType { get; init; }
    public required string LinkedWorkflowFirstName { get; init; }
    public required string LinkedWorkflowLastName { get; init; }
    public required WorkflowProcessTypeDto LinkedWorkflowProcessType { get; init; }
    public required string LinkedWorkflowStatus { get; init; }
    public required DateTime LinkedWorkflowCreatedAt { get; init; }
    public string? Notes { get; init; }
    public long? CreatedByUserId { get; init; }
    public string? CreatedByUserName { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed class CreateWorkflowLinkRequest
{
    public required Guid SourceWorkflowUid { get; init; }
    public required string LinkType { get; init; }
    public string? Notes { get; init; }
}

public sealed class LinkableWorkflowDto
{
    public required Guid Uid { get; init; }
    public required WorkflowProcessTypeDto ProcessType { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required int EmployeeNumber { get; init; }
    public required string DepartmentName { get; init; }
    public required string Status { get; init; }
    public required string WorkflowStatus { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed class RelatedWorkflowSummaryDto
{
    public required Guid Uid { get; init; }
    public required WorkflowProcessTypeDto ProcessType { get; init; }
    public required string WorkflowStatus { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required int DepartmentId { get; init; }
}

public sealed class WorkflowTargetPersonDto
{
    public required long PersonId { get; init; }
    public required string DisplayName { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? RoleId { get; init; }
    public string? RoleName { get; init; }
    public int? EmployeeNumber { get; init; }
    public int? BadgeNumber { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}

public sealed class CompletedOnboardingSearchResultDto
{
    public required Guid WorkflowUid { get; init; }
    public required long PersonId { get; init; }
    public required string DisplayName { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required int EmployeeNumber { get; init; }
    public required int BadgeNumber { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? RoleId { get; init; }
    public string? RoleName { get; init; }
    public required DateTime CompletedAt { get; init; }
    public DateTime? ArchivedAt { get; init; }
}

public sealed class DerivedAnswerDto
{
    public required string TargetAnswerKey { get; init; }
    public required string SourceAnswerKey { get; init; }
    public bool? ValueBoolean { get; init; }
    public string? ValueText { get; init; }
    public decimal? ValueNumber { get; init; }
    public string? SelectedOptionValue { get; init; }
}

// Admin-Verwaltung: Prozesstypen
public sealed class AdminProcessTypeDto
{
    public required int Id { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required bool RequiresSupervisorStep { get; init; }
    public string? ApprovalTaskTemplateKey { get; init; }
    public required bool RequiresTargetPerson { get; init; }
    public string? IconKey { get; init; }
    public required bool IsActive { get; init; }
    public required int SortOrder { get; init; }
    public required int WorkflowCount { get; init; }
    public required int AnswerDefinitionCount { get; init; }
    public required int TaskTemplateCount { get; init; }
    public required bool CanActivate { get; init; }
    public string? ActivationBlockedReason { get; init; }
}

public sealed class AdminTaskTemplateDto
{
    public required int Id { get; init; }
    public required int ProcessTypeId { get; init; }
    public required string TemplateKey { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string Description { get; init; }
    public string? IconKey { get; init; }
    public int? OwningDepartmentId { get; init; }
    public int? DefaultResponsibilityId { get; init; }
    public string? ProcessAreaLabel { get; init; }
    public required bool IsDepartmentPhaseTask { get; init; }
    public required bool IsRequired { get; init; }
    public int? DueInDays { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required int ConditionCount { get; init; }
    public required int DependencyCount { get; init; }
}

public sealed class AdminAnswerDefinitionDto
{
    public required int Id { get; init; }
    public required int ProcessTypeId { get; init; }
    public required string AnswerKey { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string Description { get; init; }
    public string? IconKey { get; init; }
    public required string InputType { get; init; }
    public required bool IsRequired { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class AdminTaskTemplateConditionDto
{
    public required long Id { get; init; }
    public required int TaskTemplateId { get; init; }
    public required int ConditionGroup { get; init; }
    public required string AnswerKey { get; init; }
    public required string Operator { get; init; }
    public string? ExpectedValueText { get; init; }
    public bool? ExpectedValueBoolean { get; init; }
    public decimal? ExpectedValueNumber { get; init; }
}

public sealed class AdminTaskTemplateDependencyDto
{
    public required long Id { get; init; }
    public required int TaskTemplateId { get; init; }
    public required int DependsOnTaskTemplateId { get; init; }
    public required string DependsOnTemplateTitle { get; init; }
    public required string RequiredStatus { get; init; }
}

public sealed class AdminRoleAnswerDefaultDto
{
    public required int ProcessTypeId { get; init; }
    public required int AppRoleId { get; init; }
    public required string AnswerKey { get; init; }
    public string? DefaultValueText { get; init; }
    public bool? DefaultValueBoolean { get; init; }
}

public sealed class AdminDependencyGraphNodeDto
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
}

public sealed class AdminDependencyGraphEdgeDto
{
    public required long Id { get; init; }
    public required int SourceTemplateId { get; init; }
    public required int TargetTemplateId { get; init; }
    public required string RequiredStatus { get; init; }
}

public sealed class AdminDependencyGraphDto
{
    public required List<AdminDependencyGraphNodeDto> Nodes { get; init; }
    public required List<AdminDependencyGraphEdgeDto> Edges { get; init; }
}

public sealed class AdminProcessTypeUpdateRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? IconKey { get; init; }
    public bool? IsActive { get; init; }
    public int? SortOrder { get; init; }
}

public sealed class AdminTaskTemplateUpsertRequest
{
    public required int ProcessTypeId { get; init; }
    public string? TemplateKey { get; init; }
    public string? Title { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public string? IconKey { get; init; }
    public int? OwningDepartmentId { get; init; }
    public int? DefaultResponsibilityId { get; init; }
    public string? ProcessAreaLabel { get; init; }
    public bool IsDepartmentPhaseTask { get; init; }
    public bool IsRequired { get; init; }
    public int? DueInDays { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class AdminTaskTemplateConditionCreateRequest
{
    public required int ConditionGroup { get; init; }
    public string? AnswerKey { get; init; }
    public string? Operator { get; init; }
    public string? ExpectedValueText { get; init; }
    public bool? ExpectedValueBoolean { get; init; }
    public decimal? ExpectedValueNumber { get; init; }
}

public sealed class AdminTaskTemplateDependencyCreateRequest
{
    public required int DependsOnTaskTemplateId { get; init; }
    public string? RequiredStatus { get; init; }
}

public sealed class AdminAnswerDefinitionUpsertRequest
{
    public required int ProcessTypeId { get; init; }
    public string? AnswerKey { get; init; }
    public string? Title { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public string? IconKey { get; init; }
    public string? InputType { get; init; }
    public bool IsRequired { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class AdminRoleAnswerDefaultUpsertItemRequest
{
    public required int AppRoleId { get; init; }
    public string? AnswerKey { get; init; }
    public string? DefaultValueText { get; init; }
    public bool? DefaultValueBoolean { get; init; }
}

public sealed class AdminRoleAnswerDefaultsBulkUpsertRequest
{
    public required int ProcessTypeId { get; init; }
    public required List<AdminRoleAnswerDefaultUpsertItemRequest> Items { get; init; }
}

// Bulk-Operationen: Massenhafte Workflow-Erstellung fuer Abteilungswechsel o.Ä.
public sealed class BulkDepartmentChangeRequest
{
    public required int SourceDepartmentId { get; init; }
    public required int TargetDepartmentId { get; init; }
    public required int TargetRoleId { get; init; }
    public DateOnly? DeadlineDate { get; init; }
    public bool DryRun { get; init; }
}

public sealed class BulkOperationResultDto
{
    public required int TotalEmployees { get; init; }
    public required int CreatedWorkflows { get; init; }
    public required int SkippedEmployees { get; init; }
    public required int FailedEmployees { get; init; }
    public required bool IsDryRun { get; init; }
    public required List<BulkOperationItemDto> Items { get; init; }
}

public sealed class BulkOperationItemDto
{
    public required long PersonId { get; init; }
    public required string DisplayName { get; init; }
    public required string Status { get; init; }
    public Guid? WorkflowUid { get; init; }
    public string? ErrorMessage { get; init; }
}

// Mitarbeiter-Lifecycle: Personenbezogene Workflow-Historie
public sealed class PersonWorkflowSummaryDto
{
    public required Guid Uid { get; init; }
    public required WorkflowProcessTypeDto ProcessType { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string RoleName { get; init; }
    public required string DepartmentName { get; init; }
    public required string Status { get; init; }
    public required string WorkflowStatus { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? ArchivedAt { get; init; }
}

public sealed class PersonWorkflowHistoryDto
{
    public required long PersonId { get; init; }
    public required string DisplayName { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? EmployeeNumber { get; init; }
    public int? BadgeNumber { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public required List<PersonWorkflowSummaryDto> Workflows { get; init; }
}

internal sealed class AnswerDefinitionRecord
{
    public required int DefinitionId { get; init; }
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string IconKey { get; init; }
    public required string InputType { get; init; }
    public required bool IsRequired { get; init; }
    public required int SortOrder { get; init; }
    public required RequirementBehaviorDto Behavior { get; set; }
    public required Dictionary<int, AnswerOptionRecord> OptionsById { get; init; }
}

internal sealed class AnswerOptionRecord
{
    public required int OptionId { get; init; }
    public required string OptionKey { get; init; }
    public required string OptionValue { get; init; }
    public required string OptionLabel { get; init; }
    public required int SortOrder { get; init; }
}

internal sealed class RoleDefaultRecord
{
    public required int AnswerDefinitionId { get; init; }
    public bool? DefaultValueBoolean { get; init; }
    public string? DefaultValueText { get; init; }
    public decimal? DefaultValueNumber { get; init; }
    public int? DefaultSelectedOptionId { get; init; }
    public required List<int> DefaultSelectedOptionIds { get; init; }
}

internal sealed class RequirementSelectionStateRecord
{
    public bool? ValueBoolean { get; set; }
    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
    public int? SelectedOptionId { get; set; }
    public required List<int> SelectedOptionIds { get; init; }
}

internal sealed class StoredWorkflowAnswerRecord
{
    public required long WorkflowAnswerId { get; init; }
    public required int AnswerDefinitionId { get; init; }
    public required string AnswerKey { get; init; }
    public required string InputType { get; init; }
    public bool? ValueBoolean { get; init; }
    public string? ValueText { get; init; }
    public decimal? ValueNumber { get; init; }
    public int? SelectedOptionId { get; init; }
    public string? SelectedOptionValue { get; init; }
    public required List<int> SelectedOptionIds { get; init; }
    public required List<string> SelectedOptionValues { get; init; }
}

internal sealed class TaskTemplateRecord
{
    public required int Id { get; init; }
    public required string TemplateKey { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string IconKey { get; init; }
    public int? DefaultResponsibilityId { get; init; }
    public string? ProcessAreaLabel { get; init; }
    public required bool IsDepartmentPhaseTask { get; init; }
    public required bool IsRequired { get; init; }
    public int? DueInDays { get; init; }
    public required int SortOrder { get; init; }
}

internal sealed class TaskTemplateConditionRecord
{
    public required int TaskTemplateId { get; init; }
    public required int ConditionGroup { get; init; }
    public required string AnswerKey { get; init; }
    public required string Operator { get; init; }
    public string? ExpectedValueText { get; init; }
    public bool? ExpectedValueBoolean { get; init; }
    public decimal? ExpectedValueNumber { get; init; }
}

internal sealed class TaskTemplateDependencyRecord
{
    public required int TaskTemplateId { get; init; }
    public required int DependsOnTaskTemplateId { get; init; }
    public required string RequiredStatus { get; init; }
}

internal sealed class CreatedWorkflowTaskRecord
{
    public required int TaskTemplateId { get; init; }
    public required long WorkflowTaskId { get; init; }
    public required string TaskKey { get; init; }
}
