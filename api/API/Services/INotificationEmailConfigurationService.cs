namespace API;

internal interface INotificationEmailConfigurationService
{
    Task<AdminNotificationEmailConfigurationDto> GetAdminConfiguration(CancellationToken cancellationToken = default);
    Task<AdminNotificationEmailConfigurationDto> SaveAdminConfiguration(
        AdminNotificationEmailConfigurationUpdateRequest request,
        CancellationToken cancellationToken = default);
    Task<AdminNotificationEmailConfigurationDto> UpdateTestStatus(
        string lastTestStatus,
        string? lastError,
        CancellationToken cancellationToken = default);
    Task<NotificationEmailRuntimeConfiguration> GetRuntimeConfiguration(CancellationToken cancellationToken = default);
}
