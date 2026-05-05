using Microsoft.Extensions.Logging;

namespace API;

internal sealed class RotationNotificationService(
    IRotationRepository rotationRepository,
    IWorkflowEmailNotificationSender emailNotificationSender,
    ISystemEventLogService systemEventLogService,
    ILogger<RotationNotificationService> logger) : IRotationNotificationService
{
    private const int DispatchBatchSize = 200;

    public async Task<RotationNotificationSweepResult> ExecuteDailySweepAsync(CancellationToken cancellationToken = default)
    {
        var asOfDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await rotationRepository.CreateDueRotationNotifications(asOfDate);

        var processedIds = new HashSet<long>();
        var dispatched = 0;
        var failed = 0;
        var disabled = 0;
        var anyTargets = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await rotationRepository.GetDispatchableRotationNotifications(DispatchBatchSize, processedIds);
            if (batch.Count == 0)
            {
                break;
            }

            // Defensive: even if the repository did not honour the exclude set, never dispatch the same id twice in one sweep.
            var fresh = batch.Where(target => processedIds.Add(target.NotificationId)).ToList();
            if (fresh.Count == 0)
            {
                break;
            }

            anyTargets = true;
            var results = await emailNotificationSender.SendRotationNotificationsAsync(fresh, cancellationToken);
            if (results.Count > 0)
            {
                await rotationRepository.ApplyRotationNotificationDispatchResults(results);
            }

            dispatched += results.Count(result => string.Equals(result.Status, "sent", StringComparison.OrdinalIgnoreCase));
            failed += results.Count(result => string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase));
            disabled += results.Count(result => string.Equals(result.Status, "disabled", StringComparison.OrdinalIgnoreCase));

            if (batch.Count < DispatchBatchSize)
            {
                break;
            }
        }

        if (!anyTargets)
        {
            return new RotationNotificationSweepResult
            {
                Created = created,
                Dispatched = 0,
                Failed = 0,
                Disabled = 0
            };
        }

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
