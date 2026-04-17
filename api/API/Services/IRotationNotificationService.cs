namespace API;

internal interface IRotationNotificationService
{
    Task<RotationNotificationSweepResult> ExecuteDailySweepAsync(CancellationToken cancellationToken = default);
}
