namespace API;

internal sealed class GraphApplicationRuntimeConfiguration
{
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public required bool HasClientSecret { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

internal sealed class GraphApplicationConfigurationValidationResult
{
    public required string Status { get; init; }
    public required bool IsConfigured { get; init; }
    public string? Message { get; init; }
}
