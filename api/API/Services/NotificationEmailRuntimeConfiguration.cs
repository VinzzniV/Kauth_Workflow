namespace API;

internal sealed class StoredNotificationEmailSettings
{
    public required bool Enabled { get; init; }
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? SenderEmail { get; init; }
    public required string FrontendBaseUrl { get; init; }
    public string? TestRecipientEmail { get; init; }
    public required string LastTestStatus { get; init; }
    public DateTime? LastTestAt { get; init; }
    public string? LastError { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

internal sealed class NotificationEmailSettingsUpsertModel
{
    public required bool Enabled { get; init; }
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? SenderEmail { get; init; }
    public required string FrontendBaseUrl { get; init; }
    public string? TestRecipientEmail { get; init; }
}

internal sealed class NotificationEmailRuntimeConfiguration
{
    public required bool Enabled { get; init; }
    public required string Provider { get; init; }
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? SenderEmail { get; init; }
    public required string FrontendBaseUrl { get; init; }
    public string? TestRecipientEmail { get; init; }
    public required bool SaveToSentItems { get; init; }
    public required bool HasClientSecret { get; init; }
    public required string LastTestStatus { get; init; }
    public DateTime? LastTestAt { get; init; }
    public string? LastError { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

internal sealed class NotificationEmailConfigurationValidationResult
{
    public required string Status { get; init; }
    public required bool CanSend { get; init; }
    public string? Message { get; init; }
}

internal sealed class NotificationEmailTestSendResult
{
    public required bool Success { get; init; }
    public required string Status { get; init; }
    public required string Message { get; init; }
    public required string RecipientEmail { get; init; }
    public string? ErrorMessage { get; init; }
}
