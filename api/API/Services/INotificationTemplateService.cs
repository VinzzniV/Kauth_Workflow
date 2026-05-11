namespace API;

internal sealed class NotificationTemplateRenderResult
{
    public required string Subject { get; init; }
    public required string TextBody { get; init; }
    public required string HtmlBody { get; init; }
    public required IReadOnlyDictionary<string, string> PlaceholderValues { get; init; }
}

internal sealed class WorkflowNotificationRenderContext
{
    public required string TemplateKey { get; init; }
    public required string RecipientName { get; init; }
    public required string WorkflowUrl { get; init; }
    public required string LegacyProcessTypeKey { get; init; }
    public required string ProcessTypeName { get; init; }
    public IReadOnlyList<string> TaskTitles { get; init; } = [];
}

internal sealed class RotationNotificationRenderContext
{
    public required string TemplateKey { get; init; }
    public required string RecipientName { get; init; }
    public required string AppUrl { get; init; }
    public required RotationNotificationPayload Payload { get; init; }
}

internal interface INotificationTemplateService
{
    Task<IReadOnlyList<AdminNotificationTemplateDto>> GetAdminTemplates(CancellationToken cancellationToken = default);
    Task<AdminNotificationTemplateDto> UpdateAdminTemplate(
        string templateKey,
        AdminNotificationTemplateUpdateRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminNotificationTemplateWorkflowPreviewTargetDto>> SearchWorkflowPreviewTargets(
        string? search,
        int limit,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminNotificationTemplateRotationPlanPreviewTargetDto>> SearchRotationPlanPreviewTargets(
        string? search,
        int limit,
        CancellationToken cancellationToken = default);
    Task<AdminNotificationTemplatePreviewResponseDto> BuildPreview(
        string templateKey,
        AdminNotificationTemplatePreviewRequest request,
        CancellationToken cancellationToken = default);
    Task<NotificationTemplateRenderResult> RenderWorkflowNotification(
        WorkflowNotificationRenderContext context,
        CancellationToken cancellationToken = default);
    Task<NotificationTemplateRenderResult> RenderRotationNotification(
        RotationNotificationRenderContext context,
        CancellationToken cancellationToken = default);
}
