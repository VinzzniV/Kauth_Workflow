namespace API;

internal sealed class StoredNotificationTemplate
{
    public required string TemplateKey { get; init; }
    public required string DisplayName { get; init; }
    public required string TriggerDescription { get; init; }
    public required string SubjectTemplate { get; init; }
    public required string BodyTemplate { get; init; }
    public required bool IsSystemLocked { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

internal sealed class NotificationTemplateUpsertModel
{
    public required string TemplateKey { get; init; }
    public required string DisplayName { get; init; }
    public required string TriggerDescription { get; init; }
    public required string SubjectTemplate { get; init; }
    public required string BodyTemplate { get; init; }
    public required bool IsSystemLocked { get; init; }
}

internal interface INotificationTemplateRepository
{
    Task<List<StoredNotificationTemplate>> GetTemplates(CancellationToken cancellationToken = default);
    Task<StoredNotificationTemplate?> GetTemplate(string templateKey, CancellationToken cancellationToken = default);
    Task<StoredNotificationTemplate> UpsertTemplate(NotificationTemplateUpsertModel model, CancellationToken cancellationToken = default);
}

internal interface INotificationTemplatePreviewRepository
{
    Task<List<WorkflowNotificationDispatchTarget>> GetWorkflowCreatedPreviewTargets(Guid workflowUid);
    Task<List<WorkflowNotificationDispatchTarget>> GetTaskReadyPreviewTargets(Guid workflowUid);
    Task<List<WorkflowNotificationDispatchTarget>> GetWorkflowCompletedPreviewTargets(Guid workflowUid);
}

internal interface IRotationNotificationPreviewRepository
{
    Task<List<RotationNotificationDispatchTarget>> GetRotationNotificationPreviewTargets(
        long rotationPlanId,
        string notificationType,
        DateOnly asOfDate);
}
