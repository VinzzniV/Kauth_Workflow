namespace API;

internal interface INotificationEmailTestSender
{
    Task<NotificationEmailTestSendResult> SendTestEmailAsync(
        string recipientEmail,
        CancellationToken cancellationToken = default);
}
