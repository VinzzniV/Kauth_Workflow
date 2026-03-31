using Microsoft.Extensions.Options;

namespace API;

internal sealed class GraphApplicationConfigurationService : IGraphApplicationConfigurationService
{
    private readonly IGraphApplicationConfigurationRepository repository;
    private readonly GraphApplicationOptions defaults;

    public GraphApplicationConfigurationService(
        IGraphApplicationConfigurationRepository repository,
        IOptions<GraphApplicationOptions> defaults)
    {
        this.repository = repository;
        this.defaults = defaults.Value;
    }

    public async Task<AdminGraphApplicationConfigurationDto> GetAdminConfiguration(
        CancellationToken cancellationToken = default)
    {
        var stored = await repository.GetSettings(cancellationToken);
        return BuildAdminConfiguration(Merge(stored));
    }

    public async Task<AdminGraphApplicationConfigurationDto> SaveAdminConfiguration(
        AdminGraphApplicationConfigurationUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var existingSettings = await repository.GetSettings(cancellationToken);
        var clientSecret = Normalize(request.ClientSecret) ?? existingSettings?.ClientSecret;

        var stored = await repository.UpsertSettings(
            new GraphApplicationSettingsUpsertModel
            {
                TenantId = Normalize(request.TenantId),
                ClientId = Normalize(request.ClientId),
                ClientSecret = clientSecret
            },
            cancellationToken);

        return BuildAdminConfiguration(Merge(stored));
    }

    public async Task<GraphApplicationRuntimeConfiguration> GetRuntimeConfiguration(
        CancellationToken cancellationToken = default)
    {
        var stored = await repository.GetSettings(cancellationToken);
        return Merge(stored);
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
            UpdatedAt = runtime.UpdatedAt,
            ConfigurationStatus = validation.Status,
            ConfigurationMessage = validation.Message
        };
    }

    private GraphApplicationRuntimeConfiguration Merge(StoredGraphApplicationSettings? stored)
    {
        var tenantId = stored?.TenantId ?? Normalize(defaults.TenantId);
        var clientId = stored?.ClientId ?? Normalize(defaults.ClientId);
        var clientSecret = stored?.ClientSecret ?? Normalize(defaults.ClientSecret);

        return new GraphApplicationRuntimeConfiguration
        {
            TenantId = tenantId,
            ClientId = clientId,
            ClientSecret = clientSecret,
            HasClientSecret = !string.IsNullOrWhiteSpace(clientSecret),
            UpdatedAt = stored?.UpdatedAt
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
