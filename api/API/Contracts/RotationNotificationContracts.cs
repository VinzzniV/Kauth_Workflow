namespace API;

internal sealed class RotationNotificationTaskMailItem
{
    public required long GeneratedTaskId { get; init; }
    public required string TaskRef { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public DateOnly? DueDate { get; init; }
    public string? DepartmentName { get; init; }
}

internal sealed class RotationNotificationPayload
{
    public required string DedupeKey { get; init; }
    public required string RecipientName { get; init; }
    public required string PlanTitle { get; init; }
    public required Guid SourceWorkflowUid { get; init; }
    public required long PersonId { get; init; }
    public required string PersonDisplayName { get; init; }
    public string? CurrentDepartmentName { get; init; }
    public string? NextDepartmentName { get; init; }
    public DateOnly? ChangeDate { get; init; }
    public required string LinkPath { get; init; }
    public required List<RotationNotificationTaskMailItem> Tasks { get; init; }
}

internal sealed class RotationNotificationDispatchTarget
{
    public required long NotificationId { get; init; }
    public required string NotificationType { get; init; }
    public required long RotationPlanId { get; init; }
    public long? RotationStationId { get; init; }
    public long? GeneratedTaskId { get; init; }
    public long? RecipientUserId { get; init; }
    public required string TargetName { get; init; }
    public required string TargetEmail { get; init; }
    public required string Subject { get; init; }
    public required RotationNotificationPayload Payload { get; init; }
}

internal sealed class RotationNotificationSweepResult
{
    public required int Created { get; init; }
    public required int Dispatched { get; init; }
    public required int Failed { get; init; }
    public required int Disabled { get; init; }
}
