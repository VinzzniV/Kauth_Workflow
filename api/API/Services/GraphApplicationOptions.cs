namespace API;

public sealed class GraphApplicationOptions
{
    public const string SectionName = "GraphApplication";

    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
}
