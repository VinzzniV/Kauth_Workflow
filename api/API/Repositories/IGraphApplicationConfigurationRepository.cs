namespace API;

internal interface IGraphApplicationConfigurationRepository
{
    Task<StoredGraphApplicationSettings?> GetSettings(CancellationToken cancellationToken = default);
    Task<StoredGraphApplicationSettings> UpsertSettings(
        GraphApplicationSettingsUpsertModel settings,
        CancellationToken cancellationToken = default);
}
