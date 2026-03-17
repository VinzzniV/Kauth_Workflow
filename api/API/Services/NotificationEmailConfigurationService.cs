using Microsoft.Extensions.Options;

namespace API;

internal sealed class NotificationEmailConfigurationService : INotificationEmailConfigurationService
{
    private readonly INotificationEmailConfigurationRepository repository;
    private readonly NotificationEmailOptions defaults;

    public NotificationEmailConfigurationService(
        INotificationEmailConfigurationRepository repository,
        IOptions<NotificationEmailOptions> defaults)
    {
        this.repository = repository;
        this.defaults = defaults.Value;
    }

    public async Task<AdminNotificationEmailConfigurationDto> GetAdminConfiguration(CancellationToken cancellationToken = default)
    {
        var stored = await repository.GetSettings(cancellationToken);
        return await BuildAdminConfiguration(stored, cancellationToken);
    }

    public async Task<AdminNotificationEmailConfigurationDto> SaveAdminConfiguration(
        AdminNotificationEmailConfigurationUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var existingSettings = await repository.GetSettings(cancellationToken);
        var tenantId = Normalize(request.TenantId);
        var clientId = Normalize(request.ClientId);
        var clientSecret = Normalize(request.ClientSecret) ?? existingSettings?.ClientSecret;
        var senderEmail = Normalize(request.SenderEmail);
        var frontendBaseUrl = Normalize(request.FrontendBaseUrl) ?? defaults.FrontendBaseUrl;
        var testRecipientEmail = Normalize(request.TestRecipientEmail);

        if (!Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Frontend-Basis-URL muss eine absolute URL sein.");
        }

        if (senderEmail is not null && !NotificationEmailConfigurationValidator.IsValidEmail(senderEmail))
        {
            throw new InvalidOperationException("Sender-Mailadresse ist ungültig.");
        }

        if (testRecipientEmail is not null && !NotificationEmailConfigurationValidator.IsValidEmail(testRecipientEmail))
        {
            throw new InvalidOperationException("Testempfänger-Mailadresse ist ungültig.");
        }

        var runtimeCandidate = new NotificationEmailRuntimeConfiguration
        {
            Enabled = request.Enabled,
            Provider = defaults.Provider,
            TenantId = tenantId,
            ClientId = clientId,
            ClientSecret = clientSecret,
            SenderEmail = senderEmail,
            FrontendBaseUrl = frontendBaseUrl,
            TestRecipientEmail = testRecipientEmail,
            SaveToSentItems = defaults.SaveToSentItems,
            HasClientSecret = !string.IsNullOrWhiteSpace(clientSecret),
            LastTestStatus = existingSettings?.LastTestStatus ?? "never",
            LastTestAt = existingSettings?.LastTestAt,
            LastError = existingSettings?.LastError,
            UpdatedAt = existingSettings?.UpdatedAt
        };

        var enableValidation = NotificationEmailConfigurationValidator.ValidateForSending(runtimeCandidate);
        if (request.Enabled && !enableValidation.CanSend)
        {
            throw new InvalidOperationException(
                $"Mailversand kann nicht aktiviert werden: {enableValidation.Message}");
        }

        var stored = await repository.UpsertSettings(
            new NotificationEmailSettingsUpsertModel
            {
                Enabled = request.Enabled,
                TenantId = tenantId,
                ClientId = clientId,
                ClientSecret = clientSecret,
                SenderEmail = senderEmail,
                FrontendBaseUrl = frontendBaseUrl,
                TestRecipientEmail = testRecipientEmail
            },
            cancellationToken);

        return await BuildAdminConfiguration(stored, cancellationToken);
    }

    public async Task<AdminNotificationEmailConfigurationDto> UpdateTestStatus(
        string lastTestStatus,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        var stored = await repository.UpdateTestStatus(lastTestStatus, Normalize(lastError), cancellationToken);
        return await BuildAdminConfiguration(stored, cancellationToken);
    }

    public async Task<NotificationEmailRuntimeConfiguration> GetRuntimeConfiguration(CancellationToken cancellationToken = default)
    {
        var stored = await repository.GetSettings(cancellationToken);
        return Merge(stored);
    }

    private Task<AdminNotificationEmailConfigurationDto> BuildAdminConfiguration(
        StoredNotificationEmailSettings? stored,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtime = Merge(stored);
        var validation = NotificationEmailConfigurationValidator.ValidateForSending(runtime);

        return Task.FromResult(new AdminNotificationEmailConfigurationDto
        {
            Enabled = runtime.Enabled,
            Mode = runtime.Enabled ? "enabled" : "disabled",
            TenantId = runtime.TenantId,
            ClientId = runtime.ClientId,
            SenderEmail = runtime.SenderEmail,
            FrontendBaseUrl = runtime.FrontendBaseUrl,
            TestRecipientEmail = runtime.TestRecipientEmail,
            LastTestStatus = runtime.LastTestStatus,
            LastTestAt = runtime.LastTestAt,
            LastError = runtime.LastError,
            UpdatedAt = runtime.UpdatedAt,
            HasClientSecret = runtime.HasClientSecret,
            ConfigurationStatus = validation.Status,
            ConfigurationMessage = validation.Message
        });
    }

    private NotificationEmailRuntimeConfiguration Merge(StoredNotificationEmailSettings? stored)
    {
        var enabled = stored?.Enabled ?? defaults.Enabled;
        var tenantId = stored?.TenantId ?? Normalize(defaults.TenantId);
        var clientId = stored?.ClientId ?? Normalize(defaults.ClientId);
        var clientSecret = stored?.ClientSecret ?? Normalize(defaults.ClientSecret);
        var senderEmail = stored?.SenderEmail ?? Normalize(defaults.SenderUserId);
        var frontendBaseUrl = stored?.FrontendBaseUrl ?? defaults.FrontendBaseUrl;
        var testRecipientEmail = stored?.TestRecipientEmail;

        return new NotificationEmailRuntimeConfiguration
        {
            Enabled = enabled,
            Provider = defaults.Provider,
            TenantId = tenantId,
            ClientId = clientId,
            ClientSecret = clientSecret,
            SenderEmail = senderEmail,
            FrontendBaseUrl = frontendBaseUrl,
            TestRecipientEmail = testRecipientEmail,
            SaveToSentItems = defaults.SaveToSentItems,
            HasClientSecret = !string.IsNullOrWhiteSpace(clientSecret),
            LastTestStatus = stored?.LastTestStatus ?? "never",
            LastTestAt = stored?.LastTestAt,
            LastError = stored?.LastError,
            UpdatedAt = stored?.UpdatedAt
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
