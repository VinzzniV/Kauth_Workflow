namespace API;

internal static class ConfigurationValidationStatus
{
    public const string Ready = "ready";
    public const string Incomplete = "incomplete";
    public const string Disabled = "disabled";
}

internal sealed class ConfigurationValidationResult
{
    public required string Status { get; init; }
    public string? Message { get; init; }
    public bool IsReady => Status == ConfigurationValidationStatus.Ready;
}
