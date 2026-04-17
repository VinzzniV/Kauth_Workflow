using System.Text.Json;

namespace API;

public sealed class CreateRotationPlanRequest
{
    public required long PersonId { get; init; }
    public required Guid SourceWorkflowUid { get; init; }
    public string? Title { get; init; }
    public string? Status { get; init; }
}

public sealed class RotationPlanListItemDto
{
    public required long Id { get; init; }
    public required long PersonId { get; init; }
    public required Guid SourceWorkflowUid { get; init; }
    public required string DisplayName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public long? CreatedByUserId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required long StationCount { get; init; }
}

public sealed class RotationPlanDetailDto
{
    public required long Id { get; init; }
    public required long PersonId { get; init; }
    public required Guid SourceWorkflowUid { get; init; }
    public required string DisplayName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public long? CreatedByUserId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required List<RotationStationDto> Stations { get; set; }
}

public sealed class RotationStationUpsertRequest
{
    public required int DepartmentId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int OrderIndex { get; init; }
    public string? Location { get; init; }
    public string? Notes { get; init; }
    public string? Status { get; init; }
}

public sealed class RotationStationDto
{
    public required long Id { get; init; }
    public required long RotationPlanId { get; init; }
    public required int DepartmentId { get; init; }
    public required string DepartmentName { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required int OrderIndex { get; init; }
    public string? Location { get; init; }
    public string? Notes { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed class DepartmentActionTemplateDto
{
    public required int Id { get; init; }
    public required int DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public required string TriggerType { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string TaskType { get; init; }
    public int? DefaultResponsibilityId { get; init; }
    public string? DefaultResponsibilityName { get; init; }
    public required int DueOffsetDays { get; init; }
    public int? ReminderOffsetDays { get; init; }
    public required bool IsAutomatable { get; init; }
    public string? AutomationKey { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed class DepartmentActionTemplateUpsertRequest
{
    public required int DepartmentId { get; init; }
    public required string TriggerType { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string TaskType { get; init; }
    public int? DefaultResponsibilityId { get; init; }
    public required int DueOffsetDays { get; init; }
    public int? ReminderOffsetDays { get; init; }
    public bool IsAutomatable { get; init; }
    public string? AutomationKey { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class RotationGeneratedTaskDto
{
    public required long Id { get; init; }
    public required string TaskRef { get; init; }
    public required long RotationPlanId { get; init; }
    public long? RotationStationId { get; init; }
    public required long PersonId { get; init; }
    public required int DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? TemplateId { get; init; }
    public string? TemplateTitle { get; init; }
    public required string TriggerType { get; init; }
    public required DateOnly AnchorDate { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string TaskType { get; init; }
    public int? ResponsibilityId { get; init; }
    public string? ResponsibilityName { get; init; }
    public DateOnly? DueDate { get; init; }
    public required string Status { get; init; }
    public string? CompletionNote { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required List<WorkflowTaskAssignmentDto> Assignments { get; init; }
    public required List<WorkflowTaskCommentDto> Comments { get; init; }
}

public sealed class RotationTaskRegenerationResultDto
{
    public required int Created { get; init; }
    public required int Updated { get; init; }
    public required int Cancelled { get; init; }
    public required int Unchanged { get; init; }
}

public sealed class RotationNotificationDto
{
    public required long Id { get; init; }
    public required long RotationPlanId { get; init; }
    public long? RotationStationId { get; init; }
    public long? GeneratedTaskId { get; init; }
    public required string NotificationType { get; init; }
    public required string RecipientEmail { get; init; }
    public string? RecipientName { get; init; }
    public long? RecipientUserId { get; init; }
    public required string Subject { get; init; }
    public JsonElement? Payload { get; init; }
    public required string Status { get; init; }
    public required int Attempts { get; init; }
    public string? LastError { get; init; }
    public DateTime? SentAt { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed class RotationAuditEntryDto
{
    public required long Id { get; init; }
    public required long RotationPlanId { get; init; }
    public long? RotationStationId { get; init; }
    public long? GeneratedTaskId { get; init; }
    public long? ActorUserId { get; init; }
    public string? ActorUserName { get; init; }
    public required string EventType { get; init; }
    public JsonElement? OldValue { get; init; }
    public JsonElement? NewValue { get; init; }
    public string? Detail { get; init; }
    public required DateTime CreatedAt { get; init; }
}
