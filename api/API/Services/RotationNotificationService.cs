using Microsoft.Extensions.Logging;

namespace API;

internal sealed class RotationNotificationService(
    IRotationRepository rotationRepository,
    IWorkflowEmailNotificationSender emailNotificationSender,
    ISystemEventLogService systemEventLogService,
    ILogger<RotationNotificationService> logger) : IRotationNotificationService
{
    public async Task<RotationNotificationSweepResult> ExecuteDailySweepAsync(CancellationToken cancellationToken = default)
    {
        var asOfDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await rotationRepository.CreateDueRotationNotifications(asOfDate);
        var targets = await rotationRepository.GetDispatchableRotationNotifications();
        if (targets.Count == 0)
        {
            return new RotationNotificationSweepResult
            {
                Created = created,
                Dispatched = 0,
                Failed = 0,
                Disabled = 0
            };
        }

        var results = await emailNotificationSender.SendRotationNotificationsAsync(targets, cancellationToken);
        if (results.Count > 0)
        {
            await rotationRepository.ApplyRotationNotificationDispatchResults(results);
        }

        var failed = results.Count(result => string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase));
        var disabled = results.Count(result => string.Equals(result.Status, "disabled", StringComparison.OrdinalIgnoreCase));
        var dispatched = results.Count(result => string.Equals(result.Status, "sent", StringComparison.OrdinalIgnoreCase));

        logger.LogInformation(
            "Rotation notification sweep completed for {AsOfDate}: created={Created}, dispatched={Dispatched}, failed={Failed}, disabled={Disabled}.",
            asOfDate,
            created,
            dispatched,
            failed,
            disabled);

        await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
        {
            Severity = failed > 0 ? "warning" : "info",
            Source = "rotation",
            Category = "notifications",
            EventKey = failed > 0 ? "rotation_notification_sweep_partial_failure" : "rotation_notification_sweep_succeeded",
            Message = $"Rotation notification sweep finished: created={created}, dispatched={dispatched}, failed={failed}, disabled={disabled}.",
            Details = new { asOfDate, created, dispatched, failed, disabled }
        }, cancellationToken);

        return new RotationNotificationSweepResult
        {
            Created = created,
            Dispatched = dispatched,
            Failed = failed,
            Disabled = disabled
        };
    }
}
