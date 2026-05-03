namespace API;

internal sealed class GraphApplicationRuntimeConfiguration
{
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public required bool HasClientSecret { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

