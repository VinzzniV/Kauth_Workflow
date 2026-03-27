namespace API;

// SECURITY LIMITATION: ClientSecret is stored as plaintext in the database.
// This is an accepted prototype limitation. Do NOT log, serialize to API responses,
// or store in caches. For production use, replace with a proper secret store (e.g. key vault).
internal sealed class StoredNotificationEmailSettings
{
    public required bool Enabled { get; init; }
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    // Plaintext secret – only use to pass to NotificationEmailRuntimeConfiguration for OAuth calls.
    public string? ClientSecret { get; init; }

    // Suppress secret in all string representations to prevent accidental logging.
    public override string ToString() =>
        $"StoredNotificationEmailSettings(Enabled={Enabled}, ClientId={ClientId}, HasSecret={ClientSecret is not null})";
    public string? SenderEmail { get; init; }
    public required string FrontendBaseUrl { get; init; }
    public string? TestRecipientEmail { get; init; }
    public string? SandboxRedirectEmail { get; init; }
    public required bool NotifyOnWorkflowCreated { get; init; }
    public required bool NotifyOnTaskReady { get; init; }
    public required bool NotifyOnWorkflowCompleted { get; init; }
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
    public string? SandboxRedirectEmail { get; init; }
    public required bool NotifyOnWorkflowCreated { get; init; }
    public required bool NotifyOnTaskReady { get; init; }
    public required bool NotifyOnWorkflowCompleted { get; init; }
}

internal sealed class NotificationEmailRuntimeConfiguration
{
    public required bool Enabled { get; init; }
    public required string Provider { get; init; }
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    // Plaintext OAuth secret – only accessed by GraphWorkflowEmailNotificationSender for token acquisition.
    public string? ClientSecret { get; init; }
    public string? SenderEmail { get; init; }
    public required string FrontendBaseUrl { get; init; }
    public string? TestRecipientEmail { get; init; }
    public string? SandboxRedirectEmail { get; init; }
    public required bool NotifyOnWorkflowCreated { get; init; }
    public required bool NotifyOnTaskReady { get; init; }
    public required bool NotifyOnWorkflowCompleted { get; init; }
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
