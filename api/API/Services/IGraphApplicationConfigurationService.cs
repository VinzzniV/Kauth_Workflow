namespace API;

internal interface IGraphApplicationConfigurationService
{
    Task<AdminGraphApplicationConfigurationDto> GetAdminConfiguration(CancellationToken cancellationToken = default);
    Task<AdminGraphApplicationConfigurationDto> SaveAdminConfiguration(
        AdminGraphApplicationConfigurationUpdateRequest request,
        CancellationToken cancellationToken = default);
    Task<GraphApplicationRuntimeConfiguration> GetRuntimeConfiguration(CancellationToken cancellationToken = default);
}
