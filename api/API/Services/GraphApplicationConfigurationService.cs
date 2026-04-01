namespace API;

internal sealed class GraphApplicationConfigurationService : IGraphApplicationConfigurationService
{
    private readonly LifecycleRuntimeSettings runtimeSettings;

    public GraphApplicationConfigurationService(LifecycleRuntimeSettings runtimeSettings)
    {
        this.runtimeSettings = runtimeSettings;
    }

    public Task<AdminGraphApplicationConfigurationDto> GetAdminConfiguration(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BuildAdminConfiguration(BuildRuntimeConfiguration()));
    }

    public Task<GraphApplicationRuntimeConfiguration> GetRuntimeConfiguration(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BuildRuntimeConfiguration());
    }

    private AdminGraphApplicationConfigurationDto BuildAdminConfiguration(
        GraphApplicationRuntimeConfiguration runtime)
    {
        var validation = GraphApplicationConfigurationValidator.Validate(runtime);
        return new AdminGraphApplicationConfigurationDto
        {
            TenantId = runtime.TenantId,
            ClientId = runtime.ClientId,
            HasClientSecret = runtime.HasClientSecret,
            UpdatedAt = null,
            ConfigurationSource = "runtime",
            ConfigurationStatus = validation.Status,
            ConfigurationMessage = validation.Message
        };
    }

    private GraphApplicationRuntimeConfiguration BuildRuntimeConfiguration()
    {
        var clientSecret = runtimeSettings.GraphClientSecret ?? runtimeSettings.EntraClientSecret;

        return new GraphApplicationRuntimeConfiguration
        {
            TenantId = runtimeSettings.EntraTenantId,
            ClientId = runtimeSettings.EntraClientId,
            ClientSecret = clientSecret,
            HasClientSecret = !string.IsNullOrWhiteSpace(clientSecret),
            UpdatedAt = null
        };
    }
}
