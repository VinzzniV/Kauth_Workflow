namespace API;

internal interface INotificationEmailConfigurationRepository
{
    Task<StoredNotificationEmailSettings?> GetSettings(CancellationToken cancellationToken = default);
    Task<StoredNotificationEmailSettings> UpsertSettings(
        NotificationEmailSettingsUpsertModel settings,
        CancellationToken cancellationToken = default);
    Task<StoredNotificationEmailSettings> UpdateTestStatus(
        string lastTestStatus,
        string? lastError,
        CancellationToken cancellationToken = default);
}
