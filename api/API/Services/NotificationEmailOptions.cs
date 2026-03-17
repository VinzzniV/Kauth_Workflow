namespace API;

public sealed class NotificationEmailOptions
{
    public const string SectionName = "NotificationEmail";

    public bool Enabled { get; init; }
    public string Provider { get; init; } = "MicrosoftGraph";
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? SenderUserId { get; init; }
    public string FrontendBaseUrl { get; init; } = "http://localhost:5173";
    public bool SaveToSentItems { get; init; } = true;
}
